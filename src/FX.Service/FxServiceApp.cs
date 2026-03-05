using Microsoft.EntityFrameworkCore;
using MoneyMovement.Contracts.Dtos;

namespace FX.Service;

/// <summary>
/// Executes the FX conversion workflow: quote, accept, execute. This service interacts with the Ledger Service
/// to post double-entry entries when FX is executed (customer funds → FX pool NGN, then FX pool NGN → FX pool USD).
/// Execute is idempotent so that Temporal retries do not post duplicate ledger entries or double-fill.
/// </summary>
public class FxServiceApp
{
    private readonly FxDbContext _db;
    private readonly IEnumerable<IFxVenue> _venues;
    private readonly ILedgerClient _ledger;
    private readonly ILogger<FxServiceApp> _logger;

    public FxServiceApp(FxDbContext db, IEnumerable<IFxVenue> venues, ILedgerClient ledger, ILogger<FxServiceApp> logger)
    {
        _db = db;
        _venues = venues;
        _ledger = ledger;
        _logger = logger;
    }

    /// <summary>Gets a quote from the first available venue. Tries each venue in order; failure moves to next (no circuit breaker in this loop).</summary>
    public async Task<FxQuoteResponse> QuoteAsync(FxQuoteRequest request, CancellationToken cancellationToken = default)
    {
        IFxVenue? venue = null;
        FxQuote? quote = null;
        foreach (var v in _venues)
        {
            try
            {
                quote = await v.GetQuoteAsync(request.SourceCurrency, request.DestinationCurrency, request.SourceAmount, cancellationToken);
                venue = v;
                break;
            }
            catch { /* try next venue */ }
        }
        if (quote == null || venue == null)
            throw new InvalidOperationException("No FX venue available");
        var quoteId = Guid.NewGuid();
        var destAmount = request.SourceAmount * quote.Rate - quote.FeeAmount;
        _db.Quotes.Add(new QuoteRecord
        {
            QuoteId = quoteId,
            TransferId = request.TransferId,
            SourceCurrency = request.SourceCurrency,
            DestCurrency = request.DestinationCurrency,
            SourceAmount = request.SourceAmount,
            Rate = quote.Rate,
            FeeAmount = quote.FeeAmount,
            ExpiresAt = quote.ExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new FxQuoteResponse(quoteId, quote.Rate, quote.ExpiresAt, destAmount, quote.FeeAmount);
    }

    /// <summary>Accepts a quote for a transfer. Returns null if quote not found, wrong transfer, or expired.</summary>
    public async Task<FxAcceptResponse?> AcceptAsync(FxAcceptRequest request, CancellationToken cancellationToken = default)
    {
        var quote = await _db.Quotes.FindAsync(new object[] { request.QuoteId }, cancellationToken);
        if (quote == null || quote.TransferId != request.TransferId) return null;
        if (DateTimeOffset.UtcNow > quote.ExpiresAt) return null;
        var acceptedId = Guid.NewGuid();
        _db.AcceptedQuotes.Add(new AcceptedQuoteRecord { AcceptedQuoteId = acceptedId, TransferId = request.TransferId, QuoteId = request.QuoteId, ExpiresAt = quote.ExpiresAt });
        await _db.SaveChangesAsync(cancellationToken);
        return new FxAcceptResponse(acceptedId, quote.ExpiresAt);
    }

    /// <summary>
    /// Executes the FX conversion: fill at venue then post to ledger. Idempotent: same idempotency key returns
    /// success without re-posting. The ledger is the single source of truth; we post two double-entry moves
    /// (customer NGN → FX pool NGN, then FX pool NGN → FX pool USD) so balances stay consistent. Retries must
    /// pass the same idempotency key to avoid duplicate money movement.
    /// </summary>
    public async Task<FxExecuteResponse> ExecuteAsync(FxExecuteRequest request, string? idempotencyKey, string? correlationId, CancellationToken cancellationToken = default)
    {
        var key = idempotencyKey ?? Guid.NewGuid().ToString("N");
        var existing = await _db.IdempotencyRecords.FindAsync(new object[] { request.TransferId, "FX_EXECUTE", key }, cancellationToken);
        if (existing != null)
        {
            // Retry: return last result without posting again. Prevents duplicate ledger entries.
            var exec = await _db.AcceptedQuotes.FirstOrDefaultAsync(x => x.TransferId == request.TransferId, cancellationToken);
            var q = exec != null ? await _db.Quotes.FindAsync(new object[] { exec.QuoteId }, cancellationToken) : null;
            return new FxExecuteResponse(Guid.NewGuid(), "Filled", q?.SourceAmount ?? 0, q?.Rate ?? 0);
        }
        var accepted = await _db.AcceptedQuotes.FindAsync(new object[] { request.AcceptedQuoteId }, cancellationToken);
        if (accepted == null || accepted.TransferId != request.TransferId) return new FxExecuteResponse(Guid.Empty, "Invalid", 0, 0);
        var quote = await _db.Quotes.FindAsync(new object[] { accepted.QuoteId }, cancellationToken);
        if (quote == null || DateTimeOffset.UtcNow > quote.ExpiresAt) return new FxExecuteResponse(Guid.Empty, "Expired", 0, 0);
        FxFillResult? fill = null;
        foreach (var v in _venues)
        {
            try
            {
                fill = await v.ExecuteAsync(request.TransferId, quote.SourceAmount, quote.Rate, quote.SourceCurrency, quote.DestCurrency, cancellationToken);
                break;
            }
            catch { }
        }
        if (fill == null) return new FxExecuteResponse(Guid.Empty, "Failed", 0, 0);
        // Double-entry: (1) debit CUSTOMER_FUNDS_NGN, credit FX_POOL_NGN; (2) debit FX_POOL_NGN, credit FX_POOL_USD. Ledger enforces balance.
        var destAmount = quote.SourceAmount * quote.Rate - quote.FeeAmount;
        var post1 = new LedgerPostingRequest(request.TransferId, "FX_EXECUTED_1", new[]
        {
            new LedgerEntryDto("CUSTOMER_FUNDS_NGN", quote.SourceAmount, 0, quote.SourceCurrency),
            new LedgerEntryDto("FX_POOL_NGN", 0, quote.SourceAmount, quote.SourceCurrency)
        });
        await _ledger.PostAsync(post1, key + "-1", correlationId, cancellationToken);
        var post2 = new LedgerPostingRequest(request.TransferId, "FX_EXECUTED_2", new[]
        {
            new LedgerEntryDto("FX_POOL_NGN", quote.SourceAmount, 0, quote.SourceCurrency),
            new LedgerEntryDto("FX_POOL_USD", 0, destAmount, quote.DestCurrency)
        });
        await _ledger.PostAsync(post2, key + "-2", correlationId, cancellationToken);
        _db.IdempotencyRecords.Add(new IdempotencyRecord { TransferId = request.TransferId, OperationType = "FX_EXECUTE", IdempotencyKey = key, ProcessedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(cancellationToken);
        return new FxExecuteResponse(Guid.NewGuid(), fill.Status, fill.FilledAmount, fill.Rate);
    }
}

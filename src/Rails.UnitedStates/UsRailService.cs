using Microsoft.EntityFrameworkCore;
using MoneyMovement.Contracts.Dtos;

namespace Rails.UnitedStates;

/// <summary>
/// United States rail service: pays out USD. Routing is internal—orchestrator does not know which vendor is used.
/// Providers are tried in health order; circuit breaker deprioritizes repeatedly failing providers. Payout is
/// idempotent by key so Temporal or HTTP retries do not double-post to the ledger (USD_PAYOUT_RESERVE → USD_BANK).
/// </summary>
public class UsRailService
{
    private readonly UsRailDbContext _db;
    private readonly IEnumerable<IUnitedStatesPayoutProvider> _providers;
    private readonly ILedgerClient _ledger;
    private readonly ILogger<UsRailService> _logger;

    public UsRailService(UsRailDbContext db, IEnumerable<IUnitedStatesPayoutProvider> providers, ILedgerClient ledger, ILogger<UsRailService> logger)
    {
        _db = db;
        _providers = providers;
        _ledger = ledger;
        _logger = logger;
    }

    /// <summary>
    /// Pays out USD. Idempotent: same idempotency key returns cached response without calling provider or ledger again.
    /// Tries providers by health; on failure records failure and tries next. Posts to ledger only after successful payout—ledger is single source of truth.
    /// </summary>
    public async Task<UsPayoutResponse> PayoutAsync(UsPayoutRequest request, string? idempotencyKey, string? correlationId, CancellationToken cancellationToken = default)
    {
        var key = idempotencyKey ?? Guid.NewGuid().ToString("N");
        var existing = await _db.IdempotencyRecords.FindAsync(new object[] { key }, cancellationToken);
        if (existing != null)
        {
            var cached = System.Text.Json.JsonSerializer.Deserialize<UsPayoutResponse>(existing.ResponsePayload);
            if (cached != null) return cached;
        }
        var providersOrdered = await GetProvidersByHealthAsync(cancellationToken);
        foreach (var provider in providersOrdered)
        {
            try
            {
                var result = await provider.CreatePayoutAsync(request.TransferId, request.Amount, request.Currency, request.BankAccountRef, cancellationToken);
                await RecordSuccessAsync(provider.Name, cancellationToken);
                // Double-entry: debit USD_PAYOUT_RESERVE, credit USD_BANK. Reserve was created by orchestrator; this completes the movement.
                var ledgerRequest = new LedgerPostingRequest(
                    request.TransferId,
                    "USD_PAYOUT_SENT",
                    new[]
                    {
                        new LedgerEntryDto("USD_PAYOUT_RESERVE", request.Amount, 0, request.Currency),
                        new LedgerEntryDto("USD_BANK", 0, request.Amount, request.Currency)
                    });
                await _ledger.PostAsync(ledgerRequest, "us-payout-" + request.TransferId, correlationId, cancellationToken);
                var response = new UsPayoutResponse(result.ReferenceId, result.Status);
                _db.IdempotencyRecords.Add(new IdempotencyRecord { IdempotencyKey = key, ResponsePayload = System.Text.Json.JsonSerializer.Serialize(response), ProcessedAt = DateTimeOffset.UtcNow });
                await _db.SaveChangesAsync(cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} failed", provider.Name);
                await RecordFailureAsync(provider.Name, cancellationToken);
            }
        }
        return new UsPayoutResponse("", "Failed");
    }

    public async Task<UsPayoutResult?> GetTransactionAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        foreach (var p in _providers)
        {
            var status = await p.GetStatusAsync(referenceId, cancellationToken);
            if (status != null) return status;
        }
        return null;
    }

    /// <summary>Orders providers so healthy ones are tried first; circuit breaker marks unhealthy after 3 failures.</summary>
    private async Task<IReadOnlyList<IUnitedStatesPayoutProvider>> GetProvidersByHealthAsync(CancellationToken cancellationToken)
    {
        var health = await _db.ProviderHealth.ToListAsync(cancellationToken);
        foreach (var p in _providers)
        {
            if (health.All(x => x.ProviderName != p.Name))
            {
                _db.ProviderHealth.Add(new ProviderHealthRecord { ProviderName = p.Name });
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        return _providers.OrderBy(p => health.FirstOrDefault(h => h.ProviderName == p.Name)?.IsHealthy == false ? 1 : 0).ToList();
    }

    private async Task RecordSuccessAsync(string providerName, CancellationToken cancellationToken)
    {
        var h = await _db.ProviderHealth.FindAsync(new object[] { providerName }, cancellationToken);
        if (h != null) { h.ConsecutiveFailures = 0; h.IsHealthy = true; await _db.SaveChangesAsync(cancellationToken); }
    }

    /// <summary>Increments failure count; after 3 consecutive failures marks provider unhealthy (circuit breaker).</summary>
    private async Task RecordFailureAsync(string providerName, CancellationToken cancellationToken)
    {
        var h = await _db.ProviderHealth.FindAsync(new object[] { providerName }, cancellationToken);
        if (h == null) { h = new ProviderHealthRecord { ProviderName = providerName }; _db.ProviderHealth.Add(h); }
        h.ConsecutiveFailures++;
        if (h.ConsecutiveFailures >= 3) h.IsHealthy = false;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

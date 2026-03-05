using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MoneyMovement.Contracts.Dtos;

namespace Transfer.Orchestrator;

/// <summary>
/// Orchestrates the money transfer workflow: collect (Nigeria rail), FX quote/accept/execute, ledger reserve, US payout.
/// The orchestrator does not know which FX venues or rail providers are used—routing is inside each service. Each step
/// calls a downstream service; failures are handled by updating status to Failed or ManualReview. Downstream services
/// enforce idempotency (via keys or transfer id) so that if this flow is retried (e.g. by Temporal), duplicate money
/// movement is prevented. Steps must be safe to retry where they are idempotent downstream.
/// </summary>
public class TransferFlowService
{
    private readonly OrchestratorDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<TransferFlowService> _logger;

    public TransferFlowService(OrchestratorDbContext db, IHttpClientFactory http, ILogger<TransferFlowService> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    /// <summary>Creates a transfer record and starts the workflow asynchronously. Returns immediately with transfer id and status Created.</summary>
    /// <param name="correlationId">Used for tracing across services; forwarded to rails and ledger.</param>
    public async Task<CreateTransferResponse> CreateAndRunAsync(CreateTransferRequest request, string? correlationId, CancellationToken cancellationToken = default)
    {
        var transferId = Guid.NewGuid();
        var correlation = correlationId ?? Guid.NewGuid().ToString("N");
        _db.Transfers.Add(new TransferRecord
        {
            TransferId = transferId,
            UserId = request.UserId,
            RecipientId = request.RecipientId,
            SourceCurrency = request.SourceCurrency,
            SourceAmount = request.SourceAmount,
            DestinationCurrency = request.DestinationCurrency,
            Status = "Created",
            CorrelationId = correlation,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _ = Task.Run(() => RunFlowAsync(transferId, request, correlation), cancellationToken);
        return new CreateTransferResponse(transferId, "Created", correlation);
    }

    /// <summary>
    /// Workflow steps in order. Each step is a call to a downstream service. Failures set status to Failed or ManualReview.
    /// Retries: Nigeria collect, FX execute, ledger reserve, and US payout are idempotent downstream (by idempotency key or transfer id), so re-running from a step would not duplicate money movement if the orchestrator were driven by Temporal with the same transfer id.
    /// </summary>
    private async Task RunFlowAsync(Guid transferId, CreateTransferRequest request, string correlationId)
    {
        try
        {
            // Step 1: Collect NGN (Nigeria rail). Orchestrator does not know which provider is used; rail routes internally.
            await UpdateStatusAsync(transferId, "AwaitingSourceFunds");
            var ngClient = _http.CreateClient("RailsNigeria");
            ngClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Correlation-ID", correlationId);
            var collectReq = new NgCollectRequest(transferId, request.SourceAmount, request.SourceCurrency, "bank-ref-" + request.RecipientId);
            var collectRes = await ngClient.PostAsJsonAsync("ng/collect", collectReq);
            if (!collectRes.IsSuccessStatusCode) { await UpdateStatusAsync(transferId, "Failed"); return; }
            var collectResult = await collectRes.Content.ReadFromJsonAsync<NgCollectResponse>();
            if (collectResult?.Status != "Succeeded") { await UpdateStatusAsync(transferId, "Failed"); return; }
            await UpdateStatusAsync(transferId, "SourceConfirmed");

            // Step 2: FX quote and accept. FX service tries venues in order; orchestrator does not know which venue is used.
            await UpdateStatusAsync(transferId, "FxQuoted");
            var fxClient = _http.CreateClient("FX");
            var quoteReq = new FxQuoteRequest(transferId, request.SourceCurrency, request.DestinationCurrency, request.SourceAmount);
            var quoteRes = await fxClient.PostAsJsonAsync("fx/quote", quoteReq);
            if (!quoteRes.IsSuccessStatusCode) { await UpdateStatusAsync(transferId, "ManualReview"); return; }
            var quote = await quoteRes.Content.ReadFromJsonAsync<FxQuoteResponse>();
            if (quote == null) { await UpdateStatusAsync(transferId, "ManualReview"); return; }
            var acceptRes = await fxClient.PostAsJsonAsync("fx/accept", new FxAcceptRequest(transferId, quote.QuoteId));
            if (!acceptRes.IsSuccessStatusCode) { await UpdateStatusAsync(transferId, "ManualReview"); return; }
            var accept = await acceptRes.Content.ReadFromJsonAsync<FxAcceptResponse>();
            if (accept == null) { await UpdateStatusAsync(transferId, "ManualReview"); return; }

            // Step 3: Execute FX (posts to ledger). FX service is idempotent by key; ledger is single source of truth.
            await UpdateStatusAsync(transferId, "FxExecuted");
            var execRes = await fxClient.PostAsJsonAsync("fx/execute", new FxExecuteRequest(transferId, accept.AcceptedQuoteId));
            if (!execRes.IsSuccessStatusCode) { await UpdateStatusAsync(transferId, "ManualReview"); return; }
            var exec = await execRes.Content.ReadFromJsonAsync<FxExecuteResponse>();
            if (exec?.Status != "Filled" && exec?.Status != "Succeeded") { await UpdateStatusAsync(transferId, "ManualReview"); return; }

            // Step 4: Reserve USD for payout. Balance check happens in ledger; insufficient funds would fail here.
            await UpdateStatusAsync(transferId, "PayoutQueued");
            var ledgerClient = _http.CreateClient("Ledger");
            ledgerClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Correlation-ID", correlationId);
            var reserveReq = new LedgerReserveRequest(transferId, "FX_POOL_USD", "USD_PAYOUT_RESERVE", exec!.FilledAmount, request.DestinationCurrency);
            var reserveRes = await ledgerClient.PostAsJsonAsync("ledger/reserve", reserveReq);
            if (!reserveRes.IsSuccessStatusCode)
            {
                var err = await reserveRes.Content.ReadAsStringAsync();
                if (err.Contains("InsufficientFunds")) await UpdateStatusAsync(transferId, "ManualReview");
                else await UpdateStatusAsync(transferId, "ManualReview");
                return;
            }

            // Step 5: US payout. Rail routes to a provider; idempotent by key so retries do not double-pay.
            var usClient = _http.CreateClient("RailsUnitedStates");
            usClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Correlation-ID", correlationId);
            var payoutReq = new UsPayoutRequest(transferId, exec.FilledAmount, request.DestinationCurrency, "us-bank-" + request.RecipientId);
            var payoutRes = await usClient.PostAsJsonAsync("us/payout", payoutReq);
            if (!payoutRes.IsSuccessStatusCode) { await UpdateStatusAsync(transferId, "ManualReview"); return; }
            var payoutResult = await payoutRes.Content.ReadFromJsonAsync<UsPayoutResponse>();
            if (payoutResult?.Status != "Succeeded") { await UpdateStatusAsync(transferId, "ManualReview"); return; }

            await UpdateStatusAsync(transferId, "Completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transfer {TransferId} failed", transferId);
            await UpdateStatusAsync(transferId, "Failed");
        }
    }

    private async Task UpdateStatusAsync(Guid transferId, string status)
    {
        try
        {
            var t = await _db.Transfers.FindAsync(transferId);
            if (t != null) { t.Status = status; t.UpdatedAt = DateTimeOffset.UtcNow; await _db.SaveChangesAsync(); }
        }
        catch { /* ignore */ }
    }

    /// <summary>Returns transfer record by id. Used for status polling.</summary>
    public async Task<TransferDto?> GetAsync(Guid transferId, CancellationToken cancellationToken = default)
    {
        var t = await _db.Transfers.FindAsync(new object[] { transferId }, cancellationToken);
        if (t == null) return null;
        return new TransferDto(t.TransferId, t.UserId, t.RecipientId, t.SourceCurrency, t.SourceAmount, t.DestinationCurrency, t.Status, t.CorrelationId, t.CreatedAt, t.UpdatedAt);
    }
}

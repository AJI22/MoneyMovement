using Microsoft.EntityFrameworkCore;
using MoneyMovement.Contracts.Dtos;

namespace Rails.Nigeria;

/// <summary>
/// Nigeria rail service: collects NGN (inbound) and pays out NGN. Routing happens here—the orchestrator does not
/// know which vendor is used; it only calls this rail. We try providers in order of health; circuit breaker marks
/// a provider unhealthy after consecutive failures to prevent repeated calls to a failing vendor. Collect is idempotent
/// by key so retries do not double-post to the ledger.
/// </summary>
public class NgRailService
{
    private readonly NigeriaRailDbContext _db;
    private readonly IEnumerable<INigeriaPaymentProvider> _providers;
    private readonly ILedgerClient _ledger;
    private readonly ILogger<NgRailService> _logger;
    /// <summary>After this many consecutive failures, provider is marked unhealthy and deprioritized.</summary>
    private const int CircuitBreakerThreshold = 3;

    public NgRailService(NigeriaRailDbContext db, IEnumerable<INigeriaPaymentProvider> providers, ILedgerClient ledger, ILogger<NgRailService> logger)
    {
        _db = db;
        _providers = providers;
        _ledger = ledger;
        _logger = logger;
    }

    /// <summary>
    /// Collects NGN (customer funds in). Idempotent: same idempotency key returns cached response without calling provider or ledger again. Tries providers by health (healthy first); on failure records failure and tries next. Ledger is posted only after successful provider call—ledger remains single source of truth.
    /// </summary>
    public async Task<NgCollectResponse> CollectAsync(NgCollectRequest request, string? idempotencyKey, string? correlationId, CancellationToken cancellationToken = default)
    {
        var key = idempotencyKey ?? Guid.NewGuid().ToString("N");
        var existing = await _db.IdempotencyRecords.FindAsync(new object[] { key }, cancellationToken);
        if (existing != null)
        {
            // Retry: return cached result so we do not double-post to ledger or double-call provider.
            var cached = System.Text.Json.JsonSerializer.Deserialize<NgCollectResponse>(existing.ResponsePayload);
            if (cached != null) return cached;
        }
        // Provider routing: orchestrator does not know vendors; we choose order by health (circuit breaker state).
        var providersOrdered = await GetProvidersByHealthAsync(cancellationToken);
        foreach (var provider in providersOrdered)
        {
            try
            {
                var result = await provider.CreateCollectionAsync(request.TransferId, request.Amount, request.Currency, request.BankAccountRef, cancellationToken);
                await RecordSuccessAsync(provider.Name, cancellationToken);
                // Double-entry: NGN_BANK debit, CUSTOMER_FUNDS_NGN credit. Ledger is single source of truth.
                var ledgerRequest = new LedgerPostingRequest(
                    request.TransferId,
                    "NGN_RECEIVED",
                    new[]
                    {
                        new LedgerEntryDto("NGN_BANK", request.Amount, 0, request.Currency),
                        new LedgerEntryDto("CUSTOMER_FUNDS_NGN", 0, request.Amount, request.Currency)
                    });
                await _ledger.PostAsync(ledgerRequest, "ng-collect-" + request.TransferId, correlationId, cancellationToken);
                var response = new NgCollectResponse(result.ReferenceId, result.Status);
                _db.IdempotencyRecords.Add(new IdempotencyRecord { IdempotencyKey = key, ResponsePayload = System.Text.Json.JsonSerializer.Serialize(response), ProcessedAt = DateTimeOffset.UtcNow });
                await _db.SaveChangesAsync(cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} failed, trying next", provider.Name);
                await RecordFailureAsync(provider.Name, cancellationToken);
            }
        }
        _logger.LogWarning("All Nigeria providers failed");
        return new NgCollectResponse("", "Failed");
    }

    public async Task<NgCollectResponse?> GetTransactionAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        foreach (var p in _providers)
        {
            var status = await p.GetStatusAsync(referenceId, cancellationToken);
            if (status != null) return new NgCollectResponse(status.ReferenceId, status.Status);
        }
        return null;
    }

    /// <summary>Orders providers so healthy ones are tried first; unhealthy (circuit open) are tried last to avoid repeated failures.</summary>
    private async Task<IReadOnlyList<INigeriaPaymentProvider>> GetProvidersByHealthAsync(CancellationToken cancellationToken)
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
        var healthyFirst = _providers.OrderBy(p => health.FirstOrDefault(h => h.ProviderName == p.Name)?.IsHealthy == false ? 1 : 0).ToList();
        return healthyFirst;
    }

    private async Task RecordSuccessAsync(string providerName, CancellationToken cancellationToken)
    {
        var h = await _db.ProviderHealth.FindAsync(new object[] { providerName }, cancellationToken);
        if (h != null) { h.ConsecutiveFailures = 0; h.IsHealthy = true; await _db.SaveChangesAsync(cancellationToken); }
    }

    /// <summary>Increments failure count; after CircuitBreakerThreshold marks provider unhealthy so it is tried last.</summary>
    private async Task RecordFailureAsync(string providerName, CancellationToken cancellationToken)
    {
        var h = await _db.ProviderHealth.FindAsync(new object[] { providerName }, cancellationToken);
        if (h == null) { h = new ProviderHealthRecord { ProviderName = providerName }; _db.ProviderHealth.Add(h); }
        h.ConsecutiveFailures++;
        h.LastFailureAt = DateTimeOffset.UtcNow;
        if (h.ConsecutiveFailures >= CircuitBreakerThreshold) h.IsHealthy = false;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

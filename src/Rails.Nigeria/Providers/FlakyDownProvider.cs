namespace Rails.Nigeria.Providers;

/// <summary>Nigeria provider that fails randomly. Used to test rail fallback and circuit breaker: rail tries next provider and records failure so after threshold this provider is deprioritized.</summary>
public class FlakyDownProvider : INigeriaPaymentProvider
{
    private readonly Random _rnd = new();
    private readonly double _failureRate;
    private readonly int _latencyMs;

    public FlakyDownProvider(double failureRate = 0.5, int latencyMs = 5000)
    {
        _failureRate = failureRate;
        _latencyMs = latencyMs;
    }

    public string Name => "FlakyDown";
    public async Task<NgCollectionResult> CreateCollectionAsync(Guid transferId, decimal amount, string currency, string bankAccountRef, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_latencyMs, cancellationToken);
        if (_rnd.NextDouble() < _failureRate)
            throw new InvalidOperationException("Simulated provider down");
        return new NgCollectionResult(Guid.NewGuid().ToString("N"), "Succeeded", "ext-" + Guid.NewGuid().ToString("N")[..8]);
    }
    public async Task<NgPayoutResult> CreatePayoutAsync(Guid transferId, decimal amount, string currency, string bankAccountRef, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_latencyMs, cancellationToken);
        if (_rnd.NextDouble() < _failureRate)
            throw new InvalidOperationException("Simulated provider down");
        return new NgPayoutResult(Guid.NewGuid().ToString("N"), "Succeeded", "ext-" + Guid.NewGuid().ToString("N")[..8]);
    }
    public Task<NgCollectionResult?> GetStatusAsync(string referenceId, CancellationToken cancellationToken = default)
        => Task.FromResult<NgCollectionResult?>(new NgCollectionResult(referenceId, "Succeeded", referenceId));
}

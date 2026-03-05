namespace Rails.UnitedStates.Providers;

/// <summary>US payout provider that fails randomly. Tests rail fallback and circuit breaker: failures are recorded and provider is deprioritized after threshold.</summary>
public class FlakyDownProvider : IUnitedStatesPayoutProvider
{
    private readonly Random _rnd = new();
    private readonly double _failureRate;

    public FlakyDownProvider(double failureRate = 0.5)
    {
        _failureRate = failureRate;
    }
    public string Name => "FlakyDown";
    public Task<UsPayoutResult> CreatePayoutAsync(Guid transferId, decimal amount, string currency, string bankAccountRef, CancellationToken cancellationToken = default)
    {
        if (_rnd.NextDouble() < _failureRate)
            throw new InvalidOperationException("Simulated provider down");
        return Task.FromResult(new UsPayoutResult(Guid.NewGuid().ToString("N"), "Succeeded", "ext-" + Guid.NewGuid().ToString("N")[..8]));
    }
    public Task<UsPayoutResult?> GetStatusAsync(string referenceId, CancellationToken cancellationToken = default)
        => Task.FromResult<UsPayoutResult?>(new UsPayoutResult(referenceId, "Succeeded", referenceId));
}

namespace Rails.UnitedStates.Providers;

/// <summary>US payout provider that always succeeds. Rail tries providers in health order; this is typically first.</summary>
public class AlwaysSuccessProvider : IUnitedStatesPayoutProvider
{
    public string Name => "AlwaysSuccess";
    public Task<UsPayoutResult> CreatePayoutAsync(Guid transferId, decimal amount, string currency, string bankAccountRef, CancellationToken cancellationToken = default)
        => Task.FromResult(new UsPayoutResult(Guid.NewGuid().ToString("N"), "Succeeded", "ext-" + Guid.NewGuid().ToString("N")[..8]));
    public Task<UsPayoutResult?> GetStatusAsync(string referenceId, CancellationToken cancellationToken = default)
        => Task.FromResult<UsPayoutResult?>(new UsPayoutResult(referenceId, "Succeeded", referenceId));
}

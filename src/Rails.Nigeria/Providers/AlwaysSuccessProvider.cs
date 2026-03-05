namespace Rails.Nigeria.Providers;

/// <summary>Nigeria payment provider that always succeeds. Used as first choice; rail tries this before FlakyDown so fallback behavior can be tested.</summary>
public class AlwaysSuccessProvider : INigeriaPaymentProvider
{
    public string Name => "AlwaysSuccess";
    public Task<NgCollectionResult> CreateCollectionAsync(Guid transferId, decimal amount, string currency, string bankAccountRef, CancellationToken cancellationToken = default)
        => Task.FromResult(new NgCollectionResult(Guid.NewGuid().ToString("N"), "Succeeded", "ext-" + Guid.NewGuid().ToString("N")[..8]));
    public Task<NgPayoutResult> CreatePayoutAsync(Guid transferId, decimal amount, string currency, string bankAccountRef, CancellationToken cancellationToken = default)
        => Task.FromResult(new NgPayoutResult(Guid.NewGuid().ToString("N"), "Succeeded", "ext-" + Guid.NewGuid().ToString("N")[..8]));
    public Task<NgCollectionResult?> GetStatusAsync(string referenceId, CancellationToken cancellationToken = default)
        => Task.FromResult<NgCollectionResult?>(new NgCollectionResult(referenceId, "Succeeded", referenceId));
}

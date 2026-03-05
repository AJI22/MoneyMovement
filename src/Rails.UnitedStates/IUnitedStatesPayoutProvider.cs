namespace Rails.UnitedStates;

public record UsPayoutResult(string ReferenceId, string Status, string? ExternalReference = null);

/// <summary>
/// Abstraction for a US payout provider. The rail service routes to one of these; the orchestrator does not know
/// which provider is used. Providers are tried in health order; circuit breaker deprioritizes failing ones.
/// </summary>
public interface IUnitedStatesPayoutProvider
{
    string Name { get; }
    Task<UsPayoutResult> CreatePayoutAsync(Guid transferId, decimal amount, string currency, string bankAccountRef, CancellationToken cancellationToken = default);
    Task<UsPayoutResult?> GetStatusAsync(string referenceId, CancellationToken cancellationToken = default);
}

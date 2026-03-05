namespace Rails.Nigeria;

public record NgCollectionResult(string ReferenceId, string Status, string? ExternalReference = null);
public record NgPayoutResult(string ReferenceId, string Status, string? ExternalReference = null);

/// <summary>
/// Abstraction for a Nigeria payment provider (e.g. bank API, aggregator). The rail service routes requests
/// to one of these; the orchestrator does not know which provider is used. Implementations are tried in health order.
/// </summary>
public interface INigeriaPaymentProvider
{
    string Name { get; }
    Task<NgCollectionResult> CreateCollectionAsync(Guid transferId, decimal amount, string currency, string bankAccountRef, CancellationToken cancellationToken = default);
    Task<NgPayoutResult> CreatePayoutAsync(Guid transferId, decimal amount, string currency, string bankAccountRef, CancellationToken cancellationToken = default);
    Task<NgCollectionResult?> GetStatusAsync(string referenceId, CancellationToken cancellationToken = default);
}

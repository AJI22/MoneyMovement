namespace FX.Service;

public record FxQuote(decimal Rate, decimal FeeAmount, DateTimeOffset ExpiresAt);
public record FxFillResult(string Status, decimal FilledAmount, decimal Rate);

/// <summary>
/// Abstraction for an FX venue (e.g. internal pool, external provider). The FX Service tries venues in order;
/// if one fails, the next is tried. Implementations can add circuit breakers to avoid repeated calls to a failing venue.
/// </summary>
public interface IFxVenue
{
    string Name { get; }
    Task<FxQuote> GetQuoteAsync(string sourceCcy, string destCcy, decimal sourceAmount, CancellationToken cancellationToken = default);
    Task<FxFillResult> ExecuteAsync(Guid transferId, decimal sourceAmount, decimal rate, string sourceCcy, string destCcy, CancellationToken cancellationToken = default);
}

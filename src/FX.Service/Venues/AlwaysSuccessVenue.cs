namespace FX.Service.Venues;

/// <summary>FX venue that always returns a quote and fill. Used for testing and as first choice so other venues act as fallback.</summary>
public class AlwaysSuccessVenue : IFxVenue
{
    public string Name => "AlwaysSuccess";
    public Task<FxQuote> GetQuoteAsync(string sourceCcy, string destCcy, decimal sourceAmount, CancellationToken cancellationToken = default)
        => Task.FromResult(new FxQuote(1m / 1650m, 0m, DateTimeOffset.UtcNow.AddSeconds(60)));
    public Task<FxFillResult> ExecuteAsync(Guid transferId, decimal sourceAmount, decimal rate, string sourceCcy, string destCcy, CancellationToken cancellationToken = default)
        => Task.FromResult(new FxFillResult("Filled", sourceAmount * rate, rate));
}

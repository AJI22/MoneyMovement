namespace FX.Service.Venues;

/// <summary>FX venue that returns a partial fill (50%). Used to test partial-fill handling in the platform.</summary>
public class PartialFillVenue : IFxVenue
{
    public string Name => "PartialFill";
    public Task<FxQuote> GetQuoteAsync(string sourceCcy, string destCcy, decimal sourceAmount, CancellationToken cancellationToken = default)
        => Task.FromResult(new FxQuote(1m / 1650m, 0m, DateTimeOffset.UtcNow.AddSeconds(60)));
    public Task<FxFillResult> ExecuteAsync(Guid transferId, decimal sourceAmount, decimal rate, string sourceCcy, string destCcy, CancellationToken cancellationToken = default)
        => Task.FromResult(new FxFillResult("PartialFill", sourceAmount * rate * 0.5m, rate));
}

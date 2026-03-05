namespace FX.Service.Venues;

/// <summary>FX venue that fails randomly at a given rate. Used to test fallback: FX Service tries next venue instead of failing. In production, circuit breakers would avoid repeated calls to a failing venue.</summary>
public class FlakyDownVenue : IFxVenue
{
    private readonly Random _rnd = new();
    private readonly double _failureRate;
    public FlakyDownVenue(double failureRate = 0.5) => _failureRate = failureRate;
    public string Name => "FlakyDown";
    public Task<FxQuote> GetQuoteAsync(string sourceCcy, string destCcy, decimal sourceAmount, CancellationToken cancellationToken = default)
    {
        if (_rnd.NextDouble() < _failureRate) throw new InvalidOperationException("Venue down");
        return Task.FromResult(new FxQuote(1m / 1650m, 0m, DateTimeOffset.UtcNow.AddSeconds(60)));
    }
    public Task<FxFillResult> ExecuteAsync(Guid transferId, decimal sourceAmount, decimal rate, string sourceCcy, string destCcy, CancellationToken cancellationToken = default)
    {
        if (_rnd.NextDouble() < _failureRate) throw new InvalidOperationException("Venue down");
        return Task.FromResult(new FxFillResult("Filled", sourceAmount * rate, rate));
    }
}

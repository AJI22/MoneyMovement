using System.Net.Http.Json;

namespace Treasury.Worker;

/// <summary>
/// Background worker that checks USD liquidity (FX_POOL_USD balance) against a threshold. The ledger is the single
/// source of truth; this worker does not move funds—it only reads balance and alerts when below threshold so
/// treasury can top up. Prevents payout failures due to insufficient FX pool.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _ledger;
    private readonly IConfiguration _config;

    public Worker(ILogger<Worker> logger, IHttpClientFactory http, IConfiguration config)
    {
        _logger = logger;
        _ledger = http.CreateClient("Ledger");
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(_config.GetValue("Treasury:IntervalMinutes", 5));
        var threshold = _config.GetValue<decimal>("Treasury:UsdLiquidityThreshold", 100m);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var res = await _ledger.GetAsync("ledger/balances/FX_POOL_USD", stoppingToken);
                if (res.IsSuccessStatusCode)
                {
                    var balance = await res.Content.ReadFromJsonAsync<LedgerBalanceDto>(stoppingToken);
                    if (balance != null && balance.Balance < threshold)
                        _logger.LogWarning("Liquidity low: FX_POOL_USD={Balance}, Threshold={Threshold}", balance.Balance, threshold);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Treasury check failed");
            }
            await Task.Delay(interval, stoppingToken);
        }
    }
}

public record LedgerBalanceDto(string Account, string Currency, decimal Balance);

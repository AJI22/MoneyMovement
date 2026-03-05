using System.Net.Http.Json;

namespace Reconciliation.Worker;

/// <summary>
/// Background worker that periodically reconciles ledger balance (FX_POOL_USD) against an external source.
/// The ledger is the single source of truth for money; this worker detects mismatches (e.g. missing or duplicate postings)
/// by comparing ledger balance to a stub external view. In production the stub would be replaced by actual external system balance.
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
        var interval = TimeSpan.FromMinutes(_config.GetValue("Reconciliation:IntervalMinutes", 5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var balanceRes = await _ledger.GetAsync("ledger/balances/FX_POOL_USD", stoppingToken);
                if (balanceRes.IsSuccessStatusCode)
                {
                    var balance = await balanceRes.Content.ReadFromJsonAsync<LedgerBalanceDto>(stoppingToken);
                    var stubExternal = 0m;
                    if (balance != null && balance.Balance != stubExternal)
                        _logger.LogWarning("Reconciliation mismatch: Ledger FX_POOL_USD={Ledger}, Stub={Stub}", balance.Balance, stubExternal);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reconciliation run failed");
            }
            await Task.Delay(interval, stoppingToken);
        }
    }
}

public record LedgerBalanceDto(string Account, string Currency, decimal Balance);

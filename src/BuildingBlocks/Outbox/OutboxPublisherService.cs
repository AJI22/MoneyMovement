using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MoneyMovement.Outbox;

public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly Microsoft.Extensions.Logging.ILogger<OutboxPublisherService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;

    public OutboxPublisherService(IServiceProvider provider, Microsoft.Extensions.Logging.ILogger<OutboxPublisherService> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _provider.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                var messages = await store.GetUnpublishedAsync(BatchSize, stoppingToken);
                foreach (var msg in messages)
                {
                    try
                    {
                        await bus.PublishAsync(msg.EventType, msg.Payload, stoppingToken);
                        await store.MarkPublishedAsync(msg.Id, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Outbox publish failed for {Id}", msg.Id);
                        await store.MarkFailedAsync(msg.Id, ex.Message, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox publisher iteration failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace MoneyMovement.Outbox;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers in-memory bus and outbox publisher. Each service must register its own IOutboxStore (using its DbContext).
    /// </summary>
    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services, bool useInMemoryBus = true)
    {
        if (useInMemoryBus)
            services.AddSingleton<IMessageBus, InMemoryBus>();
        services.AddHostedService<OutboxPublisherService>();
        return services;
    }
}

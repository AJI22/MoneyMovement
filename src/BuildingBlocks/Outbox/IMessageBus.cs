namespace MoneyMovement.Outbox;

public interface IMessageBus
{
    Task PublishAsync(string eventType, string payload, CancellationToken cancellationToken = default);
}

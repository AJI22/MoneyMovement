namespace MoneyMovement.Outbox;

public interface IOutboxStore
{
    Task SaveAsync(string eventType, string payload, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetUnpublishedAsync(int batchSize, CancellationToken cancellationToken = default);
    Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default);
}

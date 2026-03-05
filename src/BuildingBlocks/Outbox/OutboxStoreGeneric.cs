using Microsoft.EntityFrameworkCore;

namespace MoneyMovement.Outbox;

public class OutboxStore<TContext> : IOutboxStore where TContext : DbContext
{
    private readonly TContext _db;

    public OutboxStore(TContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        var msg = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Set<OutboxMessage>().Add(msg);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnpublishedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        return await _db.Set<OutboxMessage>()
            .Where(x => x.PublishedAt == null)
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var msg = await _db.Set<OutboxMessage>().FindAsync(new object[] { id }, cancellationToken);
        if (msg != null)
        {
            msg.PublishedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        var msg = await _db.Set<OutboxMessage>().FindAsync(new object[] { id }, cancellationToken);
        if (msg != null)
        {
            msg.Error = error;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}

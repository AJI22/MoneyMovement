namespace MoneyMovement.Outbox;

public class InMemoryBus : IMessageBus
{
    private readonly List<Func<string, string, CancellationToken, Task>> _handlers = new();
    private readonly object _lock = new();

    public Task PublishAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        IEnumerable<Func<string, string, CancellationToken, Task>> handlers;
        lock (_lock)
        {
            handlers = _handlers.ToList();
        }
        return Task.WhenAll(handlers.Select(h => h(eventType, payload, cancellationToken)));
    }

    public void Subscribe(Func<string, string, CancellationToken, Task> handler)
    {
        lock (_lock)
        {
            _handlers.Add(handler);
        }
    }
}

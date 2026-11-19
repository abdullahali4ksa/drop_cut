using System.Collections.Concurrent;
using DropCut.Core.Contracts;

namespace DropCut.Core.Events;

/// <summary>
/// Thread-safe in-process domain event bus with isolated exception boundaries.
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Func<object, CancellationToken, Task>>> _handlers = new();

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        if (domainEvent is null)
        {
            return;
        }

        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var subscribers))
        {
            return;
        }

        List<Func<object, CancellationToken, Task>> snapshot;
        lock (subscribers)
        {
            snapshot = new List<Func<object, CancellationToken, Task>>(subscribers);
        }

        foreach (var handler in snapshot)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await handler(domainEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Isolate handler failures to preserve Zero-Crash stability across subscribers
                System.Diagnostics.Debug.WriteLine($"[EventBus Exception] {eventType.Name}: {ex.Message}");
            }
        }
    }

    public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class
    {
        if (handler is null)
        {
            return;
        }

        var eventType = typeof(TEvent);
        var subscribers = _handlers.GetOrAdd(eventType, _ => new List<Func<object, CancellationToken, Task>>());

        lock (subscribers)
        {
            subscribers.Add((evt, token) => handler((TEvent)evt, token));
        }
    }
}

namespace DropCut.Core.Contracts;

/// <summary>
/// In-process strongly-typed domain event bus for decoupled cross-module communication.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : class;

    void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class;
}

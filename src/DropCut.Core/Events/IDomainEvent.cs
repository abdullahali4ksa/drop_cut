namespace DropCut.Core.Events;

/// <summary>
/// Marker contract for all strongly-typed domain events across modules.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredOn { get; }
}

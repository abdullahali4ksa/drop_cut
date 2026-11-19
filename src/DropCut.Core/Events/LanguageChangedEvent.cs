namespace DropCut.Core.Events;

/// <summary>
/// Domain event published when the application language changes dynamically.
/// </summary>
public class LanguageChangedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    public string NewLanguageCode { get; }

    public bool IsRtl { get; }

    public LanguageChangedEvent(string newLanguageCode, bool isRtl)
    {
        NewLanguageCode = newLanguageCode;
        IsRtl = isRtl;
    }
}

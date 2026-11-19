using DropCut.Core.Domain;

namespace DropCut.Core.Events;

/// <summary>
/// Domain event published when a screen snip has been successfully captured.
/// </summary>
public class SnippetCapturedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    public Guid SnippetId { get; }

    public string Title { get; }

    public string FilePath { get; }

    public CaptureMode Mode { get; }

    public int Width { get; }

    public int Height { get; }

    public SnippetCapturedEvent(Guid snippetId, string title, string filePath, CaptureMode mode, int width, int height)
    {
        SnippetId = snippetId;
        Title = title;
        FilePath = filePath;
        Mode = mode;
        Width = width;
        Height = height;
    }
}

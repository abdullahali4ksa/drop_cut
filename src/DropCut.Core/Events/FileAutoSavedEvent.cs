namespace DropCut.Core.Events;

/// <summary>
/// Domain event published when the continuous auto-save worker successfully commits to disk.
/// </summary>
public class FileAutoSavedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    public Guid SnippetId { get; }

    public string SavedFilePath { get; }

    public long FileSizeBytes { get; }

    public FileAutoSavedEvent(Guid snippetId, string savedFilePath, long fileSizeBytes)
    {
        SnippetId = snippetId;
        SavedFilePath = savedFilePath;
        FileSizeBytes = fileSizeBytes;
    }
}

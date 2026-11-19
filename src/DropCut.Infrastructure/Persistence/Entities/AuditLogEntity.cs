namespace DropCut.Infrastructure.Persistence.Entities;

/// <summary>
/// Database entity for system and user activity audit records.
/// </summary>
public class AuditLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? SnippetId { get; set; }

    public int EventType { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? StateSnapshotJson { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string ActorRole { get; set; } = "User";
}

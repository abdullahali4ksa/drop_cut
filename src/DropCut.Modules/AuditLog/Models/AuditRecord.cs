namespace DropCut.Modules.AuditLog.Models;

/// <summary>
/// Domain model representing an audited system or user action.
/// </summary>
public class AuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string EventType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

namespace DropCut.Modules.Workspace.Models;

/// <summary>
/// Data model representing a requested screen capture.
/// </summary>
public class SnipRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int Mode { get; set; }

    public int DelaySeconds { get; set; }
}

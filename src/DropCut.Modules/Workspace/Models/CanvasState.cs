namespace DropCut.Modules.Workspace.Models;

/// <summary>
/// In-memory state of the active snippet editing canvas.
/// </summary>
public class CanvasState
{
    public Guid SnippetId { get; set; } = Guid.NewGuid();

    public byte[]? BaseImageBytes { get; set; }

    public int BaseWidth { get; set; }

    public int BaseHeight { get; set; }

    public double ZoomLevel { get; set; } = 1.0;

    public List<AnnotationStroke> Strokes { get; set; } = new();

    public List<AnnotationStroke> RedoStack { get; set; } = new();
}

using DropCut.Core.Domain;

namespace DropCut.Modules.Workspace.Models;

/// <summary>
/// Data model encapsulating an individual visual stroke, shape, text, or redaction blur.
/// </summary>
public class AnnotationStroke
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ToolType Tool { get; set; }

    public string ColorHex { get; set; } = "#FF0000";

    public double Thickness { get; set; } = 2.0;

    public string CoordinatesDataJson { get; set; } = string.Empty;

    public string? TextContent { get; set; }

    public double BlurRadius { get; set; } = 10.0;

    public int ZIndex { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

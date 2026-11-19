using DropCut.Core.Domain;

namespace DropCut.Modules.FileVault.Models;

/// <summary>
/// Value object detailing technical and file properties of a snippet.
/// </summary>
public class SnippetMetadata
{
    public int Width { get; set; }

    public int Height { get; set; }

    public double DpiScale { get; set; } = 1.0;

    public ExportFormat Format { get; set; } = ExportFormat.Png;

    public long FileSizeBytes { get; set; }

    public bool IsDraft { get; set; } = true;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

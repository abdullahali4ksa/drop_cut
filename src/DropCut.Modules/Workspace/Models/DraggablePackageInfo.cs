namespace DropCut.Modules.Workspace.Models;

/// <summary>
/// Encapsulates bitmap bytes and physical file path for native OLE Drag-and-Drop packaging.
/// </summary>
public class DraggablePackageInfo
{
    public Guid SnippetId { get; set; }

    public string LocalFilePath { get; set; } = string.Empty;

    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();

    public string SuggestedFileName { get; set; } = string.Empty;
}

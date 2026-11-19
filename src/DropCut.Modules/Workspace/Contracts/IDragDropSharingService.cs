using DropCut.Core.Domain;
using DropCut.Modules.Workspace.Models;

namespace DropCut.Modules.Workspace.Contracts;

/// <summary>
/// Contract for orchestrating native outward Drag-and-Drop operations to external apps and web browsers.
/// </summary>
public interface IDragDropSharingService
{
    Result<DraggablePackageInfo> PrepareDraggablePackage(Guid snippetId, byte[] renderedImageBytes, string localSavedFilePath);
}

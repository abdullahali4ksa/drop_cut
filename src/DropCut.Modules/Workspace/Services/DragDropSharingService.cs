using DropCut.Core.Domain;
using DropCut.Modules.Workspace.Contracts;
using DropCut.Modules.Workspace.Models;

namespace DropCut.Modules.Workspace.Services;

/// <summary>
/// Prepares and validates outward drag-and-drop packages for sharing images to external apps.
/// </summary>
public class DragDropSharingService : IDragDropSharingService
{
    public Result<DraggablePackageInfo> PrepareDraggablePackage(Guid snippetId, byte[] renderedImageBytes, string localSavedFilePath)
    {
        if (renderedImageBytes is null || renderedImageBytes.Length == 0)
        {
            return Result<DraggablePackageInfo>.Failure("Rendered image stream cannot be empty for drag-and-drop.");
        }

        var fileName = string.IsNullOrWhiteSpace(localSavedFilePath) 
            ? $"DropCut_Snippet_{DateTime.Now:yyyyMMdd_HHmmss}.png" 
            : Path.GetFileName(localSavedFilePath);

        var package = new DraggablePackageInfo
        {
            SnippetId = snippetId,
            ImageBytes = renderedImageBytes,
            LocalFilePath = localSavedFilePath,
            SuggestedFileName = fileName
        };

        return Result<DraggablePackageInfo>.Success(package);
    }
}

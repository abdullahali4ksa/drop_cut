using DropCut.Core.Domain;
using DropCut.Modules.Workspace.Models;

namespace DropCut.Modules.Workspace.Contracts;

/// <summary>
/// Contract for mutating and querying the interactive annotation canvas.
/// </summary>
public interface IAnnotationCanvasService
{
    void SetBaseImage(Guid snippetId, byte[] imageBytes, int width, int height);

    Task<Result> AddStrokeAsync(Guid snippetId, AnnotationStroke stroke, CancellationToken cancellationToken = default);

    Task<Result> UndoAsync(Guid snippetId, CancellationToken cancellationToken = default);

    Task<Result> RedoAsync(Guid snippetId, CancellationToken cancellationToken = default);

    Task<Result> ApplyCropAsync(Guid snippetId, PixelBounds cropBounds, CancellationToken cancellationToken = default);

    CanvasState? GetCanvasState(Guid snippetId);
}

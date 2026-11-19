using System.Collections.Concurrent;
using DropCut.Core.Contracts;
using DropCut.Core.Domain;
using DropCut.Core.Events;
using DropCut.Modules.Workspace.Contracts;
using DropCut.Modules.Workspace.Models;

namespace DropCut.Modules.Workspace.Services;

/// <summary>
/// Service maintaining interactive stroke history, undo/redo stacks, and crop bounds.
/// </summary>
public class AnnotationCanvasService : IAnnotationCanvasService
{
    private readonly IEventBus _eventBus;
    private readonly ConcurrentDictionary<Guid, CanvasState> _canvasStates = new();

    public AnnotationCanvasService(IEventBus eventBus)
    {
        _eventBus = Guard.AgainstNull(eventBus, nameof(eventBus));
    }

    public void SetBaseImage(Guid snippetId, byte[] imageBytes, int width, int height)
    {
        var state = _canvasStates.GetOrAdd(snippetId, id => new CanvasState { SnippetId = id });
        state.BaseImageBytes = imageBytes;
        state.BaseWidth = width;
        state.BaseHeight = height;
    }

    public async Task<Result> AddStrokeAsync(Guid snippetId, AnnotationStroke stroke, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(stroke, nameof(stroke));

        var state = _canvasStates.GetOrAdd(snippetId, id => new CanvasState { SnippetId = id });
        state.Strokes.Add(stroke);
        state.RedoStack.Clear();

        await _eventBus.PublishAsync(new ImageAnnotatedEvent(snippetId, stroke.Tool, stroke.CoordinatesDataJson), cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> UndoAsync(Guid snippetId, CancellationToken cancellationToken = default)
    {
        if (!_canvasStates.TryGetValue(snippetId, out var state) || state.Strokes.Count == 0)
        {
            return Result.Failure("Nothing to undo.");
        }

        var lastStroke = state.Strokes[^1];
        state.Strokes.RemoveAt(state.Strokes.Count - 1);
        state.RedoStack.Add(lastStroke);

        await _eventBus.PublishAsync(new ImageAnnotatedEvent(snippetId, lastStroke.Tool, "{\"action\":\"undo\"}"), cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> RedoAsync(Guid snippetId, CancellationToken cancellationToken = default)
    {
        if (!_canvasStates.TryGetValue(snippetId, out var state) || state.RedoStack.Count == 0)
        {
            return Result.Failure("Nothing to redo.");
        }

        var strokeToRestore = state.RedoStack[^1];
        state.RedoStack.RemoveAt(state.RedoStack.Count - 1);
        state.Strokes.Add(strokeToRestore);

        await _eventBus.PublishAsync(new ImageAnnotatedEvent(snippetId, strokeToRestore.Tool, "{\"action\":\"redo\"}"), cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ApplyCropAsync(Guid snippetId, PixelBounds cropBounds, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(cropBounds, nameof(cropBounds));

        if (!_canvasStates.TryGetValue(snippetId, out var state))
        {
            return Result.Failure("Snippet not found.");
        }

        state.BaseWidth = cropBounds.Width;
        state.BaseHeight = cropBounds.Height;

        var cropStroke = new AnnotationStroke
        {
            Tool = ToolType.Crop,
            CoordinatesDataJson = $"{{\"x\":{cropBounds.X},\"y\":{cropBounds.Y},\"w\":{cropBounds.Width},\"h\":{cropBounds.Height}}}"
        };
        state.Strokes.Add(cropStroke);

        await _eventBus.PublishAsync(new ImageAnnotatedEvent(snippetId, ToolType.Crop, cropStroke.CoordinatesDataJson), cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public CanvasState? GetCanvasState(Guid snippetId)
    {
        _canvasStates.TryGetValue(snippetId, out var state);
        return state;
    }
}

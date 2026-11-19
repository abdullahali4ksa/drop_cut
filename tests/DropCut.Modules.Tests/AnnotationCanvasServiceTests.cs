using DropCut.Core.Domain;
using DropCut.Core.Events;
using DropCut.Modules.Workspace.Models;
using DropCut.Modules.Workspace.Services;
using Xunit;

namespace DropCut.Modules.Tests;

public class AnnotationCanvasServiceTests
{
    [Fact]
    public async Task AddStroke_AndUndoRedo_ShouldUpdateCanvasStateAccurately()
    {
        var bus = new InMemoryEventBus();
        var service = new AnnotationCanvasService(bus);
        var snippetId = Guid.NewGuid();

        service.SetBaseImage(snippetId, new byte[] { 1, 2, 3 }, 800, 600);

        var stroke = new AnnotationStroke
        {
            Tool = ToolType.Pen,
            ColorHex = "#FF0000",
            Thickness = 2.0,
            CoordinatesDataJson = "[{\"x\":10,\"y\":20}]"
        };

        var addResult = await service.AddStrokeAsync(snippetId, stroke);
        Assert.True(addResult.IsSuccess);

        var state = service.GetCanvasState(snippetId);
        Assert.NotNull(state);
        Assert.Single(state.Strokes);

        var undoResult = await service.UndoAsync(snippetId);
        Assert.True(undoResult.IsSuccess);
        Assert.Empty(state.Strokes);
        Assert.Single(state.RedoStack);

        var redoResult = await service.RedoAsync(snippetId);
        Assert.True(redoResult.IsSuccess);
        Assert.Single(state.Strokes);
        Assert.Empty(state.RedoStack);
    }

    [Fact]
    public async Task ApplyCropAsync_ShouldAdjustDimensionsAndAddCropRecord()
    {
        var bus = new InMemoryEventBus();
        var service = new AnnotationCanvasService(bus);
        var snippetId = Guid.NewGuid();

        service.SetBaseImage(snippetId, new byte[] { 1 }, 1920, 1080);
        var cropBounds = new PixelBounds(50, 50, 640, 480);

        var cropResult = await service.ApplyCropAsync(snippetId, cropBounds);
        Assert.True(cropResult.IsSuccess);

        var state = service.GetCanvasState(snippetId);
        Assert.NotNull(state);
        Assert.Equal(640, state.BaseWidth);
        Assert.Equal(480, state.BaseHeight);
    }
}

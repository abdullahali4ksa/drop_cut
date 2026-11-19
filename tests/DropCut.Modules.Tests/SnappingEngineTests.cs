using DropCut.Core.Domain;
using DropCut.Core.Events;
using DropCut.Modules.Workspace.Models;
using DropCut.Modules.Workspace.Services;
using Xunit;

namespace DropCut.Modules.Tests;

public class SnappingEngineTests
{
    [Fact]
    public async Task CaptureDesktopAsync_ShouldPublishSnippetCapturedEventAndReturnBytes()
    {
        var bus = new InMemoryEventBus();
        var engine = new SnappingEngine(bus);
        var snippetId = Guid.NewGuid();
        var eventFired = false;

        bus.Subscribe<SnippetCapturedEvent>((evt, token) =>
        {
            if (evt.SnippetId == snippetId)
            {
                eventFired = true;
            }
            return Task.CompletedTask;
        });

        var request = new SnipRequest
        {
            Id = snippetId,
            Mode = CaptureMode.Rectangular,
            Bounds = new PixelBounds(100, 100, 500, 400),
            DelaySeconds = 0
        };

        var result = await engine.CaptureDesktopAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value);
        Assert.True(eventFired);
    }
}

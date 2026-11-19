using DropCut.Core.Domain;
using DropCut.Core.Events;
using Xunit;

namespace DropCut.Core.Tests;

public class EventBusTests
{
    [Fact]
    public async Task PublishAsync_ShouldInvokeSubscribedHandler()
    {
        var bus = new InMemoryEventBus();
        var capturedEventReceived = false;
        var snippetId = Guid.NewGuid();

        bus.Subscribe<SnippetCapturedEvent>((evt, token) =>
        {
            if (evt.SnippetId == snippetId)
            {
                capturedEventReceived = true;
            }
            return Task.CompletedTask;
        });

        var evt = new SnippetCapturedEvent(snippetId, "Test Snip", "C:\\temp\\snip.png", CaptureMode.Rectangular, 800, 600);
        await bus.PublishAsync(evt);

        Assert.True(capturedEventReceived);
    }

    [Fact]
    public async Task PublishAsync_WhenHandlerFails_ShouldNotThrowException()
    {
        var bus = new InMemoryEventBus();
        var secondHandlerInvoked = false;

        bus.Subscribe<FileAutoSavedEvent>((evt, token) =>
        {
            throw new InvalidOperationException("Simulated crash in subscriber");
        });

        bus.Subscribe<FileAutoSavedEvent>((evt, token) =>
        {
            secondHandlerInvoked = true;
            return Task.CompletedTask;
        });

        var evt = new FileAutoSavedEvent(Guid.NewGuid(), "C:\\temp\\auto.png", 1024);
        await bus.PublishAsync(evt);

        Assert.True(secondHandlerInvoked);
    }
}

using DropCut.Core.Domain;
using DropCut.Core.Events;
using DropCut.Modules.AuditLog.Services;
using Xunit;

namespace DropCut.Modules.Tests;

public class AuditLogServiceTests
{
    [Fact]
    public async Task AuditLogService_WhenDomainEventsFire_ShouldRecordEntries()
    {
        var bus = new InMemoryEventBus();
        var service = new AuditLogService(bus);
        var snippetId = Guid.NewGuid();

        await bus.PublishAsync(new SnippetCapturedEvent(snippetId, "Capture 1", "C:\\temp\\1.png", CaptureMode.Rectangular, 1920, 1080));
        await bus.PublishAsync(new ImageAnnotatedEvent(snippetId, ToolType.Highlighter, "{}"));
        await bus.PublishAsync(new FileAutoSavedEvent(snippetId, "C:\\temp\\1.png", 4096));

        var logs = await service.GetRecentLogsAsync(10);

        Assert.Equal(3, logs.Count);
        Assert.Contains(logs, l => l.EventType == "SnippetCaptured");
        Assert.Contains(logs, l => l.EventType == "ImageAnnotated");
        Assert.Contains(logs, l => l.EventType == "FileAutoSaved");
    }
}

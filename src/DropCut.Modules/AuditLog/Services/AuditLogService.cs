using System.Collections.Concurrent;
using DropCut.Core.Contracts;
using DropCut.Core.Events;
using DropCut.Modules.AuditLog.Contracts;
using DropCut.Modules.AuditLog.Models;

namespace DropCut.Modules.AuditLog.Services;

/// <summary>
/// Audit trail service capturing lifecycle actions, undo/redo states, and compliance logs.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly ConcurrentQueue<AuditRecord> _auditHistory = new();
    private const int MaxHistoryCount = 1000;

    public AuditLogService(IEventBus eventBus)
    {
        if (eventBus is not null)
        {
            eventBus.Subscribe<SnippetCapturedEvent>(OnSnippetCapturedAsync);
            eventBus.Subscribe<ImageAnnotatedEvent>(OnImageAnnotatedAsync);
            eventBus.Subscribe<FileAutoSavedEvent>(OnFileAutoSavedAsync);
        }
    }

    public Task RecordEventAsync(string eventType, string description, CancellationToken cancellationToken = default)
    {
        var record = new AuditRecord
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Description = description,
            Timestamp = DateTime.UtcNow
        };

        _auditHistory.Enqueue(record);

        while (_auditHistory.Count > MaxHistoryCount)
        {
            _auditHistory.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditRecord>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default)
    {
        var logs = _auditHistory
            .OrderByDescending(r => r.Timestamp)
            .Take(count)
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditRecord>>(logs);
    }

    private Task OnSnippetCapturedAsync(SnippetCapturedEvent evt, CancellationToken token)
    {
        return RecordEventAsync("SnippetCaptured", $"Captured snippet '{evt.Title}' ({evt.Width}x{evt.Height}) mode {evt.Mode}", token);
    }

    private Task OnImageAnnotatedAsync(ImageAnnotatedEvent evt, CancellationToken token)
    {
        return RecordEventAsync("ImageAnnotated", $"Applied tool {evt.AppliedTool} on snippet {evt.SnippetId}", token);
    }

    private Task OnFileAutoSavedAsync(FileAutoSavedEvent evt, CancellationToken token)
    {
        return RecordEventAsync("FileAutoSaved", $"Auto-saved snippet {evt.SnippetId} to '{evt.SavedFilePath}' ({evt.FileSizeBytes} bytes)", token);
    }
}

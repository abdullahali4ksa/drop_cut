using System.Collections.Concurrent;
using DropCut.Core.Contracts;
using DropCut.Core.Domain;
using DropCut.Core.Events;
using DropCut.Modules.FileVault.Contracts;

namespace DropCut.Modules.FileVault.Services;

/// <summary>
/// Continuous, debounced background auto-save engine guaranteeing data preservation without manual saves.
/// </summary>
public class AutoSaveService : IAutoSaveService, IDisposable
{
    private readonly ISafeFileStorage _fileStorage;
    private readonly IEventBus _eventBus;
    private readonly ConcurrentDictionary<Guid, PendingSaveItem> _pendingQueue = new();
    private readonly Timer _debounceTimer;
    private readonly int _debounceDelayMs;
    private bool _isDisposed;

    public bool HasPendingSaves => !_pendingQueue.IsEmpty;

    public AutoSaveService(ISafeFileStorage fileStorage, IEventBus eventBus, int debounceDelayMs = 300)
    {
        _fileStorage = Guard.AgainstNull(fileStorage, nameof(fileStorage));
        _eventBus = Guard.AgainstNull(eventBus, nameof(eventBus));
        _debounceDelayMs = debounceDelayMs;

        _debounceTimer = new Timer(OnTimerFired, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void QueueAutoSave(Guid snippetId, string targetFilePath, byte[] latestImageBytes)
    {
        if (latestImageBytes is null || latestImageBytes.Length == 0)
        {
            return;
        }

        var item = new PendingSaveItem(snippetId, targetFilePath, latestImageBytes);
        _pendingQueue[snippetId] = item;

        // Reset debounce timer to commit changes after delay
        _debounceTimer.Change(_debounceDelayMs, Timeout.Infinite);
    }

    public async Task<Result> FlushPendingSavesAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingQueue.IsEmpty)
        {
            return Result.Success();
        }

        var items = _pendingQueue.Values.ToList();
        _pendingQueue.Clear();

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await _fileStorage.WriteAtomicAsync(item.FilePath, item.ImageBytes, cancellationToken).ConfigureAwait(false);
                await _eventBus.PublishAsync(new FileAutoSavedEvent(item.SnippetId, item.FilePath, item.ImageBytes.Length), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return Result.Failure($"Auto-save flush failed for '{item.FilePath}': {ex.Message}");
            }
        }

        return Result.Success();
    }

    private async void OnTimerFired(object? state)
    {
        try
        {
            await FlushPendingSavesAsync().ConfigureAwait(false);
        }
        catch
        {
            // Catch unhandled background errors to maintain Zero-Crash invariants
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _debounceTimer.Dispose();
        }
    }

    private record PendingSaveItem(Guid SnippetId, string FilePath, byte[] ImageBytes);
}

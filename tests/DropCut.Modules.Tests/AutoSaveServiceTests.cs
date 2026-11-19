using DropCut.Core.Events;
using DropCut.Infrastructure.FileSystem;
using DropCut.Modules.FileVault.Services;
using Xunit;

namespace DropCut.Modules.Tests;

public class AutoSaveServiceTests : IDisposable
{
    private readonly SafeFileStorageService _storage;
    private readonly InMemoryEventBus _eventBus;
    private readonly string _testDir;

    public AutoSaveServiceTests()
    {
        _storage = new SafeFileStorageService();
        _eventBus = new InMemoryEventBus();
        _testDir = Path.Combine(Path.GetTempPath(), $"dropcut_autosave_{Guid.NewGuid()}");
        _storage.EnsureDirectory(_testDir);
    }

    [Fact]
    public async Task QueueAutoSave_AndFlush_ShouldCommitFileAndPublishEvent()
    {
        using var autoSaveService = new AutoSaveService(_storage, _eventBus, debounceDelayMs: 50);

        var eventFired = false;
        var snippetId = Guid.NewGuid();
        var targetFile = Path.Combine(_testDir, "snippet_auto.png");
        var testBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }; // PNG header

        _eventBus.Subscribe<FileAutoSavedEvent>((evt, token) =>
        {
            if (evt.SnippetId == snippetId)
            {
                eventFired = true;
            }
            return Task.CompletedTask;
        });

        autoSaveService.QueueAutoSave(snippetId, targetFile, testBytes);
        Assert.True(autoSaveService.HasPendingSaves);

        var flushResult = await autoSaveService.FlushPendingSavesAsync();
        Assert.True(flushResult.IsSuccess);
        Assert.False(autoSaveService.HasPendingSaves);

        Assert.True(File.Exists(targetFile));
        Assert.True(eventFired);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, recursive: true);
            }
            catch
            {
            }
        }
    }
}

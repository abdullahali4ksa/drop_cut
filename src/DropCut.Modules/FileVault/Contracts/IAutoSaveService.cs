using DropCut.Core.Domain;

namespace DropCut.Modules.FileVault.Contracts;

/// <summary>
/// Contract for managing debounced, non-blocking auto-saving of edited snippets.
/// </summary>
public interface IAutoSaveService
{
    bool HasPendingSaves { get; }

    void QueueAutoSave(Guid snippetId, string targetFilePath, byte[] latestImageBytes);

    Task<Result> FlushPendingSavesAsync(CancellationToken cancellationToken = default);
}

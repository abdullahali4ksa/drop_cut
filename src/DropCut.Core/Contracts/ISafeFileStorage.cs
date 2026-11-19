namespace DropCut.Core.Contracts;

/// <summary>
/// Core contract for atomic file persistence and non-locking concurrent reads.
/// </summary>
public interface ISafeFileStorage
{
    Task WriteAtomicAsync(string destinationPath, byte[] content, CancellationToken cancellationToken = default);

    Task<byte[]> ReadSafeAsync(string sourcePath, CancellationToken cancellationToken = default);

    void EnsureDirectory(string path);

    void DeleteSafe(string path);
}

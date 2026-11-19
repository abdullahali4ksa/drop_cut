namespace DropCut.Infrastructure.FileSystem;

/// <summary>
/// Safe file storage adapter enforcing non-locking reads and atomic staging writes.
/// </summary>
public class SafeFileStorageService
{
    public async Task WriteAtomicAsync(string destinationPath, byte[] content, CancellationToken cancellationToken = default)
    {
        var tempPath = destinationPath + ".tmp";
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to temporary staging file first
        await File.WriteAllBytesAsync(tempPath, content, cancellationToken).ConfigureAwait(false);

        // Atomic move/replace ensures no corrupted or truncated files if power is interrupted
        File.Move(tempPath, destinationPath, overwrite: true);
    }

    public async Task<byte[]> ReadSafeAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Target file does not exist.", sourcePath);
        }

        // Open with non-locking FileShare.ReadWrite so concurrent tools or sync apps do not crash DropCut
        using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        return memoryStream.ToArray();
    }

    public void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public void DeleteSafe(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // File locked by external process; do not crash
            }
        }
    }
}

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

        await File.WriteAllBytesAsync(tempPath, content, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, destinationPath, overwrite: true);
    }
}

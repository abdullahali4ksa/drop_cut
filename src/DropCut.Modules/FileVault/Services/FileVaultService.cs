using DropCut.Core.Contracts;
using DropCut.Core.Domain;
using DropCut.Modules.FileVault.Contracts;
using DropCut.Modules.FileVault.Helpers;
using DropCut.Modules.FileVault.Models;

namespace DropCut.Modules.FileVault.Services;

/// <summary>
/// File vault coordinator responsible for path resolution, safe persistence, and recent captures.
/// </summary>
public class FileVaultService : IFileVaultService
{
    private readonly ISafeFileStorage _fileStorage;
    private readonly string _capturesDirectory;
    private readonly string _draftsDirectory;

    public FileVaultService(ISafeFileStorage fileStorage)
    {
        _fileStorage = Guard.AgainstNull(fileStorage, nameof(fileStorage));

        var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        _capturesDirectory = Path.Combine(picturesPath, "DropCut");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _draftsDirectory = Path.Combine(localAppData, "DropCut", "Drafts");

        _fileStorage.EnsureDirectory(_capturesDirectory);
        _fileStorage.EnsureDirectory(_draftsDirectory);
    }

    public async Task<Result<VaultFile>> AutoSaveAsync(byte[] imageBytes, string title, CancellationToken cancellationToken = default)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return Result<VaultFile>.Failure("Image bytes cannot be empty.");
        }

        try
        {
            var fileName = string.IsNullOrWhiteSpace(title) 
                ? FileNamingHelper.GenerateDefaultFileName() 
                : FileNamingHelper.SanitizeFileName(title);

            var fullPath = Path.Combine(_capturesDirectory, fileName);
            await _fileStorage.WriteAtomicAsync(fullPath, imageBytes, cancellationToken).ConfigureAwait(false);

            var vaultFile = new VaultFile
            {
                Id = Guid.NewGuid(),
                Title = Path.GetFileNameWithoutExtension(fileName),
                FilePath = fullPath,
                CreatedAt = DateTime.UtcNow
            };

            return Result<VaultFile>.Success(vaultFile);
        }
        catch (Exception ex)
        {
            return Result<VaultFile>.Failure($"Failed to auto-save file: {ex.Message}");
        }
    }
}

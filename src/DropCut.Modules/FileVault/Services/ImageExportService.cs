using DropCut.Core.Contracts;
using DropCut.Core.Domain;
using DropCut.Modules.FileVault.Contracts;
using DropCut.Modules.FileVault.Helpers;

namespace DropCut.Modules.FileVault.Services;

/// <summary>
/// Service for exporting rendered snippets to specified external file formats.
/// </summary>
public class ImageExportService : IImageExportService
{
    private readonly ISafeFileStorage _fileStorage;

    public ImageExportService(ISafeFileStorage fileStorage)
    {
        _fileStorage = Guard.AgainstNull(fileStorage, nameof(fileStorage));
    }

    public async Task<Result<string>> ExportImageAsync(byte[] imageBytes, string destinationPath, ExportFormat format, CancellationToken cancellationToken = default)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return Result<string>.Failure("Image content cannot be empty.");
        }

        try
        {
            var ext = FileNamingHelper.GetFileExtension(format);
            var finalPath = destinationPath;
            if (!finalPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                finalPath = Path.ChangeExtension(finalPath, ext);
            }

            await _fileStorage.WriteAtomicAsync(finalPath, imageBytes, cancellationToken).ConfigureAwait(false);
            return Result<string>.Success(finalPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Export failed: {ex.Message}");
        }
    }
}

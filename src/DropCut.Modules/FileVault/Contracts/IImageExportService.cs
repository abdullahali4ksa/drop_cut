using DropCut.Core.Domain;

namespace DropCut.Modules.FileVault.Contracts;

/// <summary>
/// Contract for multi-format image serialization and export.
/// </summary>
public interface IImageExportService
{
    Task<Result<string>> ExportImageAsync(byte[] imageBytes, string destinationPath, ExportFormat format, CancellationToken cancellationToken = default);
}

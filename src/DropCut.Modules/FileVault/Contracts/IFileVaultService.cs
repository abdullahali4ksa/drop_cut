using DropCut.Core.Domain;
using DropCut.Modules.FileVault.Models;

namespace DropCut.Modules.FileVault.Contracts;

/// <summary>
/// Contract for vault file storage, auto-saving, and retrieval.
/// </summary>
public interface IFileVaultService
{
    Task<Result<VaultFile>> AutoSaveAsync(byte[] imageBytes, string title, CancellationToken cancellationToken = default);
}

namespace DropCut.Modules.FileVault.Models;

/// <summary>
/// Domain model for files managed by the FileVault.
/// </summary>
public class VaultFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

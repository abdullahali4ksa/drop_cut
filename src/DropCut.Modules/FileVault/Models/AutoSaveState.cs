namespace DropCut.Modules.FileVault.Models;

/// <summary>
/// State tracking object for pending auto-save commits.
/// </summary>
public class AutoSaveState
{
    public Guid SnippetId { get; set; }

    public bool IsDirty { get; set; }

    public DateTime? LastSavedAt { get; set; }

    public string TargetFilePath { get; set; } = string.Empty;

    public int UnsavedChangesCount { get; set; }
}

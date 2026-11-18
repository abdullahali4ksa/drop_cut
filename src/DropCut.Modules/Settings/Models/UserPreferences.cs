namespace DropCut.Modules.Settings.Models;

/// <summary>
/// Domain model encapsulating persistent user configuration.
/// </summary>
public class UserPreferences
{
    public string PreferredLanguage { get; set; } = "ar";

    public string Theme { get; set; } = "System";

    public bool AutoSaveEnabled { get; set; } = true;

    public int AutoSaveDebounceMs { get; set; } = 300;
}

namespace DropCut.Modules.Settings.Models;

/// <summary>
/// Data model representing application settings and preferences.
/// </summary>
public class AppSettingsModel
{
    public string LanguageCode { get; set; } = "en";

    public string Theme { get; set; } = "System";

    public bool AutoSaveEnabled { get; set; } = true;

    public int DebounceMilliseconds { get; set; } = 300;

    public string VaultDirectory { get; set; } = string.Empty;

    public string DefaultExportFormat { get; set; } = "Png";

    public bool LaunchOnStartup { get; set; } = false;

    public string SnippingHotkey { get; set; } = "Ctrl+Shift+S";
}

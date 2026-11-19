using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DropCut.Core.Domain;
using DropCut.Modules.Settings.Contracts;
using DropCut.Modules.Settings.Models;

namespace DropCut.Modules.Settings.ViewModels;

/// <summary>
/// Presentation ViewModel for application settings, preferences, and pure 10-language switching.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private string _selectedTheme = "System";

    [ObservableProperty]
    private bool _autoSaveEnabled = true;

    [ObservableProperty]
    private int _debounceMilliseconds = 300;

    [ObservableProperty]
    private string _vaultDirectory = string.Empty;

    [ObservableProperty]
    private string _defaultExportFormat = "Png";

    [ObservableProperty]
    private string _snippingHotkey = "Ctrl+Shift+S";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public IReadOnlyList<LanguageOption> SupportedLanguages => _localizationService.SupportedLanguages;

    public IReadOnlyList<string> AvailableThemes { get; } = new List<string>
    {
        "System",
        "Light",
        "Dark",
        "MicaAlt"
    };

    public IReadOnlyList<string> AvailableFormats { get; } = new List<string>
    {
        "Png",
        "Jpeg",
        "Bmp"
    };

    public SettingsViewModel(ISettingsService settingsService, ILocalizationService localizationService)
    {
        _settingsService = Guard.AgainstNull(settingsService, nameof(settingsService));
        _localizationService = Guard.AgainstNull(localizationService, nameof(localizationService));

        _selectedLanguage = _localizationService.SupportedLanguages.FirstOrDefault(l =>
            l.Code.Equals(_localizationService.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
            ?? _localizationService.SupportedLanguages.First();
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);
            AutoSaveEnabled = settings.AutoSaveEnabled;
            DebounceMilliseconds = settings.DebounceMilliseconds;
            VaultDirectory = settings.VaultDirectory;
            SelectedTheme = settings.Theme;
            DefaultExportFormat = settings.DefaultExportFormat;
            SnippingHotkey = settings.SnippingHotkey;

            var lang = _localizationService.SupportedLanguages.FirstOrDefault(l =>
                l.Code.Equals(settings.LanguageCode, StringComparison.OrdinalIgnoreCase));
            if (lang is not null)
            {
                SelectedLanguage = lang;
            }

            StatusMessage = _localizationService.GetString("Status_Ready");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var settings = new AppSettingsModel
            {
                LanguageCode = SelectedLanguage?.Code ?? "ar",
                Theme = SelectedTheme,
                AutoSaveEnabled = AutoSaveEnabled,
                DebounceMilliseconds = DebounceMilliseconds,
                VaultDirectory = VaultDirectory,
                DefaultExportFormat = DefaultExportFormat,
                SnippingHotkey = SnippingHotkey
            };

            await _settingsService.SaveSettingsAsync(settings).ConfigureAwait(false);
            StatusMessage = _localizationService.GetString("Settings_SavedSuccess");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ResetAsync()
    {
        IsBusy = true;
        try
        {
            var defaults = await _settingsService.ResetToDefaultsAsync().ConfigureAwait(false);
            AutoSaveEnabled = defaults.AutoSaveEnabled;
            DebounceMilliseconds = defaults.DebounceMilliseconds;
            VaultDirectory = defaults.VaultDirectory;
            SelectedTheme = defaults.Theme;
            DefaultExportFormat = defaults.DefaultExportFormat;
            SnippingHotkey = defaults.SnippingHotkey;

            var defaultLang = _localizationService.SupportedLanguages.FirstOrDefault(l => l.Code == "ar");
            if (defaultLang is not null)
            {
                SelectedLanguage = defaultLang;
                await _localizationService.SetLanguageAsync("ar").ConfigureAwait(false);
            }

            StatusMessage = _localizationService.GetString("Settings_ResetButton");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

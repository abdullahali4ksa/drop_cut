using System.Text.Json;
using DropCut.Core.Contracts;
using DropCut.Core.Domain;
using DropCut.Modules.Settings.Contracts;
using DropCut.Modules.Settings.Models;

namespace DropCut.Modules.Settings.Services;

/// <summary>
/// Service responsible for managing application configuration, preferences persistence,
/// and fallback defaults.
/// </summary>
public class SettingsService : ISettingsService
{
    private const string SettingsKey = "AppSettings";
    private readonly IUserPreferenceRepository _preferenceRepository;
    private readonly ILocalizationService? _localizationService;

    public SettingsService(
        IUserPreferenceRepository preferenceRepository,
        ILocalizationService? localizationService = null)
    {
        _preferenceRepository = Guard.AgainstNull(preferenceRepository, nameof(preferenceRepository));
        _localizationService = localizationService;
    }

    public async Task<AppSettingsModel> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rawJson = await _preferenceRepository.GetValueAsync(SettingsKey, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(rawJson))
            {
                var settings = JsonSerializer.Deserialize<AppSettingsModel>(rawJson);
                if (settings is not null)
                {
                    EnsureDefaultVaultPath(settings);
                    return settings;
                }
            }
        }
        catch (JsonException)
        {
            // Corrupt settings payload: fallback to defaults safely
        }
        catch (Exception)
        {
            // Defensive recovery
        }

        var defaultSettings = CreateDefaultSettings();
        EnsureDefaultVaultPath(defaultSettings);
        return defaultSettings;
    }

    public async Task SaveSettingsAsync(AppSettingsModel settings, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(settings, nameof(settings));

        // Sanitize and validate
        if (settings.DebounceMilliseconds < 50)
        {
            settings.DebounceMilliseconds = 50;
        }
        else if (settings.DebounceMilliseconds > 5000)
        {
            settings.DebounceMilliseconds = 5000;
        }

        EnsureDefaultVaultPath(settings);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await _preferenceRepository.SetValueAsync(SettingsKey, json, cancellationToken).ConfigureAwait(false);

        if (_localizationService is not null && !string.IsNullOrWhiteSpace(settings.LanguageCode))
        {
            await _localizationService.SetLanguageAsync(settings.LanguageCode, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<AppSettingsModel> ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var defaults = CreateDefaultSettings();
        await SaveSettingsAsync(defaults, cancellationToken).ConfigureAwait(false);
        return defaults;
    }

    private static AppSettingsModel CreateDefaultSettings()
    {
        return new AppSettingsModel
        {
            LanguageCode = "en",
            Theme = "System",
            AutoSaveEnabled = true,
            DebounceMilliseconds = 300,
            VaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DropCut"),
            DefaultExportFormat = "Png",
            LaunchOnStartup = false,
            SnippingHotkey = "Ctrl+Shift+S"
        };
    }

    private static void EnsureDefaultVaultPath(AppSettingsModel settings)
    {
        if (string.IsNullOrWhiteSpace(settings.VaultDirectory))
        {
            settings.VaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DropCut");
        }
    }
}

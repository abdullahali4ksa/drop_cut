using DropCut.Modules.Settings.Models;

namespace DropCut.Modules.Settings.Contracts;

/// <summary>
/// Contract for managing application-wide settings and configuration persistence.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads the current application settings from persistent storage.
    /// </summary>
    Task<AppSettingsModel> LoadSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists updated application settings to storage.
    /// </summary>
    Task SaveSettingsAsync(AppSettingsModel settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets application settings to their default initial values.
    /// </summary>
    Task<AppSettingsModel> ResetToDefaultsAsync(CancellationToken cancellationToken = default);
}

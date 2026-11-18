using DropCut.Modules.Settings.Models;

namespace DropCut.Modules.Settings.Contracts;

/// <summary>
/// Contract for retrieving and updating user application preferences.
/// </summary>
public interface ISettingsService
{
    Task<UserPreferences> GetPreferencesAsync(CancellationToken cancellationToken = default);

    Task UpdatePreferencesAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
}

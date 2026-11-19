using DropCut.Modules.Settings.Models;

namespace DropCut.Modules.Settings.Contracts;

/// <summary>
/// Contract for pure i18n localization engine supporting 10 languages and dynamic runtime switching.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Gets the current active language code (e.g. "ar", "en").
    /// </summary>
    string CurrentLanguageCode { get; }

    /// <summary>
    /// Gets whether the current language uses right-to-left layout direction.
    /// </summary>
    bool IsRtl { get; }

    /// <summary>
    /// Gets the list of supported languages.
    /// </summary>
    IReadOnlyList<LanguageOption> SupportedLanguages { get; }

    /// <summary>
    /// Retrieves the localized string for the specified key.
    /// </summary>
    string GetString(string key);

    /// <summary>
    /// Retrieves the formatted localized string for the specified key.
    /// </summary>
    string GetString(string key, params object[] args);

    /// <summary>
    /// Switches the application language dynamically at runtime.
    /// </summary>
    Task SetLanguageAsync(string languageCode, CancellationToken cancellationToken = default);
}

using DropCut.Modules.Settings.Contracts;
using DropCut.Modules.Settings.Services;
using DropCut.Modules.Settings.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DropCut.Modules.Settings;

/// <summary>
/// Settings module composition and dependency injection registration.
/// </summary>
public static class SettingsModule
{
    public const string ModuleName = "Settings";

    public static IServiceCollection AddSettingsModule(this IServiceCollection services)
    {
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddTransient<SettingsViewModel>();
        return services;
    }
}

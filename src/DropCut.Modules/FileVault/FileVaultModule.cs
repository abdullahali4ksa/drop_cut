using DropCut.Modules.FileVault.Contracts;
using DropCut.Modules.FileVault.Services;
using DropCut.Modules.FileVault.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DropCut.Modules.FileVault;

/// <summary>
/// FileVault module composition and dependency injection wiring.
/// </summary>
public static class FileVaultModule
{
    public const string ModuleName = "FileVault";

    public static IServiceCollection AddFileVaultModule(this IServiceCollection services)
    {
        services.AddSingleton<IFileVaultService, FileVaultService>();
        services.AddSingleton<IAutoSaveService, AutoSaveService>();
        services.AddTransient<IImageExportService, ImageExportService>();
        services.AddTransient<VaultGalleryViewModel>();
        return services;
    }
}

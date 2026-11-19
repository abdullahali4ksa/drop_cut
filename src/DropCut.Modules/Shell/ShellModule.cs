using DropCut.Modules.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DropCut.Modules.Shell;

/// <summary>
/// Shell module composition and dependency injection registration.
/// </summary>
public static class ShellModule
{
    public const string ModuleName = "Shell";

    public static IServiceCollection AddShellModule(this IServiceCollection services)
    {
        services.AddSingleton<ShellViewModel>();
        return services;
    }
}

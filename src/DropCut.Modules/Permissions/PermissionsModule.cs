using DropCut.Modules.Permissions.Contracts;
using DropCut.Modules.Permissions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DropCut.Modules.Permissions;

/// <summary>
/// Permissions module composition and dependency injection registration.
/// </summary>
public static class PermissionsModule
{
    public const string ModuleName = "Permissions";

    public static IServiceCollection AddPermissionsModule(this IServiceCollection services)
    {
        services.AddSingleton<IModulePermissionService, ModulePermissionService>();
        return services;
    }
}

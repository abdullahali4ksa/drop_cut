using DropCut.Modules.AuditLog.Contracts;
using DropCut.Modules.AuditLog.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DropCut.Modules.AuditLog;

/// <summary>
/// AuditLog module composition and dependency injection registration.
/// </summary>
public static class AuditLogModule
{
    public const string ModuleName = "AuditLog";

    public static IServiceCollection AddAuditLogModule(this IServiceCollection services)
    {
        services.AddSingleton<IAuditLogService, AuditLogService>();
        return services;
    }
}

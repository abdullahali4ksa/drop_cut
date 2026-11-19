using DropCut.Modules.Workspace.Contracts;
using DropCut.Modules.Workspace.Services;
using DropCut.Modules.Workspace.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DropCut.Modules.Workspace;

/// <summary>
/// Workspace module composition and dependency injection registration.
/// </summary>
public static class WorkspaceModule
{
    public const string ModuleName = "Workspace";

    public static IServiceCollection AddWorkspaceModule(this IServiceCollection services)
    {
        services.AddSingleton<ISnappingEngine, SnappingEngine>();
        services.AddSingleton<IAnnotationCanvasService, AnnotationCanvasService>();
        services.AddSingleton<IDragDropSharingService, DragDropSharingService>();
        services.AddTransient<ToolboxViewModel>();
        services.AddTransient<WorkspaceViewModel>();
        return services;
    }
}

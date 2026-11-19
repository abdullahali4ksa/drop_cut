using DropCut.Modules.Shell;
using DropCut.Modules.Shell.ViewModels;
using DropCut.Core.Contracts;
using DropCut.Core.Events;
using DropCut.Infrastructure.FileSystem;
using DropCut.Infrastructure.Persistence;
using DropCut.Infrastructure.Persistence.Repositories;
using DropCut.Modules.AuditLog;
using DropCut.Modules.FileVault;
using DropCut.Modules.Permissions;
using DropCut.Modules.Settings;
using DropCut.Modules.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace DropCut.App;

/// <summary>
/// Provides application-specific behavior, Dependency Injection composition root,
/// and zero-crash fault tolerance boundaries.
/// </summary>
public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public static IServiceProvider Services { get; private set; } = null!;

    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    public App()
    {
        InitializeComponent();

        ConfigureServices();

        UnhandledException += (sender, e) =>
        {
            e.Handled = true;
            LogDiagnosticCrash("CRASH_XAML", e.Message, e.Exception);
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            e.SetObserved();
            LogDiagnosticCrash("UNOBSERVED_TASK", e.Exception?.Message ?? "Unobserved task fault", e.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            LogDiagnosticCrash("APPDOMAIN_UNHANDLED", e.ExceptionObject?.ToString() ?? "AppDomain fault", e.ExceptionObject as Exception);
        };
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        // 1. Core Services & Event Bus
        services.AddSingleton<IEventBus, InMemoryEventBus>();

        // 2. Infrastructure & Persistence
        services.AddDbContext<DropCutDbContext>();
        services.AddSingleton<ISafeFileStorage, SafeFileStorageService>();
        services.AddScoped<SnippetRepository>();
        services.AddScoped<AuditLogRepository>();
        services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();

        // 3. Vertical Feature Modules (Django/ERP Style)
        services.AddWorkspaceModule();
        services.AddFileVaultModule();
        services.AddPermissionsModule();
        services.AddAuditLogModule();
        services.AddSettingsModule();

        // 4. Shell & UI Services
        services.AddShellModule();
        services.AddTransient<MainWindow>();

        Services = services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            MainWindow = Services.GetRequiredService<MainWindow>();
            MainWindow.Activate();
        }
        catch (Exception ex)
        {
            LogDiagnosticCrash("ON_LAUNCHED_FAULT", ex.Message, ex);
        }
    }

    private static void LogDiagnosticCrash(string category, string message, Exception? ex)
    {
        try
        {
            var logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DropCut");
            if (!System.IO.Directory.Exists(logDir))
            {
                System.IO.Directory.CreateDirectory(logDir);
            }

            var logPath = System.IO.Path.Combine(logDir, "crash.log");
            var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}\n{ex}\n\n";
            System.IO.File.AppendAllText(logPath, entry);
        }
        catch
        {
            // Do not fail-fast if disk logger fails
        }
    }
}

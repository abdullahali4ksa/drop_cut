using Microsoft.UI.Xaml;

namespace DropCut.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();

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

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
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

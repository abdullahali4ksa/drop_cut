namespace DropCut.Infrastructure.Persistence;

/// <summary>
/// Persistence gateway placeholder for DropCut SQLite database operations.
/// </summary>
public class DropCutDbContext
{
    public string DatabasePath { get; }

    public DropCutDbContext(string? databasePath = null)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "DropCut");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        DatabasePath = databasePath ?? Path.Combine(dir, "dropcut.db");
    }
}

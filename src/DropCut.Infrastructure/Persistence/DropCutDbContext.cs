using DropCut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DropCut.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for DropCut's SQLite persistence layer.
/// </summary>
public class DropCutDbContext : DbContext
{
    public DbSet<SnippetEntity> Snippets => Set<SnippetEntity>();

    public DbSet<AnnotationEntity> Annotations => Set<AnnotationEntity>();

    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();

    public DbSet<UserPreferenceEntity> UserPreferences => Set<UserPreferenceEntity>();

    public string DatabasePath { get; }

    public DropCutDbContext() : this((string?)null)
    {
    }

    public DropCutDbContext(string? databasePath)
    {
        if (string.IsNullOrEmpty(databasePath))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(localAppData, "DropCut");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            DatabasePath = Path.Combine(dir, "dropcut.db");
        }
        else
        {
            DatabasePath = databasePath;
        }
    }

    public DropCutDbContext(DbContextOptions<DropCutDbContext> options)
        : base(options)
    {
        DatabasePath = string.Empty;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(DatabasePath))
        {
            optionsBuilder.UseSqlite($"Data Source={DatabasePath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SnippetEntity
        modelBuilder.Entity<SnippetEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IsFavorite);

            entity.HasMany(e => e.Annotations)
                .WithOne(a => a.Snippet)
                .HasForeignKey(a => a.SnippetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AnnotationEntity
        modelBuilder.Entity<AnnotationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SnippetId);
        });

        // AuditLogEntity
        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.SnippetId);
        });

        // PermissionEntity
        modelBuilder.Entity<PermissionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RoleName, e.FeatureKey }).IsUnique();
        });

        // UserPreferenceEntity
        modelBuilder.Entity<UserPreferenceEntity>(entity =>
        {
            entity.HasKey(e => e.Key);
        });
    }
}

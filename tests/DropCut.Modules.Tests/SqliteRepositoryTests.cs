using DropCut.Core.Domain;
using DropCut.Infrastructure.Persistence;
using DropCut.Infrastructure.Persistence.Entities;
using DropCut.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DropCut.Modules.Tests;

public class SqliteRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DropCutDbContext _context;

    public SqliteRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DropCutDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new DropCutDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task SnippetRepository_ShouldAddAndRetrieveSnippet()
    {
        var repo = new SnippetRepository(_context);
        var snippetId = Guid.NewGuid();

        var entity = new SnippetEntity
        {
            Id = snippetId,
            Title = "Snapshot 1",
            FilePath = "C:\\vault\\snap1.png",
            CaptureMode = CaptureMode.Rectangular,
            Width = 1920,
            Height = 1080,
            IsPinned = true,
            IsFavorite = true
        };

        await repo.AddAsync(entity);

        var retrieved = await repo.GetByIdAsync(snippetId);
        Assert.NotNull(retrieved);
        Assert.Equal("Snapshot 1", retrieved.Title);
        Assert.True(retrieved.IsPinned);
        Assert.True(retrieved.IsFavorite);
    }

    [Fact]
    public async Task AuditLogRepository_ShouldAddAndRetrieveLogs()
    {
        var repo = new AuditLogRepository(_context);

        await repo.AddLogAsync(new AuditLogEntity
        {
            EventType = 1,
            Description = "User drew highlighter stroke",
            ActorRole = "User"
        });

        var logs = await repo.GetRecentLogsAsync(10);
        Assert.Single(logs);
        Assert.Equal("User drew highlighter stroke", logs[0].Description);
    }

    [Fact]
    public async Task UserPreferenceRepository_ShouldSetAndRetrievePreference()
    {
        var repo = new UserPreferenceRepository(_context);

        await repo.SetValueAsync("PreferredLanguage", "ar");
        var val = await repo.GetValueAsync("PreferredLanguage");

        Assert.Equal("ar", val);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

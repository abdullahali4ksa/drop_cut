using DropCut.Core.Contracts;
using DropCut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DropCut.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for persistent user preferences backed by SQLite EF Core.
/// </summary>
public class UserPreferenceRepository : IUserPreferenceRepository
{
    private readonly DropCutDbContext _context;

    public UserPreferenceRepository(DropCutDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        var entity = await _context.UserPreferences.FindAsync(new object[] { key }, cancellationToken).ConfigureAwait(false);
        return entity?.Value;
    }

    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var entity = await _context.UserPreferences.FindAsync(new object[] { key }, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            await _context.UserPreferences.AddAsync(new UserPreferenceEntity
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entity.Value = value;
            entity.UpdatedAt = DateTime.UtcNow;
            _context.UserPreferences.Update(entity);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

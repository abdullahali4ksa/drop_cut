using DropCut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DropCut.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for managing persistent snippet records and metadata indexing.
/// </summary>
public class SnippetRepository
{
    private readonly DropCutDbContext _context;

    public SnippetRepository(DropCutDbContext context)
    {
        _context = context;
    }

    public async Task<SnippetEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Snippets
            .Include(s => s.Annotations)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<SnippetEntity>> GetRecentSnippetsAsync(int count, CancellationToken cancellationToken = default)
    {
        return await _context.Snippets
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(SnippetEntity snippet, CancellationToken cancellationToken = default)
    {
        await _context.Snippets.AddAsync(snippet, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(SnippetEntity snippet, CancellationToken cancellationToken = default)
    {
        snippet.UpdatedAt = DateTime.UtcNow;
        _context.Snippets.Update(snippet);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Snippets.FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity is not null)
        {
            _context.Snippets.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

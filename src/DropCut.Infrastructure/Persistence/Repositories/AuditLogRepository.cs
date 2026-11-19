using DropCut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DropCut.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for logging and querying system/user audit trails.
/// </summary>
public class AuditLogRepository
{
    private readonly DropCutDbContext _context;

    public AuditLogRepository(DropCutDbContext context)
    {
        _context = context;
    }

    public async Task AddLogAsync(AuditLogEntity log, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AuditLogEntity>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

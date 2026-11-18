using DropCut.Modules.AuditLog.Models;

namespace DropCut.Modules.AuditLog.Contracts;

/// <summary>
/// Contract for recording and querying audit trail entries.
/// </summary>
public interface IAuditLogService
{
    Task RecordEventAsync(string eventType, string description, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditRecord>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default);
}

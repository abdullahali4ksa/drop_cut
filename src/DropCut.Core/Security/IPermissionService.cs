namespace DropCut.Core.Security;

/// <summary>
/// Security contract for verifying user capabilities and role-based feature policies.
/// </summary>
public interface IPermissionService
{
    Task<bool> HasPermissionAsync(string featureKey, CancellationToken cancellationToken = default);
}

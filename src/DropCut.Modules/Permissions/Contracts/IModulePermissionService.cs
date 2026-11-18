using DropCut.Modules.Permissions.Models;

namespace DropCut.Modules.Permissions.Contracts;

/// <summary>
/// Module contract for querying and asserting role permissions.
/// </summary>
public interface IModulePermissionService
{
    bool IsRoleAuthorized(AppRole role, string featureKey);
}

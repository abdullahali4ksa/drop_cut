using System.Collections.Concurrent;
using DropCut.Modules.Permissions.Contracts;
using DropCut.Modules.Permissions.Models;

namespace DropCut.Modules.Permissions.Services;

/// <summary>
/// Lightweight, pluggable role-based authorization service.
/// </summary>
public class ModulePermissionService : IModulePermissionService
{
    private readonly ConcurrentDictionary<string, HashSet<AppRole>> _policyRules = new(StringComparer.OrdinalIgnoreCase);

    public ModulePermissionService()
    {
        // Default policy rules: Admin has full access, Editor has operational access, Viewer has read access
        AllowAllRolesFor("Workspace.Snip");
        AllowAllRolesFor("Workspace.Annotate");
        AllowAllRolesFor("Workspace.DragDrop");
        AllowAllRolesFor("FileVault.AutoSave");
        AllowAllRolesFor("FileVault.Export");
    }

    public bool IsRoleAuthorized(AppRole role, string featureKey)
    {
        if (role == AppRole.Admin)
        {
            return true;
        }

        if (_policyRules.TryGetValue(featureKey, out var allowedRoles))
        {
            return allowedRoles.Contains(role);
        }

        // By default, grant access unless explicitly restricted
        return true;
    }

    public void RestrictFeature(string featureKey, params AppRole[] allowedRoles)
    {
        _policyRules[featureKey] = new HashSet<AppRole>(allowedRoles);
    }

    private void AllowAllRolesFor(string featureKey)
    {
        _policyRules[featureKey] = new HashSet<AppRole> { AppRole.Admin, AppRole.Editor, AppRole.Viewer };
    }
}

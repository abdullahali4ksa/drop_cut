namespace DropCut.Modules.Permissions.Models;

/// <summary>
/// Encapsulates permission policy rule for a specific feature and role.
/// </summary>
public class FeaturePermission
{
    public string FeatureKey { get; set; } = string.Empty;

    public AppRole Role { get; set; } = AppRole.Admin;

    public bool IsAllowed { get; set; } = true;
}

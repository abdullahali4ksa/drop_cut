namespace DropCut.Infrastructure.Persistence.Entities;

/// <summary>
/// Database entity for role-based permission rules.
/// </summary>
public class PermissionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RoleName { get; set; } = string.Empty;

    public string FeatureKey { get; set; } = string.Empty;

    public bool IsAllowed { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

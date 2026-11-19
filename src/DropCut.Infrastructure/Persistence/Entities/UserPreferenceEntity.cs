namespace DropCut.Infrastructure.Persistence.Entities;

/// <summary>
/// Database entity for persistent key-value configuration.
/// </summary>
public class UserPreferenceEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

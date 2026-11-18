namespace DropCut.Core.Domain;

/// <summary>
/// Base class for all domain entities within the DropCut ecosystem.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;
}

namespace DropCut.Core.Contracts;

/// <summary>
/// Contract for retrieving and persisting key-value application preferences.
/// </summary>
public interface IUserPreferenceRepository
{
    /// <summary>
    /// Retrieves a preference value by its key.
    /// </summary>
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists or updates a preference value by its key.
    /// </summary>
    Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default);
}

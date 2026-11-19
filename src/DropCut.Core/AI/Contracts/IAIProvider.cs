using DropCut.Core.Domain;

namespace DropCut.Core.AI.Contracts;

/// <summary>
/// Provider-agnostic contract for external or local AI intelligence providers.
/// </summary>
public interface IAIProvider
{
    string ProviderName { get; }

    Task<Result<string>> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default);
}

namespace DropCut.Core.AI.Contracts;

/// <summary>
/// Provider-agnostic command handler for executing verified machine and AI actions.
/// </summary>
public interface IAICommandHandler
{
    Task<Domain.Result> HandleCommandAsync(string commandJson, CancellationToken cancellationToken = default);
}

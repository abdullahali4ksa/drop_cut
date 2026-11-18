namespace DropCut.Core.Contracts;

/// <summary>
/// Gateway for executing decoupled cross-module commands and queries.
/// </summary>
public interface ICommandGateway
{
    Task<Domain.Result> ExecuteAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class;
}

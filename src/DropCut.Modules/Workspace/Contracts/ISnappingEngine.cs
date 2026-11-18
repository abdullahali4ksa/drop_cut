using DropCut.Core.Domain;
using DropCut.Modules.Workspace.Models;

namespace DropCut.Modules.Workspace.Contracts;

/// <summary>
/// Contract for orchestrating screen snapping and desktop captures.
/// </summary>
public interface ISnappingEngine
{
    Task<Result<SnipRequest>> TriggerCaptureAsync(SnipRequest request, CancellationToken cancellationToken = default);
}

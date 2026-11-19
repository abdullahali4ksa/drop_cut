using System.Collections.Concurrent;
using DropCut.Core.AI.Contracts;

namespace DropCut.Core.AI.Commands;

/// <summary>
/// Registry for discovering and dispatching machine-controllable AI commands.
/// </summary>
public class AICommandRegistry
{
    private readonly ConcurrentDictionary<string, IAICommandHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string commandName, IAICommandHandler handler)
    {
        _handlers[commandName] = handler;
    }

    public bool TryGetHandler(string commandName, out IAICommandHandler? handler)
    {
        return _handlers.TryGetValue(commandName, out handler);
    }
}

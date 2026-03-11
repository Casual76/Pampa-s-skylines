#nullable enable

namespace PampaSkylines.Commands
{
using System.Collections.Generic;
using System.Linq;

public sealed class CommandBuffer
{
    private readonly Queue<GameCommand> _commands = new();

    public int Count => _commands.Count;

    public void Enqueue(GameCommand command) => _commands.Enqueue(command);

    public bool TryDequeue(out GameCommand? command)
    {
        if (_commands.Count == 0)
        {
            command = null;
            return false;
        }

        command = _commands.Dequeue();
        return true;
    }

    public void Clear()
    {
        _commands.Clear();
    }

    public IReadOnlyList<GameCommand> DrainAll()
    {
        var drained = _commands.ToList();
        _commands.Clear();
        return drained;
    }
}
}

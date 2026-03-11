#nullable enable

namespace PampaSkylines.Simulation
{
using PampaSkylines.Commands;
using PampaSkylines.Core;

public static class CommandReplayEngine
{
    public static ReplayExecutionResult Replay(WorldState initialState, CommandReplayLog replayLog, SimulationConfig? config = null)
    {
        var clonedState = Clone(initialState);
        var buffer = new CommandBuffer();
        foreach (var command in replayLog.Commands)
        {
            buffer.Enqueue(command);
        }

        var frameReport = SimulationEngine.SimulationStep(
            clonedState,
            buffer,
            replayLog.FixedDeltaTime,
            config ?? SimulationConfigLoader.LoadDefault());

        return new ReplayExecutionResult
        {
            FinalState = clonedState,
            FrameReport = frameReport,
            FinalStateHash = SnapshotHashing.ComputeWorldHash(clonedState)
        };
    }

    private static WorldState Clone(WorldState state)
    {
        var payload = PampaSkylinesJson.Serialize(state);
        return PampaSkylinesJson.Deserialize<WorldState>(payload) ?? new WorldState();
    }
}
}

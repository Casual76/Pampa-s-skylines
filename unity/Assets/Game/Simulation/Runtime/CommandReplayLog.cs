namespace PampaSkylines.Simulation
{
using System;
using System.Collections.Generic;
using PampaSkylines.Commands;
using PampaSkylines.Core;

public sealed class CommandReplayLog
{
    public int SchemaVersion { get; set; } = 1;

    public string CityId { get; set; } = string.Empty;

    public string ClientId { get; set; } = "local";

    public string SimulationConfigVersion { get; set; } = "unknown";

    public float FixedDeltaTime { get; set; } = 0.5f;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<GameCommand> Commands { get; set; } = new();

    public string Serialize()
    {
        return PampaSkylinesJson.Serialize(this);
    }

    public static CommandReplayLog Deserialize(string json)
    {
        return PampaSkylinesJson.Deserialize<CommandReplayLog>(json) ?? new CommandReplayLog();
    }
}

public sealed class ReplayExecutionResult
{
    public WorldState FinalState { get; set; } = new();

    public SimulationFrameReport FrameReport { get; set; } = new();

    public string FinalStateHash { get; set; } = string.Empty;
}
}

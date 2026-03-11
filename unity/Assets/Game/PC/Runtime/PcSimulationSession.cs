#nullable enable

namespace PampaSkylines.PC
{
using System;
using PampaSkylines.Commands;
using PampaSkylines.Core;
using PampaSkylines.Simulation;

public sealed class PcSimulationSession
{
    public PcSimulationSession(string cityName, SimulationConfig? config = null, string clientId = "pc")
    {
        State = WorldState.CreateNew(cityName);
        Commands = new CommandBuffer();
        Config = config ?? SimulationConfigLoader.LoadDefault();
        ClientId = clientId;
    }

    public WorldState State { get; private set; }

    public CommandBuffer Commands { get; }

    public SimulationConfig Config { get; }

    public string ClientId { get; }

    public SimulationFrameReport Tick(float dt)
    {
        return SimulationEngine.SimulationStep(State, Commands, dt, Config);
    }

    public CitySnapshot CreateSnapshot(string saveSlotId = "autosave", string saveReason = "manual", string? version = null)
    {
        return CitySnapshot.FromWorld(
            State,
            version ?? $"{State.Tick:D12}-{Guid.NewGuid():N}",
            ClientId,
            new SnapshotMetadata
            {
                SourceClientId = ClientId,
                SourcePlatform = "pc",
                SimulationConfigVersion = Config.Version,
                DebugLabel = State.CityName,
                SaveSlotId = saveSlotId,
                SaveReason = saveReason
            });
    }

    public WorldState CreateStateClone()
    {
        return PampaSkylinesClone.DeepCopy(State);
    }

    public void RestoreState(WorldState state)
    {
        State = PampaSkylinesClone.DeepCopy(state);
        Commands.Clear();
    }

    public void RestoreSnapshot(CitySnapshot snapshot)
    {
        State = PampaSkylinesClone.DeepCopy(snapshot.State);
        Commands.Clear();
    }

    public void ResetCity(string cityName)
    {
        State = WorldState.CreateNew(cityName);
        Commands.Clear();
    }
}
}

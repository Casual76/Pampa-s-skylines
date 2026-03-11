namespace PampaSkylines.Simulation
{
using PampaSkylines.Commands;
using PampaSkylines.Core;

public static class SimulationEngine
{
    public static void SimulationTick(WorldState state, CommandBuffer commands, float dt)
    {
        SimulationStep(state, commands, dt, SimulationConfigLoader.LoadDefault());
    }

    public static SimulationFrameReport SimulationStep(WorldState state, CommandBuffer commands, float dt, SimulationConfig config)
    {
        ProgressionModel.EnsureInitialized(state, config);
        CityEventModel.EnsureInitialized(state, config);
        var report = new SimulationFrameReport
        {
            TickBefore = state.Tick,
            DeltaTime = dt,
            SimulationConfigVersion = config.Version,
            StateHashBefore = SnapshotHashing.ComputeWorldHash(state)
        };

        var drainedCommands = commands.DrainAll();
        report.RequestedCommandCount = drainedCommands.Count;

        foreach (var command in drainedCommands)
        {
            var result = CommandExecutor.Execute(state, command, config, report);
            report.CommandResults.Add(result);
            if (result.Status == CommandExecutionStatus.Applied)
            {
                report.AppliedCommandCount++;
                state.AppliedCommandCount++;
            }
            else
            {
                report.RejectedCommandCount++;
            }
        }

        var simulationDeltaTime = state.Time.IsPaused
            ? 0f
            : dt * state.Time.SpeedMultiplier;

        UtilityCoverageModel.Update(state, config);
        TrafficModel.Update(state, config);
        DistrictVitalityModel.Update(state, config);
        DemandModel.Update(state, config);
        GrowthModel.Update(state, simulationDeltaTime, config);
        state.Time.Advance(simulationDeltaTime);
        ProgressionModel.Update(state, simulationDeltaTime, config, report);
        CityEventModel.Update(state, simulationDeltaTime, config, report);
        DemoRunModel.Update(state, simulationDeltaTime, config, report);
        state.Tick++;

        report.TickAfter = state.Tick;
        report.StateHashAfter = SnapshotHashing.ComputeWorldHash(state);
        return report;
    }
}
}

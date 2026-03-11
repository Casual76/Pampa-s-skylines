#nullable enable

namespace PampaSkylines.Core
{
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class WorldState
{
    public int SchemaVersion { get; set; } = 4;

    public string CityId { get; set; } = Guid.NewGuid().ToString("N");

    public string CityName { get; set; } = "Nuova Citta";

    public long Tick { get; set; }

    public long NextEntitySequence { get; set; } = 1;

    public TimeState Time { get; set; } = new();

    public BudgetState Budget { get; set; } = new();

    public DemandState Demand { get; set; } = new();

    public UtilityState Utilities { get; set; } = new();

    public ProgressionState Progression { get; set; } = new();

    public RunState RunState { get; set; } = new();

    public DemoRunState DemoRun { get; set; } = new();

    public List<RoadNode> RoadNodes { get; set; } = new();

    public List<RoadSegment> RoadSegments { get; set; } = new();

    public List<ZoneLot> Lots { get; set; } = new();

    public List<BuildingState> Buildings { get; set; } = new();

    public List<CommuterAgent> Commuters { get; set; } = new();

    public long AppliedCommandCount { get; set; }

    public float AverageCommuteMinutes { get; set; }

    public float AverageTrafficCongestion { get; set; }

    public int Population => Buildings.Sum(static building => building.Residents);

    public int Jobs => Buildings.Sum(static building => building.Jobs);

    public float AverageLandValue => Lots.Count == 0 ? 0f : Lots.Average(static lot => lot.LandValue);

    public static WorldState CreateNew(string cityName)
    {
        var fallbackConfig = SimulationConfig.CreateFallback();
        var state = new WorldState
        {
            CityName = cityName
        };

        state.Progression = ProgressionState.CreateForPopulation(
            fallbackConfig.Progression,
            population: 0,
            treatRewardsAsAlreadyGranted: true);
        state.Progression.LastLoanRepaymentDay = state.Time.Day;
        state.RunState = RunState.CreateInitial(fallbackConfig.Events, population: 0, currentDay: state.Time.Day);
        state.DemoRun = DemoRunState.CreateInitial(fallbackConfig.Events);
        state.DemoRun.Normalize();
        return state;
    }
}
}

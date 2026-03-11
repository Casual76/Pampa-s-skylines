namespace PampaSkylines.Simulation
{
using System;
using System.Collections.Generic;
using System.Linq;
using PampaSkylines.Core;

public static class TrafficModel
{
    public static void Update(WorldState state, SimulationConfig config)
    {
        var commuteMultiplier = CityEventModel.GetCommuteMinutesMultiplier(state);
        state.Commuters.Clear();

        var homes = state.Buildings
            .Where(static building => building.ZoneType == ZoneType.Residential && building.Residents > 0)
            .OrderBy(static building => building.Id, StringComparer.Ordinal)
            .ToList();
        var workplaces = state.Buildings
            .Where(static building => building.ZoneType != ZoneType.Residential && building.Jobs > 0)
            .OrderBy(static building => building.Id, StringComparer.Ordinal)
            .ToList();

        if (homes.Count == 0 || workplaces.Count == 0 || state.RoadSegments.Count == 0)
        {
            foreach (var segment in state.RoadSegments)
            {
                segment.Congestion = 0f;
            }

            state.AverageCommuteMinutes = 0f;
            state.AverageTrafficCongestion = 0f;
            return;
        }

        var segmentLoads = new Dictionary<string, int>();
        foreach (var segment in state.RoadSegments)
        {
            segmentLoads[segment.Id] = 0;
        }

        for (var homeIndex = 0; homeIndex < homes.Count; homeIndex++)
        {
            var home = homes[homeIndex];
            var workers = Math.Max(1, home.Residents / 3);
            for (var index = 0; index < workers; index++)
            {
                var workplace = workplaces[(homeIndex + index) % workplaces.Count];
                var path = RoadPathFinder.FindPathSegments(state, home.Cell, workplace.Cell);
                if (path.Count == 0)
                {
                    continue;
                }

                foreach (var segment in path)
                {
                    segmentLoads[segment.Id]++;
                }

                state.Commuters.Add(new CommuterAgent
                {
                    Id = DeterministicIdGenerator.Next(state, "commuter"),
                    HomeBuildingId = home.Id,
                    WorkBuildingId = workplace.Id,
                    CurrentRoadSegmentId = path[0].Id,
                    CommuteMinutes = path.Sum(segment => (float)(segment.Length * config.Economy.CommuteMinutesPerRoadUnit)) * commuteMultiplier
                });
            }
        }

        foreach (var segment in state.RoadSegments)
        {
            var load = segmentLoads[segment.Id];
            segment.Congestion = Math.Clamp(load / (float)Math.Max(1, segment.Capacity), 0f, 2f);
        }

        state.AverageCommuteMinutes = state.Commuters.Count == 0
            ? 0f
            : state.Commuters.Average(static commuter => commuter.CommuteMinutes);
        state.AverageTrafficCongestion = state.RoadSegments.Count == 0
            ? 0f
            : state.RoadSegments.Average(static segment => segment.Congestion);
    }
}
}

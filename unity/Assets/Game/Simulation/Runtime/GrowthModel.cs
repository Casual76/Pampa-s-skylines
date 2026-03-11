namespace PampaSkylines.Simulation
{
using System;
using System.Linq;
using PampaSkylines.Core;

public static class GrowthModel
{
    public static void Update(WorldState state, float dt, SimulationConfig config)
    {
        var growthMultiplier = CityEventModel.GetGrowthMultiplier(state);
        foreach (var lot in state.Lots
                     .Where(static lot => lot.ZoneType != ZoneType.None)
                     .OrderBy(static lot => lot.Cell.X)
                     .ThenBy(static lot => lot.Cell.Y))
        {
            if (!lot.HasRoadAccess || !lot.HasElectricity || !lot.HasWater || !lot.HasSewage)
            {
                lot.GrowthProgress = Math.Max(0f, lot.GrowthProgress - (config.Economy.GrowthDecayWithoutUtilitiesPerHour * dt));
                continue;
            }

            var zoneDefinition = config.ResolveZone(lot.ZoneType);
            var demand = lot.ZoneType switch
            {
                ZoneType.Residential => state.Demand.Residential,
                ZoneType.Commercial => state.Demand.Commercial,
                ZoneType.Industrial => state.Demand.Industrial,
                ZoneType.Office => state.Demand.Office,
                _ => 0f
            };

            var vitalityMultiplier = Math.Clamp(0.50f + (lot.DistrictVitality * (0.80f + config.Economy.DistrictVitalityGrowthWeight)), 0.25f, 1.85f);
            lot.GrowthProgress += demand * lot.LandValue * vitalityMultiplier * dt * growthMultiplier;

            if (lot.BuildingId is null && lot.GrowthProgress >= zoneDefinition.SpawnGrowthThreshold)
            {
                var building = SpawnBuilding(state, lot, zoneDefinition);
                state.Buildings.Add(building);
                lot.BuildingId = building.Id;
                lot.GrowthProgress = 0f;
                continue;
            }

            if (lot.BuildingId is not null && lot.GrowthProgress >= zoneDefinition.UpgradeGrowthThreshold)
            {
                var building = state.Buildings.FirstOrDefault(existing => existing.Id == lot.BuildingId);
                if (building is not null && building.Level < zoneDefinition.MaxLevel)
                {
                    building.Level++;
                    building.Residents += zoneDefinition.UpgradeResidents;
                    building.Jobs += zoneDefinition.UpgradeJobs;
                    building.Condition = Math.Clamp(building.Condition + (lot.DistrictVitality * 0.12f), 0.25f, 1f);
                    building.DistrictVitality = lot.DistrictVitality;
                    lot.GrowthProgress = 0f;
                }
            }
        }

        state.Budget.DailyRoadMaintenanceCost = Math.Round(
            state.RoadSegments.Sum(segment =>
            {
                var roadDefinition = config.ResolveRoadType(segment.RoadTypeId, segment.Lanes);
                return (decimal)segment.Length * roadDefinition.MaintenanceCostPerUnit;
            }),
            2);
        state.Budget.DailyRoadMaintenanceCost = Math.Round(
            state.Budget.DailyRoadMaintenanceCost * (decimal)CityEventModel.GetRoadMaintenanceMultiplier(state),
            2);
        state.Budget.LastDailyNet = state.Budget.DailyIncome - state.Budget.DailyServiceCost - state.Budget.DailyRoadMaintenanceCost;
        state.Budget.Cash += state.Budget.LastDailyNet * (decimal)Math.Max(dt, config.Economy.MinimumBudgetDeltaMultiplier);
        state.Budget.DailyConstructionCost = 0m;
    }

    private static BuildingState SpawnBuilding(WorldState state, ZoneLot lot, ZoneDefinition definition)
    {
        return new BuildingState
        {
            Id = DeterministicIdGenerator.Next(state, "building"),
            LotId = lot.Id,
            Cell = lot.Cell,
            ZoneType = lot.ZoneType,
            Level = 1,
            Residents = definition.BaseResidents,
            Jobs = definition.BaseJobs,
            DistrictVitality = lot.DistrictVitality
        };
    }
}
}

namespace PampaSkylines.Simulation
{
using System;
using System.Linq;
using PampaSkylines.Core;

public static class UtilityCoverageModel
{
    public static void Update(WorldState state, SimulationConfig config)
    {
        if (state.Lots.Count == 0)
        {
            state.Utilities = new UtilityState();
            return;
        }

        var serviceDefinitions = config.Services.ServiceDefinitions
            .OrderBy(definition => definition.ServiceType)
            .ToList();

        foreach (var lot in state.Lots.OrderBy(static lot => lot.Cell.X).ThenBy(static lot => lot.Cell.Y))
        {
            lot.HasRoadAccess = state.RoadNodes.Any(node => node.Position.ManhattanDistance(lot.Cell) <= 1);
            lot.HasElectricity = HasCoverage(state, lot, config.ResolveService(ServiceType.Electricity));
            lot.HasWater = HasCoverage(state, lot, config.ResolveService(ServiceType.Water));
            lot.HasSewage = HasCoverage(state, lot, config.ResolveService(ServiceType.Sewage));
            lot.HasWaste = HasCoverage(state, lot, config.ResolveService(ServiceType.Waste));

            var serviceBonus = serviceDefinitions
                .Where(static definition => !definition.CountsAsUtility)
                .Where(definition => HasCoverage(state, lot, definition))
                .Sum(definition => definition.LandValueBonus);

            lot.LandValue = Math.Clamp(
                config.Economy.BaseLandValue
                + (lot.HasRoadAccess ? config.Economy.RoadAccessBonus : config.Economy.RoadAccessPenalty)
                + (lot.HasElectricity ? config.Economy.ElectricityBonus : 0f)
                + (lot.HasWater ? config.Economy.WaterBonus : 0f)
                + (lot.HasSewage ? config.Economy.SewageBonus : 0f)
                + (lot.HasWaste ? config.Economy.WasteBonus : config.Economy.MissingWastePenalty)
                + serviceBonus,
                0f,
                1f);
        }

        state.Utilities.ElectricityCoverage = state.Lots.Count(lot => lot.HasElectricity) / (float)state.Lots.Count;
        state.Utilities.WaterCoverage = state.Lots.Count(lot => lot.HasWater) / (float)state.Lots.Count;
        state.Utilities.SewageCoverage = state.Lots.Count(lot => lot.HasSewage) / (float)state.Lots.Count;
        state.Utilities.WasteCoverage = state.Lots.Count(lot => lot.HasWaste) / (float)state.Lots.Count;
        state.Utilities.AverageServiceCoverage =
            (state.Utilities.ElectricityCoverage + state.Utilities.WaterCoverage + state.Utilities.SewageCoverage + state.Utilities.WasteCoverage) / 4f;

        state.Budget.DailyServiceCost = Math.Round(
            state.Buildings
                .Where(static building => building.ServiceType != ServiceType.None)
                .Sum(building => config.ResolveService(building.ServiceType).DailyUpkeep),
            2);
        state.Budget.DailyServiceCost = Math.Round(
            state.Budget.DailyServiceCost * (decimal)CityEventModel.GetServiceCostMultiplier(state),
            2);
    }

    private static bool HasCoverage(WorldState state, ZoneLot lot, ServiceDefinition definition)
    {
        return state.Buildings.Any(building =>
            building.ServiceType == definition.ServiceType &&
            building.Cell.ManhattanDistance(lot.Cell) <= Math.Max(definition.DefaultCoverageRadius, building.CoverageRadius));
    }
}
}

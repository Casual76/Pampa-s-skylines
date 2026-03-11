namespace PampaSkylines.Simulation
{
using System;
using System.Linq;
using PampaSkylines.Core;

public static class DistrictVitalityModel
{
    public static void Update(WorldState state, SimulationConfig config)
    {
        if (state.Lots.Count == 0)
        {
            state.DemoRun.AverageDistrictVitality = 0f;
            state.DemoRun.ServicePressure = Math.Clamp(1f - state.Utilities.AverageServiceCoverage, 0f, 1f);
            state.DemoRun.TrafficPressure = Math.Clamp(state.AverageTrafficCongestion / 0.95f, 0f, 1f);
            state.DemoRun.EconomicPressure = ResolveEconomicPressure(state);
            return;
        }

        var serviceCoverage = Math.Clamp(state.Utilities.AverageServiceCoverage, 0f, 1f);
        var averageTraffic = Math.Clamp(state.AverageTrafficCongestion, 0f, 1.4f);
        var sumVitality = 0f;

        foreach (var lot in state.Lots.OrderBy(static lot => lot.Cell.X).ThenBy(static lot => lot.Cell.Y))
        {
            var utilityReadiness = ResolveUtilityReadiness(lot);
            var taxRate = ResolveTaxRateForLot(state, lot);
            var taxPenalty = ResolveTaxPenalty(taxRate, config.Economy);
            var trafficPenalty = Math.Clamp(averageTraffic * 0.32f, 0f, 0.60f);
            if (!lot.HasRoadAccess)
            {
                trafficPenalty = Math.Min(0.70f, trafficPenalty + 0.18f);
            }

            var vitality =
                0.12f +
                (utilityReadiness * 0.42f) +
                (serviceCoverage * 0.18f) +
                (Math.Clamp(lot.LandValue, 0f, 1f) * config.Economy.DistrictVitalityLandValueWeight) -
                taxPenalty -
                trafficPenalty;

            lot.DistrictVitality = Math.Clamp(vitality, 0f, 1f);
            sumVitality += lot.DistrictVitality;
        }

        var averageVitality = sumVitality / state.Lots.Count;
        state.DemoRun.AverageDistrictVitality = Math.Clamp(averageVitality, 0f, 1f);
        state.DemoRun.ServicePressure = Math.Clamp(1f - serviceCoverage, 0f, 1f);
        state.DemoRun.TrafficPressure = Math.Clamp(averageTraffic / 0.95f, 0f, 1f);
        state.DemoRun.EconomicPressure = ResolveEconomicPressure(state);

        foreach (var building in state.Buildings)
        {
            var parentLot = state.Lots.FirstOrDefault(lot => lot.Id == building.LotId);
            building.DistrictVitality = parentLot?.DistrictVitality ?? state.DemoRun.AverageDistrictVitality;
        }
    }

    private static float ResolveUtilityReadiness(ZoneLot lot)
    {
        var points = 0f;
        points += lot.HasRoadAccess ? 1f : 0f;
        points += lot.HasElectricity ? 1f : 0f;
        points += lot.HasWater ? 1f : 0f;
        points += lot.HasSewage ? 1f : 0f;
        points += lot.HasWaste ? 1f : 0f;
        return points / 5f;
    }

    private static decimal ResolveTaxRateForLot(WorldState state, ZoneLot lot)
    {
        return lot.ZoneType switch
        {
            ZoneType.Residential => state.Budget.TaxRateResidential,
            ZoneType.Commercial => state.Budget.TaxRateCommercial,
            ZoneType.Industrial => state.Budget.TaxRateIndustrial,
            ZoneType.Office => state.Budget.TaxRateOffice,
            _ => 0m
        };
    }

    private static float ResolveTaxPenalty(decimal taxRate, EconomyConfig config)
    {
        var value = (float)taxRate;
        var soft = Math.Max(0f, config.StrategicTaxSoftThreshold);
        var hard = Math.Max(soft, config.StrategicTaxHardThreshold);
        if (value <= soft)
        {
            return 0f;
        }

        var softPenalty = Math.Max(0f, Math.Min(value, hard) - soft) * config.StrategicTaxDemandPenaltyPerPoint;
        var hardPenalty = value > hard
            ? (value - hard) * config.StrategicTaxDemandPenaltyPerPoint * 1.45f
            : 0f;
        return Math.Clamp(softPenalty + hardPenalty, 0f, 0.85f);
    }

    private static float ResolveEconomicPressure(WorldState state)
    {
        var net = state.Budget.LastDailyNet;
        var cash = state.Budget.Cash;
        var pressure = 0f;
        if (net < 0m)
        {
            pressure += Math.Clamp((float)(Math.Abs(net) / 6000m), 0f, 0.70f);
        }

        if (cash < 0m)
        {
            pressure += Math.Clamp((float)(Math.Abs(cash) / 35000m), 0f, 0.70f);
        }

        return Math.Clamp(pressure, 0f, 1f);
    }
}
}

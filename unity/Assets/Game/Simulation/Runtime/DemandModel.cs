namespace PampaSkylines.Simulation
{
using System;
using System.Linq;
using PampaSkylines.Core;

public static class DemandModel
{
    public static void Update(WorldState state, SimulationConfig config)
    {
        var housingCapacity = state.Buildings
            .Where(static building => building.ZoneType == ZoneType.Residential)
            .Sum(static building => Math.Max(1, building.Residents));

        var commercialJobs = state.Buildings
            .Where(static building => building.ZoneType == ZoneType.Commercial)
            .Sum(static building => building.Jobs);

        var industrialJobs = state.Buildings
            .Where(static building => building.ZoneType == ZoneType.Industrial)
            .Sum(static building => building.Jobs);

        var officeJobs = state.Buildings
            .Where(static building => building.ZoneType == ZoneType.Office)
            .Sum(static building => building.Jobs);

        var unemploymentPressure = state.Population == 0
            ? config.Economy.ZeroPopulationUnemploymentPressure
            : Math.Clamp(1f - ((float)state.Jobs / Math.Max(1, state.Population)), 0f, 1f);

        var residentialTaxPenalty = ResolveStrategicTaxPenalty(state.Budget.TaxRateResidential, config);
        var commercialTaxPenalty = ResolveStrategicTaxPenalty(state.Budget.TaxRateCommercial, config);
        var industrialTaxPenalty = ResolveStrategicTaxPenalty(state.Budget.TaxRateIndustrial, config);
        var officeTaxPenalty = ResolveStrategicTaxPenalty(state.Budget.TaxRateOffice, config);
        var averageVitality = Math.Clamp(state.DemoRun.AverageDistrictVitality, 0f, 1f);
        var vitalityDemandBoost = (averageVitality - 0.5f) * config.Economy.DistrictVitalityGrowthWeight;

        state.Demand.Residential = Math.Clamp(
            config.Economy.ResidentialBaseDemand
            + unemploymentPressure
            - ((float)state.Budget.TaxRateResidential * config.Economy.ResidentialTaxSensitivity),
            0f,
            1f);
        state.Demand.Residential = Math.Clamp(
            state.Demand.Residential - residentialTaxPenalty + vitalityDemandBoost,
            0f,
            1f) * CityEventModel.GetDemandMultiplier(state, ZoneType.Residential);
        state.Demand.Residential = Math.Clamp(state.Demand.Residential, 0f, 1f);

        state.Demand.Commercial = Math.Clamp(
            config.Economy.CommercialBaseDemand
            + (state.Population / config.Economy.CommercialPopulationDemandDivisor)
            - ((float)state.Budget.TaxRateCommercial * config.Economy.CommercialTaxSensitivity)
            - (commercialJobs / config.Economy.CommercialJobSaturationDivisor),
            0f,
            1f);
        state.Demand.Commercial = Math.Clamp(
            state.Demand.Commercial - commercialTaxPenalty + (vitalityDemandBoost * 0.7f),
            0f,
            1f) * CityEventModel.GetDemandMultiplier(state, ZoneType.Commercial);
        state.Demand.Commercial = Math.Clamp(state.Demand.Commercial, 0f, 1f);

        state.Demand.Industrial = Math.Clamp(
            config.Economy.IndustrialBaseDemand
            + unemploymentPressure
            - ((float)state.Budget.TaxRateIndustrial * config.Economy.IndustrialTaxSensitivity)
            - (industrialJobs / config.Economy.IndustrialJobSaturationDivisor),
            0f,
            1f);
        state.Demand.Industrial = Math.Clamp(
            state.Demand.Industrial - industrialTaxPenalty + (vitalityDemandBoost * 0.45f),
            0f,
            1f) * CityEventModel.GetDemandMultiplier(state, ZoneType.Industrial);
        state.Demand.Industrial = Math.Clamp(state.Demand.Industrial, 0f, 1f);

        state.Demand.Office = Math.Clamp(
            config.Economy.OfficeBaseDemand
            + (state.Utilities.AverageServiceCoverage * config.Economy.OfficeServiceCoverageWeight)
            - ((float)state.Budget.TaxRateOffice * config.Economy.OfficeTaxSensitivity)
            - (officeJobs / config.Economy.OfficeJobSaturationDivisor),
            0f,
            1f);
        state.Demand.Office = Math.Clamp(
            state.Demand.Office - officeTaxPenalty + vitalityDemandBoost,
            0f,
            1f) * CityEventModel.GetDemandMultiplier(state, ZoneType.Office);
        state.Demand.Office = Math.Clamp(state.Demand.Office, 0f, 1f);

        var rawIncome = Math.Round(
            (housingCapacity * state.Budget.TaxRateResidential)
            + (commercialJobs * state.Budget.TaxRateCommercial)
            + (industrialJobs * state.Budget.TaxRateIndustrial)
            + (officeJobs * state.Budget.TaxRateOffice),
            2);
        var vitalityIncomeMultiplier = Math.Clamp(
            1f + ((averageVitality - 0.5f) * config.Economy.DistrictVitalityIncomeWeight),
            0.70f,
            1.35f);
        var strategicTaxIncomePenalty = Math.Clamp(
            1f - ((residentialTaxPenalty + commercialTaxPenalty + industrialTaxPenalty + officeTaxPenalty) * config.Economy.StrategicTaxIncomePenaltyPerPoint * 0.25f),
            0.55f,
            1f);

        state.Budget.DailyIncome = Math.Round(
            rawIncome * (decimal)vitalityIncomeMultiplier * (decimal)strategicTaxIncomePenalty * (decimal)CityEventModel.GetTaxIncomeMultiplier(state),
            2);
    }

    private static float ResolveStrategicTaxPenalty(decimal taxRate, SimulationConfig config)
    {
        var value = (float)taxRate;
        var soft = Math.Max(0f, config.Economy.StrategicTaxSoftThreshold);
        var hard = Math.Max(soft, config.Economy.StrategicTaxHardThreshold);
        if (value <= soft)
        {
            return 0f;
        }

        var penalty = (Math.Min(value, hard) - soft) * config.Economy.StrategicTaxDemandPenaltyPerPoint;
        if (value > hard)
        {
            penalty += (value - hard) * config.Economy.StrategicTaxDemandPenaltyPerPoint * 1.65f;
        }

        return Math.Clamp(penalty, 0f, 0.75f);
    }
}
}

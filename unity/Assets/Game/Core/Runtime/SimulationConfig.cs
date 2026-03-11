#nullable enable

namespace PampaSkylines.Core
{
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SimulationConfig
{
    public string Version { get; set; } = "v1";

    public RoadCatalog Roads { get; set; } = new();

    public ServiceCatalog Services { get; set; } = new();

    public ZoneCatalog Zones { get; set; } = new();

    public EconomyConfig Economy { get; set; } = new();

    public ProgressionCatalog Progression { get; set; } = ProgressionCatalog.CreateDefault();

    public EventCatalog Events { get; set; } = EventCatalog.CreateDefault();

    public RoadTypeDefinition ResolveRoadType(string? roadTypeId, int requestedLanes)
    {
        if (!string.IsNullOrWhiteSpace(roadTypeId))
        {
            var exact = Roads.RoadTypes.FirstOrDefault(definition => definition.Id == roadTypeId);
            if (exact is not null)
            {
                return exact;
            }
        }

        var byLanes = Roads.RoadTypes
            .OrderBy(definition => Math.Abs(definition.Lanes - requestedLanes))
            .ThenBy(definition => definition.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (byLanes is not null)
        {
            return byLanes;
        }

        return Roads.RoadTypes.First(definition => definition.Id == Roads.DefaultRoadTypeId);
    }

    public ServiceDefinition ResolveService(ServiceType serviceType)
    {
        return Services.ServiceDefinitions.First(definition => definition.ServiceType == serviceType);
    }

    public ZoneDefinition ResolveZone(ZoneType zoneType)
    {
        return Zones.ZoneDefinitions.First(definition => definition.ZoneType == zoneType);
    }

    public bool SupportsZone(ZoneType zoneType)
    {
        return Zones.ZoneDefinitions.Exists(definition => definition.ZoneType == zoneType);
    }

    public bool SupportsService(ServiceType serviceType)
    {
        return Services.ServiceDefinitions.Exists(definition => definition.ServiceType == serviceType);
    }

    public bool SupportsRoadType(string? roadTypeId, int requestedLanes)
    {
        if (!string.IsNullOrWhiteSpace(roadTypeId))
        {
            return Roads.RoadTypes.Exists(definition => definition.Id == roadTypeId);
        }

        return Roads.RoadTypes.Exists(definition => definition.Lanes == requestedLanes);
    }

    public bool IsRoadUnlocked(ProgressionState? progressionState)
    {
        return progressionState?.RoadUnlocked ?? true;
    }

    public bool IsBulldozeUnlocked(ProgressionState? progressionState)
    {
        return progressionState?.BulldozeUnlocked ?? true;
    }

    public bool IsBudgetPolicyUnlocked(ProgressionState? progressionState)
    {
        return progressionState?.BudgetPolicyUnlocked ?? true;
    }

    public bool IsZoneUnlocked(ZoneType zoneType, ProgressionState? progressionState)
    {
        return progressionState?.IsZoneUnlocked(zoneType) ?? true;
    }

    public bool IsServiceUnlocked(ServiceType serviceType, ProgressionState? progressionState)
    {
        return progressionState?.IsServiceUnlocked(serviceType) ?? true;
    }

    public int? GetPopulationRequirementForRoad()
    {
        return Progression.GetPopulationRequirementForRoad();
    }

    public int? GetPopulationRequirementForBulldoze()
    {
        return Progression.GetPopulationRequirementForBulldoze();
    }

    public int? GetPopulationRequirementForBudgetPolicy()
    {
        return Progression.GetPopulationRequirementForBudgetPolicy();
    }

    public int? GetPopulationRequirementForZone(ZoneType zoneType)
    {
        return Progression.GetPopulationRequirementForZone(zoneType);
    }

    public int? GetPopulationRequirementForService(ServiceType serviceType)
    {
        return Progression.GetPopulationRequirementForService(serviceType);
    }

    public static SimulationConfig CreateFallback()
    {
        return new SimulationConfig
        {
            Version = "fallback-v4",
            Roads = new RoadCatalog
            {
                DefaultRoadTypeId = "road-2lane",
                RoadTypes = new List<RoadTypeDefinition>
                {
                    new()
                    {
                        Id = "road-2lane",
                        Lanes = 2,
                        CapacityPerLane = 600,
                        BuildCostPerUnit = 18m,
                        MaintenanceCostPerUnit = 0.75m,
                        RefundFactor = 0.22m
                    },
                    new()
                    {
                        Id = "road-4lane",
                        Lanes = 4,
                        CapacityPerLane = 700,
                        BuildCostPerUnit = 32m,
                        MaintenanceCostPerUnit = 1.40m,
                        RefundFactor = 0.22m
                    }
                }
            },
            Services = new ServiceCatalog
            {
                ServiceDefinitions = new List<ServiceDefinition>
                {
                    ServiceDefinition.Utility(ServiceType.Electricity, 1500m, 15m, 6f, 0.35m),
                    ServiceDefinition.Utility(ServiceType.Water, 1200m, 12m, 6f, 0.35m),
                    ServiceDefinition.Utility(ServiceType.Sewage, 1200m, 12m, 6f, 0.35m),
                    ServiceDefinition.Utility(ServiceType.Waste, 1800m, 18m, 6f, 0.35m),
                    ServiceDefinition.Civic(ServiceType.Fire, 5000m, 50m, 8f, 0.35m, 0.05f),
                    ServiceDefinition.Civic(ServiceType.Police, 5200m, 52m, 8f, 0.35m, 0.05f),
                    ServiceDefinition.Civic(ServiceType.Health, 6500m, 65m, 8f, 0.35m, 0.05f),
                    ServiceDefinition.Civic(ServiceType.Education, 7000m, 70m, 8f, 0.35m, 0.05f)
                }
            },
            Zones = new ZoneCatalog
            {
                ZoneDefinitions = new List<ZoneDefinition>
                {
                    ZoneDefinition.Create(ZoneType.Residential, 1f, 2f, 3, 12, 0, 8, 0),
                    ZoneDefinition.Create(ZoneType.Commercial, 1f, 2f, 3, 0, 8, 0, 6),
                    ZoneDefinition.Create(ZoneType.Industrial, 1f, 2f, 3, 0, 10, 0, 6),
                    ZoneDefinition.Create(ZoneType.Office, 1f, 2f, 3, 0, 12, 0, 6)
                }
            },
            Economy = new EconomyConfig(),
            Progression = ProgressionCatalog.CreateDefault(),
            Events = EventCatalog.CreateDefault()
        };
    }
}

public sealed class RoadCatalog
{
    public string DefaultRoadTypeId { get; set; } = "road-2lane";

    public List<RoadTypeDefinition> RoadTypes { get; set; } = new();
}

public sealed class RoadTypeDefinition
{
    public string Id { get; set; } = string.Empty;

    public int Lanes { get; set; } = 2;

    public int CapacityPerLane { get; set; } = 600;

    public decimal BuildCostPerUnit { get; set; } = 18m;

    public decimal MaintenanceCostPerUnit { get; set; } = 0.75m;

    public decimal RefundFactor { get; set; } = 0.22m;
}

public sealed class ServiceCatalog
{
    public List<ServiceDefinition> ServiceDefinitions { get; set; } = new();
}

public sealed class ServiceDefinition
{
    public ServiceType ServiceType { get; set; }

    public decimal BuildCost { get; set; }

    public decimal DailyUpkeep { get; set; }

    public float DefaultCoverageRadius { get; set; } = 6f;

    public decimal RefundFactor { get; set; } = 0.35m;

    public float LandValueBonus { get; set; }

    public bool CountsAsUtility { get; set; }

    public static ServiceDefinition Utility(ServiceType serviceType, decimal buildCost, decimal dailyUpkeep, float radius, decimal refundFactor)
    {
        return new ServiceDefinition
        {
            ServiceType = serviceType,
            BuildCost = buildCost,
            DailyUpkeep = dailyUpkeep,
            DefaultCoverageRadius = radius,
            RefundFactor = refundFactor,
            CountsAsUtility = true
        };
    }

    public static ServiceDefinition Civic(ServiceType serviceType, decimal buildCost, decimal dailyUpkeep, float radius, decimal refundFactor, float landValueBonus)
    {
        return new ServiceDefinition
        {
            ServiceType = serviceType,
            BuildCost = buildCost,
            DailyUpkeep = dailyUpkeep,
            DefaultCoverageRadius = radius,
            RefundFactor = refundFactor,
            LandValueBonus = landValueBonus,
            CountsAsUtility = false
        };
    }
}

public sealed class ZoneCatalog
{
    public List<ZoneDefinition> ZoneDefinitions { get; set; } = new();
}

public sealed class ZoneDefinition
{
    public ZoneType ZoneType { get; set; }

    public float SpawnGrowthThreshold { get; set; } = 1f;

    public float UpgradeGrowthThreshold { get; set; } = 2f;

    public int MaxLevel { get; set; } = 3;

    public int BaseResidents { get; set; }

    public int BaseJobs { get; set; }

    public int UpgradeResidents { get; set; }

    public int UpgradeJobs { get; set; }

    public static ZoneDefinition Create(
        ZoneType zoneType,
        float spawnThreshold,
        float upgradeThreshold,
        int maxLevel,
        int baseResidents,
        int baseJobs,
        int upgradeResidents,
        int upgradeJobs)
    {
        return new ZoneDefinition
        {
            ZoneType = zoneType,
            SpawnGrowthThreshold = spawnThreshold,
            UpgradeGrowthThreshold = upgradeThreshold,
            MaxLevel = maxLevel,
            BaseResidents = baseResidents,
            BaseJobs = baseJobs,
            UpgradeResidents = upgradeResidents,
            UpgradeJobs = upgradeJobs
        };
    }
}

public sealed class ProgressionCatalog
{
    public List<ProgressionMilestoneDefinition> Milestones { get; set; } = new();

    public BailoutConfig Bailout { get; set; } = new();

    public int ResolveMilestoneIndexForPopulation(int population)
    {
        if (Milestones.Count == 0)
        {
            return 0;
        }

        var clampedPopulation = Math.Max(0, population);
        var index = 0;
        for (var candidate = 0; candidate < Milestones.Count; candidate++)
        {
            if (clampedPopulation >= Milestones[candidate].RequiredPopulation)
            {
                index = candidate;
            }
        }

        return index;
    }

    public ProgressionMilestoneDefinition ResolveMilestoneForPopulation(int population)
    {
        return ResolveMilestone(ResolveMilestoneIndexForPopulation(population));
    }

    public ProgressionMilestoneDefinition ResolveMilestone(int index)
    {
        if (Milestones.Count == 0)
        {
            return ProgressionMilestoneDefinition.CreateBaseline();
        }

        var clamped = Math.Clamp(index, 0, Milestones.Count - 1);
        return Milestones[clamped];
    }

    public int? GetPopulationRequirementForRoad()
    {
        return Milestones
            .Where(static milestone => milestone.UnlockRoad)
            .Select(static milestone => (int?)milestone.RequiredPopulation)
            .FirstOrDefault();
    }

    public int? GetPopulationRequirementForBulldoze()
    {
        return Milestones
            .Where(static milestone => milestone.UnlockBulldoze)
            .Select(static milestone => (int?)milestone.RequiredPopulation)
            .FirstOrDefault();
    }

    public int? GetPopulationRequirementForBudgetPolicy()
    {
        return Milestones
            .Where(static milestone => milestone.UnlockBudgetPolicy)
            .Select(static milestone => (int?)milestone.RequiredPopulation)
            .FirstOrDefault();
    }

    public int? GetPopulationRequirementForZone(ZoneType zoneType)
    {
        return Milestones
            .Where(milestone => milestone.UnlockZones.Contains(zoneType))
            .Select(static milestone => (int?)milestone.RequiredPopulation)
            .FirstOrDefault();
    }

    public int? GetPopulationRequirementForService(ServiceType serviceType)
    {
        return Milestones
            .Where(milestone => milestone.UnlockServices.Contains(serviceType))
            .Select(static milestone => (int?)milestone.RequiredPopulation)
            .FirstOrDefault();
    }

    public static ProgressionCatalog CreateDefault()
    {
        return new ProgressionCatalog
        {
            Bailout = new BailoutConfig
            {
                CrisisCashThreshold = -5000m,
                CrisisHoursRequired = 6f,
                CashInjection = 15000m,
                LoanIncrease = 18000m,
                CooldownHours = 48f,
                MaxBailouts = 3,
                DailyRepaymentRate = 0.04m
            },
            Milestones = new List<ProgressionMilestoneDefinition>
            {
                new()
                {
                    Id = "m0",
                    DisplayName = "Fondazione",
                    RequiredPopulation = 0,
                    RewardCash = 0m,
                    UnlockRoad = true,
                    UnlockBulldoze = true,
                    UnlockZones = new List<ZoneType>
                    {
                        ZoneType.Residential
                    },
                    UnlockServices = new List<ServiceType>
                    {
                        ServiceType.Electricity,
                        ServiceType.Water,
                        ServiceType.Sewage
                    }
                },
                new()
                {
                    Id = "m1",
                    DisplayName = "Borgo",
                    RequiredPopulation = 80,
                    RewardCash = 8000m,
                    UnlockZones = new List<ZoneType>
                    {
                        ZoneType.Commercial
                    },
                    UnlockServices = new List<ServiceType>
                    {
                        ServiceType.Waste
                    }
                },
                new()
                {
                    Id = "m2",
                    DisplayName = "Distretto Lavoro",
                    RequiredPopulation = 180,
                    RewardCash = 12000m,
                    UnlockZones = new List<ZoneType>
                    {
                        ZoneType.Industrial
                    }
                },
                new()
                {
                    Id = "m3",
                    DisplayName = "Municipio",
                    RequiredPopulation = 320,
                    RewardCash = 15000m,
                    UnlockBudgetPolicy = true,
                    UnlockZones = new List<ZoneType>
                    {
                        ZoneType.Office
                    }
                },
                new()
                {
                    Id = "m4",
                    DisplayName = "Sicurezza",
                    RequiredPopulation = 500,
                    RewardCash = 20000m,
                    UnlockServices = new List<ServiceType>
                    {
                        ServiceType.Fire
                    }
                },
                new()
                {
                    Id = "m5",
                    DisplayName = "Ordine Civico",
                    RequiredPopulation = 700,
                    RewardCash = 25000m,
                    UnlockServices = new List<ServiceType>
                    {
                        ServiceType.Police
                    }
                },
                new()
                {
                    Id = "m6",
                    DisplayName = "Sanita Pubblica",
                    RequiredPopulation = 950,
                    RewardCash = 30000m,
                    UnlockServices = new List<ServiceType>
                    {
                        ServiceType.Health
                    }
                },
                new()
                {
                    Id = "m7",
                    DisplayName = "Citta della Conoscenza",
                    RequiredPopulation = 1250,
                    RewardCash = 40000m,
                    UnlockServices = new List<ServiceType>
                    {
                        ServiceType.Education
                    }
                }
            }
        };
    }
}

public sealed class ProgressionMilestoneDefinition
{
    public string Id { get; set; } = "m0";

    public string DisplayName { get; set; } = "Fondazione";

    public int RequiredPopulation { get; set; }

    public decimal RewardCash { get; set; }

    public bool UnlockRoad { get; set; }

    public bool UnlockBulldoze { get; set; }

    public bool UnlockBudgetPolicy { get; set; }

    public List<ZoneType> UnlockZones { get; set; } = new();

    public List<ServiceType> UnlockServices { get; set; } = new();

    public static ProgressionMilestoneDefinition CreateBaseline()
    {
        return new ProgressionMilestoneDefinition
        {
            Id = "m0",
            DisplayName = "Fondazione",
            RequiredPopulation = 0,
            UnlockRoad = true,
            UnlockBulldoze = true,
            UnlockZones = new List<ZoneType>
            {
                ZoneType.Residential
            },
            UnlockServices = new List<ServiceType>
            {
                ServiceType.Electricity,
                ServiceType.Water,
                ServiceType.Sewage
            }
        };
    }
}

public sealed class BailoutConfig
{
    public decimal CrisisCashThreshold { get; set; } = -5000m;

    public float CrisisHoursRequired { get; set; } = 6f;

    public decimal CashInjection { get; set; } = 15000m;

    public decimal LoanIncrease { get; set; } = 18000m;

    public float CooldownHours { get; set; } = 48f;

    public int MaxBailouts { get; set; } = 3;

    public decimal DailyRepaymentRate { get; set; } = 0.04m;
}

public sealed class EconomyConfig
{
    public float ResidentialBaseDemand { get; set; } = 0.65f;

    public float CommercialBaseDemand { get; set; } = 0.40f;

    public float IndustrialBaseDemand { get; set; } = 0.30f;

    public float OfficeBaseDemand { get; set; } = 0.25f;

    public float ZeroPopulationUnemploymentPressure { get; set; } = 0.50f;

    public float CommercialPopulationDemandDivisor { get; set; } = 1000f;

    public float CommercialJobSaturationDivisor { get; set; } = 500f;

    public float IndustrialJobSaturationDivisor { get; set; } = 600f;

    public float OfficeJobSaturationDivisor { get; set; } = 600f;

    public float OfficeServiceCoverageWeight { get; set; } = 0.40f;

    public float ResidentialTaxSensitivity { get; set; } = 1f;

    public float CommercialTaxSensitivity { get; set; } = 1f;

    public float IndustrialTaxSensitivity { get; set; } = 1f;

    public float OfficeTaxSensitivity { get; set; } = 1f;

    public float GrowthDecayWithoutUtilitiesPerHour { get; set; } = 0.10f;

    public float MinimumBudgetDeltaMultiplier { get; set; } = 0.05f;

    public float CommuteMinutesPerRoadUnit { get; set; } = 2.5f;

    public float BaseLandValue { get; set; } = 0.30f;

    public float RoadAccessBonus { get; set; } = 0.10f;

    public float RoadAccessPenalty { get; set; } = -0.20f;

    public float ElectricityBonus { get; set; } = 0.10f;

    public float WaterBonus { get; set; } = 0.10f;

    public float SewageBonus { get; set; } = 0.08f;

    public float WasteBonus { get; set; } = 0.05f;

    public float MissingWastePenalty { get; set; } = -0.05f;

    public float CivicServiceBonusPerCoverage { get; set; } = 0.05f;

    public float DistrictVitalityGrowthWeight { get; set; } = 0.45f;

    public float DistrictVitalityIncomeWeight { get; set; } = 0.35f;

    public float DistrictVitalityLandValueWeight { get; set; } = 0.25f;

    public float StrategicTaxSoftThreshold { get; set; } = 0.14f;

    public float StrategicTaxHardThreshold { get; set; } = 0.18f;

    public float StrategicTaxDemandPenaltyPerPoint { get; set; } = 1.4f;

    public float StrategicTaxIncomePenaltyPerPoint { get; set; } = 0.8f;

    public int DemoTargetPopulation { get; set; } = 1400;

    public float VictoryMinimumDistrictVitality { get; set; } = 0.62f;

    public float VictoryMinimumServiceCoverage { get; set; } = 0.72f;

    public float VictoryMaximumTrafficCongestion { get; set; } = 0.62f;

    public decimal MinimumTaxRate { get; set; } = 0m;

    public decimal MaximumTaxRate { get; set; } = 0.25m;

    public float MinimumTimeScale { get; set; } = 0f;

    public float MaximumTimeScale { get; set; } = 4f;
}
}

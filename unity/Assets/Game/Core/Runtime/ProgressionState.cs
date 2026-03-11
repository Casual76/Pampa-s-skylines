#nullable enable

namespace PampaSkylines.Core
{
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ProgressionState
{
    public int CurrentMilestoneIndex { get; set; }

    public string CurrentMilestoneId { get; set; } = "m0";

    public string CurrentMilestoneName { get; set; } = "Fondazione";

    public string NextMilestoneId { get; set; } = string.Empty;

    public string NextMilestoneName { get; set; } = string.Empty;

    public int NextMilestonePopulationTarget { get; set; }

    public decimal NextMilestoneRewardCash { get; set; }

    public List<string> ReachedMilestoneIds { get; set; } = new();

    public List<ZoneType> UnlockedZones { get; set; } = new();

    public List<ServiceType> UnlockedServices { get; set; } = new();

    public bool RoadUnlocked { get; set; }

    public bool BulldozeUnlocked { get; set; }

    public bool BudgetPolicyUnlocked { get; set; }

    public decimal TotalMilestoneRewardsAwarded { get; set; }

    public decimal LastMilestoneRewardCash { get; set; }

    public string LastMilestoneUnlockedId { get; set; } = string.Empty;

    public string LastMilestoneUnlockedName { get; set; } = string.Empty;

    public int BailoutCount { get; set; }

    public float CrisisHoursUnderThreshold { get; set; }

    public float TotalSimulatedHours { get; set; }

    public float NextBailoutAvailableAtHour { get; set; }

    public int LastLoanRepaymentDay { get; set; } = 1;

    public bool IsZoneUnlocked(ZoneType zoneType)
    {
        return zoneType == ZoneType.None || UnlockedZones.Contains(zoneType);
    }

    public bool IsServiceUnlocked(ServiceType serviceType)
    {
        return serviceType == ServiceType.None || UnlockedServices.Contains(serviceType);
    }

    public void EnsureCollections()
    {
        ReachedMilestoneIds ??= new List<string>();
        UnlockedZones ??= new List<ZoneType>();
        UnlockedServices ??= new List<ServiceType>();
    }

    public void NormalizeUnlocks()
    {
        EnsureCollections();
        UnlockedZones = UnlockedZones.Distinct().OrderBy(static value => value).ToList();
        UnlockedServices = UnlockedServices.Distinct().OrderBy(static value => value).ToList();
        ReachedMilestoneIds = ReachedMilestoneIds.Distinct(StringComparer.Ordinal).ToList();
    }

    public void RefreshNextMilestone(ProgressionCatalog catalog)
    {
        var nextIndex = Math.Clamp(CurrentMilestoneIndex + 1, 0, Math.Max(0, catalog.Milestones.Count - 1));
        if (catalog.Milestones.Count == 0 || nextIndex <= CurrentMilestoneIndex)
        {
            NextMilestoneId = CurrentMilestoneId;
            NextMilestoneName = CurrentMilestoneName;
            NextMilestonePopulationTarget = catalog.ResolveMilestone(CurrentMilestoneIndex).RequiredPopulation;
            NextMilestoneRewardCash = 0m;
            return;
        }

        var next = catalog.ResolveMilestone(nextIndex);
        NextMilestoneId = next.Id;
        NextMilestoneName = next.DisplayName;
        NextMilestonePopulationTarget = next.RequiredPopulation;
        NextMilestoneRewardCash = next.RewardCash;
    }

    public void ApplyMilestone(ProgressionMilestoneDefinition milestone, bool grantReward, BudgetState budget)
    {
        EnsureCollections();

        CurrentMilestoneId = milestone.Id;
        CurrentMilestoneName = milestone.DisplayName;
        LastMilestoneUnlockedId = milestone.Id;
        LastMilestoneUnlockedName = milestone.DisplayName;
        if (!ReachedMilestoneIds.Contains(milestone.Id, StringComparer.Ordinal))
        {
            ReachedMilestoneIds.Add(milestone.Id);
        }

        if (milestone.UnlockRoad)
        {
            RoadUnlocked = true;
        }

        if (milestone.UnlockBulldoze)
        {
            BulldozeUnlocked = true;
        }

        if (milestone.UnlockBudgetPolicy)
        {
            BudgetPolicyUnlocked = true;
        }

        foreach (var zoneType in milestone.UnlockZones)
        {
            if (zoneType == ZoneType.None)
            {
                continue;
            }

            if (!UnlockedZones.Contains(zoneType))
            {
                UnlockedZones.Add(zoneType);
            }
        }

        foreach (var serviceType in milestone.UnlockServices)
        {
            if (serviceType == ServiceType.None)
            {
                continue;
            }

            if (!UnlockedServices.Contains(serviceType))
            {
                UnlockedServices.Add(serviceType);
            }
        }

        LastMilestoneRewardCash = grantReward ? milestone.RewardCash : 0m;
        if (grantReward && milestone.RewardCash > 0m)
        {
            budget.Cash += milestone.RewardCash;
            TotalMilestoneRewardsAwarded += milestone.RewardCash;
        }

        NormalizeUnlocks();
    }

    public static ProgressionState CreateForPopulation(ProgressionCatalog catalog, int population, bool treatRewardsAsAlreadyGranted)
    {
        var state = new ProgressionState
        {
            RoadUnlocked = false,
            BulldozeUnlocked = false,
            BudgetPolicyUnlocked = false,
            LastLoanRepaymentDay = 1
        };

        var milestoneIndex = catalog.ResolveMilestoneIndexForPopulation(population);
        for (var index = 0; index <= milestoneIndex; index++)
        {
            var milestone = catalog.ResolveMilestone(index);
            state.CurrentMilestoneIndex = index;
            state.ApplyMilestone(milestone, grantReward: false, new BudgetState());
            if (treatRewardsAsAlreadyGranted && milestone.RewardCash > 0m)
            {
                state.TotalMilestoneRewardsAwarded += milestone.RewardCash;
            }
        }

        state.RefreshNextMilestone(catalog);
        return state;
    }
}
}

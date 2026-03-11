#nullable enable

namespace PampaSkylines.Core
{
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class RunState
{
    public int CurrentActIndex { get; set; }

    public string CurrentActId { get; set; } = "act1";

    public string CurrentActName { get; set; } = "Fondazione";

    public string CurrentActObjective { get; set; } = "Raggiungi 320 abitanti.";

    public int CurrentActProgressValue { get; set; }

    public int CurrentActProgressTarget { get; set; } = 320;

    public float CurrentActProgress01 { get; set; }

    public ActiveCityEventState? ActiveEvent { get; set; }

    public List<CityEventHistoryEntry> EventHistory { get; set; } = new();

    public List<EventCooldownState> EventCooldowns { get; set; } = new();

    public List<ActiveTimedModifierState> ActiveModifiers { get; set; } = new();

    public List<PendingConsequenceState> PendingConsequences { get; set; } = new();

    public float NextEventCheckAtHour { get; set; }

    public float FiscalDistressHours { get; set; }

    public int DeficitDays { get; set; }

    public int LastDeficitTrackedDay { get; set; } = 1;

    public bool IsGameOver { get; set; }

    public string GameOverReason { get; set; } = string.Empty;

    public bool IsVictory { get; set; }

    public string VictoryReason { get; set; } = string.Empty;

    public float VictoryAtHour { get; set; }

    public void EnsureCollections()
    {
        EventHistory ??= new List<CityEventHistoryEntry>();
        EventCooldowns ??= new List<EventCooldownState>();
        ActiveModifiers ??= new List<ActiveTimedModifierState>();
        PendingConsequences ??= new List<PendingConsequenceState>();
    }

    public void Normalize()
    {
        EnsureCollections();
        EventCooldowns = EventCooldowns
            .Where(static cooldown => !string.IsNullOrWhiteSpace(cooldown.EventId))
            .GroupBy(cooldown => cooldown.EventId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(static cooldown => cooldown.AvailableAtHour).First())
            .OrderBy(cooldown => cooldown.EventId, StringComparer.Ordinal)
            .ToList();
        ActiveModifiers = ActiveModifiers
            .Where(static modifier => !string.IsNullOrWhiteSpace(modifier.ModifierId))
            .OrderBy(static modifier => modifier.ExpiresAtHour)
            .ToList();
        PendingConsequences = PendingConsequences
            .Where(static consequence => !string.IsNullOrWhiteSpace(consequence.ConsequenceId))
            .OrderBy(static consequence => consequence.ApplyAtHour)
            .ToList();
        EventHistory = EventHistory
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.EventId))
            .OrderByDescending(static entry => entry.ResolvedAtHour)
            .Take(24)
            .ToList();
        VictoryAtHour = Math.Max(0f, VictoryAtHour);
    }

    public void RefreshActProgress(EventCatalog events, int population)
    {
        if (events.Acts.Count == 0)
        {
            var defaultAct = CityActDefinition.CreateDefault();
            CurrentActIndex = 0;
            CurrentActId = defaultAct.Id;
            CurrentActName = defaultAct.DisplayName;
            CurrentActObjective = defaultAct.ObjectiveDescription;
            CurrentActProgressValue = Math.Max(0, population);
            CurrentActProgressTarget = Math.Max(1, defaultAct.ObjectivePopulationTarget);
            CurrentActProgress01 = Math.Clamp(CurrentActProgressValue / (float)CurrentActProgressTarget, 0f, 1f);
            return;
        }

        var resolvedIndex = events.ResolveActIndexForPopulation(population);
        var act = events.Acts[Math.Clamp(resolvedIndex, 0, events.Acts.Count - 1)];
        CurrentActIndex = resolvedIndex;
        CurrentActId = act.Id;
        CurrentActName = act.DisplayName;
        CurrentActObjective = act.ObjectiveDescription;
        CurrentActProgressValue = Math.Max(0, population);
        CurrentActProgressTarget = Math.Max(1, act.ObjectivePopulationTarget);
        CurrentActProgress01 = Math.Clamp(CurrentActProgressValue / (float)CurrentActProgressTarget, 0f, 1f);
    }

    public void NormalizeForPopulation(EventCatalog events, int population, int currentDay)
    {
        EnsureCollections();
        RefreshActProgress(events, population);
        LastDeficitTrackedDay = Math.Max(1, currentDay);
        if (NextEventCheckAtHour < 0f)
        {
            NextEventCheckAtHour = 0f;
        }

        Normalize();
    }

    public static RunState CreateInitial(EventCatalog events, int population, int currentDay)
    {
        var state = new RunState
        {
            LastDeficitTrackedDay = Math.Max(1, currentDay),
            NextEventCheckAtHour = Math.Max(0f, events.SpawnIntervalHours)
        };
        state.NormalizeForPopulation(events, population, currentDay);
        return state;
    }
}

public sealed class ActiveCityEventState
{
    public string EventId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public float TriggeredAtHour { get; set; }

    public List<ActiveCityEventChoiceState> Choices { get; set; } = new();
}

public sealed class ActiveCityEventChoiceState
{
    public string ChoiceId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class CityEventHistoryEntry
{
    public string EventId { get; set; } = string.Empty;

    public string EventTitle { get; set; } = string.Empty;

    public string ChoiceId { get; set; } = string.Empty;

    public string ChoiceLabel { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public float ResolvedAtHour { get; set; }
}

public sealed class EventCooldownState
{
    public string EventId { get; set; } = string.Empty;

    public float AvailableAtHour { get; set; }
}

public sealed class ActiveTimedModifierState
{
    public string ModifierId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public float ExpiresAtHour { get; set; }

    public float ResidentialDemandMultiplier { get; set; } = 1f;

    public float CommercialDemandMultiplier { get; set; } = 1f;

    public float IndustrialDemandMultiplier { get; set; } = 1f;

    public float OfficeDemandMultiplier { get; set; } = 1f;

    public float GrowthMultiplier { get; set; } = 1f;

    public float ServiceCostMultiplier { get; set; } = 1f;

    public float RoadMaintenanceMultiplier { get; set; } = 1f;

    public float CommuteMinutesMultiplier { get; set; } = 1f;

    public float TaxIncomeMultiplier { get; set; } = 1f;
}

public sealed class PendingConsequenceState
{
    public string SourceEventId { get; set; } = string.Empty;

    public string ConsequenceId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public float ApplyAtHour { get; set; }

    public string FollowUpEventId { get; set; } = string.Empty;

    public decimal CashDelta { get; set; }

    public decimal LoanDelta { get; set; }

    public float ResidentialDemandDelta { get; set; }

    public float CommercialDemandDelta { get; set; }

    public float IndustrialDemandDelta { get; set; }

    public float OfficeDemandDelta { get; set; }

    public float LandValueDelta { get; set; }

    public float UtilityCoverageDelta { get; set; }

    public List<TimedModifierDefinition> TimedModifiers { get; set; } = new();
}
}

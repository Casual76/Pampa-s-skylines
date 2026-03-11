#nullable enable

namespace PampaSkylines.Simulation
{
using System;
using System.Collections.Generic;
using System.Linq;
using PampaSkylines.Core;

public static class CityEventModel
{
    private const decimal CollapseCashThreshold = -20000m;
    private const decimal CollapseRecoveryCashThreshold = -15000m;
    private const float CollapseHoursRequired = 24f;

    public static void EnsureInitialized(WorldState state, SimulationConfig config)
    {
        state.RunState ??= RunState.CreateInitial(config.Events, state.Population, state.Time.Day);
        state.RunState.NormalizeForPopulation(config.Events, state.Population, state.Time.Day);
        if (state.RunState.NextEventCheckAtHour <= 0f)
        {
            state.RunState.NextEventCheckAtHour = Math.Max(config.Events.SpawnIntervalHours, 1f);
        }
    }

    public static void Update(WorldState state, float dtHours, SimulationConfig config, SimulationFrameReport report)
    {
        EnsureInitialized(state, config);
        var run = state.RunState;
        var clampedDt = Math.Max(0f, dtHours);
        var totalHours = state.Progression.TotalSimulatedHours;

        RefreshActProgress(state, config.Events, report);
        ExpireModifiers(run, totalHours, report);
        UpdateDeficitCounter(state);
        UpdateEconomicCollapse(state, clampedDt, config.Progression.Bailout, report);
        ApplyPendingConsequences(state, config, totalHours, report);
        TrySpawnEvent(state, config.Events, totalHours, report);
        run.Normalize();
    }

    public static EventChoiceResolutionResult ResolveActiveEventChoice(
        WorldState state,
        SimulationConfig config,
        string? eventId,
        string? choiceId,
        SimulationFrameReport? report = null)
    {
        EnsureInitialized(state, config);
        var run = state.RunState;
        var activeEvent = run.ActiveEvent;
        if (activeEvent is null)
        {
            return EventChoiceResolutionResult.Failed(
                CommandRejectionReason.NoActiveEvent,
                "Nessun evento cittadino attivo da risolvere.");
        }

        if (string.IsNullOrWhiteSpace(eventId) ||
            !string.Equals(activeEvent.EventId, eventId, StringComparison.Ordinal))
        {
            return EventChoiceResolutionResult.Failed(
                CommandRejectionReason.InvalidEventChoice,
                "Scelta evento non valida: evento attivo differente.");
        }

        var eventDefinition = config.Events.FindEvent(activeEvent.EventId);
        if (eventDefinition is null)
        {
            run.ActiveEvent = null;
            return EventChoiceResolutionResult.Failed(
                CommandRejectionReason.InvalidEventChoice,
                "Evento non piu disponibile nel catalogo.");
        }

        var choiceDefinition = eventDefinition.Choices
            .FirstOrDefault(choice => string.Equals(choice.Id, choiceId, StringComparison.Ordinal));
        if (choiceDefinition is null)
        {
            return EventChoiceResolutionResult.Failed(
                CommandRejectionReason.InvalidEventChoice,
                "Scelta evento non riconosciuta.");
        }

        var currentHour = state.Progression.TotalSimulatedHours;
        var effect = choiceDefinition.Effect ?? new EventChoiceEffectDefinition();
        ApplyEffect(
            state,
            effect.CashDelta,
            effect.LoanDelta,
            effect.ResidentialDemandDelta,
            effect.CommercialDemandDelta,
            effect.IndustrialDemandDelta,
            effect.OfficeDemandDelta,
            effect.LandValueDelta,
            effect.UtilityCoverageDelta);
        ApplyTimedModifiers(run, effect.TimedModifiers, currentHour, report);
        ScheduleDelayedConsequences(run, eventDefinition, effect.DelayedConsequences, currentHour, report);

        var summary = BuildChoiceSummary(eventDefinition, choiceDefinition, effect);
        run.EventHistory.Insert(0, new CityEventHistoryEntry
        {
            EventId = eventDefinition.Id,
            EventTitle = eventDefinition.Title,
            ChoiceId = choiceDefinition.Id,
            ChoiceLabel = choiceDefinition.Label,
            Summary = summary,
            ResolvedAtHour = currentHour
        });
        if (run.EventHistory.Count > 24)
        {
            run.EventHistory.RemoveRange(24, run.EventHistory.Count - 24);
        }

        run.ActiveEvent = null;
        run.Normalize();
        AddEvent(report, "event:resolved", summary);
        return EventChoiceResolutionResult.Succeeded(summary);
    }

    public static float GetDemandMultiplier(WorldState state, ZoneType zoneType)
    {
        if (state.RunState?.ActiveModifiers is null || state.RunState.ActiveModifiers.Count == 0)
        {
            return 1f;
        }

        var multiplier = 1f;
        foreach (var modifier in state.RunState.ActiveModifiers)
        {
            multiplier *= zoneType switch
            {
                ZoneType.Residential => modifier.ResidentialDemandMultiplier,
                ZoneType.Commercial => modifier.CommercialDemandMultiplier,
                ZoneType.Industrial => modifier.IndustrialDemandMultiplier,
                ZoneType.Office => modifier.OfficeDemandMultiplier,
                _ => 1f
            };
        }

        return Math.Clamp(multiplier, 0.35f, 2.50f);
    }

    public static float GetGrowthMultiplier(WorldState state)
    {
        return AccumulateModifier(state, static modifier => modifier.GrowthMultiplier);
    }

    public static float GetServiceCostMultiplier(WorldState state)
    {
        return AccumulateModifier(state, static modifier => modifier.ServiceCostMultiplier);
    }

    public static float GetRoadMaintenanceMultiplier(WorldState state)
    {
        return AccumulateModifier(state, static modifier => modifier.RoadMaintenanceMultiplier);
    }

    public static float GetCommuteMinutesMultiplier(WorldState state)
    {
        return AccumulateModifier(state, static modifier => modifier.CommuteMinutesMultiplier);
    }

    public static float GetTaxIncomeMultiplier(WorldState state)
    {
        return AccumulateModifier(state, static modifier => modifier.TaxIncomeMultiplier);
    }

    private static float AccumulateModifier(WorldState state, Func<ActiveTimedModifierState, float> selector)
    {
        if (state.RunState?.ActiveModifiers is null || state.RunState.ActiveModifiers.Count == 0)
        {
            return 1f;
        }

        var multiplier = 1f;
        foreach (var modifier in state.RunState.ActiveModifiers)
        {
            multiplier *= Math.Max(0.05f, selector(modifier));
        }

        return Math.Clamp(multiplier, 0.25f, 3f);
    }

    private static void RefreshActProgress(WorldState state, EventCatalog catalog, SimulationFrameReport report)
    {
        var run = state.RunState;
        var previousActId = run.CurrentActId;
        run.RefreshActProgress(catalog, state.Population);
        if (!string.Equals(previousActId, run.CurrentActId, StringComparison.Ordinal))
        {
            AddEvent(
                report,
                "milestone:act",
                $"Nuovo atto: {run.CurrentActName}. Obiettivo: {run.CurrentActObjective}");
        }
    }

    private static void ExpireModifiers(RunState run, float totalHours, SimulationFrameReport report)
    {
        if (run.ActiveModifiers.Count == 0)
        {
            return;
        }

        var expired = run.ActiveModifiers
            .Where(modifier => modifier.ExpiresAtHour <= totalHours)
            .ToList();
        if (expired.Count == 0)
        {
            return;
        }

        run.ActiveModifiers.RemoveAll(modifier => modifier.ExpiresAtHour <= totalHours);
        foreach (var modifier in expired)
        {
            AddEvent(report, "event:modifier_end", $"Effetto terminato: {modifier.Label}.");
        }
    }

    private static void UpdateDeficitCounter(WorldState state)
    {
        var run = state.RunState;
        var currentDay = Math.Max(1, state.Time.Day);
        if (run.LastDeficitTrackedDay <= 0)
        {
            run.LastDeficitTrackedDay = currentDay;
            return;
        }

        if (currentDay <= run.LastDeficitTrackedDay)
        {
            return;
        }

        var deficit = state.Budget.LastDailyNet < 0m || state.Budget.Cash < 0m;
        run.DeficitDays = deficit ? run.DeficitDays + (currentDay - run.LastDeficitTrackedDay) : 0;
        run.LastDeficitTrackedDay = currentDay;
    }

    private static void UpdateEconomicCollapse(WorldState state, float dtHours, BailoutConfig bailout, SimulationFrameReport report)
    {
        var run = state.RunState;
        if (run.IsGameOver || dtHours <= 0f)
        {
            return;
        }

        var previousDistressHours = run.FiscalDistressHours;
        if (state.Budget.Cash <= CollapseCashThreshold && state.Progression.BailoutCount >= bailout.MaxBailouts)
        {
            run.FiscalDistressHours += dtHours;
        }
        else if (state.Budget.Cash > CollapseRecoveryCashThreshold)
        {
            if (run.FiscalDistressHours > 0f)
            {
                AddEvent(report, "economy:recovery", "Rischio collasso rientrato: cassa tornata sopra -15.000.");
            }

            run.FiscalDistressHours = 0f;
        }

        EmitFiscalRiskAlerts(previousDistressHours, run.FiscalDistressHours, report);

        if (run.FiscalDistressHours < CollapseHoursRequired)
        {
            return;
        }

        run.IsGameOver = true;
        run.GameOverReason = "Collasso economico: cassa sotto -20.000 per 24 ore con bailout esauriti.";
        run.ActiveEvent = null;
        AddEvent(report, "alert:gameover", $"GAME OVER ECONOMICO. {run.GameOverReason}");
    }

    private static void EmitFiscalRiskAlerts(float previousHours, float currentHours, SimulationFrameReport report)
    {
        if (currentHours <= previousHours)
        {
            return;
        }

        TryEmitFiscalRiskAlert(
            previousHours,
            currentHours,
            8f,
            report,
            "Allerta bilancio: crisi prolungata (8h). Riduci costi o aumenta entrate.");
        TryEmitFiscalRiskAlert(
            previousHours,
            currentHours,
            16f,
            report,
            "Allerta grave: rischio collasso oltre meta soglia (16h/24h).");
        TryEmitFiscalRiskAlert(
            previousHours,
            currentHours,
            22f,
            report,
            "Allerta critica: collasso economico vicino (22h/24h).");
    }

    private static void TryEmitFiscalRiskAlert(
        float previousHours,
        float currentHours,
        float thresholdHours,
        SimulationFrameReport report,
        string message)
    {
        if (previousHours < thresholdHours && currentHours >= thresholdHours)
        {
            AddEvent(report, "alert:fiscal", message);
        }
    }

    private static void TrySpawnEvent(WorldState state, EventCatalog catalog, float totalHours, SimulationFrameReport report)
    {
        var run = state.RunState;
        if (run.IsGameOver || run.ActiveEvent is not null || catalog.Events.Count == 0)
        {
            return;
        }

        var interval = Math.Max(1f, catalog.SpawnIntervalHours);
        if (run.NextEventCheckAtHour <= 0f)
        {
            run.NextEventCheckAtHour = interval;
        }

        while (totalHours >= run.NextEventCheckAtHour && run.ActiveEvent is null)
        {
            run.NextEventCheckAtHour += interval;
            var selected = SelectEventCandidate(state, catalog, totalHours);
            if (selected is null)
            {
                continue;
            }

            ActivateEvent(run, selected, totalHours);
            AddEvent(report, "event:spawn", $"Consiglio cittadino: {selected.Title}");
        }
    }

    private static CityEventDefinition? SelectEventCandidate(WorldState state, EventCatalog catalog, float totalHours)
    {
        var run = state.RunState;
        var cooldownMap = run.EventCooldowns
            .ToDictionary(cooldown => cooldown.EventId, cooldown => cooldown.AvailableAtHour, StringComparer.Ordinal);

        var candidates = catalog.Events
            .Where(candidate => candidate.Choices.Count > 0)
            .Where(candidate => state.Population >= candidate.MinPopulation)
            .Where(candidate => candidate.MaxPopulation <= 0 || state.Population <= candidate.MaxPopulation)
            .Where(candidate => run.CurrentActIndex >= candidate.MinActIndex)
            .Where(candidate => !cooldownMap.TryGetValue(candidate.Id, out var availableAt) || totalHours >= availableAt)
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var totalWeight = candidates.Sum(static candidate => Math.Max(1, candidate.Weight));
        var roll = (int)(ComputeDeterministicSeed(state, totalHours) % (uint)totalWeight);
        var cursor = 0;
        foreach (var candidate in candidates)
        {
            cursor += Math.Max(1, candidate.Weight);
            if (roll < cursor)
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    private static void ActivateEvent(RunState run, CityEventDefinition definition, float totalHours)
    {
        run.ActiveEvent = new ActiveCityEventState
        {
            EventId = definition.Id,
            Title = definition.Title,
            Description = definition.Description,
            TriggeredAtHour = totalHours,
            Choices = definition.Choices
                .Select(choice => new ActiveCityEventChoiceState
                {
                    ChoiceId = choice.Id,
                    Label = choice.Label,
                    Description = choice.Description
                })
                .ToList()
        };

        run.EventCooldowns.RemoveAll(cooldown => string.Equals(cooldown.EventId, definition.Id, StringComparison.Ordinal));
        run.EventCooldowns.Add(new EventCooldownState
        {
            EventId = definition.Id,
            AvailableAtHour = totalHours + Math.Max(0f, definition.CooldownHours)
        });
    }

    private static uint ComputeDeterministicSeed(WorldState state, float totalHours)
    {
        unchecked
        {
            var hash = 2166136261u;
            hash = (hash ^ (uint)state.Tick) * 16777619u;
            hash = (hash ^ (uint)Math.Round(totalHours * 100f)) * 16777619u;
            hash = (hash ^ (uint)state.Population) * 16777619u;
            hash = (hash ^ (uint)state.RunState.EventHistory.Count) * 16777619u;
            foreach (var character in state.CityId)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return hash;
        }
    }

    private static void ApplyEffect(
        WorldState state,
        decimal cashDelta,
        decimal loanDelta,
        float residentialDemandDelta,
        float commercialDemandDelta,
        float industrialDemandDelta,
        float officeDemandDelta,
        float landValueDelta,
        float utilityCoverageDelta)
    {
        state.Budget.Cash += cashDelta;
        state.Budget.LoanBalance = Math.Max(0m, state.Budget.LoanBalance + loanDelta);

        state.Demand.Residential = Math.Clamp(state.Demand.Residential + residentialDemandDelta, 0f, 1f);
        state.Demand.Commercial = Math.Clamp(state.Demand.Commercial + commercialDemandDelta, 0f, 1f);
        state.Demand.Industrial = Math.Clamp(state.Demand.Industrial + industrialDemandDelta, 0f, 1f);
        state.Demand.Office = Math.Clamp(state.Demand.Office + officeDemandDelta, 0f, 1f);

        if (Math.Abs(landValueDelta) > 0.0001f)
        {
            foreach (var lot in state.Lots)
            {
                lot.LandValue = Math.Clamp(lot.LandValue + landValueDelta, 0f, 1f);
            }
        }

        if (Math.Abs(utilityCoverageDelta) > 0.0001f)
        {
            state.Utilities.ElectricityCoverage = Math.Clamp(state.Utilities.ElectricityCoverage + utilityCoverageDelta, 0f, 1f);
            state.Utilities.WaterCoverage = Math.Clamp(state.Utilities.WaterCoverage + utilityCoverageDelta, 0f, 1f);
            state.Utilities.SewageCoverage = Math.Clamp(state.Utilities.SewageCoverage + utilityCoverageDelta, 0f, 1f);
            state.Utilities.WasteCoverage = Math.Clamp(state.Utilities.WasteCoverage + utilityCoverageDelta, 0f, 1f);
            state.Utilities.AverageServiceCoverage = (
                state.Utilities.ElectricityCoverage +
                state.Utilities.WaterCoverage +
                state.Utilities.SewageCoverage +
                state.Utilities.WasteCoverage) / 4f;
        }
    }

    private static void ScheduleDelayedConsequences(
        RunState run,
        CityEventDefinition eventDefinition,
        IReadOnlyList<DelayedConsequenceDefinition> delayedConsequences,
        float currentHour,
        SimulationFrameReport? report)
    {
        if (delayedConsequences.Count == 0)
        {
            return;
        }

        foreach (var consequence in delayedConsequences)
        {
            if (string.IsNullOrWhiteSpace(consequence.Id))
            {
                continue;
            }

            run.PendingConsequences.Add(new PendingConsequenceState
            {
                SourceEventId = eventDefinition.Id,
                ConsequenceId = consequence.Id,
                Label = string.IsNullOrWhiteSpace(consequence.Label) ? consequence.Id : consequence.Label,
                ApplyAtHour = currentHour + Math.Max(0.25f, consequence.DelayHours),
                FollowUpEventId = consequence.FollowUpEventId ?? string.Empty,
                CashDelta = consequence.CashDelta,
                LoanDelta = consequence.LoanDelta,
                ResidentialDemandDelta = consequence.ResidentialDemandDelta,
                CommercialDemandDelta = consequence.CommercialDemandDelta,
                IndustrialDemandDelta = consequence.IndustrialDemandDelta,
                OfficeDemandDelta = consequence.OfficeDemandDelta,
                LandValueDelta = consequence.LandValueDelta,
                UtilityCoverageDelta = consequence.UtilityCoverageDelta,
                TimedModifiers = consequence.TimedModifiers
                    .Select(CloneModifier)
                    .ToList()
            });

            AddEvent(
                report,
                "event:delayed_scheduled",
                $"Conseguenza in coda: {consequence.Label} (tra {Math.Max(0.25f, consequence.DelayHours):0.0}h).");
        }
    }

    private static void ApplyPendingConsequences(WorldState state, SimulationConfig config, float totalHours, SimulationFrameReport report)
    {
        var run = state.RunState;
        if (run.PendingConsequences.Count == 0)
        {
            return;
        }

        var dueConsequences = run.PendingConsequences
            .Where(consequence => consequence.ApplyAtHour <= totalHours)
            .OrderBy(consequence => consequence.ApplyAtHour)
            .ToList();
        if (dueConsequences.Count == 0)
        {
            return;
        }

        run.PendingConsequences.RemoveAll(consequence => consequence.ApplyAtHour <= totalHours);
        foreach (var consequence in dueConsequences)
        {
            ApplyEffect(
                state,
                consequence.CashDelta,
                consequence.LoanDelta,
                consequence.ResidentialDemandDelta,
                consequence.CommercialDemandDelta,
                consequence.IndustrialDemandDelta,
                consequence.OfficeDemandDelta,
                consequence.LandValueDelta,
                consequence.UtilityCoverageDelta);

            ApplyTimedModifiers(run, consequence.TimedModifiers, totalHours, report);
            AddEvent(
                report,
                "event:delayed_applied",
                $"Conseguenza applicata: {consequence.Label}.");

            TryActivateFollowUpEvent(state, config.Events, consequence.FollowUpEventId, totalHours, report);
        }
    }

    private static void TryActivateFollowUpEvent(
        WorldState state,
        EventCatalog catalog,
        string? followUpEventId,
        float totalHours,
        SimulationFrameReport report)
    {
        if (string.IsNullOrWhiteSpace(followUpEventId) || state.RunState.IsGameOver || state.RunState.ActiveEvent is not null)
        {
            return;
        }

        var followUp = catalog.FindEvent(followUpEventId);
        if (followUp is null || followUp.Choices.Count == 0)
        {
            return;
        }

        if (state.Population < followUp.MinPopulation)
        {
            return;
        }

        if (followUp.MaxPopulation > 0 && state.Population > followUp.MaxPopulation)
        {
            return;
        }

        if (state.RunState.CurrentActIndex < followUp.MinActIndex)
        {
            return;
        }

        ActivateEvent(state.RunState, followUp, totalHours);
        AddEvent(report, "event:follow_up", $"Nuovo evento collegato: {followUp.Title}");
    }

    private static void ApplyTimedModifiers(
        RunState run,
        IReadOnlyList<TimedModifierDefinition> modifiers,
        float currentHour,
        SimulationFrameReport? report)
    {
        if (modifiers.Count == 0)
        {
            return;
        }

        foreach (var modifier in modifiers)
        {
            if (string.IsNullOrWhiteSpace(modifier.Id) || modifier.DurationHours <= 0f)
            {
                continue;
            }

            run.ActiveModifiers.RemoveAll(active => string.Equals(active.ModifierId, modifier.Id, StringComparison.Ordinal));
            var label = string.IsNullOrWhiteSpace(modifier.Label) ? modifier.Id : modifier.Label;
            run.ActiveModifiers.Add(new ActiveTimedModifierState
            {
                ModifierId = modifier.Id,
                Label = label,
                ExpiresAtHour = currentHour + modifier.DurationHours,
                ResidentialDemandMultiplier = modifier.ResidentialDemandMultiplier,
                CommercialDemandMultiplier = modifier.CommercialDemandMultiplier,
                IndustrialDemandMultiplier = modifier.IndustrialDemandMultiplier,
                OfficeDemandMultiplier = modifier.OfficeDemandMultiplier,
                GrowthMultiplier = modifier.GrowthMultiplier,
                ServiceCostMultiplier = modifier.ServiceCostMultiplier,
                RoadMaintenanceMultiplier = modifier.RoadMaintenanceMultiplier,
                CommuteMinutesMultiplier = modifier.CommuteMinutesMultiplier,
                TaxIncomeMultiplier = modifier.TaxIncomeMultiplier
            });
            AddEvent(report, "event:modifier_start", $"Effetto attivo: {label} ({modifier.DurationHours:0.0}h).");
        }
    }

    private static string BuildChoiceSummary(
        CityEventDefinition eventDefinition,
        CityEventChoiceDefinition choiceDefinition,
        EventChoiceEffectDefinition effect)
    {
        var parts = new List<string>
        {
            $"Evento risolto: {eventDefinition.Title} -> {choiceDefinition.Label}."
        };

        if (effect.CashDelta != 0m)
        {
            parts.Add($"Cassa {(effect.CashDelta > 0m ? "+" : string.Empty)}{effect.CashDelta:N0}.");
        }

        if (effect.LoanDelta != 0m)
        {
            parts.Add($"Debito {(effect.LoanDelta > 0m ? "+" : string.Empty)}{effect.LoanDelta:N0}.");
        }

        var demandParts = new List<string>();
        AppendSignedFloat(demandParts, "R", effect.ResidentialDemandDelta, 0.001f);
        AppendSignedFloat(demandParts, "C", effect.CommercialDemandDelta, 0.001f);
        AppendSignedFloat(demandParts, "I", effect.IndustrialDemandDelta, 0.001f);
        AppendSignedFloat(demandParts, "O", effect.OfficeDemandDelta, 0.001f);
        if (demandParts.Count > 0)
        {
            parts.Add($"Domanda {string.Join(" ", demandParts)}.");
        }

        if (Math.Abs(effect.LandValueDelta) > 0.0001f)
        {
            parts.Add($"Valore suolo {(effect.LandValueDelta >= 0f ? "+" : string.Empty)}{effect.LandValueDelta * 100f:0}%.");
        }

        if (Math.Abs(effect.UtilityCoverageDelta) > 0.0001f)
        {
            parts.Add($"Copertura utility {(effect.UtilityCoverageDelta >= 0f ? "+" : string.Empty)}{effect.UtilityCoverageDelta * 100f:0}%.");
        }

        if (effect.TimedModifiers.Count > 0)
        {
            parts.Add(
                $"Effetti temporanei: " +
                $"{string.Join(", ", effect.TimedModifiers.Select(modifier => string.IsNullOrWhiteSpace(modifier.Label) ? modifier.Id : modifier.Label))}.");
        }

        if (effect.DelayedConsequences.Count > 0)
        {
            parts.Add(
                $"Conseguenze differite: {effect.DelayedConsequences.Count} in coda.");
        }

        return string.Join(" ", parts);
    }

    private static void AppendSignedFloat(List<string> parts, string label, float value, float epsilon)
    {
        if (Math.Abs(value) < epsilon)
        {
            return;
        }

        parts.Add($"{label} {(value >= 0f ? "+" : string.Empty)}{value:0.00}");
    }

    private static TimedModifierDefinition CloneModifier(TimedModifierDefinition modifier)
    {
        return new TimedModifierDefinition
        {
            Id = modifier.Id,
            Label = modifier.Label,
            DurationHours = modifier.DurationHours,
            ResidentialDemandMultiplier = modifier.ResidentialDemandMultiplier,
            CommercialDemandMultiplier = modifier.CommercialDemandMultiplier,
            IndustrialDemandMultiplier = modifier.IndustrialDemandMultiplier,
            OfficeDemandMultiplier = modifier.OfficeDemandMultiplier,
            GrowthMultiplier = modifier.GrowthMultiplier,
            ServiceCostMultiplier = modifier.ServiceCostMultiplier,
            RoadMaintenanceMultiplier = modifier.RoadMaintenanceMultiplier,
            CommuteMinutesMultiplier = modifier.CommuteMinutesMultiplier,
            TaxIncomeMultiplier = modifier.TaxIncomeMultiplier
        };
    }

    private static void AddEvent(SimulationFrameReport? report, string code, string message)
    {
        if (report is null)
        {
            return;
        }

        report.SimulationEvents.Add(new SimulationEvent
        {
            Code = code,
            Message = message
        });
    }
}

public readonly struct EventChoiceResolutionResult
{
    private EventChoiceResolutionResult(bool applied, CommandRejectionReason rejectionReason, string message)
    {
        Applied = applied;
        RejectionReason = rejectionReason;
        Message = message;
    }

    public bool Applied { get; }

    public CommandRejectionReason RejectionReason { get; }

    public string Message { get; }

    public static EventChoiceResolutionResult Succeeded(string message)
    {
        return new EventChoiceResolutionResult(true, CommandRejectionReason.None, message);
    }

    public static EventChoiceResolutionResult Failed(CommandRejectionReason rejectionReason, string message)
    {
        return new EventChoiceResolutionResult(false, rejectionReason, message);
    }
}
}

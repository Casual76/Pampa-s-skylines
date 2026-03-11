namespace PampaSkylines.SaveSync
{
using System;
using PampaSkylines.Core;

public static class CitySnapshotMigrator
{
    public const int CurrentSchemaVersion = 4;

    public static CitySnapshot MigrateToCurrent(CitySnapshot snapshot)
    {
        snapshot.State ??= WorldState.CreateNew(snapshot.CityName);
        snapshot.SchemaVersion = CurrentSchemaVersion;
        snapshot.State.SchemaVersion = CurrentSchemaVersion;

        if (snapshot.CreatedAtUtc.Year < 2000)
        {
            snapshot.CreatedAtUtc = snapshot.SavedAtUtc.Year >= 2000
                ? snapshot.SavedAtUtc
                : DateTimeOffset.UtcNow;
        }

        if (snapshot.SavedAtUtc.Year < 2000)
        {
            snapshot.SavedAtUtc = snapshot.CreatedAtUtc;
        }

        if (string.IsNullOrWhiteSpace(snapshot.ClientId))
        {
            snapshot.ClientId = "local";
        }

        snapshot.CommandCount = Math.Max(snapshot.CommandCount, snapshot.State.AppliedCommandCount);
        snapshot.Metadata ??= new SnapshotMetadata();
        if (string.IsNullOrWhiteSpace(snapshot.Metadata.SourceClientId))
        {
            snapshot.Metadata.SourceClientId = snapshot.ClientId;
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.SourcePlatform))
        {
            snapshot.Metadata.SourcePlatform = "unknown";
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.SimulationConfigVersion))
        {
            snapshot.Metadata.SimulationConfigVersion = "unknown";
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.SaveSlotId))
        {
            snapshot.Metadata.SaveSlotId = "autosave";
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.SaveReason))
        {
            snapshot.Metadata.SaveReason = "manual";
        }

        var fallbackConfig = SimulationConfig.CreateFallback();
        var fallbackProgression = fallbackConfig.Progression;
        var createdProgression = snapshot.State.Progression is null;
        snapshot.State.Progression = snapshot.State.Progression is null
            ? ProgressionState.CreateForPopulation(
                fallbackProgression,
                snapshot.State.Population,
                treatRewardsAsAlreadyGranted: true)
            : snapshot.State.Progression;

        snapshot.State.Progression.EnsureCollections();
        snapshot.State.Progression.NormalizeUnlocks();
        snapshot.State.Progression.LastLoanRepaymentDay = createdProgression
            ? Math.Max(1, snapshot.State.Time.Day)
            : Math.Max(1, snapshot.State.Progression.LastLoanRepaymentDay);
        if (snapshot.State.Progression.LastLoanRepaymentDay > snapshot.State.Time.Day)
        {
            snapshot.State.Progression.LastLoanRepaymentDay = snapshot.State.Time.Day;
        }
        snapshot.State.Progression.RefreshNextMilestone(fallbackProgression);

        snapshot.State.RunState = snapshot.State.RunState is null
            ? RunState.CreateInitial(fallbackConfig.Events, snapshot.State.Population, snapshot.State.Time.Day)
            : snapshot.State.RunState;
        var trackedDay = Math.Max(1, snapshot.State.RunState.LastDeficitTrackedDay);
        if (trackedDay > snapshot.State.Time.Day)
        {
            trackedDay = snapshot.State.Time.Day;
        }

        snapshot.State.RunState.NormalizeForPopulation(
            fallbackConfig.Events,
            snapshot.State.Population,
            trackedDay);

        snapshot.State.RunState.IsVictory = snapshot.State.RunState.IsVictory && !snapshot.State.RunState.IsGameOver;
        snapshot.State.RunState.VictoryAtHour = Math.Max(0f, snapshot.State.RunState.VictoryAtHour);

        foreach (var lot in snapshot.State.Lots)
        {
            lot.DistrictVitality = Math.Clamp(lot.DistrictVitality, 0f, 1f);
        }

        foreach (var building in snapshot.State.Buildings)
        {
            building.DistrictVitality = Math.Clamp(building.DistrictVitality, 0f, 1f);
        }

        snapshot.State.DemoRun ??= DemoRunState.CreateInitial(fallbackConfig.Events);
        snapshot.State.DemoRun.Normalize();
        snapshot.State.DemoRun.ObjectivePopulation = Math.Max(0, snapshot.State.Population);
        snapshot.State.DemoRun.CurrentObjectiveId = snapshot.State.RunState.CurrentActId;
        snapshot.State.DemoRun.CurrentObjectiveTitle = snapshot.State.RunState.CurrentActName;
        snapshot.State.DemoRun.CurrentObjectiveTargetPopulation = Math.Max(1, snapshot.State.RunState.CurrentActProgressTarget);
        snapshot.State.DemoRun.ObjectiveProgress01 = Math.Clamp(snapshot.State.RunState.CurrentActProgress01, 0f, 1f);

        if (!snapshot.State.DemoRun.TutorialEnabled)
        {
            snapshot.State.DemoRun.OnboardingStepId = "onb-complete";
            snapshot.State.DemoRun.OnboardingStepTitle = "Onboarding disattivato";
            snapshot.State.DemoRun.OnboardingStepInstruction = "Tutorial disattivato: guida contestuale e lock morbidi non attivi.";
            snapshot.State.DemoRun.OnboardingFocusTool = "Nessuno";
        }
        else if (snapshot.State.DemoRun.OnboardingCompleted)
        {
            snapshot.State.DemoRun.OnboardingStepId = "onb-complete";
            snapshot.State.DemoRun.OnboardingStepTitle = "Onboarding completato";
            snapshot.State.DemoRun.OnboardingStepInstruction = "Tutti gli strumenti demo sono ora disponibili.";
            snapshot.State.DemoRun.OnboardingFocusTool = "Gestione libera";
        }

        if (snapshot.State.RunState.IsGameOver)
        {
            snapshot.State.DemoRun.RunCompleted = true;
            snapshot.State.DemoRun.Outcome = DemoOutcomeType.EconomicCollapse;
            snapshot.State.DemoRun.OutcomeReason = snapshot.State.RunState.GameOverReason;
        }
        else if (snapshot.State.RunState.IsVictory)
        {
            snapshot.State.DemoRun.RunCompleted = true;
            snapshot.State.DemoRun.Outcome = DemoOutcomeType.Victory;
            snapshot.State.DemoRun.OutcomeReason = snapshot.State.RunState.VictoryReason;
            snapshot.State.DemoRun.OutcomeAtHour = Math.Max(
                snapshot.State.DemoRun.OutcomeAtHour,
                snapshot.State.RunState.VictoryAtHour);
        }

        return snapshot;
    }
}
}

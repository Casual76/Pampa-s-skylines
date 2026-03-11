namespace PampaSkylines.Simulation
{
using System;
using PampaSkylines.Core;

public static class DemoRunModel
{
    public static void Update(WorldState state, float dtHours, SimulationConfig config, SimulationFrameReport report)
    {
        state.DemoRun ??= DemoRunState.CreateInitial(config.Events);
        state.DemoRun.Normalize();

        var run = state.RunState;
        var demo = state.DemoRun;

        demo.CurrentObjectiveId = run.CurrentActId;
        demo.CurrentObjectiveTitle = run.CurrentActName;
        demo.CurrentObjectiveTargetPopulation = Math.Max(1, run.CurrentActProgressTarget);
        demo.ObjectivePopulation = Math.Max(0, state.Population);
        demo.ObjectiveProgress01 = Math.Clamp(run.CurrentActProgress01, 0f, 1f);

        UpdateOnboardingState(state, config);

        if (run.IsGameOver)
        {
            MarkOutcome(state, DemoOutcomeType.EconomicCollapse, run.GameOverReason, state.Progression.TotalSimulatedHours);
            return;
        }

        if (run.IsVictory)
        {
            MarkOutcome(state, DemoOutcomeType.Victory, run.VictoryReason, run.VictoryAtHour);
            return;
        }

        if (demo.RunCompleted)
        {
            return;
        }

        var populationTarget = Math.Max(1, config.Economy.DemoTargetPopulation);
        var hasTargetPopulation = state.Population >= populationTarget;
        var hasTargetVitality = demo.AverageDistrictVitality >= config.Economy.VictoryMinimumDistrictVitality;
        var hasServiceStability = state.Utilities.AverageServiceCoverage >= config.Economy.VictoryMinimumServiceCoverage;
        var hasTrafficStability = state.AverageTrafficCongestion <= config.Economy.VictoryMaximumTrafficCongestion;

        if (hasTargetPopulation && hasTargetVitality && hasServiceStability && hasTrafficStability)
        {
            run.IsVictory = true;
            run.VictoryReason =
                $"Citta stabilizzata: {state.Population:N0} abitanti, vitalita {demo.AverageDistrictVitality:0.00}, " +
                $"servizi {state.Utilities.AverageServiceCoverage:0.00}, traffico {state.AverageTrafficCongestion:0.00}.";
            run.VictoryAtHour = Math.Max(0f, state.Progression.TotalSimulatedHours);
            MarkOutcome(state, DemoOutcomeType.Victory, run.VictoryReason, run.VictoryAtHour);
            AddEvent(report, "run:victory", run.VictoryReason);
        }
    }

    private static void UpdateOnboardingState(WorldState state, SimulationConfig config)
    {
        var demo = state.DemoRun;
        if (!demo.TutorialEnabled)
        {
            demo.OnboardingStepIndex = DemoOnboardingGuide.StepCount;
            demo.OnboardingCompletedSteps = DemoOnboardingGuide.StepCount;
            demo.OnboardingCompleted = true;
            demo.SoftInputLock = false;
            demo.OnboardingStepId = "onb-complete";
            demo.OnboardingStepTitle = "Onboarding disattivato";
            demo.OnboardingStepInstruction = "Tutorial disattivato: guida contestuale e lock morbidi non attivi.";
            demo.OnboardingFocusTool = "Nessuno";
            return;
        }

        var progress = 0;
        if (state.RoadSegments.Count > 0)
        {
            progress = Math.Max(progress, 1);
        }

        if (state.Lots.Count > 0)
        {
            progress = Math.Max(progress, 2);
        }

        if (state.Buildings.Count > 0)
        {
            progress = Math.Max(progress, 3);
        }

        if (state.Utilities.AverageServiceCoverage >= 0.45f)
        {
            progress = Math.Max(progress, 4);
        }

        if (state.Population >= 120)
        {
            progress = Math.Max(progress, 5);
        }

        if (state.Progression.BudgetPolicyUnlocked)
        {
            progress = Math.Max(progress, 6);
        }

        if (state.Population >= 350)
        {
            progress = Math.Max(progress, 7);
        }

        if (state.RunState.EventHistory.Count > 0)
        {
            progress = Math.Max(progress, 8);
        }

        if (state.AverageTrafficCongestion >= 0.20f)
        {
            progress = Math.Max(progress, 9);
        }

        if (state.DemoRun.AverageDistrictVitality >= 0.58f)
        {
            progress = Math.Max(progress, 10);
        }

        if (state.Population >= Math.Max(900, config.Economy.DemoTargetPopulation / 2))
        {
            progress = Math.Max(progress, 11);
        }

        if (state.Population >= config.Economy.DemoTargetPopulation)
        {
            progress = Math.Max(progress, 12);
        }

        demo.OnboardingStepIndex = Math.Max(demo.OnboardingStepIndex, progress);
        demo.OnboardingCompletedSteps = Math.Max(demo.OnboardingCompletedSteps, progress);
        demo.OnboardingCompleted = demo.OnboardingCompletedSteps >= DemoOnboardingGuide.StepCount;
        demo.SoftInputLock = !demo.OnboardingCompleted && demo.OnboardingCompletedSteps < 2;
        ApplyOnboardingStepDescriptor(demo, config);
        demo.Normalize();
    }

    private static void ApplyOnboardingStepDescriptor(DemoRunState demo, SimulationConfig config)
    {
        if (demo.OnboardingCompleted)
        {
            demo.OnboardingStepId = "onb-complete";
            demo.OnboardingStepTitle = "Onboarding completato";
            demo.OnboardingStepInstruction = "Tutti gli strumenti demo sono ora disponibili.";
            demo.OnboardingFocusTool = "Gestione libera";
            return;
        }

        var step = DemoOnboardingGuide.ResolveStep(demo.OnboardingCompletedSteps, config);
        demo.OnboardingStepId = step.Id;
        demo.OnboardingStepTitle = step.Title;
        demo.OnboardingStepInstruction = step.Instruction;
        demo.OnboardingFocusTool = step.FocusTool;
    }

    private static void MarkOutcome(WorldState state, DemoOutcomeType outcome, string reason, float atHour)
    {
        state.DemoRun.RunCompleted = true;
        state.DemoRun.Outcome = outcome;
        state.DemoRun.OutcomeReason = reason;
        state.DemoRun.OutcomeAtHour = Math.Max(0f, atHour);
    }

    private static void AddEvent(SimulationFrameReport report, string code, string message)
    {
        report.SimulationEvents.Add(new SimulationEvent
        {
            Code = code,
            Message = message
        });
    }
}
}

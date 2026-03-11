namespace PampaSkylines.Simulation
{
using System;
using PampaSkylines.Core;

public static class ProgressionModel
{
    public static void Update(WorldState state, float dtHours, SimulationConfig config, SimulationFrameReport report)
    {
        EnsureInitialized(state, config);
        var progression = state.Progression;
        progression.TotalSimulatedHours += Math.Max(0f, dtHours);

        ApplyMilestones(state, config.Progression, report);
        ApplyLoanRepayment(state, config.Progression.Bailout, report);
        ApplyBailout(state, Math.Max(0f, dtHours), config.Progression.Bailout, report);

        progression.RefreshNextMilestone(config.Progression);
        progression.NormalizeUnlocks();
    }

    public static void EnsureInitialized(WorldState state, SimulationConfig config)
    {
        if (state.Progression is null)
        {
            state.Progression = ProgressionState.CreateForPopulation(
                config.Progression,
                state.Population,
                treatRewardsAsAlreadyGranted: true);
            state.Progression.LastLoanRepaymentDay = Math.Max(1, state.Time.Day);
        }

        state.Progression.EnsureCollections();
        if (state.Progression.LastLoanRepaymentDay <= 0)
        {
            state.Progression.LastLoanRepaymentDay = Math.Max(1, state.Time.Day);
        }

        if (state.Progression.LastLoanRepaymentDay > state.Time.Day)
        {
            state.Progression.LastLoanRepaymentDay = state.Time.Day;
        }

        if (state.Progression.ReachedMilestoneIds.Count == 0 && config.Progression.Milestones.Count > 0)
        {
            var currentIndex = config.Progression.ResolveMilestoneIndexForPopulation(state.Population);
            for (var index = 0; index <= currentIndex; index++)
            {
                var milestone = config.Progression.ResolveMilestone(index);
                state.Progression.CurrentMilestoneIndex = index;
                state.Progression.ApplyMilestone(milestone, grantReward: false, state.Budget);
                state.Progression.TotalMilestoneRewardsAwarded += milestone.RewardCash;
            }
        }

        state.Progression.CurrentMilestoneIndex = Math.Clamp(
            state.Progression.CurrentMilestoneIndex,
            0,
            Math.Max(0, config.Progression.Milestones.Count - 1));
        state.Progression.RefreshNextMilestone(config.Progression);
        state.Progression.NormalizeUnlocks();
    }

    private static void ApplyMilestones(WorldState state, ProgressionCatalog catalog, SimulationFrameReport report)
    {
        var progression = state.Progression;
        var targetIndex = catalog.ResolveMilestoneIndexForPopulation(state.Population);
        if (targetIndex <= progression.CurrentMilestoneIndex)
        {
            return;
        }

        for (var index = progression.CurrentMilestoneIndex + 1; index <= targetIndex; index++)
        {
            var milestone = catalog.ResolveMilestone(index);
            progression.CurrentMilestoneIndex = index;
            progression.ApplyMilestone(milestone, grantReward: true, state.Budget);
            AddEvent(
                report,
                $"milestone:{milestone.Id}",
                milestone.RewardCash > 0m
                    ? $"Milestone raggiunta: {milestone.DisplayName}. Ricompensa {milestone.RewardCash:N0} crediti."
                    : $"Milestone raggiunta: {milestone.DisplayName}.");
        }
    }

    private static void ApplyLoanRepayment(WorldState state, BailoutConfig bailout, SimulationFrameReport report)
    {
        if (state.Budget.LoanBalance <= 0m)
        {
            return;
        }

        var progression = state.Progression;
        var currentDay = Math.Max(1, state.Time.Day);
        if (currentDay <= progression.LastLoanRepaymentDay)
        {
            return;
        }

        decimal repaidTotal = 0m;
        var elapsedDays = currentDay - progression.LastLoanRepaymentDay;
        for (var index = 0; index < elapsedDays && state.Budget.LoanBalance > 0m; index++)
        {
            var payment = Math.Round(
                state.Budget.LoanBalance * bailout.DailyRepaymentRate,
                2,
                MidpointRounding.AwayFromZero);
            if (payment <= 0m)
            {
                payment = state.Budget.LoanBalance;
            }

            payment = Math.Min(payment, state.Budget.LoanBalance);
            state.Budget.LoanBalance -= payment;
            state.Budget.Cash -= payment;
            repaidTotal += payment;
        }

        progression.LastLoanRepaymentDay = currentDay;
        if (repaidTotal > 0m)
        {
            AddEvent(
                report,
                "loan:repayment",
                $"Rimborso prestito automatico: {repaidTotal:N0} crediti. Debito residuo {state.Budget.LoanBalance:N0}.");
        }
    }

    private static void ApplyBailout(WorldState state, float dtHours, BailoutConfig bailout, SimulationFrameReport report)
    {
        if (dtHours <= 0f)
        {
            return;
        }

        var progression = state.Progression;
        if (state.Budget.Cash < bailout.CrisisCashThreshold)
        {
            progression.CrisisHoursUnderThreshold += dtHours;
        }
        else
        {
            progression.CrisisHoursUnderThreshold = 0f;
        }

        if (progression.CrisisHoursUnderThreshold < bailout.CrisisHoursRequired)
        {
            return;
        }

        if (progression.BailoutCount >= bailout.MaxBailouts)
        {
            return;
        }

        if (progression.TotalSimulatedHours < progression.NextBailoutAvailableAtHour)
        {
            return;
        }

        state.Budget.Cash += bailout.CashInjection;
        state.Budget.LoanBalance += bailout.LoanIncrease;
        progression.BailoutCount++;
        progression.CrisisHoursUnderThreshold = 0f;
        progression.NextBailoutAvailableAtHour = progression.TotalSimulatedHours + bailout.CooldownHours;
        progression.LastLoanRepaymentDay = Math.Max(1, state.Time.Day);

        AddEvent(
            report,
            "bailout:granted",
            $"Piano di salvataggio approvato: +{bailout.CashInjection:N0} crediti, debito +{bailout.LoanIncrease:N0}.");
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

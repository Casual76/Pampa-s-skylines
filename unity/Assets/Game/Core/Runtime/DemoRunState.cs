#nullable enable

namespace PampaSkylines.Core
{
using System;

public enum DemoOutcomeType
{
    None = 0,
    Victory = 1,
    EconomicCollapse = 2,
    Timeout = 3
}

public sealed class DemoRunState
{
    public string OnboardingStepId { get; set; } = "onb-01-road";

    public string OnboardingStepTitle { get; set; } = "Traccia la prima strada";

    public string OnboardingStepInstruction { get; set; } = "Usa lo strumento Strada per collegare due celle e avviare la fondazione.";

    public string OnboardingFocusTool { get; set; } = "Strada";

    public bool TutorialEnabled { get; set; } = true;

    public int OnboardingStepIndex { get; set; }

    public int OnboardingCompletedSteps { get; set; }

    public bool OnboardingCompleted { get; set; }

    public bool SoftInputLock { get; set; } = true;

    public string CurrentObjectiveId { get; set; } = "act1";

    public string CurrentObjectiveTitle { get; set; } = "Fondazione";

    public int CurrentObjectiveTargetPopulation { get; set; } = 320;

    public int ObjectivePopulation { get; set; }

    public float ObjectiveProgress01 { get; set; }

    public float AverageDistrictVitality { get; set; } = 0.50f;

    public float EconomicPressure { get; set; }

    public float ServicePressure { get; set; }

    public float TrafficPressure { get; set; }

    public bool RunCompleted { get; set; }

    public DemoOutcomeType Outcome { get; set; }

    public string OutcomeReason { get; set; } = string.Empty;

    public float OutcomeAtHour { get; set; }

    public void Normalize()
    {
        OnboardingStepId = string.IsNullOrWhiteSpace(OnboardingStepId) ? "onb-01-road" : OnboardingStepId.Trim();
        OnboardingStepTitle = string.IsNullOrWhiteSpace(OnboardingStepTitle) ? "Traccia la prima strada" : OnboardingStepTitle.Trim();
        OnboardingStepInstruction = string.IsNullOrWhiteSpace(OnboardingStepInstruction)
            ? "Usa lo strumento Strada per collegare due celle e avviare la fondazione."
            : OnboardingStepInstruction.Trim();
        OnboardingFocusTool = string.IsNullOrWhiteSpace(OnboardingFocusTool) ? "Strada" : OnboardingFocusTool.Trim();
        OnboardingStepIndex = Math.Max(0, OnboardingStepIndex);
        OnboardingCompletedSteps = Math.Max(0, OnboardingCompletedSteps);
        if (OnboardingCompletedSteps > 0)
        {
            OnboardingStepIndex = Math.Max(OnboardingStepIndex, OnboardingCompletedSteps);
        }

        CurrentObjectiveTargetPopulation = Math.Max(1, CurrentObjectiveTargetPopulation);
        ObjectivePopulation = Math.Max(0, ObjectivePopulation);
        ObjectiveProgress01 = Math.Clamp(ObjectiveProgress01, 0f, 1f);
        AverageDistrictVitality = Math.Clamp(AverageDistrictVitality, 0f, 1f);
        EconomicPressure = Math.Clamp(EconomicPressure, 0f, 1f);
        ServicePressure = Math.Clamp(ServicePressure, 0f, 1f);
        TrafficPressure = Math.Clamp(TrafficPressure, 0f, 1f);
        OutcomeAtHour = Math.Max(0f, OutcomeAtHour);
    }

    public static DemoRunState CreateInitial(EventCatalog eventCatalog)
    {
        var firstAct = eventCatalog.Acts.Count > 0
            ? eventCatalog.Acts[0]
            : CityActDefinition.CreateDefault();

        return new DemoRunState
        {
            CurrentObjectiveId = firstAct.Id,
            CurrentObjectiveTitle = firstAct.DisplayName,
            CurrentObjectiveTargetPopulation = Math.Max(1, firstAct.ObjectivePopulationTarget)
        };
    }
}
}

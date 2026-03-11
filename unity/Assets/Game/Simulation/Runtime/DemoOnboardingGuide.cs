#nullable enable

namespace PampaSkylines.Simulation
{
using System;
using PampaSkylines.Commands;
using PampaSkylines.Core;

public static class DemoOnboardingGuide
{
    public const int StepCount = 12;

    public static DemoOnboardingStep ResolveStep(int completedSteps, SimulationConfig config)
    {
        var stepIndex = Math.Clamp(completedSteps, 0, StepCount - 1);
        var halfTargetPopulation = Math.Max(900, config.Economy.DemoTargetPopulation / 2);
        var fullTargetPopulation = Math.Max(1, config.Economy.DemoTargetPopulation);

        return stepIndex switch
        {
            0 => new DemoOnboardingStep(
                "onb-01-road",
                "Apri la rete viaria",
                "Traccia la prima strada per connettere i lotti iniziali.",
                "Strada"),
            1 => new DemoOnboardingStep(
                "onb-02-zone",
                "Definisci il primo quartiere",
                "Zonizza almeno un lotto residenziale vicino alla strada.",
                "Zona Residenziale"),
            2 => new DemoOnboardingStep(
                "onb-03-utility",
                "Accendi i servizi base",
                "Piazza almeno un servizio utility (elettricita, acqua o fogne).",
                "Servizi Utility"),
            3 => new DemoOnboardingStep(
                "onb-04-coverage",
                "Porta copertura al 45%",
                "Espandi utility finche la copertura media servizi non raggiunge il 45%.",
                "Overlay Copertura"),
            4 => new DemoOnboardingStep(
                "onb-05-pop120",
                "Consolida la fondazione",
                "Raggiungi 120 abitanti mantenendo domanda residenziale positiva.",
                "Milestone"),
            5 => new DemoOnboardingStep(
                "onb-06-budget",
                "Sblocca il Municipio",
                "Raggiungi la milestone che abilita le politiche fiscali.",
                "Tasse"),
            6 => new DemoOnboardingStep(
                "onb-07-pop350",
                "Spingi l'espansione",
                "Porta la popolazione oltre 350 per aprire il secondo ritmo di gioco.",
                "Zona + Servizi"),
            7 => new DemoOnboardingStep(
                "onb-08-event",
                "Gestisci un evento",
                "Risolvi almeno un evento del Consiglio Cittadino.",
                "Consiglio Cittadino"),
            8 => new DemoOnboardingStep(
                "onb-09-traffic",
                "Leggi la congestione",
                "Osserva il traffico e crea collegamenti per evitare colli di bottiglia.",
                "Overlay Traffico"),
            9 => new DemoOnboardingStep(
                "onb-10-vitality",
                "Rialza la vitalita",
                "Aumenta la vitalita media quartieri almeno a 0.58.",
                "Inspector Lotto"),
            10 => new DemoOnboardingStep(
                "onb-11-pop-mid",
                "Verso la stabilizzazione",
                $"Raggiungi almeno {halfTargetPopulation:N0} abitanti mantenendo bilancio sotto controllo.",
                "Diagnosi Citta"),
            _ => new DemoOnboardingStep(
                "onb-12-pop-final",
                "Completa la demo",
                $"Raggiungi {fullTargetPopulation:N0} abitanti e stabilizza servizi, traffico e vitalita.",
                "Obiettivo Demo")
        };
    }

    public static string BuildStepProgressLabel(int completedSteps)
    {
        var currentStep = Math.Clamp(completedSteps + 1, 1, StepCount);
        return $"{currentStep}/{StepCount}";
    }

    public static string BuildSoftLockReason(int completedSteps, SimulationConfig config)
    {
        var step = ResolveStep(completedSteps, config);
        return $"Tutorial {BuildStepProgressLabel(completedSteps)}: {step.Instruction}";
    }

    public static bool IsCommandAllowedDuringSoftLock(GameCommandType commandType, int completedSteps)
    {
        if (completedSteps <= 0)
        {
            return commandType is GameCommandType.BuildRoad or GameCommandType.SetTimeScale;
        }

        if (completedSteps == 1)
        {
            return commandType is GameCommandType.BuildRoad or GameCommandType.PaintZone or GameCommandType.SetTimeScale;
        }

        return true;
    }
}

public readonly struct DemoOnboardingStep
{
    public DemoOnboardingStep(string id, string title, string instruction, string focusTool)
    {
        Id = id;
        Title = title;
        Instruction = instruction;
        FocusTool = focusTool;
    }

    public string Id { get; }

    public string Title { get; }

    public string Instruction { get; }

    public string FocusTool { get; }
}
}

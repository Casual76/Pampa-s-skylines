#nullable enable

namespace PampaSkylines.Android
{
using System;
using System.Collections.Generic;
using System.Linq;
using PampaSkylines.Commands;
using PampaSkylines.Core;
using PampaSkylines.Shared;
using PampaSkylines.Simulation;

public sealed class AndroidCompanionSession
{
    private long _clientSequence;

    public AndroidCompanionSession(WorldState initialState, SimulationConfig? config = null, string clientId = "android")
    {
        State = initialState;
        Commands = new CommandBuffer();
        Config = config ?? SimulationConfigLoader.LoadDefault();
        ClientId = clientId;
    }

    public WorldState State { get; }

    public CommandBuffer Commands { get; }

    public SimulationConfig Config { get; }

    public string ClientId { get; }

    public SimulationFrameReport Tick(float dt)
    {
        return SimulationEngine.SimulationStep(State, Commands, dt, Config);
    }

    public CitySnapshot CreateSnapshot()
    {
        return CitySnapshot.FromWorld(
            State,
            $"{State.Tick:D12}-{Guid.NewGuid():N}",
            ClientId,
            new SnapshotMetadata
            {
                SourceClientId = ClientId,
                SourcePlatform = "android",
                SimulationConfigVersion = Config.Version,
                DebugLabel = State.CityName
            });
    }

    public void QueueRoad(Int2 start, Int2 end, string? roadTypeId = null, int lanes = 2)
    {
        var command = CreateCommand(GameCommandType.BuildRoad);
        command.BuildRoad = new BuildRoadCommandData
        {
            Start = start,
            End = end,
            RoadTypeId = string.IsNullOrWhiteSpace(roadTypeId) ? Config.Roads.DefaultRoadTypeId : roadTypeId,
            Lanes = Math.Max(1, lanes)
        };
        Commands.Enqueue(command);
    }

    public void QueueZone(ZoneType zoneType, params Int2[] cells)
    {
        var command = CreateCommand(GameCommandType.PaintZone);
        command.PaintZone = new ZonePaintCommandData
        {
            ZoneType = zoneType
        };
        foreach (var cell in cells)
        {
            command.PaintZone.Cells.Add(cell);
        }

        Commands.Enqueue(command);
    }

    public void QueueService(ServiceType serviceType, Int2 cell)
    {
        var command = CreateCommand(GameCommandType.PlaceService);
        command.PlaceService = new PlaceServiceCommandData
        {
            ServiceType = serviceType,
            Cell = cell
        };
        Commands.Enqueue(command);
    }

    public void QueueBulldoze(BulldozeCommandData payload)
    {
        var command = CreateCommand(GameCommandType.Bulldoze);
        command.Bulldoze = payload;
        Commands.Enqueue(command);
    }

    public void QueueBudgetPolicy(ZoneType zoneType, decimal rate)
    {
        var command = CreateCommand(GameCommandType.UpdateBudgetPolicy);
        command.BudgetPolicy = zoneType switch
        {
            ZoneType.Residential => new BudgetPolicyCommandData { ResidentialTaxRate = rate },
            ZoneType.Commercial => new BudgetPolicyCommandData { CommercialTaxRate = rate },
            ZoneType.Industrial => new BudgetPolicyCommandData { IndustrialTaxRate = rate },
            ZoneType.Office => new BudgetPolicyCommandData { OfficeTaxRate = rate },
            _ => new BudgetPolicyCommandData()
        };
        Commands.Enqueue(command);
    }

    public void QueueSetTimeScale(float speedMultiplier, bool? isPaused = null)
    {
        var command = CreateCommand(GameCommandType.SetTimeScale);
        command.TimeControl = new TimeControlCommandData
        {
            SpeedMultiplier = speedMultiplier,
            IsPaused = isPaused
        };
        Commands.Enqueue(command);
    }

    public void QueueResolveEventChoice(string eventId, string choiceId)
    {
        var command = CreateCommand(GameCommandType.ResolveEventChoice);
        command.ResolveEventChoice = new ResolveEventChoiceCommandData
        {
            EventId = eventId,
            ChoiceId = choiceId
        };
        Commands.Enqueue(command);
    }

    public AndroidCompanionDashboard BuildDashboard(int maxAlerts = 4, int maxActions = 4)
    {
        var state = State;
        var run = state.RunState;
        var demo = state.DemoRun;
        var targetPopulation = Math.Max(1, demo.CurrentObjectiveTargetPopulation);
        var progressLabel = $"{Math.Max(0, demo.ObjectivePopulation):N0}/{targetPopulation:N0}";

        var dashboard = new AndroidCompanionDashboard
        {
            CityId = state.CityId,
            CityName = state.CityName,
            Tick = state.Tick,
            Population = state.Population,
            Jobs = state.Jobs,
            Cash = state.Budget.Cash,
            NetPerDay = state.Budget.LastDailyNet,
            ServiceCoverage = state.Utilities.AverageServiceCoverage,
            TrafficCongestion = state.AverageTrafficCongestion,
            AverageDistrictVitality = demo.AverageDistrictVitality,
            EconomicPressure = demo.EconomicPressure,
            ServicePressure = demo.ServicePressure,
            TrafficPressure = demo.TrafficPressure,
            ObjectiveTitle = demo.CurrentObjectiveTitle,
            ObjectiveProgressLabel = progressLabel,
            OnboardingStepTitle = demo.OnboardingStepTitle,
            OnboardingInstruction = demo.OnboardingStepInstruction,
            OnboardingSoftLockActive = demo.SoftInputLock && !demo.OnboardingCompleted && demo.TutorialEnabled,
            IsGameOver = run.IsGameOver,
            IsVictory = run.IsVictory,
            ActiveEventTitle = run.ActiveEvent?.Title ?? string.Empty
        };

        foreach (var alert in BuildAlerts().Take(Math.Max(1, maxAlerts)))
        {
            dashboard.Alerts.Add(alert);
        }

        foreach (var action in BuildSuggestedActions().Take(Math.Max(1, maxActions)))
        {
            dashboard.SuggestedActions.Add(action);
        }

        return dashboard;
    }

    public bool QueueQuickAction(AndroidQuickActionType actionType)
    {
        switch (actionType)
        {
            case AndroidQuickActionType.PauseSimulation:
                QueueSetTimeScale(State.Time.SpeedMultiplier, true);
                return true;
            case AndroidQuickActionType.ResumeSimulation:
                QueueSetTimeScale(Math.Max(1f, State.Time.SpeedMultiplier), false);
                return true;
            case AndroidQuickActionType.LowerResidentialTax:
                QueueBudgetPolicy(ZoneType.Residential, ClampTax(State.Budget.TaxRateResidential - 0.01m));
                return true;
            case AndroidQuickActionType.LowerCommercialTax:
                QueueBudgetPolicy(ZoneType.Commercial, ClampTax(State.Budget.TaxRateCommercial - 0.01m));
                return true;
            case AndroidQuickActionType.LowerIndustrialTax:
                QueueBudgetPolicy(ZoneType.Industrial, ClampTax(State.Budget.TaxRateIndustrial - 0.01m));
                return true;
            case AndroidQuickActionType.LowerOfficeTax:
                QueueBudgetPolicy(ZoneType.Office, ClampTax(State.Budget.TaxRateOffice - 0.01m));
                return true;
            case AndroidQuickActionType.RaiseResidentialTax:
                QueueBudgetPolicy(ZoneType.Residential, ClampTax(State.Budget.TaxRateResidential + 0.01m));
                return true;
            case AndroidQuickActionType.RaiseCommercialTax:
                QueueBudgetPolicy(ZoneType.Commercial, ClampTax(State.Budget.TaxRateCommercial + 0.01m));
                return true;
            case AndroidQuickActionType.ResolveEventPrimaryChoice:
                return QueueEventChoiceAtIndex(0);
            case AndroidQuickActionType.ResolveEventSecondaryChoice:
                return QueueEventChoiceAtIndex(1);
            default:
                return false;
        }
    }

    private IEnumerable<AndroidCompanionAlert> BuildAlerts()
    {
        var state = State;
        var run = state.RunState;
        var demo = state.DemoRun;

        if (run.IsGameOver)
        {
            yield return new AndroidCompanionAlert
            {
                Severity = AndroidAlertSeverity.Critical,
                Title = "Collasso economico",
                Message = string.IsNullOrWhiteSpace(run.GameOverReason) ? "La run e terminata per crisi economica." : run.GameOverReason
            };
            yield break;
        }

        if (run.IsVictory)
        {
            yield return new AndroidCompanionAlert
            {
                Severity = AndroidAlertSeverity.Info,
                Title = "Obiettivo completato",
                Message = string.IsNullOrWhiteSpace(run.VictoryReason) ? "Run demo completata." : run.VictoryReason
            };
        }

        if (state.Budget.Cash <= -15000m || run.FiscalDistressHours >= 12f)
        {
            yield return new AndroidCompanionAlert
            {
                Severity = AndroidAlertSeverity.Critical,
                Title = "Crisi bilancio",
                Message = $"Cassa {state.Budget.Cash:N0}, rischio collasso {run.FiscalDistressHours:0.0}/24h."
            };
        }
        else if (state.Budget.LastDailyNet < 0m)
        {
            yield return new AndroidCompanionAlert
            {
                Severity = AndroidAlertSeverity.Warning,
                Title = "Deficit giornaliero",
                Message = $"Netto {state.Budget.LastDailyNet:N0}/giorno. Valuta tasse o riduzione upkeep."
            };
        }

        if (state.Utilities.AverageServiceCoverage < 0.70f)
        {
            yield return new AndroidCompanionAlert
            {
                Severity = AndroidAlertSeverity.Warning,
                Title = "Copertura servizi bassa",
                Message = $"Copertura media {state.Utilities.AverageServiceCoverage:P0}. Espandi utility nei nuovi quartieri."
            };
        }

        if (state.AverageTrafficCongestion >= 0.62f)
        {
            yield return new AndroidCompanionAlert
            {
                Severity = AndroidAlertSeverity.Warning,
                Title = "Congestione elevata",
                Message = $"Traffico medio {state.AverageTrafficCongestion:P0}. Crea bypass o nodi alternativi."
            };
        }

        if (demo.AverageDistrictVitality < 0.52f)
        {
            yield return new AndroidCompanionAlert
            {
                Severity = AndroidAlertSeverity.Warning,
                Title = "Vitalita quartieri in calo",
                Message = $"Vitalita media {demo.AverageDistrictVitality:0.00}. Migliora servizi e fluidita traffico."
            };
        }

        if (run.ActiveEvent is not null)
        {
            yield return new AndroidCompanionAlert
            {
                Severity = AndroidAlertSeverity.Info,
                Title = "Evento in attesa",
                Message = $"Apri companion e scegli una risposta per '{run.ActiveEvent.Title}'."
            };
        }

        if (demo.TutorialEnabled && !demo.OnboardingCompleted)
        {
            yield return new AndroidCompanionAlert
            {
                Severity = demo.SoftInputLock ? AndroidAlertSeverity.Info : AndroidAlertSeverity.Warning,
                Title = $"Onboarding {DemoOnboardingGuide.BuildStepProgressLabel(demo.OnboardingCompletedSteps)}",
                Message = demo.OnboardingStepInstruction
            };
        }
    }

    private IEnumerable<AndroidCompanionAction> BuildSuggestedActions()
    {
        var state = State;
        var run = state.RunState;
        var demo = state.DemoRun;

        if (state.Time.IsPaused)
        {
            yield return new AndroidCompanionAction
            {
                ActionType = AndroidQuickActionType.ResumeSimulation,
                Title = "Riprendi simulazione",
                Description = "Riattiva il tempo per continuare la run.",
                IsCritical = false
            };
        }
        else
        {
            yield return new AndroidCompanionAction
            {
                ActionType = AndroidQuickActionType.PauseSimulation,
                Title = "Pausa tattica",
                Description = "Ferma il tempo per analizzare KPI ed eventi.",
                IsCritical = false
            };
        }

        if (run.ActiveEvent is not null)
        {
            yield return new AndroidCompanionAction
            {
                ActionType = AndroidQuickActionType.ResolveEventPrimaryChoice,
                Title = "Decidi evento (opzione 1)",
                Description = $"Applica: {run.ActiveEvent.Choices.FirstOrDefault()?.Label ?? "Scelta primaria"}.",
                IsCritical = true
            };

            if (run.ActiveEvent.Choices.Count > 1)
            {
                yield return new AndroidCompanionAction
                {
                    ActionType = AndroidQuickActionType.ResolveEventSecondaryChoice,
                    Title = "Decidi evento (opzione 2)",
                    Description = $"Alternativa: {run.ActiveEvent.Choices[1].Label}.",
                    IsCritical = false
                };
            }
        }

        if (state.Budget.LastDailyNet < 0m)
        {
            yield return new AndroidCompanionAction
            {
                ActionType = AndroidQuickActionType.RaiseResidentialTax,
                Title = "Aumenta tassa R (+1%)",
                Description = "Riduce deficit nel breve periodo.",
                IsCritical = true
            };
            yield return new AndroidCompanionAction
            {
                ActionType = AndroidQuickActionType.RaiseCommercialTax,
                Title = "Aumenta tassa C (+1%)",
                Description = "Migliora entrate fiscali in emergenza.",
                IsCritical = false
            };
        }
        else if (demo.EconomicPressure < 0.35f && state.Budget.Cash > 10000m)
        {
            yield return new AndroidCompanionAction
            {
                ActionType = AndroidQuickActionType.LowerResidentialTax,
                Title = "Riduci tassa R (-1%)",
                Description = "Stimola crescita residenziale e domanda.",
                IsCritical = false
            };
        }

        if (demo.ServicePressure >= 0.65f)
        {
            yield return new AndroidCompanionAction
            {
                ActionType = AndroidQuickActionType.PauseSimulation,
                Title = "Pausa e costruisci utility",
                Description = "Pressione servizi alta: pianifica intervento prima del prossimo tick.",
                IsCritical = true
            };
        }
    }

    private bool QueueEventChoiceAtIndex(int index)
    {
        var activeEvent = State.RunState.ActiveEvent;
        if (activeEvent is null || index < 0 || index >= activeEvent.Choices.Count)
        {
            return false;
        }

        QueueResolveEventChoice(activeEvent.EventId, activeEvent.Choices[index].ChoiceId);
        return true;
    }

    private decimal ClampTax(decimal value)
    {
        return Math.Clamp(value, Config.Economy.MinimumTaxRate, Config.Economy.MaximumTaxRate);
    }

    private GameCommand CreateCommand(GameCommandType type)
    {
        return new GameCommand
        {
            Type = type,
            ClientId = ClientId,
            ClientSequence = ++_clientSequence,
            IssuedAtUtc = DateTimeOffset.UtcNow
        };
    }
}

public enum AndroidAlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum AndroidQuickActionType
{
    PauseSimulation = 0,
    ResumeSimulation = 1,
    LowerResidentialTax = 2,
    LowerCommercialTax = 3,
    LowerIndustrialTax = 4,
    LowerOfficeTax = 5,
    RaiseResidentialTax = 6,
    RaiseCommercialTax = 7,
    ResolveEventPrimaryChoice = 8,
    ResolveEventSecondaryChoice = 9
}

public sealed class AndroidCompanionAlert
{
    public AndroidAlertSeverity Severity { get; set; } = AndroidAlertSeverity.Info;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class AndroidCompanionAction
{
    public AndroidQuickActionType ActionType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsCritical { get; set; }
}

public sealed class AndroidCompanionDashboard
{
    public string CityId { get; set; } = string.Empty;

    public string CityName { get; set; } = string.Empty;

    public long Tick { get; set; }

    public int Population { get; set; }

    public int Jobs { get; set; }

    public decimal Cash { get; set; }

    public decimal NetPerDay { get; set; }

    public float ServiceCoverage { get; set; }

    public float TrafficCongestion { get; set; }

    public float AverageDistrictVitality { get; set; }

    public float EconomicPressure { get; set; }

    public float ServicePressure { get; set; }

    public float TrafficPressure { get; set; }

    public string ObjectiveTitle { get; set; } = string.Empty;

    public string ObjectiveProgressLabel { get; set; } = string.Empty;

    public string OnboardingStepTitle { get; set; } = string.Empty;

    public string OnboardingInstruction { get; set; } = string.Empty;

    public bool OnboardingSoftLockActive { get; set; }

    public bool IsGameOver { get; set; }

    public bool IsVictory { get; set; }

    public string ActiveEventTitle { get; set; } = string.Empty;

    public List<AndroidCompanionAlert> Alerts { get; set; } = new();

    public List<AndroidCompanionAction> SuggestedActions { get; set; } = new();
}
}

#nullable enable

namespace PampaSkylines.PC
{
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PampaSkylines.Commands;
using PampaSkylines.Core;
using PampaSkylines.SaveSync;
using PampaSkylines.Shared;
using PampaSkylines.Simulation;

public sealed class PcCitySessionOrchestrator
{
    private readonly Stack<WorldState> _undoStack = new();
    private readonly Stack<WorldState> _redoStack = new();
    private readonly int _historyCapacity;
    private float _tickAccumulator;
    private long _clientSequence;
    private int _lastDigestDay;
    private bool _runOutcomePresented;
    private int _lastOnboardingStep;

    public PcCitySessionOrchestrator(PcSimulationSession session, string saveRootPath, float autosaveIntervalSeconds = 45f, int historyCapacity = 24)
    {
        Session = session;
        ToolState = new PcToolState();
        OverlayState = new PcOverlayState();
        Notifications = new PcNotificationFeed();
        SaveSync = new PcSaveSyncCoordinator(session, saveRootPath, autosaveIntervalSeconds);
        SaveSync.StatusChanged += HandleCoordinatorStatusChanged;
        _historyCapacity = Math.Max(8, historyCapacity);
        _lastDigestDay = Math.Max(1, session.State.Time.Day);
        _lastOnboardingStep = Math.Max(0, session.State.DemoRun.OnboardingCompletedSteps);
        ToolState.SetRoadPreset(session.Config.Roads.DefaultRoadTypeId, 2);
        SetStatus($"Session pronta per {session.State.CityName}.");
    }

    public PcSimulationSession Session { get; private set; }

    public PcToolState ToolState { get; }

    public PcOverlayState OverlayState { get; }

    public PcNotificationFeed Notifications { get; }

    public PcSaveSyncCoordinator SaveSync { get; }

    public SimulationFrameReport? LastReport { get; private set; }

    public string StatusMessage { get; private set; } = "Pronto.";

    public PcStatusTone StatusTone { get; private set; }

    public string LastRejectedCommandMessage { get; private set; } = string.Empty;

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public string InspectorTitle => BuildInspectorTitle();

    public string InspectorBody => BuildInspectorBody();

    public string CostPreview => BuildCostPreview();

    public string CityDiagnosis => BuildCityDiagnosis();

    public string ActiveModifiersSummary => BuildActiveModifiersSummary();

    public ProgressionState Progression => Session.State.Progression;

    public RunState RunState => Session.State.RunState;

    public ActiveCityEventState? ActiveEvent => Session.State.RunState.ActiveEvent;

    public bool IsGameOver => Session.State.RunState.IsGameOver;

    public string GameOverReason => Session.State.RunState.GameOverReason;

    public bool IsVictory => Session.State.RunState.IsVictory;

    public DemoRunState DemoRun => Session.State.DemoRun;

    public bool IsRunCompleted => Session.State.DemoRun.RunCompleted;

    public string RunOutcomeTitle => Session.State.DemoRun.Outcome switch
    {
        DemoOutcomeType.Victory => "Vittoria demo",
        DemoOutcomeType.EconomicCollapse => "Crollo economico",
        DemoOutcomeType.Timeout => "Tempo scaduto",
        _ => "Run in corso"
    };

    public string RunOutcomeSummary =>
        $"{RunOutcomeTitle}\n" +
        $"{(string.IsNullOrWhiteSpace(Session.State.DemoRun.OutcomeReason) ? "Nessun dettaglio disponibile." : Session.State.DemoRun.OutcomeReason)}\n" +
        $"Popolazione finale {Session.State.Population:N0} | Vitalita media {Session.State.DemoRun.AverageDistrictVitality:0.00}";

    public bool CanAdjustBudgetPolicy =>
        !Session.State.RunState.IsGameOver &&
        Session.Config.IsBudgetPolicyUnlocked(Session.State.Progression);

    public bool IsOnboardingActive =>
        Session.State.DemoRun.TutorialEnabled &&
        !Session.State.DemoRun.OnboardingCompleted;

    public bool IsOnboardingSoftLockActive => Session.State.DemoRun.SoftInputLock && IsOnboardingActive;

    public string OnboardingProgressLabel =>
        DemoOnboardingGuide.BuildStepProgressLabel(Session.State.DemoRun.OnboardingCompletedSteps);

    public string OnboardingSummary => BuildOnboardingSummary();

    public void Tick(float unscaledDeltaTime, float targetSecondsPerTick, float simulatedHoursPerTick)
    {
        SaveSync.Tick(unscaledDeltaTime);
        var state = Session.State;
        var shouldTick = !state.Time.IsPaused || Session.Commands.Count > 0;
        if (!shouldTick)
        {
            return;
        }

        _tickAccumulator += unscaledDeltaTime;
        while (_tickAccumulator >= targetSecondsPerTick)
        {
            var beforeTick = Session.Commands.Count > 0 ? Session.CreateStateClone() : null;
            LastReport = Session.Tick(simulatedHoursPerTick);
            _tickAccumulator -= targetSecondsPerTick;

            if (beforeTick is not null && LastReport.AppliedCommandCount > 0)
            {
                PushUndoState(beforeTick);
                _redoStack.Clear();
            }

            if (LastReport.RejectedCommandCount > 0)
            {
                var rejection = LastReport.CommandResults.Last(result => result.Status == CommandExecutionStatus.Rejected);
                LastRejectedCommandMessage = rejection.Message;
                SetStatus(rejection.Message, PcStatusTone.Error);
            }
            else if (LastReport.AppliedCommandCount > 0)
            {
                LastRejectedCommandMessage = string.Empty;
                SetStatus($"Azioni applicate: {LastReport.AppliedCommandCount} al tick {LastReport.TickAfter}.");
            }

            foreach (var simulationEvent in LastReport.SimulationEvents)
            {
                var (tone, category) = ResolveNotificationStyle(simulationEvent.Code);
                Notifications.Push(simulationEvent.Message, tone, category);
                StatusMessage = simulationEvent.Message;
                StatusTone = tone;
            }

            EmitDailyDigestIfNeeded();
            EmitOnboardingProgressIfNeeded();

            if (!IsToolUnlocked(ToolState.ActiveToolMode))
            {
                ToolState.SelectTool(PcToolMode.Road);
            }

            if (Session.State.DemoRun.RunCompleted)
            {
                if (!_runOutcomePresented)
                {
                    OverlayState.OpenRunOutcome();
                    _runOutcomePresented = true;
                }
            }
        }
    }

    public void SelectTool(PcToolMode toolMode)
    {
        if (!IsToolUnlocked(toolMode))
        {
            SetStatus(GetToolLockMessage(toolMode), PcStatusTone.Warning);
            return;
        }

        ToolState.SelectTool(toolMode);
        SetStatus($"Strumento selezionato: {toolMode.ToDisplayName()}.");
    }

    public void SetHoverCell(Int2? hoverCell)
    {
        ToolState.SetHoverCell(hoverCell);
    }

    public void BeginDrag(Int2 cell)
    {
        ToolState.BeginDrag(cell);
    }

    public void ClearDrag()
    {
        ToolState.ClearDrag();
    }

    public void QueueRoad(Int2 startCell, Int2 endCell)
    {
        if (IsRunCompleted)
        {
            SetStatus(BuildRunLockedMessage("costruzione bloccata"), PcStatusTone.Warning, PcNotificationCategory.Allerta);
            return;
        }

        if (!Session.Config.IsRoadUnlocked(Session.State.Progression))
        {
            SetStatus(GetToolLockMessage(PcToolMode.Road), PcStatusTone.Warning);
            return;
        }

        if (startCell.Equals(endCell))
        {
            SetStatus("Costruzione strada ignorata: punto iniziale e finale coincidono.", PcStatusTone.Warning);
            return;
        }

        var command = CreateCommand(GameCommandType.BuildRoad);
        command.BuildRoad = new BuildRoadCommandData
        {
            RoadTypeId = ToolState.SelectedRoadTypeId,
            Start = startCell,
            End = endCell,
            Lanes = ToolState.SelectedRoadLanes
        };

        Session.Commands.Enqueue(command);
        SetStatus($"Strada in coda {startCell} -> {endCell}.");
    }

    public void QueueZone(Int2 startCell, Int2 endCell)
    {
        if (IsRunCompleted)
        {
            SetStatus(BuildRunLockedMessage("zonizzazione bloccata"), PcStatusTone.Warning, PcNotificationCategory.Allerta);
            return;
        }

        if (!Session.Config.IsZoneUnlocked(ToolState.ActiveToolMode.ToZoneType(), Session.State.Progression))
        {
            SetStatus(GetToolLockMessage(ToolState.ActiveToolMode), PcStatusTone.Warning);
            return;
        }

        var command = CreateCommand(GameCommandType.PaintZone);
        command.PaintZone = new ZonePaintCommandData
        {
            ZoneType = ToolState.ActiveToolMode.ToZoneType(),
            Cells = CreateRectangle(startCell, endCell)
        };

        Session.Commands.Enqueue(command);
        SetStatus($"Zonizzazione {ToolState.ActiveToolMode.ToDisplayName()} in coda.");
    }

    public void QueueService(Int2 cell)
    {
        if (IsRunCompleted)
        {
            SetStatus(BuildRunLockedMessage("costruzione servizi bloccata"), PcStatusTone.Warning, PcNotificationCategory.Allerta);
            return;
        }

        if (!Session.Config.IsServiceUnlocked(ToolState.ActiveToolMode.ToServiceType(), Session.State.Progression))
        {
            SetStatus(GetToolLockMessage(ToolState.ActiveToolMode), PcStatusTone.Warning);
            return;
        }

        var command = CreateCommand(GameCommandType.PlaceService);
        command.PlaceService = new PlaceServiceCommandData
        {
            ServiceType = ToolState.ActiveToolMode.ToServiceType(),
            Cell = cell
        };

        Session.Commands.Enqueue(command);
        SetStatus($"{ToolState.ActiveToolMode.ToDisplayName()} in coda su {cell}.");
    }

    public void QueueBulldoze(Int2 cell)
    {
        if (IsRunCompleted)
        {
            SetStatus(BuildRunLockedMessage("demolizione bloccata"), PcStatusTone.Warning, PcNotificationCategory.Allerta);
            return;
        }

        if (!Session.Config.IsBulldozeUnlocked(Session.State.Progression))
        {
            SetStatus(GetToolLockMessage(PcToolMode.Bulldoze), PcStatusTone.Warning);
            return;
        }

        var target = BuildBulldozePayload(cell);
        if (target is null)
        {
            SetStatus($"Nessun elemento da demolire su {cell}.", PcStatusTone.Warning);
            return;
        }

        var command = CreateCommand(GameCommandType.Bulldoze);
        command.Bulldoze = target;
        Session.Commands.Enqueue(command);
        SetStatus($"Demolizione in coda su {cell}.");
    }

    public void TogglePause()
    {
        var command = CreateCommand(GameCommandType.SetTimeScale);
        command.TimeControl = new TimeControlCommandData
        {
            IsPaused = !Session.State.Time.IsPaused
        };

        Session.Commands.Enqueue(command);
        SetStatus(command.TimeControl.IsPaused == true ? "Pausa in coda." : "Ripresa in coda.");
    }

    public void SetTimeScale(float speedMultiplier)
    {
        var clampedSpeed = Math.Clamp(
            speedMultiplier,
            Session.Config.Economy.MinimumTimeScale,
            Session.Config.Economy.MaximumTimeScale);

        var command = CreateCommand(GameCommandType.SetTimeScale);
        command.TimeControl = new TimeControlCommandData
        {
            SpeedMultiplier = clampedSpeed,
            IsPaused = false
        };

        Session.Commands.Enqueue(command);
        SetStatus($"Velocita simulazione in coda: {clampedSpeed:0.0}x.");
    }

    public void AdjustTaxRate(ZoneType zoneType, decimal delta)
    {
        if (IsRunCompleted)
        {
            SetStatus(BuildRunLockedMessage("politiche fiscali bloccate"), PcStatusTone.Warning, PcNotificationCategory.Allerta);
            return;
        }

        if (!Session.Config.IsBudgetPolicyUnlocked(Session.State.Progression))
        {
            SetStatus("Politiche di bilancio non ancora sbloccate.", PcStatusTone.Warning);
            return;
        }

        var command = CreateCommand(GameCommandType.UpdateBudgetPolicy);
        var minTax = Session.Config.Economy.MinimumTaxRate;
        var maxTax = Session.Config.Economy.MaximumTaxRate;

        switch (zoneType)
        {
            case ZoneType.Residential:
                command.BudgetPolicy = new BudgetPolicyCommandData
                {
                    ResidentialTaxRate = Math.Clamp(Session.State.Budget.TaxRateResidential + delta, minTax, maxTax)
                };
                break;
            case ZoneType.Commercial:
                command.BudgetPolicy = new BudgetPolicyCommandData
                {
                    CommercialTaxRate = Math.Clamp(Session.State.Budget.TaxRateCommercial + delta, minTax, maxTax)
                };
                break;
            case ZoneType.Industrial:
                command.BudgetPolicy = new BudgetPolicyCommandData
                {
                    IndustrialTaxRate = Math.Clamp(Session.State.Budget.TaxRateIndustrial + delta, minTax, maxTax)
                };
                break;
            case ZoneType.Office:
                command.BudgetPolicy = new BudgetPolicyCommandData
                {
                    OfficeTaxRate = Math.Clamp(Session.State.Budget.TaxRateOffice + delta, minTax, maxTax)
                };
                break;
            default:
                SetStatus("Tipo di zona non supportato per la tassazione.", PcStatusTone.Warning);
                return;
        }

        Session.Commands.Enqueue(command);
        SetStatus($"Aggiornamento aliquota {zoneType} in coda.");
    }

    public void ResolveActiveEventChoice(string eventId, string choiceId)
    {
        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(choiceId))
        {
            SetStatus("Scelta evento non valida.", PcStatusTone.Warning, PcNotificationCategory.Eventi);
            return;
        }

        if (ActiveEvent is null || !string.Equals(ActiveEvent.EventId, eventId, StringComparison.Ordinal))
        {
            SetStatus("Nessun evento attivo da risolvere.", PcStatusTone.Warning, PcNotificationCategory.Eventi);
            return;
        }

        var command = CreateCommand(GameCommandType.ResolveEventChoice);
        command.ResolveEventChoice = new ResolveEventChoiceCommandData
        {
            EventId = eventId,
            ChoiceId = choiceId
        };

        Session.Commands.Enqueue(command);
        SetStatus("Decisione evento inviata al consiglio cittadino.", PcStatusTone.Neutral, PcNotificationCategory.Eventi);
    }

    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            SetStatus("Nessuna azione da annullare.", PcStatusTone.Warning);
            return;
        }

        _redoStack.Push(Session.CreateStateClone());
        Session.RestoreState(_undoStack.Pop());
        SaveSync.ResetAutosaveTimer();
        SetStatus("Ultima azione annullata.");
    }

    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            SetStatus("Nessuna azione da ripristinare.", PcStatusTone.Warning);
            return;
        }

        _undoStack.Push(Session.CreateStateClone());
        Session.RestoreState(_redoStack.Pop());
        SaveSync.ResetAutosaveTimer();
        SetStatus("Stato ripristinato.");
    }

    public Task SaveNowAsync()
    {
        return SaveSync.SaveNowAsync("manual", "manual");
    }

    public Task<bool> LoadCurrentAsync()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _tickAccumulator = 0f;
        LastRejectedCommandMessage = string.Empty;
        _runOutcomePresented = false;
        _lastOnboardingStep = 0;
        ToolState.ClearDrag();
        ToolState.SetHoverCell(null);
        OverlayState.CloseRunOutcome();
        return SaveSync.LoadCurrentAsync();
    }

    public Task<bool> LoadCityAsync(string cityId)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _tickAccumulator = 0f;
        LastRejectedCommandMessage = string.Empty;
        _runOutcomePresented = false;
        _lastOnboardingStep = 0;
        ToolState.ClearDrag();
        ToolState.SetHoverCell(null);
        OverlayState.CloseRunOutcome();
        return SaveSync.LoadCurrentAsync(cityId);
    }

    public Task<bool> LoadVersionAsync(string version)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _tickAccumulator = 0f;
        LastRejectedCommandMessage = string.Empty;
        _runOutcomePresented = false;
        _lastOnboardingStep = 0;
        ToolState.ClearDrag();
        ToolState.SetHoverCell(null);
        OverlayState.CloseRunOutcome();
        return SaveSync.LoadVersionAsync(version);
    }

    public Task<bool> LoadVersionAsync(string cityId, string version)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _tickAccumulator = 0f;
        LastRejectedCommandMessage = string.Empty;
        _runOutcomePresented = false;
        _lastOnboardingStep = 0;
        ToolState.ClearDrag();
        ToolState.SetHoverCell(null);
        OverlayState.CloseRunOutcome();
        return SaveSync.LoadVersionAsync(version, cityId);
    }

    public void StartNewCity(string cityName)
    {
        Session.ResetCity(cityName);
        ToolState.SetRoadPreset(Session.Config.Roads.DefaultRoadTypeId, 2);
        ToolState.ClearDrag();
        ToolState.SetHoverCell(null);
        _undoStack.Clear();
        _redoStack.Clear();
        _tickAccumulator = 0f;
        LastRejectedCommandMessage = string.Empty;
        _runOutcomePresented = false;
        _lastOnboardingStep = 0;
        OverlayState.CloseTransientPanels();
        OverlayState.CloseRunOutcome();
        OverlayState.CloseMainMenu();
        Notifications.Push($"Nuova citta avviata: '{cityName}'.");
        SetStatus($"Nuova citta avviata: '{cityName}'.");
    }

    public BulldozeCommandData? BuildBulldozePayload(Int2 cell)
    {
        var building = Session.State.Buildings.FirstOrDefault(existing => existing.Cell.Equals(cell));
        if (building is not null)
        {
            return new BulldozeCommandData
            {
                BuildingId = building.Id
            };
        }

        var lot = Session.State.Lots.FirstOrDefault(existing => existing.Cell.Equals(cell));
        if (lot is not null)
        {
            return new BulldozeCommandData
            {
                LotId = lot.Id
            };
        }

        var roadSegment = FindRoadSegmentAtCell(cell);
        if (roadSegment is not null)
        {
            return new BulldozeCommandData
            {
                RoadSegmentId = roadSegment.Id
            };
        }

        return null;
    }

    public bool IsToolUnlocked(PcToolMode toolMode)
    {
        if ((IsGameOver || IsVictory) && (toolMode.IsZoneTool() || toolMode.IsServiceTool() || toolMode == PcToolMode.Road || toolMode == PcToolMode.Bulldoze))
        {
            return false;
        }

        if (IsToolSoftLockedByOnboarding(toolMode))
        {
            return false;
        }

        return toolMode switch
        {
            PcToolMode.Road => Session.Config.IsRoadUnlocked(Session.State.Progression),
            PcToolMode.Bulldoze => Session.Config.IsBulldozeUnlocked(Session.State.Progression),
            _ when toolMode.IsZoneTool() => Session.Config.IsZoneUnlocked(toolMode.ToZoneType(), Session.State.Progression),
            _ when toolMode.IsServiceTool() => Session.Config.IsServiceUnlocked(toolMode.ToServiceType(), Session.State.Progression),
            _ => true
        };
    }

    public int? GetToolUnlockPopulationRequirement(PcToolMode toolMode)
    {
        return toolMode switch
        {
            PcToolMode.Road => Session.Config.GetPopulationRequirementForRoad(),
            PcToolMode.Bulldoze => Session.Config.GetPopulationRequirementForBulldoze(),
            _ when toolMode.IsZoneTool() => Session.Config.GetPopulationRequirementForZone(toolMode.ToZoneType()),
            _ when toolMode.IsServiceTool() => Session.Config.GetPopulationRequirementForService(toolMode.ToServiceType()),
            _ => null
        };
    }

    public string GetToolLockMessage(PcToolMode toolMode)
    {
        if (IsVictory && (toolMode.IsZoneTool() || toolMode.IsServiceTool() || toolMode == PcToolMode.Road || toolMode == PcToolMode.Bulldoze))
        {
            return "Run demo completata: avvia una nuova citta o carica un salvataggio.";
        }

        if (IsGameOver && (toolMode.IsZoneTool() || toolMode.IsServiceTool() || toolMode == PcToolMode.Road || toolMode == PcToolMode.Bulldoze))
        {
            return "Game over economico: avvia una nuova citta o carica un salvataggio.";
        }

        if (IsToolSoftLockedByOnboarding(toolMode))
        {
            return BuildOnboardingSoftLockMessage();
        }

        var requirement = GetToolUnlockPopulationRequirement(toolMode);
        return requirement.HasValue
            ? $"{toolMode.ToDisplayName()} bloccato: raggiungi {requirement.Value:N0} abitanti."
            : $"{toolMode.ToDisplayName()} bloccato dalla progressione.";
    }

    private GameCommand CreateCommand(GameCommandType commandType)
    {
        return new GameCommand
        {
            Type = commandType,
            ClientId = Session.ClientId,
            ClientSequence = ++_clientSequence,
            IssuedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static (PcStatusTone Tone, PcNotificationCategory Category) ResolveNotificationStyle(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return (PcStatusTone.Neutral, PcNotificationCategory.Allerta);
        }

        if (code.StartsWith("milestone:", StringComparison.Ordinal))
        {
            return (PcStatusTone.Neutral, PcNotificationCategory.Milestone);
        }

        if (code.StartsWith("event:", StringComparison.Ordinal))
        {
            return (PcStatusTone.Neutral, PcNotificationCategory.Eventi);
        }

        if (code.StartsWith("services:", StringComparison.Ordinal))
        {
            return (PcStatusTone.Warning, PcNotificationCategory.Servizi);
        }

        if (code.StartsWith("economy:", StringComparison.Ordinal) ||
            code.StartsWith("loan:", StringComparison.Ordinal))
        {
            return (PcStatusTone.Neutral, PcNotificationCategory.Economia);
        }

        if (code.StartsWith("bailout:", StringComparison.Ordinal))
        {
            return (PcStatusTone.Warning, PcNotificationCategory.Economia);
        }

        if (code.StartsWith("alert:", StringComparison.Ordinal))
        {
            return (PcStatusTone.Error, PcNotificationCategory.Allerta);
        }

        return (PcStatusTone.Neutral, PcNotificationCategory.Allerta);
    }

    private void HandleCoordinatorStatusChanged(string message, PcStatusTone tone)
    {
        Notifications.Push(message, tone, PcNotificationCategory.Allerta);
        StatusMessage = message;
        StatusTone = tone;
    }

    private void SetStatus(
        string message,
        PcStatusTone tone = PcStatusTone.Neutral,
        PcNotificationCategory category = PcNotificationCategory.Allerta)
    {
        StatusMessage = message;
        StatusTone = tone;
        Notifications.Push(message, tone, category);
    }

    private void PushUndoState(WorldState state)
    {
        _undoStack.Push(state);
        while (_undoStack.Count > _historyCapacity)
        {
            var trimmed = _undoStack.Reverse().ToList();
            trimmed.RemoveAt(0);
            _undoStack.Clear();
            foreach (var entry in trimmed)
            {
                _undoStack.Push(entry);
            }
        }
    }

    private string BuildInspectorTitle()
    {
        if (!ToolState.HoverCell.HasValue)
        {
            return "Ispezione";
        }

        var cell = ToolState.HoverCell.Value;
        var building = Session.State.Buildings.FirstOrDefault(existing => existing.Cell.Equals(cell));
        if (building is not null)
        {
            return building.ServiceType != ServiceType.None
                ? $"Servizio {building.ServiceType}"
                : $"Edificio {building.ZoneType} L{building.Level}";
        }

        var lot = Session.State.Lots.FirstOrDefault(existing => existing.Cell.Equals(cell));
        if (lot is not null)
        {
            return lot.ZoneType == ZoneType.None ? "Lotto tecnico" : $"Lotto {lot.ZoneType}";
        }

        return $"Cella {cell}";
    }

    private string BuildInspectorBody()
    {
        if (!ToolState.HoverCell.HasValue)
        {
            return "Muovi il cursore sulla mappa per leggere stato, blocchi crescita e servizi.";
        }

        var cell = ToolState.HoverCell.Value;
        var lot = Session.State.Lots.FirstOrDefault(existing => existing.Cell.Equals(cell));
        var building = Session.State.Buildings.FirstOrDefault(existing => existing.Cell.Equals(cell));
        var road = FindRoadSegmentAtCell(cell);

        if (building is not null)
        {
            var parentLot = Session.State.Lots.FirstOrDefault(existing => existing.Id == building.LotId);
            return
                $"Residenti {building.Residents}\n" +
                $"Lavori {building.Jobs}\n" +
                $"Condizione {PcBootstrapController.FormatPercent(building.Condition)}\n" +
                $"Vitalita distretto {building.DistrictVitality:0.00}\n" +
                $"Blocchi crescita: {BuildGrowthBlockers(parentLot, building.ZoneType)}\n" +
                $"Azione consigliata: {BuildRecommendedAction(parentLot, building.ZoneType)}";
        }

        if (lot is not null)
        {
            return
                $"Valore suolo {PcBootstrapController.FormatPercent(lot.LandValue)}\n" +
                $"Strada {FormatBool(lot.HasRoadAccess)}  Elec {FormatBool(lot.HasElectricity)}  Acqua {FormatBool(lot.HasWater)}\n" +
                $"Fogne {FormatBool(lot.HasSewage)}  Rifiuti {FormatBool(lot.HasWaste)}\n" +
                $"Vitalita distretto {lot.DistrictVitality:0.00}\n" +
                $"Blocchi crescita: {BuildGrowthBlockers(lot, lot.ZoneType)}\n" +
                $"Azione consigliata: {BuildRecommendedAction(lot, lot.ZoneType)}";
        }

        if (road is not null)
        {
            return $"Strada {road.RoadTypeId}\nCorsie {road.Lanes}\nCongestione {PcBootstrapController.FormatPercent(road.Congestion)}";
        }

        return "Terreno libero.";
    }

    private string BuildCostPreview()
    {
        if (!IsToolUnlocked(ToolState.ActiveToolMode))
        {
            return GetToolLockMessage(ToolState.ActiveToolMode);
        }

        if (!ToolState.HoverCell.HasValue)
        {
            return "Anteprima: muovi il cursore nell'area di costruzione.";
        }

        var cell = ToolState.HoverCell.Value;
        return ToolState.ActiveToolMode switch
            {
                PcToolMode.Road when ToolState.DragStartCell.HasValue => BuildRoadPreview(ToolState.DragStartCell.Value, cell),
                _ when ToolState.ActiveToolMode.IsServiceTool() => BuildServicePreview(),
                _ when ToolState.ActiveToolMode.IsZoneTool() => BuildZonePreview(),
                PcToolMode.Bulldoze => AppendLastCommandRejectionHint("Anteprima: rimuove l'asset selezionato e rimborsa una quota del costo."),
                _ => "Anteprima non disponibile."
            };
    }

    private string BuildRoadPreview(Int2 startCell, Int2 endCell)
    {
        var definition = Session.Config.ResolveRoadType(ToolState.SelectedRoadTypeId, ToolState.SelectedRoadLanes);
        var length = startCell.EuclideanDistance(endCell);
        var estimate = Math.Round((decimal)length * definition.BuildCostPerUnit, 2);
        var pressure = Session.State.DemoRun.TrafficPressure;
        var preview =
            $"Anteprima: strada {definition.Id}, lunghezza {length:0.0}, costo {estimate:N0}.\n" +
            $"Impatto previsto: {(pressure >= 0.66f ? "alleggerisce pressione traffico alta" : "incremento connessioni rete viaria")}.";
        return AppendLastCommandRejectionHint(preview);
    }

    private string BuildServicePreview()
    {
        var service = Session.Config.ResolveService(ToolState.ActiveToolMode.ToServiceType());
        var servicePressure = Session.State.DemoRun.ServicePressure;
        var preview =
            $"Anteprima: costruzione {ToolState.ActiveToolMode.ToDisplayName()} a {service.BuildCost:N0}, upkeep {service.DailyUpkeep:N0}/giorno.\n" +
            $"Impatto previsto: {(servicePressure >= 0.60f ? "priorita alta: copertura servizi critica" : "stabilizza crescita e valore suolo")}.";
        return AppendLastCommandRejectionHint(preview);
    }

    private string BuildZonePreview()
    {
        var zone = Session.Config.ResolveZone(ToolState.ActiveToolMode.ToZoneType());
        var demand = ToolState.ActiveToolMode.ToZoneType() switch
        {
            ZoneType.Residential => Session.State.Demand.Residential,
            ZoneType.Commercial => Session.State.Demand.Commercial,
            ZoneType.Industrial => Session.State.Demand.Industrial,
            ZoneType.Office => Session.State.Demand.Office,
            _ => 0f
        };
        var preview =
            $"Anteprima: zonizzazione {ToolState.ActiveToolMode.ToDisplayName()}, crescita soglia {zone.SpawnGrowthThreshold:0.0}.\n" +
            $"Impatto previsto: domanda {demand:0.00}, vitalita media {Session.State.DemoRun.AverageDistrictVitality:0.00}.\n" +
            "Valuta copertura servizi e accesso strada prima di espandere.";
        return AppendLastCommandRejectionHint(preview);
    }

    private string BuildGrowthBlockers(ZoneLot? lot, ZoneType zoneType)
    {
        if (lot is null || zoneType == ZoneType.None)
        {
            return "n/d";
        }

        var blockers = new List<string>();
        if (!lot.HasRoadAccess)
        {
            blockers.Add("manca strada");
        }

        if (!lot.HasElectricity)
        {
            blockers.Add("manca elettricita");
        }

        if (!lot.HasWater)
        {
            blockers.Add("manca acqua");
        }

        if (!lot.HasSewage)
        {
            blockers.Add("mancano fogne");
        }

        if (!lot.HasWaste)
        {
            blockers.Add("raccolta rifiuti assente");
        }

        var demand = zoneType switch
        {
            ZoneType.Residential => Session.State.Demand.Residential,
            ZoneType.Commercial => Session.State.Demand.Commercial,
            ZoneType.Industrial => Session.State.Demand.Industrial,
            ZoneType.Office => Session.State.Demand.Office,
            _ => 0f
        };
        if (demand < 0.25f)
        {
            blockers.Add($"domanda bassa ({demand:0.00})");
        }

        var taxRate = zoneType switch
        {
            ZoneType.Residential => Session.State.Budget.TaxRateResidential,
            ZoneType.Commercial => Session.State.Budget.TaxRateCommercial,
            ZoneType.Industrial => Session.State.Budget.TaxRateIndustrial,
            ZoneType.Office => Session.State.Budget.TaxRateOffice,
            _ => 0m
        };
        if (taxRate >= 0.16m)
        {
            blockers.Add($"tasse alte ({taxRate * 100m:0}%)");
        }

        if (Session.State.RunState.IsGameOver)
        {
            blockers.Add("partita in game over economico");
        }
        else if (Session.State.RunState.IsVictory)
        {
            blockers.Add("run demo gia completata");
        }

        return blockers.Count == 0 ? "nessuno" : string.Join(", ", blockers);
    }

    private string BuildRecommendedAction(ZoneLot? lot, ZoneType zoneType)
    {
        if (lot is null)
        {
            return "espandi rete stradale e zonizza il lotto.";
        }

        if (!lot.HasRoadAccess)
        {
            return "collega il lotto a una strada.";
        }

        if (!lot.HasElectricity || !lot.HasWater || !lot.HasSewage || !lot.HasWaste)
        {
            return "completa la copertura utility prima di espandere.";
        }

        var demand = zoneType switch
        {
            ZoneType.Residential => Session.State.Demand.Residential,
            ZoneType.Commercial => Session.State.Demand.Commercial,
            ZoneType.Industrial => Session.State.Demand.Industrial,
            ZoneType.Office => Session.State.Demand.Office,
            _ => 0f
        };

        if (demand < 0.30f)
        {
            return "riduci pressione fiscale o migliora servizi per rialzare la domanda.";
        }

        if (lot.DistrictVitality < 0.45f)
        {
            return "migliora traffico e servizi civici per alzare la vitalita del distretto.";
        }

        return "area in salute: continua espansione graduale.";
    }

    private static string FormatBool(bool value)
    {
        return value ? "ok" : "no";
    }

    private string AppendLastCommandRejectionHint(string preview)
    {
        if (string.IsNullOrWhiteSpace(LastRejectedCommandMessage))
        {
            return preview;
        }

        return $"{preview}\nUltimo blocco comando: {LastRejectedCommandMessage}";
    }

    private string BuildRunLockedMessage(string suffix)
    {
        if (IsVictory)
        {
            return $"Run completata: {suffix}.";
        }

        return $"Game over economico: {suffix}.";
    }

    private void EmitDailyDigestIfNeeded()
    {
        var day = Math.Max(1, Session.State.Time.Day);
        if (day < _lastDigestDay)
        {
            _lastDigestDay = day;
            return;
        }

        if (day == _lastDigestDay)
        {
            return;
        }

        _lastDigestDay = day;
        var state = Session.State;
        var run = state.RunState;
        var message =
            $"Report giorno {day}: pop {state.Population:N0}, cassa {state.Budget.Cash:N0}, netto {state.Budget.LastDailyNet:N0}/giorno.";

        if (run.ActiveModifiers.Count > 0)
        {
            message += $" Effetti attivi: {run.ActiveModifiers.Count}.";
        }

        if (run.FiscalDistressHours > 0f)
        {
            message += $" Rischio collasso {run.FiscalDistressHours:0.0}/24h.";
        }

        Notifications.Push(message, PcStatusTone.Neutral, PcNotificationCategory.Economia);
    }

    private void EmitOnboardingProgressIfNeeded()
    {
        var demo = Session.State.DemoRun;
        var completedSteps = Math.Max(0, demo.OnboardingCompletedSteps);
        if (completedSteps <= _lastOnboardingStep)
        {
            return;
        }

        _lastOnboardingStep = completedSteps;
        if (demo.OnboardingCompleted)
        {
            Notifications.Push(
                "Onboarding completato: tutti gli strumenti sono ora disponibili.",
                PcStatusTone.Neutral,
                PcNotificationCategory.Milestone);
            return;
        }

        Notifications.Push(
            $"Onboarding {OnboardingProgressLabel}: {demo.OnboardingStepTitle}.",
            PcStatusTone.Neutral,
            PcNotificationCategory.Milestone);
    }

    private string BuildCityDiagnosis()
    {
        var state = Session.State;
        var run = state.RunState;
        var demo = state.DemoRun;
        if (run.IsGameOver)
        {
            return "Partita bloccata dal collasso economico. Carica un salvataggio o avvia una nuova citta.";
        }

        if (run.IsVictory)
        {
            return $"Obiettivo demo completato. {run.VictoryReason}";
        }

        var priorities = new List<string>();
        if (IsOnboardingActive)
        {
            priorities.Add(
                $"Onboarding {OnboardingProgressLabel}: {demo.OnboardingStepInstruction} " +
                $"(focus: {demo.OnboardingFocusTool}).");
        }

        if (run.FiscalDistressHours > 0f || state.Budget.Cash <= -15000m)
        {
            priorities.Add(
                $"Emergenza bilancio: cassa {state.Budget.Cash:N0}, rischio {run.FiscalDistressHours:0.0}/24h. " +
                "Riduci costi servizi o migliora entrate subito.");
        }

        if (state.Budget.LastDailyNet < 0m)
        {
            priorities.Add($"Bilancio in deficit: {state.Budget.LastDailyNet:N0}/giorno. Controlla upkeep e aliquote.");
        }

        if (state.Utilities.AverageServiceCoverage < 0.80f)
        {
            priorities.Add(
                $"Copertura servizi bassa ({PcBootstrapController.FormatPercent(state.Utilities.AverageServiceCoverage)}). " +
                "Espandi elettricita/acqua/fogne/rifiuti sulle nuove zone.");
        }

        if (state.AverageTrafficCongestion >= 0.60f)
        {
            priorities.Add(
                $"Traffico critico ({PcBootstrapController.FormatPercent(state.AverageTrafficCongestion)}). " +
                "Aggiungi collegamenti stradali o riduci espansione in colli di bottiglia.");
        }

        var (weakDemandName, weakDemandValue) = ResolveWeakestDemand(state);
        if (weakDemandValue < 0.25f)
        {
            priorities.Add(
                $"Domanda {weakDemandName} debole ({weakDemandValue:0.00}). " +
                "Controlla tasse, servizi e accesso stradale dei lotti.");
        }

        if (demo.AverageDistrictVitality < 0.52f)
        {
            priorities.Add(
                $"Vitalita quartieri bassa ({demo.AverageDistrictVitality:0.00}). " +
                "Riduci congestione e migliora copertura servizi.");
        }

        if (priorities.Count == 0)
        {
            var target = Math.Max(1, state.Progression.NextMilestonePopulationTarget);
            return
                $"Stato stabile. Obiettivo: {state.Progression.NextMilestoneName} " +
                $"({state.Population:N0}/{target:N0} abitanti). " +
                $"Pressioni E/S/T: {demo.EconomicPressure:0.00}/{demo.ServicePressure:0.00}/{demo.TrafficPressure:0.00}.";
        }

        return string.Join("\n", priorities.Take(3));
    }

    private string BuildActiveModifiersSummary()
    {
        var run = Session.State.RunState;
        if (run.ActiveModifiers.Count == 0)
        {
            return "Nessun effetto temporaneo attivo.";
        }

        var currentHour = Session.State.Progression.TotalSimulatedHours;
        var lines = run.ActiveModifiers
            .OrderBy(modifier => modifier.ExpiresAtHour)
            .Take(2)
            .Select(modifier =>
            {
                var remaining = Math.Max(0f, modifier.ExpiresAtHour - currentHour);
                return $"{modifier.Label} ({remaining:0.0}h): {DescribeModifierImpact(modifier)}";
            })
            .ToList();

        if (run.ActiveModifiers.Count > 2)
        {
            lines.Add($"+{run.ActiveModifiers.Count - 2} altri effetti attivi.");
        }

        return string.Join("\n", lines);
    }

    private static string DescribeModifierImpact(ActiveTimedModifierState modifier)
    {
        var impacts = new List<string>();
        AddImpact(impacts, "crescita", modifier.GrowthMultiplier);
        AddImpact(impacts, "costi servizi", modifier.ServiceCostMultiplier);
        AddImpact(impacts, "manutenzione strade", modifier.RoadMaintenanceMultiplier);
        AddImpact(impacts, "pendolarismo", modifier.CommuteMinutesMultiplier);
        AddImpact(impacts, "entrate fiscali", modifier.TaxIncomeMultiplier);

        if (impacts.Count == 0)
        {
            return "impatto neutro";
        }

        return string.Join(", ", impacts);
    }

    private static void AddImpact(List<string> impacts, string label, float multiplier)
    {
        var delta = multiplier - 1f;
        if (Math.Abs(delta) < 0.02f)
        {
            return;
        }

        var percent = delta * 100f;
        impacts.Add($"{label} {(percent >= 0f ? "+" : string.Empty)}{percent:0}%");
    }

    private static (string Name, float Value) ResolveWeakestDemand(WorldState state)
    {
        var demands = new List<(string Name, float Value)>
        {
            ("residenziale", state.Demand.Residential),
            ("commerciale", state.Demand.Commercial),
            ("industriale", state.Demand.Industrial),
            ("uffici", state.Demand.Office)
        };

        return demands.OrderBy(demand => demand.Value).First();
    }

    private bool IsToolSoftLockedByOnboarding(PcToolMode toolMode)
    {
        if (!IsOnboardingSoftLockActive)
        {
            return false;
        }

        var completedSteps = Math.Max(0, Session.State.DemoRun.OnboardingCompletedSteps);
        if (completedSteps <= 0)
        {
            return toolMode != PcToolMode.Road;
        }

        if (completedSteps == 1)
        {
            return !(toolMode == PcToolMode.Road || toolMode.IsZoneTool());
        }

        return false;
    }

    private string BuildOnboardingSoftLockMessage()
    {
        var demo = Session.State.DemoRun;
        if (!IsOnboardingSoftLockActive)
        {
            return string.Empty;
        }

        return
            $"Tutorial {OnboardingProgressLabel}: {demo.OnboardingStepInstruction} " +
            $"(focus: {demo.OnboardingFocusTool}).";
    }

    private string BuildOnboardingSummary()
    {
        var demo = Session.State.DemoRun;
        if (!demo.TutorialEnabled)
        {
            return "Tutorial disattivato per questa run.";
        }

        if (demo.OnboardingCompleted)
        {
            return "Onboarding completato: guida contestuale terminata, tools sbloccati.";
        }

        var lockText = demo.SoftInputLock ? "lock morbido attivo" : "lock morbido disattivo";
        return
            $"Step {OnboardingProgressLabel}: {demo.OnboardingStepTitle}\n" +
            $"{demo.OnboardingStepInstruction}\n" +
            $"Focus: {demo.OnboardingFocusTool} | {lockText}";
    }

    private static List<Int2> CreateRectangle(Int2 startCell, Int2 endCell)
    {
        var cells = new List<Int2>();
        var minX = Math.Min(startCell.X, endCell.X);
        var maxX = Math.Max(startCell.X, endCell.X);
        var minY = Math.Min(startCell.Y, endCell.Y);
        var maxY = Math.Max(startCell.Y, endCell.Y);

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                cells.Add(new Int2(x, y));
            }
        }

        return cells;
    }

    private RoadSegment? FindRoadSegmentAtCell(Int2 cell)
    {
        var target = new PampaSkylines.Shared.Int2(cell.X, cell.Y);
        return Session.State.RoadSegments.FirstOrDefault(segment =>
        {
            var from = Session.State.RoadNodes.FirstOrDefault(node => node.Id == segment.FromNodeId);
            var to = Session.State.RoadNodes.FirstOrDefault(node => node.Id == segment.ToNodeId);
            if (from is null || to is null)
            {
                return false;
            }

            return DistancePointToSegment(target, from.Position, to.Position) <= 0.45f;
        });
    }

    private static float DistancePointToSegment(Int2 point, Int2 segmentStart, Int2 segmentEnd)
    {
        var startX = segmentStart.X;
        var startY = segmentStart.Y;
        var deltaX = segmentEnd.X - startX;
        var deltaY = segmentEnd.Y - startY;
        var lengthSquared = (deltaX * deltaX) + (deltaY * deltaY);
        if (lengthSquared == 0)
        {
            return (float)point.EuclideanDistance(segmentStart);
        }

        var t = ((point.X - startX) * deltaX + (point.Y - startY) * deltaY) / (float)lengthSquared;
        t = Math.Clamp(t, 0f, 1f);
        var projectedX = startX + (deltaX * t);
        var projectedY = startY + (deltaY * t);
        var dx = point.X - projectedX;
        var dy = point.Y - projectedY;
        return (float)Math.Sqrt((dx * dx) + (dy * dy));
    }
}
}

#nullable enable

namespace PampaSkylines.PC
{
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PampaSkylines.Core;
using PampaSkylines.SaveSync;
using PampaSkylines.Shared;
using PampaSkylines.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PcBootstrapController : MonoBehaviour
{
    [SerializeField] private string cityName = "Sandbox";
    [SerializeField] private float realSecondsPerTick = 0.20f;
    [SerializeField] private float simulatedHoursPerTick = 0.25f;
    [SerializeField] private float autosaveIntervalSeconds = 45f;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private int visibleGridHalfExtent = 24;
    [SerializeField] private bool startPaused;
    [SerializeField] private string remoteSyncBaseUrl = string.Empty;
    [SerializeField] private string remoteSyncAccessToken = string.Empty;
    [SerializeField] private PcVisualTheme? visualTheme;
    [SerializeField] private PcWorldView? worldView;
    [SerializeField] private PcHudController? hud;
    [SerializeField] private Camera? managedCamera;
    [SerializeField] private Light? managedLight;

    private static readonly PcToolMode[] ToolBarModes =
    {
        PcToolMode.Road,
        PcToolMode.Residential,
        PcToolMode.Commercial,
        PcToolMode.Industrial,
        PcToolMode.Office,
        PcToolMode.Electricity,
        PcToolMode.Water,
        PcToolMode.Sewage,
        PcToolMode.Waste,
        PcToolMode.Fire,
        PcToolMode.Police,
        PcToolMode.Health,
        PcToolMode.Education,
        PcToolMode.Bulldoze
    };

    private PcCitySessionOrchestrator? _orchestrator;
    private PcVisualTheme? _resolvedTheme;
    private readonly List<LocalCitySlotSummary> _saveSlots = new();
    private readonly List<string> _backupVersions = new();
    private string _configSource = "fallback";
    private bool _usingFallbackConfig;
    private int _newCityCounter = 1;

    private float CellSize => Mathf.Max(0.5f, cellSize);

    private int VisibleGridHalfExtent => Mathf.Max(8, visibleGridHalfExtent);

    public WorldState? CurrentState => _orchestrator?.Session.State;

    public SimulationConfig? CurrentConfig => _orchestrator?.Session.Config;

    public PcToolMode ActiveToolMode => _orchestrator?.ToolState.ActiveToolMode ?? PcToolMode.Road;

    public Int2? HoverCell => _orchestrator?.ToolState.HoverCell;

    public Int2? DragStartCell => _orchestrator?.ToolState.DragStartCell;

    public SimulationFrameReport? LastReport => _orchestrator?.LastReport;

    public string ConfigSource => _configSource;

    public bool UsingFallbackConfig => _usingFallbackConfig;

    public string StatusMessage => _orchestrator?.StatusMessage ?? "Avvio in corso...";

    public PcStatusTone StatusTone => _orchestrator?.StatusTone ?? PcStatusTone.Neutral;

    public float CurrentTimeScale => _orchestrator?.Session.State.Time.SpeedMultiplier ?? 1f;

    public bool IsPaused => _orchestrator?.Session.State.Time.IsPaused ?? true;

    public float CellWorldSize => CellSize;

    public int GridHalfExtent => VisibleGridHalfExtent;

    public IReadOnlyList<PcToolMode> AvailableToolModes => ToolBarModes;

    public string CurrentConfigVersion => _orchestrator?.Session.Config.Version ?? "non caricato";

    public PcVisualTheme Theme => _resolvedTheme ??= visualTheme ?? PcVisualTheme.LoadOrCreateDefault();

    public PcOverlayState? OverlayState => _orchestrator?.OverlayState;

    public string InspectorTitle => _orchestrator?.InspectorTitle ?? "Ispezione";

    public string InspectorBody => _orchestrator?.InspectorBody ?? "Nessuna selezione.";

    public string CostPreview => _orchestrator?.CostPreview ?? "Anteprima non disponibile.";

    public string CityDiagnosis => _orchestrator?.CityDiagnosis ?? "Diagnosi non disponibile.";

    public string ActiveModifiersSummary => _orchestrator?.ActiveModifiersSummary ?? "Nessun effetto temporaneo attivo.";

    public bool IsOnboardingActive => _orchestrator?.IsOnboardingActive ?? false;

    public bool IsOnboardingSoftLockActive => _orchestrator?.IsOnboardingSoftLockActive ?? false;

    public string OnboardingProgressLabel => _orchestrator?.OnboardingProgressLabel ?? "1/12";

    public string OnboardingSummary => _orchestrator?.OnboardingSummary ?? "Onboarding non disponibile.";

    public string NotificationSummary => _orchestrator?.Notifications.FormatRecent(5) ?? string.Empty;

    public string SaveStatusText => _orchestrator?.SaveSync.StatusText ?? "Sistema salvataggi non disponibile.";

    public string SaveRootPath => _orchestrator?.SaveSync.SaveRootPath ?? string.Empty;

    public string LastSavedVersion => _orchestrator?.SaveSync.LastSavedVersion ?? string.Empty;

    public DateTimeOffset? LastSavedAtUtc => _orchestrator?.SaveSync.LastSavedAtUtc;

    public IReadOnlyList<LocalCitySlotSummary> SaveSlots => _saveSlots;

    public IReadOnlyList<string> BackupVersions => _backupVersions;

    public bool CanUndo => _orchestrator?.CanUndo ?? false;

    public bool CanRedo => _orchestrator?.CanRedo ?? false;

    public bool EdgeScrollEnabled => _orchestrator?.OverlayState.EdgeScrollEnabled ?? true;

    public ProgressionState? ProgressionState => _orchestrator?.Progression;

    public RunState? RunState => _orchestrator?.RunState;

    public ActiveCityEventState? ActiveCityEvent => _orchestrator?.ActiveEvent;

    public bool IsGameOver => _orchestrator?.IsGameOver ?? false;

    public string GameOverReason => _orchestrator?.GameOverReason ?? string.Empty;

    public bool BudgetPolicyUnlocked => _orchestrator?.CanAdjustBudgetPolicy ?? false;

    public bool IsVictory => _orchestrator?.IsVictory ?? false;

    public bool IsRunCompleted => _orchestrator?.IsRunCompleted ?? false;

    public string RunOutcomeTitle => _orchestrator?.RunOutcomeTitle ?? "Run in corso";

    public string RunOutcomeSummary => _orchestrator?.RunOutcomeSummary ?? string.Empty;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        EnsureSession();
        EnsurePresentation();
        ApplyThemeToPresentation();
        RefreshSaveSlotsAsync();
    }

    private void Update()
    {
        if (_orchestrator is null)
        {
            return;
        }

        EnsurePresentation();
        ApplyThemeToPresentation();
        HandleHotkeys();
        UpdateHoveredCell();
        HandleWorldInput();
        AdvanceSimulation();
        UpdateLighting();

        if (worldView is not null)
        {
            worldView.BindTheme(Theme);
            worldView.Configure(CellSize, VisibleGridHalfExtent);
            worldView.Render(
                _orchestrator.Session.State,
                _orchestrator.ToolState.HoverCell,
                _orchestrator.ToolState.DragStartCell,
                _orchestrator.ToolState.ActiveToolMode,
                _orchestrator.OverlayState);
        }

        if (hud is not null)
        {
            hud.Bind(this, Theme);
            hud.Refresh();
        }
    }

    private void EnsureSession()
    {
        if (_orchestrator is not null)
        {
            return;
        }

        var config = LoadSimulationConfig();
        var session = new PcSimulationSession(cityName, config, "pc");
        session.State.Time.IsPaused = startPaused;

        var saveRoot = Path.Combine(Application.persistentDataPath, "PampaSkylines", "Cities");
        _orchestrator = new PcCitySessionOrchestrator(session, saveRoot, autosaveIntervalSeconds);

        if (!string.IsNullOrWhiteSpace(remoteSyncBaseUrl) && !string.IsNullOrWhiteSpace(remoteSyncAccessToken))
        {
            _orchestrator.SaveSync.ConfigureRemoteSync(remoteSyncBaseUrl, remoteSyncAccessToken);
        }

        _orchestrator.OverlayState.OpenMainMenu();
    }

    private SimulationConfig LoadSimulationConfig()
    {
        foreach (var candidate in GetConfigCandidates())
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            try
            {
                _configSource = candidate;
                _usingFallbackConfig = false;
                return SimulationConfigLoader.LoadFromDirectory(candidate);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Impossibile caricare il catalogo simulazione da '{candidate}': {exception.Message}");
            }
        }

        _configSource = "fallback";
        _usingFallbackConfig = true;
        return SimulationConfig.CreateFallback();
    }

    private IEnumerable<string> GetConfigCandidates()
    {
        yield return Path.Combine(Application.dataPath, "Game", "Data", "Simulation");
        yield return Path.Combine(Application.dataPath, "..", "Assets", "Game", "Data", "Simulation");
        yield return Path.Combine(Application.streamingAssetsPath, "Simulation");
    }

    private void EnsurePresentation()
    {
        managedCamera ??= GetComponentInChildren<Camera>(true);
        if (managedCamera is null)
        {
            var cameraObject = new GameObject("PcCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.SetPositionAndRotation(new Vector3(12f, 18f, -12f), Quaternion.Euler(55f, 45f, 0f));
            managedCamera = cameraObject.AddComponent<Camera>();
        }

        if (managedCamera is not null)
        {
            var cameraController = managedCamera.GetComponent<PcTopDownCameraController>();
            if (cameraController is null)
            {
                cameraController = managedCamera.gameObject.AddComponent<PcTopDownCameraController>();
            }

            if (_orchestrator?.OverlayState is not null)
            {
                cameraController.SetEdgeScrollEnabled(_orchestrator.OverlayState.EdgeScrollEnabled);
            }

            if (managedCamera.GetComponent<AudioListener>() is null && UnityEngine.Object.FindFirstObjectByType<AudioListener>() is null)
            {
                managedCamera.gameObject.AddComponent<AudioListener>();
            }
        }

        managedLight ??= GetComponentInChildren<Light>(true);
        if (managedLight is null)
        {
            var lightObject = new GameObject("PcKeyLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.SetPositionAndRotation(new Vector3(0f, 12f, 0f), Quaternion.Euler(50f, -25f, 0f));
            managedLight = lightObject.AddComponent<Light>();
        }

        if (managedLight is not null)
        {
            managedLight.type = LightType.Directional;
            managedLight.intensity = 1.16f;
            managedLight.color = new Color(1f, 0.97f, 0.93f);
            managedLight.shadows = LightShadows.Soft;
            managedLight.shadowStrength = 0.78f;
        }

        worldView ??= GetComponentInChildren<PcWorldView>(true);
        if (worldView is null)
        {
            var worldViewObject = new GameObject("PcWorldView");
            worldViewObject.transform.SetParent(transform, false);
            worldView = worldViewObject.AddComponent<PcWorldView>();
        }

        hud ??= GetComponentInChildren<PcHudController>(true);
        if (hud is null)
        {
            var canvasObject = new GameObject("PcHudCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.55f;
            canvasObject.AddComponent<GraphicRaycaster>();
            hud = canvasObject.AddComponent<PcHudController>();
        }

        worldView.BindTheme(Theme);
        hud.Bind(this, Theme);
        EnsureEventSystem();
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current is not null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.transform.SetParent(transform, false);
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void ApplyThemeToPresentation()
    {
        if (managedCamera is not null)
        {
            managedCamera.backgroundColor = Theme.CameraBackgroundColor;
        }

        RenderSettings.ambientLight = new Color(0.52f, 0.56f, 0.60f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.67f, 0.72f, 0.76f);
        RenderSettings.fogDensity = 0.0018f;
    }

    private void HandleHotkeys()
    {
        if (_orchestrator is null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectTool(PcToolMode.Road);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectTool(PcToolMode.Residential);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectTool(PcToolMode.Commercial);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SelectTool(PcToolMode.Industrial);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            SelectTool(PcToolMode.Office);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            SelectTool(PcToolMode.Electricity);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            SelectTool(PcToolMode.Water);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            SelectTool(PcToolMode.Sewage);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            SelectTool(PcToolMode.Waste);
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            SelectTool(PcToolMode.Fire);
        }
        else if (Input.GetKeyDown(KeyCode.P))
        {
            SelectTool(PcToolMode.Police);
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            SelectTool(PcToolMode.Health);
        }
        else if (Input.GetKeyDown(KeyCode.J))
        {
            SelectTool(PcToolMode.Education);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            SelectTool(PcToolMode.Bulldoze);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            _orchestrator.TogglePause();
        }

        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            _orchestrator.SetTimeScale(Mathf.Max(_orchestrator.Session.Config.Economy.MinimumTimeScale, _orchestrator.Session.State.Time.SpeedMultiplier - 1f));
        }

        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            _orchestrator.SetTimeScale(Mathf.Min(_orchestrator.Session.Config.Economy.MaximumTimeScale, _orchestrator.Session.State.Time.SpeedMultiplier + 1f));
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            _orchestrator.OverlayState.ToggleOverlay(PcOverlayKind.LandValue);
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            _orchestrator.OverlayState.ToggleOverlay(PcOverlayKind.Traffic);
        }

        if (Input.GetKeyDown(KeyCode.U))
        {
            _orchestrator.OverlayState.ToggleOverlay(PcOverlayKind.Utilities);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            _orchestrator.OverlayState.ToggleOverlay(PcOverlayKind.ServiceCoverage);
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            _orchestrator.OverlayState.ToggleGrid();
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            _orchestrator.OverlayState.ToggleHelp();
        }

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.S))
        {
            SaveNowFromHud();
        }

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Z))
        {
            _orchestrator.Undo();
        }

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Y))
        {
            _orchestrator.Redo();
        }

        if (Input.GetKeyDown(KeyCode.F5))
        {
            LoadCurrentFromHud();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_orchestrator.OverlayState.ShowRunOutcome)
            {
                _orchestrator.OverlayState.CloseRunOutcome();
                return;
            }

            _orchestrator.OverlayState.TogglePauseMenu();
        }

        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && _orchestrator.OverlayState.ShowRunOutcome)
        {
            _orchestrator.OverlayState.CloseRunOutcome();
        }
    }

    private void UpdateHoveredCell()
    {
        if (managedCamera is null || _orchestrator is null || _orchestrator.OverlayState.IsAnyModalOpen || IsPointerOverUserInterface())
        {
            _orchestrator?.SetHoverCell(null);
            return;
        }

        if (TryGetHoveredCell(out var hoveredCell))
        {
            _orchestrator.SetHoverCell(hoveredCell);
            return;
        }

        _orchestrator.SetHoverCell(null);
    }

    private bool TryGetHoveredCell(out Int2 hoveredCell)
    {
        hoveredCell = default;
        if (managedCamera is null)
        {
            return false;
        }

        var ray = managedCamera.ScreenPointToRay(Input.mousePosition);
        var groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (!groundPlane.Raycast(ray, out var distance))
        {
            return false;
        }

        var worldPoint = ray.GetPoint(distance);
        hoveredCell = new Int2(
            Mathf.RoundToInt(worldPoint.x / CellSize),
            Mathf.RoundToInt(worldPoint.z / CellSize));
        return true;
    }

    private void HandleWorldInput()
    {
        if (_orchestrator is null || !_orchestrator.ToolState.HoverCell.HasValue || _orchestrator.OverlayState.IsAnyModalOpen || IsPointerOverUserInterface())
        {
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            _orchestrator.ClearDrag();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (_orchestrator.ToolState.ActiveToolMode.IsDragTool())
            {
                _orchestrator.BeginDrag(_orchestrator.ToolState.HoverCell.Value);
            }
            else
            {
                ExecuteSingleCellTool(_orchestrator.ToolState.HoverCell.Value);
            }
        }

        if (Input.GetMouseButtonUp(0) && _orchestrator.ToolState.DragStartCell.HasValue)
        {
            ExecuteDragTool(_orchestrator.ToolState.DragStartCell.Value, _orchestrator.ToolState.HoverCell.Value);
            _orchestrator.ClearDrag();
        }
    }

    private void ExecuteSingleCellTool(Int2 cell)
    {
        if (_orchestrator is null)
        {
            return;
        }

        if (_orchestrator.ToolState.ActiveToolMode.IsServiceTool())
        {
            _orchestrator.QueueService(cell);
            return;
        }

        if (_orchestrator.ToolState.ActiveToolMode == PcToolMode.Bulldoze)
        {
            _orchestrator.QueueBulldoze(cell);
        }
    }

    private void ExecuteDragTool(Int2 startCell, Int2 endCell)
    {
        if (_orchestrator is null)
        {
            return;
        }

        if (_orchestrator.ToolState.ActiveToolMode == PcToolMode.Road)
        {
            _orchestrator.QueueRoad(startCell, endCell);
            return;
        }

        if (_orchestrator.ToolState.ActiveToolMode.IsZoneTool())
        {
            _orchestrator.QueueZone(startCell, endCell);
        }
    }

    private void AdvanceSimulation()
    {
        if (_orchestrator is null)
        {
            return;
        }

        if (_orchestrator.OverlayState.IsAnyModalOpen)
        {
            _orchestrator.SaveSync.Tick(Time.unscaledDeltaTime);
            return;
        }

        _orchestrator.Tick(
            Time.unscaledDeltaTime,
            Mathf.Max(0.02f, realSecondsPerTick),
            Mathf.Max(0.05f, simulatedHoursPerTick));
    }

    private void UpdateLighting()
    {
        if (managedLight is null || _orchestrator is null)
        {
            return;
        }

        var timeOfDay = Mathf.Repeat(_orchestrator.Session.State.Time.TimeOfDayHours, 24f);
        var normalized = timeOfDay / 24f;
        var sunPitch = Mathf.Lerp(-20f, 200f, normalized);
        managedLight.transform.rotation = Quaternion.Euler(sunPitch, -25f, 0f);

        var daylight = Mathf.Clamp01(Mathf.Sin((normalized * Mathf.PI * 2f) - (Mathf.PI * 0.5f)) * 0.5f + 0.5f);
        managedLight.intensity = Mathf.Lerp(0.38f, 1.22f, daylight);
        managedLight.color = Color.Lerp(new Color(0.48f, 0.52f, 0.60f), new Color(1f, 0.97f, 0.91f), daylight);
    }

    public void SelectTool(PcToolMode toolMode)
    {
        _orchestrator?.SelectTool(toolMode);
    }

    public void TogglePauseFromHud()
    {
        _orchestrator?.TogglePause();
    }

    public void SetTimeScaleFromHud(float speedMultiplier)
    {
        _orchestrator?.SetTimeScale(speedMultiplier);
    }

    public void ToggleOverlayFromHud(PcOverlayKind overlayKind)
    {
        _orchestrator?.OverlayState.ToggleOverlay(overlayKind);
    }

    public void ToggleGridFromHud()
    {
        _orchestrator?.OverlayState.ToggleGrid();
    }

    public void ToggleHelpFromHud()
    {
        _orchestrator?.OverlayState.ToggleHelp();
    }

    public void ToggleLoadMenuFromHud()
    {
        if (_orchestrator is null)
        {
            return;
        }

        _orchestrator.OverlayState.ToggleLoadMenu();
        if (_orchestrator.OverlayState.ShowLoadMenu)
        {
            RefreshSaveSlotsAsync();
        }
    }

    public void ToggleSettingsFromHud()
    {
        _orchestrator?.OverlayState.ToggleSettings();
    }

    public void OpenMainMenuFromHud()
    {
        _orchestrator?.OverlayState.OpenMainMenu();
    }

    public void ContinueGameFromHud()
    {
        _orchestrator?.OverlayState.CloseAllMenus();
        _orchestrator?.OverlayState.CloseRunOutcome();
    }

    public void TogglePauseMenuFromHud()
    {
        _orchestrator?.OverlayState.TogglePauseMenu();
    }

    public void CloseRunOutcomeFromHud()
    {
        _orchestrator?.OverlayState.CloseRunOutcome();
    }

    public void ToggleNotificationsFromHud()
    {
        _orchestrator?.OverlayState.ToggleNotifications();
    }

    public void UndoFromHud()
    {
        _orchestrator?.Undo();
    }

    public void RedoFromHud()
    {
        _orchestrator?.Redo();
    }

    public void IncreaseTaxFromHud(ZoneType zoneType)
    {
        _orchestrator?.AdjustTaxRate(zoneType, 0.01m);
    }

    public void DecreaseTaxFromHud(ZoneType zoneType)
    {
        _orchestrator?.AdjustTaxRate(zoneType, -0.01m);
    }

    public void ToggleInspectorFromHud()
    {
        _orchestrator?.OverlayState.ToggleInspector();
    }

    public void ToggleEdgeScrollFromHud()
    {
        if (_orchestrator?.OverlayState is null)
        {
            return;
        }

        _orchestrator.OverlayState.SetEdgeScrollEnabled(!_orchestrator.OverlayState.EdgeScrollEnabled);
    }

    public void SaveNowFromHud()
    {
        _ = SaveNowAsync();
    }

    public void LoadCurrentFromHud()
    {
        _ = LoadCurrentAsync();
    }

    public void StartNewCityFromHud()
    {
        if (_orchestrator is null)
        {
            return;
        }

        _orchestrator.StartNewCity(CreateNewCityName());
        RefreshSaveSlotsAsync();
    }

    public void LoadCityFromHud(string cityId)
    {
        _ = LoadCityAsync(cityId);
    }

    public void LoadBackupFromHud(string cityId, string version)
    {
        _ = LoadBackupAsync(cityId, version);
    }

    public void RefreshSaveSlotsFromHud()
    {
        RefreshSaveSlotsAsync();
    }

    public void ResolveEventChoiceFromHud(string eventId, string choiceId)
    {
        _orchestrator?.ResolveActiveEventChoice(eventId, choiceId);
    }

    private async Task SaveNowAsync()
    {
        if (_orchestrator is null)
        {
            return;
        }

        await _orchestrator.SaveNowAsync();
        RefreshSaveSlotsAsync();
    }

    private async Task LoadCurrentAsync()
    {
        if (_orchestrator is null)
        {
            return;
        }

        if (await _orchestrator.LoadCurrentAsync())
        {
            ContinueGameFromHud();
        }

        RefreshSaveSlotsAsync();
    }

    private async Task LoadCityAsync(string cityId)
    {
        if (_orchestrator is null)
        {
            return;
        }

        if (await _orchestrator.LoadCityAsync(cityId))
        {
            ContinueGameFromHud();
        }

        RefreshBackupVersions();
    }

    private async Task LoadBackupAsync(string cityId, string version)
    {
        if (_orchestrator is null)
        {
            return;
        }

        if (await _orchestrator.LoadVersionAsync(cityId, version))
        {
            ContinueGameFromHud();
        }

        RefreshBackupVersions();
    }

    private async void RefreshSaveSlotsAsync()
    {
        if (_orchestrator is null)
        {
            return;
        }

        try
        {
            var cities = await _orchestrator.SaveSync.ListCitiesAsync();
            _saveSlots.Clear();
            _saveSlots.AddRange(cities);
            RefreshBackupVersions();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Impossibile aggiornare gli slot salvataggio: {exception.Message}");
        }
    }

    private void RefreshBackupVersions()
    {
        _backupVersions.Clear();
        if (_orchestrator is null)
        {
            return;
        }

        _backupVersions.AddRange(_orchestrator.SaveSync.ListBackupVersions());
    }

    private string CreateNewCityName()
    {
        var name = $"Sandbox {_newCityCounter:00}";
        _newCityCounter++;
        return name;
    }

    private static bool IsPointerOverUserInterface()
    {
        return EventSystem.current is not null && EventSystem.current.IsPointerOverGameObject();
    }

    internal static string FormatHours(float hours)
    {
        var clamped = Mathf.Repeat(hours, 24f);
        var wholeHours = Mathf.FloorToInt(clamped);
        var minutes = Mathf.FloorToInt((clamped - wholeHours) * 60f);
        return $"{wholeHours:00}:{minutes:00}";
    }

    internal static string FormatPercent(float value)
    {
        return $"{Mathf.RoundToInt(value * 100f)}%";
    }

    internal static string FormatDemand(float value)
    {
        return $"{value:0.00}";
    }

    internal static string ShortHash(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return "-";
        }

        return hash.Length <= 10 ? hash : hash[..10];
    }

    public bool IsToolUnlocked(PcToolMode toolMode)
    {
        return _orchestrator?.IsToolUnlocked(toolMode) ?? true;
    }

    public string ToolLockReason(PcToolMode toolMode)
    {
        return _orchestrator?.GetToolLockMessage(toolMode) ?? string.Empty;
    }
}
}

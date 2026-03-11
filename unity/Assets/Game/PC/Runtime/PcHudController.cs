#nullable enable

namespace PampaSkylines.PC
{
using System;
using System.Collections.Generic;
using System.Linq;
using PampaSkylines.Core;
using PampaSkylines.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public sealed class PcHudController : MonoBehaviour
{
    private sealed class ButtonChrome
    {
        public Button Button = null!;

        public Image Background = null!;

        public TMP_Text Label = null!;
    }

    private PcBootstrapController? _controller;
    private PcVisualTheme? _theme;
    private TMP_FontAsset? _tmpFont;
    private Texture2D? _whiteTexture;
    private Sprite? _whiteSprite;
    private bool _built;
    private string _loadSignature = string.Empty;

    private TMP_Text? _cityText;
    private TMP_Text? _timeText;
    private TMP_Text? _configText;
    private TMP_Text? _saveText;
    private TMP_Text? _toolText;
    private TMP_Text? _hoverText;
    private TMP_Text? _statusText;
    private TMP_Text? _controlsText;
    private TMP_Text? _cashText;
    private TMP_Text? _populationText;
    private TMP_Text? _progressionText;
    private TMP_Text? _runStateText;
    private TMP_Text? _onboardingText;
    private TMP_Text? _eventTitleText;
    private TMP_Text? _eventBodyText;
    private TMP_Text? _riskText;
    private TMP_Text? _networkText;
    private TMP_Text? _demandText;
    private TMP_Text? _utilityText;
    private TMP_Text? _taxesText;
    private TMP_Text? _trafficText;
    private TMP_Text? _inspectorTitleText;
    private TMP_Text? _inspectorBodyText;
    private TMP_Text? _previewText;
    private TMP_Text? _notificationsText;
    private TMP_Text? _loadSummaryText;
    private TMP_Text? _settingsText;
    private TMP_Text? _helpText;
    private TMP_Text? _runOutcomeTitleText;
    private TMP_Text? _runOutcomeBodyText;
    private readonly Dictionary<PcToolMode, ButtonChrome> _toolButtons = new();
    private readonly Dictionary<float, ButtonChrome> _speedButtons = new();
    private readonly Dictionary<PcOverlayKind, ButtonChrome> _overlayButtons = new();
    private ButtonChrome? _pauseButton;
    private ButtonChrome? _saveButton;
    private ButtonChrome? _loadButton;
    private ButtonChrome? _menuButton;
    private ButtonChrome? _helpButton;
    private ButtonChrome? _undoButton;
    private ButtonChrome? _redoButton;
    private ButtonChrome? _gridButton;
    private ButtonChrome? _inspectorButton;
    private ButtonChrome? _notificationsButton;
    private ButtonChrome? _edgeScrollButton;
    private ButtonChrome? _resTaxDownButton;
    private ButtonChrome? _resTaxUpButton;
    private ButtonChrome? _comTaxDownButton;
    private ButtonChrome? _comTaxUpButton;
    private ButtonChrome? _indTaxDownButton;
    private ButtonChrome? _indTaxUpButton;
    private ButtonChrome? _offTaxDownButton;
    private ButtonChrome? _offTaxUpButton;
    private RectTransform? _progressionFillBar;
    private RectTransform? _riskFillBar;
    private RectTransform? _mainMenuPanel;
    private RectTransform? _pausePanel;
    private RectTransform? _loadPanel;
    private RectTransform? _settingsPanel;
    private RectTransform? _helpPanel;
    private RectTransform? _runOutcomePanel;
    private RectTransform? _loadCitiesList;
    private RectTransform? _loadBackupsList;
    private readonly List<ButtonChrome> _eventChoiceButtons = new();
    private readonly List<string> _eventChoiceIds = new();

    public void Bind(PcBootstrapController controller, PcVisualTheme theme)
    {
        _controller = controller;
        _theme = theme;
        EnsureCanvasSetup();
        EnsureBuilt();
    }

    public void Refresh()
    {
        if (!_built || _controller is null || _theme is null)
        {
            return;
        }

        var state = _controller.CurrentState;
        if (state is null)
        {
            return;
        }

        _cityText!.text = state.CityName;
        _timeText!.text = $"Giorno {state.Time.Day}  {PcBootstrapController.FormatHours(state.Time.TimeOfDayHours)}  Tick {state.Tick}";
        _configText!.text = $"{_controller.CurrentConfigVersion} | {(_controller.UsingFallbackConfig ? "catalogo fallback" : "catalogo dati")}";
        _saveText!.text =
            $"Versione {(_controller.LastSavedVersion.Length > 0 ? PcBootstrapController.ShortHash(_controller.LastSavedVersion) : "-")}\n" +
            $"{_controller.SaveStatusText}";

        _toolText!.text = $"Strumento: {_controller.ActiveToolMode.ToDisplayName()}";
        _hoverText!.text = $"Hover: {FormatCell(_controller.HoverCell)}";
        _statusText!.text = _controller.StatusMessage;
        _statusText.color = _controller.StatusTone switch
        {
            PcStatusTone.Warning => _theme.HudWarningColor,
            PcStatusTone.Error => _theme.HudErrorColor,
            _ => _theme.HudTextColor
        };

        _cashText!.text = $"Cassa {state.Budget.Cash:N0}\nNetto/giorno {state.Budget.LastDailyNet:N0}";
        _populationText!.text = $"Popolazione {state.Population:N0}\nLavori {state.Jobs:N0}";
        _networkText!.text = $"Strade {state.RoadSegments.Count}\nLotti {state.Lots.Count} | Edifici {state.Buildings.Count}";
        if (_progressionText is not null)
        {
            var progression = _controller.ProgressionState;
            if (progression is null)
            {
                _progressionText.text = "Progressione non disponibile.";
            }
            else
            {
                var target = Mathf.Max(1, progression.NextMilestonePopulationTarget);
                var progress = Mathf.Clamp01(state.Population / (float)target);
                _progressionText.text =
                    $"Milestone: {progression.CurrentMilestoneName}\n" +
                    $"Verso {progression.NextMilestoneName}: {state.Population:N0}/{target:N0}\n" +
                    $"Reward prossimo: {progression.NextMilestoneRewardCash:N0}";
                if (_progressionFillBar is not null)
                {
                    _progressionFillBar.anchorMax = new Vector2(progress, 1f);
                }
            }
        }

        var runState = _controller.RunState;
        if (_runStateText is not null)
        {
            if (runState is null)
            {
                _runStateText.text = "Consiglio cittadino non disponibile.";
            }
            else
            {
                var diagnosis = _controller.CityDiagnosis;
                var diagnosisLine = diagnosis.Split('\n').FirstOrDefault() ?? diagnosis;
                _runStateText.text =
                    $"Atto: {runState.CurrentActName}\n" +
                    $"{runState.CurrentActObjective}\n" +
                    $"Progresso: {runState.CurrentActProgressValue:N0}/{runState.CurrentActProgressTarget:N0}\n" +
                    $"Focus: {diagnosisLine}";
            }
        }

        if (_onboardingText is not null)
        {
            _onboardingText.text = _controller.OnboardingSummary;
            _onboardingText.color = _controller.IsOnboardingSoftLockActive
                ? _theme.HudWarningColor
                : _controller.IsOnboardingActive
                    ? _theme.HudTextColor
                    : _theme.HudMutedTextColor;
        }

        if (_riskText is not null)
        {
            var fiscalRisk = runState?.FiscalDistressHours ?? 0f;
            var progress = Mathf.Clamp01(fiscalRisk / 24f);
            var remaining = Mathf.Max(0f, 24f - fiscalRisk);
            var bailoutUsed = state.Progression.BailoutCount;
            var bailoutMax = _controller.CurrentConfig?.Progression.Bailout.MaxBailouts ?? 0;
            var bailoutText = bailoutMax > 0 ? $"{bailoutUsed}/{bailoutMax}" : $"{bailoutUsed}";
            _riskText.text = runState is null
                ? "Rischio collasso n/d"
                : runState.IsVictory
                    ? $"OBIETTIVO RAGGIUNTO\n{runState.VictoryReason}"
                    : runState.IsGameOver
                    ? $"GAME OVER ECONOMICO\n{runState.GameOverReason}"
                    : $"Rischio collasso: {fiscalRisk:0.0}/24.0 ore\nTempo al collasso: {remaining:0.0} ore\nBailout usati: {bailoutText}";
            _riskText.color = runState?.IsVictory == true
                ? _theme.HudAccentColor
                : runState?.IsGameOver == true
                ? _theme.HudErrorColor
                : progress >= 0.66f ? _theme.HudWarningColor : _theme.HudTextColor;
            if (_riskFillBar is not null)
            {
                _riskFillBar.anchorMax = new Vector2(
                    runState?.IsVictory == true || runState?.IsGameOver == true ? 1f : progress,
                    1f);
            }
        }

        if (_eventTitleText is not null && _eventBodyText is not null)
        {
            var activeEvent = runState?.ActiveEvent;
            if (activeEvent is null)
            {
                _eventTitleText.text = "Nessun evento attivo";
                _eventBodyText.text =
                    "In attesa della prossima sessione del Consiglio Cittadino.\n\n" +
                    "Effetti attivi\n" +
                    _controller.ActiveModifiersSummary;
            }
            else
            {
                _eventTitleText.text = activeEvent.Title;
                _eventBodyText.text =
                    $"{activeEvent.Description}\n\n" +
                    "Effetti attivi\n" +
                    _controller.ActiveModifiersSummary;
            }

            RefreshEventChoiceButtons(activeEvent);
        }

        _demandText!.text =
            $"R {PcBootstrapController.FormatDemand(state.Demand.Residential)}  C {PcBootstrapController.FormatDemand(state.Demand.Commercial)}\n" +
            $"I {PcBootstrapController.FormatDemand(state.Demand.Industrial)}  O {PcBootstrapController.FormatDemand(state.Demand.Office)}";
        _utilityText!.text =
            $"E {PcBootstrapController.FormatPercent(state.Utilities.ElectricityCoverage)}  W {PcBootstrapController.FormatPercent(state.Utilities.WaterCoverage)}\n" +
            $"S {PcBootstrapController.FormatPercent(state.Utilities.SewageCoverage)}  Wa {PcBootstrapController.FormatPercent(state.Utilities.WasteCoverage)}";
        if (_taxesText is not null)
        {
            _taxesText.text =
                $"R {(state.Budget.TaxRateResidential * 100m):0}%  C {(state.Budget.TaxRateCommercial * 100m):0}%\n" +
                $"I {(state.Budget.TaxRateIndustrial * 100m):0}%  O {(state.Budget.TaxRateOffice * 100m):0}%";
            if (!_controller.BudgetPolicyUnlocked)
            {
                _taxesText.text += "\nSblocca il Municipio (320 pop).";
            }
        }
        _trafficText!.text =
            $"Traffico {PcBootstrapController.FormatPercent(state.AverageTrafficCongestion)}\n" +
            $"Pendolarismo {state.AverageCommuteMinutes:0.0} min";
        _inspectorTitleText!.text = _controller.InspectorTitle;
        _inspectorBodyText!.text = _controller.InspectorBody;
        _previewText!.text = _controller.CostPreview;
        _notificationsText!.text = string.IsNullOrWhiteSpace(_controller.NotificationSummary)
            ? "Nessuna notifica."
            : _controller.NotificationSummary;
        _loadSummaryText!.text =
            $"Percorso salvataggi\n{_controller.SaveRootPath}\n\n" +
            $"Ultimo salvataggio {_controller.LastSavedAtUtc?.ToLocalTime().ToString("g") ?? "-"}";
        _settingsText!.text =
            $"Scorrimento bordi: {(_controller.EdgeScrollEnabled ? "Attivo" : "Disattivo")}\n" +
            $"Griglia: {(_controller.OverlayState?.ShowGrid == true ? "Attiva" : "Disattiva")}\n" +
            $"Ispezione: {(_controller.OverlayState?.ShowInspector == true ? "Attiva" : "Disattiva")}\n" +
            $"Notifiche: {(_controller.OverlayState?.ShowNotifications == true ? "Attive" : "Disattive")}\n" +
            $"Partita terminata: {(_controller.IsRunCompleted ? "SI" : "NO")}";
        _helpText!.text =
            "Strumenti costruzione\n" +
            "1 strada, 2 residenziale, 3 commerciale, 4 industriale, 5 uffici\n" +
            "6 elettricita, 7 acqua, 8 fogne, 9 rifiuti, F vigili, P polizia, H sanita, J istruzione, 0 bulldozer\n\n" +
            "Overlay\n" +
            "L valore terreno, U utility, T traffico, R copertura servizi, G griglia\n\n" +
            "Flusso\n" +
            "Drag sinistro per strade/zone. Click sinistro per servizi e bulldozer.\n" +
            "Segui il pannello onboarding per step contestuali e lock morbidi iniziali.\n" +
            "Rispondi agli eventi nel riquadro Consiglio Cittadino per gestire crisi e bonus.\n" +
            "Esc apre il menu pausa.";
        if (_runOutcomeTitleText is not null)
        {
            _runOutcomeTitleText.text = _controller.RunOutcomeTitle;
        }

        if (_runOutcomeBodyText is not null)
        {
            _runOutcomeBodyText.text = _controller.RunOutcomeSummary;
        }
        _controlsText!.text =
            "WASD movimento, drag centrale pan, Q/E rotazione, rotella zoom\n" +
            "Ctrl+S salva, Ctrl+Z annulla, Ctrl+Y ripeti, F5 carica, F1 aiuto";

        ApplyButtonState();
        UpdateModalVisibility();
        RefreshLoadLists();
    }

    private void EnsureCanvasSetup()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler is null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.55f;

        if (GetComponent<GraphicRaycaster>() is null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void EnsureBuilt()
    {
        if (_built || _theme is null)
        {
            return;
        }

        var root = CreateRect("HudRoot", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var topBar = CreatePanel("TopBar", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -110f), new Vector2(-18f, -18f), _theme.HudPanelColor);
        BuildTopBar(topBar);

        var leftRail = CreatePanel("ToolRail", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(18f, 110f), new Vector2(220f, -110f), _theme.HudPanelSecondaryColor);
        BuildToolRail(leftRail);

        var rightPanel = CreatePanel("StatsPanel", root, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-372f, 110f), new Vector2(-18f, -110f), _theme.HudPanelSecondaryColor);
        BuildStatsPanel(rightPanel);

        var bottomBar = CreatePanel("BottomBar", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(236f, 18f), new Vector2(-390f, 108f), _theme.HudPanelColor);
        BuildBottomBar(bottomBar);

        var modalLayer = CreateRect("ModalLayer", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        BuildModalLayer(modalLayer);

        _built = true;
    }

    private void BuildTopBar(RectTransform parent)
    {
        ConfigureHorizontalLayout(parent.gameObject, 16, 16, 12, 12, 12);

        var infoStack = CreateLayoutPanel("InfoStack", parent, _theme!.HudPanelSecondaryColor);
        ConfigureVerticalLayout(infoStack.gameObject, 10, 10, 10, 10, 4);
        infoStack.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        _cityText = CreateText("CityText", infoStack, 24, FontStyle.Bold, _theme.HudTextColor, TextAnchor.MiddleLeft);
        _timeText = CreateText("TimeText", infoStack, 16, FontStyle.Normal, _theme.HudTextColor, TextAnchor.MiddleLeft);
        _configText = CreateText("ConfigText", infoStack, 13, FontStyle.Normal, _theme.HudMutedTextColor, TextAnchor.MiddleLeft);

        var savePanel = CreateLayoutPanel("SavePanel", parent, _theme.HudPanelSecondaryColor);
        ConfigureVerticalLayout(savePanel.gameObject, 10, 10, 10, 10, 4);
        savePanel.gameObject.AddComponent<LayoutElement>().preferredWidth = 280f;
        CreateText("SaveHeader", savePanel, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Salvataggio");
        _saveText = CreateText("SaveBody", savePanel, 13, FontStyle.Normal, _theme.HudTextColor, TextAnchor.UpperLeft);

        var controlsPanel = CreateLayoutPanel("ControlsPanel", parent, _theme.HudPanelSecondaryColor);
        var controlsLayout = controlsPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        controlsLayout.spacing = 8f;
        controlsLayout.childControlWidth = true;
        controlsLayout.childControlHeight = true;
        controlsLayout.childForceExpandWidth = false;
        controlsLayout.childForceExpandHeight = true;
        controlsLayout.padding = new RectOffset(10, 10, 10, 10);
        var controlsElement = controlsPanel.gameObject.AddComponent<LayoutElement>();
        controlsElement.preferredWidth = 860f;
        controlsElement.minWidth = 760f;

        _menuButton = CreateButton("MenuButton", controlsPanel, "Menu", () => _controller?.OpenMainMenuFromHud());
        _saveButton = CreateButton("SaveButton", controlsPanel, "Salva", () => _controller?.SaveNowFromHud());
        _loadButton = CreateButton("LoadButton", controlsPanel, "Carica", () => _controller?.ToggleLoadMenuFromHud());
        _helpButton = CreateButton("HelpButton", controlsPanel, "Aiuto", () => _controller?.ToggleHelpFromHud());
        _pauseButton = CreateButton("PauseButton", controlsPanel, "Pausa", () => _controller?.TogglePauseFromHud());
        _speedButtons[1f] = CreateButton("Speed1Button", controlsPanel, "1x", () => _controller?.SetTimeScaleFromHud(1f));
        _speedButtons[2f] = CreateButton("Speed2Button", controlsPanel, "2x", () => _controller?.SetTimeScaleFromHud(2f));
        _speedButtons[4f] = CreateButton("Speed4Button", controlsPanel, "4x", () => _controller?.SetTimeScaleFromHud(4f));
    }

    private void BuildToolRail(RectTransform parent)
    {
        ConfigureVerticalLayout(parent.gameObject, 12, 12, 12, 12, 8);
        CreateText("ToolsHeader", parent, 16, FontStyle.Bold, _theme!.HudTextColor, TextAnchor.MiddleLeft, "Costruzione");

        foreach (var toolMode in _controller!.AvailableToolModes)
        {
            var cachedMode = toolMode;
            _toolButtons[cachedMode] = CreateButton(
                $"{cachedMode}Button",
                parent,
                cachedMode.ToDisplayName(),
                () => _controller?.SelectTool(cachedMode));
        }

        CreateText("OverlayHeader", parent, 15, FontStyle.Bold, _theme.HudTextColor, TextAnchor.MiddleLeft, "Overlay");
        _overlayButtons[PcOverlayKind.LandValue] = CreateButton("LandValueButton", parent, "Valore suolo", () => _controller?.ToggleOverlayFromHud(PcOverlayKind.LandValue));
        _overlayButtons[PcOverlayKind.Utilities] = CreateButton("UtilitiesButton", parent, "Utility", () => _controller?.ToggleOverlayFromHud(PcOverlayKind.Utilities));
        _overlayButtons[PcOverlayKind.Traffic] = CreateButton("TrafficButton", parent, "Traffico", () => _controller?.ToggleOverlayFromHud(PcOverlayKind.Traffic));
        _overlayButtons[PcOverlayKind.ServiceCoverage] = CreateButton("CoverageButton", parent, "Copertura", () => _controller?.ToggleOverlayFromHud(PcOverlayKind.ServiceCoverage));

        CreateText("ActionsHeader", parent, 15, FontStyle.Bold, _theme.HudTextColor, TextAnchor.MiddleLeft, "Azioni");
        _undoButton = CreateButton("UndoButton", parent, "Annulla", () => _controller?.UndoFromHud());
        _redoButton = CreateButton("RedoButton", parent, "Ripeti", () => _controller?.RedoFromHud());
        CreateButton("NewCityButton", parent, "Nuova citta", () => _controller?.StartNewCityFromHud());
        _gridButton = CreateButton("GridButton", parent, "Mostra griglia", () => _controller?.ToggleGridFromHud());
        _inspectorButton = CreateButton("InspectorButton", parent, "Ispeziona", () => _controller?.ToggleInspectorFromHud());
        _notificationsButton = CreateButton("NotificationsButton", parent, "Notifiche", () => _controller?.ToggleNotificationsFromHud());
    }

    private void BuildStatsPanel(RectTransform parent)
    {
        ConfigureVerticalLayout(parent.gameObject, 12, 12, 12, 12, 10);
        _cashText = CreateCard(parent, "Bilancio");
        _populationText = CreateCard(parent, "Popolazione");

        var progressionCard = CreateLayoutPanel("ProgressionCard", parent, _theme!.HudCardColor);
        ConfigureVerticalLayout(progressionCard.gameObject, 10, 10, 10, 10, 4);
        progressionCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 112f;
        CreateText("ProgressionHeader", progressionCard, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Progressione");
        _progressionText = CreateText("ProgressionBody", progressionCard, 13, FontStyle.Normal, _theme.HudTextColor, TextAnchor.UpperLeft);
        var progressionTrack = CreateLayoutPanel("ProgressionTrack", progressionCard, _theme.HudPanelSecondaryColor);
        progressionTrack.gameObject.AddComponent<LayoutElement>().preferredHeight = 12f;
        _progressionFillBar = CreateRect("ProgressionFill", progressionTrack, new Vector2(0f, 0f), new Vector2(0.01f, 1f), Vector2.zero, Vector2.zero);
        var fillImage = _progressionFillBar.gameObject.AddComponent<Image>();
        fillImage.sprite = GetWhiteSprite();
        fillImage.type = Image.Type.Sliced;
        fillImage.color = _theme.HudAccentColor;

        var councilCard = CreateLayoutPanel("CouncilCard", parent, _theme.HudCardColor);
        ConfigureVerticalLayout(councilCard.gameObject, 10, 10, 10, 10, 4);
        councilCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 150f;
        CreateText("CouncilHeader", councilCard, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Consiglio Cittadino");
        _runStateText = CreateText("CouncilBody", councilCard, 13, FontStyle.Normal, _theme.HudTextColor, TextAnchor.UpperLeft);
        _runStateText.overflowMode = TextOverflowModes.Truncate;

        var onboardingCard = CreateLayoutPanel("OnboardingCard", parent, _theme.HudCardColor);
        ConfigureVerticalLayout(onboardingCard.gameObject, 10, 10, 10, 10, 4);
        onboardingCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 128f;
        CreateText("OnboardingHeader", onboardingCard, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Onboarding guidato");
        _onboardingText = CreateText("OnboardingBody", onboardingCard, 13, FontStyle.Normal, _theme.HudTextColor, TextAnchor.UpperLeft);
        _onboardingText.overflowMode = TextOverflowModes.Truncate;

        var riskCard = CreateLayoutPanel("CollapseRiskCard", parent, _theme.HudCardColor);
        ConfigureVerticalLayout(riskCard.gameObject, 10, 10, 10, 10, 4);
        riskCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;
        CreateText("RiskHeader", riskCard, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Barra rischio collasso");
        var riskTrack = CreateLayoutPanel("RiskTrack", riskCard, _theme.HudPanelSecondaryColor);
        riskTrack.gameObject.AddComponent<LayoutElement>().preferredHeight = 12f;
        _riskFillBar = CreateRect("RiskFill", riskTrack, new Vector2(0f, 0f), new Vector2(0.01f, 1f), Vector2.zero, Vector2.zero);
        var riskFillImage = _riskFillBar.gameObject.AddComponent<Image>();
        riskFillImage.sprite = GetWhiteSprite();
        riskFillImage.type = Image.Type.Sliced;
        riskFillImage.color = _theme.HudErrorColor;
        _riskText = CreateText("RiskBody", riskCard, 13, FontStyle.Normal, _theme.HudTextColor, TextAnchor.UpperLeft);

        var eventCard = CreateLayoutPanel("ActiveEventCard", parent, _theme.HudCardColor);
        ConfigureVerticalLayout(eventCard.gameObject, 10, 10, 10, 10, 6);
        eventCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 248f;
        CreateText("EventHeader", eventCard, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Evento attivo");
        _eventTitleText = CreateText("EventTitle", eventCard, 14, FontStyle.Bold, _theme.HudTextColor, TextAnchor.UpperLeft);
        _eventBodyText = CreateText("EventBody", eventCard, 13, FontStyle.Normal, _theme.HudMutedTextColor, TextAnchor.UpperLeft);
        _eventBodyText.overflowMode = TextOverflowModes.Truncate;
        var eventActions = CreateLayoutPanel("EventActions", eventCard, Color.clear);
        ConfigureVerticalLayout(eventActions.gameObject, 0, 0, 0, 0, 6);
        for (var index = 0; index < 3; index++)
        {
            var capturedIndex = index;
            var button = CreateButton(
                $"EventChoice{capturedIndex}",
                eventActions,
                $"Scelta {capturedIndex + 1}",
                () => HandleEventChoicePressed(capturedIndex));
            var layoutElement = button.Button.GetComponent<LayoutElement>();
            if (layoutElement is not null)
            {
                layoutElement.preferredHeight = 54f;
                layoutElement.minWidth = 0f;
                layoutElement.preferredWidth = 0f;
            }

            button.Label.enableWordWrapping = true;
            button.Label.overflowMode = TextOverflowModes.Truncate;
            button.Label.fontSize = 12f;

            _eventChoiceButtons.Add(button);
            _eventChoiceIds.Add(string.Empty);
        }

        _networkText = CreateCard(parent, "Rete");
        _demandText = CreateCard(parent, "Domanda");
        _utilityText = CreateCard(parent, "Utility");

        var taxesCard = CreateLayoutPanel("TaxesCard", parent, _theme.HudCardColor);
        ConfigureVerticalLayout(taxesCard.gameObject, 10, 10, 10, 10, 4);
        taxesCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 150f;
        CreateText("TaxesHeader", taxesCard, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Tasse (sblocco M3)");
        _taxesText = CreateText("TaxesBody", taxesCard, 13, FontStyle.Normal, _theme.HudTextColor, TextAnchor.UpperLeft);
        var taxesActions = CreateLayoutPanel("TaxesActions", taxesCard, Color.clear);
        ConfigureHorizontalLayout(taxesActions.gameObject, 0, 0, 0, 0, 6);
        _resTaxDownButton = CreateButton("TaxResDown", taxesActions, "R-", () => _controller?.DecreaseTaxFromHud(ZoneType.Residential));
        _resTaxUpButton = CreateButton("TaxResUp", taxesActions, "R+", () => _controller?.IncreaseTaxFromHud(ZoneType.Residential));
        _comTaxDownButton = CreateButton("TaxComDown", taxesActions, "C-", () => _controller?.DecreaseTaxFromHud(ZoneType.Commercial));
        _comTaxUpButton = CreateButton("TaxComUp", taxesActions, "C+", () => _controller?.IncreaseTaxFromHud(ZoneType.Commercial));
        _indTaxDownButton = CreateButton("TaxIndDown", taxesActions, "I-", () => _controller?.DecreaseTaxFromHud(ZoneType.Industrial));
        _indTaxUpButton = CreateButton("TaxIndUp", taxesActions, "I+", () => _controller?.IncreaseTaxFromHud(ZoneType.Industrial));
        _offTaxDownButton = CreateButton("TaxOffDown", taxesActions, "O-", () => _controller?.DecreaseTaxFromHud(ZoneType.Office));
        _offTaxUpButton = CreateButton("TaxOffUp", taxesActions, "O+", () => _controller?.IncreaseTaxFromHud(ZoneType.Office));

        _trafficText = CreateCard(parent, "Traffico");

        var inspectorCard = CreateLayoutPanel("InspectorCard", parent, _theme!.HudCardColor);
        ConfigureVerticalLayout(inspectorCard.gameObject, 10, 10, 10, 10, 4);
        inspectorCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 126f;
        _inspectorTitleText = CreateText("InspectorTitle", inspectorCard, 13, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft);
        _inspectorBodyText = CreateText("InspectorBody", inspectorCard, 14, FontStyle.Normal, _theme.HudTextColor, TextAnchor.UpperLeft);

        var previewCard = CreateLayoutPanel("PreviewCard", parent, _theme.HudCardColor);
        ConfigureVerticalLayout(previewCard.gameObject, 10, 10, 10, 10, 4);
        previewCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 92f;
        CreateText("PreviewHeader", previewCard, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Anteprima Azione");
        _previewText = CreateText("PreviewBody", previewCard, 14, FontStyle.Normal, _theme.HudTextColor, TextAnchor.UpperLeft);

        var notificationsCard = CreateLayoutPanel("NotificationsCard", parent, _theme.HudCardColor);
        ConfigureVerticalLayout(notificationsCard.gameObject, 10, 10, 10, 10, 4);
        notificationsCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 140f;
        CreateText("NotificationsHeader", notificationsCard, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Notifiche");
        _notificationsText = CreateText("NotificationsBody", notificationsCard, 13, FontStyle.Normal, _theme.HudTextColor, TextAnchor.UpperLeft);
    }

    private void BuildBottomBar(RectTransform parent)
    {
        ConfigureHorizontalLayout(parent.gameObject, 14, 14, 12, 12, 14);

        var statePanel = CreateLayoutPanel("StatePanel", parent, _theme!.HudPanelSecondaryColor);
        ConfigureVerticalLayout(statePanel.gameObject, 10, 10, 10, 10, 3);
        statePanel.gameObject.AddComponent<LayoutElement>().preferredWidth = 240f;
        _toolText = CreateText("ToolState", statePanel, 14, FontStyle.Bold, _theme.HudTextColor, TextAnchor.MiddleLeft);
        _hoverText = CreateText("HoverState", statePanel, 13, FontStyle.Normal, _theme.HudMutedTextColor, TextAnchor.MiddleLeft);

        var statusPanel = CreateLayoutPanel("StatusPanel", parent, _theme.HudPanelSecondaryColor);
        ConfigureVerticalLayout(statusPanel.gameObject, 10, 10, 10, 10, 4);
        statusPanel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        CreateText("StatusHeader", statusPanel, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Ultima azione");
        _statusText = CreateText("StatusText", statusPanel, 15, FontStyle.Normal, _theme.HudTextColor, TextAnchor.MiddleLeft);

        var controlsPanel = CreateLayoutPanel("ControlsPanel", parent, _theme.HudPanelSecondaryColor);
        ConfigureVerticalLayout(controlsPanel.gameObject, 10, 10, 10, 10, 4);
        controlsPanel.gameObject.AddComponent<LayoutElement>().preferredWidth = 300f;
        CreateText("ControlsHeader", controlsPanel, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Controlli");
        _controlsText = CreateText(
            "ControlsText",
            controlsPanel,
            13,
            FontStyle.Normal,
            _theme.HudMutedTextColor,
            TextAnchor.UpperLeft);
    }

    private void BuildModalLayer(RectTransform parent)
    {
        _mainMenuPanel = CreatePanel("MainMenuPanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230f, -250f), new Vector2(230f, 250f), new Color(0.08f, 0.10f, 0.14f, 0.94f));
        ConfigureVerticalLayout(_mainMenuPanel.gameObject, 18, 18, 18, 18, 10);
        CreateText("MainTitle", _mainMenuPanel, 30, FontStyle.Bold, _theme!.HudTextColor, TextAnchor.MiddleCenter, "Pampa Skylines");
        CreateText("MainSubtitle", _mainMenuPanel, 14, FontStyle.Normal, _theme.HudMutedTextColor, TextAnchor.MiddleCenter, "Vertical slice evoluta");
        CreateButton("ContinueButton", _mainMenuPanel, "Continua", () => _controller?.ContinueGameFromHud());
        CreateButton("MainNewCityButton", _mainMenuPanel, "Nuova citta", () => _controller?.StartNewCityFromHud());
        CreateButton("MainSaveButton", _mainMenuPanel, "Salva ora", () => _controller?.SaveNowFromHud());
        CreateButton("MainLoadButton", _mainMenuPanel, "Carica citta", () => _controller?.ToggleLoadMenuFromHud());
        CreateButton("MainSettingsButton", _mainMenuPanel, "Impostazioni", () => _controller?.ToggleSettingsFromHud());
        CreateButton("MainHelpButton", _mainMenuPanel, "Aiuto", () => _controller?.ToggleHelpFromHud());

        _pausePanel = CreatePanel("PausePanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-170f, -180f), new Vector2(170f, 180f), new Color(0.08f, 0.10f, 0.14f, 0.94f));
        ConfigureVerticalLayout(_pausePanel.gameObject, 18, 18, 18, 18, 10);
        CreateText("PauseTitle", _pausePanel, 24, FontStyle.Bold, _theme.HudTextColor, TextAnchor.MiddleCenter, "Pausa");
        CreateButton("ResumeButton", _pausePanel, "Riprendi", () => _controller?.ContinueGameFromHud());
        CreateButton("PauseSaveButton", _pausePanel, "Salva", () => _controller?.SaveNowFromHud());
        CreateButton("PauseLoadButton", _pausePanel, "Carica", () => _controller?.ToggleLoadMenuFromHud());
        CreateButton("PauseSettingsButton", _pausePanel, "Impostazioni", () => _controller?.ToggleSettingsFromHud());
        CreateButton("PauseHelpButton", _pausePanel, "Aiuto", () => _controller?.ToggleHelpFromHud());

        _loadPanel = CreatePanel("LoadPanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420f, -280f), new Vector2(420f, 280f), new Color(0.08f, 0.10f, 0.14f, 0.96f));
        ConfigureVerticalLayout(_loadPanel.gameObject, 18, 18, 18, 18, 10);
        CreateText("LoadTitle", _loadPanel, 24, FontStyle.Bold, _theme.HudTextColor, TextAnchor.MiddleLeft, "Carica citta");
        _loadSummaryText = CreateText("LoadSummary", _loadPanel, 13, FontStyle.Normal, _theme.HudMutedTextColor, TextAnchor.UpperLeft);

        var loadColumns = CreateLayoutPanel("LoadColumns", _loadPanel, Color.clear);
        ConfigureHorizontalLayout(loadColumns.gameObject, 0, 0, 0, 0, 12);
        _loadCitiesList = CreateLayoutPanel("CitiesList", loadColumns, _theme.HudPanelSecondaryColor);
        ConfigureVerticalLayout(_loadCitiesList.gameObject, 10, 10, 10, 10, 8);
        _loadCitiesList.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        _loadBackupsList = CreateLayoutPanel("BackupsList", loadColumns, _theme.HudPanelSecondaryColor);
        ConfigureVerticalLayout(_loadBackupsList.gameObject, 10, 10, 10, 10, 8);
        _loadBackupsList.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var loadActions = CreateLayoutPanel("LoadActions", _loadPanel, Color.clear);
        ConfigureHorizontalLayout(loadActions.gameObject, 0, 0, 0, 0, 8);
        CreateButton("RefreshLoadButton", loadActions, "Aggiorna", () => _controller?.RefreshSaveSlotsFromHud());
        CreateButton("CloseLoadButton", loadActions, "Chiudi", () => _controller?.ToggleLoadMenuFromHud());

        _settingsPanel = CreatePanel("SettingsPanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-240f, -210f), new Vector2(240f, 210f), new Color(0.08f, 0.10f, 0.14f, 0.95f));
        ConfigureVerticalLayout(_settingsPanel.gameObject, 18, 18, 18, 18, 10);
        CreateText("SettingsTitle", _settingsPanel, 24, FontStyle.Bold, _theme.HudTextColor, TextAnchor.MiddleLeft, "Impostazioni");
        _settingsText = CreateText("SettingsSummary", _settingsPanel, 14, FontStyle.Normal, _theme.HudMutedTextColor, TextAnchor.UpperLeft);
        _edgeScrollButton = CreateButton("EdgeScrollButton", _settingsPanel, "Scorrimento bordi", () => _controller?.ToggleEdgeScrollFromHud());
        CreateButton("SettingsGridButton", _settingsPanel, "Griglia", () => _controller?.ToggleGridFromHud());
        CreateButton("SettingsInspectorButton", _settingsPanel, "Ispezione", () => _controller?.ToggleInspectorFromHud());
        CreateButton("SettingsNotificationsButton", _settingsPanel, "Notifiche", () => _controller?.ToggleNotificationsFromHud());
        CreateButton("CloseSettingsButton", _settingsPanel, "Chiudi", () => _controller?.ToggleSettingsFromHud());

        _runOutcomePanel = CreatePanel("RunOutcomePanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-320f, -230f), new Vector2(320f, 230f), new Color(0.08f, 0.10f, 0.14f, 0.97f));
        ConfigureVerticalLayout(_runOutcomePanel.gameObject, 20, 20, 20, 20, 10);
        _runOutcomeTitleText = CreateText("RunOutcomeTitle", _runOutcomePanel, 30, FontStyle.Bold, _theme.HudTextColor, TextAnchor.MiddleCenter, "Run completata");
        _runOutcomeBodyText = CreateText("RunOutcomeBody", _runOutcomePanel, 14, FontStyle.Normal, _theme.HudMutedTextColor, TextAnchor.UpperLeft, "Dettagli run");
        var outcomeActions = CreateLayoutPanel("RunOutcomeActions", _runOutcomePanel, Color.clear);
        ConfigureHorizontalLayout(outcomeActions.gameObject, 0, 0, 0, 0, 8);
        CreateButton("RunOutcomeNewCity", outcomeActions, "Nuova citta", () => _controller?.StartNewCityFromHud());
        CreateButton("RunOutcomeLoad", outcomeActions, "Carica", () => _controller?.ToggleLoadMenuFromHud());
        CreateButton("RunOutcomeClose", outcomeActions, "Chiudi", () => _controller?.CloseRunOutcomeFromHud());

        _helpPanel = CreatePanel("HelpPanel", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-440f, 18f), new Vector2(-18f, 280f), new Color(0.08f, 0.10f, 0.14f, 0.95f));
        ConfigureVerticalLayout(_helpPanel.gameObject, 16, 16, 16, 16, 8);
        CreateText("HelpTitle", _helpPanel, 20, FontStyle.Bold, _theme.HudTextColor, TextAnchor.MiddleLeft, "Aiuto");
        _helpText = CreateText("HelpBody", _helpPanel, 13, FontStyle.Normal, _theme.HudMutedTextColor, TextAnchor.UpperLeft);
    }

    private void ApplyButtonState()
    {
        if (_controller is null || _theme is null)
        {
            return;
        }

        foreach (var entry in _toolButtons)
        {
            var isActive = entry.Key == _controller.ActiveToolMode;
            var isUnlocked = _controller.IsToolUnlocked(entry.Key);
            entry.Value.Button.interactable = isUnlocked;
            entry.Value.Background.color = !isUnlocked
                ? new Color(_theme.HudButtonColor.r * 0.7f, _theme.HudButtonColor.g * 0.7f, _theme.HudButtonColor.b * 0.7f, _theme.HudButtonColor.a)
                : isActive
                    ? _theme.GetToolColor(entry.Key)
                    : _theme.HudButtonColor;
            entry.Value.Label.color = isUnlocked ? _theme.HudButtonTextColor : _theme.HudMutedTextColor;
            entry.Value.Label.text = isUnlocked
                ? entry.Key.ToDisplayName()
                : BuildLockedToolLabel(entry.Key);
        }

        foreach (var entry in _overlayButtons)
        {
            var isActive = _controller.OverlayState?.ActiveOverlay == entry.Key;
            entry.Value.Background.color = isActive ? _theme.HudButtonActiveColor : _theme.HudButtonColor;
            entry.Value.Label.color = _theme.HudButtonTextColor;
        }

        if (_pauseButton is not null)
        {
            _pauseButton.Label.text = _controller.IsPaused ? "Riprendi" : "Pausa";
            _pauseButton.Background.color = _controller.IsPaused ? _theme.HudWarningColor : _theme.HudButtonColor;
            _pauseButton.Label.color = _theme.HudButtonTextColor;
        }

        foreach (var entry in _speedButtons)
        {
            var isActive = !_controller.IsPaused && Mathf.Abs(entry.Key - _controller.CurrentTimeScale) < 0.01f;
            entry.Value.Background.color = isActive ? _theme.HudButtonActiveColor : _theme.HudButtonColor;
            entry.Value.Label.color = _theme.HudButtonTextColor;
        }

        if (_undoButton is not null)
        {
            ApplySecondaryState(_undoButton, _controller.CanUndo);
        }

        if (_redoButton is not null)
        {
            ApplySecondaryState(_redoButton, _controller.CanRedo);
        }

        if (_gridButton is not null)
        {
            ApplySecondaryState(_gridButton, _controller.OverlayState?.ShowGrid == true);
        }

        if (_inspectorButton is not null)
        {
            ApplySecondaryState(_inspectorButton, _controller.OverlayState?.ShowInspector == true);
        }

        if (_notificationsButton is not null)
        {
            ApplySecondaryState(_notificationsButton, _controller.OverlayState?.ShowNotifications == true);
        }

        if (_edgeScrollButton is not null)
        {
            ApplySecondaryState(_edgeScrollButton, _controller.EdgeScrollEnabled);
        }

        var budgetUnlocked = _controller.BudgetPolicyUnlocked;
        SetButtonInteractable(_resTaxDownButton, budgetUnlocked);
        SetButtonInteractable(_resTaxUpButton, budgetUnlocked);
        SetButtonInteractable(_comTaxDownButton, budgetUnlocked);
        SetButtonInteractable(_comTaxUpButton, budgetUnlocked);
        SetButtonInteractable(_indTaxDownButton, budgetUnlocked);
        SetButtonInteractable(_indTaxUpButton, budgetUnlocked);
        SetButtonInteractable(_offTaxDownButton, budgetUnlocked);
        SetButtonInteractable(_offTaxUpButton, budgetUnlocked);

        if (_loadButton is not null)
        {
            ApplySecondaryState(_loadButton, _controller.OverlayState?.ShowLoadMenu == true);
        }

        if (_helpButton is not null)
        {
            ApplySecondaryState(_helpButton, _controller.OverlayState?.ShowHelp == true);
        }
    }

    private void UpdateModalVisibility()
    {
        var overlayState = _controller?.OverlayState;
        if (overlayState is null)
        {
            return;
        }

        if (_mainMenuPanel is not null)
        {
            _mainMenuPanel.gameObject.SetActive(overlayState.ShowMainMenu);
        }

        if (_pausePanel is not null)
        {
            _pausePanel.gameObject.SetActive(overlayState.ShowPauseMenu);
        }

        if (_loadPanel is not null)
        {
            _loadPanel.gameObject.SetActive(overlayState.ShowLoadMenu);
        }

        if (_settingsPanel is not null)
        {
            _settingsPanel.gameObject.SetActive(overlayState.ShowSettings);
        }

        if (_helpPanel is not null)
        {
            _helpPanel.gameObject.SetActive(overlayState.ShowHelp);
        }

        if (_runOutcomePanel is not null)
        {
            _runOutcomePanel.gameObject.SetActive(overlayState.ShowRunOutcome);
        }
    }

    private void RefreshLoadLists()
    {
        if (_controller is null || _loadCitiesList is null || _loadBackupsList is null)
        {
            return;
        }

        var signature =
            $"{string.Join("|", _controller.SaveSlots.Select(slot => $"{slot.CityId}:{slot.CurrentVersion}:{slot.LastSavedAtUtc:O}"))}#" +
            $"{string.Join("|", _controller.BackupVersions)}#" +
            $"{_controller.CurrentState?.CityId}";

        if (signature == _loadSignature)
        {
            return;
        }

        _loadSignature = signature;
        ClearChildren(_loadCitiesList);
        ClearChildren(_loadBackupsList);

        CreateText("CitiesHeader", _loadCitiesList, 13, FontStyle.Bold, _theme!.HudTextColor, TextAnchor.MiddleLeft, "Citta");
        if (_controller.SaveSlots.Count == 0)
        {
            CreateText("CitiesEmpty", _loadCitiesList, 13, FontStyle.Normal, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Nessuna citta locale.");
        }
        else
        {
            foreach (var slot in _controller.SaveSlots)
            {
                var cityId = slot.CityId;
                CreateButton(
                    $"LoadCity_{cityId}",
                    _loadCitiesList,
                    $"{slot.DisplayName}  {slot.LastSavedAtUtc.ToLocalTime():g}",
                    () => _controller?.LoadCityFromHud(cityId));
            }
        }

        CreateText("BackupsHeader", _loadBackupsList, 13, FontStyle.Bold, _theme.HudTextColor, TextAnchor.MiddleLeft, "Backup citta corrente");
        if (_controller.BackupVersions.Count == 0 || string.IsNullOrWhiteSpace(_controller.CurrentState?.CityId))
        {
            CreateText("BackupsEmpty", _loadBackupsList, 13, FontStyle.Normal, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, "Nessun backup per la citta corrente.");
        }
        else
        {
            foreach (var version in _controller.BackupVersions.Take(8))
            {
                var currentCityId = _controller.CurrentState!.CityId;
                CreateButton(
                    $"LoadBackup_{version}",
                    _loadBackupsList,
                    version,
                    () => _controller?.LoadBackupFromHud(currentCityId, version));
            }
        }
    }

    private void HandleEventChoicePressed(int index)
    {
        if (_controller is null || index < 0 || index >= _eventChoiceIds.Count)
        {
            return;
        }

        var activeEvent = _controller.ActiveCityEvent;
        if (activeEvent is null)
        {
            return;
        }

        var choiceId = _eventChoiceIds[index];
        if (string.IsNullOrWhiteSpace(choiceId))
        {
            return;
        }

        _controller.ResolveEventChoiceFromHud(activeEvent.EventId, choiceId);
    }

    private void RefreshEventChoiceButtons(ActiveCityEventState? activeEvent)
    {
        if (_theme is null)
        {
            return;
        }

        for (var index = 0; index < _eventChoiceButtons.Count; index++)
        {
            var button = _eventChoiceButtons[index];
            var isVisible = activeEvent is not null && index < activeEvent.Choices.Count;
            button.Button.gameObject.SetActive(isVisible);
            if (!isVisible)
            {
                _eventChoiceIds[index] = string.Empty;
                continue;
            }

            var choice = activeEvent!.Choices[index];
            _eventChoiceIds[index] = choice.ChoiceId;
            button.Button.interactable = true;
            button.Background.color = _theme.HudButtonColor;
            button.Label.color = _theme.HudButtonTextColor;
            button.Label.alignment = TextAlignmentOptions.TopLeft;
            button.Label.text = $"{choice.Label}\n{choice.Description}";
        }
    }

    private string BuildLockedToolLabel(PcToolMode toolMode)
    {
        if (_controller is null)
        {
            return $"{toolMode.ToDisplayName()} [LOCK]";
        }

        var reason = _controller.ToolLockReason(toolMode);
        if (string.IsNullOrWhiteSpace(reason))
        {
            return $"{toolMode.ToDisplayName()} [LOCK]";
        }

        if (reason.Contains("Game over economico", StringComparison.OrdinalIgnoreCase))
        {
            return $"{toolMode.ToDisplayName()} [LOCK]\nGame over economico";
        }

        if (reason.Contains("Run demo completata", StringComparison.OrdinalIgnoreCase))
        {
            return $"{toolMode.ToDisplayName()} [LOCK]\nRun completata";
        }

        const string marker = "raggiungi";
        var markerIndex = reason.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return $"{toolMode.ToDisplayName()} [LOCK]\n{reason}";
        }

        var requirement = reason[(markerIndex + marker.Length)..].Trim();
        requirement = requirement.TrimEnd('.');
        return $"{toolMode.ToDisplayName()} [LOCK]\n{requirement}";
    }

    private void ApplySecondaryState(ButtonChrome button, bool active)
    {
        var theme = _theme!;
        button.Background.color = active ? theme.HudButtonActiveColor : theme.HudButtonColor;
        button.Label.color = theme.HudButtonTextColor;
    }

    private void SetButtonInteractable(ButtonChrome? button, bool interactable)
    {
        if (button is null)
        {
            return;
        }

        button.Button.interactable = interactable;
        button.Background.color = interactable
            ? _theme!.HudButtonColor
            : new Color(_theme!.HudButtonColor.r * 0.65f, _theme.HudButtonColor.g * 0.65f, _theme.HudButtonColor.b * 0.65f, _theme.HudButtonColor.a);
        button.Label.color = interactable ? _theme.HudButtonTextColor : _theme.HudMutedTextColor;
    }

    private TMP_Text CreateCard(RectTransform parent, string title)
    {
        var card = CreateLayoutPanel($"{title}Card", parent, _theme!.HudCardColor);
        ConfigureVerticalLayout(card.gameObject, 10, 10, 10, 10, 3);
        card.gameObject.AddComponent<LayoutElement>().preferredHeight = 84f;
        CreateText($"{title}Title", card, 12, FontStyle.Bold, _theme.HudMutedTextColor, TextAnchor.MiddleLeft, title);
        return CreateText($"{title}Body", card, 15, FontStyle.Normal, _theme.HudTextColor, TextAnchor.UpperLeft);
    }

    private ButtonChrome CreateButton(string name, RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var buttonRoot = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var layoutElement = buttonRoot.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 38f;
        layoutElement.minHeight = 36f;
        layoutElement.minWidth = 88f;
        layoutElement.preferredWidth = Mathf.Clamp(28f + (label.Length * 8.4f), 88f, 220f);

        var image = buttonRoot.gameObject.AddComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.type = Image.Type.Sliced;
        image.color = _theme!.HudButtonColor;

        var button = buttonRoot.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var labelText = CreateText($"{name}Label", buttonRoot, 14, FontStyle.Bold, _theme.HudButtonTextColor, TextAnchor.MiddleCenter, label);
        labelText.enableWordWrapping = false;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        labelText.raycastTarget = false;
        return new ButtonChrome
        {
            Button = button,
            Background = image,
            Label = labelText
        };
    }

    private RectTransform CreateLayoutPanel(string name, RectTransform parent, Color color)
    {
        var panel = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        if (color.a > 0f)
        {
            var image = panel.gameObject.AddComponent<Image>();
            image.sprite = GetWhiteSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        return panel;
    }

    private RectTransform CreatePanel(
        string name,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        Color color)
    {
        var panel = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        var image = panel.gameObject.AddComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        return panel;
    }

    private RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        var rectObject = new GameObject(name, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        var rect = rectObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    private TMP_Text CreateText(
        string name,
        RectTransform parent,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment,
        string text = "")
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.font = GetTmpFont();
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = ToTmpFontStyle(fontStyle);
        textComponent.color = color;
        textComponent.alignment = ToTmpAlignment(alignment);
        textComponent.enableWordWrapping = true;
        textComponent.overflowMode = TextOverflowModes.Overflow;
        textComponent.raycastTarget = false;
        textComponent.text = text;
        return textComponent;
    }

    private void ConfigureVerticalLayout(GameObject gameObject, int left, int right, int top, int bottom, int spacing)
    {
        var layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(left, right, top, bottom);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private void ConfigureHorizontalLayout(GameObject gameObject, int left, int right, int top, int bottom, int spacing)
    {
        var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(left, right, top, bottom);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
    }

    private TMP_FontAsset GetTmpFont()
    {
        if (_tmpFont is not null)
        {
            return _tmpFont;
        }

        _tmpFont = TMP_Settings.defaultFontAsset;
        if (_tmpFont is null)
        {
            _tmpFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        if (_tmpFont is null)
        {
            throw new MissingReferenceException("TextMeshPro default font asset is missing.");
        }

        return _tmpFont;
    }

    private static FontStyles ToTmpFontStyle(FontStyle fontStyle)
    {
        return fontStyle switch
        {
            FontStyle.Bold => FontStyles.Bold,
            FontStyle.Italic => FontStyles.Italic,
            FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
            _ => FontStyles.Normal
        };
    }

    private static TextAlignmentOptions ToTmpAlignment(TextAnchor alignment)
    {
        return alignment switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Midline,
            TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.TopLeft
        };
    }

    private Sprite GetWhiteSprite()
    {
        if (_whiteSprite is not null)
        {
            return _whiteSprite;
        }

        _whiteTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        _whiteTexture.SetPixel(0, 0, Color.white);
        _whiteTexture.Apply();
        _whiteTexture.hideFlags = HideFlags.HideAndDontSave;
        _whiteSprite = Sprite.Create(_whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return _whiteSprite;
    }

    private void ClearChildren(RectTransform parent)
    {
        var children = new List<GameObject>();
        foreach (Transform child in parent)
        {
            children.Add(child.gameObject);
        }

        foreach (var child in children)
        {
            Destroy(child);
        }
    }

    private void OnDestroy()
    {
        if (_whiteSprite is not null)
        {
            Destroy(_whiteSprite);
        }

        if (_whiteTexture is not null)
        {
            Destroy(_whiteTexture);
        }
    }

    private static string FormatCell(Int2? cell)
    {
        return cell.HasValue ? cell.Value.ToString() : "-";
    }
}
}

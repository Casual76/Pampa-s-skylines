namespace PampaSkylines.PC
{
public sealed class PcOverlayState
{
    public PcOverlayKind ActiveOverlay { get; private set; }

    public bool ShowGrid { get; private set; } = true;

    public bool ShowInspector { get; private set; } = true;

    public bool ShowNotifications { get; private set; } = true;

    public bool ShowHelp { get; private set; }

    public bool ShowMainMenu { get; private set; } = true;

    public bool ShowLoadMenu { get; private set; }

    public bool ShowSettings { get; private set; }

    public bool ShowPauseMenu { get; private set; }

    public bool ShowRunOutcome { get; private set; }

    public bool EdgeScrollEnabled { get; private set; } = true;

    public void SetOverlay(PcOverlayKind overlay)
    {
        ActiveOverlay = overlay;
    }

    public void ToggleOverlay(PcOverlayKind overlay)
    {
        ActiveOverlay = ActiveOverlay == overlay ? PcOverlayKind.None : overlay;
    }

    public void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
    }

    public void ToggleInspector()
    {
        ShowInspector = !ShowInspector;
    }

    public void ToggleNotifications()
    {
        ShowNotifications = !ShowNotifications;
    }

    public void ToggleHelp()
    {
        ShowHelp = !ShowHelp;
    }

    public void OpenMainMenu()
    {
        ShowMainMenu = true;
        ShowPauseMenu = false;
        ShowLoadMenu = false;
        ShowSettings = false;
    }

    public void CloseMainMenu()
    {
        ShowMainMenu = false;
    }

    public void CloseAllMenus()
    {
        ShowMainMenu = false;
        ShowLoadMenu = false;
        ShowSettings = false;
        ShowPauseMenu = false;
        ShowHelp = false;
        ShowRunOutcome = false;
    }

    public void ToggleLoadMenu()
    {
        ShowLoadMenu = !ShowLoadMenu;
        if (ShowLoadMenu)
        {
            ShowMainMenu = false;
            ShowSettings = false;
            ShowPauseMenu = false;
        }
    }

    public void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
        if (ShowSettings)
        {
            ShowMainMenu = false;
            ShowLoadMenu = false;
            ShowPauseMenu = false;
        }
    }

    public void TogglePauseMenu()
    {
        ShowPauseMenu = !ShowPauseMenu;
        if (ShowPauseMenu)
        {
            ShowMainMenu = false;
            ShowLoadMenu = false;
            ShowSettings = false;
        }
    }

    public void CloseTransientPanels()
    {
        ShowLoadMenu = false;
        ShowSettings = false;
        ShowPauseMenu = false;
    }

    public void OpenRunOutcome()
    {
        ShowRunOutcome = true;
        ShowMainMenu = false;
        ShowLoadMenu = false;
        ShowSettings = false;
        ShowPauseMenu = false;
        ShowHelp = false;
    }

    public void CloseRunOutcome()
    {
        ShowRunOutcome = false;
    }

    public void SetEdgeScrollEnabled(bool enabled)
    {
        EdgeScrollEnabled = enabled;
    }

    public bool IsAnyModalOpen =>
        ShowMainMenu ||
        ShowLoadMenu ||
        ShowSettings ||
        ShowPauseMenu ||
        ShowRunOutcome;
}
}

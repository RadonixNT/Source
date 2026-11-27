using NT.Localization;

namespace RouterXYZACTC;

/// <summary>Operator interface with an independent designer-owned layout.</summary>
public partial class MainForm : NT.Tools.sForm
{
    private readonly NT.Tools.sLocalization localization;

    public MainForm()
    {
        InitializeComponent();
        localization = new NT.Tools.sLocalization(components);
        BindLocalizedTexts();
        if (System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
            return;

        InitializeThemeButton();
        R_Minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        R_Exit.Click += (_, _) => { if (!IsProgramLoading) Close(); };
        R_ThemeToggle.Click += (_, _) =>
        {
            if (R_ThemeManager.IsTransitioning || !CanExecuteWindowCommand())
                return;

            NT.Tools.NTControlTheme next = RuntimeTheme == NT.Tools.NTControlTheme.Dark
                ? NT.Tools.NTControlTheme.Light
                : NT.Tools.NTControlTheme.Dark;
            if (runtimeInitialized && !runtimeDisposed)
                generalLinks.ThemeLink.Value = next.ToString();
            else
                RuntimeTheme = next;
        };
        Disposed += (_, _) => DisposeRuntime();
        NTLocalization.LanguageChanged += ApplicationLanguageChanged;
        NTLocalization.TextsChanged += ApplicationTextsChanged;
        Disposed += (_, _) => NTLocalization.LanguageChanged -= ApplicationLanguageChanged;
        Disposed += (_, _) => NTLocalization.TextsChanged -= ApplicationTextsChanged;
        CompleteFormInitialization();
    }

    private static string T(NTTextId textId) => NTLocalization.Get(textId);

    private void BindLocalizedTexts()
    {
        localization.Bind(R_XAxis, RouterXYZACTCTexts.XAxis);
        localization.Bind(R_YAxis, RouterXYZACTCTexts.YAxis);
        localization.Bind(R_ZAxis, RouterXYZACTCTexts.ZAxis);
        localization.Bind(R_AAxis, RouterXYZACTCTexts.AAxis);
        localization.Bind(R_CAxis, RouterXYZACTCTexts.CAxis);
        localization.Bind(R_AxisAlarm, RouterXYZACTCTexts.Alarm);
        localization.Bind(R_Emergency, RouterXYZACTCTexts.Emergency);
        localization.Bind(R_Homed, RouterXYZACTCTexts.Homed);
        localization.Bind(R_Configuration, RouterXYZACTCTexts.Configuration);
        localization.Bind(R_MachinePositionTitle, RouterXYZACTCTexts.MachinePosition);
        localization.Bind(R_WorkPositionTitle, RouterXYZACTCTexts.WorkPosition);
        localization.Bind(R_JogPositionHeader, RouterXYZACTCTexts.PositionMm);
        localization.Bind(R_JogStepHeader, RouterXYZACTCTexts.StepMm);
        localization.Bind(R_JogVelocityHeader, RouterXYZACTCTexts.VelocityMmPerSecond);
        localization.Bind(R_MachineFeedValue, RouterXYZACTCTexts.MachineFeedPlaceholder);
        localization.Bind(R_JogPause, RouterXYZACTCTexts.Pause);
        localization.Bind(R_Home, RouterXYZACTCTexts.Home);
        localization.Bind(R_GoHome, RouterXYZACTCTexts.GoHome);
        localization.Bind(R_GoToReference, RouterXYZACTCTexts.GoReference);
        localization.Bind(R_ReferencePause, RouterXYZACTCTexts.Pause);
        localization.Bind(R_RefXZero, RouterXYZACTCTexts.SetXZero);
        localization.Bind(R_RefYZero, RouterXYZACTCTexts.SetYZero);
        localization.Bind(R_RefZZero, RouterXYZACTCTexts.SetZZero);
        localization.Bind(R_ToolSelectorLabel, RouterXYZACTCTexts.Tool);
        localization.Bind(R_Spindle, RouterXYZACTCTexts.Spindle);
        localization.Bind(R_MeasureToolOffset, RouterXYZACTCTexts.SetTool);
        localization.Bind(R_EditGCode, RouterXYZACTCTexts.EditGCode);
        localization.Bind(R_SelectStartLine, RouterXYZACTCTexts.StartLine);
        localization.Bind(R_SpindlePanelTitle, RouterXYZACTCTexts.SpindleProperties);
        localization.Bind(R_ReferencePanelTitle, RouterXYZACTCTexts.ReferenceMm);
        ApplyLocalizedChrome();
    }

    private void ApplicationLanguageChanged(object? sender, NTLanguageChangedEventArgs e)
        => RefreshApplicationTexts();

    private void ApplicationTextsChanged(object? sender, EventArgs e) => RefreshApplicationTexts();

    private void RefreshApplicationTexts()
    {
        displayedMachineFeed = double.NaN;
        ApplyLocalizedChrome();
        if (!runtimeInitialized || runtimeDisposed) return;
        generalLinks.RefreshLocalizedConfiguration();
        linkLocalization?.Refresh();
        RefreshDiagnosticMetadata();
        openFileCaption = T(RouterXYZACTCTexts.OpenFile);
        RefreshProgramLoadButton();
        RequestRuntimeStateRefresh();
    }

    private void ApplyLocalizedChrome()
    {
        RefreshConnectionIndicator();
        R_RunProgram.Text = T(RouterXYZACTCTexts.Run);
        R_PauseProgram.Text = T(RouterXYZACTCTexts.Pause);
        R_ResetProgram.Text = T(RouterXYZACTCTexts.Stop);
        if (!runtimeInitialized) R_JogMode.Text = T(RouterXYZACTCTexts.StepJog);
        if (!runtimeInitialized) R_OpenFile.Text = T(RouterXYZACTCTexts.OpenFile);
        if (!runtimeInitialized) R_ProgramPath.Text = T(RouterXYZACTCTexts.NoProgramLoaded);
        if (!runtimeInitialized) R_ProgramProgress.Text = T(RouterXYZACTCTexts.DesignPreview);
    }

    private void InitializeThemeButton()
    {
        void RefreshThemeButton()
        {
            R_ThemeToggle.DarkThemeText = T(RouterXYZACTCTexts.SwitchToDarkTheme);
            R_ThemeToggle.LightThemeText = T(RouterXYZACTCTexts.SwitchToLightTheme);
        }
        NTLocalization.LanguageChanged += RefreshThemeButtonLanguage;
        NTLocalization.TextsChanged += RefreshThemeButtonTexts;
        RefreshThemeButton();
        Disposed += (_, _) => NTLocalization.LanguageChanged -= RefreshThemeButtonLanguage;
        Disposed += (_, _) => NTLocalization.TextsChanged -= RefreshThemeButtonTexts;
        void RefreshThemeButtonLanguage(object? sender, NTLanguageChangedEventArgs e) => RefreshThemeButton();
        void RefreshThemeButtonTexts(object? sender, EventArgs e) => RefreshThemeButton();
    }
    private void SetCurrentAxisReference(int displayAxisIndex)
    {
        if (!runtimeInitialized || runtimeDisposed || !controller.IsConnected ||
            !CaptureCommandAvailability().CanSetReference ||
            displayAxisIndex < 0 || displayAxisIndex >= positionAxes.Length)
            return;

        int slot = positionSlots[displayAxisIndex];
        double value = positionAxes[displayAxisIndex].CommandPosition -
            mainToolPath.Offset[slot] - mainToolPath.LiveOffset[slot];
        referenceLinks.SetValue(referenceLinks.SelectedIndex, slot, value);
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        if (DesignMode || System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
            return;
        InitializeRuntime();
        WindowState = FormWindowState.Maximized;
        generalLinks.AccessSession.Start(this, AttachDialogTheme);
        if (connectOnStartup && generalLinks.AccessSession.CanOperate)
            ConnectController();
    }
}

using NT.Localization;

namespace RouterXYZBCTC;

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
        localization.Bind(R_XAxis, RouterXYZBCTCTexts.XAxis);
        localization.Bind(R_YAxis, RouterXYZBCTCTexts.YAxis);
        localization.Bind(R_ZAxis, RouterXYZBCTCTexts.ZAxis);
        localization.Bind(R_BAxis, RouterXYZBCTCTexts.BBxis);
        localization.Bind(R_CAxis, RouterXYZBCTCTexts.CAxis);
        localization.Bind(R_AxisAlarm, RouterXYZBCTCTexts.Alarm);
        localization.Bind(R_Emergency, RouterXYZBCTCTexts.Emergency);
        localization.Bind(R_Homed, RouterXYZBCTCTexts.Homed);
        localization.Bind(R_Configuration, RouterXYZBCTCTexts.Configuration);
        localization.Bind(R_MachinePositionTitle, RouterXYZBCTCTexts.MachinePosition);
        localization.Bind(R_WorkPositionTitle, RouterXYZBCTCTexts.WorkPosition);
        localization.Bind(R_JogPositionHeader, RouterXYZBCTCTexts.PositionMm);
        localization.Bind(R_JogStepHeader, RouterXYZBCTCTexts.StepMm);
        localization.Bind(R_JogVelocityHeader, RouterXYZBCTCTexts.VelocityMmPerSecond);
        localization.Bind(R_MachineFeedValue, RouterXYZBCTCTexts.MachineFeedPlaceholder);
        localization.Bind(R_JogPause, RouterXYZBCTCTexts.Pause);
        localization.Bind(R_Home, RouterXYZBCTCTexts.Home);
        localization.Bind(R_GoHome, RouterXYZBCTCTexts.GoHome);
        localization.Bind(R_GoToReference, RouterXYZBCTCTexts.GoReference);
        localization.Bind(R_ReferencePause, RouterXYZBCTCTexts.Pause);
        localization.Bind(R_RefXZero, RouterXYZBCTCTexts.SetXZero);
        localization.Bind(R_RefYZero, RouterXYZBCTCTexts.SetYZero);
        localization.Bind(R_RefZZero, RouterXYZBCTCTexts.SetZZero);
        localization.Bind(R_ToolSelectorLabel, RouterXYZBCTCTexts.Tool);
        localization.Bind(R_Spindle, RouterXYZBCTCTexts.Spindle);
        localization.Bind(R_MeasureToolOffset, RouterXYZBCTCTexts.SetTool);
        localization.Bind(R_EditGCode, RouterXYZBCTCTexts.EditGCode);
        localization.Bind(R_SelectStartLine, RouterXYZBCTCTexts.StartLine);
        localization.Bind(R_SpindlePanelTitle, RouterXYZBCTCTexts.SpindleProperties);
        localization.Bind(R_ReferencePanelTitle, RouterXYZBCTCTexts.ReferenceMm);
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
        openFileCaption = T(RouterXYZBCTCTexts.OpenFile);
        RefreshProgramLoadButton();
        RequestRuntimeStateRefresh();
    }

    private void ApplyLocalizedChrome()
    {
        RefreshConnectionIndicator();
        R_RunProgram.Text = T(RouterXYZBCTCTexts.Run);
        R_PauseProgram.Text = T(RouterXYZBCTCTexts.Pause);
        R_ResetProgram.Text = T(RouterXYZBCTCTexts.Stop);
        if (!runtimeInitialized) R_JogMode.Text = T(RouterXYZBCTCTexts.StepJog);
        if (!runtimeInitialized) R_OpenFile.Text = T(RouterXYZBCTCTexts.OpenFile);
        if (!runtimeInitialized) R_ProgramPath.Text = T(RouterXYZBCTCTexts.NoProgramLoaded);
        if (!runtimeInitialized) R_ProgramProgress.Text = T(RouterXYZBCTCTexts.DesignPreview);
    }

    private void InitializeThemeButton()
    {
        void RefreshThemeButton()
        {
            R_ThemeToggle.DarkThemeText = T(RouterXYZBCTCTexts.SwitchToDarkTheme);
            R_ThemeToggle.LightThemeText = T(RouterXYZBCTCTexts.SwitchToLightTheme);
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

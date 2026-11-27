using NT.Localization;

namespace CutterXYZ;

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
        localization.Bind(R_XAxis, RouterXYZTCTexts.XAxis);
        localization.Bind(R_YAxis, RouterXYZTCTexts.YAxis);
        localization.Bind(R_ZAxis, RouterXYZTCTexts.ZAxis);
        localization.Bind(R_AxisAlarm, RouterXYZTCTexts.Alarm);
        localization.Bind(R_Emergency, RouterXYZTCTexts.Emergency);
        localization.Bind(R_Homed, RouterXYZTCTexts.Homed);
        localization.Bind(R_Configuration, RouterXYZTCTexts.Configuration);
        localization.Bind(R_MachinePositionTitle, RouterXYZTCTexts.MachinePosition);
        localization.Bind(R_WorkPositionTitle, RouterXYZTCTexts.WorkPosition);
        localization.Bind(R_JogPositionHeader, RouterXYZTCTexts.PositionMm);
        localization.Bind(R_JogStepHeader, RouterXYZTCTexts.StepMm);
        localization.Bind(R_JogVelocityHeader, RouterXYZTCTexts.VelocityMmPerSecond);
        localization.Bind(R_MachineFeedValue, RouterXYZTCTexts.MachineFeedPlaceholder);
        localization.Bind(R_JogPause, RouterXYZTCTexts.Pause);
        localization.Bind(R_Home, RouterXYZTCTexts.Home);
        localization.Bind(R_GoHome, RouterXYZTCTexts.GoHome);
        localization.Bind(R_GoToReference, RouterXYZTCTexts.GoReference);
        localization.Bind(R_ReferencePause, RouterXYZTCTexts.Pause);
        localization.Bind(R_RefXZero, RouterXYZTCTexts.SetXZero);
        localization.Bind(R_RefYZero, RouterXYZTCTexts.SetYZero);
        localization.Bind(R_RefZZero, RouterXYZTCTexts.SetZZero);
        localization.Bind(R_EditGCode, RouterXYZTCTexts.EditGCode);
        localization.Bind(R_SelectStartLine, RouterXYZTCTexts.StartLine);
        localization.Bind(R_ReferencePanelTitle, RouterXYZTCTexts.ReferenceMm);
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
        openFileCaption = T(RouterXYZTCTexts.OpenFile);
        RefreshProgramLoadButton();
        RequestRuntimeStateRefresh();
    }

    private void ApplyLocalizedChrome()
    {
        RefreshConnectionIndicator();
        R_RunProgram.Text = T(RouterXYZTCTexts.Run);
        R_PauseProgram.Text = T(RouterXYZTCTexts.Pause);
        R_ResetProgram.Text = T(RouterXYZTCTexts.Stop);
        if (!runtimeInitialized) R_JogMode.Text = T(RouterXYZTCTexts.StepJog);
        if (!runtimeInitialized) R_OpenFile.Text = T(RouterXYZTCTexts.OpenFile);
        if (!runtimeInitialized) R_ProgramPath.Text = T(RouterXYZTCTexts.NoProgramLoaded);
        if (!runtimeInitialized) R_ProgramProgress.Text = T(RouterXYZTCTexts.DesignPreview);
    }

    private void InitializeThemeButton()
    {
        void RefreshThemeButton()
        {
            R_ThemeToggle.DarkThemeText = T(RouterXYZTCTexts.SwitchToDarkTheme);
            R_ThemeToggle.LightThemeText = T(RouterXYZTCTexts.SwitchToLightTheme);
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

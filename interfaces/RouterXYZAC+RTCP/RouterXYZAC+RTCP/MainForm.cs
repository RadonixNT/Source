using NT.Localization;

using NT.Controller;
using OpenTK.Mathematics;

namespace RouterXYZACRTCP;

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
        localization.Bind(R_XAxis, RouterXYZACRTCPTexts.XAxis);
        localization.Bind(R_YAxis, RouterXYZACRTCPTexts.YAxis);
        localization.Bind(R_ZAxis, RouterXYZACRTCPTexts.ZAxis);
        localization.Bind(R_AAxis, RouterXYZACRTCPTexts.AAxis);
        localization.Bind(R_CAxis, RouterXYZACRTCPTexts.CAxis);
        localization.Bind(R_AxisAlarm, RouterXYZACRTCPTexts.Alarm);
        localization.Bind(R_Emergency, RouterXYZACRTCPTexts.Emergency);
        localization.Bind(R_Homed, RouterXYZACRTCPTexts.Homed);
        localization.Bind(R_Configuration, RouterXYZACRTCPTexts.Configuration);
        localization.Bind(R_MachinePositionTitle, RouterXYZACRTCPTexts.MachinePosition);
        localization.Bind(R_WorkPositionTitle, RouterXYZACRTCPTexts.WorkPosition);
        localization.Bind(R_JogPositionHeader, RouterXYZACRTCPTexts.PositionMm);
        localization.Bind(R_JogStepHeader, RouterXYZACRTCPTexts.StepMm);
        localization.Bind(R_JogVelocityHeader, RouterXYZACRTCPTexts.VelocityMmPerSecond);
        localization.Bind(R_MachineFeedValue, RouterXYZACRTCPTexts.MachineFeedPlaceholder);
        localization.Bind(R_JogPause, RouterXYZACRTCPTexts.Pause);
        localization.Bind(R_Home, RouterXYZACRTCPTexts.Home);
        localization.Bind(R_GoHome, RouterXYZACRTCPTexts.GoHome);
        localization.Bind(R_GoToReference, RouterXYZACRTCPTexts.GoReference);
        localization.Bind(R_ReferencePause, RouterXYZACRTCPTexts.Pause);
        localization.Bind(R_RefXZero, RouterXYZACRTCPTexts.SetXZero);
        localization.Bind(R_RefYZero, RouterXYZACRTCPTexts.SetYZero);
        localization.Bind(R_RefZZero, RouterXYZACRTCPTexts.SetZZero);
        localization.Bind(R_Spindle, RouterXYZACRTCPTexts.Spindle);
        localization.Bind(R_EditGCode, RouterXYZACRTCPTexts.EditGCode);
        localization.Bind(R_SelectStartLine, RouterXYZACRTCPTexts.StartLine);
        localization.Bind(R_SpindlePageTitle, RouterXYZACRTCPTexts.SpindleProperties);
        localization.Bind(R_RtcpPageTitle, RouterXYZACRTCPTexts.RtcpPart);
        localization.Bind(R_ReferencePanelTitle, RouterXYZACRTCPTexts.ReferenceMm);
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
        openFileCaption = T(RouterXYZACRTCPTexts.OpenFile);
        RefreshProgramLoadButton();
        RequestRuntimeStateRefresh();
    }

    private void ApplyLocalizedChrome()
    {
        RefreshConnectionIndicator();
        UpdateToolLengthCaption();
        R_GotoSpindlePage.AccessibleName = T(RouterXYZACRTCPTexts.SpindleProperties);
        R_GotoRtcpPage.AccessibleName = T(RouterXYZACRTCPTexts.RtcpPart);
        R_GotoNavigatorGCodePage.AccessibleName = T(RouterXYZACRTCPTexts.GCodeTitle);
        R_GotoNavigatorProgramPage.AccessibleName = T(RouterXYZACRTCPTexts.ProgramTitle);
        R_RunProgram.Text = T(RouterXYZACRTCPTexts.Run);
        R_PauseProgram.Text = T(RouterXYZACRTCPTexts.Pause);
        R_ResetProgram.Text = T(RouterXYZACRTCPTexts.Stop);
        if (!runtimeInitialized) R_JogMode.Text = T(RouterXYZACRTCPTexts.StepJog);
        if (!runtimeInitialized) R_OpenFile.Text = T(RouterXYZACRTCPTexts.OpenFile);
        if (!runtimeInitialized) R_ExpandedProgramPath.Text = R_ProgramPath.Text = T(RouterXYZACRTCPTexts.NoProgramLoaded);
        if (!runtimeInitialized) R_ProgramProgress.Text = T(RouterXYZACRTCPTexts.DesignPreview);
    }

    private void InitializeThemeButton()
    {
        void RefreshThemeButton()
        {
            R_ThemeToggle.DarkThemeText = T(RouterXYZACRTCPTexts.SwitchToDarkTheme);
            R_ThemeToggle.LightThemeText = T(RouterXYZACRTCPTexts.SwitchToLightTheme);
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
            (uint)displayAxisIndex >= 3)
            return;

        var rtcp = controller.MotionController.RTCP;
        if (rtcp.SwitchStatus == RtcpSwitchStatus.Pending) return;
        int slot = positionSlots[displayAxisIndex];
        double coordinate;
        if (rtcp.Mode == RtcpMode.RTCP)
        {
            if (rtcp.Geometry is not { } geometry) return;
            double a = positionAxes[3].CommandPosition;
            double c = positionAxes[4].CommandPosition;
            var (first, second) = geometry.FirstRotary.AxisIndex == positionAxes[3].Index ? (a, c) : (c, a);
            var carriage = new Vector3d(positionAxes[0].CommandPosition, positionAxes[1].CommandPosition,
                positionAxes[2].CommandPosition);
            if (!WorkCoordinatePresentation.TryRtcp(rtcp, geometry, default, first, second, out var frame))
                return;
            coordinate = frame.GetWorkPosition(carriage)[displayAxisIndex];
        }
        else
        {
            coordinate = positionAxes[displayAxisIndex].CommandPosition;
        }
        double value = coordinate - mainToolPath.Offset[slot] - mainToolPath.LiveOffset[slot];
        if (!double.IsFinite(value)) return;
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

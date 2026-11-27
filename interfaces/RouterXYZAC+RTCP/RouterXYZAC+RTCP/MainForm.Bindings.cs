using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;
using NT.Tools;

namespace RouterXYZACRTCP;

/// <summary>
/// Connects the designer-owned XYZAC controls to the existing command and value Links.
/// Keyboard and buttons share command admission; page visibility requests a UI refresh.
/// Runtime-only Links belong to runtimeLinks and are disposed with the form.
/// </summary>
public partial class MainForm
{
    private RealLink velocityFactorLink = null!;
    private LogicLink spindleLink = null!;
    private RealLink spindleSpeedLink = null!;
    private CommandLink pauseLink = null!;

    private void BindRuntimeControls()
    {
        controllerLinks.RunLink.Bind(R_RunProgram);
        controllerLinks.ResetLink.Bind(R_ResetProgram);
        controllerLinks.HomeLink.Bind(R_Home);
        controllerLinks.GoHomeLink.Bind(R_GoHome);
        controllerLinks.GoToReferenceLink.Bind(R_GoToReference);
        BindSetReferenceCommand("SetReferenceX", 0, R_RefXZero);
        BindSetReferenceCommand("SetReferenceY", 1, R_RefYZero);
        BindSetReferenceCommand("SetReferenceZ", 2, R_RefZZero);
        generalLinks.OpenFileLink.Bind(R_OpenFile);
        generalLinks.OpenConfigurationLink.Bind(R_Configuration);
        generalLinks.SelectStartLineLink.Bind(R_SelectStartLine);
        generalLinks.EditGCodeLink.Bind(R_EditGCode);
        generalLinks.OpenReportLink.BindDoubleClick(R_ProgramProgress);

        pauseLink.Bind(R_PauseProgram);
        pauseLink.Bind(R_JogPause);
        pauseLink.Bind(R_ReferencePause);

        BindJog("X", R_JogNX, R_JogPX);
        BindJog("Y", R_JogNY, R_JogPY);
        BindJog("Z", R_JogNZ, R_JogPZ);
        BindJog("A", R_JogNA, R_JogPA);
        BindJog("C", R_JogNC, R_JogPC);
        // Page selection belongs to this form session, never to persisted settings.
        IntegerLink jogPageLink = Link.CreateInteger("JogPage", "Jog Page", 0, 0, 2);
        runtimeLinks.Add(jogPageLink);
        jogPageLink.Bind(R_JogPagePanel);
        // Hidden editors skip Link pushes. Refresh after their page becomes visible,
        // even when an idle controller has no status change to trigger a refresh.
        R_JogPagePanel.PageChanged += (_, _) => { StopRtcpJog(); RequestRuntimeUiRefresh(); };
        R_GotoJogProperties.Click += (_, _) => jogPageLink.Value = 1;
        R_GotoJogPositions.Click += (_, _) => jogPageLink.Value = 2;
        R_GotoJog.Click += (_, _) => jogPageLink.Value = 0;
        controllerLinks.XYJogCommands.NegativeXPositiveY.Bind(R_JogNXPY);
        controllerLinks.XYJogCommands.PositiveXPositiveY.Bind(R_JogPXPY);
        controllerLinks.XYJogCommands.NegativeXNegativeY.Bind(R_JogNXNY);
        controllerLinks.XYJogCommands.PositiveXNegativeY.Bind(R_JogPXNY);
        generalLinks.JogModeLink.Bind(R_JogMode);
        BindJogParameters("X", R_JogVelocityX, R_StepX, R_PositionX);
        BindJogParameters("Y", R_JogVelocityY, R_StepY, R_PositionY);
        BindJogParameters("Z", R_JogVelocityZ, R_StepZ, R_PositionZ);
        BindJogParameters("A", R_JogVelocityA, R_StepA, R_PositionA);
        BindJogParameters("C", R_JogVelocityC, R_StepC, R_PositionC);
        controllerLinks.AxisJogCommands.Single(c => c.AxisName == "A").GoToPosition.Bind(R_GoToA);
        controllerLinks.AxisJogCommands.Single(c => c.AxisName == "C").GoToPosition.Bind(R_GoToC);
        controllerLinks.AxisJogCommands.Single(c => c.AxisName == "X").GoToPosition.Bind(R_GoToX);
        controllerLinks.AxisJogCommands.Single(c => c.AxisName == "Y").GoToPosition.Bind(R_GoToY);
        controllerLinks.AxisJogCommands.Single(c => c.AxisName == "Z").GoToPosition.Bind(R_GoToZ);

        BindRtcpControls();
        BindHeadPages();
        BindNavigatorPages();

        velocityFactorLink = CreateFeedOverrideLink();
        runtimeLinks.Add(velocityFactorLink);
        velocityFactorLink.Bind(R_FeedOverride, options => options.SliderStep = R_FeedOverride.Step);
        generalLinks.JogFeedPercentLink.Bind(R_JogFeedOverride, options => options.SliderStep = R_JogFeedOverride.Step);

        spindleLink = controllerLinks.CreateSpindleControlLink();
        spindleSpeedLink = routerLinks.CurrentSpindleSpeed;
        controllerLinks.BindSpindleSpeed(spindleSpeedLink, routerLinks.SpindleSpeedStep);
        runtimeLinks.Add(spindleLink);
        spindleLink.Bind(R_Spindle);
        // The spindle source steps manual input. Keep the display continuous
        // so a PLC speed can be shown even when it falls between manual steps.
        spindleSpeedLink.Bind(R_SpindleSpeed, options =>
        {
            options.SliderStep = 0;
            options.IgnorePushWhileFocused = false;
        });
        R_SpindleSpeed.Scroll += (_, _) => R_SpindleSpeed.Value = spindleSpeedLink.Value;
        routerLinks.SpindleSpeedStep.Changed += SpindleSpeedStepChanged;
        SpindleSpeedStepChanged(this, EventArgs.Empty);

        // The NC circuit is high when healthy. Its existing State Link owns
        // synchronization; only the red lamp presentation is inverted.
        LogicLink emergencyState = controllerLinks.GetDigitalIoStateLink("EmergencySwitch");
        emergencyState.ActionBegin += (_, _) => R_Emergency.SignalOn = false;
        emergencyState.ActionEnd += (_, _) => R_Emergency.SignalOn = true;
        R_Emergency.SignalOn = !emergencyState.Value;
        R_ProgramPath.Text = T(RouterXYZACRTCPTexts.NoProgramLoaded);
    }

    private void BindNavigatorPages()
    {
        IntegerLink pageLink = Link.CreateInteger("NavigatorPage", "Navigator Page",
            R_NavigatorPanel.SelectedPage, 0, R_NavigatorPanel.PageCount - 1);
        runtimeLinks.Add(pageLink);
        pageLink.Bind(R_NavigatorPanel);
        R_NavigatorPanel.PageChanged += (_, e) =>
        {
            RefreshExpandedGCodeExecution();
            if ((uint)e.PreviousPage < (uint)R_NavigatorPanel.PageCount)
            {
                sPage previousPage = R_NavigatorPanel.Pages[e.PreviousPage];
                sPage? selectedPage = R_NavigatorPanel.SelectedPageControl;
                if (previousPage == R_NavigatorProgramPage && selectedPage == R_NavigatorGCodePage)
                    CopyNavigatorSelection(R_GCodeView, R_GCodeExpandedView);
                else if (previousPage == R_NavigatorGCodePage && selectedPage == R_NavigatorProgramPage)
                    CopyNavigatorSelection(R_GCodeExpandedView, R_GCodeView);
            }
            RequestRuntimeUiRefresh();
        };
        R_GotoNavigatorGCodePage.Click += (_, _) => pageLink.Value = R_NavigatorPanel.Pages.IndexOf(R_NavigatorGCodePage);
        R_GotoNavigatorProgramPage.Click += (_, _) => pageLink.Value = R_NavigatorPanel.Pages.IndexOf(R_NavigatorProgramPage);

        R_GCodeExpandedView.Document = R_GCodeView.Document;
        R_GCodeExpandedView.BlockDoubleClicked += ProgramBlockDoubleClicked;
        R_GCodeView.CurrentLineChanged += NavigatorExecutionChanged;
        R_GCodeView.DoneIndexChanged += NavigatorExecutionChanged;
        R_ProgramPath.TextChanged += NavigatorProgramPathChanged;
        NavigatorProgramPathChanged(this, EventArgs.Empty);
    }

    private static void CopyNavigatorSelection(sGCodeView source, sGCodeView destination)
    {
        destination.SelectedLineIndex = source.SelectedLineIndex;
        if (destination.SelectedLineIndex >= 0)
            destination.EnsureLineVisible(destination.SelectedLineIndex);
    }

    private void NavigatorExecutionChanged(object? sender, EventArgs e) => RefreshExpandedGCodeExecution();

    private void RefreshExpandedGCodeExecution()
    {
        if (R_GCodeExpandedView.IsDisposed || R_NavigatorPanel.SelectedPageControl != R_NavigatorGCodePage)
            return;
        R_GCodeExpandedView.CurrentLineIndex = R_GCodeView.CurrentLineIndex;
        R_GCodeExpandedView.DoneIndex = R_GCodeView.DoneIndex;
    }

    private void NavigatorProgramPathChanged(object? sender, EventArgs e)
    {
        if (!R_ExpandedProgramPath.IsDisposed) R_ExpandedProgramPath.Text = R_ProgramPath.Text;
    }

    private void BindHeadPages()
    {
        IntegerLink headPageLink = Link.CreateInteger("HeadPage", "Head Page",
            R_HeadPagePanel.SelectedPage, 0, R_HeadPagePanel.PageCount - 1);
        runtimeLinks.Add(headPageLink);
        headPageLink.Bind(R_HeadPagePanel);
        R_HeadPagePanel.PageChanged += (_, _) => RequestRuntimeUiRefresh();
        R_GotoSpindlePage.Click += (_, _) => headPageLink.Value = R_HeadPagePanel.Pages.IndexOf(R_HeadSpindlePage);
        R_GotoRtcpPage.Click += (_, _) => headPageLink.Value = R_HeadPagePanel.Pages.IndexOf(R_HeadRtcpPage);
    }

    /// <summary>
    /// Presents TP0's live velocity factor as percent. Zero is a controlled feed
    /// hold, not Reset: increasing the factor resumes the retained path cursor.
    /// This Link is deliberately outside the persisted machine settings graph.
    /// </summary>
    private RealLink CreateFeedOverrideLink()
    {
        double initialPercent = R_FeedOverride.Value;
        mainToolPath.VelocityFactor = initialPercent / 100.0;
        RealLink link = Link.CreateReal(nameof(ToolPathWrapper.VelocityFactor),
            nameof(ToolPathWrapper.VelocityFactor), initialPercent, R_FeedOverride.Minimum, R_FeedOverride.Maximum, 0);
        link.Description = "TP0 runtime velocity factor in percent.";
        link.Source = controllerLinks.RegisterLiveRuntimeSource(new VelocityFactorSource(mainToolPath));
        link.QueryEnabled += (_, e) => e.Enabled &= controller.IsConnected;
        return link;
    }

    // The Link source interface is the synchronization boundary. A separate
    // factory class adds no ownership; keep the adapter next to its only binding.
    private sealed class VelocityFactorSource(ToolPathWrapper toolPath) : IRealSource, ILiveRuntimeSource
    {
        public event EventHandler? ValueChanged;
        public void PublishValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);
        public double GetValue() => toolPath.VelocityFactor * 100.0;
        public void SetValue(double value) => toolPath.VelocityFactor = value / 100.0;
    }

    private CommandLink CreateRuntimeCommand(string name, Action execute, Func<bool> enabled)
    {
        CommandLink command = CommandLink.CreateCommand(name, DisplayNames.FromIdentifier(name));
        command.QueryEnabled += (_, e) => e.Enabled &= enabled();
        command.ActionExecute += (_, _) => execute();
        runtimeLinks.Add(command);
        return command;
    }

    private void BindSetReferenceCommand(string name, int displayAxisIndex, Button control)
        => CreateRuntimeCommand(
            name,
            () => SetCurrentAxisReference(displayAxisIndex),
            () => commandAvailability.CanSetReference &&
                controller.MotionController.RTCP.SwitchStatus != RtcpSwitchStatus.Pending).Bind(control);

    private bool IsMotionOrProgramActive()
    {
        if (mainToolPath.IsProgramActive || controller.IsBusy || controllerLinks.IsHeadPositioningActive ||
            rtcpToolLengthSource?.IsPending == true)
            return true;
        return controller.ToolPaths.Any(path => path.IsProgramActive || !path.IsIdle)
            || controller.Axes.Any(axis => axis.IsMoving || axis.IsRunning || axis.IsStopping || axis.IsJogging || axis.IsHoming)
            || controller.PLC.Programs.Any(program => !program.Cyclic && program.IsRunning);
    }

    private void SpindleSpeedStepChanged(object? sender, EventArgs e)
    {
        double step = routerLinks.SpindleSpeedStep.Value;
        R_SpindleSpeed.SmallChange = step;
        R_SpindleSpeed.LargeChange = step * 10.0;
    }

    private void BindJog(string name, Button negative, Button positive)
    {
        AxisJogCommandLinks commands = controllerLinks.AxisJogCommands.Single(c => c.AxisName == name);
        commands.Negative.Bind(negative);
        commands.Positive.Bind(positive);
    }

    private void BindJogParameters(string name, sTextBox velocity, sTextBox step, sTextBox position)
    {
        int axisIndex = controller.GetAxisByName(name).Index;
        generalLinks.GetJogVelocityLink(axisIndex).Bind(velocity, options => options.DisplayFormat = "0.###");
        generalLinks.GetJogStepLink(axisIndex).Bind(step, options => options.DisplayFormat = "0.###");
        generalLinks.GetJogPositionLink(axisIndex).Bind(position, options => options.DisplayFormat = "0.###");
    }

}

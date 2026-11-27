using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;
using NT.Tools;

namespace RouterXYZACTC;

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
    private ToolParameterBinding? toolParameterBinding;

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
        IntegerLink jogPageLink = Link.CreateInteger("JogPage", "Jog Page", 0, 0, 1);
        runtimeLinks.Add(jogPageLink);
        jogPageLink.Bind(R_JogPagePanel);
        // Hidden editors skip Link pushes. Refresh after their page becomes visible,
        // even when an idle controller has no status change to trigger a refresh.
        R_JogPagePanel.PageChanged += (_, _) => RequestRuntimeUiRefresh();
        R_GotoJogProperties.Click += (_, _) => jogPageLink.Value = 1;
        R_GotoJog.Click += (_, _) => jogPageLink.Value = 0;
        controllerLinks.XYJogCommands.NegativeXPositiveY.Bind(R_JogNXPY);
        controllerLinks.XYJogCommands.PositiveXPositiveY.Bind(R_JogPXPY);
        controllerLinks.XYJogCommands.NegativeXNegativeY.Bind(R_JogNXNY);
        controllerLinks.XYJogCommands.PositiveXNegativeY.Bind(R_JogPXNY);
        // The cyan checked face shows that operator jog is in step mode.
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

        // Jog and program feed use the Designer range, step and startup value.
        velocityFactorLink = CreateFeedOverrideLink();
        runtimeLinks.Add(velocityFactorLink);
        velocityFactorLink.Bind(R_FeedOverride, options => options.SliderStep = R_FeedOverride.Step);
        generalLinks.JogFeedPercentLink.Bind(R_JogFeedOverride, options => options.SliderStep = R_JogFeedOverride.Step);

        spindleLink = controllerLinks.CreateSpindleControlLink();
        spindleSpeedLink = routerLinks.CurrentSpindleSpeed;
        controllerLinks.BindSpindleSpeed(spindleSpeedLink, routerLinks.SpindleSpeedStep);
        runtimeLinks.Add(spindleLink);
        // Either mapped run direction lights the face while PLC owns spindle operation.
        spindleLink.Bind(R_Spindle);
        // The retained speed Link supplies the range. Step manual input in the
        // source so an executed S value can be displayed between manual steps.
        spindleSpeedLink.Bind(R_SpindleSpeed, options =>
        {
            options.SliderStep = 0;
            options.IgnorePushWhileFocused = false;
        });
        R_SpindleSpeed.Scroll += (_, _) => R_SpindleSpeed.Value = spindleSpeedLink.Value;
        routerLinks.SpindleSpeedStep.Changed += SpindleSpeedStepChanged;
        SpindleSpeedStepChanged(this, EventArgs.Empty);

        toolParameterBinding = new ToolParameterBinding(
            toolChangerLinks,
            R_ToolSelector,
            () => commandAvailability.CanPrepareManualToolChange,
            () => CaptureCommandAvailability().CanPrepareManualToolChange,
            ToolChangeAvailabilityChanged);
        toolChangerLinks.MeasureToolOffsetLink.Bind(R_MeasureToolOffset);
        toolChangerLinks.MeasurementStatusChanged += ToolOffsetMeasurementStatusChanged;
        // The NC circuit is high when healthy. Its existing State Link owns
        // synchronization; only the red lamp presentation is inverted.
        LogicLink emergencyState = controllerLinks.GetDigitalIoStateLink("EmergencySwitch");
        emergencyState.ActionBegin += (_, _) => R_Emergency.SignalOn = false;
        emergencyState.ActionEnd += (_, _) => R_Emergency.SignalOn = true;
        R_Emergency.SignalOn = !emergencyState.Value;
        R_ProgramPath.Text = T(RouterXYZACTCTexts.NoProgramLoaded);
    }

    /// <summary>
    /// Applies the session-only feed override to TP0 without changing the G-code.
    /// At zero, feed hold retains the execution cursor so a higher override can resume it.
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

    private void ToolOffsetMeasurementStatusChanged(string message)
    {
        preserveMeasurementStatusUntil = Environment.TickCount64 + 5000;
        R_ProgramProgress.Text = message;
    }

    private void ToolChangeAvailabilityChanged(string message)
    {
        if (Environment.TickCount64 >= preserveMeasurementStatusUntil)
            R_ProgramProgress.Text = message;
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
            () => commandAvailability.CanSetReference).Bind(control);

    private bool IsMotionOrProgramActive()
    {
        if (mainToolPath.IsProgramActive || controller.IsBusy || controllerLinks.IsHeadPositioningActive || toolChangerLinks.IsChangingTool || IsTransferActive)
            return true;
        return controller.ToolPaths.Any(path => path.IsProgramActive || !path.IsIdle)
            || controller.Axes.Any(axis => axis.IsMoving || axis.IsRunning || axis.IsStopping || axis.IsJogging || axis.IsHoming)
            || controller.PLC.Programs.Any(program => !program.Cyclic && program.IsRunning);
    }

    private bool ConfirmEmptySpindleReference() => sMessage.Show(this, new SMessageRequest(
        T(RouterXYZACTCTexts.EmptySpindleQuestion))
    {
        Title = T(RouterXYZACTCTexts.ToolMeasurement),
        Severity = SMessageSeverity.Warning,
        Buttons = SMessageButtons.YesNo,
        ReportDescription = T(RouterXYZACTCTexts.EmptySpindleDescription)
    }, RuntimeTheme) == SMessageResult.Yes;

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

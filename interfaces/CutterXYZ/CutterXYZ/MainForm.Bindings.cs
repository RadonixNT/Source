using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;
using NT.Tools;

namespace CutterXYZ;

/// <summary>
/// Binds existing commands and value Links to designer controls. Add presentation
/// here; put machine admission in the command owner so keyboard and UI agree.
/// Runtime-only Links belong to runtimeLinks and are disposed with the form.
/// </summary>
public partial class MainForm
{
    private RealLink velocityFactorLink = null!;
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
        controllerLinks.XYJogCommands.NegativeXPositiveY.Bind(R_JogNXPY);
        controllerLinks.XYJogCommands.PositiveXPositiveY.Bind(R_JogPXPY);
        controllerLinks.XYJogCommands.NegativeXNegativeY.Bind(R_JogNXNY);
        controllerLinks.XYJogCommands.PositiveXNegativeY.Bind(R_JogPXNY);
        // The cyan checked face shows that operator jog is in step mode.
        generalLinks.JogModeLink.Bind(R_JogMode);
        BindJogParameters("X", R_JogVelocityX, R_StepX, R_PositionX);
        BindJogParameters("Y", R_JogVelocityY, R_StepY, R_PositionY);
        BindJogParameters("Z", R_JogVelocityZ, R_StepZ, R_PositionZ);
        controllerLinks.AxisJogCommands.Single(c => c.AxisName == "X").GoToPosition.Bind(R_GoToX);
        controllerLinks.AxisJogCommands.Single(c => c.AxisName == "Y").GoToPosition.Bind(R_GoToY);
        controllerLinks.AxisJogCommands.Single(c => c.AxisName == "Z").GoToPosition.Bind(R_GoToZ);

        // Jog and program feed use the Designer range, step and startup value.
        velocityFactorLink = CreateFeedOverrideLink();
        runtimeLinks.Add(velocityFactorLink);
        velocityFactorLink.Bind(R_FeedOverride, options => options.SliderStep = R_FeedOverride.Step);
        generalLinks.JogFeedPercentLink.Bind(R_JogFeedOverride, options => options.SliderStep = R_JogFeedOverride.Step);

        // The NC circuit is high when healthy. Its existing State Link owns
        // synchronization; only the red lamp presentation is inverted.
        LogicLink emergencyState = controllerLinks.GetDigitalIoStateLink("EmergencySwitch");
        emergencyState.ActionBegin += (_, _) => R_Emergency.SignalOn = false;
        emergencyState.ActionEnd += (_, _) => R_Emergency.SignalOn = true;
        R_Emergency.SignalOn = !emergencyState.Value;
        R_ProgramPath.Text = T(RouterXYZTCTexts.NoProgramLoaded);
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
        if (mainToolPath.IsProgramActive || controller.IsBusy || controllerLinks.IsHeadPositioningActive)
            return true;
        return controller.ToolPaths.Any(path => path.IsProgramActive || !path.IsIdle)
            || controller.Axes.Any(axis => axis.IsMoving || axis.IsRunning || axis.IsStopping || axis.IsJogging || axis.IsHoming)
            || controller.PLC.Programs.Any(program => !program.Cyclic && program.IsRunning);
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

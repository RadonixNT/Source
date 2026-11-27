using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.LinkManager;

namespace RouterXYZTC;

/// <summary>
/// Owns the persisted tool-changer model, its manual command and M6 registration.
/// Physical input/output definitions remain exclusively in the PLC I/O mapping.
/// </summary>
internal sealed partial class ToolChangerLinks : IDisposable
{
    private VariableLink<PointD3> toolProbePositionLink = null!;
    private readonly LOGIC toolChangeActive;
    private readonly INTEGER toolChangeFault;
    private readonly LOGIC toolChangeSpindleStopped;
    private readonly LOGIC toolChangeRollersConfirmed;
    private readonly Func<PositioningSettings> positioningSettingsProvider;
    private readonly Func<bool> canStartHomedMotion;
    private readonly Func<bool> confirmEmptySpindleReference;
    private Func<int, bool>? prepareGCodeContinuation;

    internal ToolChangerLinks(
        ControllerWrapper controller,
        LinkGroup parentGroup,
        Func<double> speedProvider,
        Func<PositioningSettings> positioningSettingsProvider,
        Func<bool> canStartHomedMotion,
        Func<bool> confirmEmptySpindleReference)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(parentGroup);
        ArgumentNullException.ThrowIfNull(speedProvider);
        ArgumentNullException.ThrowIfNull(canStartHomedMotion);
        ArgumentNullException.ThrowIfNull(confirmEmptySpindleReference);
        this.controller = controller;
        this.speedProvider = speedProvider;
        this.positioningSettingsProvider = positioningSettingsProvider ?? throw new ArgumentNullException(nameof(positioningSettingsProvider));
        this.canStartHomedMotion = canStartHomedMotion;
        this.confirmEmptySpindleReference = confirmEmptySpindleReference;
        PLCWrapper plc = controller.PLC;
        workPath = controller.ToolPaths[0];
        movePath = controller.ToolPaths.Length > 2 ? controller.ToolPaths[2]
            : throw new ArgumentException("Tool changing requires TP2.", nameof(controller));
        moveAxisNames = movePath.Axes.Select(axis => axis.Name).ToArray();
        if (!HasXyzAxes(moveAxes))
            throw new ArgumentException("Tool changing requires XYZ axes on TP2.", nameof(controller));
        xSlot = Array.FindIndex(moveAxes, axis => axis.Name == "X");
        ySlot = Array.FindIndex(moveAxes, axis => axis.Name == "Y");
        zSlot = Array.FindIndex(moveAxes, axis => axis.Name == "Z");

        // TP0 retains the machining program and its active tool compensation. TP2
        // owns every motion and embedded PLC block generated for a tool change.
        LinkGroup toolChangerGroup = parentGroup.AddGroup(
            "ToolChanger", "Tool Changer",
            "Current/requested tool, exchange motion, clamp timing, sensor position, station geometry, and tool offsets.");
        toolChangerGroup.IconKey = "toolpath";

        ToolChangingVelocity = toolChangerGroup.AddReal(
            "Velocity", "Velocity", 10.0, 0.000001, 1000000.0, 3,
            "Velocity in millimeters per second from PickP to Position and from Position to PlaceP while storing the old tool.");
        PLCVariableBridge.AddApplicationVariable(plc, ToolChangingVelocity,
            "ToolChangingVelocity", PLCVariableAccess.ReadOnly);

        ToolReleaseTime = toolChangerGroup.AddInteger(
            "ReleaseTime", "Release Time", 500, 0, 60000,
            "Continuous confirmed released-state settling time in milliseconds.");
        PLCVariableBridge.AddApplicationVariable(
            plc, ToolReleaseTime, "ToolReleaseTime", PLCVariableAccess.ReadOnly);

        ToolClampTime = toolChangerGroup.AddInteger(
            "ClampTime", "Clamp Time", 500, 0, 60000,
            "Continuous confirmed clamped/piston-retracted settling time in milliseconds.");
        PLCVariableBridge.AddApplicationVariable(
            plc, ToolClampTime, "ToolClampTime", PLCVariableAccess.ReadOnly);
        IntegerLink feedbackTimeout = toolChangerGroup.AddInteger(
            "FeedbackTimeout", "Feedback Timeout", 5000, 1, 60000,
            "Additional sensor wait in milliseconds, beyond release/clamp settling time.");
        PLCVariableBridge.AddApplicationVariable(
            plc, feedbackTimeout, "ToolFeedbackTimeout", PLCVariableAccess.ReadOnly);

        // These session-only variables coordinate the generated stages. StopSpindle/WaitSpindle
        // own the spindle-stop handshake. ToolChangeActive drives the mapped
        // ToolChanging output continuously across TP2 and Offset stage boundaries.
        toolChangeSpindleStopped = plc.ApplicationVariables.AddLogic(
            "ToolChangeSpindleStopped", false, PLCVariableAccess.ReadWrite);
        toolChangeFault = plc.ApplicationVariables.AddInteger(
            "ToolChangeFault", 0, PLCVariableAccess.ReadWrite);
        toolChangeActive = plc.ApplicationVariables.AddLogic(
            "ToolChangeActive", false, PLCVariableAccess.ReadOnly);
        toolChangeRollersConfirmed = plc.ApplicationVariables.AddLogic(
            "ToolChangeRollersConfirmed", false, PLCVariableAccess.ReadWrite);

        InitializeToolLength(plc, toolChangerGroup);

        CurrentTool = toolChangerGroup.AddInteger(
            "CurrentTool", "Current Tool",
            0,
            0,
            Initialize.ToolCount,
            "Recorded physical tool in the spindle; 0 means empty.");
        PLCVariableBridge.AddApplicationVariable(plc, CurrentTool, PLCVariableAccess.ReadWrite);

        AutomaticToolChangeEnabled = toolChangerGroup.AddLogic(
            "AutomaticToolChange", "Enabled",
            true,
            "Allows G-code M6 to perform a physical tool exchange. Manual tool changes remain available when disabled.");
        AutomaticToolChangeEnabled.QueryEnabled += (_, e) =>
            e.Enabled &= !workPath.IsProgramActive;

        // NewTool is an operation argument, so it stays outside the persisted
        // settings graph.
        NewTool = Link.CreateInteger("NewTool", "NewTool", 0, 0, Initialize.ToolCount);
        PLCVariableBridge.AddApplicationVariable(plc, NewTool, PLCVariableAccess.ReadOnly);

        // The operator command always uses manual mode and never resumes TP0.
        ChangeToolLink = CommandLink.CreateCommand("ChangeTool", DisplayNames.FromIdentifier("ChangeTool"));
        ChangeToolLink.QueryEnabled += (_, e) => e.Enabled &= CanChangeTool(NewTool.Value, ManualChangeMode);
        ChangeToolLink.ActionExecute += (_, _) => StartManualChange(NewTool.Value);
        controller.ConnectionStateChanged += OnConnectionChanged;

        ToolsGroup = toolChangerGroup.AddGroup(
            "Tools", "Tools",
            "Per-tool holder position, route vectors, and XYZ machining offset.");
        ToolsGroup.IconKey = "toolpath";

        var tools = new Tool[Initialize.ToolCount];
        for (int index = 0; index < tools.Length; index++)
            // User-facing tool numbers are one-based; the collection remains
            // zero-based for normal C# indexing.
            tools[index] = new Tool(plc, ToolsGroup, index + 1, () => CurrentTool.Value);
        Tools = tools;
        CurrentTool.Changed += CurrentToolChanged;
        // M6 receives the selected T value through the ToolPath ProgramArg channel.
        controller.MotionController.Macros.AddProgram("M6", QueueM6);
    }

    internal PointD3 ToolProbePosition => toolProbePositionLink.Value;

    internal IReadOnlyList<Tool> Tools { get; }
    internal LinkGroup ToolsGroup { get; }
    internal LinkGroup ToolLengthMeasurmentGroup { get; private set; } = null!;

    internal RealLink ToolChangingVelocity { get; }
    internal RealLink ToolSearchVelocity { get; private set; } = null!;
    internal RealLink ToolFineSearchVelocity { get; private set; } = null!;
    internal RealLink ToolProbeApproachClearance { get; private set; } = null!;
    internal RealLink ReferenceOffsetZ { get; private set; } = null!;
    internal LogicLink ReferenceValid { get; private set; } = null!;
    internal IntegerLink ToolReleaseTime { get; }
    internal IntegerLink ToolClampTime { get; }
    internal IntegerLink CurrentTool { get; }
    internal LogicLink AutomaticToolChangeEnabled { get; }
    internal IntegerLink NewTool { get; }
    internal CommandLink ChangeToolLink { get; }
    internal CommandLink MeasureToolOffsetLink { get; private set; } = null!;
    internal event Action<string>? MeasurementStatusChanged;

    internal void ConfigureGCodeContinuation(Func<int, bool> prepare)
        => prepareGCodeContinuation = prepare ?? throw new ArgumentNullException(nameof(prepare));

    /// <summary>
    /// Reports whether the operator may prepare a manual request. Hardware feedback,
    /// exchange programs and the selected pocket are checked when Set tool submits the
    /// actual tool number; they must not disable the selector for a hypothetical tool.
    /// </summary>
    internal bool CanAcceptManualRequest(out string reason)
    {
        lock (gate)
        {
            reason = string.Empty;
            if (disposed) reason = "Tool changer is disposed.";
            else if (synchronizeOffset) reason = synchronizationError.Length == 0
                ? "Initial tool offset is not acknowledged yet."
                : synchronizationError;
            else if (pending is not null || pendingM6 is not null || pendingMeasurement is not null)
                reason = "Another tool operation is pending.";
            else if (!controller.IsConnected) reason = "Controller is offline.";
            else if (!canStartHomedMotion()) reason = "All configured axes must be homed and no Home operation may be active.";
            return reason.Length == 0;
        }
    }

    /// <summary>Captures the operator request and starts the shared manual exchange path.</summary>
    internal bool StartManualChange(long newTool)
    {
        return StartManualChange(newTool, out _);
    }

    /// <summary>Captures a validated operator request and returns its exact rejection reason.</summary>
    internal bool StartManualChange(long newTool, out string reason)
    {
        if (!ValidTool(newTool))
        {
            reason = "Invalid requested tool number.";
            return false;
        }
        NewTool.Value = newTool;
        if (!CanChangeTool(newTool, ManualChangeMode, out reason))
        {
            RejectChange(reason);
            return false;
        }

        bool started = ChangeTool(newTool, ManualChangeMode);
        if (!started && reason.Length == 0)
            reason = "Tool-change route could not be prepared. See the controller report.";
        return started;
    }

    internal bool IsToolEnabled(long toolNumber)
        => toolNumber == 0 ||
           toolNumber is >= 1 and <= Initialize.ToolCount && Tools[checked((int)toolNumber - 1)].Enabled;

    /// <summary>
    /// Checks the semantic three-axis layout without sorting or allocating during
    /// command-availability polling. Order is resolved separately into compact slots.
    /// </summary>
    private static bool HasXyzAxes(ReadOnlySpan<AxisWrapper> axes)
    {
        if (axes.Length != 3) return false;
        int mask = 0;
        foreach (AxisWrapper axis in axes)
            mask |= axis.Name switch { "X" => 1, "Y" => 2, "Z" => 4, _ => 0 };
        return mask == 7;
    }

    /// <summary>
    /// Toggles one magazine tool only while every motion source is inactive.
    /// The tool physically recorded in the spindle always remains enabled.
    /// </summary>
    internal bool TryToggleToolEnabled(int toolNumber)
    {
        lock (gate)
        {
            if (disposed || toolNumber is < 1 or > Initialize.ToolCount ||
                CurrentTool.Value == toolNumber || IsActive ||
                controller.ToolPaths.Any(path => !path.IsIdle) ||
                controller.Axes.Any(axis => axis.IsMoving || axis.IsRunning || axis.IsStopping ||
                    axis.IsJogging || axis.IsHoming))
                return false;

            LogicLink enabled = Tools[toolNumber - 1].EnabledLink;
            bool requested = !enabled.Value;
            enabled.Value = requested;
            return enabled.Value == requested;
        }
    }

    private void CurrentToolChanged(object? sender, LinkChangedEventArgs e)
    {
        long current = CurrentTool.Value;
        if (current is >= 1 and <= Initialize.ToolCount)
            Tools[checked((int)current - 1)].EnsureCurrentToolRemainsEnabled();
    }

}

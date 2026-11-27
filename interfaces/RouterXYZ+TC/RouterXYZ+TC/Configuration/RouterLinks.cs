using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.LinkManager;

namespace RouterXYZTC;

/// <summary>
/// Owns router-specific settings and read-only PLC cutting bounds. Roller policy
/// executes in Background.plc; this class only supplies configuration and the
/// accepted preparation result. Clear availability before replacing any bound so
/// the PLC never treats a partially published rectangle as a valid cutting region.
/// </summary>
internal sealed class RouterLinks
{
    private readonly LOGIC cuttingBoundsAvailable;
    private readonly REAL cuttingMinimumX;
    private readonly REAL cuttingMaximumX;
    private readonly REAL cuttingMinimumY;
    private readonly REAL cuttingMaximumY;
    internal LoaderLinks Loader { get; }
    internal LinkGroup Group { get; }
    internal RealLink MinimumSpindleSpeed { get; }
    internal RealLink MaximumSpindleSpeed { get; }
    internal IntegerLink SpindleSpeedStep { get; }
    internal RealLink CurrentSpindleSpeed { get; }
    internal IntegerLink SpindleFeedbackTimeout { get; }
    internal RealLink G73RetractDistance { get; }
    internal LinkGroup RollerGroup { get; }
    internal LogicLink Roller1Enabled { get; }
    internal LogicLink Roller2Enabled { get; }
    internal TextLink OnBridgeAxis { get; }
    internal RealLink Roller1Offset { get; }
    internal RealLink Roller2Offset { get; }
    internal IntegerLink UnloaderUpTime { get; }
    internal IntegerLink UnloaderDownTime { get; }
    internal RealLink UnloaderEndPosition { get; }
    internal RealLink UnloaderStartPosition { get; }
    internal RealLink UnloaderVelocity { get; }
    internal bool ValidateSpindleSpeedRange()
    {
        bool valid = MinimumSpindleSpeed.Value <= MaximumSpindleSpeed.Value;
        MinimumSpindleSpeed.SetState(valid ? OperationalState.Normal : OperationalState.Error,
            valid ? string.Empty : "Minimum Spindle Speed must be less than or equal to Maximum Spindle Speed.");
        return valid;
    }

    private void SpindleSpeedRangeChanged(object? sender, EventArgs e)
    {
        if (!ValidateSpindleSpeedRange()) return;
        CurrentSpindleSpeed.Minimum = MinimumSpindleSpeed.Value;
        CurrentSpindleSpeed.Maximum = MaximumSpindleSpeed.Value;
    }

    internal RouterLinks(PLCWrapper plc, LinkGroup parentGroup)
    {
        ArgumentNullException.ThrowIfNull(plc);
        ArgumentNullException.ThrowIfNull(parentGroup);
        Group = parentGroup.AddGroup("Router", "Router", "Router-specific machine settings.");
        Group.IconKey = "parameter";
        Loader = new LoaderLinks(plc, Group);
        G73RetractDistance = Group.AddReal("G73RetractDistance", "G73 Retract Distance",
            0.254, 0.001, 1000, 3,
            "G73 chip-breaking retract in millimeters (0.001..1000), capped at R. Independent of G20/G21 and G51. Applies when loading or reloading G-code; already prepared paths retain their value.");
        LinkGroup unloaderGroup = Group.AddGroup("Unloader", "Unloader", "Unloader settings.");
        unloaderGroup.IconKey = "parameter";
        UnloaderVelocity = unloaderGroup.AddReal("UnloaderVelocity", "Transfer Velocity", 100,
            0.001, 1000000, 3, "Unloading stroke velocity in mm/s. Approach and Z lift use traverse velocity.");
        PLCVariableBridge.AddApplicationVariable(plc, UnloaderVelocity, PLCVariableAccess.ReadOnly);
        IntegerLink unloaderUpTime = UnloaderUpTime = unloaderGroup.AddInteger("UnloaderUpTime", "Up Time", 1000, 0, 60000,
            "Up delay in milliseconds without a sensor; with a sensor, confirmation is immediate and the deadline is three times this value.");
        IntegerLink unloaderDownTime = UnloaderDownTime = unloaderGroup.AddInteger("UnloaderDownTime", "Down Time", 1000, 0, 60000,
            "Down delay in milliseconds without a sensor; with a sensor, confirmation is immediate and the deadline is three times this value.");
        PLCVariableBridge.AddApplicationVariable(plc, unloaderUpTime, PLCVariableAccess.ReadOnly);
        PLCVariableBridge.AddApplicationVariable(plc, unloaderDownTime, PLCVariableAccess.ReadOnly);
        RealLink unloaderEndPosition = UnloaderEndPosition = unloaderGroup.AddReal("UnloaderEndPosition", "End Position",
            0, -1000000000.0, 1000000000.0, 3, "Unloader end position in millimeters.");
        RealLink unloaderStartPosition = UnloaderStartPosition = unloaderGroup.AddReal("UnloaderStartPosition", "Start Position",
            0, -1000000000.0, 1000000000.0, 3, "Unloader start position in millimeters.");
        PLCVariableBridge.AddApplicationVariable(plc, unloaderEndPosition, PLCVariableAccess.ReadOnly);
        PLCVariableBridge.AddApplicationVariable(plc, unloaderStartPosition, PLCVariableAccess.ReadOnly);
        OnBridgeAxis = Group.AddText("OnBridgeAxis", "On-Bridge Axis", "X", new[] { "X", "Y" },
            "Axis travelling on the bridge. Rollers are parallel to this axis; X uses Y roller offsets, Y uses X roller offsets.");
        OnBridgeAxis.ItemsOnly = true;
        OnBridgeAxis.Validator = value => value is "X" or "Y";
        // RollerParallelX is the derived PLC geometry flag, not a second setting.
        PLCVariableBridge.AddParallelXAxisApplicationVariable(plc, OnBridgeAxis, "RollerParallelX");
        LinkGroup spindle = Group.AddGroup("Spindle", "Spindle", "Spindle setpoint, scale and feedback settings.");
        spindle.IconKey = "parameter";
        MaximumSpindleSpeed = spindle.AddReal("MaximumSpindleSpeed", "Maximum Spindle Speed",
            24000, 1, 1000000, 0,
            "Spindle full-scale speed in RPM. S is divided by this value and clamped to 0..1.");
        MinimumSpindleSpeed = spindle.AddReal("MinimumSpindleSpeed", "Minimum Spindle Speed",
            Math.Min(1000, MaximumSpindleSpeed.Value), 0, 1000000, 0, "Lower limit in RPM for the slider and retained speed.");
        SpindleSpeedStep = spindle.AddInteger("SpindleSpeedStep", "Spindle Speed Step",
            100, 1, 1000000, "Increment in RPM for manual slider adjustments.");
        // This Link supplies the slider setpoint and is saved with the Spindle group.
        CurrentSpindleSpeed = spindle.AddReal("CurrentSpindleSpeed", "Current Spindle Speed",
            1000, Math.Min(MinimumSpindleSpeed.Value, MaximumSpindleSpeed.Value), MaximumSpindleSpeed.Value, 0,
            "Last requested speed in RPM, limited to the configured slider range.");
        MinimumSpindleSpeed.Changed += SpindleSpeedRangeChanged;
        MaximumSpindleSpeed.Changed += SpindleSpeedRangeChanged;
        SpindleFeedbackTimeout = spindle.AddInteger("SpindleFeedbackTimeout", "Spindle Feedback Timeout",
            5000, 1, 60000, "Maximum additional wait in milliseconds for spindle speed/stop feedback.");
        PLCVariableBridge.AddApplicationVariable(plc, MaximumSpindleSpeed, PLCVariableAccess.ReadOnly);
        PLCVariableBridge.AddApplicationVariable(plc, SpindleFeedbackTimeout, PLCVariableAccess.ReadOnly);
        IntegerLink spindleOnTime = spindle.AddInteger("SpindleOnTime", "Spindle On Time",
            3000, 0, 60000, "Minimum wait in milliseconds after M3/M4 starts the spindle; does not measure RPM.");
        IntegerLink spindleOffTime = spindle.AddInteger("SpindleOffTime", "Spindle Off Time",
            3000, 0, 60000, "Deceleration wait in milliseconds before Background starts its additional 1000 ms quiet check.");
        PLCVariableBridge.AddApplicationVariable(plc, spindleOnTime, PLCVariableAccess.ReadOnly);
        PLCVariableBridge.AddApplicationVariable(plc, spindleOffTime, PLCVariableAccess.ReadOnly);
        RollerGroup = Group.AddGroup("Roller", "Roller", "Roller availability, orientation and spindle-relative offsets.");
        RollerGroup.IconKey = "axis";
        IntegerLink rollerTimeout = RollerGroup.AddInteger("ReleaseTimeout", "Release Timeout", 5000, 1, 60000,
            "Maximum milliseconds to confirm enabled rollers are free before tool-change motion.");
        PLCVariableBridge.AddApplicationVariable(plc, rollerTimeout, "RollerReleaseTimeout", PLCVariableAccess.ReadOnly);
        Roller1Enabled = RollerGroup.AddLogic("Roller1Enabled", "Roller 1 Enabled", false,
            "Enables Roller 1.");
        Roller2Enabled = RollerGroup.AddLogic("Roller2Enabled", "Roller 2 Enabled", false,
            "Enables Roller 2.");
        Roller1Offset = RollerGroup.AddReal("Roller1Offset", "Roller 1 Offset", 0,
            -1000000000.0, 1000000000.0, 3);
        Roller2Offset = RollerGroup.AddReal("Roller2Offset", "Roller 2 Offset", 0,
            -1000000000.0, 1000000000.0, 3);
        const string description = "Signed roller position minus spindle position in millimeters. OnBridgeAxis X uses a Y offset; OnBridgeAxis Y uses an X offset. Changing OnBridgeAxis retains the numeric value.";
        Roller1Offset.Description = description;
        Roller2Offset.Description = description;
        PLCVariableBridge.AddApplicationVariable(plc, Roller1Enabled, PLCVariableAccess.ReadOnly);
        PLCVariableBridge.AddApplicationVariable(plc, Roller2Enabled, PLCVariableAccess.ReadOnly);
        PLCVariableBridge.AddApplicationVariable(plc, Roller1Offset, PLCVariableAccess.ReadOnly);
        PLCVariableBridge.AddApplicationVariable(plc, Roller2Offset, PLCVariableAccess.ReadOnly);
        cuttingBoundsAvailable = plc.ApplicationVariables.AddLogic("RollerCuttingBoundsAvailable", false, PLCVariableAccess.ReadOnly);
        cuttingMinimumX = plc.ApplicationVariables.AddReal("RollerCuttingMinimumX", 0, PLCVariableAccess.ReadOnly);
        cuttingMaximumX = plc.ApplicationVariables.AddReal("RollerCuttingMaximumX", 0, PLCVariableAccess.ReadOnly);
        cuttingMinimumY = plc.ApplicationVariables.AddReal("RollerCuttingMinimumY", 0, PLCVariableAccess.ReadOnly);
        cuttingMaximumY = plc.ApplicationVariables.AddReal("RollerCuttingMaximumY", 0, PLCVariableAccess.ReadOnly);
    }

    /// <summary>Publishes accepted cutting bounds once per load, in untransformed TP0 coordinates.</summary>
    internal void SetCuttingBounds(ToolPathGeometrySnapshot? geometry)
    {
        cuttingBoundsAvailable.Value = false;
        if (geometry is null || !geometry.HasCuttingBounds) return;
        // Background uses the machine profile's fixed compact XYZ slots.
        if (geometry.AxisNames.Count < 2 || geometry.AxisNames[0] != "X" || geometry.AxisNames[1] != "Y")
            throw new InvalidOperationException("Roller control requires X and Y in TP0 slots 0 and 1.");
        double minX = geometry.CuttingMinimumPosition[0], maxX = geometry.CuttingMaximumPosition[0];
        double minY = geometry.CuttingMinimumPosition[1], maxY = geometry.CuttingMaximumPosition[1];
        if (!double.IsFinite(minX) || !double.IsFinite(maxX) || minX > maxX ||
            !double.IsFinite(minY) || !double.IsFinite(maxY) || minY > maxY) return;
        cuttingMinimumX.Value = minX;
        cuttingMaximumX.Value = maxX;
        cuttingMinimumY.Value = minY;
        cuttingMaximumY.Value = maxY;
        cuttingBoundsAvailable.Value = true;
    }
}

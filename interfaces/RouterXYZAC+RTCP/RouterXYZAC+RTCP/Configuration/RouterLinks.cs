using NT;
using NT.Controller.PLC;
using NT.LinkManager;

namespace RouterXYZACRTCP;

/// <summary>Owns router-specific machine settings.</summary>
internal sealed class RouterLinks
{
    internal LinkGroup Group { get; }
    internal RealLink MinimumSpindleSpeed { get; }
    internal RealLink MaximumSpindleSpeed { get; }
    internal IntegerLink SpindleSpeedStep { get; }
    internal RealLink CurrentSpindleSpeed { get; }
    internal IntegerLink SpindleFeedbackTimeout { get; }
    internal RealLink G73RetractDistance { get; }
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
        G73RetractDistance = Group.AddReal("G73RetractDistance", "G73 Retract Distance",
            0.254, 0.001, 1000, 3,
            "G73 chip-breaking retract in millimeters (0.001..1000), capped at R. Independent of G20/G21 and G51. Applies when loading or reloading G-code; already prepared paths retain their value.");
        LinkGroup spindle = Group.AddGroup("Spindle", "Spindle", "Spindle setpoint, scale and feedback settings.");
        spindle.IconKey = "parameter";
        MaximumSpindleSpeed = spindle.AddReal("MaximumSpindleSpeed", "Maximum Spindle Speed",
            24000, 1, 1000000, 0,
            "Spindle full-scale speed in RPM. S is divided by this value and clamped to 0..1.");
        MinimumSpindleSpeed = spindle.AddReal("MinimumSpindleSpeed", "Minimum Spindle Speed",
            Math.Min(1000, MaximumSpindleSpeed.Value), 0, 1000000, 0, "Lower limit in RPM for the slider and retained speed.");
        SpindleSpeedStep = spindle.AddInteger("SpindleSpeedStep", "Spindle Speed Step",
            100, 1, 1000000, "Increment in RPM for manual slider adjustments.");
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
    }
}

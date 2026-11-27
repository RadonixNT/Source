using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;

namespace RouterXYZACTC;

/// <summary>
/// Owns tool-setter settings and the managed half of MeasureToolLength. The PLC
/// owns probing motion and publishes contact Z; only a successful activation
/// commits the empty-spindle reference or tool length. Compensation then uses
/// the same stopped TP0 acknowledgement as tool exchange. Cancellation retains
/// the last confirmed calibration and never treats an unconfirmed contact as data.
/// </summary>
internal sealed partial class ToolChangerLinks
{
    private const string ToolLengthProgramName = "MeasureToolLength";
    private REAL measuredToolContactZ = null!;
    private PendingMeasurement? pendingMeasurement;

    private void InitializeToolLength(PLCWrapper plc, LinkGroup toolChangerGroup)
    {
        ToolLengthMeasurmentGroup = toolChangerGroup.AddGroup(
            "ToolLengthMeasurment", "Tool Length Measurement",
            "Probe geometry, search motion and the empty-spindle reference used for tool-length measurement.");
        ToolLengthMeasurmentGroup.IconKey = "toolpath";

        toolProbePositionLink = ToolLengthMeasurmentGroup.AddValue(
            "ProbePosition", "Probe Position", PointD3.Zero,
            "XYZ probe position in machine coordinates, in millimeters; Z is the simulated empty-spindle contact position.");
        PLCVariableBridge.AddApplicationVariable(plc, toolProbePositionLink, "ToolProbePosition");

        ToolSearchVelocity = ToolLengthMeasurmentGroup.AddReal(
            "SearchVelocity", "Search Velocity", 10.0, 0.000001, 1000000.0, 3,
            "Velocity in mm/s used for the first downward contact search and upward probe release.");
        PLCVariableBridge.AddApplicationVariable(
            plc, ToolSearchVelocity, "ToolSearchVelocity", PLCVariableAccess.ReadOnly);

        ToolFineSearchVelocity = ToolLengthMeasurmentGroup.AddReal(
            "FineSearchVelocity", "Fine Search Velocity", 1.0, 0.000001, 1000000.0, 3,
            "Velocity in mm/s used for the final precision contact search.");
        PLCVariableBridge.AddApplicationVariable(
            plc, ToolFineSearchVelocity, "ToolFineSearchVelocity", PLCVariableAccess.ReadOnly);

        ToolProbeApproachClearance = ToolLengthMeasurmentGroup.AddReal(
            "ApproachClearance", "Approach Clearance", 100.0, 0.000001, 1000000.0, 3,
            "Positive Z clearance in millimeters above the empty-spindle probe contact used for approach and final retract.");
        PLCVariableBridge.AddApplicationVariable(
            plc, ToolProbeApproachClearance, "ToolProbeApproachClearance", PLCVariableAccess.ReadOnly);

        ReferenceOffsetZ = ToolLengthMeasurmentGroup.AddReal(
            "ReferenceOffsetZ", "Reference Offset Z", 0.0, -1000000.0, 1000000.0, 6,
            "Measured machine Z in millimeters where the empty spindle touches the probe.");
        ReferenceValid = ToolLengthMeasurmentGroup.AddLogic(
            "ReferenceValid", "Reference Valid", false, "Not measured", "Measured",
            "Confirms that ReferenceOffsetZ was captured by a completed empty-spindle measurement.");
        ReferenceOffsetZ.View = AccessView.ReadOnly;
        ReferenceValid.View = AccessView.ReadOnly;

        measuredToolContactZ = plc.ApplicationVariables.AddReal(
            "MeasuredToolContactZ", 0.0, PLCVariableAccess.ReadWrite);

        // This is an operator action, not a persisted setting. Keep it outside
        // the ToolLengthMeasurment settings branch and bind it directly to the UI.
        MeasureToolOffsetLink = CommandLink.CreateCommand("MeasureToolOffset", "Measure Tool");
        MeasureToolOffsetLink.Description =
            "With an empty spindle, measure the reference; otherwise measure the current tool length relative to that reference.";
        MeasureToolOffsetLink.QueryEnabled += (sender, e) =>
            e.Enabled &= CanAcceptToolOffsetMeasurement(out _);
        MeasureToolOffsetLink.ActionExecute += (_, _) => ExecuteToolLengthMeasurement();

        toolProbePositionLink.Flush();
        ToolSearchVelocity.Flush();
        ToolFineSearchVelocity.Flush();
        ToolProbeApproachClearance.Flush();
        ReferenceOffsetZ.Flush();
        ReferenceValid.Flush();
    }

    private void ExecuteToolLengthMeasurement()
    {
        bool referenceMeasurement = CurrentTool.Value == 0;
        if (!CanMeasureToolLength(referenceMeasurement, out string reason))
        {
            RejectMeasurement(reason);
            return;
        }

        if (referenceMeasurement && !confirmEmptySpindleReference())
        {
            PublishMeasurementStatus("Empty-spindle reference measurement cancelled.");
            return;
        }

        StartToolLengthMeasurement(referenceMeasurement);
    }

    private void StartToolLengthMeasurement(bool referenceMeasurement)
    {
        lock (gate)
        {
            if (!CanMeasureToolLength(referenceMeasurement, out string reason))
            {
                RejectMeasurement(reason);
                return;
            }

            PLCProgram program = controller.PLC.Programs[ToolLengthProgramName];
            long previousActivation = program.ActivationId;
            if (!program.Run())
            {
                RejectMeasurement("PLC Run request was rejected.");
                return;
            }
            pendingMeasurement = new PendingMeasurement(
                program, CurrentTool.Value, referenceMeasurement, previousActivation, Environment.TickCount64);
            PublishMeasurementStatus(referenceMeasurement
                ? "Measuring empty-spindle Z reference..."
                : $"Measuring T{CurrentTool.Value:00} Z offset...");
        }
    }

    private bool IsProbeOrientationReady()
    {
        foreach (string name in new[] { "A", "C" })
        {
            AxisWrapper axis = controller.GetAxisByName(name);
            if (!double.IsFinite(axis.CommandPosition) || !double.IsFinite(axis.ActualPosition)
                || !double.IsFinite(axis.AxisTolerance) || axis.AxisTolerance <= 0
                || Math.Abs(axis.CommandPosition) > axis.AxisTolerance
                || Math.Abs(axis.ActualPosition) > axis.AxisTolerance || !axis.IsStopped) return false;
        }
        return true;
    }

    private bool CanMeasureToolLength(bool referenceMeasurement, out string reason)
    {
        lock (gate)
        {
            reason = string.Empty;
            if (disposed) reason = "Tool changer is disposed.";
            else if (synchronizeOffset) reason = synchronizationError.Length == 0
                ? "Current tool offset is not acknowledged yet." : synchronizationError;
            else if (pending is not null || pendingM6 is not null || pendingMeasurement is not null)
                reason = "Another tool operation is pending.";
            else if (referenceMeasurement && CurrentTool.Value != 0)
                reason = "The spindle must be empty before measuring the reference.";
            else if (!referenceMeasurement && CurrentTool.Value is < 1 or > Initialize.ToolCount)
                reason = "A physical current tool is required.";
            else if (!referenceMeasurement && !ReferenceValid.Value)
                reason = "Measure the empty-spindle reference before measuring a tool.";
            else if (!controller.IsConnected) reason = "Controller is offline.";
            else if (controller.PLC.IsApplying) reason = "PLC configuration is being applied.";
            else if (workPath.IsProgramActive) reason = "The machining program is active.";
            else if (!canStartHomedMotion()) reason = "All configured axes must be homed and no Home operation may be active.";
            else if (!IsProbeOrientationReady()) reason = "Tool-length measurement requires stopped A and C at machine zero; no RTCP is applied.";
            else if (!ProbeInputAvailable()) reason = "ToolHeightSensor must be assigned to a physical digital input.";
            else if (!MeasurementSettingsValid(out reason)) return false;
            else if (!MeasurementSpindleStateValid(referenceMeasurement, out reason)) return false;
            else if (!controller.PLC.Programs.TryGetValue(
                    ToolLengthProgramName, out PLCProgram? program) ||
                program is not { IsLoaded: true, Enabled: true, Cyclic: false, IsRunning: false, HasLoadError: false })
                reason = "MeasureToolLength PLC is missing, disabled, busy, or not loaded.";
            else if (!HasXyzacAxes(workPath.Axes))
                reason = "Tool compensation requires XYZAC axes on TP0.";
            else
            {
                foreach (ToolPathWrapper path in controller.ToolPaths)
                    if (!path.IsIdle) { reason = $"TP{path.Index} is not idle."; break; }
            }

            if (reason.Length == 0 && !workPath.IsStopped) reason = "TP0 is not stopped.";
            if (reason.Length == 0 && !ChangeAxesAvailable(requireStopped: true, out reason)) return false;
            return reason.Length == 0;
        }
    }

    /// <summary>
    /// Keeps the operator command available whenever motion may be requested.
    /// Probe, reference and spindle feedback are validated again by the action so
    /// a hidden configuration detail cannot permanently lock the button.
    /// </summary>
    private bool CanAcceptToolOffsetMeasurement(out string reason)
    {
        lock (gate)
        {
            reason = string.Empty;
            if (disposed) reason = "Tool changer is disposed.";
            else if (synchronizeOffset) reason = synchronizationError.Length == 0
                ? "Current tool offset is not acknowledged yet." : synchronizationError;
            else if (pending is not null || pendingM6 is not null || pendingMeasurement is not null)
                reason = "Another tool operation is pending.";
            else if (!controller.IsConnected) reason = "Controller is offline.";
            else if (controller.PLC.IsApplying) reason = "PLC configuration is being applied.";
            else if (workPath.IsProgramActive) reason = "The machining program is active.";
            else if (!canStartHomedMotion()) reason = "All configured axes must be homed and no Home operation may be active.";
            else
            {
                foreach (ToolPathWrapper path in controller.ToolPaths)
                    if (!path.IsIdle) { reason = $"TP{path.Index} is not idle."; break; }
            }

            if (reason.Length == 0 && !workPath.IsStopped) reason = "TP0 is not stopped.";
            if (reason.Length == 0 && !ChangeAxesAvailable(requireStopped: true, out reason)) return false;
            return reason.Length == 0;
        }
    }

    private bool ProbeInputAvailable()
        => controller.PLC.IOMappings.TryGet("ToolHeightSensor", out PLCIOMapping? sensor)
            && sensor is { Type: PLCIOMappingType.DigitalInput, IsPhysicalTarget: true };

    private bool MeasurementSettingsValid(out string reason)
    {
        PointD3 probe = ToolProbePosition;
        double approachZ = probe.Z + ToolProbeApproachClearance.Value;
        AxisWrapper x = controller.GetAxisByName("X");
        AxisWrapper y = controller.GetAxisByName("Y");
        AxisWrapper z = controller.GetAxisByName("Z");
        bool valid = double.IsFinite(probe.X) && double.IsFinite(probe.Y) && double.IsFinite(probe.Z)
            && double.IsFinite(approachZ)
            && ToolSearchVelocity.Value > 0.0 && double.IsFinite(ToolSearchVelocity.Value)
            && ToolFineSearchVelocity.Value > 0.0 && double.IsFinite(ToolFineSearchVelocity.Value)
            && ToolProbeApproachClearance.Value > 0.0 && double.IsFinite(ToolProbeApproachClearance.Value)
            && probe.X >= x.MinCourse && probe.X <= x.MaxCourse
            && probe.Y >= y.MinCourse && probe.Y <= y.MaxCourse
            && probe.Z >= z.MinCourse && probe.Z <= z.MaxCourse
            && approachZ >= z.MinCourse && approachZ <= z.MaxCourse;
        reason = valid ? string.Empty : "Tool-length probe position, approach clearance, or search velocities are outside the machine limits.";
        return valid;
    }

    private bool MeasurementSpindleStateValid(bool referenceMeasurement, out string reason)
    {
        reason = string.Empty;
        PLCProgram? background = controller.PLC.Programs.TryGetValue("Background", out PLCProgram? program) ? program : null;
        if (background is not { IsLoaded: true, Enabled: true, Cyclic: true, IsRunning: true, HasLoadError: false })
            reason = "The cyclic Background program must be running before measurement.";
        else if (!((LOGIC)controller.PLC.ApplicationVariables["SpindleFeedbackConfigured"]).Value)
            reason = "The spindle run output is not configured.";
        else
        {
            foreach (string signal in new[] { "PressureSensor", "SpindleThermalOK", "SpindleDriveReady" })
                if (!SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings[signal]))
                {
                    reason = $"{signal} is not confirmed.";
                    return false;
                }
            if (!SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindleToolReleased"], false) ||
                     !SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindlePistonRetracted"]))
                reason = "The spindle grip mechanism is not closed and retracted.";
            else if (!referenceMeasurement && !SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindleToolClamped"]))
                reason = "The current tool is not confirmed clamped by SpindleToolClamped(S1).";
            else if (controller.PLC.IOMappings["SpindleToolRelease"].GetLogic())
                reason = "The spindle tool-release output is active.";
        }
        return reason.Length == 0;
    }

    private void UpdateToolLength()
    {
        PendingMeasurement? measurement = pendingMeasurement;
        if (measurement is null) return;
        if (!controller.IsConnected || controller.PLC.IsApplying || CurrentTool.Value != measurement.ToolNumber)
        {
            FailMeasurement("Measurement was interrupted or the current tool changed.");
            return;
        }

        PLCProgram program = measurement.Program;
        if (measurement.ActivationId == 0)
        {
            long activation = program.ActivationId;
            if (activation == measurement.PreviousActivationId)
            {
                if (Environment.TickCount64 - measurement.RequestedAt >= StageTimeoutMilliseconds)
                    FailMeasurement("PLC activation was not acknowledged.");
                return;
            }
            measurement.ActivationId = activation;
        }

        if (program.CompletionId != measurement.ActivationId || program.IsRunning) return;
        pendingMeasurement = null;
        if (program.LastExitReason != PLCProgramExitReason.Completed || program.LastResult != 0)
        {
            RejectMeasurement($"PLC ended with {program.LastExitReason}, result {program.LastResult}.");
            return;
        }

        double contactZ = measuredToolContactZ.Value;
        if (!double.IsFinite(contactZ))
        {
            RejectMeasurement("PLC returned a non-finite contact position.");
            return;
        }

        if (measurement.ReferenceMeasurement)
        {
            ReferenceOffsetZ.Value = contactZ;
            ReferenceValid.Value = true;
            ReferenceOffsetZ.Flush();
            ReferenceValid.Flush();
            NTLog.Report($"MeasureToolLength: empty-spindle reference Z = {contactZ:0.######}.");
            PublishMeasurementStatus($"Empty-spindle Z reference measured: {contactZ:0.######}.");
            return;
        }

        double length = contactZ - ReferenceOffsetZ.Value;
        if (!double.IsFinite(length) || length <= 0.0)
        {
            RejectMeasurement($"Measured tool length must be positive; contact Z {contactZ:0.######}, reference Z {ReferenceOffsetZ.Value:0.######}.");
            return;
        }

        Tool tool = Tools[checked((int)measurement.ToolNumber - 1)];
        tool.SetMeasuredLength(length);
        synchronizeOffset = true;
        synchronizationGeneration = 0;
        synchronizationValue = [];
        synchronizationError = string.Empty;
        NTLog.Report($"MeasureToolLength: T{measurement.ToolNumber:00} Length = {length:0.######}.");
        PublishMeasurementStatus($"T{measurement.ToolNumber:00} length measured: {length:0.######}.");
    }

    private void CancelToolLength()
    {
        PendingMeasurement? measurement = pendingMeasurement;
        pendingMeasurement = null;
        if (measurement is null || !controller.IsConnected) return;
        PLCProgram program = measurement.Program;
        if (program.IsRunning || program.ActivationId == measurement.PreviousActivationId) program.Stop();
    }

    private void FailMeasurement(string reason)
    {
        CancelToolLength();
        RejectMeasurement(reason);
    }

    private void RejectMeasurement(string reason)
    {
        NTLog.Error(NTErrorCode.MotionCommandRejected, NTErrorSeverity.Warning, $"MeasureToolLength: {reason}");
        PublishMeasurementStatus($"Tool Z measurement rejected: {reason}");
    }

    private void PublishMeasurementStatus(string message)
        => MeasurementStatusChanged?.Invoke(message);

    private sealed class PendingMeasurement(
        PLCProgram program, long toolNumber, bool referenceMeasurement, long previousActivationId, long requestedAt)
    {
        internal PLCProgram Program { get; } = program;
        internal long ToolNumber { get; } = toolNumber;
        internal bool ReferenceMeasurement { get; } = referenceMeasurement;
        internal long PreviousActivationId { get; } = previousActivationId;
        internal long RequestedAt { get; } = requestedAt;
        internal long ActivationId { get; set; }
    }
}

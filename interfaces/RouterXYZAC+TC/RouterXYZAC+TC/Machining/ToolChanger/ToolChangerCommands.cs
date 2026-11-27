using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;

namespace RouterXYZACTC;

/// <summary>
/// Admission, runtime state machine and cancellation for tool exchange. The gate
/// serializes UI updates with connection/macro notifications. Every asynchronous
/// completion is tied to a path or PLC activation generation; elapsed time alone
/// never proves a physical exchange or an offset commit succeeded.
/// </summary>
internal sealed partial class ToolChangerLinks
{
    // Numeric modes are part of the shared ChangeTool contract.
    private const long GCodeChangeMode = 0;
    private const long ManualChangeMode = 1;
    private const long StageTimeoutMilliseconds = 5000;
    private static readonly string[] ExchangeProgramNames = ["WaitRollers", "StopSpindle", "WaitSpindle", "ReleaseTool", "ClampTool"];
    private readonly object gate = new();
    private readonly ControllerWrapper controller;
    private readonly Func<double> speedProvider;
    private readonly ToolPathWrapper workPath;
    private readonly ToolPathWrapper movePath;
    private AxisWrapper[] moveAxes => movePath.Axes;
    private readonly string[] moveAxisNames;
    private readonly int xSlot, ySlot, zSlot;
    private PendingChange? pending;
    private bool synchronizeOffset;
    private long synchronizationGeneration;
    private double[] synchronizationValue = [];
    private string synchronizationError = string.Empty;
    private bool disposed;

    internal bool IsActive { get { lock (gate) return synchronizeOffset || pending is not null || pendingM6 is not null || pendingMeasurement is not null; } }
    internal bool IsChangingTool { get { lock (gate) return pending is not null || pendingM6 is not null || pendingMeasurement is not null; } }

    private static bool ValidTool(long number) => number is >= 0 && number <= Initialize.ToolCount;
    private static bool ValidChangeMode(long mode) => mode is GCodeChangeMode or ManualChangeMode;

    private bool CanChangeTool(long newTool, long changeMode)
        => CanChangeTool(newTool, changeMode, out _);

    private bool CanChangeTool(long newTool, long changeMode, out string reason)
    {
        lock (gate)
        {
            bool appendM100 = changeMode == GCodeChangeMode && workPath.HasBlocksAvailableForRun;
            reason = string.Empty;
            if (disposed) reason = "Tool changer is disposed.";
            else if (synchronizeOffset) reason = synchronizationError.Length == 0
                ? "Initial tool offset is not acknowledged yet."
                : synchronizationError;
            else if (pending is not null || pendingM6 is not null || pendingMeasurement is not null)
                reason = "Another tool operation is pending.";
            else if (!ValidTool(newTool) || !ValidTool(CurrentTool.Value)) reason = "Invalid current/requested tool number.";
            else if (!IsToolEnabled(newTool)) reason = $"Tool {newTool} is disabled and is not available in the magazine.";
            else if (!ValidChangeMode(changeMode)) reason = "Tool-change mode must be 0 for G-code or 1 for manual operation.";
            else if (!controller.IsConnected) reason = "Controller is offline.";
            else if (!canStartHomedMotion()) reason = "All configured axes must be homed and no Home operation may be active.";
            else if (!AxisLayout.Matches(moveAxes, moveAxisNames)) reason = "TP2 axis configuration changed.";
            else if (!HasXyzacAxes(workPath.Axes))
                reason = "Tool compensation requires XYZAC axes on TP0.";
            if (reason.Length != 0) return false;
            if (newTool != CurrentTool.Value)
            {
                if (!SpindleExchangeAvailable(out reason)) return false;
                foreach (string name in ExchangeProgramNames)
                {
                    if (ReadyExchangeProgram(name) is not null) continue;
                    reason = $"{name} is missing, disabled, busy, or not loaded.";
                    return false;
                }
            }
            foreach (ToolPathWrapper path in controller.ToolPaths)
                if (!path.IsIdle)
                {
                    reason = $"TP{path.Index} is not idle.";
                    return false;
                }
            if (controller.PLC.Programs.TryGetValue("M100", out PLCProgram? handoff) && handoff is { IsRunning: true })
                reason = "M100 is already running.";
            else if (!movePath.IsReadyToMove) reason = "TP2 is not ready to move.";
            else if (!workPath.IsStopped) reason = "TP0 is not stopped.";
            if (reason.Length != 0) return false;
            if (!ChangeAxesAvailable(requireStopped: true, out reason)) return false;
            if (appendM100 && !workPath.IsReadyToStartOrResume)
                reason = "TP0 is not ready to resume.";
            else if (appendM100 && ReadyM100() is null)
                reason = "M100 is missing, disabled, or not loaded.";
            return reason.Length == 0;
        }
    }

    private PLCProgram? ReadyM100()
        => controller.PLC.Programs.TryGetValue("M100", out PLCProgram? program) &&
           program is { IsLoaded: true, Enabled: true, Cyclic: false, IsRunning: false, HasLoadError: false }
             ? program : null;

    private bool SpindleExchangeAvailable(out string reason)
    {
        reason = string.Empty;
        PLCProgram? background = controller.PLC.Programs.TryGetValue("Background", out PLCProgram? program) ? program : null;
        if (background is not { IsLoaded: true, Enabled: true, Cyclic: true, IsRunning: true, HasLoadError: false })
            reason = "The cyclic Background program must be running before a tool exchange.";
        else if (!((LOGIC)controller.PLC.ApplicationVariables["SpindleToolChangeConfigured"]).Value)
            reason = "The spindle run or tool release output is not configured.";
        else
        {
            foreach (string signal in new[] { "PressureSensor", "SpindleThermalOK", "SpindleDriveReady" })
                if (!SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings[signal]))
                {
                    reason = $"{signal} is not confirmed.";
                    return false;
                }
            if (CurrentTool.Value != 0 &&
                (!SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindleToolClamped"]) ||
                 !SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindleToolReleased"], false) ||
                 !SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindlePistonRetracted"])))
                reason = "Recorded CurrentTool disagrees with the spindle grip feedback; reconcile the physical tool before retrying.";
            else if (CurrentTool.Value == 0 && !SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindleToolClamped"], false))
                reason = "The spindle reports a clamped tool while CurrentTool is empty; reconcile the physical tool before retrying.";
        }
        return reason.Length == 0;
    }

    private PLCProgram? ReadyExchangeProgram(string name)
        => controller.PLC.Programs.TryGetValue(name, out PLCProgram? program) &&
           program is { IsLoaded: true, Enabled: true, Cyclic: false, IsRunning: false, HasLoadError: false }
            ? program : null;

    private double[] InMovePathOrder(PointD3 point, double a, double c)
    {
        double[] values = new double[moveAxes.Length];
        values[Array.FindIndex(moveAxes, axis => axis.Name == "A")] = a;
        values[Array.FindIndex(moveAxes, axis => axis.Name == "C")] = c;
        values[xSlot] = point.X;
        values[ySlot] = point.Y;
        values[zSlot] = point.Z;
        return values;
    }

    private double[] GetWorkOffset(long toolNumber)
    {
        PointD3 offset;
        if (toolNumber == 0)
        {
            offset = PointD3.Zero;
        }
        else
        {
            Tool tool = Tools[checked((int)toolNumber - 1)];
            PointD3 correction = tool.Offset;
            offset = new PointD3(correction.X, correction.Y, tool.Length + correction.Z);
        }
        if (!double.IsFinite(offset.X) || !double.IsFinite(offset.Y) || !double.IsFinite(offset.Z))
            throw new InvalidOperationException("Tool XYZ offset must be finite.");
        var values = new double[workPath.Axes.Length];
        for (int slot = 0; slot < values.Length; slot++)
            values[slot] = workPath.Axes[slot].Name switch
            {
                "X" => offset.X,
                "Y" => offset.Y,
                "Z" => offset.Z,
                "A" or "C" => 0.0,
                _ => throw new InvalidOperationException("Tool compensation requires XYZAC axes on TP0.")
            };
        return values;
    }

    private int FindWorkSlot(string axisName)
    {
        for (int slot = 0; slot < workPath.Axes.Length; slot++)
            if (workPath.Axes[slot].Name == axisName) return slot;
        throw new InvalidOperationException($"TP0 does not contain the {axisName} axis.");
    }

    private bool ChangeAxesAvailable(bool requireStopped)
        => ChangeAxesAvailable(requireStopped, out _);

    private bool ChangeAxesAvailable(bool requireStopped, out string reason)
    {
        reason = string.Empty;
        if (!movePath.AreAxesReady || (requireStopped && !movePath.AreAxesIdle))
        {
            reason = "TP2 motors are unavailable or have not stopped.";
            return false;
        }
        foreach (AxisWrapper axis in moveAxes)
        {
            AxisMask expected = axis.Mask;
            foreach (AxisWrapper member in controller.Axes)
                if (ReferenceEquals(member.CouplingMaster, axis)) expected |= member.Mask;
            bool coupled = expected != axis.Mask;
            if (axis.CouplingMaster is not null ||
                axis.CouplingRole != (coupled ? AxisCouplingRole.Master : AxisCouplingRole.Independent) ||
                (coupled && axis.CoupledAxes != expected))
            {
                reason = $"{axis.Name}: configured coupling is not established.";
                return false;
            }
            foreach (AxisWrapper member in controller.Axes)
            {
                if ((expected & member.Mask) == 0) continue;
                if (member.Owner != AxisOwner.Controller ||
                    !double.IsFinite(member.AxisTolerance) || member.AxisTolerance <= 0 || member.IsJogging || member.IsHoming ||
                    (member != axis && (member.CouplingRole != AxisCouplingRole.Slave ||
                        member.CouplingMasterIndex != axis.Index || member.CoupledAxes != expected)))
                {
                    reason = $"{member.Name}: axis unavailable (Ready={member.IsReadyToMove}, Fault={member.HasFault}, " +
                        $"CouplingFault={member.HasCouplingFault}, Owner={member.Owner}, Jogging={member.IsJogging}, Homing={member.IsHoming}).";
                    return false;
                }
            }
        }
        return true;
    }

    private void ValidateChangePoint(double[] point)
    {
        for (int slot = 0; slot < moveAxes.Length; slot++)
        {
            AxisWrapper axis = moveAxes[slot];
            foreach (AxisWrapper member in controller.Axes)
            {
                if (member != axis && !ReferenceEquals(member.CouplingMaster, axis)) continue;
                double value = point[slot] + (member == axis ? 0 : member.CouplingOffset);
                double pdo = (value - member.HomeOffset) / member.Step;
                if (!double.IsFinite(value) || !double.IsFinite(pdo) || pdo < int.MinValue || pdo > int.MaxValue ||
                    !double.IsFinite(member.MinCourse) || !double.IsFinite(member.MaxCourse) ||
                    member.MinCourse > member.MaxCourse || value < member.MinCourse || value > member.MaxCourse)
                    throw new InvalidOperationException($"Destination for {member.Name} is outside its valid travel/PDO range.");
            }
        }
    }

    private static void BeginOffset(PendingChange change, ChangePhase phase, double[] value)
    {
        change.Phase = phase;
        change.PendingOffset = value;
        change.OffsetGeneration = 0;
        change.OffsetStartedAt = Environment.TickCount64;
        NTLog.Report($"ChangeTool: {(phase == ChangePhase.ClearingToolOffset ? "clearing" : "applying")} tool offset.");
    }

    /// <summary>
    /// Advances at most the currently eligible stage from the form's UI timer.
    /// Initial compensation and measurement have priority over queued M6 work.
    /// This method observes completion; it never blocks waiting for the machine.
    /// </summary>
    internal void Update()
    {
        lock (gate)
        {
            if (synchronizeOffset)
            {
                UpdateInitialOffset();
                if (synchronizeOffset) return;
            }
            UpdateToolLength();
            if (pendingMeasurement is not null) return;
            UpdateM6();
            if (pending is not { } change) return;
            if (controller.IsConnected && movePath.CoordinateResetGeneration == change.MoveGeneration
                && change.Phase is ChangePhase.Placing or ChangePhase.Picking or ChangePhase.Returning)
                ReportReachedRouteBlocks(movePath.CurrentBlock, change.BlockDescriptions,
                    ref change.LastReportedBlock, NTLog.Report);
            if (toolChangeActive.Value && toolChangeFault.Value != 0)
            {
                string detail = toolChangeFault.Value switch
                {
                    2 => "Spindle mapping, air, thermal, cooling, or drive interlock was lost.",
                    3 => "Spindle stop was not confirmed or rotation resumed.",
                    4 => "S2 release feedback did not settle before the timeout.",
                    5 => "The confirmed S2 released state was lost or the clamp sequence did not start open.",
                    6 => "S1/S2/S5 clamp feedback was lost or did not settle before the timeout.",
                    7 => "Enabled roller free-position feedback timed out or was lost during tool exchange.",
                    _ => "Unexpected spindle interlock failure."
                };
                FailChange($"Spindle result {toolChangeFault.Value}: {detail}");
                return;
            }
            if (toolChangeActive.Value && !controller.PLC.Programs["Background"].IsRunning)
            {
                FailChange("The cyclic Background program stopped during tool exchange.");
                return;
            }
            if (!controller.IsConnected || controller.PLC.IsApplying ||
                !AxisLayout.Matches(moveAxes, moveAxisNames) || workPath.CoordinateResetGeneration != change.WorkGeneration)
            {
                FailChange("Tool-change operation was interrupted.");
                return;
            }

            switch (change.Phase)
            {
                case ChangePhase.Placing:
                case ChangePhase.Picking:
                case ChangePhase.Returning:
                    UpdateMotionStage(change);
                    break;
                case ChangePhase.ClearingToolOffset:
                case ChangePhase.ApplyingToolOffset:
                    UpdateOffsetStage(change);
                    break;
                case ChangePhase.Continuation:
                    UpdateContinuation(change);
                    break;
            }
        }
    }

    private void UpdateInitialOffset()
    {
        if (!controller.IsConnected || controller.PLC.IsApplying) return;
        if (synchronizationGeneration == 0)
        {
            try
            {
                synchronizationValue = GetWorkOffset(CurrentTool.Value);
            }
            catch (InvalidOperationException exception)
            {
                SetSynchronizationError($"Initial tool offset is invalid: {exception.Message}");
                return;
            }
            if (workPath.Offset.SequenceEqual(synchronizationValue))
            {
                synchronizeOffset = false;
                synchronizationError = string.Empty;
                return;
            }
            if (!workPath.TryApplyOffset(synchronizationValue, out synchronizationGeneration)) return;
        }

        ToolPathOffsetApplySnapshot snapshot = workPath.OffsetApplySnapshot;
        if (snapshot.CompletedGeneration < synchronizationGeneration) return;
        if (snapshot.CompletedGeneration == synchronizationGeneration && snapshot.Succeeded &&
            workPath.Offset.SequenceEqual(synchronizationValue))
        {
            double[] currentValue;
            try
            {
                currentValue = GetWorkOffset(CurrentTool.Value);
            }
            catch (InvalidOperationException exception)
            {
                SetSynchronizationError($"Initial tool offset is invalid: {exception.Message}");
                return;
            }
            if (!currentValue.SequenceEqual(synchronizationValue))
            {
                synchronizationGeneration = 0;
                synchronizationValue = [];
                synchronizationError = string.Empty;
                return;
            }
            synchronizeOffset = false;
            synchronizationGeneration = 0;
            synchronizationError = string.Empty;
            return;
        }

        try
        {
            double[] currentValue = GetWorkOffset(CurrentTool.Value);
            if (!currentValue.SequenceEqual(synchronizationValue))
            {
                synchronizationGeneration = 0;
                synchronizationValue = [];
                synchronizationError = string.Empty;
                return;
            }
        }
        catch (InvalidOperationException exception)
        {
            SetSynchronizationError($"Initial tool offset is invalid: {exception.Message}");
            return;
        }
        SetSynchronizationError($"NT-Core rejected initial tool offset generation {synchronizationGeneration} with {snapshot.Result}.");
    }

    private void SetSynchronizationError(string reason)
    {
        if (synchronizationError == reason) return;
        synchronizationError = reason;
        RejectChange(reason);
    }

    private void UpdateMotionStage(PendingChange change)
    {
        foreach (ProgramExpectation expectation in change.ActivePrograms)
        {
            PLCProgram program = expectation.Program;
            if (program.ActivationId != expectation.ActivationId && program.HasCompleted && !program.Succeeded)
            {
                FailChange($"{program.Name} ended with {program.LastExitReason}, result {program.LastResult}.");
                return;
            }
            if (!expectation.CompletionReported && program.ActivationId != expectation.ActivationId
                && program.Succeeded)
            {
                expectation.CompletionReported = true;
                NTLog.Report($"ChangeTool: {program.Name} succeeded, result {program.LastResult}.");
            }
        }

        if (movePath.CoordinateResetGeneration != change.MoveGeneration || movePath.IsStopping ||
            movePath.IsStopRequested || !ChangeAxesAvailable(requireStopped: false))
        {
            FailChange("Tool-change motion was interrupted.");
            return;
        }
        if (!movePath.IsIdle || !movePath.IsStopped || !ChangeAxesAvailable(requireStopped: true)) return;
        if (movePath.HasBlocksAvailableForRun || movePath.CurrentBlock != change.FinalBlock)
        {
            FailChange("TP2 stopped before the end of the current tool-change stage.");
            return;
        }

        if (change.ActivePrograms.Any(expectation =>
                expectation.Program.ActivationId == expectation.ActivationId ||
                !expectation.Program.HasCompleted || expectation.Program.IsRunning))
        {
            change.ProgramCompletionStartedAt = change.ProgramCompletionStartedAt == 0
                ? Environment.TickCount64
                : change.ProgramCompletionStartedAt;
            if (Environment.TickCount64 - change.ProgramCompletionStartedAt >= StageTimeoutMilliseconds)
                FailChange("A tool-change PLC program did not publish its completion state.");
            return;
        }

        if (change.Phase == ChangePhase.Placing)
        {
            if (!ConfirmToolFeedback(toolPresent: false)) return;
            if (!CommitCurrentTool(0)) return;
            NTLog.Report("ChangeTool: previous tool released; CurrentTool=0 recorded.");
            BeginOffset(change, ChangePhase.ClearingToolOffset, change.EmptyOffset);
        }
        else if (change.Phase == ChangePhase.Picking)
        {
            if (!ConfirmToolFeedback(toolPresent: true)) return;
            if (!CommitCurrentTool(change.NewTool)) return;
            NTLog.Report($"ChangeTool: T{change.NewTool:00} clamped; CurrentTool recorded.");
            BeginOffset(change, ChangePhase.ApplyingToolOffset, change.ToolOffset);
        }
        else
        {
            NTLog.Report("ChangeTool: return route completed.");
            StartContinuation(change);
        }
    }

    private void UpdateOffsetStage(PendingChange change)
    {
        if (!workPath.IsIdle || !workPath.IsStopped || !ChangeAxesAvailable(requireStopped: true))
        {
            if (Environment.TickCount64 - change.OffsetStartedAt >= StageTimeoutMilliseconds)
                FailChange("Axes did not remain stopped for tool-offset application.");
            return;
        }

        if (change.OffsetGeneration == 0)
        {
            if (!workPath.TryApplyOffset(change.PendingOffset, out long generation))
            {
                if (Environment.TickCount64 - change.OffsetStartedAt >= StageTimeoutMilliseconds)
                    FailChange("TP0 did not accept the tool-offset request.");
                return;
            }
            change.OffsetGeneration = generation;
            return;
        }

        ToolPathOffsetApplySnapshot snapshot = workPath.OffsetApplySnapshot;
        if (snapshot.CompletedGeneration < change.OffsetGeneration)
        {
            if (Environment.TickCount64 - change.OffsetStartedAt >= StageTimeoutMilliseconds)
                FailChange("NT-Core did not acknowledge the tool-offset request.");
            return;
        }
        if (snapshot.CompletedGeneration != change.OffsetGeneration || !snapshot.Succeeded)
        {
            FailChange($"NT-Core rejected tool offset generation {change.OffsetGeneration} with {snapshot.Result}.");
            return;
        }

        NTLog.Report("ChangeTool: tool offset acknowledged.");
        if (change.Phase == ChangePhase.ClearingToolOffset && change.PickRoute.Count != 0)
            StartMove(change, ChangePhase.Picking, change.PickRoute);
        else if (change.ReturnRoute.Count != 0)
            StartMove(change, ChangePhase.Returning, change.ReturnRoute);
        else
            StartContinuation(change);
    }

    private void StartContinuation(PendingChange change)
    {
        if (change.PreparedContinuation is { } prepared)
        {
            if (CurrentTool.Value != change.NewTool)
            {
                FailChange("The recorded tool changed during entry preparation.");
                return;
            }
            // A same-tool start has no ClampTool activation and therefore no exchange
            // latch after reconnect. Its live grip feedback must still be valid.
            if (change.NewTool > 0 && !ConfirmToolFeedback(toolPresent: true,
                    requireExchangeConfirmation: change.ToolExchanged)) return;
            pending = null;
            toolChangeActive.Value = false;
            try
            {
                if (!prepared()) RejectChange("Tool prepared, but entry positioning was rejected.");
                else NTLog.Report($"ChangeTool: T{change.NewTool:00} prepared; entry positioning accepted.");
            }
            catch (Exception error) when (error is NTException or ArgumentException or InvalidOperationException)
            {
                RejectChange($"Tool prepared, but entry positioning failed: {error.Message}");
            }
            return;
        }
        if (change.Continuation is null)
        {
            NTLog.Report($"ChangeTool: completed, CurrentTool=T{change.NewTool:00}.");
            pending = null;
            toolChangeActive.Value = false;
            return;
        }
        toolChangeActive.Value = false;
        NTLog.Report("ChangeTool: starting M100 to resume the work program.");
        change.ActivationId = change.Continuation.ActivationId;
        if (!change.Continuation.Run())
        {
            FailChange("M100 Run was rejected after the tool offset was acknowledged.");
            return;
        }
        change.Phase = ChangePhase.Continuation;
        change.ContinuationStartedAt = Environment.TickCount64;
    }

    private void UpdateContinuation(PendingChange change)
    {
        PLCProgram program = change.Continuation!;
        if (program.ActivationId == change.ActivationId)
        {
            if (Environment.TickCount64 - change.ContinuationStartedAt >= StageTimeoutMilliseconds)
                FailChange("M100 was not activated after the tool change completed.");
            return;
        }
        if (!program.HasCompleted || program.IsRunning) return;
        pending = null;
        if (!program.Succeeded)
            RejectChange($"M100 ended with {program.LastExitReason}, result {program.LastResult}.");
        else
            NTLog.Report($"ChangeTool: M100 succeeded; completed, CurrentTool=T{change.NewTool:00}.");
    }

    private bool ConfirmToolFeedback(bool toolPresent, bool requireExchangeConfirmation = true)
    {
        // Absent sensors omit only their level check, never the PLC timed confirmation.
        bool clamped = SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindleToolClamped"], toolPresent);
        bool released = SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindleToolReleased"], !toolPresent);
        bool confirmed = toolPresent
            ? (!requireExchangeConfirmation || ((LOGIC)controller.PLC.SharedVariables["SpindleClampConfirmed"]).Value)
                && clamped && released && SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindlePistonRetracted"])
            : ((LOGIC)controller.PLC.SharedVariables["SpindleReleaseConfirmed"]).Value && released && clamped;
        if (!confirmed)
            FailChange("Tool feedback changed before the CurrentTool commit; reconcile the physical tool before retrying.");
        return confirmed;
    }

    private bool CommitCurrentTool(long toolNumber)
    {
        try
        {
            CurrentTool.Value = toolNumber;
        }
        catch (InvalidOperationException exception)
        {
            FailChange($"Could not record physical tool state {toolNumber}: {exception.Message}");
            return false;
        }

        try
        {
            // ReleaseTool/ClampTool are the physical-state commit points. Persist immediately
            // so an application or machine restart cannot restore the pre-exchange
            // tool number after the spindle state has already changed.
            CurrentTool.Flush();
            return true;
        }
        catch (Exception exception) when (exception is IOException or
            UnauthorizedAccessException or ObjectDisposedException or System.Security.SecurityException)
        {
            // Keep the new in-memory value because it reflects the physical spindle,
            // but halt the route at the holder until persistence is repaired.
            FailChange($"Could not persist physical tool state {toolNumber}: {exception.Message}");
            return false;
        }
    }

    private void FailChange(string reason)
    {
        Cancel(reportCancellation: false);
        RejectChange(reason);
    }

    internal void Cancel(bool reportCancellation = true)
    {
        lock (gate)
        {
            pendingM6 = null;
            toolChangeRollersConfirmed.Value = false;
            toolChangeActive.Value = false;
            controller.MotionController.Macros.StopProgram("M6");
            CancelToolLength();
            PendingChange? change = pending;
            pending = null;
            if (change is not null && reportCancellation)
                NTLog.Report($"ChangeTool: cancelled during {change.Phase}.");
            if (change is null || !controller.IsConnected) return;
            if (!movePath.IsIdle) controller.StopToolPath(movePath);
            foreach (ProgramExpectation expectation in change.ActivePrograms)
                if (expectation.Program.ActivationId != expectation.ActivationId && expectation.Program.IsRunning)
                    expectation.Program.Stop();
            change.Continuation?.Stop();
        }
    }

    private void OnConnectionChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        lock (gate)
        {
            if (e.State == ConnectionState.Online)
            {
                synchronizeOffset = true;
                synchronizationGeneration = 0;
                synchronizationValue = [];
                synchronizationError = string.Empty;
            }
            else
            {
                Cancel();
                synchronizeOffset = false;
                synchronizationGeneration = 0;
                synchronizationValue = [];
                synchronizationError = string.Empty;
            }
        }
    }

    private static bool RejectChange(string reason)
    {
        NTLog.Error(NTErrorCode.MotionCommandRejected, NTErrorSeverity.Warning, $"ChangeTool: {reason}");
        return false;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            Cancel();
            controller.ConnectionStateChanged -= OnConnectionChanged;
            CurrentTool.Changed -= CurrentToolChanged;
            ChangeToolLink.Dispose();
            MeasureToolOffsetLink.Dispose();
            NewTool.Dispose();
        }
    }

    private enum ChangePhase
    {
        Placing,
        ClearingToolOffset,
        Picking,
        ApplyingToolOffset,
        Returning,
        Continuation
    }

    private abstract record RouteBlock(string? Description);
    private sealed record RoutePoint(double[] Point, double Velocity, string? Description = null) : RouteBlock(Description);
    private sealed record RouteProgram(string Name, bool Wait, string? Description = null) : RouteBlock(Description);
    private sealed record ProgramExpectation(PLCProgram Program, long ActivationId)
    {
        internal bool CompletionReported { get; set; }
    }

    internal static void ReportReachedRouteBlocks(int currentBlock, IReadOnlyList<string?> descriptions,
        ref int lastReportedBlock, Action<string> report)
    {
        if (currentBlock <= lastReportedBlock || currentBlock >= descriptions.Count) return;
        // UI ticks can coalesce several sequential block edges. Report every
        // reached annotation without consuming ToolPath.BlockChanged again.
        while (lastReportedBlock < currentBlock)
        {
            int block = ++lastReportedBlock;
            if (descriptions[block] is { } description)
                report($"ChangeTool: TP2 block {block}: {description}");
        }
    }

    /// <summary>
    /// One captured operation, including immutable route/offset inputs and mutable
    /// acknowledgement cursors. Keep this state together so Cancel can revoke the
    /// continuation without reconstructing intent from live settings or UI controls.
    /// </summary>
    private sealed class PendingChange(
        long workGeneration,
        long newTool,
        List<RouteBlock> pickRoute,
        List<RouteBlock> returnRoute,
        double[] emptyOffset,
        double[] toolOffset,
        PLCProgram? continuation)
    {
        internal long WorkGeneration { get; } = workGeneration;
        internal long NewTool { get; } = newTool;
        internal List<RouteBlock> PickRoute { get; } = pickRoute;
        internal List<RouteBlock> ReturnRoute { get; } = returnRoute;
        internal double[] EmptyOffset { get; } = emptyOffset;
        internal double[] ToolOffset { get; } = toolOffset;
        internal PLCProgram? Continuation { get; } = continuation;
        internal Func<bool>? PreparedContinuation { get; init; }
        internal bool ToolExchanged { get; init; }
        internal ChangePhase Phase { get; set; }
        internal long MoveGeneration { get; set; }
        internal int FinalBlock { get; set; }
        internal List<ProgramExpectation> ActivePrograms { get; set; } = [];
        internal string?[] BlockDescriptions { get; set; } = [];
        internal int LastReportedBlock;
        internal long ProgramCompletionStartedAt { get; set; }
        internal double[] PendingOffset { get; set; } = [];
        internal long OffsetGeneration { get; set; }
        internal long OffsetStartedAt { get; set; }
        internal long ActivationId { get; set; }
        internal long ContinuationStartedAt { get; set; }
    }
}

using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;

namespace RouterXYZBCTC;

internal enum HeadPositioningTarget { ReturnToWork, GoToReference, GoHome, GoToPark }

internal readonly record struct PositioningSettings(double SafeZ, double ApproachDistance, double ApproachVelocity)
{
    // Clearance is a floor for machine-space traverse. Preserve a higher current Z.
    internal double GetTraverseHeight(double currentZ, double minimum, double maximum)
    {
        double clearance = SafeZ != 0 && SafeZ >= minimum && SafeZ <= maximum ? SafeZ : maximum;
        return Math.Max(currentZ, clearance);
    }

    internal double GetApproachStartZ(double fromZ, double toZ)
        => ApproachDistance < Math.Abs(fromZ - toZ) ? toZ + Math.CopySign(ApproachDistance, fromZ - toZ) : fromZ;

    internal double GetApproachVelocity(double traverseVelocity) => Math.Min(ApproachVelocity, traverseVelocity);
}

/// <summary>
/// Builds stopped XYZBC safe-Z legs on TP1, retaining angular endpoints without RTCP.
/// The non-waiting M100 PLC
/// program owns the TP1-to-TP0 handoff; this class only monitors its result.
/// </summary>
internal sealed class HeadPositioningCommands : IDisposable
{
    private const long CompletionProgramTimeoutMilliseconds = 5000;
    private readonly object gate = new();
    private readonly ControllerWrapper controller;
    private readonly ToolPathWrapper workPath;
    private readonly ToolPathWrapper movePath;
    private AxisWrapper[] moveAxes => movePath.Axes;
    private readonly string[] moveAxisNames;
    private AxisWrapper[] workAxes => workPath.Axes;
    private readonly string[] workAxisNames;
    private readonly int[] workSlots;
    private readonly int zSlot;
    private readonly Func<double> speedProvider;
    private readonly Func<PositioningSettings> positioningSettingsProvider;
    private readonly Func<string, double> parkPositionProvider;
    private readonly Func<bool> canStartHomedMotion;
    private PendingMove? pending;
    private bool disposed;

    internal HeadPositioningCommands(
        ControllerWrapper controller,
        Func<double> speedProvider,
        Func<PositioningSettings> positioningSettingsProvider,
        Func<bool> canStartHomedMotion,
        Func<string, double> parkPositionProvider)
    {
        this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
        this.speedProvider = speedProvider ?? throw new ArgumentNullException(nameof(speedProvider));
        this.positioningSettingsProvider = positioningSettingsProvider ?? throw new ArgumentNullException(nameof(positioningSettingsProvider));
        this.parkPositionProvider = parkPositionProvider ?? throw new ArgumentNullException(nameof(parkPositionProvider));
        this.canStartHomedMotion = canStartHomedMotion ?? throw new ArgumentNullException(nameof(canStartHomedMotion));
        if (controller.ToolPaths.Length < 2)
            throw new ArgumentException("Head positioning requires TP0 and TP1.", nameof(controller));
        workPath = controller.ToolPaths[0];
        movePath = controller.ToolPaths[1];
        moveAxisNames = movePath.Axes.Select(axis => axis.Name).ToArray();
        workAxisNames = workPath.Axes.Select(axis => axis.Name).ToArray();
        if (moveAxes.Length != 5 || workAxes.Length != 5 ||
            !moveAxes.Select(axis => axis.Name).Order().SequenceEqual(new[] { "B", "C", "X", "Y", "Z" }) ||
            !workAxes.Select(axis => axis.Name).Order().SequenceEqual(new[] { "B", "C", "X", "Y", "Z" }))
            throw new ArgumentException("Head positioning requires the same XYZBC axes on TP0 and TP1.", nameof(controller));
        workSlots = moveAxes.Select(axis => Array.IndexOf(workAxes, axis)).ToArray();
        if (workSlots.Any(slot => slot < 0))
            throw new ArgumentException("TP0 and TP1 must use the same controller axes.", nameof(controller));
        zSlot = Array.FindIndex(moveAxes, axis => axis.Name == "Z");

        ReturnToWorkLink = CreateCommand(HeadPositioningTarget.ReturnToWork, ReturnToWork);
        GoToReferenceLink = CreateCommand(HeadPositioningTarget.GoToReference, GoToReference);
        GoHomeLink = CreateCommand(HeadPositioningTarget.GoHome, GoHome);
        GoToParkLink = CreateCommand(HeadPositioningTarget.GoToPark, GoToPark);
        controller.ToolPathPositionMismatch += OnPositionMismatch;
        controller.ConnectionStateChanged += OnConnectionChanged;
    }

    internal CommandLink ReturnToWorkLink { get; }
    internal CommandLink GoToReferenceLink { get; }
    internal CommandLink GoHomeLink { get; }
    internal CommandLink GoToParkLink { get; }
    internal bool IsActive { get { lock (gate) return pending is not null; } }
    internal bool IsReturningToWork
    {
        get
        {
            lock (gate)
                return pending is { Target: HeadPositioningTarget.ReturnToWork } move
                    && move.WorkGeneration == workPath.CoordinateResetGeneration;
        }
    }

    internal bool ReturnToWork() => TryExecute(HeadPositioningTarget.ReturnToWork);
    internal bool GoToReference() => TryExecute(HeadPositioningTarget.GoToReference);
    internal bool GoHome() => TryExecute(HeadPositioningTarget.GoHome);
    internal bool GoToPark() => TryExecute(HeadPositioningTarget.GoToPark);

    private CommandLink CreateCommand(HeadPositioningTarget target, Func<bool> execute)
    {
        string name = target.ToString();
        CommandLink link = CommandLink.CreateCommand(name, DisplayNames.FromIdentifier(name));
        link.QueryEnabled += (_, e) => e.Enabled &= CanExecute(target);
        link.ActionExecute += (_, _) => execute();
        return link;
    }

    internal bool CanExecute(HeadPositioningTarget target)
    {
        lock (gate)
            return Enum.IsDefined(target) && !disposed && pending is null && controller.IsConnected &&
                canStartHomedMotion() && LayoutMatches() &&
                controller.ToolPaths.All(path => path.IsIdle) && movePath.IsReadyToMove &&
                !(controller.PLC.Programs.TryGetValue("M100", out PLCProgram? handoff) && handoff is { IsRunning: true }) &&
                AxesAvailable(requireStopped: true) &&
                !IsAtDestination(target) &&
                (target != HeadPositioningTarget.ReturnToWork ||
                    (workPath.IsReadyToStartOrResume && GetReadyM100() is not null));
    }

    private bool IsAtDestination(HeadPositioningTarget target)
    {
        if (target == HeadPositioningTarget.ReturnToWork)
            return false;

        ReadOnlySpan<double> reference = workPath.Reference;
        ReadOnlySpan<double> offset = workPath.Offset;
        ReadOnlySpan<double> liveOffset = workPath.LiveOffset;
        for (int i = 0; i < moveAxes.Length; i++)
        {
            double destination = 0.0;
            if (target == HeadPositioningTarget.GoToPark)
                destination = parkPositionProvider(moveAxes[i].Name);
            if (target == HeadPositioningTarget.GoToReference)
            {
                int workSlot = workSlots[i];
                destination = reference[workSlot] + offset[workSlot] + liveOffset[workSlot];
            }

            double tolerance = moveAxes[i].AxisTolerance;
            double current = moveAxes[i].CommandPosition;
            if (!double.IsFinite(destination) || !double.IsFinite(current) ||
                !double.IsFinite(tolerance) || tolerance <= 0.0 ||
                Math.Abs(destination - current) >= tolerance)
                return false;
        }
        return true;
    }

    private PLCProgram? GetReadyM100()
        => controller.PLC.Programs.TryGetValue("M100", out PLCProgram? program) &&
           program is { IsLoaded: true, Enabled: true, Cyclic: false, IsRunning: false, HasLoadError: false }
            ? program : null;

    private bool TryExecute(HeadPositioningTarget target)
    {
        lock (gate)
        {
            NTLog.Report($"Positioning requested: {target}; TP0 SourceLine={workPath.CurrentBlock}");
            if (!Enum.IsDefined(target) || !CanExecute(target))
                return Reject($"{target}: axes or ToolPaths are busy, unavailable, or no work remains.");
            try
            {
                double speed = speedProvider();
                if (!double.IsFinite(speed) || speed <= 0)
                    return Reject($"{target}: traverse velocity must be finite and positive.");
                double[] start = moveAxes.Select(axis => axis.CommandPosition).ToArray();
                double[] destination = ResolveDestination(target);

                ValidatePoint(start, "Current position");
                ValidatePoint(destination, "Destination");
                var points = BuildNormalRoute(target, start, destination, speed, positioningSettingsProvider());
                foreach (var (point, _) in points)
                {
                    ValidatePoint(point, "Positioning waypoint");
                    for (int i = 0; i < point.Length; i++)
                    {
                        point[i] -= start[i];
                        if (!double.IsFinite(point[i]))
                            throw new InvalidOperationException("Positioning displacement is not finite.");
                    }
                }

                // All validation precedes buffer mutation. TP1's local zero is
                // the current commanded position, never machine zero or TP0's reference.
                long workGeneration = workPath.CoordinateResetGeneration;
                PLCProgram? continuation = target == HeadPositioningTarget.ReturnToWork ? GetReadyM100() : null;
                if (target == HeadPositioningTarget.ReturnToWork && continuation is null)
                    return Reject("ReturnToWork: the M100 PLC program is not available.");
                movePath.Begin(points.Count + 1);
                movePath.Reference = start;
                for (int i = 0; i < moveAxes.Length; i++) movePath.SetLiveOffset(i, 0);
                int block = 0;
                double[] previous = new double[moveAxes.Length];
                foreach (var (point, velocity) in points)
                {
                    AxisLayout.AddStoppedLine(movePath, ++block, previous, point, velocity);
                    previous = point;
                }
                if (target == HeadPositioningTarget.ReturnToWork &&
                    !movePath.AddProgram(++block, ToolPath.ProgramType.PLC, "M100", stop: false))
                    throw new InvalidOperationException("Could not append M100 to TP1.");
                movePath.End();
                pending = new PendingMove(target, workGeneration,
                    movePath.CoordinateResetGeneration, block, continuation);
                if (controller.RunToolPath(movePath))
                {
                    NTLog.Report($"Positioning accepted: {target}; " +
                        string.Join("; ", moveAxes.Select((axis, slot) => FormattableString.Invariant($"{axis.Name}: {start[slot]:G17} -> {destination[slot]:G17}"))) +
                        FormattableString.Invariant($"; Speed={speed:G17} mm/s"));
                    return true;
                }
                pending = null;
                return Reject($"{target}: TP1 Run was rejected.");
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                pending = null;
                return Reject($"{target}: {exception.Message}");
            }
        }
    }

    internal List<(double[] Point, double Velocity)> BuildNormalRoute(HeadPositioningTarget target,
        double[] start, double[] destination, double speed, PositioningSettings settings)
    {
        bool traverse = false;
        for (int i = 0; i < moveAxes.Length; i++)
            if (i != zSlot && Math.Abs(destination[i] - start[i]) >= moveAxes[i].AxisTolerance)
                traverse = true;

        var points = new List<(double[] Point, double Velocity)>(4);
        double[] final = start.ToArray();
        double fromZ = start[zSlot];
        double toZ = destination[zSlot];
        if (traverse)
        {
            AxisWrapper z = moveAxes[zSlot];
            fromZ = settings.GetTraverseHeight(fromZ, z.MinCourse, z.MaxCourse);
            if (fromZ > start[zSlot])
            {
                double[] lift = start.ToArray();
                lift[zSlot] = fromZ;
                points.Add((lift, speed));
            }
            double[] across = destination.ToArray();
            across[zSlot] = fromZ;
            points.Add((across, speed));
            final = destination.ToArray();
        }

        if (target == HeadPositioningTarget.ReturnToWork && settings.ApproachDistance > 0 && fromZ != toZ)
        {
            // Split the final Z leg within its existing endpoints. A short leg
            // begins at approach speed without adding a move away from the target.
            double approachStart = settings.GetApproachStartZ(fromZ, toZ);
            if (approachStart != fromZ && approachStart != toZ)
            {
                double[] approach = final.ToArray();
                approach[zSlot] = approachStart;
                points.Add((approach, speed));
            }
            speed = settings.GetApproachVelocity(speed);
        }
        // The last tracked block must be a real move unless a program handoff follows it.
        if (fromZ != toZ || points.Count == 0)
        {
            final[zSlot] = toZ;
            points.Add((final, speed));
        }
        return points;
    }

    private double[] ResolveDestination(HeadPositioningTarget target)
    {
        var destination = new double[moveAxes.Length];
        if (target == HeadPositioningTarget.GoHome)
            return destination;
        if (target == HeadPositioningTarget.GoToPark)
        {
            // Capture settings once; subsequent edits cannot redirect the queued path.
            for (int i = 0; i < destination.Length; i++)
                destination[i] = parkPositionProvider(moveAxes[i].Name);
            return destination;
        }

        ReadOnlySpan<double> primary = target == HeadPositioningTarget.ReturnToWork
            ? workPath.LastPosition : workPath.Reference;
        ReadOnlySpan<double> offset = workPath.Offset;
        ReadOnlySpan<double> liveOffset = workPath.LiveOffset;
        for (int i = 0; i < destination.Length; i++)
        {
            int workSlot = workSlots[i];
            destination[i] = primary[workSlot];
            if (target == HeadPositioningTarget.GoToReference)
                destination[i] += offset[workSlot] + liveOffset[workSlot];
        }
        return destination;
    }

    /// <summary>Called by the existing UI monitor; never waits or runs on the RT cycle.</summary>
    internal void Update()
    {
        lock (gate)
        {
            if (pending is not { } move) return;
            // PLC owns the handoff once M100 is activated. In particular, its
            // own TP1 Stop and TP0 Run are not cancellations or UI commands.
            if (move.Continuation is { } program && program.ActivationId != move.ActivationId)
            {
                if (program.HasCompleted && !program.IsRunning)
                {
                    pending = null;
                    if (!program.Succeeded)
                    {
                        string reason = $"M100: PLC ended with {program.LastExitReason}, result {program.LastResult}.";
                        if (program.LastExitReason == PLCProgramExitReason.Completed && program.LastResult == 3)
                            reason += DescribeObservedAlignment(program.ActivationId);
                        Reject(reason);
                    }
                    else NTLog.Report("ReturnToWork completed; M100 successfully handed execution to TP0.");
                }
                return;
            }
            if (!controller.IsConnected || controller.PLC.IsApplying ||
                !LayoutMatches() || !workPath.IsIdle ||
                movePath.IsStopping || movePath.IsStopRequested ||
                movePath.CoordinateResetGeneration != move.MoveGeneration ||
                workPath.CoordinateResetGeneration != move.WorkGeneration ||
                !AxesAvailable(requireStopped: false))
            {
                CancelCore("Positioning continuation cancelled: motion state, path, or axis availability changed.");
                return;
            }
            if (!movePath.IsIdle || !movePath.IsStopped || !AxesAvailable(requireStopped: true)) return;
            if (movePath.HasBlocksAvailableForRun || movePath.CurrentBlock != move.FinalBlock)
            {
                CancelCore("Positioning stopped before the end; TP0 will not start.");
                return;
            }
            if (move.Target != HeadPositioningTarget.ReturnToWork)
            {
                NTLog.Report($"Positioning completed: {move.Target}");
                pending = null;
                return;
            }
            move.CompletedAt ??= Environment.TickCount64;
            if (Environment.TickCount64 - move.CompletedAt.Value >= CompletionProgramTimeoutMilliseconds)
                CancelCore("M100 PLC was not activated after TP1 completion.");
        }
    }

    private string DescribeObservedAlignment(long activationId)
    {
        // Diagnostic snapshot only, after PLC completion. Do not imply that a
        // later UI observation is an atomic capture of the rejection scan.
        AxisWrapper[] axes = workPath.Axes.ToArray();
        double[] last = workPath.LastPosition.ToArray();
        double[] command = workPath.CommandPosition.ToArray();
        double[] tolerances = axes.Select(axis => axis.AxisTolerance).ToArray();
        double commonTolerance = tolerances.Length == 0 ? double.NaN : tolerances.Min();
        var details = new List<string>(axes.Length);
        for (int slot = 0; slot < Math.Min(axes.Length, Math.Min(last.Length, command.Length)); slot++)
        {
            double tolerance = tolerances[slot];
            double delta = Math.Abs(command[slot] - last[slot]);
            bool aligned = double.IsFinite(last[slot]) && double.IsFinite(command[slot]) &&
                double.IsFinite(tolerance) && tolerance > 0 && delta <= tolerance;
            details.Add(FormattableString.Invariant(
                $"{axes[slot].Name}[{slot}]: CommandPosition={command[slot]:G17}, LastPosition={last[slot]:G17}, AbsDelta={delta:G17}, Tolerance={tolerance:G17}, Aligned={aligned}"));
        }
        return FormattableString.Invariant($" Activation={activationId}; observed after PLC completion (not a rejection-scan capture); M100CommonTolerance={commonTolerance:G17}; per-axis checks: ") +
            string.Join("; ", details);
    }

    private void OnPositionMismatch(object? sender, ToolPathPositionMismatchEventArgs e)
    {
        if (e.ToolPathIndex != workPath.Index && e.ToolPathIndex != movePath.Index) return;
        e.ContinueRun = false;
        lock (gate)
        {
            if (!disposed && e.ToolPathIndex == workPath.Index)
                ReturnToWork();
        }
    }

    private void OnConnectionChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (e.State != ConnectionState.Online) CancelContinuation();
    }

    internal void CancelContinuation()
    {
        lock (gate)
        {
            PendingMove? move = pending;
            pending = null;
            if (move is not null) NTLog.Report($"Positioning cancelled: {move.Target}; TP0 SourceLine={workPath.CurrentBlock}");
            if (move?.Continuation is { } program && controller.IsConnected)
            {
                // Prevent an unreached M100 from starting, and stop an activation
                // already waiting for TP1 release. Operator Stop also stops TP0.
                if (!movePath.IsIdle) controller.StopToolPath(movePath);
                program.Stop();
            }
        }
    }

    private void CancelCore(string reason)
    {
        CancelContinuation();
        NTLog.Error(NTErrorCode.MotionCommandRejected, NTErrorSeverity.Warning, reason);
    }

    private static bool Reject(string reason)
    {
        NTLog.Error(NTErrorCode.MotionCommandRejected, NTErrorSeverity.Warning, reason);
        return false;
    }

    private bool LayoutMatches()
        => AxisLayout.Matches(moveAxes, moveAxisNames) && AxisLayout.Matches(workAxes, workAxisNames);

    private bool AxesAvailable(bool requireStopped)
    {
        foreach (AxisWrapper axis in moveAxes)
        {
            AxisMask expected = axis.Mask;
            foreach (AxisWrapper member in controller.Axes)
                if (ReferenceEquals(member.CouplingMaster, axis)) expected |= member.Mask;
            bool coupled = expected != axis.Mask;
            if (axis.CouplingMaster is not null ||
                axis.CouplingRole != (coupled ? AxisCouplingRole.Master : AxisCouplingRole.Independent) ||
                (coupled && axis.CoupledAxes != expected)) return false;
            foreach (AxisWrapper member in controller.Axes)
            {
                if ((expected & member.Mask) == 0) continue;
                if (!member.IsReadyToMove || member.HasFault || member.HasCouplingFault || member.Owner != AxisOwner.Controller ||
                    !double.IsFinite(member.AxisTolerance) || member.AxisTolerance <= 0 ||
                    member.IsJogging || member.IsHoming ||
                    (member != axis && (member.CouplingRole != AxisCouplingRole.Slave ||
                        member.CouplingMasterIndex != axis.Index || member.CoupledAxes != expected))) return false;
                // Axis Stopping also represents the normal smoothing tail after
                // TP1 completes. Wait for it; only TP1.Stopping cancels a return.
                if (requireStopped && (!member.IsStopped || member.IsMoving || member.IsRunning || member.IsStopping)) return false;
            }
        }
        return true;
    }

    /// <summary>Rejects unsafe endpoints and intermediate legs before changing the TP1 buffer, including coupled motors.</summary>
    private void ValidatePoint(double[] point, string context)
    {
        for (int i = 0; i < moveAxes.Length; i++)
        {
            AxisWrapper axis = moveAxes[i];
            foreach (AxisWrapper member in controller.Axes)
            {
                if (member != axis && !ReferenceEquals(member.CouplingMaster, axis)) continue;
                double value = point[i] + (member == axis ? 0 : member.CouplingOffset);
                double pdo = (value - member.HomeOffset) / member.Step;
                if (!double.IsFinite(value) ||
                    !double.IsFinite(member.MinCourse) || !double.IsFinite(member.MaxCourse) ||
                    member.MinCourse > member.MaxCourse || value < member.MinCourse || value > member.MaxCourse)
                    throw new InvalidOperationException(FormattableString.Invariant(
                        $"{context}: axis {member.Name}, requested position {value:G17} mm is outside valid travel [{member.MinCourse:G17}, {member.MaxCourse:G17}] mm. Motion was not started."));
                if (!double.IsFinite(pdo) || pdo < int.MinValue || pdo > int.MaxValue)
                    throw new InvalidOperationException(FormattableString.Invariant(
                        $"{context}: axis {member.Name}, requested position {value:G17} mm cannot be represented by the drive position range (Step={member.Step:G17}, HomeOffset={member.HomeOffset:G17}). Motion was not started."));
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            CancelContinuation();
            controller.ToolPathPositionMismatch -= OnPositionMismatch;
            controller.ConnectionStateChanged -= OnConnectionChanged;
            ReturnToWorkLink.Dispose();
            GoToReferenceLink.Dispose();
            GoHomeLink.Dispose();
            GoToParkLink.Dispose();
        }
    }

    private sealed class PendingMove(HeadPositioningTarget target,
        long workGeneration, long moveGeneration, int finalBlock, PLCProgram? continuation)
    {
        internal HeadPositioningTarget Target { get; } = target;
        internal long WorkGeneration { get; } = workGeneration;
        internal long MoveGeneration { get; } = moveGeneration;
        internal int FinalBlock { get; } = finalBlock;
        internal PLCProgram? Continuation { get; } = continuation;
        internal long ActivationId { get; } = continuation?.ActivationId ?? 0;
        internal long? CompletedAt { get; set; }
    }
}

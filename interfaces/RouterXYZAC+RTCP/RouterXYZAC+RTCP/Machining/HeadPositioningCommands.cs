using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;

namespace RouterXYZACRTCP;

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
/// Positions the head before program entry, using machine-space clearance legs.
/// Normal positioning uses TP1. RTCP positioning uses machine-space PLC point
/// moves. Only a return to the retained work position may hand off to TP0.
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
    private readonly Func<bool> canStartProgramMotion;
    private readonly Func<bool> canRunInCurrentCoordinateMode;
    private readonly Func<double> axialReturnDistance;
    private readonly Func<double> axialReturnVelocity;
    private PendingMove? pending;
    private PendingRtcpMove? pendingRtcp;
    private bool disposed;

    internal HeadPositioningCommands(
        ControllerWrapper controller,
        Func<double> speedProvider,
        Func<PositioningSettings> positioningSettingsProvider,
        Func<bool> canStartHomedMotion,
        Func<string, double> parkPositionProvider,
        Func<bool> canStartProgramMotion,
        Func<bool> canRunInCurrentCoordinateMode,
        Func<double> axialReturnDistance,
        Func<double> axialReturnVelocity)
    {
        this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
        this.speedProvider = speedProvider ?? throw new ArgumentNullException(nameof(speedProvider));
        this.positioningSettingsProvider = positioningSettingsProvider ?? throw new ArgumentNullException(nameof(positioningSettingsProvider));
        this.parkPositionProvider = parkPositionProvider ?? throw new ArgumentNullException(nameof(parkPositionProvider));
        this.canStartHomedMotion = canStartHomedMotion ?? throw new ArgumentNullException(nameof(canStartHomedMotion));
        this.canStartProgramMotion = canStartProgramMotion ?? throw new ArgumentNullException(nameof(canStartProgramMotion));
        this.canRunInCurrentCoordinateMode = canRunInCurrentCoordinateMode ?? throw new ArgumentNullException(nameof(canRunInCurrentCoordinateMode));
        this.axialReturnDistance = axialReturnDistance ?? throw new ArgumentNullException(nameof(axialReturnDistance));
        this.axialReturnVelocity = axialReturnVelocity ?? throw new ArgumentNullException(nameof(axialReturnVelocity));
        if (controller.ToolPaths.Length < 2)
            throw new ArgumentException("Head positioning requires TP0 and TP1.", nameof(controller));
        workPath = controller.ToolPaths[0];
        movePath = controller.ToolPaths[1];
        moveAxisNames = movePath.Axes.Select(axis => axis.Name).ToArray();
        workAxisNames = workPath.Axes.Select(axis => axis.Name).ToArray();
        if (moveAxes.Length != 5 || workAxes.Length != 5 ||
            !moveAxes.Select(axis => axis.Name).Order().SequenceEqual(new[] { "A", "C", "X", "Y", "Z" }) ||
            !workAxes.Select(axis => axis.Name).Order().SequenceEqual(new[] { "A", "C", "X", "Y", "Z" }))
            throw new ArgumentException("Head positioning requires the same XYZAC axes on TP0 and TP1.", nameof(controller));
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
    internal bool IsActive { get { lock (gate) return pending is not null || pendingRtcp is not null; } }
    internal bool IsReturningToWork
    {
        get
        {
            lock (gate)
                return pendingRtcp is { Target: HeadPositioningTarget.ReturnToWork } rtcpMove
                    && rtcpMove.WorkGeneration == workPath.CoordinateResetGeneration ||
                    pending is { Target: HeadPositioningTarget.ReturnToWork } move
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
        {
            if (!Enum.IsDefined(target) || disposed || pending is not null || pendingRtcp is not null ||
                !controller.IsConnected || !LayoutMatches() || !controller.ToolPaths.All(path => path.IsIdle) ||
                !AxesAvailable(requireStopped: true))
                return false;

            var rtcp = controller.MotionController.RTCP;
            bool returning = target == HeadPositioningTarget.ReturnToWork;
            if (rtcp.Mode == RtcpMode.RTCP)
                return canStartProgramMotion() && !workPath.IsPausedForProgram &&
                    (!returning || (workPath.IsReadyToStartOrResume && canRunInCurrentCoordinateMode())) &&
                    ((target is HeadPositioningTarget.GoToReference or HeadPositioningTarget.ReturnToWork) || !IsAtDestination(target)) &&
                    rtcp.Geometry is not null && rtcp.JogStatus is not (RtcpJogStatus.Jogging or RtcpJogStatus.Stopping) &&
                    GetReadyProgram(RtcpProgramName(target)) is not null;

            if (returning && !canStartProgramMotion()) return false;
            return canStartHomedMotion() && movePath.IsReadyToMove &&
                !(controller.PLC.Programs.TryGetValue(Initialize.RunTP0PlcProgramName, out PLCProgram? handoff) && handoff is { IsRunning: true }) &&
                !IsAtDestination(target) &&
                (target != HeadPositioningTarget.ReturnToWork ||
                    (workPath.IsReadyToStartOrResume && GetReadyProgram(Initialize.RunTP0PlcProgramName) is not null));
        }
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

    private PLCProgram? GetReadyProgram(string name)
        => controller.PLC.Programs.TryGetValue(name, out PLCProgram? program) &&
           program is { IsLoaded: true, Enabled: true, Cyclic: false, IsRunning: false, HasLoadError: false }
            ? program : null;

    private static string RtcpProgramName(HeadPositioningTarget target)
        => target == HeadPositioningTarget.ReturnToWork
            ? Initialize.PositionRtcpApproachPlcProgramName : Initialize.PositionRtcpDeparturePlcProgramName;

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
                PositioningSettings settings = positioningSettingsProvider();
                if (controller.MotionController.RTCP.Mode == RtcpMode.RTCP)
                    return StartRtcpPositioning(target, speed, settings);

                double[] start = moveAxes.Select(axis => axis.CommandPosition).ToArray();
                double[] destination = ResolveDestination(target);

                ValidatePoint(start, "Current position", start);
                ValidatePoint(destination, "Destination");
                var points = BuildNormalRoute(target, start, destination, speed, settings);
                foreach (var (point, _) in points)
                {
                    ValidatePoint(point, "Positioning waypoint", start);
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
                PLCProgram? continuation = target == HeadPositioningTarget.ReturnToWork
                    ? GetReadyProgram(Initialize.RunTP0PlcProgramName) : null;
                if (target == HeadPositioningTarget.ReturnToWork && continuation is null)
                    return Reject("ReturnToWork: the RunTP0 PLC program is not available.");
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
                    !movePath.AddProgram(++block, ToolPath.ProgramType.PLC, Initialize.RunTP0PlcProgramName, stop: false))
                    throw new InvalidOperationException("Could not append RunTP0 to TP1.");
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
        var points = new List<(double[] Point, double Velocity)>(4);
        double[] final = start.ToArray();
        double fromZ = start[zSlot];
        double toZ = destination[zSlot];
        if (RequiresTraverse(start, destination))
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

    private bool RequiresTraverse(ReadOnlySpan<double> start, ReadOnlySpan<double> destination)
    {
        for (int i = 0; i < moveAxes.Length; i++)
            if (i != zSlot && Math.Abs(destination[i] - start[i]) >= moveAxes[i].AxisTolerance)
                return true;
        return false;
    }

    private bool StartRtcpPositioning(HeadPositioningTarget target, double speed, PositioningSettings settings)
    {
        var rtcp = controller.MotionController.RTCP;
        var geometry = rtcp.Geometry;
        string programName = RtcpProgramName(target);
        var program = GetReadyProgram(programName);
        if (geometry is null || program is null)
            return Reject($"{target}: RTCP calibration or the {programName} PLC program is unavailable.");

        bool returning = target == HeadPositioningTarget.ReturnToWork;
        double requestedDistance = axialReturnDistance();
        if (!double.IsFinite(requestedDistance) || requestedDistance < 0 || (returning && requestedDistance == 0))
            return Reject($"{target}: check Axial Return Distance under Controller > RTCP.");
        double axialSpeed = axialReturnVelocity();
        if (!double.IsFinite(axialSpeed) || axialSpeed <= 0)
            return Reject($"{target}: ZT Jog Velocity must be finite and positive.");

        long generation = workPath.CoordinateResetGeneration;
        double[]? work = returning ? workPath.LastPosition.ToArray() : null;
        double[] start = moveAxes.Select(axis => axis.CommandPosition).ToArray();
        double[] destination = work is null ? ResolveDestination(target) : workSlots.Select(slot => work[slot]).ToArray();
        int Slot(int index) => Array.FindIndex(moveAxes, axis => axis.Index == index);
        int x = Slot(geometry.LinearAxes.X), y = Slot(geometry.LinearAxes.Y), z = Slot(geometry.LinearAxes.Z);
        int first = Slot(geometry.FirstRotary.AxisIndex), second = Slot(geometry.SecondRotary.AxisIndex);
        if (x != Array.IndexOf(moveAxisNames, "X") || y != Array.IndexOf(moveAxisNames, "Y") || z != zSlot ||
            first < 0 || second < 0 || first == second || moveAxisNames[first] is not ("A" or "C") || moveAxisNames[second] is not ("A" or "C"))
            return Reject($"{target}: the RTCP calibration axes do not match the positioning axes.");

        var axes = new RtcpAxes(x, y, z, first, second);
        // Reference and work positions are TCP coordinates. Home and Park are
        // machine positions, including their configured rotary destinations.
        if (returning || target == HeadPositioningTarget.GoToReference)
        {
            if (!rtcp.TryGetMachinePosition(axes.ReadPoint(destination), destination[first], destination[second], out var machine))
                return Reject($"{target}: the work destination could not be converted to machine coordinates.");
            axes.WritePoint(destination, machine);
        }
        ValidatePoint(start, "Current position", returning ? start : null);
        ValidatePoint(destination, "Destination");
        if (!returning && !RequiresTraverse(start, destination) && Math.Abs(destination[z] - start[z]) < moveAxes[z].AxisTolerance)
            return false;

        var (arguments, description) = returning
            ? BuildRtcpApproach(axes, geometry, start, destination, requestedDistance, speed, axialSpeed, settings)
            : BuildRtcpDeparture(axes, geometry, start, destination, requestedDistance, speed, axialSpeed, settings);
        if (!canStartProgramMotion() || rtcp.Mode != RtcpMode.RTCP || !ReferenceEquals(geometry, rtcp.Geometry) ||
            generation != workPath.CoordinateResetGeneration ||
            (work is not null && !work.AsSpan().SequenceEqual(workPath.LastPosition)) ||
            !start.SequenceEqual(moveAxes.Select(axis => axis.CommandPosition)))
            return Reject($"{target}: the RTCP pose or program entry changed during preparation.");

        var move = new PendingRtcpMove(target, program, generation, work, geometry);
        if (!program.Run(arguments))
            return Reject($"{target}: {programName} was rejected.");
        pendingRtcp = move;
        NTLog.Report(FormattableString.Invariant($"{target} accepted: {description}; tool length={geometry.ToolLengthMm:G17} mm."));
        return true;
    }

    private (PLCProgramArgument[] Arguments, string Description) BuildRtcpApproach(RtcpAxes axes,
        RtcpMachineGeometry geometry, double[] start, double[] destination, double requestedDistance, double speed,
        double axialSpeed, PositioningSettings settings)
    {
        var machine = axes.ReadPoint(destination);
        var outward = RtcpReturnApproach.OutwardDirection(geometry, destination[axes.First], destination[axes.Second]);
        double distance = LimitAxialDistance(axes, machine, outward, requestedDistance);
        var entry = RtcpReturnApproach.PointAtDistance(machine, outward, distance);
        var tolerance = new RtcpVector3(moveAxes[axes.X].AxisTolerance, moveAxes[axes.Y].AxisTolerance, moveAxes[axes.Z].AxisTolerance);
        if (Math.Abs(entry.X - machine.X) <= tolerance.X && Math.Abs(entry.Y - machine.Y) <= tolerance.Y &&
            Math.Abs(entry.Z - machine.Z) <= tolerance.Z)
            throw new InvalidOperationException(distance < requestedDistance
                ? "The travel limits leave no tool-axis approach longer than the axis resolution."
                : "Axial Return Distance is smaller than the axis resolution.");

        double[] entryPose = destination.ToArray();
        axes.WritePoint(entryPose, entry);
        ValidatePoint(entryPose, "Tool-axis entry");

        // An aligned tool can withdraw and reapproach along the same line.
        // Otherwise Safe Z applies to the cross-machine leg only.
        bool sameAngles = Math.Abs(start[axes.First] - destination[axes.First]) <= 1e-9 &&
            Math.Abs(start[axes.Second] - destination[axes.Second]) <= 1e-9;
        bool useClearance = !sameAngles || !RtcpReturnApproach.IsOnAxis(axes.ReadPoint(start), machine, outward, tolerance);
        double clearance = start[axes.Z];
        double mixedTraverseSpeed = speed;
        if (useClearance)
        {
            if (entry.Z < machine.Z && Math.Abs(entry.X - machine.X) <= tolerance.X &&
                Math.Abs(entry.Y - machine.Y) <= tolerance.Y)
                throw new InvalidOperationException("Approaching the tool-axis entry from Safe Z would pass through the target.");

            AxisWrapper z = moveAxes[axes.Z];
            clearance = settings.GetTraverseHeight(Math.Max(start[axes.Z], entry.Z), z.MinCourse, z.MaxCourse);
            double[] lift = start.ToArray();
            double[] traverse = entryPose.ToArray();
            lift[axes.Z] = clearance;
            traverse[axes.Z] = clearance;
            ValidatePoint(lift, "Retraction", start);
            ValidatePoint(traverse, "Clearance position", start);
            mixedTraverseSpeed = AxisLayout.PathVelocity(moveAxes, lift, traverse, speed);
        }

        // The effective axial distance is already limited by the configured
        // axial return distance and by the first machine travel plane.
        // A zero setting preserves the whole axial leg at ZT jog speed.
        double slowDistance = settings.ApproachDistance > 0 ? Math.Min(settings.ApproachDistance, distance) : distance;
        double approachSpeed = settings.ApproachDistance > 0 ? settings.GetApproachVelocity(speed) : axialSpeed;
        var approach = RtcpReturnApproach.PointAtDistance(machine, outward, slowDistance);
        double[] approachPose = destination.ToArray();
        axes.WritePoint(approachPose, approach);
        ValidatePoint(approachPose, "Slow approach start");

        return ([
            PLCProgramArgument.Real("EntryX", entry.X), PLCProgramArgument.Real("EntryY", entry.Y), PLCProgramArgument.Real("EntryZ", entry.Z),
            PLCProgramArgument.Real("TargetX", machine.X), PLCProgramArgument.Real("TargetY", machine.Y), PLCProgramArgument.Real("TargetZ", machine.Z),
            PLCProgramArgument.Real("TargetA", destination[Array.IndexOf(moveAxisNames, "A")]),
            PLCProgramArgument.Real("TargetC", destination[Array.IndexOf(moveAxisNames, "C")]),
            PLCProgramArgument.Real("ClearanceZ", clearance), PLCProgramArgument.Real("TraverseSpeed", speed),
            PLCProgramArgument.Real("MixedTraverseSpeed", mixedTraverseSpeed), PLCProgramArgument.Real("ApproachSpeed", approachSpeed),
            PLCProgramArgument.Logic("UseClearance", useClearance),
            PLCProgramArgument.Real("ApproachX", approach.X), PLCProgramArgument.Real("ApproachY", approach.Y),
            PLCProgramArgument.Real("ApproachZ", approach.Z)
        ], FormattableString.Invariant(
            $"axial distance={distance:G17} mm (requested {requestedDistance:G17} mm); slow approach={slowDistance:G17} mm at {approachSpeed:G17} mm/s; Safe Z traverse={useClearance}; entry XYZ=({entry.X:G17}, {entry.Y:G17}, {entry.Z:G17}); target XYZ=({machine.X:G17}, {machine.Y:G17}, {machine.Z:G17})"));
    }

    private (PLCProgramArgument[] Arguments, string Description) BuildRtcpDeparture(RtcpAxes axes,
        RtcpMachineGeometry geometry, double[] start, double[] destination, double requestedDistance, double speed,
        double axialSpeed, PositioningSettings settings)
    {
        // Use the current pose, including any jog since Pause. The axial
        // departure ends at its requested distance or the first travel plane.
        var position = axes.ReadPoint(start);
        var outward = RtcpReturnApproach.OutwardDirection(geometry, start[axes.First], start[axes.Second]);
        double distance = LimitAxialDistance(axes, position, outward, requestedDistance);
        var exit = RtcpReturnApproach.PointAtDistance(position, outward, distance);
        double[] exitPose = start.ToArray();
        axes.WritePoint(exitPose, exit);
        ValidatePoint(exitPose, "Tool-axis exit");

        // Only the following lateral move uses the clearance floor. If the
        // exit is already higher, keep that height through reorientation.
        AxisWrapper z = moveAxes[axes.Z];
        bool traverse = RequiresTraverse(exitPose, destination);
        double clearance = traverse ? settings.GetTraverseHeight(exit.Z, z.MinCourse, z.MaxCourse) : exit.Z;
        double[] lift = exitPose.ToArray();
        double[] across = destination.ToArray();
        lift[axes.Z] = clearance;
        across[axes.Z] = clearance;
        ValidatePoint(lift, "Retraction");
        ValidatePoint(across, "Clearance position");
        var machine = axes.ReadPoint(destination);
        double mixedTraverseSpeed = AxisLayout.PathVelocity(moveAxes, lift, across, speed);
        return ([
            PLCProgramArgument.Real("ExitX", exit.X), PLCProgramArgument.Real("ExitY", exit.Y), PLCProgramArgument.Real("ExitZ", exit.Z),
            PLCProgramArgument.Real("ClearanceZ", clearance),
            PLCProgramArgument.Real("TargetX", machine.X), PLCProgramArgument.Real("TargetY", machine.Y), PLCProgramArgument.Real("TargetZ", machine.Z),
            PLCProgramArgument.Real("TargetA", destination[Array.IndexOf(moveAxisNames, "A")]),
            PLCProgramArgument.Real("TargetC", destination[Array.IndexOf(moveAxisNames, "C")]),
            PLCProgramArgument.Real("ExitSpeed", axialSpeed), PLCProgramArgument.Real("TraverseSpeed", speed),
            PLCProgramArgument.Real("MixedTraverseSpeed", mixedTraverseSpeed)
        ], FormattableString.Invariant(
            $"axial exit={distance:G17} mm (requested {requestedDistance:G17} mm); exit XYZ=({exit.X:G17}, {exit.Y:G17}, {exit.Z:G17}); traverse Z={clearance:G17}; destination XYZ=({machine.X:G17}, {machine.Y:G17}, {machine.Z:G17})"));
    }

    private double LimitAxialDistance(RtcpAxes axes, RtcpVector3 origin, RtcpVector3 direction, double distance)
    {
        distance = LimitAxisDistance(axes.X, origin.X, direction.X, distance);
        distance = LimitAxisDistance(axes.Y, origin.Y, direction.Y, distance);
        return LimitAxisDistance(axes.Z, origin.Z, direction.Z, distance);
    }

    private double LimitAxisDistance(int slot, double target, double direction, double distance)
    {
        AxisWrapper axis = moveAxes[slot];
        foreach (AxisWrapper member in controller.Axes)
        {
            if (member != axis && !ReferenceEquals(member.CouplingMaster, axis)) continue;
            distance = RtcpReturnApproach.LimitDistance(distance, target, direction,
                member.MinCourse, member.MaxCourse, member == axis ? 0 : member.CouplingOffset);
        }
        return distance;
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
            if (pendingRtcp is { } rtcpMove)
            {
                UpdateRtcpMove(rtcpMove);
                return;
            }
            if (pending is not { } move) return;
            // PLC owns the handoff once RunTP0 is activated. In particular, its
            // own TP1 Stop and TP0 Run are not cancellations or UI commands.
            if (move.Continuation is { } program && program.ActivationId != move.ActivationId)
            {
                if (program.HasCompleted && !program.IsRunning)
                {
                    pending = null;
                    if (!program.Succeeded)
                    {
                        string reason = $"RunTP0: PLC ended with {program.LastExitReason}, result {program.LastResult}.";
                        if (program.LastExitReason == PLCProgramExitReason.Completed && program.LastResult == 3)
                            reason += DescribeObservedAlignment(program.ActivationId);
                        Reject(reason);
                    }
                    else NTLog.Report("ReturnToWork completed; RunTP0 successfully handed execution to TP0.");
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
                CancelCore("RunTP0 PLC was not activated after TP1 completion.");
        }
    }

    private void UpdateRtcpMove(PendingRtcpMove move)
    {
        var rtcp = controller.MotionController.RTCP;
        if (!canStartProgramMotion() || (move.Target == HeadPositioningTarget.ReturnToWork && !canRunInCurrentCoordinateMode()) ||
            controller.PLC.IsApplying || !LayoutMatches() || !workPath.IsIdle ||
            rtcp.Mode != RtcpMode.RTCP || !ReferenceEquals(move.Geometry, rtcp.Geometry) ||
            workPath.CoordinateResetGeneration != move.WorkGeneration ||
            (move.WorkDestination is { } work && !work.AsSpan().SequenceEqual(workPath.LastPosition)))
        {
            CancelCore($"{move.Target} cancelled: the RTCP calibration, entry position, or controller state changed.");
            return;
        }

        var program = move.Program;
        if (program.ActivationId == move.ActivationId)
        {
            if (Environment.TickCount64 - move.RequestedAt >= CompletionProgramTimeoutMilliseconds)
                CancelCore($"{move.Target}: {RtcpProgramName(move.Target)} was not activated.");
            return;
        }
        if (program.IsRunning || !program.HasCompleted) return;
        if (!program.Succeeded)
        {
            pendingRtcp = null;
            Reject($"{move.Target}: {RtcpProgramName(move.Target)} ended with {program.LastExitReason}, result {program.LastResult}.");
            return;
        }
        if (!AxesAvailable(requireStopped: true))
        {
            move.CompletedAt ??= Environment.TickCount64;
            if (Environment.TickCount64 - move.CompletedAt.Value >= CompletionProgramTimeoutMilliseconds)
                CancelCore($"{move.Target}: the axes did not become available after positioning.");
            return;
        }

        if (move.Target != HeadPositioningTarget.ReturnToWork)
        {
            pendingRtcp = null;
            NTLog.Report($"Positioning completed: {move.Target}.");
            return;
        }

        // Use the controller's RTCP alignment check, with this return still marked
        // active so a rejected final pose cannot schedule another repositioning.
        try
        {
            if (!controller.RunToolPath(workPath))
                Reject("ReturnToWork: the controller rejected the RTCP handoff to TP0.");
            else
                NTLog.Report("ReturnToWork completed; controller accepted the RTCP handoff to TP0.");
        }
        finally
        {
            pendingRtcp = null;
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
        return FormattableString.Invariant($" Activation={activationId}; observed after PLC completion (not a rejection-scan capture); RunTP0CommonTolerance={commonTolerance:G17}; per-axis checks: ") +
            string.Join("; ", details);
    }

    private void OnPositionMismatch(object? sender, ToolPathPositionMismatchEventArgs e)
    {
        if (e.ToolPathIndex != workPath.Index && e.ToolPathIndex != movePath.Index) return;
        e.ContinueRun = false;
        lock (gate)
        {
            if (!disposed && pendingRtcp is null && e.ToolPathIndex == workPath.Index)
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
            PendingRtcpMove? rtcpMove = pendingRtcp;
            pendingRtcp = null;
            if (rtcpMove is not null && controller.IsConnected)
                rtcpMove.Program.Stop();
            if (move is not null) NTLog.Report($"Positioning cancelled: {move.Target}; TP0 SourceLine={workPath.CurrentBlock}");
            if (move?.Continuation is { } program && controller.IsConnected)
            {
                // Prevent an unreached RunTP0 from starting, and stop an activation
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
    private void ValidatePoint(double[] point, string context, double[]? initial = null)
    {
        for (int i = 0; i < moveAxes.Length; i++)
        {
            AxisWrapper axis = moveAxes[i];
            foreach (AxisWrapper member in controller.Axes)
            {
                if (member != axis && !ReferenceEquals(member.CouplingMaster, axis)) continue;
                double value = point[i] + (member == axis ? 0 : member.CouplingOffset);
                double pdo = (value - member.HomeOffset) / member.Step;
                // A safe-Z leg may hold an absolute axis at its restored coordinate.
                // Every changed coordinate and the requested destination must be inside.
                bool holdsRestoredPosition = initial != null && point[i] == initial[i] &&
                    controller.MotionController.Axes[member.Index].HomingMethod == HomingMethod.AbsoluteEncoder &&
                    member.IsHomed;
                if (!double.IsFinite(value) ||
                    !double.IsFinite(member.MinCourse) || !double.IsFinite(member.MaxCourse) ||
                    member.MinCourse > member.MaxCourse || (!holdsRestoredPosition && (value < member.MinCourse || value > member.MaxCourse)))
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

    private readonly record struct RtcpAxes(int X, int Y, int Z, int First, int Second)
    {
        internal RtcpVector3 ReadPoint(double[] pose) => new(pose[X], pose[Y], pose[Z]);

        internal void WritePoint(double[] pose, RtcpVector3 point)
        {
            pose[X] = point.X;
            pose[Y] = point.Y;
            pose[Z] = point.Z;
        }
    }

    private sealed class PendingRtcpMove(HeadPositioningTarget target, PLCProgram program, long workGeneration,
        double[]? workDestination, RtcpMachineGeometry geometry)
    {
        internal HeadPositioningTarget Target { get; } = target;
        internal PLCProgram Program { get; } = program;
        internal long WorkGeneration { get; } = workGeneration;
        internal double[]? WorkDestination { get; } = workDestination;
        internal RtcpMachineGeometry Geometry { get; } = geometry;
        internal long ActivationId { get; } = program.ActivationId;
        internal long RequestedAt { get; } = Environment.TickCount64;
        internal long? CompletedAt { get; set; }
    }
}

using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;

namespace RouterXYZBCTC;

/// <summary>
/// Builds finite TP2 routes from one captured request. Each route stops at a
/// physical-state commit boundary: release, clamp, then withdrawal/continuation.
/// The Commands partial advances those stages only after PLC completion and
/// acknowledged TP0 compensation. Route generation never waits on the UI thread.
/// </summary>
internal sealed partial class ToolChangerLinks
{
    /// <summary>
    /// Starts a staged exchange on TP2. Mode 0 is an M6 request and may resume TP0;
    /// mode 1 is an operator request and always ends with TP0 paused.
    /// CurrentTool and TP0 Offset commit only after ReleaseTool or ClampTool completes with
    /// every exchange axis stopped at the holder position.
    /// </summary>
    internal bool ChangeTool(long newTool, long changeMode)
        => ChangeTool(newTool, changeMode, null);

    /// <summary>Acquires the entry tool and acknowledges its offset before the host positions TP0.</summary>
    internal bool PrepareStartTool(int newTool, Func<bool> positionAndRun)
    {
        ArgumentNullException.ThrowIfNull(positionAndRun);
        return ChangeTool(newTool, GCodeChangeMode, positionAndRun);
    }

    private bool ChangeTool(long newTool, long changeMode, Func<bool>? preparedContinuation)
    {
        lock (gate)
        {
            if (disposed || !ValidTool(newTool) || !ValidTool(CurrentTool.Value) || !ValidChangeMode(changeMode))
                return RejectChange("Invalid current/requested tool number or change mode.");

            // Disabling automatic exchange ignores only G-code mode. Retaining the
            // current tool turns M6 into the established same-tool offset/continuation
            // handshake, so TP0 resumes through M100 without any physical TP2 route.
            if (changeMode == GCodeChangeMode && !AutomaticToolChangeEnabled.Value)
            {
                NTLog.Report($"ChangeTool: automatic M6 request for T{newTool:00} was ignored.");
                newTool = CurrentTool.Value;
            }
            if (!CanChangeTool(newTool, changeMode, out string reason))
                return RejectChange(reason);

            try
            {
                bool appendM100 = preparedContinuation is null && changeMode == GCodeChangeMode && workPath.HasBlocksAvailableForRun;
                double traverseSpeed = speedProvider();
                if (!double.IsFinite(traverseSpeed) || traverseSpeed <= 0)
                    return RejectChange("Traverse velocity must be finite and positive.");
                double changingSpeed = ToolChangingVelocity.Value;
                if (!double.IsFinite(changingSpeed) || changingSpeed <= 0)
                    return RejectChange("Tool-changing velocity must be finite and positive.");

                long currentTool = CurrentTool.Value;
                double[] start = moveAxes.Select(axis => axis.CommandPosition).ToArray();
                ValidateChangePoint(start);
                double safeZ = moveAxes[zSlot].MaxCourse;
                // Each list ends at a state-commit boundary. The old-tool route ends
                // at ReleaseTool, the new-tool route at ClampTool, and the return route at Start.
                var placeRoute = new List<RouteBlock>(10);
                var pickRoute = new List<RouteBlock>(10);
                var returnRoute = new List<RouteBlock>(3);
                List<RouteBlock> route = placeRoute;
                double[] position = start.ToArray();

                void Move(double[] point, double velocity, string? description = null)
                {
                    ValidateChangePoint(point);
                    route.Add(new RoutePoint(point, velocity, description));
                    position = point;
                }

                void Program(string name, bool wait, string? description = null)
                    => route.Add(new RouteProgram(name, wait, description));

                void MoveZ(double z)
                {
                    if (position[zSlot] == z) return;
                    double[] point = position.ToArray();
                    point[zSlot] = z;
                    Move(point, traverseSpeed);
                }

                void ApproachAtSafeZ(double[] entry)
                {
                    if (moveAxes.Where((axis, slot) => slot != zSlot)
                        .Any(axis => Math.Abs(position[Array.IndexOf(moveAxes, axis)] - entry[Array.IndexOf(moveAxes, axis)]) >= axis.AxisTolerance))
                    {
                        MoveZ(safeZ);
                        double[] traverse = entry.ToArray();
                        traverse[zSlot] = safeZ;
                        Move(traverse, traverseSpeed);
                    }
                    MoveZ(entry[zSlot]);
                }

                (double[] Station, double[] Place, double[] Pick) GetStationPoints(long number)
                {
                    Tool tool = Tools[checked((int)number - 1)];
                    PointD3 toolPosition = tool.Position;
                    double[] station = InMovePathOrder(toolPosition, tool.PositionB, tool.PositionC);
                    double[] placePoint = InMovePathOrder(toolPosition + tool.PlaceVector, tool.PositionB, tool.PositionC);
                    double[] pickPoint = InMovePathOrder(toolPosition + tool.PickVector, tool.PositionB, tool.PositionC);
                    ValidateChangePoint(station);
                    ValidateChangePoint(placePoint);
                    ValidateChangePoint(pickPoint);
                    return (station, placePoint, pickPoint);
                }

                // Store the current tool: approach through PickP, wait for spindle
                // shutdown, insert at the exchange velocity, release at Position,
                // and stop this stage before leaving the holder.
                if (currentTool != newTool && currentTool != 0)
                {
                    (double[] station, double[] placePoint, double[] pickPoint) = GetStationPoints(currentTool);
                    Program("StopSpindle", wait: false, "Requesting spindle stop.");
                    ApproachAtSafeZ(pickPoint);
                    Program("WaitSpindle", wait: true, "Waiting for spindle stop confirmation above the holder.");
                    Move(station, changingSpeed, $"Entering holder for T{currentTool:00}.");
                    Program("ReleaseTool", wait: true, $"Releasing T{currentTool:00}.");

                    // placeRoute ends at ReleaseTool. UpdateMotionStage records an empty
                    // spindle and clears TP0 Offset before the departure route runs.
                    route = newTool != 0 ? pickRoute : returnRoute;
                    Move(placePoint, changingSpeed, $"Withdrawing from the T{currentTool:00} holder.");
                    MoveZ(safeZ);
                    // An unload-only request must not leave the drawbar open.
                    // Close after withdrawing from the holder; an empty spindle
                    // confirms S2 OFF and S5 ON without requiring tool sensor S1.
                    if (newTool == 0) Program("ClampTool", wait: true, "Closing the empty spindle.");
                }

                // Pick the requested tool: leave the previous holder when needed,
                // approach the new holder through PlaceP, clamp at Position, and
                // stop this stage before withdrawing through PickP.
                if (currentTool != newTool && newTool != 0)
                {
                    route = pickRoute;
                    (double[] station, double[] placePoint, double[] pickPoint) = GetStationPoints(newTool);
                    bool startsWithEmptySpindle = currentTool == 0;
                    if (startsWithEmptySpindle) Program("StopSpindle", wait: false, "Requesting spindle stop.");
                    ApproachAtSafeZ(placePoint);
                    if (startsWithEmptySpindle)
                    {
                        Program("WaitSpindle", wait: true, "Waiting for spindle stop confirmation above the holder.");
                        Program("ReleaseTool", wait: true, "Opening the empty spindle for pickup.");
                    }
                    Move(station, traverseSpeed, $"Entering holder for T{newTool:00}.");
                    Program("ClampTool", wait: true, $"Clamping T{newTool:00}.");

                    // pickRoute ends at ClampTool. UpdateMotionStage records the new tool
                    // and applies its captured XYZ Offset before withdrawal.
                    route = returnRoute;
                    Move(pickPoint, traverseSpeed, $"Withdrawing T{newTool:00} from its holder.");
                    MoveZ(safeZ);
                }

                // M6 must return to TP0's position after the new Offset is applied so
                // M100 can verify alignment and resume the buffered work program.
                // A manual exchange has no TP0 continuation and returns to the exact
                // machine StartPosition; applying the offset must not push a homed
                // axis beyond its course before the operator requests another move.
                double[] requestedOffset = GetWorkOffset(newTool);
                double[] appliedOffset = workPath.Offset.ToArray();
                if (appliedOffset.Length != requestedOffset.Length)
                    throw new InvalidOperationException("TP0 offset layout does not match its configured axes.");
                double[] returnTarget = start.ToArray();
                if (preparedContinuation is null && changeMode == GCodeChangeMode)
                {
                    ReadOnlySpan<double> continuationTarget = workPath.LastPosition;
                    for (int slot = 0; slot < moveAxes.Length; slot++)
                    {
                        int workSlot = FindWorkSlot(moveAxes[slot].Name);
                        returnTarget[slot] = continuationTarget[workSlot] +
                            requestedOffset[workSlot] - appliedOffset[workSlot];
                    }
                }
                // Entry preparation ends after withdrawal at safe Z. The host resolves
                // its entry destination only after the new offset is acknowledged.
                if (preparedContinuation is null && (currentTool != newTool || !returnTarget.SequenceEqual(start)))
                {
                    route = returnRoute;
                    double[] safeReturn = returnTarget.ToArray();
                    safeReturn[zSlot] = safeZ;
                    Move(safeReturn, traverseSpeed, "Returning above the work position.");
                    double returnSpeed = traverseSpeed;
                    // An automatic M6 handoff enters work through the same slow final Z leg as ReturnToWork.
                    if (appendM100)
                    {
                        PositioningSettings settings = positioningSettingsProvider();
                        double targetZ = returnTarget[zSlot];
                        if (settings.ApproachDistance > 0 && safeZ != targetZ)
                        {
                            double approachStart = settings.GetApproachStartZ(safeZ, targetZ);
                            if (approachStart != safeZ && approachStart != targetZ)
                                MoveZ(approachStart);
                            returnSpeed = settings.GetApproachVelocity(traverseSpeed);
                        }
                    }
                    Move(returnTarget, returnSpeed, "Returning to the work position.");
                }

                PLCProgram? continuation = appendM100 ? ReadyM100() : null;
                if (appendM100 && continuation is null)
                    return RejectChange("M100 is not available.");

                var change = new PendingChange(
                    workPath.CoordinateResetGeneration,
                    newTool,
                    pickRoute,
                    returnRoute,
                    GetWorkOffset(0),
                    requestedOffset,
                    continuation)
                {
                    PreparedContinuation = preparedContinuation,
                    ToolExchanged = currentTool != newTool
                };
                // ClampTool and Background use this same captured request to distinguish
                // pickup from closing an empty spindle after an unload.
                if (preparedContinuation is not null) NewTool.Value = newTool;
                pending = change;
                NTLog.Report($"ChangeTool: starting T{currentTool:00} -> T{newTool:00}, {(changeMode == GCodeChangeMode ? "G-code" : "manual")}.");
                toolChangeFault.Value = 0;
                toolChangeSpindleStopped.Value = false;
                // Reset confirmation before activation; prior exchanges cannot release this wait.
                toolChangeRollersConfirmed.Value = false;
                toolChangeActive.Value = currentTool != newTool;

                if (currentTool != newTool)
                {
                    List<RouteBlock> firstRoute = placeRoute.Count != 0 ? placeRoute : pickRoute;
                    firstRoute.Insert(0, new RouteProgram("WaitRollers", true,
                        "Waiting for enabled rollers to reach their free position."));
                }

                if (placeRoute.Count != 0)
                    return StartMove(change, ChangePhase.Placing, placeRoute);
                if (pickRoute.Count != 0)
                    return StartMove(change, ChangePhase.Picking, pickRoute);
                BeginOffset(change, ChangePhase.ApplyingToolOffset, requestedOffset);
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                pending = null;
                toolChangeActive.Value = false;
                return RejectChange(exception.Message);
            }
        }
    }

    private bool StartMove(PendingChange change, ChangePhase phase, IReadOnlyList<RouteBlock> route)
    {
        if (route.Count == 0) throw new InvalidOperationException("A tool-change motion stage is empty.");
        double[] start = moveAxes.Select(axis => axis.CommandPosition).ToArray();
        ValidateChangePoint(start);
        movePath.Begin(route.Count);
        double[] moveOffset = movePath.Offset.ToArray();
        movePath.Reference = start.Select((value, slot) => value - moveOffset[slot]).ToArray();
        for (int slot = 0; slot < moveAxes.Length; slot++) movePath.SetLiveOffset(slot, 0);
        int block = 0;
        double[] previous = new double[moveAxes.Length];
        var programs = new List<ProgramExpectation>();
        foreach (RouteBlock item in route)
        {
            if (item is RoutePoint waypoint)
            {
                double[] displacement = waypoint.Point.Select((value, slot) => value - start[slot]).ToArray();
                AxisLayout.AddStoppedLine(movePath, ++block, previous, displacement, waypoint.Velocity);
                previous = displacement;
                continue;
            }

            RouteProgram requested = (RouteProgram)item;
            PLCProgram program = ReadyExchangeProgram(requested.Name)
                ?? throw new InvalidOperationException($"{requested.Name} is not ready for the tool-change route.");
            if (!movePath.AddProgram(++block, ToolPath.ProgramType.PLC, requested.Name, requested.Wait))
                throw new InvalidOperationException($"Could not append {requested.Name} to TP2.");
            programs.Add(new ProgramExpectation(program, program.ActivationId));
        }
        movePath.End();
        change.Phase = phase;
        change.MoveGeneration = movePath.CoordinateResetGeneration;
        change.FinalBlock = block;
        change.ActivePrograms = programs;
        // Index zero matches TP2's pre-start CurrentBlock. Null entries keep
        // optional descriptions aligned with the original one-based blocks.
        change.BlockDescriptions = [null, .. route.Select(item => item.Description)];
        change.LastReportedBlock = 0;
        change.ProgramCompletionStartedAt = 0;
        if (controller.RunToolPath(movePath)) return true;
        pending = null;
        toolChangeActive.Value = false;
        return RejectChange($"TP2 Run was rejected during {phase}.");
    }

}

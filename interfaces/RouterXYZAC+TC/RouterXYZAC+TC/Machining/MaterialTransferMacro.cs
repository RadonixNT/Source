using NT;
using NT.Controller;
using NT.Controller.Macros;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;

namespace RouterXYZACTC;

/// <summary>UI-owned TP1 loading/unloading sequences; waiting PLC rows own output feedback.</summary>
internal sealed class MaterialTransferMacro : IDisposable
{
    private readonly MainForm form;
    private readonly ControllerWrapper controller;
    private readonly RouterLinks settings;
    private readonly ToolPathWrapper path;
    private AxisWrapper[] axes => path.Axes;
    private readonly MacroProgram macro;
    private readonly PLCIOMapping output;
    private TaskCompletionSource<bool>? completion;
    private MacrosArg? request;
    private long pathGeneration;
    private readonly bool loading;
    private readonly string commandName, onProgram, offProgram;
    private readonly RealLink startPosition, endPosition, velocity;
    private readonly Link[] editableSettings;
    private readonly string[] programNames;
    private readonly Dictionary<string, long> activations = new();
    private readonly PLCIOMapping? vacuum;
    internal CommandLink? VacuumOffLink { get; }
    private int FinalBlock => loading ? 8 : 6;
    private long executionId;
    private long preparationGeneration;
    private ToolPathStoppedEventArgs? latestStop;
    private bool routeActive;
    private bool disposed;

    internal MaterialTransferMacro(MainForm form, ControllerWrapper controller, RouterLinks settings, bool loading = false)
    {
        this.form = form;
        this.controller = controller;
        this.settings = settings;
        this.loading = loading;
        commandName = loading ? "Load" : "Unload";
        onProgram = loading ? "LowerLoader" : "LowerUnloader";
        offProgram = loading ? "RaiseLoader" : "RaiseUnloader";
        startPosition = loading ? settings.Loader.StartPosition : settings.UnloaderStartPosition;
        endPosition = loading ? settings.Loader.EndPosition : settings.UnloaderEndPosition;
        velocity = loading ? settings.Loader.Velocity : settings.UnloaderVelocity;
        editableSettings = loading
            ? [settings.OnBridgeAxis, startPosition, endPosition, velocity, settings.Loader.UpTime,
               settings.Loader.DownTime, settings.Loader.PickDelay, settings.Loader.ReleaseDelay]
            : [settings.OnBridgeAxis, startPosition, endPosition, velocity, settings.UnloaderUpTime, settings.UnloaderDownTime];
        programNames = loading ? [onProgram, offProgram, "PickWithLoader", "ReleaseFromLoader"] : [onProgram, offProgram];
        if (loading)
        {
            vacuum = controller.PLC.IOMappings["LoaderVacuum"];
            VacuumOffLink = settings.Loader.Group.AddCommand("ReleaseLoaderVacuum", "Release Loader Vacuum",
                "Explicitly release retained loader vacuum while all motion is stopped.");
            VacuumOffLink.QueryEnabled += (_, e) => e.Enabled &= CanReleaseVacuum();
            VacuumOffLink.ActionExecute += (_, _) =>
            {
                if (!CanReleaseVacuum()) return;
                vacuum.SetLogic(false);
                NTLog.Report("Loader vacuum release requested by operator.");
            };
        }
        path = controller.ToolPaths[1];
        controller.ToolPathStopped += PathStopped;

        output = controller.PLC.IOMappings[loading ? "LoaderDown" : "UnloaderDown"];
        macro = controller.MotionController.Macros.AddProgram(commandName, RunAsync);
        RunLink = CommandLink.CreateCommand(commandName, commandName);
        RunLink.QueryEnabled += (_, e) => e.Enabled &= !macro.IsRunning && CanStart();
        RunLink.ActionExecute += (_, _) =>
        {
            if (!macro.IsRunning && CanStart() && !controller.MotionController.Macros.RunProgram(commandName))
                NTLog.Warning($"{commandName} rejected: {controller.MotionController.Macros.LastError}");
        };
        foreach (Link link in editableSettings)
            link.QueryEnabled += SettingCanEdit;
    }

    internal CommandLink RunLink { get; }
    internal bool IsActive => routeActive || macro.IsRunning;

    private void PathStopped(object? sender, ToolPathStoppedEventArgs e)
    {
        if (e.ToolPathIndex != 1) return;
        ToolPathStoppedEventArgs? previous;
        do
        {
            previous = Volatile.Read(ref latestStop);
            if (previous is not null && previous.ExecutionId >= e.ExecutionId) return;
        } while (!ReferenceEquals(Interlocked.CompareExchange(ref latestStop, e, previous), previous));
        // UI-owned Update consumes the immutable event on the existing refresh timer.
        form.RequestRuntimeStateRefresh();
    }

    internal static long NextActivation(long value) => value == long.MaxValue ? 1 : value + 1;

    internal static bool IsMatchingStop(ToolPathStoppedEventArgs stop, long execution, long preparation, long coordinates)
        => stop.ToolPathIndex == 1 && stop.ExecutionId == execution &&
            stop.PreparationGeneration == preparation && stop.CoordinateGeneration == coordinates;

    private void SettingCanEdit(object? sender, QueryEventArgs e) => e.Enabled &= !IsActive;

    private bool CanReleaseVacuum() => !disposed && vacuum is { IsPhysicalTarget: true } && form.IsMachineOnline &&
        form.generalLinks.AccessSession?.CanOperate == true && !form.IsTransferActive && !form.IsProgramLoading &&
        !controller.IsBusy && !form.toolChangerLinks.IsActive && !form.controllerLinks.IsHeadPositioningActive &&
        controller.ToolPaths.All(p => p.IsIdle && !p.IsProgramActive) &&
        controller.PLC.Programs.All(p => p.Cyclic || !p.IsRunning) &&
        controller.Axes.All(a => a.IsStopped && !a.IsMoving && !a.IsRunning && !a.IsStopping && !a.IsJogging && !a.IsHoming);

    private bool CanStart()
    {
        if (disposed || routeActive || (loading ? form.unloadMacro?.IsActive == true : form.loadMacro?.IsActive == true) || !form.IsMachineOnline || form.IsProgramLoading ||
            form.generalLinks.AccessSession?.CanOperate != true ||
            !form.controllerLinks.CanStartHomedMotion() || form.controllerLinks.IsHeadPositioningActive ||
            form.toolChangerLinks.IsActive || controller.ToolPaths[0].IsProgramActive ||
            controller.ToolPaths.Any(candidate => !candidate.IsIdle || candidate.IsProgramActive) ||
            controller.PLC.Programs.Any(program => !program.Cyclic && program.IsRunning) ||
            !output.IsPhysicalTarget || !path.IsReadyToMove || !path.Axes.SequenceEqual(axes) ||
            programNames.Any(name => !ReadyProgram(name)) || (loading && vacuum?.IsPhysicalTarget != true)) return false;
        return controller.Axes.All(axis => axis.IsReadyToMove && axis.IsStopped &&
            !axis.IsMoving && !axis.IsRunning && !axis.IsStopping && !axis.IsJogging && !axis.IsHoming &&
            !axis.HasFault && !axis.HasCouplingFault && axis.Owner == AxisOwner.Controller);
    }

    private bool ReadyProgram(string name) => controller.PLC.Programs.TryGetValue(name, out PLCProgram? program)
        && program is { IsLoaded: true, Enabled: true, Cyclic: false, IsRunning: false, HasLoadError: false };

    private async Task RunAsync(object sender, MacrosArg arg)
    {
        if (arg.ToolPath is not null)
            throw new InvalidOperationException($"{commandName} requires a direct command, with TP0 inactive.");
        if (disposed || arg.IsCancellationRequested || form.IsDisposed || !form.IsHandleCreated) return;
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration cancellation = arg.CancellationToken.Register(
            () => finished.TrySetCanceled(arg.CancellationToken));
        try
        {
            form.BeginInvoke((Action)(() =>
            {
                if (disposed || arg.IsCancellationRequested || !TryStart(arg, finished))
                    finished.TrySetResult(false);
            }));
        }
        catch (InvalidOperationException) when (form.IsDisposed || !form.IsHandleCreated) { return; }
        if (!await finished.Task.ConfigureAwait(false))
            throw new InvalidOperationException($"{commandName} was rejected or interrupted; see the operation report.");
    }

    /// <summary>Raise Z first, then move the perpendicular axis while retaining the raised height.</summary>
    internal static (double[] Raised, double[] Start, double[] End) ResolvePositions(string onBridgeAxis,
        IReadOnlyList<string> axisNames, double[] current, double startPosition, double endPosition, double safeZ)
    {
        string moveAxis = onBridgeAxis switch
        {
            "X" => "Y",
            "Y" => "X",
            _ => throw new ArgumentException("OnBridgeAxis must be X or Y.")
        };
        int slot = Array.IndexOf(axisNames.ToArray(), moveAxis);
        int zSlot = Array.IndexOf(axisNames.ToArray(), "Z");
        if (slot < 0 || zSlot < 0 || current.Length != axisNames.Count ||
            !double.IsFinite(startPosition) || !double.IsFinite(endPosition) || !double.IsFinite(safeZ))
            throw new ArgumentException("Unload requires valid positions, Z and the perpendicular axis.");
        double[] raised = current.ToArray();
        raised[zSlot] = safeZ;
        double[] start = raised.ToArray(), end = raised.ToArray();
        start[slot] = startPosition;
        end[slot] = endPosition;
        return (raised, start, end);
    }

    private bool TryStart(MacrosArg arg, TaskCompletionSource<bool> finished)
    {
        if (!CanStart()) return false;
        bool began = false;
        try
        {
            double speed = form.generalLinks.TraverseVelocity;
            if (!double.IsFinite(speed) || speed <= 0) throw new InvalidOperationException("Invalid traverse velocity.");
            double transferSpeed = velocity.Value;
            if (!double.IsFinite(transferSpeed) || transferSpeed <= 0) throw new InvalidOperationException("Invalid transfer velocity.");
            double[] current = axes.Select(axis => axis.CommandPosition).ToArray();
            var points = ResolvePositions(settings.OnBridgeAxis.Value, axes.Select(axis => axis.Name).ToArray(),
                current, startPosition.Value, endPosition.Value,
                axes.Single(axis => axis.Name == "Z").MaxCourse);
            ValidatePoint(current);
            ValidatePoint(points.Raised);
            ValidatePoint(points.Start);
            ValidatePoint(points.End);
            path.Begin(FinalBlock);
            began = true;
            pathGeneration = path.CoordinateResetGeneration;
            double[] offset = path.Offset.ToArray();
            path.Reference = current.Select((value, slot) => value - offset[slot]).ToArray();
            for (int slot = 0; slot < axes.Length; slot++) path.SetLiveOffset(slot, 0);
            if (!path.AddProgram(1, ToolPath.ProgramType.PLC, offProgram, true))
                throw new InvalidOperationException("Could not append initial mechanism raise.");
            path.AddLine(2, points.Raised.Select((value, slot) => value - current[slot]).ToArray(), speed, stop: true);
            path.AddLine(3, points.Start.Select((value, slot) => value - current[slot]).ToArray(), speed, stop: true);
            if (!path.AddProgram(4, ToolPath.ProgramType.PLC, onProgram, true))
                throw new InvalidOperationException("Could not append mechanism lower.");
            if (loading && !path.AddProgram(5, ToolPath.ProgramType.PLC, "PickWithLoader", true))
                throw new InvalidOperationException("Could not append Loader Pick.");
            path.AddLine(loading ? 6 : 5, points.End.Select((value, slot) => value - current[slot]).ToArray(), transferSpeed, stop: true);
            if (loading && !path.AddProgram(7, ToolPath.ProgramType.PLC, "ReleaseFromLoader", true))
                throw new InvalidOperationException("Could not append Loader Release.");
            if (!path.AddProgram(FinalBlock, ToolPath.ProgramType.PLC, offProgram, true))
                throw new InvalidOperationException("Could not append mechanism raise.");
            path.End();
            activations.Clear();
            foreach (string name in programNames) activations[name] = controller.PLC.Programs[name].ActivationId;
            completion = finished;
            request = arg;
            preparationGeneration = controller.MotionController.ToolPaths[1].PreparationGeneration;
            Volatile.Write(ref latestStop, null);
            routeActive = true;
            // Ordered scan-boundary output commands precede the path's first PLC row.
            controller.PLC.IOMappings["SpindleCW"].SetLogic(false);
            controller.PLC.IOMappings["SpindleCCW"].SetLogic(false);
            if (!controller.RunToolPath(path)) throw new InvalidOperationException("TP1 Run rejected.");
            executionId = path.ExecutionId;
            form.RequestRuntimeStateRefresh();
            NTLog.Report($"{commandName} accepted: spindle off, mechanism up, raise Z, approach, mechanism down, transfer and raise; loader includes vacuum pick/release waits.");
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            routeActive = false;
            request = null;
            completion = null;
            if (began && path.IsIdle) controller.ResetToolPath(path);
            NTLog.Warning($"{commandName} rejected: {exception.Message}");
            return false;
        }
    }

    private void ValidatePoint(double[] point)
    {
        for (int slot = 0; slot < axes.Length; slot++)
        {
            AxisWrapper axis = axes[slot];
            foreach (AxisWrapper member in controller.Axes)
            {
                if (member != axis && !ReferenceEquals(member.CouplingMaster, axis)) continue;
                double value = point[slot] + (member == axis ? 0 : member.CouplingOffset);
                double pdo = (value - member.HomeOffset) / member.Step;
                if (!double.IsFinite(value) || !double.IsFinite(pdo) || pdo < int.MinValue || pdo > int.MaxValue ||
                    !double.IsFinite(member.MinCourse) || !double.IsFinite(member.MaxCourse) ||
                    member.MinCourse > member.MaxCourse || value < member.MinCourse || value > member.MaxCourse)
                    throw new InvalidOperationException($"{commandName} destination for {member.Name} is outside travel/PDO range.");
            }
        }
    }

    internal void Update()
    {
        if (!routeActive) return;
        if (request?.IsCancellationRequested == true || !form.IsMachineOnline ||
            controller.ToolPaths[0].IsProgramActive || path.CoordinateResetGeneration != pathGeneration)
        {
            Cancel();
            return;
        }
        if (path.IsStopping) { Cancel(); return; }
        foreach (var entry in activations)
        {
            var stage = (Program: controller.PLC.Programs[entry.Key], Activation: entry.Value);
            if (stage.Program.ActivationId == stage.Activation || !stage.Program.HasCompleted || stage.Program.Succeeded) continue;
            NTLog.Warning($"{commandName}: {stage.Program.Name} ended with {stage.Program.LastExitReason}, result {stage.Program.LastResult}.");
            Cancel();
            return;
        }
        ToolPathStoppedEventArgs? stopped = Volatile.Read(ref latestStop);
        if (stopped is null || !IsMatchingStop(stopped, executionId, preparationGeneration, pathGeneration)) return;
        bool complete = stopped.Reason == ToolPathStopReason.Completed && stopped.BlockIndex == FinalBlock &&
            activations.All(entry => controller.PLC.Programs[entry.Key].Succeeded &&
                controller.PLC.Programs[entry.Key].ActivationId == (entry.Key == offProgram
                    ? NextActivation(NextActivation(entry.Value)) : NextActivation(entry.Value))) &&
            path.IsIdle && path.IsStopped && controller.Axes.All(axis => axis.IsStopped && !axis.IsMoving);
        if (!complete) { Cancel(); return; }
        routeActive = false;
        request = null;
        completion?.TrySetResult(true);
        completion = null;
        NTLog.Report($"{commandName} completed: final raise confirmed and matching TP1 Completed received.");
        form.RequestRuntimeStateRefresh();
    }

    internal void Cancel()
    {
        controller.MotionController.Macros.StopProgram(commandName);
        if (!routeActive) return;
        if (controller.IsConnected)
        {
            if (path.CoordinateResetGeneration == pathGeneration) controller.StopToolPath(path);
            foreach (string name in programNames)
                if (controller.PLC.Programs.TryGetValue(name, out PLCProgram? program) && program is { IsRunning: true })
                    program.Stop();
            // Stopping can skip the final raise row. Request raising without resuming motion.
            // Never write LoaderVacuum here: only normal release or the operator may release it.
            if (output.IsPhysicalTarget) output.SetLogic(false);
        }
        routeActive = false;
        request = null;
        completion?.TrySetResult(false);
        completion = null;
        NTLog.Warning($"{commandName} interrupted; mechanism output released; loader vacuum unchanged. Check physical position before retrying.");
        form.RequestRuntimeStateRefresh();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        controller.ToolPathStopped -= PathStopped;
        Cancel();
        foreach (Link link in editableSettings)
            link.QueryEnabled -= SettingCanEdit;
        VacuumOffLink?.Dispose();
        RunLink.Dispose();
    }
}

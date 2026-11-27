using NT_Interpreter.Shared.Router.Controller;
using NT_Interpreter.Shared.Router;
using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;
using NT.Localization;
using NT.Tools;
using NT_Interpreter.Interpreters.RouterXYZAC;
using NT_Interpreter.Interpreters.RouterXYZAC.Controller;
using NT_Interpreter.WinForms;
using OpenTK.Mathematics;
using System.Globalization;
using System.Text;

namespace RouterXYZACRTCP;

/// <summary>
/// Owns operator file/edit/start intent and its machining presentation. Preparing
/// or selecting a source line never starts motion. The execution session plans
/// entry state; head positioning brings the machine to that entry before TP0 runs.
/// Periodic refresh is owned by the Runtime partial, not by independent UI timers.
/// </summary>
public partial class MainForm
{
    private RouterSpindleMonitor? spindleMonitor;

    // The existing position marker keeps its direct-angle orientation.
    private readonly S3DHeadModel machineHeadModel = new(S3DHeadAxes.XYZAC, S3DHeadArrangement.CParent);
    // Marker policy belongs to this interface; physical head orientation remains independent.
    private const S3DPointerOrientationMode pointerOrientationMode = S3DPointerOrientationMode.COnly;
    private static readonly S3DHeadDefinition routerSpindle = new(
        S3DHeadStyle.RoundSpindle, new(70, 70, 135), Vector3.Zero,
        new(S3DRotaryChannel.C, new(0, 0, 167), Vector3.UnitZ),
        new(S3DRotaryChannel.A, new(0, 0, 143), Vector3.UnitX));
    private RtcpMachineGeometry? displayedHeadGeometry;
    private AxisWrapper[] positionAxes = [];
    private sLabel[] commandPositionLabels = [];
    private sLabel[] workPositionLabels = [];
    private double[] lastCommandPositions = [double.NaN, double.NaN, double.NaN, double.NaN, double.NaN];
    private readonly MachineFeedMonitor machineFeedMonitor = new();
    private double displayedMachineFeed = double.NaN;
    private double maximumMachineFeed;
    private double[] lastWorkPositions = [double.NaN, double.NaN, double.NaN, double.NaN, double.NaN];
    private int[] positionSlots = [];
    private string? currentFile;
    private bool lastHeadPositioningActive;
    private WorkCoordinatePresentation? lastWorkFrame;
    private bool lastRunCommandsAvailable;
    private RouterStartRequest? requestedProgramStart;
    internal bool HasPendingProgramStart => requestedProgramStart.HasValue;
    internal bool IsProgramLoading => gCodeInterpreter?.IsLoading == true;
    internal Task PendingProgramPreparation => gCodeInterpreter.PendingLoad;

    internal void RefreshClearedConfigurationToolPath()
    {
        gCodeInterpreter.RefreshClearedToolPath();
        requestedProgramStart = null;
        currentFile = null;
        R_ProgramPath.Text = string.Empty;
        RequestRuntimeUiRefresh();
    }
    internal CommandLink ClearToolPathLink { get; private set; } = null!;

    // Editing an idle path is independent of connectivity, homing and fault state.
    // ProgramActive also covers pending Run and waiting M-code rows.
    internal bool CanClearMainToolPath => runtimeInitialized && !runtimeDisposed && !IsDisposed &&
        !IsProgramLoading && mainToolPath.IsIdle && !mainToolPath.IsProgramActive &&
        controller.Axes.All(axis => !axis.IsMoving && !axis.IsRunning && !axis.IsStopping &&
            !axis.IsJogging && !axis.IsHoming) &&
        controller.PLC.Programs.All(program => program.Cyclic || !program.IsRunning) &&
        controller.MotionController.Macros.Programs.All(program => !program.IsRunning);

    internal void ClearMainToolPath()
    {
        if (!CanClearMainToolPath) return;
        gCodeInterpreter.Clear();
        requestedProgramStart = null;
        currentFile = null;
        R_ProgramPath.Text = string.Empty;
        ReportOperation("TP0 cleared, including its source and 3D preview");
        RequestRuntimeUiRefresh();
    }
    internal bool CanCancelProgramLoad => IsProgramLoading && !gCodeInterpreter.IsCancellingLoad;
    private string? openFileCaption;
    private readonly Dictionary<Control, Cursor> loadingCursors = new();

    private void ShowProgramWarning(NTProgramDiagnostic diagnostic)
    {
        ReportOperation($"Program warning: Code={diagnostic.Code}; Message={diagnostic.Message}; Detail={diagnostic.Detail}");
        R_ProgramProgress.Text = diagnostic.Message;
        diagnostic.Report();
        sMessage.Show(this, SMessageRequest.FromError(diagnostic.Code, SMessageSeverity.Warning, diagnostic.Detail) with
        {
            Title = T(RouterXYZACRTCPTexts.ProgramTitle),
            WriteToReport = false
        }, RuntimeTheme);
    }

    private void InitializeSpindleNotices()
        => spindleMonitor = new RouterSpindleMonitor(controller.MotionController,
            [new("M3", false), new("M4", true)], PostRuntimeUi,
            CaptureSpindleConditions, ShowProgramWarning);

    private void DisposeSpindleNotices() => spindleMonitor?.Dispose();

    private NTSpindleConditions CaptureSpindleConditions()
    {
        bool OptionalInput(string name) => SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings[name]);
        bool Flag(string name) => ((LOGIC)controller.PLC.ApplicationVariables[name]).Value;
        // The fitted cutter is operator-owned. Preserve all configured spindle feedback checks.
        const int fixedToolNumber = 1;
        return new(fixedToolNumber, Flag("SpindleFeedbackConfigured"),
            OptionalInput("SpindleToolClamped"), !SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindleToolReleased"], false), OptionalInput("SpindlePistonRetracted"),
            OptionalInput("PressureSensor"), OptionalInput("SpindleThermalOK"), true,
            OptionalInput("SpindleDriveReady"), false, false,
            Flag("SpindleReverseConfigured"), false);
    }

    private void RegisterProgramLoadingGates()
    {
        var registered = new HashSet<Link>();
        void Register(Link link)
        {
            if (!registered.Add(link) || ReferenceEquals(link, controllerLinks.ResetLink)
                || ReferenceEquals(link, generalLinks.OpenFileLink)
                || ReferenceEquals(link, generalLinks.OpenReportLink)) return;
            link.QueryEnabled += (_, e) => e.Enabled &= !IsProgramLoading;
        }
        void RegisterGroup(LinkGroup group)
        {
            foreach (Link link in group.Members.Values) Register(link);
            foreach (LinkGroup child in group.Groups.Values) RegisterGroup(child);
        }
        RegisterGroup(baseGroup);
        foreach (Link link in runtimeLinks) Register(link);
        foreach (CommandLink command in keyboardLinks.Commands) Register(command);
    }

    private void ProgramLoadingStateChanged()
    {
        if (runtimeDisposed || IsDisposed) return;
        if (IsProgramLoading)
        {
            void CaptureCursor(Control control)
            {
                loadingCursors.TryAdd(control, control.Cursor);
                foreach (Control child in control.Controls) CaptureCursor(child);
            }
            // Capture inherited cursors before changing any parent cursor.
            CaptureCursor(this);
            foreach (Control control in loadingCursors.Keys)
                control.Cursor = ReferenceEquals(control, R_OpenFile) ? Cursors.Default : Cursors.AppStarting;
        }
        else
        {
            foreach ((Control control, Cursor cursor) in loadingCursors)
                if (!control.IsDisposed) control.Cursor = cursor;
            loadingCursors.Clear();
        }
        RequestRuntimeStateRefresh();
        RefreshRuntime(this, EventArgs.Empty);
        UI.Update();
    }

    // The existing Open command changes caption and action while a load is active;
    // the original caption is restored after completion or cancellation.
    private void RefreshProgramLoadButton()
    {
        openFileCaption ??= R_OpenFile.Text;
        string caption = !IsProgramLoading ? openFileCaption : T(NTCommonTexts.Cancel);
        if (R_OpenFile.Text != caption) R_OpenFile.Text = caption;
    }

    // Cancellation is cooperative. Keep the command disabled while the worker finishes.
    internal void CancelProgramLoad()
    {
        if (!CanCancelProgramLoad) return;
        gCodeInterpreter.CancelLoad();
        RefreshProgramLoadButton();
        RequestRuntimeUiRefresh();
        UI.Update();
    }

    // Homing can proceed independently while TP0 is idle; active machining cannot.
    internal bool CanOpenProgram => runtimeInitialized && !runtimeDisposed && !IsConnectionTransition && !IsProgramLoading &&
        (controller.IsConnected || controller.ConnectionState.AllowsOfflineConfiguration()) &&
        mainToolPath.IsIdle && !mainToolPath.IsProgramActive && !controllerLinks.IsHeadPositioningActive &&
         (!controller.IsBusy || controllerLinks.IsHomingActive());

    // The viewer follows execution, so its selection alone cannot express restart intent.
    private RouterStartRequest ResolveProgramStartRequest()
        => requestedProgramStart ?? new(RouterStartOrigin.OperatorRun, 1, generalLinks.StartFromNextCuttingLine);

    internal void OnMainProgramReset()
    {
        ReportOperation("Program reset accepted; cursor returned to beginning");
        requestedProgramStart = null;
        RequestRuntimeUiRefresh();
    }

    internal bool CanOpenSelectStartLineDialog()
        => runtimeInitialized && !runtimeDisposed && !IsDisposed &&
            !IsConnectionTransition &&
            !IsProgramLoading && gCodeInterpreter.Compilation?.Success == true && R_GCodeView.LineCount > 0 &&
            !mainToolPath.IsProgramActive && !controller.IsBusy;

    internal void OpenSelectStartLineDialog()
    {
        if (!CanOpenSelectStartLineDialog()) return;
        sGCodeView viewer = R_NavigatorPanel.SelectedPageControl == R_NavigatorGCodePage
            ? R_GCodeExpandedView : R_GCodeView;
        GCodeDocument document = viewer.Document;
        int initialLine = requestedProgramStart?.SourceLine ??
            (viewer.SelectedLineIndex >= 0 ? viewer.SelectedLineIndex + 1 : 1);
        string? sourceFilePath = CurrentDocumentMatchesOpenedFile(document) ? currentFile : null;
        string title = sourceFilePath is null
            ? T(RouterXYZACRTCPTexts.SelectStartLineTitle)
            : NTLocalization.Format(RouterXYZACRTCPTexts.SelectStartLineFileTitle, sourceFilePath);
        using var dialog = new GCodeStartLineDialog(document, initialLine, RuntimeTheme,
            title,
            NTLocalization.Format(RouterXYZACRTCPTexts.LineCount, document.LineCount),
            T(RouterXYZACRTCPTexts.Select),
            T(NTCommonTexts.Cancel));
        AttachDialogTheme(dialog);
        generalLinks.RestoreSelectStartLineBounds(dialog);
        DialogResult result;
        try
        {
            result = dialog.ShowDialog(this);
        }
        finally
        {
            generalLinks.SaveSelectStartLineBounds(dialog);
        }
        // Preserve the previewed line in the active viewer even if the operator cancels.
        if (ReferenceEquals(viewer.Document, document) && dialog.SelectedPreviewLineIndex >= 0)
            viewer.SelectedLineIndex = dialog.SelectedPreviewLineIndex;
        if (result != DialogResult.OK) return;
        SelectStartLine(dialog.SelectedLineNumber);
    }

    private bool SelectStartLine(int sourceLine)
    {
        if (!CanOpenSelectStartLineDialog() || sourceLine < 1 || sourceLine > R_GCodeView.LineCount)
            return false;

        requestedProgramStart = RouterStartRequest.FromSourceBlock(
            sourceLine, generalLinks.StartFromNextCuttingLine);
        // Visual completion is only a preview; Run remains an explicit operator action.
        gCodeInterpreter.MarkCompletedBefore(sourceLine);
        ReportOperation($"Start line selected: {sourceLine}; FirstCuttingEntry={generalLinks.StartFromNextCuttingLine}");
        RequestRuntimeUiRefresh();
        return true;
    }

    internal bool CanOpenGCodeEditor()
    {
        RouterDocument? document = gCodeInterpreter?.Compilation?.Document;
        return runtimeInitialized && !runtimeDisposed && !IsDisposed && !IsConnectionTransition &&
            !IsProgramLoading && document is not null && !string.IsNullOrWhiteSpace(document.FileName) &&
            !mainToolPath.IsProgramActive && !controller.IsBusy;
    }

    internal async Task OpenGCodeEditorAsync()
    {
        if (!CanOpenGCodeEditor()) return;
        RouterDocument document = gCodeInterpreter.Compilation!.Document;
        string fileName = Path.GetFullPath(document.FileName!);
        Encoding sourceEncoding = document.SourceEncoding?.CreateEncoding() ?? new UTF8Encoding(false, true);
        using var dialog = new GCodeEditorDialog(
            document.Text,
            RuntimeTheme,
            NTLocalization.Format(RouterXYZACRTCPTexts.EditGCodeTitle, fileName),
            T(RouterXYZACRTCPTexts.Save),
            T(NTCommonTexts.Cancel),
            text => SaveEditedGCodeAsync(fileName, text, sourceEncoding));
        AttachDialogTheme(dialog);
        generalLinks.RestoreEditGCodeBounds(dialog);

        DialogResult result;
        try
        {
            result = dialog.ShowDialog(this);
        }
        finally
        {
            generalLinks.SaveEditGCodeBounds(dialog);
        }

        if (result == DialogResult.OK)
            await OpenFileAsync(fileName);
    }

    private async Task<bool> SaveEditedGCodeAsync(string fileName, string source, Encoding encoding)
    {
        try
        {
            await File.WriteAllTextAsync(fileName, source, encoding);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or NotSupportedException)
        {
            NTLog.Error($"RouterXYZAC+RTCP G-code save failed: {error.Message}");
            sMessage.Show(this, new SMessageRequest(T(RouterXYZACRTCPTexts.GCodeSaveFailed))
            {
                Title = T(RouterXYZACRTCPTexts.GCodeTitle),
                Severity = SMessageSeverity.Error,
                TechnicalDetails = error.Message,
                Exception = error,
                WriteToReport = false
            }, RuntimeTheme);
            return false;
        }
    }

    private void UpdateMachinePositionPose()
    {
        Vector3 carriage = new((float)lastCommandPositions[0], (float)lastCommandPositions[1], (float)lastCommandPositions[2]);
        double a = lastCommandPositions[3], c = lastCommandPositions[4];
        // RTCP has already been applied to the physical command positions.
        R_ProgramView3D.SetHeadAxes(new(carriage, A: a, C: c));
        R_ProgramView3D.SetPositionPose(R_ProgramView3D.HeadTipPosition ?? carriage,
            machineHeadModel.EvaluatePointerOrientation(pointerOrientationMode, cDegrees: c, tiltDegrees: a));
    }

    private void RefreshMachineHead(RtcpMachineGeometry? geometry)
    {
        if (ReferenceEquals(displayedHeadGeometry, geometry) && R_ProgramView3D.HeadDefinition is not null) return;
        var definition = routerSpindle;
        if (geometry is not null)
        {
            var gauge = geometry.SpindleGaugePointMm;
            var direction = geometry.ToolDirection;
            double length = geometry.ToolLengthMm / Math.Sqrt(
                direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
            definition = routerSpindle with
            {
                TipOffset = new((float)(gauge.X + direction.X * length),
                    (float)(gauge.Y + direction.Y * length), (float)(gauge.Z + direction.Z * length)),
                Outer = geometry.MachineType == RtcpMachineType.TableTable ? null : HeadJoint(geometry.FirstRotary),
                Inner = geometry.MachineType == RtcpMachineType.HeadHead ? HeadJoint(geometry.SecondRotary) : null
            };
        }
        R_ProgramView3D.ConfigureHead(definition);
        // A tool-length or calibration change can move the displayed tip while the axes stand still.
        if (R_ProgramView3D.HeadTipPosition is Vector3 tip) R_ProgramView3D.SetPositionPointer(tip);
        displayedHeadGeometry = geometry;

        S3DHeadJoint HeadJoint(RtcpRotaryAxis joint)
            => new(joint.AxisIndex == positionAxes[3].Index ? S3DRotaryChannel.A : S3DRotaryChannel.C,
                new((float)joint.PivotMm.X, (float)joint.PivotMm.Y, (float)joint.PivotMm.Z),
                new((float)joint.Direction.X, (float)joint.Direction.Y, (float)joint.Direction.Z),
                joint.ReferenceAngleDegrees);
    }

    private void InitializeMachineView()
    {
        R_ProgramView3D.DirectionalPositionPointer = true;
        // Refresh logical-axis bindings after an accepted offline topology change.
        {
            positionAxes = [controller.GetAxisByName("X"), controller.GetAxisByName("Y"), controller.GetAxisByName("Z"), controller.GetAxisByName("A"), controller.GetAxisByName("C")];
            commandPositionLabels = [R_CommandPositionX, R_CommandPositionY, R_CommandPositionZ, R_CommandPositionA, R_CommandPositionC];
            workPositionLabels = [R_WorkPositionX, R_WorkPositionY, R_WorkPositionZ, R_WorkPositionA, R_WorkPositionC];
            positionSlots = positionAxes.Select(a => Array.FindIndex(mainToolPath.Axes, p => p.Name == a.Name)).ToArray();
        }
        // Feed is linear XYZ speed; angular limits must not contribute to mm/s.
        // The gantry slave YG is not a second path coordinate.
        double squaredLimit = 0;
        for (int i = 0; i < 3; i++)
            squaredLimit += positionAxes[i].VelocityLimit * positionAxes[i].VelocityLimit;
        maximumMachineFeed = Math.Sqrt(squaredLimit);
        displayedMachineFeed = double.NaN;
        R_ProgramView3D.SetTable(new Vector3((float)positionAxes[0].MinCourse, (float)positionAxes[1].MinCourse, (float)positionAxes[2].MinCourse),
            new Vector3((float)positionAxes[0].MaxCourse, (float)positionAxes[1].MaxCourse, (float)positionAxes[2].MaxCourse));
    }

    /// <summary>Loads on a worker, then publishes controller-accepted geometry on the UI thread without executing it.</summary>
    /// <remarks>May run during homing while the main path is idle; the task ends after publication or cancellation cleanup.</remarks>
    public async Task OpenFileAsync(string fileName)
    {
        if (!CanOpenProgram) return;
        NTLog.Report($"Opening G-code requested: {Path.GetFullPath(fileName)}");
        requestedProgramStart = null;
        currentFile = null;
        try
        {
            Task<bool> loading = gCodeInterpreter.OpenFileAsync(fileName);
            RefreshProgramLoadButton();
            RequestRuntimeUiRefresh();
            UI.Update();
            bool accepted = await loading;
            if (runtimeDisposed || IsDisposed) return;
            if (!accepted)
            {
                currentFile = null;
                R_ProgramPath.Text = NTLocalization.Format(
                    RouterXYZACRTCPTexts.ProgramRejected, Path.GetFullPath(fileName));
                NTLog.Error($"RouterXYZAC+RTCP program rejected: {fileName}{Environment.NewLine}{gCodeInterpreter.BlockingDiagnosticMessage}");
                return;
            }
            currentFile = Path.GetFullPath(fileName);
            generalLinks.RecentFileLink.Value = currentFile;
            ReportOperation("G-code opened successfully");
            R_ProgramPath.Text = currentFile;
            // End() changes managed producer state without requiring an RT status
            // edge. Re-query command bindings immediately so Run reflects the
            // newly sealed program even while the controller remains stationary.
            RequestRuntimeUiRefresh();
        }
        catch (OperationCanceledException)
        {
            NTLog.Report($"Opening G-code cancelled: {fileName}");
            if (!runtimeDisposed && !IsDisposed) R_ProgramPath.Text = T(RouterXYZACRTCPTexts.ProgramLoadCancelled);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            if (runtimeDisposed || IsDisposed) return;
            currentFile = null;
            R_ProgramPath.Text = T(RouterXYZACRTCPTexts.ProgramLoadFailed);
            NTLog.Error($"RouterXYZAC+RTCP program load failed: {error.Message}");
            NTLog.Report(error);
            sMessage.Show(this, new SMessageRequest(T(RouterXYZACRTCPTexts.ProgramLoadFailed))
            {
                Title = T(RouterXYZACRTCPTexts.GCodeTitle),
                Severity = SMessageSeverity.Error,
                TechnicalDetails = error.Message,
                Exception = error,
                WriteToReport = false
            }, RuntimeTheme);
        }
        finally
        {
            if (!runtimeDisposed && !IsDisposed) { RefreshProgramLoadButton(); UI.Update(); }
            RequestRuntimeUiRefresh();
        }
    }

    /// <summary>Supplies operator intent and machine operations to the interpreter execution service.</summary>
    internal bool RunMainProgram()
    {
        if (ToolLengthBlocksProgram || controller.MotionController.RTCP.SwitchStatus == RtcpSwitchStatus.Pending ||
            controllerLinks.IsPauseSwitchOpen || IsProgramLoading) return false;
        try
        {
            RouterStartRequest request = ResolveProgramStartRequest();
            ReportOperation($"Run requested: Origin={request.Origin}; RequestedLine={request.SourceLine}; FirstCuttingEntry={request.FirstCuttingEntry}");
            gCodeInterpreter.Start(request,
                controllerLinks.RunOrPositionMainToolPath, controllerLinks.ReturnToWork);
            ReportOperation($"Run accepted: SelectedStartLine={gCodeInterpreter.SelectedStartLine}; positioning or execution pending");
            requestedProgramStart = null;
            controllerLinks.RunOperatorPlc("Run");
            return true;
        }
        catch (NTException error)
        {
            ShowProgramWarning(new(error.Code, error.Message));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            ShowProgramWarning(new(NTErrorCode.MotionCommandRejected, error.Message));
        }
        RequestRuntimeUiRefresh();
        return false;
    }

    /// <summary>
    /// Records the double-clicked block as the restart line while stopped. Rows before it are shown completed;
    /// when configured, the interpreter advances from that line to the first accepted cutting entry on Run.
    /// </summary>
    private void ProgramBlockDoubleClicked(object? sender, GCodeBlockEventArgs e)
    {
        if (!runtimeInitialized || runtimeDisposed || IsProgramLoading || mainToolPath.IsProgramActive ||
            !mainToolPath.IsIdle || !mainToolPath.IsStopped ||
            !ReferenceEquals(e.Document, R_GCodeView.Document)) return;
        requestedProgramStart = RouterStartRequest.FromSourceBlock(
            e.BlockNumber, generalLinks.StartFromNextCuttingLine);
        gCodeInterpreter.MarkCompletedBefore(e.BlockNumber);
        ReportOperation($"Start line selected by double-click: {e.BlockNumber}; FirstCuttingEntry={generalLinks.StartFromNextCuttingLine}");
        RequestRuntimeUiRefresh();
    }

    private void ProgramDocumentChanged(object? sender, EventArgs e)
    {
        requestedProgramStart = null;
        if (!R_GCodeExpandedView.IsDisposed) R_GCodeExpandedView.Document = R_GCodeView.Document;
        if (currentFile is not null && !CurrentDocumentMatchesOpenedFile(R_GCodeView.Document))
            currentFile = null;
    }

    private bool CurrentDocumentMatchesOpenedFile(GCodeDocument document)
    {
        // A document may supply FileName without having been opened through this form.
        if (currentFile is null || string.IsNullOrWhiteSpace(document.FileName)) return false;
        try
        {
            return string.Equals(currentFile, Path.GetFullPath(document.FileName),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Displays measured speed and its share of the cached TP0 resultant limit.</summary>
    internal void DisplayMachineFeed(double feed)
    {
        R_MachineFeedValue.Value = maximumMachineFeed > 0 && double.IsFinite(maximumMachineFeed)
            ? Math.Clamp(feed / maximumMachineFeed * 100, 0, 100) : 0;
        double roundedFeed = Math.Round(feed, 1);
        if (roundedFeed == displayedMachineFeed) return;
        displayedMachineFeed = roundedFeed;
        R_MachineFeedValue.Text = T(RouterXYZACRTCPTexts.MachineFeedPlaceholder)
            .Replace("000.0", roundedFeed.ToString("000.0", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Synchronizes every visible consequence of Reference, tool Offset and
    /// LiveOffset changes from their single authoritative ToolPath state.
    /// </summary>
    private void SynchronizeWorkCoordinatePresentation(bool positionChanged)
    {
        // One transform sample feeds both the scene and numeric work positions.
        // Offsets can change without an Axis/ToolPath status edge, so retain
        // bounded sampling but skip numeric work on unchanged ticks.
        var frame = gCodeInterpreter.SynchronizeWorkCoordinateScene(lastCommandPositions[3], lastCommandPositions[4]);
        if (!positionChanged && frame == lastWorkFrame) return;
        lastWorkFrame = frame;
        var xyz = frame.GetWorkPosition(new(lastCommandPositions[0], lastCommandPositions[1], lastCommandPositions[2]));
        ReadOnlySpan<double> workPositions = [xyz.X, xyz.Y, xyz.Z,
            lastCommandPositions[3] - frame.Origin.A, lastCommandPositions[4] - frame.Origin.C];
        for (int i = 0; i < positionAxes.Length; i++)
        {
            double work = workPositions[i];
            if (work == lastWorkPositions[i]) continue;
            lastWorkPositions[i] = work;
            workPositionLabels[i].Text = FormatAxisPosition(positionAxes[i].Name, work);
        }
    }

    private ToolPathStoppedEventArgs? latestProgramStop;

    /// <summary>Background notification: retain only the newest execution and wake the existing UI refresh.</summary>
    private void ControllerToolPathStopped(object? sender, ToolPathStoppedEventArgs e)
    {
        if (e.ToolPathIndex != 0 || runtimeDisposed) return;
        ToolPathStoppedEventArgs? previous;
        do
        {
            previous = Volatile.Read(ref latestProgramStop);
            if (previous is not null && previous.ExecutionId >= e.ExecutionId) return;
        }
        while (!ReferenceEquals(Interlocked.CompareExchange(ref latestProgramStop, e, previous), previous));
        RequestRuntimeStateRefresh();
    }

    /// <summary>Rejects delayed notifications after a new Run, cursor selection, file replacement or disconnect.</summary>
    private bool IsCurrentProgramStop(ToolPathStoppedEventArgs? stop)
        => stop is not null && IsMachineOnline && !IsProgramLoading &&
            stop.ExecutionId == mainToolPath.ExecutionId &&
            stop.PreparationGeneration == controller.MotionController.ToolPaths[0].PreparationGeneration &&
            stop.CoordinateGeneration == mainToolPath.CoordinateResetGeneration &&
            mainToolPath.IsIdle && !mainToolPath.IsProgramActive;

    private bool HasUnfinishedProgram()
    {
        if (gCodeInterpreter.Geometry is null || !mainToolPath.IsEnded || mainToolPath.BlockCount == 0)
            return false;

        ToolPathStoppedEventArgs? stop = Volatile.Read(ref latestProgramStop);
        if (IsCurrentProgramStop(stop)) return stop!.Reason != ToolPathStopReason.Completed;

        // Standstill can be visible before the stop notification reaches the form.
        // Preparing or selecting a start line does not fill the runtime buffer.
        return mainToolPath.HasBlocksAvailableForRun &&
            (mainToolPath.BufferedBlockCount > 0 || mainToolPath.RemainingDistance > 0);
    }

    /// <summary>Progress follows block feedback; completion comes only from the controller's termination observation.</summary>
    private void RefreshProgramExecution()
    {
        int currentBlock = mainToolPath.CurrentBlock;
        ToolPathStoppedEventArgs? stop = Volatile.Read(ref latestProgramStop);
        bool currentStop = IsCurrentProgramStop(stop);
        bool completed = currentStop && stop!.Reason == ToolPathStopReason.Completed;
        gCodeInterpreter.UpdateExecution(currentBlock, completed);
        if (currentStop && stop!.ExecutionId != reportedStopExecution)
        {
            reportedStopExecution = stop.ExecutionId;
            ReportOperation(stop.Reason switch
            {
                ToolPathStopReason.Completed => "Program completed naturally",
                ToolPathStopReason.StopRequested => "Program stopped after a requested stop",
                ToolPathStopReason.Fault => "Program execution ended with an observed axis, coupling or PLC fault",
                _ => "Program execution ended with unknown stop origin"
            });
            // Consume the completion before attempting motion: an unavailable park is
            // reported by the existing command and must never become a delayed retry.
            // Spindle sequencing remains entirely in the main G-code program.
            if (completed && generalLinks.ParkEnabledLink.Value)
            {
                controllerLinks.GoToPark();
                RequestRuntimeStateRefresh();
            }
        }
    }

    private static string FormatAxisPosition(string axisName, double position)
    {
        if (Math.Abs(position) < 0.0005) position = 0; // Avoid displaying negative zero at the readout precision.
        string spacing = position < 0.0 ? " " : "  ";
        return axisName + spacing + position.ToString("0000.000", CultureInfo.InvariantCulture) + (axisName is "A" or "C" ? " °" : " mm");
    }
}

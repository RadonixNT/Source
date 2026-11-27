using NT_Interpreter.Shared.Router.Controller;
using NT_Interpreter.Shared.Router;
using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;
using NT.Localization;
using NT.Tools;
using NT_Interpreter.Interpreters.RouterXYZTC;
using NT_Interpreter.Interpreters.RouterXYZTC.Controller;
using NT_Interpreter.WinForms;
using OpenTK.Mathematics;
using System.Globalization;
using System.Text;

namespace RouterXYZTC;

/// <summary>
/// Owns operator file/edit/start intent and its machining presentation. Preparing
/// or selecting a source line never starts motion. The execution session plans
/// entry state; TP2 prepares the tool, TP1 positions, and M100 hands off to TP0.
/// Periodic refresh is owned by the Runtime partial, not by independent UI timers.
/// </summary>
public partial class MainForm
{
    private RouterSpindleMonitor? spindleMonitor;
    private static readonly RouterToolStartCodes toolStartCodes = new(RouterAuxiliaryCode.Parse("M6"),
        new HashSet<RouterAuxiliaryCode> { RouterAuxiliaryCode.Parse("M3"), RouterAuxiliaryCode.Parse("M4") });

    private AxisWrapper[] positionAxes = [];
    private sLabel[] commandPositionLabels = [];
    private sLabel[] workPositionLabels = [];
    private double[] lastCommandPositions = [double.NaN, double.NaN, double.NaN];
    private readonly MachineFeedMonitor machineFeedMonitor = new();
    private double displayedMachineFeed = double.NaN;
    private double maximumMachineFeed;
    private double[] lastWorkPositions = [double.NaN, double.NaN, double.NaN];
    private int[] positionSlots = [];
    private string? currentFile;
    private bool lastHeadPositioningActive;
    private bool lastToolChangerActive;
    private RouterPosition lastWorkTranslation = new(double.NaN, double.NaN, double.NaN);
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

    private RouterToolStartState CaptureToolStartState() => new(
        toolChangerLinks.AutomaticToolChangeEnabled.Value, toolChangerLinks.CurrentTool.Value,
        SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindleToolClamped"]),
        !SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindleToolReleased"], false),
        number => toolChangerLinks.IsToolEnabled(number), toolStartCodes);

    private void ShowProgramWarning(NTProgramDiagnostic diagnostic)
    {
        ReportOperation($"Program warning: Code={diagnostic.Code}; Message={diagnostic.Message}; Detail={diagnostic.Detail}");
        R_ProgramProgress.Text = diagnostic.Message;
        diagnostic.Report();
        sMessage.Show(this, SMessageRequest.FromError(diagnostic.Code, SMessageSeverity.Warning, diagnostic.Detail) with
        {
            Title = T(RouterXYZTCTexts.ProgramTitle),
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
        bool Input(string name) => controller.PLC.IOMappings[name].GetLogic();
        bool OptionalInput(string name) => SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings[name]);
        bool Flag(string name) => ((LOGIC)controller.PLC.ApplicationVariables[name]).Value;
        return new(toolChangerLinks.CurrentTool.Value, Flag("SpindleFeedbackConfigured"),
            OptionalInput("SpindleToolClamped"), !SpindleInterlocks.OptionalInputSatisfied(controller.PLC.IOMappings["SpindleToolReleased"], false), OptionalInput("SpindlePistonRetracted"),
            OptionalInput("PressureSensor"), OptionalInput("SpindleThermalOK"), true,
            OptionalInput("SpindleDriveReady"), false, Input("SpindleToolRelease"),
            Flag("SpindleReverseConfigured"), toolChangerLinks.IsActive);
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
        !toolChangerLinks.IsActive && (!controller.IsBusy || controllerLinks.IsHomingActive());

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
        GCodeDocument document = R_GCodeView.Document;
        int initialLine = requestedProgramStart?.SourceLine ??
            (R_GCodeView.SelectedLineIndex >= 0 ? R_GCodeView.SelectedLineIndex + 1 : 1);
        string? sourceFilePath = CurrentDocumentMatchesOpenedFile(document) ? currentFile : null;
        string title = sourceFilePath is null
            ? T(RouterXYZTCTexts.SelectStartLineTitle)
            : NTLocalization.Format(RouterXYZTCTexts.SelectStartLineFileTitle, sourceFilePath);
        using var dialog = new GCodeStartLineDialog(document, initialLine, RuntimeTheme,
            title,
            NTLocalization.Format(RouterXYZTCTexts.LineCount, document.LineCount),
            T(RouterXYZTCTexts.Select),
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
        // Preserve the previewed line in the main viewer even if the operator cancels.
        if (ReferenceEquals(R_GCodeView.Document, document) && dialog.SelectedPreviewLineIndex >= 0)
            R_GCodeView.SelectedLineIndex = dialog.SelectedPreviewLineIndex;
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
            NTLocalization.Format(RouterXYZTCTexts.EditGCodeTitle, fileName),
            T(RouterXYZTCTexts.Save),
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
            NTLog.Error($"RouterXYZ+TC G-code save failed: {error.Message}");
            sMessage.Show(this, new SMessageRequest(T(RouterXYZTCTexts.GCodeSaveFailed))
            {
                Title = T(RouterXYZTCTexts.GCodeTitle),
                Severity = SMessageSeverity.Error,
                TechnicalDetails = error.Message,
                Exception = error,
                WriteToReport = false
            }, RuntimeTheme);
            return false;
        }
    }

    private void InitializeMachineView()
    {
        // Refresh logical-axis bindings after an accepted offline topology change.
        {
            positionAxes = [controller.GetAxisByName("X"), controller.GetAxisByName("Y"), controller.GetAxisByName("Z")];
            commandPositionLabels = [R_CommandPositionX, R_CommandPositionY, R_CommandPositionZ];
            workPositionLabels = [R_WorkPositionX, R_WorkPositionY, R_WorkPositionZ];
            positionSlots = positionAxes.Select(a => Array.FindIndex(mainToolPath.Axes, p => p.Name == a.Name)).ToArray();
        }
        // Cache only the TP0 member-axis limits when the profile is applied.
        // The gantry slave YG is not a second path coordinate.
        double squaredLimit = 0;
        foreach (AxisWrapper axis in mainToolPath.Axes)
            squaredLimit += axis.VelocityLimit * axis.VelocityLimit;
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
                    RouterXYZTCTexts.ProgramRejected, Path.GetFullPath(fileName));
                NTLog.Error($"RouterXYZ+TC program rejected: {fileName}{Environment.NewLine}{gCodeInterpreter.BlockingDiagnosticMessage}");
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
            if (!runtimeDisposed && !IsDisposed) R_ProgramPath.Text = T(RouterXYZTCTexts.ProgramLoadCancelled);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            if (runtimeDisposed || IsDisposed) return;
            currentFile = null;
            R_ProgramPath.Text = T(RouterXYZTCTexts.ProgramLoadFailed);
            NTLog.Error($"RouterXYZ+TC program load failed: {error.Message}");
            NTLog.Report(error);
            sMessage.Show(this, new SMessageRequest(T(RouterXYZTCTexts.ProgramLoadFailed))
            {
                Title = T(RouterXYZTCTexts.GCodeTitle),
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
        if (controllerLinks.IsPauseSwitchOpen || IsProgramLoading || IsTransferActive) return false;
        try
        {
            RouterStartRequest request = ResolveProgramStartRequest();
            ReportOperation($"Run requested: Origin={request.Origin}; RequestedLine={request.SourceLine}; FirstCuttingEntry={request.FirstCuttingEntry}");
            gCodeInterpreter.Start(request,
                CaptureToolStartState(), controllerLinks.RunOrPositionMainToolPath, controllerLinks.ReturnToWork,
                toolChangerLinks.PrepareStartTool);
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

    private bool PrepareToolChangeContinuation(int completedSourceBlock)
    {
        try
        {
            gCodeInterpreter.PrepareToolChangeContinuation(completedSourceBlock, generalLinks.StartFromNextCuttingLine);
            return true;
        }
        catch (NTException error)
        {
            ShowProgramWarning(new(error.Code, error.Message));
            return false;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            ShowProgramWarning(new(NTErrorCode.MotionCommandRejected, error.Message));
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
        R_MachineFeedValue.Text = T(RouterXYZTCTexts.MachineFeedPlaceholder)
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
        RouterPosition translation = gCodeInterpreter.SynchronizeWorkCoordinateScene();
        if (!positionChanged && translation == lastWorkTranslation) return;
        lastWorkTranslation = translation;
        ReadOnlySpan<double> xyz = [translation.X, translation.Y, translation.Z];
        for (int i = 0; i < positionAxes.Length; i++)
        {
            double work = lastCommandPositions[i] - xyz[i];
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

    /// <summary>Uses the retained execution cursor and controller stop reason to decide whether to confirm exit.</summary>
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
        string spacing = position < 0.0 ? " " : "  ";
        return axisName + spacing + position.ToString("0000.000", CultureInfo.InvariantCulture) + " mm";
    }
}

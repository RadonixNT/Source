using NT_Interpreter.Shared.Router.Controller;
using NT_Interpreter.Shared.Router;
using NT.Controller;
using NT.Diagnostics;
using NT.Localization;
using NT.Tools;
using NT_Interpreter.Interpreters.RouterXYZACTC;
using NT_Interpreter.Interpreters.RouterXYZACTC.Controller;
using NT_Interpreter.Shared.Router.WinForms;
using MacrosArg = NT.Controller.Macros.MacrosArg;
using PLCProgram = NT.Controller.PLC.PLCProgram;
using PLCProgramExitReason = NT.Controller.PLC.PLCProgramExitReason;

namespace RouterXYZACTC;

/// <summary>
/// Composes RouterXYZAC+TC machine context, auxiliary bindings and preview synchronization.
/// Parsing, execution planning and presentation are delegated to NT-Interpreter services.
/// </summary>
internal sealed class GCodeInterpreter : IDisposable
{
    private RouterToolPathBuilder builder;
    private readonly Core controllerCore;
    private readonly RouterProgramPresenter presenter;
    private readonly ReferenceLinks references;
    private readonly RouterLinks rollers;
    private readonly Action loadingStateChanged;
    private readonly Func<double> traverseVelocityProvider;
    private readonly Func<string, double> parkPositionProvider;
    private readonly sProgressBar progress;
    private readonly s3DView sceneView;
    private int timingBlock = -1, completedNominalMilliseconds;
    private long timingGeneration = -1;
    private bool timingCompleted;
    private (long Elapsed, long Remaining, long Total, double Percent)? displayedTiming;
    private string loadingFileName = "";
    private RouterAuxiliaryDefinition[] auxiliaryDefinitions;
    private readonly HashSet<string> reportedIgnoredCodes = new(StringComparer.Ordinal);
    // Map semantic XYZAC to compact path slots once. Gantry motors are not
    // extra job coordinates; motor slot order is independent of path order.
    private readonly int[] coordinateSlots;
    private RouterExecutionSession execution;
    private RouterPosition lastTranslation = new(double.NaN, double.NaN, double.NaN);
    private bool disposed;
    private CancellationTokenSource? loadCancellation;
    private int loadProgress, displayedLoadProgress = -1;
    private bool displayedLoading;
    private int preparedInitialReference;
    internal bool IsLoading { get; private set; }
    internal Task<bool> PendingLoad { get; private set; } = Task.FromResult(false);
    internal bool IsCancellingLoad => IsLoading && loadCancellation?.IsCancellationRequested == true;
    internal void CancelLoad()
    {
        if (!IsLoading || IsCancellingLoad) return;
        loadCancellation?.Cancel();
        RefreshLoadingProgress();
    }

    // Stage (0..3) and percentage (0..100) share one atomic mailbox. The existing
    // UI refresh timer samples the latest value, avoiding per-line UI callbacks.
    private sealed class LoadProgress(GCodeInterpreter owner, int stage) : IProgress<int>
    {
        public void Report(int value) => Volatile.Write(ref owner.loadProgress, stage * 1000 + Math.Clamp(value, 0, 100));
    }

    internal bool RefreshLoadingProgress()
    {
        bool changed = displayedLoading != IsLoading;
        displayedLoading = IsLoading;
        if (!IsLoading) return changed;
        if (IsCancellingLoad)
        {
            progress.Text = NTLocalization.Get(RouterXYZACTCTexts.ProgramLoadingCancelled);
            return changed;
        }
        int value = Volatile.Read(ref loadProgress);
        if (displayedLoadProgress == value) return changed;
        displayedLoadProgress = value;
        int stage = value / 1000, percent = value % 1000;
        // Stage weights are presentation only, not a time or work-remaining estimate.
        int totalPercent = GetLoadProgressPercent(stage, percent);
        progress.Value = totalPercent;
        progress.Text = stage switch
        {
            0 or 1 => NTLocalization.Format(RouterXYZACTCTexts.OpeningGCode, loadingFileName, totalPercent),
            2 => NTLocalization.Format(RouterXYZACTCTexts.PreparingToolPath, totalPercent),
            _ => NTLocalization.Get(RouterXYZACTCTexts.PreparingPreview)
        };
        return changed;
    }

    internal static int GetLoadProgressPercent(int stage, int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        return (int)Math.Round(stage switch
        {
            0 => 0,
            1 => percent * .50,
            2 => 50 + percent * .50,
            _ => 100
        });
    }

    internal GCodeInterpreter(Core controller, sGCodeView textView, s3DView sceneView,
        sProgressBar progress, Func<double> traverseVelocityProvider, ReferenceLinks references, RouterLinks rollers,
        Action loadingStateChanged, Func<string, double> parkPositionProvider)
    {
        controllerCore = controller;
        this.references = references;
        this.rollers = rollers;
        this.loadingStateChanged = loadingStateChanged;
        this.traverseVelocityProvider = traverseVelocityProvider;
        this.parkPositionProvider = parkPositionProvider ?? throw new ArgumentNullException(nameof(parkPositionProvider));
        this.progress = progress;
        this.sceneView = sceneView;
        sceneView.StatusBarText = Initialize.InterfaceName;
        controller.Macros.AddProgram("M30", ExecuteM30Async);
        (auxiliaryDefinitions, Dictionary<RouterAuxiliaryCode, RouterProgramBinding> auxiliaryBindings) =
            CreateAuxiliaryCatalog(controller);
        // Standard G behavior stays in NT-Interpreter. Interface-owned M/S/T/P
        // programs are discovered by canonical name and retain only restart metadata here.
        builder = new RouterToolPathBuilder(controller, 0,
            new Dictionary<RouterCommandKind, RouterProgramBinding>(), auxiliaryBindings);
        execution = new RouterExecutionSession(builder);
        presenter = new RouterProgramPresenter(textView, sceneView, progress);
        coordinateSlots = RouterToolPathBuilder.ResolveAxes(builder.Path.Axes.Select(a => a.Name).ToArray(), "XYZAC");
        SynchronizeWorkCoordinateScene();
        references.ValueChanged += ReferenceChanged;
        references.SelectionChanged += ReferenceChanged;
    }

    internal void RefreshAuxiliaryCatalog()
    {
        if (IsLoading || !controllerCore.ConnectionState.AllowsOfflineConfiguration()
            || controllerCore.ToolPaths.Any(path => path.BlockCount != 0))
            throw new InvalidOperationException("Clear all ToolPaths and disconnect before changing the auxiliary catalog.");
        RefreshClearedToolPath();
        var catalog = CreateAuxiliaryCatalog(controllerCore);
        auxiliaryDefinitions = catalog.Definitions;
        builder = new RouterToolPathBuilder(controllerCore, 0,
            new Dictionary<RouterCommandKind, RouterProgramBinding>(), catalog.Bindings);
        execution = new RouterExecutionSession(builder);
    }

    internal RouterCompilation? Compilation { get; private set; }
    internal void Clear()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsLoading) throw new InvalidOperationException("Cannot clear a program while it is being prepared.");
        // Clear accepted controller rows first; never remove the preview if this is rejected.
        execution.Clear();
        RefreshClearedToolPath();
    }

    internal void RefreshClearedToolPath()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsLoading) throw new InvalidOperationException("Program preparation is active.");
        execution.ForgetClearedProgram();
        Compilation = null;
        LoadDiagnostics = [];
        rollers.SetCuttingBounds(null);
        presenter.Clear();
        completedBeforeBlock = 1;
        displayedCursorGeneration = builder.Path.CoordinateResetGeneration;
        timingGeneration = -1;
        timingBlock = -1;
        completedNominalMilliseconds = 0;
        timingCompleted = false;
        displayedTiming = null;
        progress.Text = string.Empty;
        sceneView.StatusBarText = Initialize.InterfaceName;
    }
    internal ToolPathGeometrySnapshot? Geometry => execution.Geometry;
    internal GCodeToolPathMap? PreviewMap => presenter.PreviewMap;
    internal bool CalculateDepthIntensity
    {
        get => presenter.CalculateDepthIntensity;
        set => presenter.CalculateDepthIntensity = value;
    }
    internal float MinimumDepthIntensity
    {
        get => presenter.MinimumDepthIntensity;
        set => presenter.MinimumDepthIntensity = value;
    }
    internal int LastSourceBlock => execution.LastSourceBlock;
    internal IReadOnlyList<RouterDiagnostic> LoadDiagnostics { get; private set; } = [];
    internal string BlockingDiagnosticMessage => string.Join(Environment.NewLine,
        LoadDiagnostics.Where(d => d.IsBlocking).Select(d => $"Line {d.Source.LineNumber}, column {d.Source.Column + 1}: {d.Message}"));

    /// <summary>Starts background preparation and publishes only a completed result on the UI thread.</summary>
    /// <returns>True for an accepted program; false for blocking diagnostics. I/O and cancellation failures propagate.</returns>
    public Task<bool> OpenFileAsync(string fileName)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsLoading) throw new InvalidOperationException("Program preparation is already active.");
        return PendingLoad = LoadAsync(fileName, null);
    }

    /// <summary>Recompiles retained text after accepted reference/profile changes, without rereading the file.</summary>
    /// <remarks>Returns immediately; PendingLoad tracks the asynchronous replacement and errors are logged.</remarks>
    internal async void Rebuild()
    {
        if (Compilation is null || disposed || IsLoading) return;
        RouterDocument source = Compilation.Document;
        try
        {
            PendingLoad = LoadAsync(source.FileName ?? "Router program", source);
            if (!await PendingLoad)
                NTLog.Error($"RouterXYZAC+TC program rebuild rejected: {source.FileName}{Environment.NewLine}{BlockingDiagnosticMessage}");
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!disposed) progress.Text = NTLocalization.Format(
                RouterXYZACTCTexts.ReloadRequired, error.Message.Split('\n')[0]);
            NTLog.Error($"RouterXYZAC+TC reference update: {error.Message}");
        }
    }

    private async Task<bool> LoadAsync(string fileName, RouterDocument? retainedSource)
    {
        if (!builder.Path.IsIdle || builder.Path.IsProgramActive)
            throw new InvalidOperationException("Stop the work program before loading a file.");
        (RouterCompiler compiler, RouterMachineContext context) = CaptureContext();
        int initialReference = references.SelectedIndex;
        bool depth = presenter.CalculateDepthIntensity;
        float minimumDepth = presenter.MinimumDepthIntensity;
        using var cancellation = new CancellationTokenSource();
        loadCancellation = cancellation;
        loadingFileName = Path.GetFullPath(fileName);
        IsLoading = references.ProgramPreparationActive = true;
        rollers.SetCuttingBounds(null);
        displayedLoadProgress = -1;
        Volatile.Write(ref loadProgress, 0);
        presenter.Clear();
        sceneView.StatusBarText = Initialize.InterfaceName;
        timingCompleted = false;
        displayedTiming = null;
        Compilation = null;
        LoadDiagnostics = [];
        if (retainedSource is null) reportedIgnoredCodes.Clear();
        RefreshLoadingProgress();
        try
        {
            loadingStateChanged();
            // The worker owns TP0 exclusively through clear, compilation, path emission
            // and preview preparation. Controls are touched only after await.
            var result = await Task.Run(async () =>
            {
                execution.Clear();
                var compileProgress = new LoadProgress(this, 1);
                RouterCompilation compilation = retainedSource is null
                    ? await compiler.CompileFileAsync(fileName, context, cancellation.Token, compileProgress).ConfigureAwait(false)
                    : compiler.Recompile(retainedSource, context, cancellation.Token, compileProgress);
                // A file with one explicit work system starts at that system's origin,
                // independently of the UI selector. Omitted axes then translate with it too.
                if (compilation.Program is { UsesInitialReference: false, UsesMachineCoordinates: false } program &&
                    program.ExplicitReferenceMask != 0 &&
                    (program.ExplicitReferenceMask & (program.ExplicitReferenceMask - 1)) == 0)
                {
                    int index = System.Numerics.BitOperations.TrailingZeroCount((uint)program.ExplicitReferenceMask);
                    RouterPosition origin = context.GetWorkOrigin(index);
                    if (origin != context.InitialPosition)
                    {
                        context = new RouterMachineContext(origin, context.RapidVelocity,
                            Enumerable.Range(0, 6).Select(context.GetWorkOrigin).ToArray(), context.InitialFeedVelocity,
                            g28Position: context.G28Position, g30Position: context.G30Position);
                        compilation = compiler.Recompile(compilation.Document, context, cancellation.Token, compileProgress);
                    }
                }
                cancellation.Token.ThrowIfCancellationRequested();
                IReadOnlyList<RouterDiagnostic> diagnostics = compilation.Diagnostics;
                RouterPreparedPreview? preview = null;
                ToolPathGeometrySnapshot? geometry = null;
                if (compilation.Success)
                {
                    try
                    {
                        new LoadProgress(this, 2).Report(0);
                        builder.Path.Reference = ToCompact(context.InitialPosition);
                        var prepared = execution.Load(compilation.Program!, cancellation.Token, new LoadProgress(this, 2));
                        diagnostics = Array.AsReadOnly(compilation.Diagnostics.Concat(prepared.Diagnostics).ToArray());
                        geometry = prepared.Geometry;
                    }
                    catch (RouterPreparationException error)
                    { diagnostics = Array.AsReadOnly(compilation.Diagnostics.Concat(error.Diagnostics).ToArray()); }
                }
                cancellation.Token.ThrowIfCancellationRequested();
                return (compilation, diagnostics, geometry, preview);
            }, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (result.geometry is not null)
            {
                // Complete the graphical scale before starting detached preview work.
                // The UI timer keeps the full bar while subsequent stages change text only.
                new LoadProgress(this, 3).Report(0);
                RefreshLoadingProgress();
                progress.Update();
                result.preview = await Task.Run(() => RouterProgramPresenter.Prepare(
                    result.compilation, result.geometry, result.diagnostics, depth, minimumDepth, cancellation.Token), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(disposed, this);
            }
            rollers.SetCuttingBounds(result.preview is null ? null : execution.Geometry);
            Compilation = result.compilation;
            preparedInitialReference = initialReference;
            LoadDiagnostics = result.diagnostics;
            completedBeforeBlock = 1;
            displayedCursorGeneration = builder.Path.CoordinateResetGeneration;
            lastTranslation = new(double.NaN, double.NaN, double.NaN);
            if (result.preview is null) presenter.ShowDiagnostics(result.compilation, result.diagnostics);
            else presenter.Publish(result.preview);
            if (result.preview is not null)
            {
                // Publish clears execution progress; keep loading complete through fitting.
                progress.Value = 100;
                sceneView.StatusBarText = GetProgramStatus(result.compilation.Program!, execution.Geometry!, initialReference);
                timingGeneration = builder.Path.CoordinateResetGeneration;
                timingBlock = -1;
                completedNominalMilliseconds = 0;
            }
            ReportIgnoredCodes();
            SynchronizeWorkCoordinateScene();
            presenter.Fit();
            // Loading used the same control while the presenter's execution cache
            // remained at zero. Restore the actual value at the ownership handoff.
            if (result.preview is not null) progress.Value = 0;
            return result.preview is not null;
        }
        catch
        {
            // The worker has finished before any cleanup touches its path or UI state.
            execution.Clear();
            if (!disposed && !progress.IsDisposed)
            {
                progress.Value = 0;
                progress.Text = NTLocalization.Get(cancellation.IsCancellationRequested
                    ? RouterXYZACTCTexts.ProgramLoadCancelled
                    : RouterXYZACTCTexts.PreparationFailed);
            }
            throw;
        }
        finally
        {
            loadCancellation = null;
            IsLoading = references.ProgramPreparationActive = false;
            RefreshExecutionTiming();
            loadingStateChanged();
        }
    }

    /// <summary>Reads a controller snapshot in the compact TP0 order as semantic XYZAC.</summary>
    private RouterPosition ReadCoordinates(ReadOnlySpan<double> values)
        => new(values[coordinateSlots[0]], values[coordinateSlots[1]], values[coordinateSlots[2]], A: values[coordinateSlots[3]], C: values[coordinateSlots[4]]);

    private RouterPosition ReadOrigin(int index)
        => new(references.GetValue(index, coordinateSlots[0]), references.GetValue(index, coordinateSlots[1]),
            references.GetValue(index, coordinateSlots[2]), A: references.GetValue(index, coordinateSlots[3]), C: references.GetValue(index, coordinateSlots[4]));

    private double[] ToCompact(RouterPosition position)
    {
        var values = new double[5];
        values[coordinateSlots[0]] = position.X;
        values[coordinateSlots[1]] = position.Y;
        values[coordinateSlots[2]] = position.Z;
        values[coordinateSlots[3]] = position.A;
        values[coordinateSlots[4]] = position.C;
        return values;
    }

    /// <summary>
    /// Captures UI-owned settings before starting a worker. Compilation uses this
    /// immutable context throughout the load, never live Links from another thread.
    /// </summary>
    private (RouterCompiler, RouterMachineContext) CaptureContext()
    {
        var origins = new RouterPosition[ReferenceLinks.ReferenceCount];
        for (int index = 0; index < origins.Length; ++index)
            origins[index] = ReadOrigin(index);
        RouterPosition initial = origins[references.SelectedIndex];
        var context = new RouterMachineContext(initial, traverseVelocityProvider(), origins,
            initialFeedVelocity: Initialize.InitialRouterFeedVelocity,
            // Reuse the selected Reference and existing per-axis Park settings; no duplicate Links.
            g28Position: initial,
            g30Position: new RouterPosition(parkPositionProvider("X"), parkPositionProvider("Y"), parkPositionProvider("Z"), A: parkPositionProvider("A"), C: parkPositionProvider("C")));
        var profile = new RouterProfile(initialWorkCoordinate: references.SelectedIndex,
            maximumToolNumber: Initialize.ToolCount, auxiliaryDefinitions: auxiliaryDefinitions,
            unboundCodePolicy: RouterUnboundCodePolicy.ReportAndIgnore,
            legacyEncodingCodePage: 54936, g73RetractDistance: rollers.G73RetractDistance.Value);
        return (new RouterCompiler(profile), context);
    }

    internal int SelectedStartLine => execution.SelectedStartLine;

    internal void Start(RouterStartRequest request, RouterToolStartState tool, Func<bool> run, Func<bool> positionAndRun,
        Func<int, Func<bool>, bool> prepareTool)
    {
        if (IsLoading) throw new InvalidOperationException("Wait for program preparation to complete.");
        // The session may select a cursor before the machine handoff is rejected; keep presentation synchronized.
        try { execution.Start(request, tool, run, positionAndRun, prepareTool); }
        finally { SynchronizeWorkCoordinateScene(); }
    }

    internal void PrepareToolChangeContinuation(int completedSourceBlock, bool firstCuttingEntry)
    {
        execution.PrepareToolChangeContinuation(completedSourceBlock, firstCuttingEntry, RouterAuxiliaryCode.Parse("M6"));
        SynchronizeWorkCoordinateScene();
    }

    internal double GetLogicalReference(int compactAxisIndex) => execution.GetLogicalReference(compactAxisIndex);

    private RouterPosition GetLogicalReferencePosition()
    {
        // GetLogicalReference(slot) reads this same canonical row, but each
        // getter creates a complete snapshot. Read it once for all XYZAC slots.
        return ReadCoordinates(builder.Path.Reference);
    }
    private static (RouterAuxiliaryDefinition[] Definitions,
        Dictionary<RouterAuxiliaryCode, RouterProgramBinding> Bindings) CreateAuxiliaryCatalog(Core controller)
    {
        var settings = new Dictionary<RouterAuxiliaryCode, AuxiliarySettings>
        {
            [RouterAuxiliaryCode.Parse("S")] = new(RouterExecutionLifetime.Modal, "SpindleSpeed",
                RouterArgumentSource.OwnValue, Wait: false),
            [RouterAuxiliaryCode.Parse("M2")] = new(RouterExecutionLifetime.Point, "ProgramControl",
                TerminatesProgram: true),
            [RouterAuxiliaryCode.Parse("M3")] = new(RouterExecutionLifetime.Modal, "Spindle"),
            [RouterAuxiliaryCode.Parse("M4")] = new(RouterExecutionLifetime.Modal, "Spindle"),
            [RouterAuxiliaryCode.Parse("M5")] = new(RouterExecutionLifetime.Modal, "Spindle"),
            [RouterAuxiliaryCode.Parse("M6")] = new(RouterExecutionLifetime.RequiredState, "Tool",
                RouterArgumentSource.T),
            [RouterAuxiliaryCode.Parse("M30")] = new(RouterExecutionLifetime.Point, "ProgramControl",
                TerminatesProgram: true, Wait: false)
        };
        var definitions = new List<RouterAuxiliaryDefinition>();
        var bindings = new Dictionary<RouterAuxiliaryCode, RouterProgramBinding>();

        foreach (var program in controller.PLC.Programs)
            if (RouterAuxiliaryCode.TryParse(program.Name, out RouterAuxiliaryCode code))
                Add(code, ToolPath.ProgramType.PLC, program.Name);
        foreach (var program in controller.Macros.Programs)
            if (RouterAuxiliaryCode.TryParse(program.Name, out RouterAuxiliaryCode code))
                Add(code, ToolPath.ProgramType.Macro, program.Name);

        return (definitions.ToArray(), bindings);

        void Add(RouterAuxiliaryCode code, ToolPath.ProgramType type, string programName)
        {
            if (bindings.ContainsKey(code))
                throw new InvalidOperationException($"Auxiliary code '{code}' is registered as both PLC and Macro.");
            AuxiliarySettings setting = settings.TryGetValue(code, out AuxiliarySettings? configured)
                ? configured
                : AuxiliarySettings.Default(code);
            definitions.Add(new RouterAuxiliaryDefinition(code.ToString(), setting.ExecutionLifetime,
                setting.Group, Initialize.ProgramPriorities.GetValueOrDefault(code.ToString()),
                setting.ArgumentSource, setting.TerminatesProgram));
            bindings.Add(code, new RouterProgramBinding(type, programName, setting.Wait,
                setting.ArgumentSource != RouterArgumentSource.None));
        }
    }

    private sealed record AuxiliarySettings(RouterExecutionLifetime ExecutionLifetime,
        string Group, RouterArgumentSource ArgumentSource = RouterArgumentSource.None,
        bool TerminatesProgram = false, bool Wait = true)
    {
        internal static AuxiliarySettings Default(RouterAuxiliaryCode code) => new(
            RouterExecutionLifetime.Point, string.Empty,
            code.Letter is 'S' or 'T' or 'P' ? RouterArgumentSource.OwnValue : RouterArgumentSource.None);
    }

    private static async Task ExecuteM30Async(object sender, MacrosArg arg)
    {
        ToolPath source = arg.ToolPath
            ?? throw new InvalidOperationException("M30 requires a ToolPath request.");
        if (!arg.PLC.Programs.TryGetValue("M2", out PLCProgram? program) || program is null)
            throw new InvalidOperationException("M30 requires the M2 PLC program.");
        EnsureCurrentRequest();

        long previousActivation = program.ActivationId;
        if (!program.Run())
            throw new InvalidOperationException("M30 could not start the M2 PLC program.");

        long activation = 0;
        while (activation == 0)
        {
            await Task.Delay(20, arg.CancellationToken).ConfigureAwait(false);
            EnsureCurrentRequest();
            long candidate = program.ActivationId;
            if (candidate != 0 && candidate != previousActivation) activation = candidate;
        }
        while (program.CompletionId != activation)
        {
            await Task.Delay(20, arg.CancellationToken).ConfigureAwait(false);
            EnsureCurrentRequest();
        }
        if (program.LastExitReason != PLCProgramExitReason.Completed || program.LastResult != 0)
            throw new InvalidOperationException($"M2 ended with {program.LastExitReason} and result {program.LastResult}.");

        while (!source.IsIdle)
        {
            await Task.Delay(20, arg.CancellationToken).ConfigureAwait(false);
            EnsureCurrentRequest();
        }
        EnsureCurrentRequest();
        if (!source.Reset()) throw new InvalidOperationException("M30 could not reset its completed ToolPath.");

        void EnsureCurrentRequest()
        {
            if (arg.IsCancellationRequested) throw new OperationCanceledException(arg.CancellationToken);
            if (source.CoordinateResetGeneration != arg.ToolPathGeneration)
                throw new InvalidOperationException("M30 source ToolPath was replaced before completion.");
        }
    }

    private void ReportIgnoredCodes()
    {
        foreach (RouterDiagnostic diagnostic in LoadDiagnostics)
            if (diagnostic.Code == RouterDiagnosticCode.IgnoredAuxiliaryCode && diagnostic.CommandCode is { } code &&
                reportedIgnoredCodes.Add(code))
                NTLog.Error($"RouterXYZAC+TC G-code: {Compilation!.Document.FileName} | Line {diagnostic.Source.LineNumber}, " +
                    $"column {diagnostic.Source.Column + 1}: {diagnostic.Message}");
    }

    /// <summary>
    /// Returns the new translation when retained path rows remain valid, or null
    /// when changed work systems require semantic recompilation. A program using
    /// one work system can translate as a whole; mixed systems and G53 machine
    /// coordinates must retain their individually resolved endpoints.
    /// </summary>
    internal static RouterPosition? GetReferenceTranslation(RouterProgram program, int initialReference,
        int selectedReference, IReadOnlyList<RouterPosition> origins)
    {
        int mask = program.ExplicitReferenceMask;
        if (program.UsesInitialReference) mask |= 1 << selectedReference;
        int oldMask = program.ExplicitReferenceMask;
        if (program.UsesInitialReference) oldMask |= 1 << initialReference;
        bool single = (mask & (mask - 1)) == 0 && (oldMask & (oldMask - 1)) == 0;
        if (single && !program.UsesMachineCoordinates)
        {
            if (mask == 0) return origins[selectedReference];
            int current = System.Numerics.BitOperations.TrailingZeroCount((uint)mask);
            int previous = System.Numerics.BitOperations.TrailingZeroCount((uint)oldMask);
            RouterPosition before = program.Context.GetWorkOrigin(previous);
            RouterPosition after = origins[current];
            RouterPosition start = program.Context.InitialPosition;
            return new(start.X + after.X - before.X, start.Y + after.Y - before.Y,
                start.Z + after.Z - before.Z, A: start.A + after.A - before.A, C: start.C + after.C - before.C);
        }

        if (program.UsesInitialReference && selectedReference != initialReference) return null;
        for (int index = 0; index < ReferenceLinks.ReferenceCount; index++)
            if ((mask & (1 << index)) != 0 && origins[index] != program.Context.GetWorkOrigin(index))
                return null;
        // G53 endpoints stay fixed, but the first segment starts at the captured work origin.
        if (program.UsesMachineCoordinates &&
            origins[initialReference] != program.Context.GetWorkOrigin(initialReference)) return null;
        return program.Context.InitialPosition;
    }

    private void ReferenceChanged(object? sender, EventArgs e)
    {
        try
        {
            if (Compilation?.Program is { } program && !IsLoading)
            {
                RouterPosition[] origins = Enumerable.Range(0, ReferenceLinks.ReferenceCount)
                    .Select(index => ReadOrigin(index)).ToArray();
                RouterPosition? translation = GetReferenceTranslation(program,
                    preparedInitialReference, references.SelectedIndex, origins);
                if (translation is { } position)
                    builder.Path.Reference = ToCompact(position);
                else Rebuild();
            }
            else Rebuild();
            SynchronizeWorkCoordinateScene();
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException)
        {
            progress.Text = NTLocalization.Format(
                RouterXYZACTCTexts.ReloadRequired, error.Message.Split('\n')[0]);
            NTLog.Error($"RouterXYZAC+TC reference update: {error.Message}");
        }
    }

    /// <summary>
    /// Synchronizes every 3D consequence of the active work-coordinate transform.
    /// Tool compensation and live offsets move the displayed work origin together
    /// with the retained program geometry; callers must not update either separately.
    /// </summary>
    internal RouterPosition SynchronizeWorkCoordinateScene()
    {
        RouterPosition reference = GetLogicalReferencePosition();
        RouterPosition offset = ReadCoordinates(builder.Path.Offset);
        RouterPosition live = ReadCoordinates(builder.Path.LiveOffset);
        var translation = new RouterPosition(
            reference.X + offset.X + live.X,
            reference.Y + offset.Y + live.Y,
            reference.Z + offset.Z + live.Z, A: reference.A + offset.A + live.A, C: reference.C + offset.C + live.C);
        if (translation == lastTranslation) return translation;
        presenter.SetReferencePointer(translation);
        if (Geometry is not null) presenter.SetTransform(translation);
        lastTranslation = translation;
        return translation;
    }

    private int completedBeforeBlock = 1;
    private long displayedCursorGeneration;

    internal void UpdateExecution(int currentBlock, bool completed = false)
    {
        if (IsLoading) return;
        long generation = builder.Path.CoordinateResetGeneration;
        if (generation != displayedCursorGeneration)
        {
            // Selection preserves an accepted prefix; Reset/load clears it. Raw
            // CurrentBlock alone cannot distinguish reset from a replayed preamble.
            completedBeforeBlock = Math.Max(1, execution.SelectedStartLine);
            displayedCursorGeneration = generation;
        }
        presenter.UpdateExecution(currentBlock, completed, completedBeforeBlock);
        timingCompleted = completed;
        RefreshExecutionTiming(completed);
    }

    internal static string GetProgramStatus(RouterProgram program, ToolPathGeometrySnapshot geometry, int initialReference)
    {
        var dimensions = new List<string>(geometry.AxisCount);
        for (int axis = 0; axis < geometry.AxisCount; axis++)
        {
            string extent = geometry.HasCuttingBounds
                ? (geometry.CuttingMaximumPosition[axis] - geometry.CuttingMinimumPosition[axis])
                    .ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " mm"
                : "—";
            dimensions.Add(geometry.AxisNames[axis] + ": " + extent);
        }
        int mask = program.ExplicitReferenceMask;
        if (program.UsesInitialReference) mask |= 1 << initialReference;
        return string.Join(" | ", dimensions) + " | " + NTLocalization.Format(
            RouterXYZACTCTexts.ProgramCounts, program.Summary.RequiredTools.Count,
            System.Numerics.BitOperations.PopCount((uint)mask));
    }

    // Called by the existing UI timer. Only format when displayed whole seconds
    // or percentage changes; source timing lookup is cached by block/generation.
    internal void RefreshExecutionTiming(bool completed = false)
    {
        if (IsLoading || Compilation?.Program is null || Geometry is null) return;
        long generation = builder.Path.CoordinateResetGeneration;
        if (timingGeneration != generation)
        {
            timingGeneration = generation;
            timingBlock = -1;
            displayedTiming = null;
            timingCompleted = false;
        }
        completed |= timingCompleted;
        timingCompleted = completed;
        int block = builder.Path.CurrentBlock;
        if (block != timingBlock)
        {
            timingBlock = block;
            int preceding = Math.Max(completedBeforeBlock - 1, block - 1);
            completedNominalMilliseconds = builder.Path.GetEstimatedCumulativeTimeMilliseconds(Math.Max(0, preceding));
        }
        // All three values describe the original whole program's nominal timeline,
        // including the skipped prefix when selecting a start in the middle.
        long total = (long)Math.Ceiling(builder.Path.EstimatedDurationMilliseconds / 1000.0);
        long elapsed = completed ? total : Math.Min(total, completedNominalMilliseconds / 1000L);
        long remaining = total - elapsed;
        var state = (elapsed, remaining, total, progress.Value);
        if (displayedTiming == state) return;
        displayedTiming = state;
        progress.Text = NTLocalization.Format(RouterXYZACTCTexts.ExecutionTiming, progress.Value,
            FormatDuration(elapsed), FormatDuration(remaining), FormatDuration(total));
    }

    internal static string FormatDuration(long seconds)
        => $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";

    /// <summary>Marks only source rows before the requested restart block complete in text and preview.</summary>
    internal void MarkCompletedBefore(int blockNumber)
    {
        presenter.MarkCompletedBefore(blockNumber);
        completedBeforeBlock = blockNumber;
        displayedCursorGeneration = builder.Path.CoordinateResetGeneration;
        timingBlock = -1;
        displayedTiming = null;
        RefreshExecutionTiming();
    }

    public void Dispose()
    {
        if (disposed) return;
        references.ValueChanged -= ReferenceChanged;
        references.SelectionChanged -= ReferenceChanged;
        presenter.Dispose();
        if (!sceneView.IsDisposed) sceneView.StatusBarText = "";
        Compilation = null;
        disposed = true;
    }
}

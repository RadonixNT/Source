using Microsoft.Win32;
using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diag;
using NT.Diagnostics;
using NT.LinkManager;
using NT.Tools;
using NT.Localization;
using NT_Interpreter.WinForms;
using OpenTK.Mathematics;
using RouterXYZTC.Simulation;

namespace RouterXYZTC;

/// <summary>Application composition and lifetime. Designer construction never creates a controller or opens settings.</summary>
public partial class MainForm
{
    internal RegistryKey registryRoot = null!;
    private OperatorSwitches? operatorSwitches;
    private System.Windows.Forms.Timer? operatorSwitchTimer;
    private bool ownsRegistryRoot;
    internal LinkGroup baseGroup = null!;
    internal ControllerWrapper controller = null!;
    internal ControllerLinks controllerLinks = null!;
    internal GeneralLinks generalLinks = null!;
    internal RouterLinks routerLinks = null!;
    internal ZoneLinks zoneLinks = null!;
    internal ReferenceLinks referenceLinks = null!;
    internal ToolChangerLinks toolChangerLinks = null!;
    internal MaterialTransferMacro unloadMacro = null!;
    internal MaterialTransferMacro loadMacro = null!;
    internal bool IsTransferActive => unloadMacro?.IsActive == true || loadMacro?.IsActive == true;
    internal KeyboardLinks keyboardLinks = null!;
    private RouterLinkLocalization? linkLocalization;
    internal GCodeInterpreter gCodeInterpreter = null!;
    private LinkLocalizationOverrideProvider? localizationOverrides;
    private ToolPathWrapper mainToolPath = null!;
    private System.Windows.Forms.Timer? refreshTimer;
    private ScopeForm? scopeForm;
    private PlcMonitorForm? plcMonitorForm;
    private ReferenceEditorBinding? referenceEditorBinding;
    private SimulationHost? simulationHost;
    private readonly List<Link> runtimeLinks = [];
    private int runtimeUiRefreshPending = 1;
    private int fullRuntimeStateRefreshPending = 1;
    private bool runtimeInitialized;
    private bool runtimeDisposed;
    private bool connectOnStartup = true;
    private bool startupModeNoticeChecked;
    private long preserveMeasurementStatusUntil;
    private MachineCommandAvailability commandAvailability;
    private bool ethercatOperational;

    internal bool IsMachineOnline => controller.IsConnected && ethercatOperational && !IsConnectionTransition;

    private void ObserveNetworkMessage(ControllerRuntimeMessage message)
    {
        bool? operational = message.EthercatOperationalState;
        // Link-up alone does not imply successful authentication, mapping or OP.
        if (operational is not bool value || ethercatOperational == value) return;
        ethercatOperational = value;
        RequestRuntimeStateRefresh();
    }

    internal bool CanEditReference => commandAvailability.CanEditReference;
    internal bool CanEditJogParameters => commandAvailability.CanEditJogParameters;
    internal bool CanJog => commandAvailability.CanJog;
    internal bool CanUseManualSpindle => commandAvailability.CanUseManualSpindle;
    internal bool CanAdjustSpindleSpeed => commandAvailability.CanAdjustSpindleSpeed;
    internal bool CanUseWindowCommands => commandAvailability.CanUseWindowCommands;

    private bool CanExecuteWindowCommand() => !runtimeDisposed && !Disposing &&
        (!runtimeInitialized || !IsConnectionTransition && !IsMotionOrProgramActive());

    internal MachineCommandAvailability CaptureCommandAvailability()
        => CaptureCommandAvailability(
            controllerLinks?.IsHomingActive() == true,
            controllerLinks?.IsHeadPositioningActive == true,
            toolChangerLinks?.IsActive == true || IsTransferActive);

    private MachineCommandAvailability CaptureCommandAvailability(
        bool homeActive,
        bool headPositioningActive,
        bool toolOperationActive)
        => new(
            IsMachineOnline,
            controller.IsBusy || IsConnectionTransition,
            mainToolPath.IsProgramActive,
            IsProgramLoading || IsConnectionTransition,
            homeActive,
            headPositioningActive,
            toolOperationActive);

    /// <summary>Builds the stable model once on the UI thread. MainForm_Load owns the optional automatic connection.</summary>
    internal void InitializeRuntime(RegistryKey? configurationRoot = null, bool connectOnStartup = true)
    {
        if (runtimeInitialized) return;
        if (runtimeDisposed) throw new ObjectDisposedException(nameof(MainForm));
        this.connectOnStartup = connectOnStartup;
        try
        {
            // An injected settings root belongs to the caller and can be reused after this form closes.
            ownsRegistryRoot = configurationRoot is null;
            registryRoot = configurationRoot ?? Initialize.RegistryRoot;
            TransferNameMigration.Apply(registryRoot);
            baseGroup = new LinkGroup(Initialize.InterfaceName, Initialize.InterfaceName, registryRoot)
            {
                Description = "RouterXYZ+TC machine settings.", IconKey = "general"
            };
            localizationOverrides = new LinkLocalizationOverrideProvider(baseGroup);
            NTLog.ReportPath = Initialize.ReportPath;
            controller = new ControllerWrapper(Initialize.DefaultAxisCount,
                Initialize.ToolPathAxes.Length, Initialize.EniPath);
            controllerLinks = new ControllerLinks(this, controller, baseGroup);
            generalLinks = new GeneralLinks(this, controller, baseGroup, R_JogFeedOverride);
            runtimeLinks.Add(generalLinks.JogFeedPercentLink);
            routerLinks = new RouterLinks(controller.PLC, baseGroup);
            zoneLinks = new ZoneLinks(this, baseGroup);
            mainToolPath = controller.ToolPaths[0];
            controllerLinks.CreateAxisCommandLinks(generalLinks);
            controllerLinks.CreateHeadPositioningCommands(generalLinks);
            toolChangerLinks = new ToolChangerLinks(
                controller,
                routerLinks.Group,
                () => generalLinks.TraverseVelocity,
                () => generalLinks.Positioning,
                () => !IsTransferActive && controllerLinks.CanStartHomedMotion(),
                ConfirmEmptySpindleReference);
            unloadMacro = new MaterialTransferMacro(this, controller, routerLinks);
            loadMacro = new MaterialTransferMacro(this, controller, routerLinks, loading: true);
            referenceLinks = new ReferenceLinks(
                baseGroup,
                mainToolPath,
                () => CanEditReference,
                () => CaptureCommandAvailability().CanEditReference);
            LinkGroup keyboardGroup = baseGroup.AddGroup(
                "Keyboard", "Keyboard",
                "Application command keyboard shortcuts.",
                sKeyboardShortcutEditor.EditorKey);
            keyboardGroup.IconKey = "parameter";
            pauseLink = CreateRuntimeCommand("Pause", controllerLinks.PauseMotion,
                () => controller.IsConnected);
            ClearToolPathLink = CreateRuntimeCommand("ClearToolPath", ClearMainToolPath,
                () => CanClearMainToolPath);
            ClearToolPathLink.DisplayName = "Clear Tool Path";
            keyboardLinks = new KeyboardLinks(this, keyboardGroup, controllerLinks, generalLinks, pauseLink);

            zoneLinks.StartRuntime();
            // All application variables must exist before validating the complete candidate.
            bool plcValid = controllerLinks.ValidatePlcSettings();
            bool referencesValid = referenceLinks.ValidateReferences(out string referenceError);
            if (!plcValid) NTLog.Warning($"RouterXYZ+TC PLC: {controller.PLC.LastError}");
            if (!referencesValid) NTLog.Warning(referenceError);

            gCodeInterpreter = new GCodeInterpreter(controller.MotionController, R_GCodeView,
                R_ProgramView3D, R_ProgramProgress, () => generalLinks.TraverseVelocity, referenceLinks, routerLinks,
                ProgramLoadingStateChanged, generalLinks.GetParkPosition);
            R_GCodeView.BlockDoubleClicked += ProgramBlockDoubleClicked;
            R_GCodeView.DocumentChanged += ProgramDocumentChanged;
            toolChangerLinks.ConfigureGCodeContinuation(PrepareToolChangeContinuation);
            InitializeSpindleNotices();
            generalLinks.InitializeAccess();
            BindRuntimeControls();
            RegisterProgramLoadingGates();
            referenceEditorBinding = new ReferenceEditorBinding(referenceLinks,
                [R_ReferenceX, R_ReferenceY, R_ReferenceZ], R_ReferenceSelector,
                message => R_ProgramProgress.Text = message);
            controller.ConnectionStateChanged += ControllerConnectionStateChanged;
            controller.NotificationRaised += ControllerNotificationRaised;
            controller.ToolPathStopped += ControllerToolPathStopped;
            controller.ToolPathStopped += PathProgramFailureReported;
            controller.PLC.ProgramRuntimeChanged += PlcProgramResultChanged;
            refreshTimer = new System.Windows.Forms.Timer { Interval = generalLinks.RefreshIntervalMilliseconds };
            refreshTimer.Tick += RefreshRuntime;
            linkLocalization = new RouterLinkLocalization(baseGroup, keyboardLinks.Commands);
            runtimeInitialized = true;
            // Keep pushbutton sampling independent of the 40/100 ms display rate.
            // A press must span a sample; this is operator control, not an E-stop.
            operatorSwitches = new(controller.PLC.IOMappings["StopSwitch"],
                controller.PLC.IOMappings["PauseSwitch"], controller.PLC.IOMappings["RunSwitch"],
                controllerLinks.ResetLink, pauseLink, controllerLinks.RunLink);
            var commands = keyboardLinks.Commands.ToDictionary(command => command.Name);
            operatorSwitches.ConfigureJog(Initialize.JogInputs.Select(input =>
                new OperatorSwitches.JogInput(controller.PLC.IOMappings[input.Signal], commands[input.Command], input.Hold)));
            operatorSwitchTimer = new System.Windows.Forms.Timer { Interval = 20 };
            operatorSwitchTimer.Tick += (_, _) => operatorSwitches.Poll(IsMachineOnline);
            operatorSwitchTimer.Start();
            // The form's bindings were created before the persisted override provider.
            // Refresh once after composition even when the restored language is unchanged.
            NTLocalization.RefreshTexts();
            lastSelectedMode = generalLinks.ModeLink.Value;
            RefreshConnectionIndicator();
            InitializeMachineView();
            RefreshRuntime(this, EventArgs.Empty);
            refreshTimer.Start();
            R_ProgramProgress.Text = MissingMachineLicense ? MissingLicenseMessage : plcValid && referencesValid
                ? NTLocalization.Get(RouterXYZTCTexts.OfflineReviewConfiguration)
                : NTLocalization.Get(RouterXYZTCTexts.ConfigurationNeedsAttention);
        }
        catch
        {
            DisposeRuntime();
            throw;
        }
    }

    /// <summary>Applies an accepted refresh preset on the UI thread without restarting controller execution.</summary>
    /// <remarks>Initialization reads the restored Link value when creating the timer. Later changes only update
    /// its nominal interval; no timer is created here and disposed runtime instances ignore notifications.</remarks>
    internal void ApplyRefreshRate()
    {
        if (runtimeDisposed || refreshTimer is null) return;
        int interval = generalLinks.RefreshIntervalMilliseconds;
        if (refreshTimer.Interval != interval) refreshTimer.Interval = interval;
    }

    internal void ApplyControllerProperties(bool rebuildProgram = true)
    {
        if (IsProgramLoading) throw new InvalidOperationException("Wait for program preparation before changing machine settings.");
        controllerLinks.ApplyControllerProperties();
        RefreshDiagnosticMetadata();
        if (CaptureCommandAvailability().CanEditReference) referenceLinks.ApplyCurrentReference();
        if (rebuildProgram)
        {
            InitializeMachineView();
            gCodeInterpreter?.Rebuild();
        }
        RequestRuntimeStateRefresh();
    }

    /// <summary>Includes a partial first block and an explicitly selected continuation cursor, even while motion is paused.</summary>
    internal bool HasToolPathProgress => gCodeInterpreter.Compilation is not null
        && (mainToolPath.CurrentBlock > 0 || mainToolPath.Position > 0);

    /// <summary>Ordinary settings may rebuild an idle paused path, but never motion, program waits or managed operations.</summary>
    /// <remarks>This does not grant PLC mapping edits; those independently require every ToolPath to be empty.</remarks>
    internal bool CanRebuildFromConfiguration => !IsConnectionTransition && mainToolPath.IsIdle
        && CaptureCommandAvailability().CanUseWindowCommands;

    internal bool ValidateSettings()
        => !IsConnectionTransition && ValidateSettingsCandidate();

    private bool ValidateSettingsCandidate()
    {
        bool keyboardValid = keyboardLinks.ValidateSettings();
        bool plcValid = controllerLinks.ValidatePlcSettings(allowProgramErrors: true);
        bool referencesValid = referenceLinks.ValidateReferences(out _);
        bool spindleRangeValid = routerLinks.ValidateSpindleSpeedRange();
        return spindleRangeValid && keyboardValid && plcValid && referencesValid && zoneLinks.ValidateSettings();
    }

    internal void RestoreConfigurationAfterCancel()
    {
        controllerLinks.RestorePlcSourcesAfterCancel();
        keyboardLinks.ValidateSettings();
        RefreshDiagnosticMetadata();
    }

    internal void AttachDialogTheme(Form dialog)
    {
        if (dialog is NT.ConfigurationManager.ConfigurationForm settings)
        {
            settings.AttachTheme(R_ThemeManager);
            if (localizationOverrides is not null)
                settings.ConfigureTranslations(localizationOverrides, generalLinks.UiGroup,
                    () => generalLinks.SaveWindowLayoutLink.Value);
        }
        else if (dialog is NT.ConfigurationManager.ConfigurationSecurityDialog security)
            security.AttachTheme(R_ThemeManager);
        else if (dialog is GCodeStartLineDialog selectStartLine)
            selectStartLine.AttachTheme(R_ThemeManager);
        else if (dialog is GCodeEditorDialog editGCode)
            editGCode.AttachTheme(R_ThemeManager);
        else if (dialog is DiagnosticForm diagnostic)
            diagnostic.AttachTheme(R_ThemeManager);
        else
            R_ThemeManager.Attach(dialog);
    }

    internal NTControlTheme RuntimeTheme { get => R_ThemeManager.ThemeMode; set => R_ThemeManager.ThemeMode = value; }

    internal void ChangeLanguage(NTLanguage language) => R_ThemeManager.ChangeLanguage(language);

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!runtimeInitialized || runtimeDisposed) return;

        // Bindings skip hidden controls. Publish one complete refresh after the
        // form becomes visible; later global refreshes are change-driven.
        controllerLinks.RefreshLivePlcValues(publishSourcedValues: false);
        UI.Update();
        if (!startupModeNoticeChecked)
        {
            startupModeNoticeChecked = true;
            if (generalLinks.SelectedRuntimeMode == ControllerRuntimeMode.HardSimulation)
                sMessage.Show(this, new SMessageRequest(T(RouterXYZTCTexts.SimulationModeNotice))
                {
                    Title = T(RouterXYZTCTexts.ControllerTitle),
                    Severity = SMessageSeverity.Information
                }, RuntimeTheme);
        }
    }

    internal void OpenDiagnosticForm()
    {
        if (scopeForm is { IsDisposed: false }) { scopeForm.Activate(); return; }
        scopeForm = new ScopeForm(controller.MotionController);
        scopeForm.FormClosed += (_, _) => scopeForm = null;
        scopeForm.Show(this);
    }

    internal void OpenPlcMonitorForm()
    {
        if (plcMonitorForm is { IsDisposed: false }) { plcMonitorForm.Activate(); return; }
        plcMonitorForm = new PlcMonitorForm(controller.MotionController);
        plcMonitorForm.FormClosed += (_, _) => plcMonitorForm = null;
        plcMonitorForm.Show(this);
    }

    internal void RefreshJogInputMappings() => operatorSwitches?.RefreshJogMappings();

    private void ControllerConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        => PostRuntimeUi(() =>
        {
            // Initial OP is consumed by Controller startup; later cable events
            // arrive through the runtime queue without a lifecycle transition.
            if (e.State == ConnectionState.Online) controllerLinks.RefreshSpindleSpeed();
            ethercatOperational = e.State == ConnectionState.Online;
            operatorSwitches?.Disconnect();
            if (e.State == ConnectionState.Online)
                operatorSwitches?.Poll(IsMachineOnline);
            RefreshConnectionIndicator();
            RequestRuntimeStateRefresh();
            if (!IsProgramLoading)
                R_ProgramProgress.Text = NTLocalization.Format(RouterXYZTCTexts.ControllerState, e.State);
            if (e.State == ConnectionState.Failed)
                sMessage.Show(this, new SMessageRequest(T(RouterXYZTCTexts.ControllerConnectionFailed))
                {
                    Title = T(RouterXYZTCTexts.ControllerTitle),
                    Severity = SMessageSeverity.Warning,
                    TechnicalDetails = string.IsNullOrWhiteSpace(e.Error) ? controller.LastError : e.Error
                }, RuntimeTheme);
        });

    internal void RequestRuntimeUiRefresh()
        => Interlocked.Exchange(ref runtimeUiRefreshPending, 1);

    internal void RequestRuntimeStateRefresh()
    {
        Interlocked.Exchange(ref fullRuntimeStateRefreshPending, 1);
        RequestRuntimeUiRefresh();
    }

    private bool ConsumeRuntimeUiRefreshRequest()
        => Interlocked.Exchange(ref runtimeUiRefreshPending, 0) != 0;

    private bool ConsumeRuntimeStateRefreshRequest()
        => Interlocked.Exchange(ref fullRuntimeStateRefreshPending, 0) != 0;

    private void ControllerNotificationRaised(object? sender, ControllerNotificationEventArgs e)
    {
        // Engineering NDIS intentionally runs with the vendor evaluation license.
        // Core has already written this notification to NTLog, so keep it in the
        // report without interrupting the engineering operator with a modal dialog.
        if (controller.MotionController.SimulationMode == SimulationModes.EngineeringNdis
            && e.Code == NTErrorCode.CoreAcontisLicenseMissing)
        {
            return;
        }

        PostRuntimeUi(() =>
        {
            SMessageSeverity severity = e.Severity == NTErrorSeverity.Warning
                ? SMessageSeverity.Warning
                : e.Severity == NTErrorSeverity.CriticalError
                    ? SMessageSeverity.CriticalError
                    : SMessageSeverity.Error;
            SMessageRequest request = SMessageRequest.FromError(e.Code, severity, e.Message) with
            {
                Title = T(RouterXYZTCTexts.ControllerTitle),
                WriteToReport = false
            };
            sMessage.Show(this, request, RuntimeTheme);
        });
    }

    private void PostRuntimeUi(Action action)
    {
        if (runtimeDisposed || IsDisposed || !IsHandleCreated) return;
        if (!InvokeRequired) { action(); return; }
        try { BeginInvoke((Action)(() => { if (!runtimeDisposed && !IsDisposed) action(); })); }
        catch (InvalidOperationException) when (runtimeDisposed || IsDisposed || !IsHandleCreated) { }
    }

    private bool exitConfirmationOpen;

    // Use live controller and managed-operation state for the Exit button and for close admission.
    private bool IsExitBlocked() => runtimeInitialized && !runtimeDisposed &&
        (IsConnectionTransition || IsMotionOrProgramActive() ||
         toolChangerLinks.IsActive ||
         controller.MotionController.Macros.Programs.Any(program => program.IsRunning));

    private bool ConfirmExitWithUnfinishedProgram()
    {
        long execution = mainToolPath.ExecutionId;
        long cursor = mainToolPath.CoordinateResetGeneration;
        exitConfirmationOpen = true;
        try
        {
            var answer = sMessage.Show(this, new SMessageRequest(T(RouterXYZTCTexts.ConfirmExitUnfinishedProgram))
            {
                Title = T(RouterXYZTCTexts.ProgramTitle),
                Severity = SMessageSeverity.Question,
                Buttons = SMessageButtons.YesNo,
                WriteToReport = false
            }, RuntimeTheme);
            // The modal loop can process motion or a change of the program cursor.
            return answer == SMessageResult.Yes && !IsProgramLoading && !IsExitBlocked() &&
                execution == mainToolPath.ExecutionId && cursor == mainToolPath.CoordinateResetGeneration;
        }
        finally
        {
            exitConfirmationOpen = false;
        }
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        if (exitConfirmationOpen || IsExitBlocked())
        {
            e.Cancel = true;
            return;
        }
        if (IsProgramLoading)
        {
            e.Cancel = true;
            gCodeInterpreter.CancelLoad();
            try { await gCodeInterpreter.PendingLoad; }
            catch (Exception) { /* The load owner reports failures; shutdown only waits for worker ownership to end. */ }
            if (!IsDisposed) BeginInvoke((Action)Close);
            return;
        }
        if (!e.Cancel && runtimeInitialized && !runtimeDisposed &&
            e.CloseReason is CloseReason.UserClosing or CloseReason.ApplicationExitCall &&
            HasUnfinishedProgram() && !ConfirmExitWithUnfinishedProgram())
        {
            e.Cancel = true;
            return;
        }
        base.OnFormClosing(e);
        if (e.Cancel || !runtimeInitialized || runtimeDisposed) return;
        if (IsExitBlocked())
        {
            e.Cancel = true;
            return;
        }
        try
        {
            controllerLinks.RefreshSpindleSpeed(captureOnly: true);
            baseGroup.SaveRegistry();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            e.Cancel = true;
            sMessage.Show(this, new SMessageRequest(T(RouterXYZTCTexts.ConfigurationSaveFailed))
            {
                Title = T(RouterXYZTCTexts.ConfigurationTitle),
                Severity = SMessageSeverity.Error,
                TechnicalDetails = error.Message,
                Exception = error
            }, RuntimeTheme);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        DisposeRuntime();
        base.OnFormClosed(e);
    }

    private async void DisposeRuntime()
    {
        if (IsProgramLoading)
        {
            gCodeInterpreter.CancelLoad();
            try { await gCodeInterpreter.PendingLoad; }
            catch (Exception) { /* Keep shared memory alive until the worker has released it. */ }
        }
        if (runtimeDisposed) return;
        runtimeDisposed = true;
        zoneLinks?.Dispose();
        operatorSwitchTimer?.Dispose();
        operatorSwitches?.Disconnect();
        connectionLifetime.Cancel();
        if (connectionTransition is not null) await connectionTransition;
        R_GCodeView.BlockDoubleClicked -= ProgramBlockDoubleClicked;
        R_GCodeView.DocumentChanged -= ProgramDocumentChanged;
        DisposeSpindleNotices();
        refreshTimer?.Stop();
        refreshTimer?.Dispose();
        scopeForm?.Close();
        plcMonitorForm?.Close();
        CloseDiagnosticWindows();
        if (controller != null)
        {
            controller.ConnectionStateChanged -= ControllerConnectionStateChanged;
            controller.NotificationRaised -= ControllerNotificationRaised;
            controller.ToolPathStopped -= ControllerToolPathStopped;
            controller.ToolPathStopped -= PathProgramFailureReported;
            controller.PLC.ProgramRuntimeChanged -= PlcProgramResultChanged;
        }
        gCodeInterpreter?.Dispose();
        referenceEditorBinding?.Dispose();
        toolParameterBinding?.Dispose();
        toolParameterBinding = null;
        keyboardLinks?.Dispose();
        generalLinks?.AccessSession?.Dispose();
        controllerLinks?.DisposeSettingsEditors();
        controllerLinks?.DisposeHeadPositioningCommands();
        simulationHost?.Dispose();
        if (toolChangerLinks != null)
            toolChangerLinks.MeasurementStatusChanged -= ToolOffsetMeasurementStatusChanged;
        loadMacro?.Dispose();
        unloadMacro?.Dispose();
        toolChangerLinks?.Dispose();
        if (controllerLinks != null)
            routerLinks.SpindleSpeedStep.Changed -= SpindleSpeedStepChanged;
        foreach (Link link in runtimeLinks) link.Dispose();
        runtimeLinks.Clear();
        localizationOverrides?.Dispose();
        localizationOverrides = null;
        baseGroup?.Dispose();
        // Controller owns controlled stop, safe feedback wait and process shutdown.
        await ChangeControllerConnectionAsync(connect: false, shutdown: true);
        connectionLifetime.Dispose();
        if (ownsRegistryRoot) registryRoot?.Dispose();
    }

    /// <summary>
    /// Runs one bounded UI refresh: drain messages/change latches, advance managed
    /// operations, capture command availability, then update bindings and geometry.
    /// Axis/ToolPath change properties are read-and-clear; each has exactly one
    /// consumer here. New presentation must reuse these results, not read them again.
    /// Continuous values without change latches are sampled at the same UI rate.
    /// </summary>
    private string lastDisplayedMmUid = string.Empty;

    private void RefreshRuntime(object? sender, EventArgs e)
    {
        if (!runtimeInitialized || runtimeDisposed) return;
        int messageCount = 0;
        while (messageCount++ < 64 && controller.MotionController.TryReadRuntimeMessage(out var message))
        {
            ObserveNetworkMessage(message);
            if (!message.IsError) NTLog.Report(message.Text);
        }

        bool controllerStatusChanged = controller.StatusChanged;
        bool axisStatusChanged = false;
        foreach (AxisWrapper axis in controller.Axes)
            if (axis.StatusChanged) axisStatusChanged = true;

        bool toolPathStatusChanged = false;
        bool mainToolPathRuntimeChanged = false;
        // Every read clears a latch. Drain every ToolPath exactly once even
        // though only TP0 drives the G-code execution presentation.
        foreach (ToolPathWrapper toolPath in controller.ToolPaths)
        {
            bool statusChanged = toolPath.StatusChanged;
            bool blockChanged = toolPath.BlockChanged;
            if (statusChanged) toolPathStatusChanged = true;
            if (ReferenceEquals(toolPath, mainToolPath))
                mainToolPathRuntimeChanged |= statusChanged || blockChanged;
        }

        if (Visible) controller.RefreshMmPresence();
        string mmUid = controller.MicroMmUid;
        bool mmChanged = mmUid != lastDisplayedMmUid;
        lastDisplayedMmUid = mmUid;
        bool refreshAllRuntimeStates = ConsumeRuntimeStateRefreshRequest() || mmChanged;
        bool refreshUi = ConsumeRuntimeUiRefreshRequest() || refreshAllRuntimeStates ||
            controllerStatusChanged || axisStatusChanged || toolPathStatusChanged;
        // Sample the latest worker stage once per UI tick; refresh command availability on state edges.
        if (gCodeInterpreter.RefreshLoadingProgress()) refreshUi = true;
        if (controllerLinks.UpdateStopReset()) refreshUi = true;
        if (refreshAllRuntimeStates)
        {
            controllerLinks.RefreshRuntimeStates();
        }
        else
        {
            if (axisStatusChanged) controllerLinks.RefreshAxisRuntimeStates();
            if (toolPathStatusChanged) controllerLinks.RefreshToolPathRuntimeStates();
            if (controllerLinks.RefreshModuleRuntimeStatesIfChanged()) refreshUi = true;
        }

        bool headPositioningActive = controllerLinks.IsHeadPositioningActive;
        if (headPositioningActive != lastHeadPositioningActive) refreshUi = true;
        if (headPositioningActive)
        {
            controllerLinks.UpdateHeadPositioning();
            headPositioningActive = controllerLinks.IsHeadPositioningActive;
            if (headPositioningActive != lastHeadPositioningActive) refreshUi = true;
        }
        lastHeadPositioningActive = headPositioningActive;

        if (unloadMacro.IsActive) unloadMacro.Update();
        if (loadMacro.IsActive) loadMacro.Update();
        bool toolChangerActive = toolChangerLinks.IsActive || IsTransferActive;
        if (toolChangerActive != lastToolChangerActive) refreshUi = true;
        if (toolChangerActive)
        {
            toolChangerLinks.Update();
            toolChangerActive = toolChangerLinks.IsActive || IsTransferActive;
            if (toolChangerActive != lastToolChangerActive) refreshUi = true;
        }
        lastToolChangerActive = toolChangerActive;

        bool homeActive = controllerLinks.IsHomingActive();
        MachineCommandAvailability nextCommandAvailability = CaptureCommandAvailability(
            homeActive,
            headPositioningActive,
            toolChangerActive);
        if (nextCommandAvailability != commandAvailability) refreshUi = true;
        commandAvailability = nextCommandAvailability;
        // Run readiness also depends on managed buffer state, completed Home,
        // and the application-owned offset/tool-change state machines. Not all
        // of those transitions produce an Axis or ToolPath RT status edge.
        bool runCommandsAvailable = controllerLinks.AreRunCommandsAvailable;
        if (runCommandsAvailable != lastRunCommandsAvailable) refreshUi = true;
        lastRunCommandsAvailable = runCommandsAvailable;
        R_Exit.Enabled = !IsProgramLoading && !IsExitBlocked();
        if (refreshUi)
        {
            R_Minimize.Enabled = !IsProgramLoading;
            R_ThemeToggle.Enabled = commandAvailability.CanUseWindowCommands;
            RefreshProgramLoadButton();
            toolParameterBinding?.Refresh();
        }

        // Live PLC sources still need bounded sampling, but they refresh only
        // their owning Links rather than sweeping every registered UI Link.
        controllerLinks.RefreshLivePlcValues(publishSourcedValues: !refreshUi);
        if (refreshUi) UI.Update();

        if (refreshAllRuntimeStates)
            RefreshConnectionIndicator();
        if (refreshAllRuntimeStates || axisStatusChanged)
        {
            bool alarm = false;
            bool homed = IsMachineOnline && mainToolPath.IsHomed;
            // Machine alarms cover every configured motor. Homing follows TP0 membership.
            foreach (AxisWrapper axis in controller.Axes)
            {
                alarm |= axis.HasFault || axis.HasCouplingFault;
            }
            R_AxisAlarm.SignalOn = alarm;
            // Axis readiness and completed Home have independent indicators.
            R_XAxis.SignalOn = IsMachineOnline && positionAxes[0].IsReadyToMove && !positionAxes[0].HasFault && !positionAxes[0].HasCouplingFault;
            R_YAxis.SignalOn = IsMachineOnline && positionAxes[1].IsReadyToMove && !positionAxes[1].HasFault && !positionAxes[1].HasCouplingFault;
            R_ZAxis.SignalOn = IsMachineOnline && positionAxes[2].IsReadyToMove && !positionAxes[2].HasFault && !positionAxes[2].HasCouplingFault;
            R_Homed.SignalOn = homed;
            R_ProgramView3D.IsHomed = homed;
        }
        bool positionChanged = false;
        for (int i = 0; i < positionAxes.Length; i++)
        {
            double command = positionAxes[i].CommandPosition;
            if (command != lastCommandPositions[i])
            {
                lastCommandPositions[i] = command;
                commandPositionLabels[i].Text = FormatAxisPosition(positionAxes[i].Name, command);
                positionChanged = true;
            }
        }
        if (machineFeedMonitor.TrySample(lastCommandPositions[0], lastCommandPositions[1],
            lastCommandPositions[2], (double)System.Diagnostics.Stopwatch.GetTimestamp() /
            System.Diagnostics.Stopwatch.Frequency, IsMachineOnline, out double feed))
            DisplayMachineFeed(feed);
        if (positionChanged)
            R_ProgramView3D.SetPositionPointer(new Vector3((float)lastCommandPositions[0], (float)lastCommandPositions[1], (float)lastCommandPositions[2]));
        // Polling the cached synchronizer also covers an acknowledged initial
        // tool offset that produces no TP0 status edge.
        SynchronizeWorkCoordinatePresentation(positionChanged);
        RefreshOperationReport();
        if (refreshUi)
            R_ProgramView3D.ToolPathPickingEnabled = !controller.IsBusy;
        if (refreshAllRuntimeStates || mainToolPathRuntimeChanged)
            RefreshProgramExecution();
        else
            gCodeInterpreter.RefreshExecutionTiming();
    }

    /// <summary>
    /// Composes this machine's mechanical models after settings are applied and
    /// before connection. The host stays dormant until Hard Simulation is Online.
    /// Adapt this composition together with the profile and PLC when deriving a
    /// router; changing a sensor's name in only one place breaks its ownership.
    /// </summary>
    internal void RecreateMachineSimulation()
    {
        simulationHost?.Dispose();
        simulationHost = null;
        if (generalLinks.SelectedRuntimeMode != ControllerRuntimeMode.HardSimulation)
            return;

        const double simulatedOvertravel = 5.0;
        AxisWrapper x = controller.GetAxisByName("X");
        AxisWrapper y = controller.GetAxisByName("Y");
        AxisWrapper z = controller.GetAxisByName("Z");
        TimeSpan releaseTime = ToSimulationTime(toolChangerLinks.ToolReleaseTime.Value);
        TimeSpan clampTime = ToSimulationTime(toolChangerLinks.ToolClampTime.Value);
        INTEGER currentTool = controller.PLC.ApplicationVariables["CurrentTool"];
        double clearance = toolChangerLinks.ToolProbeApproachClearance.Value;
        double[] simulatedToolLengths = toolChangerLinks.Tools
            .Select((tool, index) => tool.Length > 0.0
                ? tool.Length
                : clearance * (0.4 + (0.25 * (index + 1) / Initialize.ToolCount)))
            .ToArray();

        var runtime = new ControllerSimulationRuntime(controller);
        List<ISimulationModule> modules = [];
        PLCIOMapping emergency = controller.PLC.IOMappings["EmergencySwitch"];
        if (!emergency.Enabled || emergency.Target is not (PLCIOMapping.ConstantTrueTarget or PLCIOMapping.ConstantFalseTarget))
            modules.Add(new PredicateInputSimulation("RouterXYZTC.EmergencyCircuit", "EmergencySwitch", () => true));
        modules.AddRange([
            new PositionSwitchSimulation("RouterXYZTC.HomeSwitches",
                controller.Axes.Select(axis => new PositionSwitchDefinition(axis.Name, $"HomeSensor{axis.Name}",
                    axis.Name.StartsWith("Z", StringComparison.Ordinal) ? axis.MaxCourse + simulatedOvertravel : axis.MinCourse - simulatedOvertravel,
                    axis.Name.StartsWith("Z", StringComparison.Ordinal) ? PositionSwitchSide.AtOrAbove : PositionSwitchSide.AtOrBelow))),
            new ToolProbeSimulation(
                () => toolChangerLinks.ToolProbePosition,
                () => currentTool.Value,
                simulatedToolLengths),
            new DigitalFeedbackSimulation("RouterXYZTC.Rollers",
            [
                new("Roller1", "Roller1Sensor", TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300), Inverted: true),
                new("Roller2", "Roller2Sensor", TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300), Inverted: true)
            ]),
            new SpindleSimulation(
                () => currentTool.Value != 0,
                simulation =>
                {
                    long requested = toolChangerLinks.NewTool.Value;
                    if (requested < 1 || requested > toolChangerLinks.Tools.Count) return false;
                    Tool tool = toolChangerLinks.Tools[(int)requested - 1];
                    PointD3 position = tool.Position;
                    return tool.Enabled
                        && Math.Abs(simulation.GetAxisPosition("X") - position.X) < 0.01
                        && Math.Abs(simulation.GetAxisPosition("Y") - position.Y) < 0.01
                        && Math.Abs(simulation.GetAxisPosition("Z") - position.Z) < 0.01;
                },
                releaseTime, clampTime,
                Initialize.PlcIoSignals
                    .Where(signal => signal.Type == PLCIOMappingType.DigitalInput
                        && (signal.Name.StartsWith("Spindle", StringComparison.Ordinal)
                            || signal.Name == "PressureSensor")
                        && (!controller.PLC.IOMappings[signal.Name].Enabled
                            || controller.PLC.IOMappings[signal.Name].Target is not
                                (PLCIOMapping.ConstantTrueTarget or PLCIOMapping.ConstantFalseTarget)))
                    .Select(signal => signal.Name).ToArray())
        ]);
        simulationHost = new SimulationHost(runtime, modules);
        simulationHost.Faulted += (_, e) =>
        {
            NTLog.Warning($"Hard Simulation module '{e.ModuleName}' stopped: {e.Error.Message}");
            PostRuntimeUi(() => R_ProgramProgress.Text = NTLocalization.Format(
                RouterXYZTCTexts.HardSimulationFailed, e.ModuleName));
        };
    }

    private static TimeSpan ToSimulationTime(long milliseconds)
        => TimeSpan.FromTicks(Math.Min(
            milliseconds,
            TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond) * TimeSpan.TicksPerMillisecond);
}

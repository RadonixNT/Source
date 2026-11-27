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
using RouterXYZACRTCP.Simulation;

namespace RouterXYZACRTCP;

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
    internal ReferenceLinks referenceLinks = null!;
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
            controllerLinks?.IsHeadPositioningActive == true);

    private MachineCommandAvailability CaptureCommandAvailability(
        bool homeActive,
        bool headPositioningActive)
        => new(
            IsMachineOnline,
            controller.IsBusy || IsConnectionTransition,
            mainToolPath.IsProgramActive,
            IsProgramLoading || IsConnectionTransition,
            homeActive,
            headPositioningActive);

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
            ResetShortcutMigration.Apply(registryRoot);
            baseGroup = new LinkGroup(Initialize.InterfaceName, Initialize.InterfaceName, registryRoot)
            {
                Description = "RouterXYZAC+RTCP machine settings.", IconKey = "general"
            };
            localizationOverrides = new LinkLocalizationOverrideProvider(baseGroup);
            NTLog.ReportPath = Initialize.ReportPath;
            controller = new ControllerWrapper(Initialize.DefaultAxisCount,
                Initialize.ToolPathAxes.Length, Initialize.EniPath);
            controllerLinks = new ControllerLinks(this, controller, baseGroup);
            generalLinks = new GeneralLinks(this, controller, baseGroup, R_JogFeedOverride);
            runtimeLinks.Add(generalLinks.JogFeedPercentLink);
            runtimeLinks.Add(generalLinks.JogModeLink);
            routerLinks = new RouterLinks(controller.PLC, baseGroup);
            rtcpSettings = new RtcpSettings(controller, baseGroup.Groups["Controller"], baseGroup.Groups["General"]);
            mainToolPath = controller.ToolPaths[0];
            controllerLinks.CreateAxisCommandLinks(generalLinks);
            controllerLinks.CreateHeadPositioningCommands(generalLinks, rtcpSettings);
            referenceLinks = new ReferenceLinks(
                baseGroup,
                mainToolPath,
                () => CanEditReference,
                () => CaptureCommandAvailability().CanEditReference,
                () => rtcpSettings.IsConfigured);
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

            // All application variables must exist before validating the complete candidate.
            bool plcValid = controllerLinks.ValidatePlcSettings();
            bool referencesValid = referenceLinks.ValidateReferences(out string referenceError);
            if (!plcValid) NTLog.Warning($"RouterXYZAC+RTCP PLC: {controller.PLC.LastError}");
            if (!referencesValid) NTLog.Warning(referenceError);

            gCodeInterpreter = new GCodeInterpreter(controller.MotionController, R_GCodeView,
                R_ProgramView3D, R_ProgramProgress, () => generalLinks.TraverseVelocity, referenceLinks, routerLinks,
                ProgramLoadingStateChanged, generalLinks.GetParkPosition);
            R_GCodeView.BlockDoubleClicked += ProgramBlockDoubleClicked;
            R_GCodeView.DocumentChanged += ProgramDocumentChanged;
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
                ? NTLocalization.Get(RouterXYZACRTCPTexts.OfflineReviewConfiguration)
                : NTLocalization.Get(RouterXYZACRTCPTexts.ConfigurationNeedsAttention);
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
        rtcpSettings.Apply();
        if (controller.MotionController.RTCP.Geometry is null)
            rtcpToolLengthSource?.ClearConfirmation();
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
        bool rtcpValid = rtcpSettings.Validate();
        bool spindleRangeValid = routerLinks.ValidateSpindleSpeedRange();
        return spindleRangeValid && keyboardValid && plcValid && referencesValid && rtcpValid;
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
                sMessage.Show(this, new SMessageRequest(T(RouterXYZACRTCPTexts.SimulationModeNotice))
                {
                    Title = T(RouterXYZACRTCPTexts.ControllerTitle),
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
                R_ProgramProgress.Text = NTLocalization.Format(RouterXYZACRTCPTexts.ControllerState, e.State);
            if (e.State == ConnectionState.Failed)
                sMessage.Show(this, new SMessageRequest(T(RouterXYZACRTCPTexts.ControllerConnectionFailed))
                {
                    Title = T(RouterXYZACRTCPTexts.ControllerTitle),
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
                Title = T(RouterXYZACRTCPTexts.ControllerTitle),
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

    private bool IsExitBlocked() => runtimeInitialized && !runtimeDisposed &&
        (IsConnectionTransition || IsMotionOrProgramActive() ||
         activeRtcpJog is not null ||
         controller.MotionController.RTCP.JogStatus is RtcpJogStatus.Jogging or RtcpJogStatus.Stopping ||
         controller.MotionController.Macros.Programs.Any(program => program.IsRunning));

    private bool ConfirmExitWithUnfinishedProgram()
    {
        long execution = mainToolPath.ExecutionId;
        long cursor = mainToolPath.CoordinateResetGeneration;
        exitConfirmationOpen = true;
        try
        {
            var answer = sMessage.Show(this, new SMessageRequest(T(RouterXYZACRTCPTexts.ConfirmExitUnfinishedProgram))
            {
                Title = T(RouterXYZACRTCPTexts.ProgramTitle),
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
            sMessage.Show(this, new SMessageRequest(T(RouterXYZACRTCPTexts.ConfigurationSaveFailed))
            {
                Title = T(RouterXYZACRTCPTexts.ConfigurationTitle),
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
        StopRtcpJog();
        runtimeDisposed = true;
        operatorSwitchTimer?.Dispose();
        operatorSwitches?.Disconnect();
        connectionLifetime.Cancel();
        if (connectionTransition is not null) await connectionTransition;
        R_GCodeView.BlockDoubleClicked -= ProgramBlockDoubleClicked;
        R_GCodeView.DocumentChanged -= ProgramDocumentChanged;
        R_GCodeView.CurrentLineChanged -= NavigatorExecutionChanged;
        R_GCodeView.DoneIndexChanged -= NavigatorExecutionChanged;
        R_GCodeExpandedView.BlockDoubleClicked -= ProgramBlockDoubleClicked;
        R_ProgramPath.TextChanged -= NavigatorProgramPathChanged;
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
        keyboardLinks?.Dispose();
        generalLinks?.AccessSession?.Dispose();
        controllerLinks?.DisposeSettingsEditors();
        controllerLinks?.DisposeHeadPositioningCommands();
        simulationHost?.Dispose();
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


        refreshUi |= RefreshRtcpControls();
        bool homeActive = controllerLinks.IsHomingActive();
        MachineCommandAvailability nextCommandAvailability = CaptureCommandAvailability(
            homeActive,
            headPositioningActive);
        if (nextCommandAvailability != commandAvailability) refreshUi = true;
        commandAvailability = nextCommandAvailability;
        // Run readiness also depends on managed buffer state, completed Home,
        // and the pending host positioning state. Not all
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
            R_AAxis.SignalOn = IsMachineOnline && positionAxes[3].IsReadyToMove && !positionAxes[3].HasFault && !positionAxes[3].HasCouplingFault;
            R_CAxis.SignalOn = IsMachineOnline && positionAxes[4].IsReadyToMove && !positionAxes[4].HasFault && !positionAxes[4].HasCouplingFault;
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
            UpdateMachinePositionPose();
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

    /// <summary>Captures a nearby training sensor in actual-position coordinates for one Home activation.</summary>
    internal PositionSwitchDefinition CreateHomeSwitchDefinition(AxisWrapper axis)
    {
        double searchTravel = axis.Name is "A" or "C" ? 2.0 : 5.0;
        string storageName = Initialize.AxisDefinitions.Single(item => item.Name == axis.Name).StorageName;
        bool positive = baseGroup.Groups["Controller"].Groups["Axes"].Groups[storageName]
            .ReadText("HomeDirection") == "Positive";
        return new(axis.Name, "HomeSensor" + axis.Name,
            axis.ActualPosition + (positive ? searchTravel : -searchTravel),
            positive ? PositionSwitchSide.AtOrAbove : PositionSwitchSide.AtOrBelow);
    }

    private ISimulationModule CreateHomingSimulation(AxisWrapper axis)
    {
        string primary = Initialize.AxisDefinitions.Single(item => item.Name == axis.Name).Master ?? axis.Name;
        var fullHome = controller.PLC.Programs[Initialize.HomePlcProgramName];
        var axisHome = controller.PLC.Programs["Home" + primary];
        return new HomingSwitchSimulation(axis.Name,
            () => fullHome.IsRunning || axisHome.IsRunning,
            () => CreateHomeSwitchDefinition(axis));
    }

    /// <summary>
    /// Builds interface-owned input models after configuration is applied. Only the
    /// online Hard Simulation host runs them; physical Home settings are unchanged.
    /// </summary>
    internal void RecreateMachineSimulation()
    {
        simulationHost?.Dispose();
        simulationHost = null;
        if (generalLinks.SelectedRuntimeMode != ControllerRuntimeMode.HardSimulation)
            return;

        var runtime = new ControllerSimulationRuntime(controller);
        simulationHost = new SimulationHost(runtime,
        [
            new PredicateInputSimulation(
                "RouterXYZACRTCP.EmergencyCircuit",
                "EmergencySwitch",
                () => true),
            .. controller.Axes.Select(CreateHomingSimulation),
            new SpindleSimulation(
                Initialize.PlcIoSignals
                    .Where(signal => signal.Type == PLCIOMappingType.DigitalInput
                        && (signal.Name.StartsWith("Spindle", StringComparison.Ordinal)
                            || signal.Name == "PressureSensor")
                        && (!controller.PLC.IOMappings[signal.Name].Enabled
                            || controller.PLC.IOMappings[signal.Name].Target is not
                                (PLCIOMapping.ConstantTrueTarget or PLCIOMapping.ConstantFalseTarget)))
                    .Select(signal => signal.Name).ToArray())
        ]);
        simulationHost.Faulted += (_, e) =>
        {
            NTLog.Warning($"Hard Simulation module '{e.ModuleName}' stopped: {e.Error.Message}");
            PostRuntimeUi(() => R_ProgramProgress.Text = NTLocalization.Format(
                RouterXYZACRTCPTexts.HardSimulationFailed, e.ModuleName));
        };
    }

}

using NT;
using NT.Diagnostics;
using NT.LinkManager;
using NT.Controller;
using NT.Controller.PLC;
using NT.Tools;
using NT.Localization;

namespace RouterXYZACRTCP
{
    /// <summary>
    /// Owns persisted XYZAC operator parameters and application commands. Jog mode is
    /// session-only; MainForm owns its lifetime outside the persisted group tree.
    /// </summary>
    internal class GeneralLinks
    {
        private const double DefaultTraverseVelocity = 500.0;
        private const double DefaultJogVelocity = 100.0;
        private const double DefaultJogStep = 1.0;

        private readonly MainForm mainForm;
        private readonly ControllerWrapper controller;
        private readonly LinkGroup baseGroup;
        private readonly NT.ConfigurationManager.ConfigurationTreeState configurationTreeState = new();
        // Keys retain controller indices; a main-axis slot is not a controller index (Z follows YG).
        private readonly Dictionary<string, RealLink> jogVelocityLinks = [];
        private readonly Dictionary<string, RealLink> jogStepLinks = [];
        private readonly Dictionary<string, RealLink> jogPositionLinks = [];
        private readonly Dictionary<string, RealLink> parkPositionLinks = [];
        private readonly LinkGroup uiGroup;
        internal LinkGroup UiGroup => uiGroup;
        private readonly IntegerLink selectStartLineX;
        private readonly IntegerLink selectStartLineY;
        private readonly IntegerLink selectStartLineWidth;
        private readonly IntegerLink selectStartLineHeight;
        private readonly IntegerLink editGCodeX;
        private readonly IntegerLink editGCodeY;
        private readonly IntegerLink editGCodeWidth;
        private readonly IntegerLink editGCodeHeight;
        internal NT.ConfigurationManager.ConfigurationAccessSession AccessSession { get; private set; } = null!;
        private NT.ConfigurationManager.ConfigurationSecurityService securityService = null!;

        internal void InitializeAccess()
        {
            securityService = new NT.ConfigurationManager.ConfigurationSecurityService(baseGroup,
                controller.MotionController.TransferSettingsSecurityAsync,
                () => controller.MotionController.AuthenticatedBoardSerial,
                () => AccessSession is not null && AccessSession.CanEngineer,
                () => controller.ConnectionState == ConnectionState.Online && controller.MotionController.AuthenticatedBoardSerial.Length == 8);
            AccessSession = new NT.ConfigurationManager.ConfigurationAccessSession(securityService, baseGroup,
                OpenConfigurationLink, OpenReportLink, mainForm.controllerLinks.ResetLink);
        }

        private bool openingConfiguration;

        internal TextLink LanguageLink { get; }
        internal TextLink ThemeLink { get; }
        /// <summary>Persisted root-level UI refresh preset. High is 40 ms; Low is 100 ms.</summary>
        internal TextLink RefreshRateLink { get; }
        /// <summary>Nominal WinForms refresh interval; does not configure the controller's motion or PLC cycle.</summary>
        internal int RefreshIntervalMilliseconds => RefreshRateLink.Value == "Low" ? 100 : 40;
        internal TextLink RecentFileLink { get; }
        internal TextLink ModeLink { get; }
        internal ControllerRuntimeMode SelectedRuntimeMode => ModeLink.Value == "Simulation"
            ? ControllerRuntimeMode.HardSimulation : ControllerRuntimeMode.Normal;
        internal CommandLink ChangeModeLink { get; private set; } = null!;
        internal LogicLink SaveWindowLayoutLink { get; }
        internal LogicLink StartFromNextCuttingLineLink { get; }
        internal RealLink TraverseVelocityLink { get; }
        internal RealLink SafeZLink { get; }
        internal RealLink ApproachDistanceLink { get; }
        internal RealLink ApproachVelocityLink { get; }
        /// <summary>Opt-in parking after an accepted natural TP0 completion; persisted by LinkManager.</summary>
        internal LogicLink ParkEnabledLink { get; }
        internal RealLink JogFeedPercentLink { get; }
        internal LogicLink JogModeLink { get; }

        internal CommandLink OpenFileLink { get; private set; } = null!;
        internal CommandLink OpenConfigurationLink { get; private set; } = null!;
        internal CommandLink OpenReportLink { get; private set; } = null!;
        internal CommandLink OpenScopeLink { get; private set; } = null!;
        internal CommandLink OpenPlcMonitorLink { get; private set; } = null!;
        internal CommandLink OpenIoDiagnosticsLink { get; private set; } = null!;
        internal CommandLink OpenPlcProgramsLink { get; private set; } = null!;
        internal CommandLink OpenMachineZeroLink { get; private set; } = null!;
        internal CommandLink OpenAxisVelocityLink { get; private set; } = null!;
        internal CommandLink OpenCycleTimingLink { get; private set; } = null!;
        internal CommandLink OpenPlcMemoryLink { get; private set; } = null!;
        internal CommandLink SelectStartLineLink { get; private set; } = null!;
        internal CommandLink EditGCodeLink { get; private set; } = null!;

        public GeneralLinks(MainForm mainForm, ControllerWrapper controller, LinkGroup baseGroup, sSlider jogFeed)
        {
            this.mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.baseGroup = baseGroup ?? throw new ArgumentNullException(nameof(baseGroup));

            ModeLink = (TextLink)baseGroup.Groups["Controller"].Members["Mode"];
            ModeLink.ItemsOnly = true;
            ModeLink.Description = "Controller runtime mode. Changes apply immediately and reconnect the idle machine; Configuration Cancel does not undo this live operation.";
            ModeLink.Validator = value => (value is "Simulation" or "Normal")
                && mainForm.CanAcceptModeValue(value);
            ModeLink.QueryEnabled += (_, e) => e.Enabled &= mainForm.CanChangeMode;
            ModeLink.Changed += (_, _) => mainForm.RuntimeModeChanged();

            LinkGroup internalGroup = GetOrAddHiddenGroup(
                baseGroup, "Internal", "Application-owned internal state.");
            uiGroup = GetOrAddHiddenGroup(internalGroup, "UI", "Internal user-interface state.");
            selectStartLineX = GetOrAddInteger(uiGroup, "SelectStartLineX", long.MinValue);
            selectStartLineY = GetOrAddInteger(uiGroup, "SelectStartLineY", long.MinValue);
            selectStartLineWidth = GetOrAddInteger(uiGroup, "SelectStartLineWidth", long.MinValue);
            selectStartLineHeight = GetOrAddInteger(uiGroup, "SelectStartLineHeight", long.MinValue);
            editGCodeX = GetOrAddInteger(uiGroup, "EditGCodeX", long.MinValue);
            editGCodeY = GetOrAddInteger(uiGroup, "EditGCodeY", long.MinValue);
            editGCodeWidth = GetOrAddInteger(uiGroup, "EditGCodeWidth", long.MinValue);
            editGCodeHeight = GetOrAddInteger(uiGroup, "EditGCodeHeight", long.MinValue);

            LinkGroup generalGroup = baseGroup.AddGroup("General", "General", "General RouterXYZAC+RTCP interface preferences.");
            generalGroup.IconKey = "general";
            ParkEnabledLink = generalGroup.AddLogic("ParkEnabled", "Park Enabled", false,
                "Automatically request GoToPark once after successful TP0 completion. Manual stops, faults and unknown stops do not park. Spindle control belongs to the main G-code program.");

            TraverseVelocityLink = generalGroup.AddReal(
                "TraverseVelocity", "Traverse Velocity",
                DefaultTraverseVelocity,
                0.000001,
                1000000.0,
                3,
                "Velocity used for G0 traverse blocks.");
            PLCVariableBridge.AddApplicationVariable(
                controller.PLC,
                TraverseVelocityLink,
                "General.TraverseVelocity",
                PLCVariableAccess.ReadOnly);

            SafeZLink = generalGroup.AddReal("SafeZ", "Safe Z", 0, -1e9, 1e9, 3,
                "Minimum machine Z height for traverse, in millimeters. Zero or a value outside Z travel uses the upper travel limit.");
            ApproachDistanceLink = generalGroup.AddReal("ApproachDistance", "Approach Distance", 10, 0, 1e9, 3,
                "Length of the slow final move to the start or resume point, in millimeters. Zero keeps the existing entry speeds.");
            ApproachVelocityLink = generalGroup.AddReal("ApproachVelocity", "Approach Velocity", 5, 0.000001, 1000000, 3,
                "Velocity of the slow final move to the start or resume point, in millimeters per second.");

            JogFeedPercentLink = Link.CreateReal("JogFeedPercent", "Jog Feed",
                jogFeed.Value, jogFeed.Minimum, jogFeed.Maximum, 0);
            JogFeedPercentLink.Description = "Manual jog velocity override in percent.";
            JogFeedPercentLink.QueryEnabled += (_, e) =>
                e.Enabled &= mainForm.CanEditJogParameters;

            foreach (string axisName in Initialize.MainAxes)
            {
                int axisIndex = controller.GetAxisByName(axisName).Index;
                string unit = axisName is "A" or "C" ? "degree" : "mm";
                LinkGroup axisGroup = generalGroup.AddGroup(
                    axisName, axisName,
                    $"Operator jog and machine-coordinate parking parameters for axis {axisName}.");
                axisGroup.IconKey = "axis";

                RealLink jogVelocity = jogVelocityLinks[axisName] = axisGroup.AddReal(
                    "JogVelocity", "Jog Velocity",
                    DefaultJogVelocity,
                    0.000001,
                    1000000.0,
                    3,
                    $"Default jog velocity for axis {axisName} in {unit}/s.");
                RealLink jogStep = jogStepLinks[axisName] = axisGroup.AddReal(
                    "JogStep", "Jog Step",
                    DefaultJogStep,
                    0.000001,
                    1000000000.0,
                    6,
                    $"Incremental jog distance for axis {axisName} in {unit}.");
                RealLink jogPosition = jogPositionLinks[axisName] = axisGroup.AddReal(
                    "JogPosition", "Jog Position",
                    0.0,
                    -1000000000.0,
                    1000000000.0,
                    3,
                    $"Absolute machine-coordinate jog destination for axis {axisName} in {unit}.");

                jogVelocity.QueryEnabled += JogParameterCanEdit;
                jogStep.QueryEnabled += JogParameterCanEdit;
                jogPosition.QueryEnabled += JogParameterCanEdit;

                RealLink parkPosition = parkPositionLinks[axisName] = axisGroup.AddReal(
                    "ParkPosition", "Park Position", 0.0, -1000000000.0, 1000000000.0, 3,
                    $"Absolute machine-coordinate parking destination for axis {axisName}. Captured when parking starts; validated against axis travel before motion.");
                parkPosition.QueryEnabled += JogParameterCanEdit;
                PLCVariableBridge.AddApplicationVariable(controller.PLC, jogPosition,
                    $"General.{axisName}.JogPosition", PLCVariableAccess.ReadOnly);
                PLCVariableBridge.AddApplicationVariable(controller.PLC, parkPosition,
                    $"General.{axisName}.ParkPosition", PLCVariableAccess.ReadOnly);

                PLCVariableBridge.AddApplicationVariable(
                    controller.PLC,
                    jogVelocity,
                    $"General.{axisName}.JogVelocity",
                    PLCVariableAccess.ReadOnly);
                PLCVariableBridge.AddApplicationVariable(
                    controller.PLC,
                    jogStep,
                    $"General.{axisName}.JogStep",
                    PLCVariableAccess.ReadOnly);
            }

            ThemeLink = baseGroup.AddText("Theme", "Theme", mainForm.RuntimeTheme.ToString(), "Light", "Dark");
            ThemeLink.ItemsOnly = true;
            ThemeLink.Description = "Preferred UI theme.";
            ThemeLink.QueryEnabled += (_, e) => e.Enabled &= mainForm.CanUseWindowCommands;
            ThemeLink.Changed += (_, _) => ApplyTheme();
            ApplyTheme();

            string[] languageNames = NTLanguages.All.Select(language => language.NativeName).ToArray();
            LanguageLink = baseGroup.AddText("Language", "Language",
                NTLanguages.Get(NTLocalization.CurrentLanguage).NativeName, languageNames);
            LanguageLink.ItemsOnly = true;
            LanguageLink.Description = "Preferred UI language.";
            LanguageLink.Changed += (_, _) => ApplyLanguage();
            ApplyLanguage();

            RefreshRateLink = baseGroup.AddText("RefreshRate", "Monitoring Refresh Rate", "High", "High", "Low");
            RefreshRateLink.ItemsOnly = true;
            RefreshLocalizedConfiguration();
            RefreshRateLink.Changed += (_, _) => mainForm.ApplyRefreshRate();

            RecentFileLink = internalGroup.AddText("RecentFile", "Recent File", "");
            RecentFileLink.Description = "Full path of the last successfully opened G-code file.";

            SaveWindowLayoutLink = generalGroup.AddLogic("SaveWindowLayout", "Save Window Layout", true);
            SaveWindowLayoutLink.Description = "Persist window placement and layout preferences.";

            StartFromNextCuttingLineLink = generalGroup.AddLogic(
                "StartFromNextCuttingLine", "Start From Next Cutting Line",
                false,
                "Start and resume at the first cutting entry at or after the requested source line, after replaying required auxiliary commands by priority.");

            // Session-only operator mode: always start with continuous Jog.
            JogModeLink = Link.CreateLogic("JogMode", "Jog Mode", false, "Continuous Jog", "Step Jog");
            JogModeLink.Description = "Jog behavior: off moves continuously until release; on moves one configured step.";
            JogModeLink.QueryEnabled += JogParameterCanEdit;

            CreateCommandLinks();
        }

        internal double TraverseVelocity => TraverseVelocityLink.Value;
        internal PositioningSettings Positioning => new(SafeZLink.Value, ApproachDistanceLink.Value, ApproachVelocityLink.Value);
        internal bool StartFromNextCuttingLine => StartFromNextCuttingLineLink.Value;

        private void ApplyTheme()
        {
            if (Enum.TryParse(ThemeLink.Value, out NTControlTheme mode) && Enum.IsDefined(mode))
                mainForm.RuntimeTheme = mode;
        }

        /// <summary>Refreshes presentation only; persisted names and High/Low values remain invariant.</summary>
        internal void RefreshLocalizedConfiguration()
        {
            RefreshRateLink.DisplayName = NTLocalization.Get(RouterXYZACRTCPTexts.RefreshRate);
            RefreshRateLink.Description = NTLocalization.Get(RouterXYZACRTCPTexts.RefreshRateDescription);
        }

        private void ApplyLanguage()
        {
            if (NTLanguages.TryParseNativeName(LanguageLink.Value, out NTLanguage language))
                mainForm.ChangeLanguage(language);
        }

        internal double GetJogVelocity(int axisIndex)
        {
            if (!jogVelocityLinks.ContainsKey(controller.Axes[axisIndex].Name))
                throw new ArgumentOutOfRangeException(nameof(axisIndex));
            // Apply the operator override once, at command capture, so every
            // single-axis and diagonal Jog uses the same effective velocity.
            return jogVelocityLinks[controller.Axes[axisIndex].Name].Value * JogFeedPercentLink.Value / 100.0;
        }

        internal double GetJogStep(int axisIndex)
        {
            if (!jogStepLinks.ContainsKey(controller.Axes[axisIndex].Name))
                throw new ArgumentOutOfRangeException(nameof(axisIndex));
            return jogStepLinks[controller.Axes[axisIndex].Name].Value;
        }

        internal double GetJogPosition(int axisIndex)
        {
            if (!jogPositionLinks.ContainsKey(controller.Axes[axisIndex].Name))
                throw new ArgumentOutOfRangeException(nameof(axisIndex));
            return jogPositionLinks[controller.Axes[axisIndex].Name].Value;
        }

        internal RealLink GetJogVelocityLink(int axisIndex) => jogVelocityLinks[controller.Axes[axisIndex].Name];
        internal RealLink GetJogStepLink(int axisIndex) => jogStepLinks[controller.Axes[axisIndex].Name];
        internal RealLink GetJogPositionLink(int axisIndex) => jogPositionLinks[controller.Axes[axisIndex].Name];
        internal double GetParkPosition(string axisName) => parkPositionLinks[axisName].Value;

        private void JogParameterCanEdit(object? sender, QueryEventArgs e)
            => e.Enabled &= mainForm.CanEditJogParameters;

        internal void RestoreSelectStartLineBounds(Form form)
            => RestoreWindowBounds(form, selectStartLineX, selectStartLineY,
                selectStartLineWidth, selectStartLineHeight);

        internal void RestoreEditGCodeBounds(Form form)
            => RestoreWindowBounds(form, editGCodeX, editGCodeY, editGCodeWidth, editGCodeHeight);

        private void RestoreWindowBounds(Form form, IntegerLink xLink, IntegerLink yLink,
            IntegerLink widthLink, IntegerLink heightLink)
        {
            ArgumentNullException.ThrowIfNull(form);
            if (!SaveWindowLayoutLink.Value ||
                widthLink.Value == long.MinValue || heightLink.Value == long.MinValue)
                return;

            int width = (int)Math.Clamp(widthLink.Value, form.MinimumSize.Width, int.MaxValue);
            int height = (int)Math.Clamp(heightLink.Value, form.MinimumSize.Height, int.MaxValue);
            if (xLink.Value is < int.MinValue or > int.MaxValue ||
                yLink.Value is < int.MinValue or > int.MaxValue)
            {
                form.Size = new Size(width, height);
                return;
            }

            var saved = new Rectangle((int)xLink.Value, (int)yLink.Value, width, height);
            Screen? screen = Screen.AllScreens.FirstOrDefault(candidate =>
                Rectangle.Intersect(candidate.WorkingArea, saved) is { Width: >= 100, Height: >= 100 });
            if (screen is null)
            {
                form.Size = new Size(width, height);
                return;
            }

            Rectangle area = screen.WorkingArea;
            width = Math.Min(width, area.Width);
            height = Math.Min(height, area.Height);
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = new Rectangle(
                Math.Clamp(saved.X, area.Left, area.Right - width),
                Math.Clamp(saved.Y, area.Top, area.Bottom - height),
                width,
                height);
        }

        internal void SaveSelectStartLineBounds(Form form)
            => SaveWindowBounds(form, selectStartLineX, selectStartLineY,
                selectStartLineWidth, selectStartLineHeight, "Select Start Line");

        internal void SaveEditGCodeBounds(Form form)
            => SaveWindowBounds(form, editGCodeX, editGCodeY, editGCodeWidth, editGCodeHeight, "Edit G-code");

        private void SaveWindowBounds(Form form, IntegerLink xLink, IntegerLink yLink,
            IntegerLink widthLink, IntegerLink heightLink, string windowName)
        {
            ArgumentNullException.ThrowIfNull(form);
            if (!SaveWindowLayoutLink.Value) return;

            Rectangle bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
            xLink.Value = bounds.X;
            yLink.Value = bounds.Y;
            widthLink.Value = bounds.Width;
            heightLink.Value = bounds.Height;
            try
            {
                uiGroup.SaveRegistry();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                NTLog.Error($"{windowName} window state could not be saved: {error.Message}");
            }
        }

        private static IntegerLink GetOrAddInteger(LinkGroup group, string name, long defaultValue)
        {
            if (!group.Members.TryGetValue(name, out Link? existing))
                return group.AddInteger(name, name, defaultValue);
            return existing as IntegerLink
                ?? throw new InvalidOperationException($"Internal setting '{name}' must be an integer Link.");
        }

        private static LinkGroup GetOrAddHiddenGroup(LinkGroup parent, string name, string description)
        {
            LinkGroup group = parent.Groups.TryGetValue(name, out LinkGroup? existing)
                ? existing
                : parent.AddGroup(name, name, description);
            group.View = AccessView.Hidden;
            return group;
        }

        private void CreateCommandLinks()
        {
            ChangeModeLink = CommandLink.CreateCommand("ChangeMode", DisplayNames.FromIdentifier("ChangeMode"));
            ChangeModeLink.QueryEnabled += (_, e) => e.Enabled &= mainForm.CanChangeMode;
            ChangeModeLink.ActionExecute += (_, _) =>
            {
                if (mainForm.CanChangeMode)
                    ModeLink.Value = ModeLink.Value == "Simulation" ? "Normal" : "Simulation";
            };
            // One bound command opens a file while idle and requests cancellation
            // during preparation; the caption and enabled state follow that mode.
            OpenFileLink = CommandLink.CreateCommand("OpenFile", DisplayNames.FromIdentifier("OpenFile"));
            OpenFileLink.ActionExecute += async (_, _) => await OpenGCodeFileAsync();
            OpenFileLink.QueryEnabled += (_, e) => e.Enabled = mainForm.CanOpenProgram || mainForm.CanCancelProgramLoad;

            SelectStartLineLink = CommandLink.CreateCommand("SelectStartLine", DisplayNames.FromIdentifier("SelectStartLine"));
            SelectStartLineLink.QueryEnabled += (_, e) => e.Enabled &= mainForm.CanOpenSelectStartLineDialog();
            SelectStartLineLink.ActionExecute += (_, _) => mainForm.OpenSelectStartLineDialog();

            EditGCodeLink = CommandLink.CreateCommand("EditGCode", DisplayNames.FromIdentifier("EditGCode"));
            EditGCodeLink.QueryEnabled += (_, e) => e.Enabled &= mainForm.CanOpenGCodeEditor();
            EditGCodeLink.ActionExecute += async (_, _) => await mainForm.OpenGCodeEditorAsync();

            OpenReportLink = CommandLink.CreateCommand("OpenReportFile", DisplayNames.FromIdentifier("OpenReportFile"));
            OpenReportLink.ActionExecute += (_, _) => NTLog.OpenReportFile();
            OpenReportLink.Description = "Opens the current session report in Notepad.";

            OpenConfigurationLink = CommandLink.CreateCommand("OpenConfiguration", DisplayNames.FromIdentifier("OpenConfiguration"));
            OpenConfigurationLink.ActionExecute += async (_, _) => await OpenConfigurationAsync();
            OpenConfigurationLink.QueryEnabled += (_, e) => e.Enabled = !openingConfiguration && !mainForm.IsConnectionTransition && !controller.IsBusy && !mainForm.IsProgramLoading;

            OpenScopeLink = CommandLink.CreateCommand("OpenDiagnostics", DisplayNames.FromIdentifier("OpenDiagnostics"));
            OpenScopeLink.ActionExecute += (_, _) => mainForm.OpenDiagnosticForm();
            OpenScopeLink.QueryEnabled += (_, e) => e.Enabled = !controller.IsBusy;

            OpenPlcMonitorLink = CommandLink.CreateCommand("OpenPLCMonitoring", DisplayNames.FromIdentifier("OpenPLCMonitoring"));
            OpenPlcMonitorLink.ActionExecute += (_, _) => mainForm.OpenPlcMonitorForm();

            // Observational tools remain available during motion. Each command lazily
            // creates its own modeless window; no diagnostic session is created here.
            OpenIoDiagnosticsLink = CommandLink.CreateCommand("OpenIoDiagnostics", DisplayNames.FromIdentifier("OpenIoDiagnostics"));
            OpenIoDiagnosticsLink.ActionExecute += (_, _) => mainForm.OpenIoDiagnostics();
            OpenPlcProgramsLink = CommandLink.CreateCommand("OpenPlcPrograms", DisplayNames.FromIdentifier("OpenPlcPrograms"));
            OpenPlcProgramsLink.ActionExecute += (_, _) => mainForm.OpenPlcPrograms();
            OpenMachineZeroLink = CommandLink.CreateCommand("OpenMachineZero", "Register machine zero");
            OpenMachineZeroLink.ActionExecute += (_, _) => mainForm.controllerLinks.OpenMachineZeroForm();
            OpenAxisVelocityLink = CommandLink.CreateCommand("OpenAxisVelocity", DisplayNames.FromIdentifier("OpenAxisVelocity"));
            OpenAxisVelocityLink.ActionExecute += (_, _) => mainForm.OpenAxisVelocity();
            OpenCycleTimingLink = CommandLink.CreateCommand("OpenCycleTiming", "Jitter / EtherCAT health");
            OpenCycleTimingLink.ActionExecute += (_, _) => mainForm.OpenCycleTiming();
            OpenPlcMemoryLink = CommandLink.CreateCommand("OpenPlcMemory", DisplayNames.FromIdentifier("OpenPlcMemory"));
            OpenPlcMemoryLink.ActionExecute += (_, _) => mainForm.OpenPlcMemory();
        }

        private async Task OpenGCodeFileAsync()
        {
            if (mainForm.IsProgramLoading)
            {
                mainForm.CancelProgramLoad();
                return;
            }
            if (!mainForm.CanOpenProgram) return;
            using var dialog = new OpenFileDialog
            {
                Title = NTLocalization.Get(RouterXYZACRTCPTexts.OpenGCodeTitle),
                Filter = NTLocalization.Get(RouterXYZACRTCPTexts.GCodeFilter),
                RestoreDirectory = true,
            };

            string recentFile = RecentFileLink.Value;
            if (File.Exists(recentFile))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(recentFile);
                dialog.FileName = Path.GetFileName(recentFile);
            }

            if (dialog.ShowDialog(mainForm) == DialogResult.OK)
            {
                await mainForm.OpenFileAsync(dialog.FileName);
            }
        }

        private Task OpenConfigurationAsync()
        {
            if (mainForm.IsProgramLoading || mainForm.IsConnectionTransition || openingConfiguration) return Task.CompletedTask;
            openingConfiguration = true;
            mainForm.RequestRuntimeUiRefresh();
            try
            {
                AccessSession.OpenConfiguration(mainForm, mainForm.AttachDialogTheme);

                // ConfigurationForm edits the existing Link graph. OK validates and persists.
                // Cancel restores only journaled dialog edits, leaves excluded live PLC values in place,
                // and asks RouterXYZAC+RTCP to resynchronize its PLC draft presentation.
                using var configurationForm = new NT.ConfigurationManager.ConfigurationForm(
                    baseGroup,
                    mainForm.controllerLinks.CreateCodeEditorProfile,
                    mainForm.controllerLinks.ValidateCodeLink,
                    mainForm.ValidateSettings,
                    mainForm.RestoreConfigurationAfterCancel,
                    link => ReferenceEquals(link, ModeLink) || link is RealLink { Source: ILivePlcSource }
                        or LogicLink { Source: ILivePlcSource } || mainForm.controllerLinks.IsLiveSpindleSpeed(link))
                {
                    KeyboardShortcutConflictProvider = mainForm.keyboardLinks.GetConflict,
                    TreeState = configurationTreeState,
                    CurrentPermission = UI.CurrentPermission
                };
                configurationForm.CommandPrograms = mainForm.controllerLinks.CommandProgramEditor;
                ConfigureConfigurationAcceptance(configurationForm);
                configurationForm.SecurityService = securityService;
                configurationForm.BindController(controller, SelectedRuntimeMode);
                configurationForm.PendingControllerWork = mainForm.PendingProgramPreparation;
                configurationForm.ConfigureConfigurationTransfer(Initialize.InterfaceName);
                configurationForm.ConfigurationChanged += (_, change) =>
                {
                    if (change.Tag == NT.ConfigurationManager.ConfigurationChangeTag.ToolPathCleared && change.ToolPathIndex == 0)
                        mainForm.RefreshClearedConfigurationToolPath();
                    else if (change.Tag == NT.ConfigurationManager.ConfigurationChangeTag.ConfigurationApplied)
                    {
                        ApplyAcceptedConfiguration(change.Links);
                        configurationForm.PendingControllerWork = mainForm.PendingProgramPreparation;
                        if (controller.ConnectionState.AllowsOfflineConfiguration()) mainForm.RecreateMachineSimulation();
                    }
                    mainForm.RequestRuntimeUiRefresh();
                };
                if (controller.ConnectionState.AllowsOfflineConfiguration()) mainForm.RecreateMachineSimulation();
                EventHandler<LinkChangedEventArgs> modeChanged = (_, _) => configurationForm.ConnectionMode = SelectedRuntimeMode;
                ModeLink.Changed += modeChanged;
                mainForm.AttachDialogTheme(configurationForm);

                // The dialog snapshots the process permission when it is created. Keep it aligned
                // with a later authenticated permission change for the remainder of this modal session.
                EventHandler permissionChanged = (_, _) =>
                    configurationForm.CurrentPermission = UI.CurrentPermission;
                UI.PermissionChanged += permissionChanged;

                DialogResult result;
                try
                {
                    result = configurationForm.ShowDialog(mainForm);
                }
                finally
                {
                    ModeLink.Changed -= modeChanged;
                    UI.PermissionChanged -= permissionChanged;
                }

                if (result != DialogResult.OK || !AccessSession.CanOperate)
                {
                    return Task.CompletedTask;
                }

                NTLog.Report("Configuration accepted.");
                return Task.CompletedTask;
            }
            finally
            {
                openingConfiguration = false;
                // Command availability is refreshed on UI state edges, not on
                // every timer tick. Reevaluate QueryEnabled after every exit,
                // including Cancel, access-dialog cancellation and exceptions.
                mainForm.RequestRuntimeUiRefresh();
            }
        }

        /// <summary>Only deferred motion-profile values invalidate prepared geometry; live references keep their existing coordinate policy.</summary>
        internal bool RequiresToolPathRebuild(Link link)
            => ReferenceEquals(link, TraverseVelocityLink) || mainForm.controllerLinks.RequiresToolPathRebuild(link);

        /// <summary>Uses the shared dialog journal for per-field consent, rollback and final admission; owns no second value snapshot.</summary>
        /// <param name="dialog">Borrowed UI-thread dialog for this editing session.</param>
        /// <param name="confirmReset">Optional confirmation presenter; null uses the standard localized warning.</param>
        internal void ConfigureConfigurationAcceptance(NT.ConfigurationManager.ConfigurationForm dialog,
            Func<IReadOnlyList<Link>, bool>? confirmReset = null)
        {
            // Consent belongs to this dialog and this cursor generation. A reconnect,
            // reset or new cursor cannot silently reuse approval for an earlier path.
            var approved = new Dictionary<Link, long>();
            bool Confirm(IReadOnlyList<Link> links)
            {
                bool accepted = confirmReset?.Invoke(links) ?? sMessage.Show(dialog, new SMessageRequest(
                    NTLocalization.Get(RouterXYZACRTCPTexts.ConfigurationPathResetQuestion) + "\n\n" +
                    string.Join("\n", links.Select(link => $"{link.Group?.DisplayName}: {link.DisplayName}")))
                {
                    Title = NTLocalization.Get(RouterXYZACRTCPTexts.ConfigurationTitle),
                    Severity = SMessageSeverity.Warning,
                    Buttons = SMessageButtons.YesNo
                }, mainForm.RuntimeTheme) == SMessageResult.Yes;
                if (accepted)
                    foreach (Link link in links) approved[link] = controller.ToolPaths[0].CoordinateResetGeneration;
                return accepted;
            }
            bool Admit()
            {
                if (mainForm.CanRebuildFromConfiguration) return true;
                sMessage.Show(dialog, new SMessageRequest(NTLocalization.Get(RouterXYZACRTCPTexts.ConfigurationMotionBusy))
                {
                    Title = NTLocalization.Get(RouterXYZACRTCPTexts.ConfigurationTitle),
                    Severity = SMessageSeverity.Warning
                }, mainForm.RuntimeTheme);
                return false;
            }
            bool AdmitGantry(Link link)
            {
                if (mainForm.controllerLinks.AcceptGantryDraft(link)) return true;
                sMessage.Show(dialog, new SMessageRequest(link.Group?.StateMessage ?? "Invalid axis configuration.")
                {
                    Title = NTLocalization.Get(RouterXYZACRTCPTexts.ConfigurationTitle),
                    Severity = SMessageSeverity.Warning
                }, mainForm.RuntimeTheme);
                return false;
            }
            dialog.ConfirmDraftEdit = link => AdmitGantry(link) && (!RequiresToolPathRebuild(link)
                || !dialog.ChangedLinks.Contains(link)
                || Admit() && (!mainForm.HasToolPathProgress || Confirm([link])));
            dialog.ConfirmAcceptance = () =>
            {
                IReadOnlyList<Link> changed = dialog.ChangedLinks;
                if (changed.Any(link => RequiresToolPathRebuild(link) || mainForm.controllerLinks.IsControllerSetting(link)) && !Admit())
                    return false;
                if (!mainForm.HasToolPathProgress) return true;
                Link[] unconfirmed = changed.Where(link => RequiresToolPathRebuild(link)
                    && (!approved.TryGetValue(link, out long generation)
                        || generation != controller.ToolPaths[0].CoordinateResetGeneration)).ToArray();
                return unconfirmed.Length == 0 || Confirm(unconfirmed);
            };
        }

        /// <summary>Applies only effective accepted edits. UI-only/no-op dialogs never touch the controller or prepared cursor.</summary>
        internal void ApplyAcceptedConfiguration(IReadOnlyList<Link> changed)
        {
            bool rebuild = changed.Any(RequiresToolPathRebuild);
            if (rebuild || changed.Any(mainForm.controllerLinks.IsControllerSetting))
            {
                if (!mainForm.CanRebuildFromConfiguration)
                    throw new InvalidOperationException("Stop motion before applying controller settings.");
                mainForm.ApplyControllerProperties(rebuildProgram: rebuild);
            }
            mainForm.controllerLinks.AcceptSavedPlcSettingsBaseline();
        }
    }
}

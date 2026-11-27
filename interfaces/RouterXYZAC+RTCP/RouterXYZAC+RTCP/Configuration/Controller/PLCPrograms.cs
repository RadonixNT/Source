using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;
using NT.Tools;
using NT.Localization;
using PlcCore = NT.PLCWrapper;

namespace RouterXYZACRTCP
{
    /// <summary>
    /// PLC source/editor and validation portion of <see cref="ControllerLinks"/>.
    /// Link values are the editable draft; NT-Controller owns compilation and the
    /// separate transactional Apply of a validated image.
    /// </summary>
    internal sealed partial class ControllerLinks
    {
        internal NTCodeEditorProfile CreateCodeEditorProfile(string editorKey)
        {
            if (!string.Equals(editorKey, NTCodeEditorProfiles.PlcEditorKey, StringComparison.OrdinalIgnoreCase))
                return NTCodeEditorProfiles.Create(editorKey);

            // Build completion from the controller's authoritative symbol catalog.
            // The editor must not maintain a duplicate list of PLC system members.
            IReadOnlyDictionary<string, string> systemSymbolTypes =
                controller.PLC.GetPredefinedSymbolTypes().ToDictionary(
                    item => item.Key,
                    item => item.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);

            return NTCodeEditorProfiles.Plc(
                symbols: controller.Axes.Select(axis => axis.Name),
                sharedSourceProvider: GetSharedPlcSource,
                systemSymbols: systemSymbolTypes.Keys,
                systemSymbolTypes: systemSymbolTypes);
        }

        internal IReadOnlyList<NTCodeDiagnostic> ValidateCodeLink(TextLink link)
        {
            // Delayed editor validation still validates the complete PLC candidate,
            // because allocation and symbol visibility span every source document.
            if (!plcDocumentsByLink.TryGetValue(link, out PLCSourceDocument? document))
                return [];

            try
            {
                ValidatePlcWithAxisCandidate();
                return ToEditorDiagnostics(document.CompilationDiagnostics);
            }
            catch (NTException ex)
            {
                LogPlcExceptionOnce(ex);
                string message = NTErrorCatalog.FormatOperatorMessage(ex.Code);
                link.SetState(OperationalState.Error, message);
                return [new NTCodeDiagnostic(0, message)];
            }
            catch (Exception ex)
            {
                NTLog.Error(
                    NTErrorCode.InterfaceInvalidConfiguration,
                    NTErrorSeverity.RecoverableError,
                    ex.Message,
                    ex);
                string message = NTErrorCatalog.FormatOperatorMessage(
                    NTErrorCode.InterfaceInvalidConfiguration);
                link.SetState(OperationalState.Error, message);
                return [new NTCodeDiagnostic(0, message)];
            }
        }

        internal bool ValidatePlcSettings(bool allowProgramErrors = false)
        {
            if (!ValidateAxisTopology() || !ValidateModuleTopology() || !ValidateInverterTopology()) return false;
            if (!plcLoadSucceeded)
            {
                plcGroup.SetState(OperationalState.Error, plcLoadError);
                return false;
            }

            try
            {
                bool valid = ValidatePlcWithAxisCandidate();
                // Keep rejected program drafts editable and persistable. Shared
                // configuration failures still prevent accepting these settings.
                return ValidateCustomCommandPrograms() && (valid || (allowProgramErrors && controller.PLC.HasValidatedImage));
            }
            catch (NTException ex)
            {
                LogPlcExceptionOnce(ex);
                plcGroup.SetState(
                    OperationalState.Error,
                    NTErrorCatalog.FormatOperatorMessage(ex.Code));
                return false;
            }
            catch (Exception ex)
            {
                NTLog.Error(
                    NTErrorCode.InterfaceInvalidConfiguration,
                    NTErrorSeverity.RecoverableError,
                    ex.Message,
                    ex);
                plcGroup.SetState(
                    OperationalState.Error,
                    NTErrorCatalog.FormatOperatorMessage(
                        NTErrorCode.InterfaceInvalidConfiguration));
                return false;
            }
        }

        internal void RestorePlcSourcesAfterCancel()
        {
            ValidatePlcSettings();
        }

        private static string GetPlcValidationError(PlcCore plc)
        {
            NTErrorCode code = plc.LastErrorCode
                ?? NTErrorCode.InterfaceInvalidConfiguration;
            return NTErrorCatalog.FormatOperatorMessage(code);
        }

        private string GetSharedPlcSource()
        {
            return plcSourceLinks.TryGetValue(Initialize.SharedPlcVariablesName, out TextLink? sourceLink)
                ? sourceLink.Value
                : string.Empty;
        }

        private void ApplyPlcSourceProperties()
        {
            if (!plcLoadSucceeded || plcSourceLinks.Count == 0)
                return;

            PlcCore plc = controller.PLC;

            try
            {
                if (!ValidateCurrentPlcConfiguration() && !plc.HasValidatedImage)
                    sMessage.Show(mainForm, new SMessageRequest(NTLocalization.Get(RouterXYZACRTCPTexts.PlcConfigurationInvalid))
                    {
                        Title = NTLocalization.Get(RouterXYZACRTCPTexts.PlcTitle),
                        Severity = SMessageSeverity.Warning,
                        TechnicalDetails = plc.LastError
                    }, mainForm.RuntimeTheme);
            }
            catch (Exception ex)
            {
                NTLog.Error(
                    NTErrorCode.InterfaceInvalidConfiguration,
                    NTErrorSeverity.RecoverableError,
                    $"PLC source validation failed: {ex.Message}",
                    ex);
                plcGroup.SetState(OperationalState.Error, ex.Message);
                sMessage.Show(mainForm, new SMessageRequest(NTLocalization.Get(RouterXYZACRTCPTexts.PlcValidationFailed))
                {
                    Title = NTLocalization.Get(RouterXYZACRTCPTexts.PlcTitle),
                    Severity = SMessageSeverity.Warning,
                    TechnicalDetails = ex.Message,
                    Exception = ex,
                    WriteToReport = false
                }, mainForm.RuntimeTheme);
            }
        }

        private void LoadPlcPrograms()
        {
            PlcCore plc = controller.PLC;
            IReadOnlyList<PLCProgram> addedPrograms = [];
            var addedLinks = new List<(LinkGroup Parent, TextLink Link)>();

            try
            {
                // Selection and call order are explicit. PLCPrograms is a reusable
                // library; scanning it would deploy unused programs and silently
                // change runtime indexes and scan load.
                LinkGroup sharedVariablesGroup = plcGroup.Groups[SharedVariablesGroupName];
                LinkGroup cyclicProgramsGroup = plcGroup.Groups[CyclicProgramsGroupName];
                LinkGroup commandProgramsGroup = plcGroup.Groups[CommandProgramsGroupName];

                var sourceDefinitions = new (
                    string Name,
                    LinkGroup Group,
                    bool? Cyclic)[]
                {
                    (Initialize.SharedPlcVariablesName, sharedVariablesGroup, null),
                    // Core scans cyclic programs in reverse registration order.
                    // Keep the operator gate and background interlocks registered.
                    ("OperatorInterlocks", cyclicProgramsGroup, true),
                    ("Background", cyclicProgramsGroup, true),
                    ("Run", commandProgramsGroup, false),
                    ("Pause", commandProgramsGroup, false),
                    ("Reset", commandProgramsGroup, false),
                    ("S", commandProgramsGroup, false),
                    ("M2", commandProgramsGroup, false),
                    ("M3", commandProgramsGroup, false),
                    ("M4", commandProgramsGroup, false),
                    ("M5", commandProgramsGroup, false),
                    (Initialize.RunTP0PlcProgramName, commandProgramsGroup, false),
                    ("HomeX", commandProgramsGroup, false),
                    ("HomeY", commandProgramsGroup, false),
                    ("HomeZ", commandProgramsGroup, false),
                    ("HomeA", commandProgramsGroup, false),
                    ("HomeC", commandProgramsGroup, false),
                    (Initialize.HomePlcProgramName, commandProgramsGroup, false),
                    ("ReturnToWork", commandProgramsGroup, false),
                    (Initialize.PositionReturnPlcProgramName, commandProgramsGroup, false),
                    (Initialize.PositionAxialReturnPlcProgramName, commandProgramsGroup, false),
                    (Initialize.PositionRtcpApproachPlcProgramName, commandProgramsGroup, false),
                    (Initialize.PositionRtcpDeparturePlcProgramName, commandProgramsGroup, false),
                };

                // Read every selected embedded source before mutating the Link tree or PLC
                // collection. A missing build resource therefore cannot leave a
                // partially registered program set behind.
                var defaultSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach ((string name, _, _) in sourceDefinitions)
                    defaultSources.Add(name, Initialize.GetPlcSource(name));

                foreach ((string name, LinkGroup group, _) in sourceDefinitions)
                {
                    TextLink link = AddPlcSourceLink(group, name, defaultSources[name]);
                    addedLinks.Add((group, link));
                }

                RefreshHomeSources();
                TextLink sharedSource = plcSourceLinks[Initialize.SharedPlcVariablesName];
                PLCProgramDefinition[] programDefinitions = sourceDefinitions
                    .Where(item => item.Cyclic is not null)
                    .Select(item => new PLCProgramDefinition(
                        item.Name,
                        plcSourceLinks[item.Name].Value,
                        item.Cyclic!.Value))
                    .ToArray();
                addedPrograms = plc.LoadProgramSet(new PLCProgramSetDefinition(
                    sharedSource.Value,
                    programDefinitions));
                plcDocumentsByLink[sharedSource] = plc.SharedVariables;

                foreach (PLCProgram program in addedPrograms)
                {
                    TextLink sourceLink = plcSourceLinks[program.Name];
                    plcDocumentsByLink[sourceLink] = program;
                    program.RuntimeStateChanged += ProgramRuntimeStateChanged;
                }

                InitializeCustomCommandPrograms();
                plcLoadSucceeded = true;
                plcLoadError = string.Empty;
            }
            catch (Exception ex)
            {
                foreach (PLCProgram program in addedPrograms)
                    program.RuntimeStateChanged -= ProgramRuntimeStateChanged;

                foreach ((LinkGroup parent, TextLink link) in addedLinks)
                {
                    link.Changed -= PlcSourceLinkChanged;
                    parent.RemoveLink(link.Name);
                }

                plcSourceLinks.Clear();
                plcDocumentsByLink.Clear();
                plcLoadSucceeded = false;
                plcLoadError = $"PLC program library could not be loaded as one complete set: {ex.Message}";
                plcGroup.SetState(OperationalState.Error, plcLoadError);
                NTLog.Error(
                    NTErrorCode.InterfaceInvalidConfiguration,
                    NTErrorSeverity.RecoverableError,
                    plcLoadError,
                    ex);
            }
        }

        private void ProgramRuntimeStateChanged(object? sender, PLCProgramRuntimeChangedEventArgs e)
            => QueuePlcStateRefresh();

        private void QueuePlcStateRefresh()
        {
            if (mainForm.IsDisposed)
                return;

            if (!mainForm.IsHandleCreated)
                return;

            if (!mainForm.InvokeRequired)
            {
                TryUpdatePlcStatesFromRuntimeEvent();
                return;
            }

            if (Interlocked.Exchange(ref plcStateRefreshPending, 1) != 0)
                return;

            try
            {
                mainForm.BeginInvoke((Action)(() =>
                {
                    Interlocked.Exchange(ref plcStateRefreshPending, 0);
                    if (!mainForm.IsDisposed)
                        TryUpdatePlcStatesFromRuntimeEvent();
                }));
            }
            catch (InvalidOperationException)
            {
                Interlocked.Exchange(ref plcStateRefreshPending, 0);
            }
        }

        private void TryUpdatePlcStatesFromRuntimeEvent()
        {
            try
            {
                UpdatePlcStates();
                mainForm.RequestRuntimeUiRefresh();
                Volatile.Write(ref plcStateRefreshFailureReported, 0);
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref plcStateRefreshFailureReported, 1) == 0)
                {
                    NTLog.Error(
                        NTErrorCode.InterfaceInvalidConfiguration,
                        NTErrorSeverity.Warning,
                        $"PLC runtime-state UI refresh failed: {ex.Message}",
                        ex);
                }
            }
        }

        private bool TryGetPlcProgram(string name, out PLCProgram program)
        {
            program = null!;
            if (!plcLoadSucceeded ||
                !controller.PLC.Programs.TryGetValue(name, out PLCProgram? candidate) ||
                candidate is null)
            {
                return false;
            }

            program = candidate;
            return true;
        }

        private TextLink AddPlcSourceLink(LinkGroup parentGroup, string name, string defaultSource)
        {
            // The file supplies the first-run default. Registry loading may replace
            // it with the operator's previously accepted source text.
            string linkName = Path.GetFileNameWithoutExtension(name);
            TextLink sourceLink = parentGroup.AddText(
                linkName, DisplayNames.FromIdentifier(linkName),
                defaultSource,
                $"Source code for the '{name}' PLC document.");
            plcSourceLinks.Add(name, sourceLink);
            // Carry the former factory program's saved source forward only until
            // LinkManager has a value under the new name.
            if (name == Initialize.RunTP0PlcProgramName &&
                parentGroup.Registry is { } registry && registry.GetValue(linkName) is null &&
                registry.GetValue("M100") is string savedSource)
            {
                sourceLink.Value = savedSource;
            }
            if (name == "OperatorInterlocks")
            {
                sourceLink.Value = defaultSource;
                sourceLink.View = AccessView.ReadOnly;
                sourceLink.Description = "Application-owned Emergency and NC Pause Hold gate. One writer owns Hold assertion and release. Customize the Run, Pause and Reset command programs instead.";
            }
            if (name == "Background")
            {
                // Migrate only the former factory Hold assignment. Preserve the
                // engineer's remaining source, including all output policies.
                sourceLink.Value = MigrateBackgroundHold(sourceLink.Value);
                sourceLink.Description = "Cyclic spindle and brake interlocks. OperatorInterlocks owns the combined Emergency/Pause controller Hold. Do not add a competing Hold writer here. Edit, validate and apply this saved source through the PLC editor.";
            }
            sourceLink.IconKey = parentGroup.IconKey;
            sourceLink.Changed += PlcSourceLinkChanged;
            return sourceLink;
        }

        internal static string MigrateBackgroundHold(string source)
            => System.Text.RegularExpressions.Regex.Replace(source,
                @"(?im)^([ \t]*)CONTROLLER\s*\.\s*HOLD\s*:=\s*NOT\s+EmergencyClosed\s*;",
                "$1// Emergency/Pause Hold is owned by OperatorInterlocks.");

        private void PlcSourceLinkChanged(object? sender, EventArgs e)
        {
            if (sender is TextLink link)
            {
                bool pendingChanges = HasUnsavedChanges(link);
                link.SetStateAndPendingChanges(
                    OperationalState.Normal,
                    pendingChanges,
                    pendingChanges
                        ? "PLC source changed and is waiting for validation."
                        : "PLC source matches the saved settings and is waiting for validation.");
            }
        }

        private bool SynchronizePlcSources()
        {
            if (!plcLoadSucceeded)
                return false;

            PlcCore plc = controller.PLC;
            Dictionary<string, string> programSources = plc.Programs.ToDictionary(
                program => program.Name,
                program => plcSourceLinks[program.Name].Value,
                StringComparer.OrdinalIgnoreCase);
            return plc.SynchronizeSources(
                plcSourceLinks[Initialize.SharedPlcVariablesName].Value,
                programSources);
        }

        private bool ValidateCurrentPlcConfiguration()
        {
            PlcCore plc = controller.PLC;
            if (!ValidatePlcIoMappings(logNewDiagnostics: true))
            {
                UpdatePlcStates();
                return false;
            }
            bool sourceChanged = SynchronizePlcSources();

            // Unrelated settings may be accepted without rebuilding an already
            // validated PLC candidate. Validation is unavailable while ToolPaths
            // contain blocks, so repeating it here would incorrectly reject an
            // unrelated module, axis, or controller setting.
            if (!sourceChanged && plc.HasValidatedImage)
            {
                UpdatePlcStates();
                return plc.IsValidated;
            }

            bool valid = plc.Validate();
            ReportPlcDiagnosticsOnce(plc);
            UpdatePlcStates();
            if (!valid)
                plcGroup.SetState(OperationalState.Error, GetPlcValidationError(plc));
            return valid;
        }

        private void ReportPlcDiagnosticsOnce(PlcCore plc)
        {
            var current = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<PLCSourceDocument> documents =
                new PLCSourceDocument[] { plc.SharedVariables }.Concat(plc.Programs);
            foreach (PLCCompilationDiagnostic diagnostic in documents
                         .SelectMany(document => document.CompilationDiagnostics)
                         .Where(item => item.Severity >= NTErrorSeverity.Warning))
            {
                string fingerprint = $"{(int)diagnostic.Code}|{diagnostic.DocumentName}|{diagnostic.Position}|{diagnostic.Message}";
                current.Add(fingerprint);
                if (loggedPlcDiagnostics.Contains(fingerprint))
                    continue;
                NTLog.Error(
                    diagnostic.Code,
                    diagnostic.Severity,
                    $"{diagnostic.DocumentName} line {diagnostic.Line}, column {diagnostic.Column}: {diagnostic.Message}");
            }

            foreach (PLCVariableBindingWarning warning in plc.SharedVariables.BindingWarnings)
            {
                string fingerprint = $"{(int)warning.Code}|{warning.VariableName}|{warning.Message}";
                current.Add(fingerprint);
                if (!loggedPlcDiagnostics.Contains(fingerprint))
                    NTLog.Error(warning.Code, warning.Severity, warning.Message);
            }

            loggedPlcDiagnostics.IntersectWith(current);
            loggedPlcDiagnostics.UnionWith(current);
        }

        private void LogPlcExceptionOnce(NTException exception)
        {
            string fingerprint = $"{(int)exception.Code}|EXCEPTION|{exception.Message}";
            if (loggedPlcDiagnostics.Add(fingerprint))
                NTLog.Error(exception.Code, exception.Severity, exception.Message, exception);
        }

        private void UpdatePlcStates()
        {
            if (!plcLoadSucceeded)
            {
                plcGroup.SetState(OperationalState.Error, plcLoadError);
                return;
            }

            PlcCore plc = controller.PLC;
            SetDocumentState(plc.SharedVariables, plcSourceLinks[Initialize.SharedPlcVariablesName]);
            foreach (PLCProgram program in plc.Programs)
                SetDocumentState(program, plcSourceLinks[program.Name]);

            foreach (LinkGroup group in new[]
                     {
                         plcGroup.Groups[SharedVariablesGroupName],
                         plcGroup.Groups[CyclicProgramsGroupName],
                         plcGroup.Groups[CommandProgramsGroupName]
                     })
            {
                SetAggregateState(group, group.Members.Values.Where(link => link.View != AccessView.Hidden));
            }

            SetAggregateState(plcGroup, plcGroup.Groups.Values);
        }

        private void SetDocumentState(PLCSourceDocument document, TextLink link)
        {
            // Presentation priority: compile diagnostics, runtime failure,
            // draft/validation state, enabled/running state, then valid.
            bool pendingChanges = HasUnsavedChanges(link);
            PLCCompilationDiagnostic? error = document.CompilationDiagnostics.FirstOrDefault(
                diagnostic => diagnostic.Severity >= NTErrorSeverity.RecoverableError);
            if (error is not null)
            {
                link.SetStateAndPendingChanges(
                    OperationalState.Error,
                    pendingChanges,
                    $"Line {error.Line}, column {error.Column}: {error.Message}" +
                    (document is PLCProgram ? " This draft is excluded from the next PLC upload." : string.Empty));
                return;
            }

            PLCCompilationDiagnostic? warning = document.CompilationDiagnostics.FirstOrDefault(
                diagnostic => diagnostic.Severity == NTErrorSeverity.Warning);
            if (warning is not null)
            {
                link.SetStateAndPendingChanges(
                    OperationalState.Warning,
                    pendingChanges,
                    $"Line {warning.Line}, column {warning.Column}: {warning.Message}");
                return;
            }

            if (document is PLCProgram program && program.HasError)
            {
                link.SetStateAndPendingChanges(
                    OperationalState.Error,
                    pendingChanges,
                    program.LastErrorDescriptor?.Summary ?? "PLC program has a runtime or load error.");
                return;
            }

            if (!document.IsValidated)
            {
                link.SetStateAndPendingChanges(
                    pendingChanges ? OperationalState.Normal : OperationalState.Valid,
                    pendingChanges,
                    pendingChanges
                        ? "PLC source is waiting for validation."
                        : "The unchanged deployed PLC source remains valid.");
                return;
            }

            if (document is PLCProgram disabledProgram && !disabledProgram.Enabled)
            {
                link.SetStateAndPendingChanges(
                    OperationalState.Inactive,
                    pendingChanges,
                    "PLC program is valid but disabled.");
                return;
            }

            if (document is PLCProgram activeProgram && activeProgram.IsRunning)
            {
                link.SetStateAndPendingChanges(
                    OperationalState.Active,
                    pendingChanges,
                    "PLC program is currently running.");
                return;
            }

            link.SetStateAndPendingChanges(
                OperationalState.Valid,
                pendingChanges,
                "PLC source is valid.");
        }

        private static IReadOnlyList<NTCodeDiagnostic> ToEditorDiagnostics(
            IReadOnlyList<PLCCompilationDiagnostic> diagnostics)
        {
            return diagnostics.Select(diagnostic => new NTCodeDiagnostic(
                    Math.Max(0, diagnostic.Line - 1),
                    diagnostic.Message,
                    diagnostic.Severity switch
                    {
                        NTErrorSeverity.Info => NTCodeDiagnosticSeverity.Information,
                        NTErrorSeverity.Warning => NTCodeDiagnosticSeverity.Warning,
                        _ => NTCodeDiagnosticSeverity.Error
                    },
                    Math.Max(0, diagnostic.Column - 1),
                    Math.Max(1, diagnostic.Length)))
                .ToArray();
        }
    }
}

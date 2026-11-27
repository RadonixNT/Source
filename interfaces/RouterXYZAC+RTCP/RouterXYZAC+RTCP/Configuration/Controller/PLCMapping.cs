using NT;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;
using PlcCore = NT.PLCWrapper;

namespace RouterXYZACRTCP;

/// <summary>
/// Builds semantic I/O settings and their live value adapters. Settings choose
/// destinations; the Controller compiler resolves them to typed addresses. Disabled
/// signals retain their target text but use independent storage. Edit gates and
/// live-output rollback exclusions must remain consistent with GeneralLinks.
/// </summary>
internal sealed partial class ControllerLinks
{
    private const string UnassignedIoTarget = "Unassigned";
    private readonly Dictionary<Link, string> mappingDescriptions = new();

    internal string GetSignalDisplayName(PLCIOMapping mapping)
        => plcIoMappingLinks.TryGetValue(mapping, out var links) && !string.IsNullOrWhiteSpace(links.Group.DisplayName)
            ? links.Group.DisplayName : DisplayNames.FromIdentifier(mapping.Name);

    private sealed record PlcIoMappingLinks(
        LinkGroup Group,
        LogicLink Enabled,
        TextLink Target,
        TextLink? FilterMode,
        Link State);

    // Rebuild from the current draft, retaining only the selected unresolved address.
    // ItemsOnly must not silently replace an assignment when its device is removed.
    private void RefreshMappingTargetChoices()
    {
        foreach (var (mapping, links) in plcIoMappingLinks)
            links.Target.Items = GetIoTargetItems(mapping.Type).Append(links.Target.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private void CreatePlcIoMappingLinks()
    {
        PlcCore plc = controller.PLC;
        foreach (Initialize.PlcIoSignal signal in Initialize.PlcIoSignals)
        {
            PLCIOMapping mapping = plc.IOMappings.Add(signal.Name, signal.Type);
            LinkGroup category = GetMappingCategory(signal.Type);
            string displayName = signal.DisplayName.Length == 0 ? DisplayNames.FromIdentifier(signal.Name) : signal.DisplayName;
            LinkGroup group = category.AddGroup(
                signal.Name, signal.Sensor.Length == 0 ? displayName : $"{displayName} ({signal.Sensor})",
                $"{signal.Type} signal resolved to a physical address, fixed digital input, or independent typed PLC storage.");
            group.IconKey = "mapping";
            if (signal.Name == "PauseSwitch")
                group.Description = "Normally closed Pause input: ON permits a new Run request; OFF requests Pause and holds all motion starts. Checked at connection. Closing the circuit never resumes automatically. Unassigned or disabled input does not apply this interlock.";
            LogicLink enabled = group.AddLogic("Enabled", "Enabled", true,
                "Preserve the selected target but use unassigned internal PLC storage while disabled.");
            enabled.QueryEnabled += PlcIoMappingCanEditQuery;

            string[] targets = GetIoTargetItems(signal.Type);
            string defaultTarget = targets.Contains(signal.DefaultTarget, StringComparer.OrdinalIgnoreCase)
                ? signal.DefaultTarget
                : UnassignedIoTarget;
            TextLink target = group.AddText(
                signal.Type is PLCIOMappingType.DigitalInput or PLCIOMappingType.AnalogInput
                    ? "Input"
                    : "Output", signal.Type is PLCIOMappingType.DigitalInput or PLCIOMappingType.AnalogInput
                    ? "Input"
                    : "Output",
                defaultTarget,
                targets,
                "Physical PLC channel or explicit digital fallback. A referenced Unassigned signal uses independent typed PLC storage and reports a warning.");
            group.DisplayMemberName = target.Name;
            // Keep a saved unresolved address visible for explicit correction.
            target.Items = targets.Append(target.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            target.ItemsOnly = true;
            target.QueryEnabled += PlcIoMappingCanEditQuery;

            TextLink? filterMode = null;
            if (signal.Type == PLCIOMappingType.DigitalInput)
            {
                string[] modes = Enum.GetNames<PLCDigitalInputFilterMode>();
                filterMode = group.AddText(
                    "FilterMode", "Filter Mode",
                    signal.FilterMode.ToString(),
                    modes,
                    "Micro-DM input filter: Disabled, LowSpeed (~100 ms), Normal (~10 ms), or HighSpeed (~1 ms).");
                filterMode.ItemsOnly = true;
                filterMode.QueryEnabled += PlcIoMappingCanEditQuery;
            }

            Link state = signal.Type is PLCIOMappingType.DigitalInput or PLCIOMappingType.DigitalOutput
                ? group.AddLogic("State", "State", false, "Off", "On", "Current physical, fixed-input or independent PLC signal state.")
                : group.AddReal("State", "State", 0, 0, 1, 4, "Current assigned normalized analog value (0..1).");
            state.View = AccessView.ReadOnly;
            if (signal.Name == "EmergencySwitch")
                state.Description = "Normally-closed emergency input: On means the circuit is closed; Off makes Background request a controlled stop of all axis/path motion and block new motion. Releasing the switch does not resume interrupted motion. This input indication is not confirmation that all motion has stopped.";

            mapping.Enabled = enabled.Value;
            mapping.Target = FromDisplayTarget(target.Value);
            if (filterMode != null)
                mapping.FilterMode = ParseFilterMode(filterMode.Value);

            if (state is RealLink real)
            {
                real.Source = RegisterLiveRuntimeSource(new MappedRealSource(mapping));
                if (signal.Type == PLCIOMappingType.AnalogOutput)
                {
                    state.View = AccessView.ReadWrite;
                    state.QueryEnabled += (_, e) => e.Enabled &= controller.IsConnected &&
                        !plc.IsApplying && mapping.IsPhysicalTarget;
                }
            }

            var links = new PlcIoMappingLinks(group, enabled, target, filterMode, state);
            plcIoMappingLinks.Add(mapping, links);
            target.Changed += PlcIoMappingLinkChanged;
            enabled.Changed += PlcIoMappingLinkChanged;
            if (filterMode != null)
                filterMode.Changed += PlcIoMappingLinkChanged;
        }
    }

    internal LogicLink GetDigitalIoStateLink(string signalName)
    {
        if (!controller.PLC.IOMappings.TryGet(signalName, out PLCIOMapping? mapping)
            || mapping is null)
        {
            throw new InvalidOperationException($"PLC I/O mapping '{signalName}' is not defined.");
        }

        if (mapping.Type is not (PLCIOMappingType.DigitalInput or PLCIOMappingType.DigitalOutput)
            || plcIoMappingLinks[mapping].State is not LogicLink state)
        {
            throw new InvalidOperationException($"PLC I/O mapping '{signalName}' is not digital.");
        }

        return state;
    }

    /// <summary>Displays either spindle direction; manual On selects CW and Off clears both directions.</summary>
    internal LogicLink CreateSpindleControlLink()
    {
        PLCIOMapping mapping = GetOutputMapping("SpindleCW", PLCIOMappingType.DigitalOutput);
        PLCIOMapping reverse = GetOutputMapping("SpindleCCW", PLCIOMappingType.DigitalOutput);
        LogicLink link = Link.CreateLogic("Spindle", "Spindle", false, "Off", "On");
        link.Source = RegisterLiveRuntimeSource(new SpindleOutputSource(mapping, reverse));
        ConfigureManualOutputAvailability(link, mapping);
        return link;
    }

    private PLCIOMapping GetOutputMapping(string signalName, PLCIOMappingType expectedType)
    {
        if (!controller.PLC.IOMappings.TryGet(signalName, out PLCIOMapping? mapping)
            || mapping is null)
        {
            throw new InvalidOperationException($"PLC I/O mapping '{signalName}' is not defined.");
        }

        if (mapping.Type != expectedType)
            throw new InvalidOperationException($"PLC I/O mapping '{signalName}' is not a {expectedType}.");

        return mapping;
    }

    private void ConfigureManualOutputAvailability(Link link, PLCIOMapping mapping)
        => link.QueryEnabled += (_, e) => e.Enabled &= controller.IsConnected
            && mapping.IsPhysicalTarget
            && !mainToolPath.IsProgramActive
            && mainForm.CanUseManualSpindle;

    private void PlcIoMappingCanEditQuery(object? sender, QueryEventArgs e)
    {
        e.Enabled &= controller.PLC.IOMappings.CanModify;
        if (sender is not Link link) return;
        if (!mappingDescriptions.TryGetValue(link, out string? description))
            mappingDescriptions.Add(link, description = link.Description);
        // Explain retained-program admission without turning a valid mapping into an error.
        link.Description = controller.MotionController.ToolPaths.Any(path => path.BlockCount != 0)
            ? NT.Localization.NTLocalization.Get(new NT.Localization.NTTextId("Radonix.RouterXYZACRTCP.Mapping.ClearPrograms")) + "\n" + description
            : description;
    }

    private void PlcIoMappingLinkChanged(object? sender, EventArgs e)
    {
        try
        {
            ApplyPlcIoMappingProperties();
            RefreshHomeSources();
            RefreshPlcIoMappingStates(logNewDiagnostics: true);
        }
        catch (NTException ex)
        {
            if (sender is Link link)
                link.SetState(OperationalState.Error, NTErrorCatalog.FormatOperatorMessage(ex.Code));
            LogPlcIoDiagnosticOnce(ex.Code, ex.Severity, ex.Message, sender is Link source ? source.Name : string.Empty);
        }
    }

    private void ApplyPlcIoMappingProperties()
    {
        PlcCore plc = controller.PLC;
        if (!plc.IOMappings.CanModify)
            return;

        foreach ((PLCIOMapping mapping, PlcIoMappingLinks links) in plcIoMappingLinks)
        {
            mapping.Enabled = links.Enabled.Value;
            mapping.Target = FromDisplayTarget(links.Target.Value);
            if (links.FilterMode != null)
                mapping.FilterMode = ParseFilterMode(links.FilterMode.Value);
        }
        mainForm.RefreshJogInputMappings();
        RefreshSpindleConfiguration();
    }

    private bool ValidatePlcIoMappings(bool logNewDiagnostics)
    {
        ApplyPlcIoMappingProperties();
        IReadOnlyList<PLCIOMappingDiagnostic> diagnostics =
            controller.PLC.IOMappings.Validate();
        ApplyPlcIoMappingDiagnostics(diagnostics, logNewDiagnostics);
        return diagnostics.All(item => item.Severity < NTErrorSeverity.RecoverableError);
    }

    private void RefreshPlcIoMappingStates(bool logNewDiagnostics)
    {
        IReadOnlyList<PLCIOMappingDiagnostic> diagnostics =
            controller.PLC.IOMappings.Validate();
        ApplyPlcIoMappingDiagnostics(diagnostics, logNewDiagnostics);

        foreach ((PLCIOMapping mapping, PlcIoMappingLinks links) in plcIoMappingLinks)
            RefreshPlcIoStateValue(mapping, links.State);
    }

    private void ApplyPlcIoMappingDiagnostics(
        IReadOnlyList<PLCIOMappingDiagnostic> diagnostics,
        bool logNewDiagnostics)
    {
        Dictionary<string, PLCIOMappingDiagnostic> bySignal = diagnostics
            .GroupBy(item => item.SignalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var currentFingerprints = new HashSet<string>(StringComparer.Ordinal);

        PLCIOMappingDiagnostic[] unassigned = diagnostics
            .Where(item => item.Code == NTErrorCode.PlcIoMappingUnassigned)
            .OrderBy(item => item.SignalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (PLCIOMappingDiagnostic diagnostic in diagnostics.Where(
                     item => item.Code != NTErrorCode.PlcIoMappingUnassigned))
        {
            string fingerprint = CreatePlcIoDiagnosticFingerprint(
                diagnostic.Code,
                diagnostic.SignalName,
                diagnostic.Target);
            currentFingerprints.Add(fingerprint);
            if (logNewDiagnostics && !loggedPlcIoDiagnostics.Contains(fingerprint))
                NTLog.Error(diagnostic.Code, diagnostic.Severity, diagnostic.Message);
        }
        if (unassigned.Length != 0)
        {
            string names = string.Join(", ", unassigned.Select(item => item.SignalName));
            string fingerprint = $"{(int)NTErrorCode.PlcIoMappingUnassigned}|{names.ToUpperInvariant()}";
            currentFingerprints.Add(fingerprint);
            if (logNewDiagnostics && !loggedPlcIoDiagnostics.Contains(fingerprint))
            {
                NTLog.Error(
                    NTErrorCode.PlcIoMappingUnassigned,
                    NTErrorSeverity.Warning,
                    $"Unassigned PLC I/O signals use independent typed PLC storage: {names}.");
            }
        }

        foreach ((PLCIOMapping mapping, PlcIoMappingLinks links) in plcIoMappingLinks)
        {
            bool pendingChanges = HasUnsavedChanges(mapping);
            if (bySignal.TryGetValue(mapping.Name, out PLCIOMappingDiagnostic? diagnostic))
            {
                SetPlcIoMappingPresentation(
                    links,
                    diagnostic.Severity >= NTErrorSeverity.RecoverableError
                        ? OperationalState.Error
                        : OperationalState.Warning,
                    pendingChanges,
                    diagnostic.Message);
                continue;
            }

            bool assigned = mapping.Enabled && mapping.Target.Length != 0;
            string message = !mapping.Enabled ? "Disabled signal uses independent typed PLC storage; selected target is retained." : mapping.Target switch
            {
                PLCIOMapping.ConstantFalseTarget or PLCIOMapping.ConstantTrueTarget =>
                    $"Read-only fixed input {mapping.Target}.",
                PLCIOMapping.VirtualFalseTarget or PLCIOMapping.VirtualTrueTarget =>
                    $"Read/write virtual output initialized to {mapping.Target[8..]}.",
                _ when assigned => $"Direct alias to {mapping.Target}.",
                _ => "Optional signal is unassigned."
            };
            SetPlcIoMappingPresentation(
                links,
                assigned ? OperationalState.Valid : OperationalState.Inactive,
                pendingChanges,
                message);
        }

        loggedPlcIoDiagnostics.IntersectWith(currentFingerprints);
        loggedPlcIoDiagnostics.UnionWith(currentFingerprints);

        SetAggregateState(digitalInputsMappingGroup, digitalInputsMappingGroup.Groups.Values);
        SetAggregateState(digitalOutputsMappingGroup, digitalOutputsMappingGroup.Groups.Values);
        SetAggregateState(analogInputsMappingGroup, analogInputsMappingGroup.Groups.Values);
        SetAggregateState(analogOutputsMappingGroup, analogOutputsMappingGroup.Groups.Values);
        SetAggregateState(mappingGroup, mappingGroup.Groups.Values);
    }

    private static void SetPlcIoMappingPresentation(
        PlcIoMappingLinks links,
        OperationalState state,
        bool pendingChanges,
        string message)
    {
        links.Group.SetStateAndPendingChanges(state, pendingChanges, message);
        links.Enabled.SetStateAndPendingChanges(state, pendingChanges, message);
        links.Target.SetStateAndPendingChanges(state, pendingChanges, message);
        links.FilterMode?.SetStateAndPendingChanges(state, pendingChanges, message);
    }

    private void LogPlcIoDiagnosticOnce(
        NTErrorCode code,
        NTErrorSeverity severity,
        string message,
        string identity)
    {
        string fingerprint = CreatePlcIoDiagnosticFingerprint(code, identity, message);
        if (loggedPlcIoDiagnostics.Add(fingerprint))
            NTLog.Error(code, severity, message);
    }

    private static string CreatePlcIoDiagnosticFingerprint(
        NTErrorCode code,
        string signal,
        string target)
        => $"{(int)code}|{signal.ToUpperInvariant()}|{target.ToUpperInvariant()}";

    private void RefreshPlcIoStateValue(PLCIOMapping mapping, Link stateLink)
    {
        if (stateLink is LogicLink logic)
        {
            if (mapping.Enabled && mapping.Type == PLCIOMappingType.DigitalInput && !mapping.IsPhysicalTarget && mapping.Target.Length != 0)
            {
                logic.Value = mapping.GetLogic();
                return;
            }

            logic.Value = controller.IsConnected && mapping.GetLogic();
        }
    }

    private sealed class MappedRealSource(PLCIOMapping mapping) : IRealSource, ILivePlcSource
    {
        public event EventHandler? ValueChanged;
        public void PublishValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);
        public double GetValue() => mapping.GetReal();
        public void SetValue(double value) => mapping.SetReal(value);
    }

    private sealed class SpindleOutputSource(PLCIOMapping clockwise, PLCIOMapping counterclockwise) : ILogicSource, ILivePlcSource
    {
        public event EventHandler? ValueChanged;
        public void PublishValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);
        public bool GetValue() => clockwise.GetLogic() || counterclockwise.GetLogic();
        public void SetValue(bool value)
        {
            counterclockwise.SetLogic(false);
            clockwise.SetLogic(value);
        }
    }

    private static bool TryParseTarget(
        PLCIOMappingType type,
        string target,
        out int module,
        out int channel)
    {
        module = -1;
        channel = -1;
        bool digital = type is PLCIOMappingType.DigitalInput or PLCIOMappingType.DigitalOutput;
        bool input = type is PLCIOMappingType.DigitalInput or PLCIOMappingType.AnalogInput;
        string family = digital ? "DM" : "AM";
        string member = input ? ".IN" : ".OUT";
        if (!target.StartsWith(family, StringComparison.OrdinalIgnoreCase))
            return false;
        int separator = target.IndexOf(member, StringComparison.OrdinalIgnoreCase);
        return separator > family.Length
            && int.TryParse(target.AsSpan(family.Length, separator - family.Length), out module)
            && int.TryParse(target.AsSpan(separator + member.Length), out channel)
            && module is >= 0 and < 8
            && channel >= 0
            && channel < (digital ? 8 : 2);
    }

    private LinkGroup GetMappingCategory(PLCIOMappingType type)
        => type switch
        {
            PLCIOMappingType.DigitalInput => digitalInputsMappingGroup,
            PLCIOMappingType.DigitalOutput => digitalOutputsMappingGroup,
            PLCIOMappingType.AnalogInput => analogInputsMappingGroup,
            PLCIOMappingType.AnalogOutput => analogOutputsMappingGroup,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    private string[] GetIoTargetItems(PLCIOMappingType type)
    {
        IEnumerable<string> targets = SelectedModuleGroups().Select(group => group.Name).SelectMany(moduleName =>
        {
            bool digital = moduleName.StartsWith("DM", StringComparison.OrdinalIgnoreCase);
            bool analog = moduleName.StartsWith("AM", StringComparison.OrdinalIgnoreCase);
            int count = digital ? 8 : analog ? 2 : 0;
            bool input = type is PLCIOMappingType.DigitalInput or PLCIOMappingType.AnalogInput;
            bool familyMatches = digital
                ? type is PLCIOMappingType.DigitalInput or PLCIOMappingType.DigitalOutput
                : analog && type is PLCIOMappingType.AnalogInput or PLCIOMappingType.AnalogOutput;
            return familyMatches
                ? Enumerable.Range(0, count).Select(channel => $"{moduleName}.{(input ? "IN" : "OUT")}{channel}")
                : [];
        });
        if (type is PLCIOMappingType.AnalogOutput or PLCIOMappingType.DigitalOutput)
            targets = targets.Concat(SelectedInverterNames().Select(name =>
                $"{name}.{(type == PLCIOMappingType.AnalogOutput ? "SPEED" : "RUN")}"));
        string[] independentTargets = type switch
        {
            PLCIOMappingType.DigitalInput =>
                [UnassignedIoTarget, PLCIOMapping.ConstantFalseTarget, PLCIOMapping.ConstantTrueTarget],
            PLCIOMappingType.DigitalOutput =>
                [UnassignedIoTarget, PLCIOMapping.VirtualFalseTarget, PLCIOMapping.VirtualTrueTarget],
            _ => [UnassignedIoTarget]
        };
        return independentTargets.Concat(targets).ToArray();
    }

    private static string FromDisplayTarget(string value)
        => string.Equals(value, UnassignedIoTarget, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value.Trim();

    private static PLCDigitalInputFilterMode ParseFilterMode(string value)
        => Enum.TryParse(value, true, out PLCDigitalInputFilterMode mode) && Enum.IsDefined(mode)
            ? mode
            : throw new NTException(
                NTErrorCode.PlcIoMappingInvalidTarget,
                NTErrorSeverity.RecoverableError,
                $"Digital input filter mode '{value}' is invalid.");
}

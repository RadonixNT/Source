using NT;
using NT.Controller.PLC;
using NT.LinkManager;
using NT.Tools;
using PlcCore = NT.PLCWrapper;

namespace RouterXYZTC
{
    /// <summary>
    /// Common PLC settings-tree construction for <see cref="ControllerLinks"/>.
    /// Program lifecycle and module topology live in their matching partial files.
    /// </summary>
    internal sealed partial class ControllerLinks
    {
        private const string SharedVariablesGroupName = "Shared Variables";
        private const string CyclicProgramsGroupName = "Cyclic Programs";
        private const string CommandProgramsGroupName = "Command Programs";

        private readonly LinkGroup modulesGroup;
        private LinkGroup[] moduleGroups;
        private readonly LinkGroup mappingGroup;
        private readonly LinkGroup digitalInputsMappingGroup;
        private readonly LinkGroup digitalOutputsMappingGroup;
        private readonly LinkGroup analogInputsMappingGroup;
        private readonly LinkGroup analogOutputsMappingGroup;
        private readonly LinkGroup plcGroup;
        private readonly LinkGroup sharedVariablesGroup;
        private TextLink microMmUidLink = null!;
        private readonly Dictionary<string, TextLink> plcSourceLinks = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<TextLink, PLCSourceDocument> plcDocumentsByLink = new();
        private readonly List<ILiveRuntimeSource> liveRuntimeSources = [];
        private bool plcLoadSucceeded;
        private string plcLoadError = string.Empty;
        private int plcStateRefreshPending;
        private int plcStateRefreshFailureReported;
        private readonly Dictionary<PLCIOMapping, PlcIoMappingLinks> plcIoMappingLinks = [];
        private readonly Dictionary<PLCIOMapping, (string Target, PLCDigitalInputFilterMode FilterMode, bool Enabled)>
            savedPlcIoMappingBaseline = [];
        private readonly Dictionary<TextLink, string> savedPlcSourceBaseline = [];
        private readonly HashSet<string> loggedPlcIoDiagnostics = new(StringComparer.Ordinal);
        private readonly HashSet<string> loggedPlcDiagnostics = new(StringComparer.Ordinal);

        internal bool IsPlcAvailable => plcLoadSucceeded;
        internal string PlcAvailabilityError => plcLoadError;

        internal T RegisterLiveRuntimeSource<T>(T source) where T : ILiveRuntimeSource
        {
            liveRuntimeSources.Add(source);
            return source;
        }

        internal void RefreshLivePlcValues(bool publishSourcedValues = true)
        {
            RefreshSpindleSpeed();
            if (publishSourcedValues)
                foreach (ILiveRuntimeSource source in liveRuntimeSources)
                    source.PublishValueChanged();

            foreach ((PLCIOMapping mapping, PlcIoMappingLinks links) in plcIoMappingLinks)
                if (links.State is LogicLink && IsActiveZoneMapping(mapping.Name))
                    RefreshPlcIoStateValue(mapping, links.State);
        }

        private void CreatePlcSettings()
        {
            // LinkGroup loads the one current settings location. Do not infer
            // defaults from historical registry branches or migrate old formats.
            IntegerLink homeTimeout = plcGroup.AddInteger(
                "HomeTimeout", "Home Timeout",
                120,
                1,
                86400,
                "Home search/release/stop phase timeout in seconds; captured at home activation.");
            PLCVariableBridge.AddApplicationVariable(
                controller.PLC,
                homeTimeout,
                "Controller.HomeTimeout",
                PLCVariableAccess.ReadOnly);

            foreach (string suffix in Initialize.MainAxes.Append("All"))
                controller.PLC.ApplicationVariables.AddInteger("HomeFailure" + suffix, 0, PLCVariableAccess.ReadWrite);

            IntegerLink couplingErrorCountLimit = plcGroup.AddInteger(
                "CouplingErrorCountLimit", "Coupling Error Count Limit",
                NT.Controller.Core.DefaultCouplingErrorCountLimit,
                1,
                int.MaxValue,
                "Number of net out-of-tolerance 1 ms coupling samples required before a controlled group stop.");
            ConfigureReadOnlyWhileControllerSessionActive(couplingErrorCountLimit);

            microMmUidLink = plcGroup.AddText("MM UID", "MM UID", "Not available");
            microMmUidLink.Description =
                "Eight-digit certificate serial of the currently observed physical MM board.";
            microMmUidLink.View = AccessView.ReadOnly;
        }

        private void RefreshPlcSettings()
        {
            string uid = controller.MicroMmUid;
            microMmUidLink.Value = string.IsNullOrEmpty(uid) ? "Not available" : uid;
        }

        internal void AcceptSavedPlcSettingsBaseline()
        {
            CaptureSavedPlcSettingsBaseline();
            RefreshPlcIoMappingStates(logNewDiagnostics: false);
            UpdatePlcStates();
        }

        private void CaptureSavedPlcSettingsBaseline()
        {
            savedPlcIoMappingBaseline.Clear();
            foreach (PLCIOMapping mapping in plcIoMappingLinks.Keys)
                savedPlcIoMappingBaseline.Add(mapping, (mapping.Target, mapping.FilterMode, mapping.Enabled));

            savedPlcSourceBaseline.Clear();
            foreach (TextLink link in plcSourceLinks.Values)
                savedPlcSourceBaseline.Add(link, link.Value);
        }

        private bool HasUnsavedChanges(PLCIOMapping mapping)
            => savedPlcIoMappingBaseline.TryGetValue(mapping, out var saved)
                && (!string.Equals(mapping.Target, saved.Target, StringComparison.OrdinalIgnoreCase)
                    || mapping.FilterMode != saved.FilterMode || mapping.Enabled != saved.Enabled);

        private bool HasUnsavedChanges(TextLink link)
            => savedPlcSourceBaseline.TryGetValue(link, out string? saved)
                ? !string.Equals(link.Value, saved, StringComparison.Ordinal)
                : customProgramSources.ContainsKey(link.Name);

        private LinkGroup[] CreatePlcGroups(
            LinkGroup plcGroup,
            out LinkGroup modulesGroup,
            out LinkGroup mappingGroup,
            out LinkGroup digitalInputsMappingGroup,
            out LinkGroup digitalOutputsMappingGroup,
            out LinkGroup analogInputsMappingGroup,
            out LinkGroup analogOutputsMappingGroup,
            out LinkGroup sharedVariablesGroup)
        {
            // Group insertion order is intentional: physical topology first,
            // semantic I/O mapping second, then editable source documents.
            modulesGroup = plcGroup.AddGroup(
                "Modules", "Modules",
                "MM side modules in physical CAN assignment order.");
            modulesGroup.IconKey = "module";

            mappingGroup = plcGroup.AddGroup(
                "Mapping", "Mapping",
                "Semantic PLC signals mapped to physical channels, fixed digital inputs, or virtual digital outputs at compile time.");
            mappingGroup.IconKey = "mapping";
            digitalInputsMappingGroup = mappingGroup.AddGroup("Digital Inputs", "Digital Inputs", "Semantic read-only digital input signals.");
            digitalOutputsMappingGroup = mappingGroup.AddGroup("Digital Outputs", "Digital Outputs", "Semantic read/write digital output signals.");
            analogInputsMappingGroup = mappingGroup.AddGroup("Analog Inputs", "Analog Inputs", "Semantic read-only REAL input signals (0..1).");
            analogOutputsMappingGroup = mappingGroup.AddGroup("Analog Outputs", "Analog Outputs", "Semantic read/write REAL output signals (0..1).");
            foreach (LinkGroup group in new[]
                     {
                         digitalInputsMappingGroup,
                         digitalOutputsMappingGroup,
                         analogInputsMappingGroup,
                         analogOutputsMappingGroup
                     })
            {
                group.IconKey = "mapping";
            }

            sharedVariablesGroup = plcGroup.AddGroup(
                SharedVariablesGroupName, SharedVariablesGroupName,
                "Typed shared-memory variables used by PLC programs and application settings.",
                NTCodeEditorProfiles.PlcEditorKey);
            sharedVariablesGroup.IconKey = "shared";
            LinkGroup cyclicGroup = plcGroup.AddGroup(
                CyclicProgramsGroupName, CyclicProgramsGroupName,
                "PLC programs executed continuously by the cyclic runtime.",
                NTCodeEditorProfiles.PlcEditorKey);
            cyclicGroup.IconKey = "cyclic";
            LinkGroup commandGroup = plcGroup.AddGroup(
                CommandProgramsGroupName, CommandProgramsGroupName,
                "PLC programs started explicitly by a command or M-code.",
                NTCodeEditorProfiles.PlcEditorKey);
            commandGroup.IconKey = "command";

            return CreateModuleSettings(modulesGroup);
        }

        private void CreateApplicationVariableLinks()
        {
            // These values are application-owned persisted settings, not declarations
            // in Shared.plc. PLC sees read-only symbols while the UI remains the owner.
            PlcCore plc = controller.PLC;
            CreateAxisHomeApplicationVariables(plc);
            CreateSpindleSettings();
        }
    }
}

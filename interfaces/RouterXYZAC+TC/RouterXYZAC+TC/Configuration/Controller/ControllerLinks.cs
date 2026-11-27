using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;
using NT.Tools;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace RouterXYZACTC
{
    /// <summary>
    /// Owns the controller settings tree and the controller-related interface commands.
    /// Runtime objects remain owned by MainForm; this partial class only connects them to LinkGroup settings.
    /// </summary>
    internal sealed partial class ControllerLinks
    {
        private readonly MainForm mainForm;
        private readonly ControllerWrapper controller;

        private readonly LinkGroup controllerGroup;
        private TextLink macAddressLink = null!;
        private IntegerLink activeAxisCountLink = null!;
        private TextLink acontisLicenseStateLink = null!;
        private TextLink licenseKeyLink = null!;
        internal bool HasLicenseFile => !string.IsNullOrWhiteSpace(licenseKeyLink.Value);
        private TypeDescriptionProvider? licenseKeyEditorProvider;

        public ControllerLinks(MainForm mainForm, ControllerWrapper controller, LinkGroup parentGroup)
        {
            this.mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            ArgumentNullException.ThrowIfNull(parentGroup);

            // ToolPath 0 owns the work program; ToolPath 1 is reserved for the
            // application positioning commands created after General settings.
            mainToolPath = controller.ToolPaths[0];

            controllerGroup = parentGroup.AddGroup(
                "Controller", "Controller",
                "Motion controller, axis, ToolPath, and PLC configuration.");
            controllerGroup.IconKey = "general";

            CreateControllerSettings();

            axesGroup = controllerGroup.AddGroup("Axes", "Axes", "Per-axis controller configuration.");
            axesGroup.IconKey = "axis";
            CreateGantrySettings();
            allAxisGroups = CreateAxisGroups(axesGroup);
            axisGroups = SelectedAxisGroups();
            RefreshGantryVisibility();
            CreateInverterLinks();

            toolPathsGroup = controllerGroup.AddGroup("ToolPaths", "Tool Paths", "Coordinated motion configuration.");
            toolPathsGroup.IconKey = "toolpath";
            toolPathGroups = CreateToolPathGroups(toolPathsGroup);

            plcGroup = controllerGroup.AddGroup(
                "PLC", "PLC",
                "PLC programs, shared variables, and I/O module configuration.");
            plcGroup.IconKey = "plc";
            CreatePlcSettings();
            moduleGroups = CreatePlcGroups(
                plcGroup,
                out modulesGroup,
                out mappingGroup,
                out digitalInputsMappingGroup,
                out digitalOutputsMappingGroup,
                out analogInputsMappingGroup,
                out analogOutputsMappingGroup,
                out sharedVariablesGroup);
            CreatePlcIoMappingLinks();
            CreateApplicationVariableLinks();

            // Construct all Links before applying them. PLC programs are loaded only
            // after application variables exist, so final validation sees the full
            // application symbol catalog.
            ApplyControllerProperties();
            LoadPlcPrograms();
            CaptureSavedPlcSettingsBaseline();
            CreateToolPathCommandLinks();
            CreateConnectionCommandLinks();
            RefreshRuntimeStates();
        }

        internal CommandLink ConnectLink { get; private set; } = null!;
        internal CommandLink DisconnectLink { get; private set; } = null!;

        private void CreateConnectionCommandLinks()
        {
            ConnectLink = CommandLink.CreateCommand("Connect", DisplayNames.FromIdentifier("Connect"));
            ConnectLink.ActionExecute += (_, _) => mainForm.ConnectController();
            ConnectLink.QueryEnabled += (_, e) =>
                e.Enabled = !mainForm.IsConnectionTransition && !mainForm.IsProgramLoading && controller.ConnectionState.AllowsOfflineConfiguration();

            DisconnectLink = CommandLink.CreateCommand("Disconnect", DisplayNames.FromIdentifier("Disconnect"));
            DisconnectLink.ActionExecute += (_, _) => mainForm.DisconnectController();
            DisconnectLink.QueryEnabled += (_, e) =>
                e.Enabled = !mainForm.IsConnectionTransition && !mainForm.IsProgramLoading && controller.ConnectionState is ConnectionState.Connecting or ConnectionState.Online;
        }

        internal void RefreshRuntimeStates()
        {
            RefreshSpindleConfiguration();
            string macAddress = controller.MotionController.EthercatMacAddress;
            macAddressLink.Value = string.IsNullOrEmpty(macAddress) ? "Not available" : macAddress;
            activeAxisCountLink.Value = controller.ActiveAxisCount;
            acontisLicenseStateLink.Value = controller.IsAcontisLicenseActive ? "Active" : "Inactive";
            RefreshPlcSettings();
            RefreshAxisStates();
            RefreshToolPathStates();
            RefreshModuleStates();
            RefreshPlcIoMappingStates(logNewDiagnostics: false);
            UpdatePlcStates();
        }

        internal void RefreshAxisRuntimeStates() => RefreshAxisStates();

        internal void RefreshToolPathRuntimeStates() => RefreshToolPathStates();

        internal bool IsHeadPositioningActive => headPositioning?.IsActive == true;

        internal void UpdateHeadPositioning() => headPositioning?.Update();

        internal void ApplyControllerProperties()
        {
            if (!InverterTopologyMatches() && !ValidateInverterTopology())
                throw new InvalidOperationException(inverterGroup.StateMessage);
            if (!SelectedAxisGroups().SequenceEqual(axisGroups) && !ValidateAxisTopology())
                throw new InvalidOperationException(axesGroup.StateMessage);
            // Copy persisted/editable Link values into the stable controller model.
            // Each domain decides whether a value takes effect now or after reconnect.
            if (controller.ConnectionState.AllowsOfflineConfiguration())
            {
                controller.CouplingErrorCountLimit = checked((int)plcGroup.ReadInteger("CouplingErrorCountLimit"));
            }
            controller.SetAcontisLicenseKey(controllerGroup.ReadText("License Key"));
            ApplyPlcIoMappingProperties();
            ApplyInverterProperties();
            ApplyModuleProperties();
            ApplyAxisProperties();
            ApplyToolPathProperties();
            ApplyPlcSourceProperties();
        }

        /// <summary>Identifies buffered controller settings without comparing names or duplicating their values.</summary>
        internal bool IsControllerSetting(Link link) => IsInGroup(link, controllerGroup);

        /// <summary>Axis and coordinated-motion drafts invalidate the prepared path when accepted.</summary>
        internal bool RequiresToolPathRebuild(Link link)
            => IsInGroup(link, axesGroup) || IsInGroup(link, toolPathsGroup);

        private static bool IsInGroup(Link link, LinkGroup branch)
        {
            for (LinkGroup? group = link.Group; group is not null; group = group.Parent)
                if (ReferenceEquals(group, branch)) return true;
            return false;
        }

        private void CreateControllerSettings()
        {
            controllerGroup.AddText("Mode", "Mode", "Simulation", new[] { "Simulation", "Normal" });

            macAddressLink = controllerGroup.AddText("MAC", "MAC", "Not available");
            macAddressLink.Description = "MAC address used by the EtherCAT runtime link layer.";
            macAddressLink.View = AccessView.ReadOnly;

            licenseKeyLink = controllerGroup.AddText("License Key", "License File", string.Empty);
            if (!LicenseFileEditor.IsImportablePackage(licenseKeyLink.Value))
                licenseKeyLink.Value = string.Empty;
            licenseKeyLink.Description = "Import a signed Radonix RNT3 .ntlic file using the dialog button. The file content is stored in the registry and takes effect on the next connection.";
            licenseKeyLink.View = AccessView.ReadWrite;
            licenseKeyLink.Permission = AccessPermission.Engineering;
            licenseKeyLink.UnauthorizedView = AccessView.ReadOnly;
            licenseKeyEditorProvider = TypeDescriptor.AddAttributes(
                licenseKeyLink,
                new EditorAttribute(typeof(LicenseFileEditor), typeof(System.Drawing.Design.UITypeEditor)),
                new TypeConverterAttribute(typeof(LicenseFileContentConverter)));

            activeAxisCountLink = controllerGroup.AddInteger(
                "ActiveAxisCount", "Active Axis Count",
                0,
                0,
                ControllerWrapper.MaxAxisCount,
                "Number of physical servo axes detected by the current EtherCAT runtime.");
            activeAxisCountLink.View = AccessView.ReadOnly;

            acontisLicenseStateLink = controllerGroup.AddText("Acontis License", "Acontis License", "Inactive");
            acontisLicenseStateLink.Description =
                "Active when EC-Master accepted the Acontis key during the latest connection attempt.";
            acontisLicenseStateLink.View = AccessView.ReadOnly;
        }

        private void ConfigureReadOnlyWhileControllerSessionActive(params Link[] links)
        {
            foreach (Link link in links)
                link.QueryEnabled += ControllerSessionInactiveQuery;
        }

        private void ControllerSessionInactiveQuery(object? sender, QueryEventArgs e)
            => e.Enabled &= controller.ConnectionState.AllowsOfflineConfiguration();

        internal void DisposeSettingsEditors()
        {
            if (licenseKeyEditorProvider is null)
                return;

            TypeDescriptor.RemoveProvider(licenseKeyEditorProvider, licenseKeyLink);
            licenseKeyEditorProvider = null;
        }

        private static void SetAggregateState(LinkGroup target, IEnumerable<Access> children)
        {
            // This runs for several branches during runtime refresh. Enumerate each
            // child once without materializing a temporary array. Do not stop at an
            // error: a later child may still contribute pending settings changes.
            bool any = false, pendingChanges = false, allInactive = true;
            bool error = false, warning = false, active = false;
            foreach (Access child in children)
            {
                any = true;
                pendingChanges |= child.HasPendingChanges;
                OperationalState childState = child.State;
                allInactive &= childState == OperationalState.Inactive;
                error |= childState == OperationalState.Error;
                warning |= childState == OperationalState.Warning;
                active |= childState == OperationalState.Active;
            }
            if (!any)
            {
                target.SetStateAndPendingChanges(OperationalState.Normal, pendingChanges: false);
                return;
            }

            OperationalState state;
            if (error)
                state = OperationalState.Error;
            else if (warning)
                state = OperationalState.Warning;
            else if (active)
                state = OperationalState.Active;
            else if (allInactive)
                state = OperationalState.Inactive;
            else
                state = OperationalState.Valid;

            string message = state switch
            {
                OperationalState.Error => "One or more child items contain errors.",
                OperationalState.Warning => "One or more child items contain warnings.",
                OperationalState.Active => "One or more child items are active.",
                OperationalState.Inactive => "All child items are inactive.",
                _ => "All visible child items are valid."
            };
            if (pendingChanges)
                message += " One or more child items have pending changes.";
            target.SetStateAndPendingChanges(state, pendingChanges, message);
        }
    }
}

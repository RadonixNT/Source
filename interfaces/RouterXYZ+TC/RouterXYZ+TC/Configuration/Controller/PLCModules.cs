using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.LinkManager;
using System.Globalization;

namespace RouterXYZTC
{
    /// <summary>
    /// MM module-topology portion of <see cref="ControllerLinks"/>.
    /// Profile order defines physical CAN assignment priority; the family suffix
    /// defines the stable PLC I/O index.
    /// </summary>
    internal sealed partial class ControllerLinks
    {
        private LinkGroup[] CreateModuleGroups(LinkGroup parentGroup, IReadOnlyList<string> moduleNames)
        {
            ArgumentNullException.ThrowIfNull(moduleNames);
            if (moduleNames.Count > ModuleCollection.MaximumCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moduleNames),
                    $"The interface can define at most {ModuleCollection.MaximumCount} PLC modules.");
            }

            var groups = new LinkGroup[moduleNames.Count];
            var ioSlots = new HashSet<(byte Type, byte Index)>();
            for (int index = 0; index < groups.Length; index++)
            {
                string moduleName = (moduleNames[index] ?? string.Empty).Trim();
                (byte moduleType, string typeName, byte ioIndex, _) = ParseModuleProfile(moduleName);
                if (!ioSlots.Add((moduleType, ioIndex)))
                    throw new ArgumentException($"Module I/O slot '{moduleName}' is defined more than once.", nameof(moduleNames));

                LinkGroup module = groups[index] = parentGroup.AddGroup(
                    moduleName, moduleName,
                    $"{moduleName}; fixed DM, AM, EM order. Physical modules receive compact CAN Node IDs.");

                module.IconKey = "module";

                TextLink mode = module.AddText(
                    "Mode", "Mode",
                    DeviceMode.Physical.ToString(),
                    [DeviceMode.Physical.ToString(), DeviceMode.Virtual.ToString()],
                    "Physical requires the CAN module; Virtual preserves its definition without requiring hardware.");
                mode.View = AccessView.ReadWrite;

                IntegerLink uid = module.AddInteger(
                    "UID", "UID",
                    0,
                    0,
                    uint.MaxValue,
                    IntegerFormat.Hexadecimal,
                    8,
                    "Permanent 32-bit module UID. Engineering may edit and save it in any connection state; the new UID takes effect on the next connection.");
                uid.View = AccessView.ReadWrite;
                uid.Permission = AccessPermission.Engineering;
                uid.UnauthorizedView = AccessView.ReadOnly;
                uid.ShowHexadecimalPrefix = false;

                ConfigureReadOnlyWhileControllerSessionActive(mode);

                TextLink name = module.AddText(
                    "Name", "Name",
                    moduleName,
                    "Stable module name and PLC I/O index.");
                name.Value = moduleName;
                name.View = AccessView.ReadOnly;

                TextLink type = module.AddText(
                    "Type", "Type",
                    typeName,
                    "Module family derived from its stable name.");
                type.Value = typeName;
                type.View = AccessView.ReadOnly;
                mode.Changed += (_, _) => RefreshEditedModule(module);
                uid.Changed += (_, _) => RefreshEditedModule(module);
            }

            return groups;
        }

        private static (byte Type, string TypeName, byte IoIndex, uint Configuration)
            ParseModuleProfile(string moduleName)
        {
            // Supported families and index limits match the fixed PLC symbol maps.
            // Accepting a broader name here would create an unusable symbol.
            if (moduleName.Length < 3
                || !int.TryParse(moduleName.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out int ioIndex))
            {
                throw new ArgumentException(
                    $"Module name '{moduleName}' must contain a two-letter family and a zero-based index, such as DM0 or AM0.",
                    nameof(moduleName));
            }

            string prefix = moduleName[..2].ToUpperInvariant();
            return prefix switch
            {
                "DM" when ioIndex is >= 0 and < 8 =>
                    (MicroModuleTypes.Digital, "Digital", (byte)ioIndex, 0x0000AAAA),
                "AM" when ioIndex is >= 0 and < 2 =>
                    (MicroModuleTypes.Analog, "Analog", (byte)ioIndex, AnalogModuleConfiguration.Default),
                "EM" when ioIndex is >= 0 and < 2 =>
                    (MicroModuleTypes.Encoder, "Encoder", (byte)ioIndex, 0),
                "DM" => throw new ArgumentOutOfRangeException(
                    nameof(moduleName),
                    "Digital module indices must be from DM0 through DM7."),
                "AM" => throw new ArgumentOutOfRangeException(
                    nameof(moduleName),
                    "Analog module indices must be AM0 or AM1."),
                "EM" => throw new ArgumentOutOfRangeException(
                    nameof(moduleName),
                    "Encoder module indices must be EM0 or EM1."),
                _ => throw new ArgumentException(
                    $"Module family '{prefix}' is not supported. Use DM, AM, or EM.",
                    nameof(moduleName))
            };
        }

        private void ApplyModuleProperties()
        {
            // Module topology is frozen for the complete Connecting/Online session.
            // UID edits made during that session stay staged in the Link until reconnect.
            if (!controller.ConnectionState.AllowsOfflineConfiguration())
                return;

            // Initial loading must leave an invalid saved draft editable. Acceptance
            // and Connect validate it; replacing an existing topology validates here too.
            if (!SelectedModuleGroups().SequenceEqual(moduleGroups) && !ValidateModuleTopology())
                throw new InvalidOperationException(modulesGroup.StateMessage);
            RefreshModuleSelection();
            WriteModuleProperties(moduleGroups);
        }

        private void WriteModuleProperties(IReadOnlyList<LinkGroup> groups)
        {
            ModuleCollection modules = controller.PLC.Modules;
            // Rebuild the complete offline topology in profile order. Module slots
            // are stable controller objects; Clear resets their definitions.
            modules.Clear();

            for (int index = 0; index < groups.Count; index++)
            {
                LinkGroup group = groups[index];
                Module module = modules[index];
                string moduleName = group.Name;
                (byte moduleType, _, byte ioIndex, uint configuration) =
                    ParseModuleProfile(moduleName);

                module.Name = moduleName;
                module.Uid = checked((uint)group.ReadInteger("UID"));
                module.PlcIndex = ioIndex;
                module.Type = moduleType;
                module.InternalConfiguration = moduleType == MicroModuleTypes.Digital
                    ? controller.PLC.IOMappings.GetDigitalInputConfiguration(ioIndex)
                    : configuration;
                module.Mode = ReadDeviceMode(group);
            }
        }

        private static DeviceMode ReadDeviceMode(LinkGroup group)
        {
            string value = group.ReadText("Mode");
            if (!Enum.TryParse(value, true, out DeviceMode mode) || !Enum.IsDefined(mode))
                throw new InvalidOperationException($"'{value}' is not a supported device mode.");

            return mode;
        }

        private void RefreshEditedModule(LinkGroup group)
        {
            if (moduleGroups is null) return;
            int index = Array.IndexOf(moduleGroups, group);
            RefreshModuleState(group, index < 0 ? null : controller.PLC.Modules[index]);
            ValidateModuleTopology();
        }

        private void RefreshModuleState(LinkGroup group, Module? module)
        {
            DeviceMode requestedMode = ReadDeviceMode(group);
            uint requestedUid = checked((uint)group.ReadInteger("UID"));
            bool pendingChanges = module is not null && !controller.ConnectionState.AllowsOfflineConfiguration()
                && (module.Mode != requestedMode || module.Uid != requestedUid);
            OperationalState state;
            string message;

            if (requestedMode == DeviceMode.Physical && requestedUid == 0)
                (state, message) = (OperationalState.Error, $"{group.ReadText("Name")}: No module UID entered.");
            else if (requestedMode == DeviceMode.Virtual)
                (state, message) = (OperationalState.Inactive, "Module is virtual; no physical CAN hardware is required.");
            else if (module is null || !controller.IsConnected)
                (state, message) = (OperationalState.Inactive, "Module is configured while the controller is offline.");
            else if (!module.IsOnline)
                (state, message) = (OperationalState.Error, "Configured module is offline.");
            else if (!module.IsConfigurationApplied)
                (state, message) = (OperationalState.Warning, "Module configuration is not applied.");
            else
                (state, message) = (OperationalState.Valid, "Module is online and configured.");

            if (pendingChanges)
                message += $" Module configuration will change to {requestedMode} after reconnect.";
            group.SetStateAndPendingChanges(state, pendingChanges, message);
        }

        private void RefreshModuleStates()
        {
            ModuleCollection modules = controller.PLC.Modules;
            for (int index = 0; index < moduleGroups.Length; index++)
            {
                RefreshModuleState(moduleGroups[index], modules[index]);
            }

            SetAggregateState(modulesGroup, moduleGroups);
            CaptureModuleRuntimeStates();
        }

        private bool[]? moduleOnlineStates;
        private bool[]? moduleConfigurationStates;

        private void CaptureModuleRuntimeStates()
        {
            ModuleCollection modules = controller.PLC.Modules;
            if (moduleOnlineStates?.Length != moduleGroups.Length) moduleOnlineStates = new bool[moduleGroups.Length];
            if (moduleConfigurationStates?.Length != moduleGroups.Length) moduleConfigurationStates = new bool[moduleGroups.Length];
            for (int index = 0; index < moduleGroups.Length; index++)
            {
                moduleOnlineStates[index] = modules[index].IsOnline;
                moduleConfigurationStates[index] = modules[index].IsConfigurationApplied;
            }
        }

        internal bool RefreshModuleRuntimeStatesIfChanged()
        {
            ModuleCollection modules = controller.PLC.Modules;
            if (moduleOnlineStates is null || moduleConfigurationStates is null)
            {
                RefreshModuleStates();
                return true;
            }

            for (int index = 0; index < moduleGroups.Length; index++)
            {
                if (moduleOnlineStates[index] == modules[index].IsOnline &&
                    moduleConfigurationStates[index] == modules[index].IsConfigurationApplied)
                    continue;

                RefreshModuleStates();
                return true;
            }

            return false;
        }
    }
}

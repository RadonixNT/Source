using NT;
using Microsoft.Win32;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using System.Text;

namespace RouterXYZACRTCP
{
    /// <summary>
    /// Defines the fixed machine profile, ordered PLC document selection,
    /// and application-owned runtime paths.
    /// LinkGroup construction belongs to ControllerLinks and GeneralLinks.
    /// </summary>
    internal static class Initialize
    {
        // This is a compiled machine profile, not user-editable runtime state.
        // ControllerLinks creates persisted Links for values operators may change.
        internal const string InterfaceName = "RouterXYZAC+RTCP";
        // Explicit machine-profile feed in mm/s before the first F word.
        internal const double InitialRouterFeedVelocity = 200.0;
        // Programmer-owned ordering for commands in one block and restart preambles.
        // Lower values run first; unlisted PLC/macro codes default to zero.
        // These values are compiled into the interface and are never persisted as Links.
        internal static readonly IReadOnlyDictionary<string, int> ProgramPriorities =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["S"] = 10,
                ["M3"] = 20,
                ["M4"] = 20,
                ["M5"] = 20,
                ["M2"] = 100,
                ["M30"] = 100
            };
        internal const string HomePlcProgramName = "Home XYZAC";
        internal const string RunTP0PlcProgramName = "RunTP0";
        internal const string PositionReturnPlcProgramName = "PositionReturn";
        internal const string PositionAxialReturnPlcProgramName = "PositionAxialReturn";
        internal const string PositionRtcpApproachPlcProgramName = "PositionRtcpApproach";
        internal const string PositionRtcpDeparturePlcProgramName = "PositionRtcpDeparture";
        internal const string SharedPlcVariablesName = "Shared";

        // One profile defines primary axes, gantry defaults and the retained slot order.
        // Slot order is independent of the enabled defaults: changing a default must
        // not redirect stored axis settings to another motor.
        internal sealed record MainAxisDefinition(string Name, int Order, int? GantryOrder, bool GantryByDefault);
        internal static readonly MainAxisDefinition[] AxisProfile =
        [
            new("X", 0, 4, false),
            new("Y", 1, 2, false),
            new("Z", 3, 5, false),
            new("A", 6, null, false),
            new("C", 7, null, false)
        ];
        internal sealed record AxisDefinition(string Name, int Order, string? Master, bool EnabledByDefault)
        {
            internal string StorageName => Order < 4 ? $"Axis{Order:00}" : Name;
        }
        internal static readonly string[] MainAxes = AxisProfile.Select(axis => axis.Name).ToArray();
        internal static readonly AxisDefinition[] AxisDefinitions = AxisProfile.SelectMany(axis => axis.GantryOrder is int gantryOrder
            ? new[] { new AxisDefinition(axis.Name, axis.Order, null, true),
                new AxisDefinition(axis.Name + "G", gantryOrder, axis.Name, axis.GantryByDefault) }
            : new[] { new AxisDefinition(axis.Name, axis.Order, null, true) }).OrderBy(axis => axis.Order).ToArray();
        internal static int DefaultAxisCount => AxisDefinitions.Count(axis => axis.EnabledByDefault);
        internal const int DefaultInverterCount = 0;
        internal static readonly string[] ToolPathAxes = ["XYZAC", "XYZAC", "XYZAC"];
        internal const int DefaultDigitalModuleCount = 2;
        internal const int DefaultAnalogModuleCount = 1;
        internal const int DefaultEncoderModuleCount = 0;

        internal sealed record PlcIoSignal(
            string Name,
            PLCIOMappingType Type,
            string DefaultTarget = "",
            PLCDigitalInputFilterMode FilterMode = PLCDigitalInputFilterMode.Normal,
            string Sensor = "",
            string DisplayName = "");

        // Signal names are the stable machine contract used by PLC source.
        // Physical channels remain editable and raw DM/AM symbols stay available.
        internal static readonly PlcIoSignal[] PlcIoSignals =
        [
            new("HomeSensorX", PLCIOMappingType.DigitalInput, "DM0.IN0", DisplayName: "Home Sensor X"),
            new("HomeSensorY", PLCIOMappingType.DigitalInput, "DM0.IN1", DisplayName: "Home Sensor Y"),
            new("HomeSensorZ", PLCIOMappingType.DigitalInput, "DM0.IN2", DisplayName: "Home Sensor Z"),
            new("HomeSensorA", PLCIOMappingType.DigitalInput, "DM0.IN4", DisplayName: "Home Sensor A"),
            new("HomeSensorC", PLCIOMappingType.DigitalInput, "DM0.IN5", DisplayName: "Home Sensor C"),
            new("HomeSensorYG", PLCIOMappingType.DigitalInput, "DM0.IN3", DisplayName: "Home Sensor YG"),
            new("HomeSensorXG", PLCIOMappingType.DigitalInput, "Unassigned", DisplayName: "Home Sensor XG"),
            new("HomeSensorZG", PLCIOMappingType.DigitalInput, "Unassigned", DisplayName: "Home Sensor ZG"),
            new("EmergencySwitch", PLCIOMappingType.DigitalInput, "DM0.IN7", DisplayName: "Emergency Switch"),

            new("SpindleToolClamped", PLCIOMappingType.DigitalInput, "DM1.IN0", Sensor: "S1", DisplayName: "Spindle Tool Clamped"),
            new("SpindleToolReleased", PLCIOMappingType.DigitalInput, "DM1.IN1", Sensor: "S2", DisplayName: "Spindle Tool Released"),
            new("SpindleRotationSensor", PLCIOMappingType.DigitalInput, "DM1.IN2",
                FilterMode: PLCDigitalInputFilterMode.HighSpeed, Sensor: "S3", DisplayName: "Spindle Rotation Sensor"),
            new("SpindleThermalOK", PLCIOMappingType.DigitalInput, "DM1.IN6", DisplayName: "Spindle Thermal OK"),
            new("SpindleDriveReady", PLCIOMappingType.DigitalInput, "DM1.IN7", DisplayName: "Spindle Drive Ready"),

            new("StopSwitch", PLCIOMappingType.DigitalInput, DisplayName: "Reset Switch"),
            new("RunSwitch", PLCIOMappingType.DigitalInput, DisplayName: "Run Switch"),
            new("PauseSwitch", PLCIOMappingType.DigitalInput, DisplayName: "Pause Switch"),
            new("SpindlePistonRetracted", PLCIOMappingType.DigitalInput, Sensor: "S5", DisplayName: "Spindle Piston Retracted"),
            new("PressureSensor", PLCIOMappingType.DigitalInput, DisplayName: "Pressure Sensor"),

            new("SpindleCW", PLCIOMappingType.DigitalOutput, "DM0.OUT0", DisplayName: "Spindle CW"),
            new("SpindleCCW", PLCIOMappingType.DigitalOutput, DisplayName: "Spindle CCW"),

            new("Lubricant", PLCIOMappingType.DigitalOutput, "DM0.OUT1"),
            new("Coolant", PLCIOMappingType.DigitalOutput),

            new("Alarm", PLCIOMappingType.DigitalOutput, "DM0.OUT6"),
            new("Brake", PLCIOMappingType.DigitalOutput, "DM0.OUT7"),
            new("SpindleSpeed", PLCIOMappingType.AnalogOutput, "AM0.OUT0", DisplayName: "Spindle Speed")
        ];

        internal sealed record JogInput(string Signal, string Command, bool Hold);
        internal static readonly JogInput[] JogInputs =
        [
            .. MainAxes.SelectMany(axis => new JogInput[]
            {
                new($"Jog{axis}Minus", $"Jog {axis}-", true),
                new($"Jog{axis}Plus", $"Jog {axis}+", true),
                new($"StepJog{axis}Minus", $"Step Jog {axis}-", false),
                new($"StepJog{axis}Plus", $"Step Jog {axis}+", false),
                new($"GoTo{axis}Switch", $"GoTo {axis}", false)
            }),
        ];

        static Initialize()
        {
            PlcIoSignals = PlcIoSignals.Concat(JogInputs.Select(input =>
                new PlcIoSignal(input.Signal, PLCIOMappingType.DigitalInput, DisplayName: input.Command))).ToArray();
            // Fail during application startup instead of allowing an inconsistent
            // machine profile to reach ControllerWrapper or the PLC compiler.
            ValidateProfile();
        }

        /// <summary>
        /// Reads one deliberately selected PLC document from this assembly's embedded factory library.
        /// Names are restricted to a single extension-free file name and map to one stable logical resource name.
        /// </summary>
        internal static string GetPlcSource(string name)
        {
            string normalizedName = (name ?? string.Empty).Trim();
            if (normalizedName.Length == 0)
                throw new ArgumentException("PLC document name is required.", nameof(name));

            if (!string.Equals(normalizedName, Path.GetFileNameWithoutExtension(normalizedName), StringComparison.Ordinal)
                || normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(
                    $"PLC document name '{normalizedName}' must be a file name without a path or extension.",
                    nameof(name));
            }

            string resourceName = $"RouterXYZACRTCP.PLC.{normalizedName}.plc";
            Stream stream = typeof(Initialize).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded PLC source was not found: {resourceName}");
            using (stream)
            using (var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true))
            {
                return reader.ReadToEnd();
            }
        }

        private static void ValidateProfile()
        {

            if (AxisDefinitions.Length <= 0 || AxisDefinitions.Length > ControllerWrapper.MaxAxisCount)
            {
                throw new InvalidOperationException(
                    $"Axis profile count must be between 1 and {ControllerWrapper.MaxAxisCount}.");
            }

            if (ToolPathAxes.Length <= 0 || ToolPathAxes.Length > ControllerWrapper.MaxToolPathCount)
            {
                throw new InvalidOperationException(
                    $"ToolPathAxes count must be between 1 and {ControllerWrapper.MaxToolPathCount}.");
            }

            HashSet<string> axisNames = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < AxisDefinitions.Length; index++)
            {
                AxisDefinition definition = AxisDefinitions[index];
                string axisName = NormalizeAxisName(definition.Name, $"AxisDefinitions[{index}]");
                if (axisName != definition.Name || definition.Order != index)
                    throw new InvalidOperationException("Axis profile requires canonical names and unique contiguous slot orders.");

                if (!axisNames.Add(axisName))
                    throw new InvalidOperationException($"Duplicate controller axis name: '{axisName}'.");
            }

            for (int toolPathIndex = 0; toolPathIndex < ToolPathAxes.Length; toolPathIndex++)
            {
                // Use the controller's authoritative compact-name parser. This keeps
                // names such as XGYZ unambiguous and avoids a second UI-side grammar.
                string normalizedToolPathAxes = NormalizeToolPathAxes(ToolPathAxes[toolPathIndex], toolPathIndex);
                ToolPathAxes[toolPathIndex] = normalizedToolPathAxes;

                foreach (string axisName in Axis.ParseCompactNames(normalizedToolPathAxes))
                {
                    if (!AxisDefinitions.Any(axis => axis.Name == axisName && axis.EnabledByDefault))
                    {
                        throw new InvalidOperationException(
                            $"ToolPathAxes[{toolPathIndex}] uses undefined axis '{axisName}'.");
                    }
                }
            }
        }

        private static string NormalizeAxisName(string axisName, string sourceName)
        {
            try
            {
                return Axis.NormalizeName(axisName);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    $"{sourceName} must be a letter optionally followed by one digit, or XG, YG, ZG.",
                    ex);
            }
        }

        private static string NormalizeToolPathAxes(string toolPathAxes, int toolPathIndex)
        {
            IReadOnlyList<string> names;
            try
            {
                names = Axis.ParseCompactNames(toolPathAxes);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"ToolPathAxes[{toolPathIndex}] is invalid: {ex.Message}", ex);
            }

            HashSet<string> usedAxes = new(StringComparer.OrdinalIgnoreCase);
            foreach (string axisName in names)
            {
                if (!usedAxes.Add(axisName))
                {
                    throw new InvalidOperationException(
                        $"ToolPathAxes[{toolPathIndex}] contains duplicate axis '{axisName}'.");
                }
            }

            return string.Concat(names);
        }

        internal static RegistryKey RegistryRoot => Registry.CurrentUser.CreateSubKey("Software\\Radonix")
            ?? throw new InvalidOperationException("Unable to open HKCU\\Software\\Radonix for settings.");

        internal static string ReportPath => Path.Combine(NTLog.AppDataPath, "Report");

        internal static string EniPath => Path.Combine(NTLog.AppDataPath, "Network", "ENI.xml");
    }
}

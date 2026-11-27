using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;

namespace RouterXYZBCTC
{
    internal sealed record AxisJogCommandLinks(
        string AxisName,
        CommandLink Negative,
        CommandLink Positive,
        CommandLink StepNegative,
        CommandLink StepPositive,
        CommandLink GoToPosition);

    internal sealed record XYJogCommandLinks(
        CommandLink NegativeXPositiveY,
        CommandLink PositiveXPositiveY,
        CommandLink NegativeXNegativeY,
        CommandLink PositiveXNegativeY);

    /// <summary>
    /// Axis-specific portion of <see cref="ControllerLinks"/>: settings,
    /// runtime presentation state, jog commands, and the configured home command.
    /// </summary>
    internal sealed partial class ControllerLinks
    {
        private const string IndependentCouplingMaster = "None";

        private readonly LinkGroup axesGroup;
        private LinkGroup[] axisGroups;
        private GeneralLinks generalLinks = null!;
        private readonly Dictionary<string, AxisWrapper[]> homeGroups = new(StringComparer.Ordinal);

        internal IReadOnlyList<AxisJogCommandLinks> AxisJogCommands { get; private set; } = [];
        internal XYJogCommandLinks XYJogCommands { get; private set; } = null!;
        internal CommandLink HomeLink { get; private set; } = null!;
        internal CommandLink HomeXLink { get; private set; } = null!;
        internal CommandLink HomeYLink { get; private set; } = null!;
        internal CommandLink HomeZLink { get; private set; } = null!;
        internal CommandLink HomeBLink { get; private set; } = null!;
        internal CommandLink HomeCLink { get; private set; } = null!;

        private LinkGroup[] CreateAxisGroups(LinkGroup parentGroup)
        {
            var groups = new LinkGroup[Initialize.AxisDefinitions.Length];

            for (int index = 0; index < groups.Length; index++)
            {
                Initialize.AxisDefinition definition = Initialize.AxisDefinitions[index];
                string axisName = definition.Name;
                bool isRotary = axisName is "B" or "C";
                bool isZ = axisName.StartsWith("Z", StringComparison.Ordinal);
                (double defaultMinCourse, double defaultMaxCourse) = axisName.ToUpperInvariant() switch
                {
                    "X" or "XG" => (0.0, 3000.0),
                    "Y" or "YG" => (0.0, 2000.0),
                    "Z" or "ZG" => (-500.0, 0.0),
                    "B" => (-180.0, 180.0),
                    "C" => (-360.0, 360.0),
                    _ => (-2000.0, 2000.0),
                };
                LinkGroup axis = groups[index] = parentGroup.AddGroup(
                    definition.StorageName, axisName,
                    $"Configuration for axis {axisName}.");
                CreateAbsoluteReferenceLinks(axis);
                axisGroupNames.Add(axis, axisName);

                axis.IconKey = "axis";
                TextLink mode = axis.AddText(
                    "Mode", "Mode",
                    DeviceMode.Physical.ToString(),
                    [DeviceMode.Physical.ToString(), DeviceMode.Virtual.ToString()],
                    "Physical requires a hardware drive; Virtual keeps only the logical axis definition. Changes are available when the controller connection is inactive.");
                axis.AddInteger("InPositionTolerance", "In Position Tolerance", 5, 0, int.MaxValue,
                    "Maximum absolute difference between encoder feedback and command position, in encoder counts.");
                axis.AddReal("Acceleration", "Acceleration", 500.0, 0.001, 50000.0, 3, "Configured acceleration limit in axis units per second squared. A coupled group uses its master's acceleration limit.");
                axis.AddReal("Deceleration", "Deceleration", 500.0, 0.001, 50000.0, 3, "Configured deceleration limit in axis units per second squared. A coupled group uses its master's deceleration limit, including controlled stops.");
                TextLink direction = axis.AddText(
                    "Direction", "Direction",
                    "Positive",
                    "Logical positive direction; it can change only when no controller connection session is active.",
                    "Positive",
                    "Negative");
                RealLink homePosition = axis.AddReal("HomePosition", "Home Position", 0.0, -1000000000.0, 1000000000.0, 3, "Final homing coordinate in the configured axis unit; changes are available only while the controller is offline.");
                axis.AddText(
                    "HomeDirection", "Home Direction",
                    isZ ? "Positive" : "Negative",
                    "Sensor-search direction for the next home activation. Sensor release moves in the opposite direction.",
                    "Positive",
                    "Negative");
                axis.AddReal(
                    "HomeVelocity", "Home Velocity",
                    isZ ? 20.0 : 40.0,
                    0.000001,
                    1000000.0,
                    3,
                    "Sensor-search velocity in axis units per second used by the machine Home PLC program.");
                axis.AddReal(
                    "HomeReturnVelocity", "Home Return Velocity",
                    isZ ? 2.0 : 4.0,
                    0.000001,
                    1000000.0,
                    3,
                    "Sensor-release velocity in axis units per second used by the machine Home PLC program.");
                RealLink homeDisplace = axis.AddReal(
                    "HomeDisplace", "Home Displace",
                    0.0,
                    -1000000000.0,
                    1000000000.0,
                    3,
                    "Signed physical displacement in the configured axis unit after sensor homing; the final coordinate is HomePosition. Changes are available only while the controller is offline.");
                axis.AddReal("MaxCourse", "Max Course", defaultMaxCourse, -1000000000.0, 1000000000.0, 3, "Maximum allowed command position in the configured axis unit.");
                RealLink maxCouplingError = axis.AddReal("MaxCouplingError", "Max Coupling Error", 5.0, 0.000001, 1000000000.0, 6, "Maximum permitted gantry following error in engineering units.");
                TextLink couplingMaster = axis.AddText(
                    "CouplingMaster", "Coupling Master",
                    definition.Master ?? IndependentCouplingMaster,
                    "Master axis selected automatically by the gantry configuration.");
                couplingMaster.View = AccessView.ReadOnly;
                couplingMaster.Value = definition.Master ?? IndependentCouplingMaster;
                axis.AddReal("MinCourse", "Min Course", defaultMinCourse, -1000000000.0, 1000000000.0, 3, "Minimum allowed command position in the configured axis unit.");
                axis.AddReal("Smoothness", "Smoothness", 50.0, 1.0, ControllerWrapper.MaxAxisSmoothness, 0, "Motion smoothing amount.");
                RealLink step = axis.AddReal(
                    "Step", "Step",
                    0.001,
                    0.000001,
                    1000000.0,
                    6,
                    "Positive engineering-unit magnitude per encoder count; it can change only when no controller connection session is active.");
                axis.AddText("Unit", "Unit", isRotary ? "degree" : "mm", "Engineering unit used by this axis.", "mm", "degree", "pulse");
                axis.AddReal("VelocityLimit", "Velocity Limit", 200.0, 0.001, 1000000.0, 3, "Configured velocity limit in axis units per second. A coupled group uses its master's velocity limit.");
                TextLink name = axis.AddText(
                    "Name", "Name",
                    axisName,
                    "Program name for this controller axis; changes are available when the controller connection is inactive.");
                name.View = AccessView.ReadOnly;
                name.Value = axisName;

                ConfigureReadOnlyWhileControllerSessionActive(mode, name, direction, step, maxCouplingError, couplingMaster, homePosition, homeDisplace);
            }

            return groups;
        }

        private void ApplyAxisProperties()
        {
            // Names are applied as one set because aliases and ToolPath axis lists
            // depend on a globally unique controller-axis catalog.
            if (controller.ConnectionState.AllowsOfflineConfiguration())
            {
                LinkGroup[] candidate = SelectedAxisGroups();
                if (!candidate.SequenceEqual(axisGroups) && !ValidateAxisTopology())
                    throw new InvalidOperationException(axesGroup.StateMessage);
                string[] names = candidate.Select(group => axisGroupNames[group]).ToArray();
                if (!names.SequenceEqual(controller.Axes.Select(axis => axis.Name)))
                    controller.ConfigureAxes(names);
                axisGroups = candidate;
                controller.SetAxisNames(axisGroups
                    .Select(group => axisGroupNames[group])
                    .ToArray());

                for (int index = 0; index < axisGroups.Length; index++)
                    controller.Axes[index].Mode = ReadDeviceMode(axisGroups[index]);

                // Clear the previous topology first so a valid edited star does
                // not fail because its old relationships form a transient chain.
                for (int index = 0; index < axisGroups.Length; index++)
                    controller.Axes[index].CouplingMaster = null;

                for (int index = 0; index < axisGroups.Length; index++)
                {
                    AxisWrapper axis = controller.Axes[index];
                    string axisName = axisGroupNames[axisGroups[index]];
                    string masterName = axisName.Length == 2 ? axisName[..1] : IndependentCouplingMaster;
                    axis.CouplingMaster = masterName.Equals(IndependentCouplingMaster, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : controller.GetAxisByName(masterName);
                }
            }

            for (int index = 0; index < axisGroups.Length; index++)
            {
                LinkGroup group = axisGroups[index];
                AxisWrapper axis = controller.Axes[index];

                // Physical topology and encoder scale/direction are session inputs.
                // Keep the active session stable and show pending edits as a badge.
                axis.InPositionTolerance = checked((int)group.ReadInteger("InPositionTolerance"));
                axis.Acceleration = group.ReadReal("Acceleration");
                axis.Deceleration = group.ReadReal("Deceleration");
                axis.MinCourse = group.ReadReal("MinCourse");
                axis.MaxCourse = group.ReadReal("MaxCourse");
                axis.Smoothness = checked((long)group.ReadReal("Smoothness", 0));
                if (controller.ConnectionState.AllowsOfflineConfiguration())
                {
                    axis.HomePosition = group.ReadReal("HomePosition");
                    axis.Step = ReadConfiguredAxisStep(group);
                    ApplyAbsoluteReference(group, controller.MotionController.Axes[index]);
                    axis.MaxCouplingError = group.ReadReal("MaxCouplingError");
                }
                axis.VelocityLimit = group.ReadReal("VelocityLimit");
            }
            foreach (string name in Initialize.MainAxes)
                homeGroups[name] = controller.Axes.Where(axis => axis.Name == name || axis.CouplingMaster?.Name == name).ToArray();
        }

        private static double ReadConfiguredAxisStep(LinkGroup group)
        {
            double stepMagnitude = group.ReadReal("Step");
            return group.ReadText("Direction") switch
            {
                "Positive" => stepMagnitude,
                "Negative" => -stepMagnitude,
                string direction => throw new InvalidOperationException(
                    $"Axis direction '{direction}' must be Positive or Negative."),
            };
        }

        private void RefreshAxisStates()
        {
            for (int index = 0; index < axisGroups.Length; index++)
            {
                AxisWrapper axis = controller.Axes[index];
                LinkGroup group = axisGroups[index];
                bool pendingChanges = !controller.ConnectionState.AllowsOfflineConfiguration()
                    && (axis.Mode != ReadDeviceMode(group)
                        || axis.Step != ReadConfiguredAxisStep(group)
                        || axis.HomePosition != group.ReadReal("HomePosition")
                        || !axis.Name.Equals(group.ReadText("Name"), StringComparison.Ordinal)
                        || !ConfiguredCouplingMasterMatches(axis, group));
                OperationalState state;
                string message;
                if (axis.Mode == DeviceMode.Virtual)
                    (state, message) = (OperationalState.Inactive, "Axis is virtual; no physical drive is required.");
                else if (!controller.IsConnected)
                    (state, message) = (OperationalState.Inactive, "Controller is offline.");
                else if (axis.HasFault)
                    (state, message) = (OperationalState.Error, "Axis reports a drive fault.");
                else if (axis.HasWarning)
                    (state, message) = (OperationalState.Warning, "Axis reports a warning.");
                else if (!axis.IsExisting || !axis.IsPdoMapped)
                    (state, message) = (OperationalState.Inactive, "Axis is not available or its PDO is not mapped.");
                else if (axis.IsMoving || axis.IsRunning || axis.IsJogging || axis.IsHoming)
                    (state, message) = (OperationalState.Active, "Axis is active.");
                else if ((axis.IsEnabled || axis.IsReadyToMove) && !axis.IsHomed)
                    (state, message) = (OperationalState.Warning,
                        $"Axis is ready but not homed; Jog range is temporarily {axis.OperationalMinCourse:G} to {axis.OperationalMaxCourse:G}.");
                else if (axis.IsEnabled || axis.IsReadyToMove)
                    (state, message) = (OperationalState.Valid, "Axis is ready and homed.");
                else
                    (state, message) = (OperationalState.Inactive, "Axis is available but disabled.");

                if (pendingChanges)
                    message += " Axis mode, name, direction, step, home coordinate, or coupling configuration will change after reconnect.";
                group.SetStateAndPendingChanges(state, pendingChanges, message);
            }

            SetAggregateState(axesGroup, axisGroups);
        }

        internal void CreateAxisCommandLinks(GeneralLinks generalLinks)
        {
            this.generalLinks = generalLinks ?? throw new ArgumentNullException(nameof(generalLinks));

            // Only main axes expose operator commands. Resolve each axis by its
            // logical name so topology changes cannot redirect a command to another motor.
            AxisJogCommands = Initialize.MainAxes.Select(controller.GetAxisByName)
                .Select(axis => new AxisJogCommandLinks(
                    axis.Name,
                    CreateJogCommand($"Jog {axis.Name}-", axis.Name, -1),
                    CreateJogCommand($"Jog {axis.Name}+", axis.Name, +1),
                    CreateStepJogCommand($"Step Jog {axis.Name}-", axis.Name, -1),
                    CreateStepJogCommand($"Step Jog {axis.Name}+", axis.Name, +1),
                    CreatePositionJogCommand($"GoTo {axis.Name}", axis.Name)))
                .ToArray();

            int xAxisIndex = controller.GetAxisByName("X").Index;
            int yAxisIndex = controller.GetAxisByName("Y").Index;
            XYJogCommands = new XYJogCommandLinks(
                CreateTwoAxisJogCommand("Jog X- Y+", xAxisIndex, -1, yAxisIndex, +1),
                CreateTwoAxisJogCommand("Jog X+ Y+", xAxisIndex, +1, yAxisIndex, +1),
                CreateTwoAxisJogCommand("Jog X- Y-", xAxisIndex, -1, yAxisIndex, -1),
                CreateTwoAxisJogCommand("Jog X+ Y-", xAxisIndex, +1, yAxisIndex, -1));

            HomeLink = CreateHomeCommand("Home", Initialize.HomePlcProgramName, () => controller.Axes, requiresGantryCoupling: true);
            HomeXLink = CreateHomeCommand("Home X", "HomeX", () => HomeGroup("X"), requiresGantryCoupling: true);
            HomeYLink = CreateHomeCommand("Home Y", "HomeY", () => HomeGroup("Y"), requiresGantryCoupling: true);
            HomeZLink = CreateHomeCommand("Home Z", "HomeZ", () => HomeGroup("Z"), requiresGantryCoupling: true);
            HomeBLink = CreateHomeCommand("Home B", "HomeB", () => HomeGroup("B"), requiresGantryCoupling: true);
            HomeCLink = CreateHomeCommand("Home C", "HomeC", () => HomeGroup("C"), requiresGantryCoupling: true);
        }

        private AxisWrapper[] HomeGroup(string name)
            => homeGroups[name];

        private CommandLink CreateJogCommand(string name, string axisName, int direction)
        {
            CommandLink command = CommandLink.CreateCommand(name, DisplayNames.FromIdentifier(name));
            var continuousSources = new HashSet<ILinkBinding?>();
            command.ActionBegin += (_, e) =>
            {
                if (!generalLinks.JogModeLink.Value && continuousSources.Count > 0)
                {
                    continuousSources.Add(e.SourceBinding);
                    return;
                }
                if (BeginAxisJog(controller.GetAxisIndex(axisName), direction)) continuousSources.Add(e.SourceBinding);
            };
            command.ActionEnd += (_, e) =>
            {
                // One-shot step and position moves must continue after key/button release.
                if (continuousSources.Remove(e.SourceBinding) && continuousSources.Count == 0)
                    controller.StopJog(controller.GetAxisIndex(axisName));
            };
            generalLinks.JogModeLink.Changed += (_, _) =>
            {
                if (continuousSources.Count == 0) return;
                continuousSources.Clear();
                controller.StopJog(controller.GetAxisIndex(axisName));
            };
            command.QueryEnabled += (_, e) =>
                e.Enabled = mainForm.CanJog && IsAxisReadyForJog(controller.GetAxisIndex(axisName));
            return command;
        }

        private bool BeginAxisJog(int axisIndex, int direction)
        {
            if (!CanJogAxis(axisIndex)) return false;
            double velocity = generalLinks.GetJogVelocity(axisIndex);
            if (generalLinks.JogModeLink.Value)
            {
                controller.Jog(axisIndex, direction * generalLinks.GetJogStep(axisIndex), velocity);
                return false;
            }

            return controller.Jog(axisIndex, (double)direction * double.MaxValue, velocity);
        }

        private CommandLink CreateStepJogCommand(string name, string axisName, int direction)
        {
            CommandLink command = CommandLink.CreateCommand(name, DisplayNames.FromIdentifier(name));
            command.ActionExecute += (_, _) => controller.Jog(
                controller.GetAxisIndex(axisName),
                (double)direction * generalLinks.GetJogStep(controller.GetAxisIndex(axisName)),
                generalLinks.GetJogVelocity(controller.GetAxisIndex(axisName)));
            command.QueryEnabled += (_, e) =>
                e.Enabled = mainForm.CanJog && IsAxisReadyForJog(controller.GetAxisIndex(axisName));
            return command;
        }

        private CommandLink CreatePositionJogCommand(string name, string axisName)
        {
            CommandLink command = CommandLink.CreateCommand(name, DisplayNames.FromIdentifier(name));
            command.QueryEnabled += (_, e) => e.Enabled = CanPositionJog(controller.GetAxisIndex(axisName));
            command.ActionExecute += (_, _) =>
            {
                // Recheck live Home/readiness at dispatch, including keyboard execution.
                if (!CanPositionJog(controller.GetAxisIndex(axisName)))
                {
                    NTLog.Warning($"{name}: absolute Jog rejected; machine must be homed and available.");
                    return;
                }
                double destination = generalLinks.GetJogPosition(controller.GetAxisIndex(axisName));
                double speed = generalLinks.GetJogVelocity(controller.GetAxisIndex(axisName));
                mainForm.ReportOperation(FormattableString.Invariant($"{name} requested: Position={controller.Axes[controller.GetAxisIndex(axisName)].CommandPosition}; Destination={destination}; Speed={speed} {(axisName is "B" or "C" ? "degree/s" : "mm/s")}"));
                if (!controller.MoveToPosition(controller.GetAxisIndex(axisName), destination, speed))
                    NTLog.Error(NTErrorCode.MotionCommandRejected, NTErrorSeverity.Warning,
                        $"{name}: absolute Jog request was rejected.");
                else mainForm.ReportOperation($"{name}: absolute Jog accepted");
            };
            return command;
        }

        private bool CanPositionJog(int axisIndex)
            => mainForm.CanJog && CanJogAxis(axisIndex) && CanStartHomedMotion();

        private CommandLink CreateTwoAxisJogCommand(
            string name,
            int firstAxisIndex,
            int firstDirection,
            int secondAxisIndex,
            int secondDirection)
        {
            CommandLink command = CommandLink.CreateCommand(name, DisplayNames.FromIdentifier(name));
            var continuousSources = new HashSet<ILinkBinding?>();
            command.ActionBegin += (_, e) =>
            {
                if (!generalLinks.JogModeLink.Value && continuousSources.Count > 0)
                {
                    continuousSources.Add(e.SourceBinding);
                    return;
                }
                if (BeginTwoAxisJog(firstAxisIndex, firstDirection, secondAxisIndex, secondDirection))
                    continuousSources.Add(e.SourceBinding);
            };
            command.ActionEnd += (_, e) =>
            {
                if (continuousSources.Remove(e.SourceBinding) && continuousSources.Count == 0)
                {
                    controller.StopJog(firstAxisIndex);
                    controller.StopJog(secondAxisIndex);
                }
            };
            generalLinks.JogModeLink.Changed += (_, _) =>
            {
                if (continuousSources.Count == 0) return;
                continuousSources.Clear();
                controller.StopJog(firstAxisIndex);
                controller.StopJog(secondAxisIndex);
            };
            command.QueryEnabled += (_, e) =>
                e.Enabled = mainForm.CanJog &&
                    IsAxisReadyForJog(firstAxisIndex) &&
                    IsAxisReadyForJog(secondAxisIndex);
            return command;
        }

        private bool BeginTwoAxisJog(
            int firstAxisIndex,
            int firstDirection,
            int secondAxisIndex,
            int secondDirection)
        {
            // Both requests are published from one UI command before returning to
            // the message loop. If the second is rejected, stop the first instead
            // of leaving a partial diagonal command active.
            if (!CanJogAxes(firstAxisIndex, secondAxisIndex))
                return false;

            bool stepJog = generalLinks.JogModeLink.Value;
            double firstValue = stepJog
                ? firstDirection * generalLinks.GetJogStep(firstAxisIndex)
                : (double)firstDirection * double.MaxValue;
            double secondValue = stepJog
                ? secondDirection * generalLinks.GetJogStep(secondAxisIndex)
                : (double)secondDirection * double.MaxValue;

            bool firstStarted = controller.Jog(
                firstAxisIndex,
                firstValue,
                generalLinks.GetJogVelocity(firstAxisIndex));
            if (!firstStarted)
                return false;

            bool secondStarted = controller.Jog(
                secondAxisIndex,
                secondValue,
                generalLinks.GetJogVelocity(secondAxisIndex));
            if (!secondStarted)
            {
                controller.StopJog(firstAxisIndex);
                return false;
            }
            return !stepJog;
        }

        private bool CanJogAxis(int axisIndex)
            => CanJogNow() && IsAxisReadyForJog(axisIndex);

        private bool CanJogAxes(int firstAxisIndex, int secondAxisIndex)
            => CanJogNow()
                && IsAxisReadyForJog(firstAxisIndex)
                && IsAxisReadyForJog(secondAxisIndex);

        internal bool AreJogCommandsAvailable => mainForm.CanJog;

        private bool CanJogNow()
        {
            if (!mainForm.IsMachineOnline
                || mainToolPath.IsProgramActive
                || headPositioning?.IsActive == true
                || mainForm.toolChangerLinks?.IsChangingTool == true
                || IsHomingActive())
            {
                return false;
            }

            return true;
        }

        private bool IsAxisReadyForJog(int axisIndex)
            => (uint)axisIndex < (uint)controller.Axes.Length
                && controller.Axes[axisIndex].IsReadyToMove;

        internal bool IsHomingActive()
        {
            foreach (AxisWrapper axis in controller.Axes)
                if (axis.IsHoming)
                    return true;

            return IsPlcProgramRunning(Initialize.HomePlcProgramName)
                || IsPlcProgramRunning("HomeX")
                || IsPlcProgramRunning("HomeY")
                || IsPlcProgramRunning("HomeZ")
                || IsPlcProgramRunning("HomeB")
                || IsPlcProgramRunning("HomeC");
        }

        private bool IsPlcProgramRunning(string name)
            => TryGetPlcProgram(name, out PLCProgram program) && program.IsRunning;

        private CommandLink CreateHomeCommand(
            string name, string programName, Func<AxisWrapper[]> axes, bool requiresGantryCoupling = false)
        {
            CommandLink command = CommandLink.CreateCommand(name, DisplayNames.FromIdentifier(name));
            command.ActionExecute += (_, _) => RunHomeProgram(programName, axes(), requiresGantryCoupling);
            command.QueryEnabled += (_, e) => e.Enabled =
                TryGetPlcProgram(programName, out PLCProgram program)
                && CanRunHomeProgram(program, axes(), requiresGantryCoupling);
            return command;
        }

        private void RunHomeProgram(string programName, AxisWrapper[] axes, bool requiresGantryCoupling)
        {
            mainForm.ReportOperation($"Home requested: PLC={programName}; Axes={string.Join(",", axes.Select(axis => axis.Name))}");
            // Homing executes as a Core-side PLC command program. The UI only asks
            // for activation; ownership, motion, timeout, and result remain in PLC.
            if (!TryGetPlcProgram(programName, out PLCProgram program))
            {
                NTLog.Error(
                    NTErrorCode.PlcRunRequestRejected,
                    NTErrorSeverity.Warning,
                    $"PLC {programName} request rejected: {plcLoadError}");
                return;
            }

            // Recheck at execution as feedback may have changed since UI refresh.
            if (!CanRunHomeProgram(program, axes, requiresGantryCoupling))
            {
                NTLog.Error(
                    NTErrorCode.PlcRunRequestRejected,
                    NTErrorSeverity.Warning,
                    $"PLC {programName} request rejected: program or axes are not ready for homing.");
                return;
            }

            mainForm.ObserveHomeRequest(programName, program);
            if (!program.Run())
            {
                NTLog.Error(
                    program.LastErrorCode ?? NTErrorCode.PlcRunRequestRejected,
                    program.LastErrorSeverity ?? NTErrorSeverity.Warning,
                    $"PLC {programName} request rejected: {program.LastErrorDescriptor?.Summary ?? "program is not ready"}.");
            }
            else mainForm.ReportOperation($"Home request accepted: PLC={programName}; awaiting PLC activation and completion");
        }

        private bool CanRunHomeProgram(PLCProgram program, AxisWrapper[] axes, bool requiresGantryCoupling)
        {
            return mainForm.IsMachineOnline
                && CanStartExclusiveHomeMotion()
                && program.Enabled
                && program.IsDeployed
                && program.IsLoaded
                && !program.IsRunning
                && (!requiresGantryCoupling || IsGantryCoupled())
                && axes.All(axis =>
                    axis.IsReadyToMove
                    && axis.IsStopped
                    && !axis.IsMoving
                    && !axis.IsRunning
                    && !axis.IsJogging
                    && !axis.IsHoming
                    && !axis.IsStopping
                    && axis.Owner == AxisOwner.Controller
                    && !axis.HasCouplingFault);
        }

        private bool CanStartExclusiveHomeMotion()
        {
            if (mainForm.IsTransferActive || mainToolPath.IsPausedForProgram
                || headPositioning?.IsActive == true
                || mainForm.toolChangerLinks?.IsChangingTool == true)
            {
                return false;
            }

            foreach (ToolPathWrapper toolPath in controller.ToolPaths)
                if (!toolPath.IsIdle)
                    return false;

            foreach (AxisWrapper axis in controller.Axes)
                if (axis.IsMoving || axis.IsRunning || axis.IsJogging || axis.IsHoming || axis.IsStopping)
                    return false;

            return true;
        }

        private bool IsGantryCoupled()
        {
            foreach (AxisWrapper slave in controller.Axes)
            {
                AxisWrapper? master = slave.CouplingMaster;
                if (master is null) continue;
                AxisMask expectedMask = master.Mask | slave.Mask;
                if (!(master.CouplingRole == AxisCouplingRole.Master
                && slave.CouplingRole == AxisCouplingRole.Slave
                && slave.CouplingMasterIndex == master.Index
                && master.CoupledAxes == expectedMask
                && slave.CoupledAxes == expectedMask)) return false;
            }
            return true;
        }

        private static bool ConfiguredCouplingMasterMatches(AxisWrapper axis, LinkGroup group)
        {
            string configuredMaster = group.ReadText("CouplingMaster");
            if (configuredMaster.Equals(IndependentCouplingMaster, StringComparison.OrdinalIgnoreCase))
                return axis.CouplingMaster is null;

            return axis.CouplingMaster?.Name.Equals(configuredMaster, StringComparison.OrdinalIgnoreCase) == true;
        }

        private void CreateAxisHomeApplicationVariables(PLCWrapper plc)
        {
            foreach (LinkGroup axisGroup in allAxisGroups)
            {
                string axisName = axisGroup.ReadText("Name");
                PLCVariableBridge.AddDirectionApplicationVariable(
                    plc,
                    (TextLink)axisGroup.Members["HomeDirection"],
                    $"{axisName}.HomeDirection");
                PLCVariableBridge.AddApplicationVariable(
                    plc,
                    axisGroup.Members["HomeVelocity"],
                    $"{axisName}.HomeVelocity",
                    PLCVariableAccess.ReadOnly);
                PLCVariableBridge.AddApplicationVariable(
                    plc,
                    axisGroup.Members["HomeReturnVelocity"],
                    $"{axisName}.HomeReturnVelocity",
                    PLCVariableAccess.ReadOnly);
                PLCVariableBridge.AddOfflineApplicationVariable(
                    plc,
                    (RealLink)axisGroup.Members["HomeDisplace"],
                    $"{axisName}.HomeDisplace",
                    () => controller.ConnectionState.AllowsOfflineConfiguration());
            }
        }
    }
}

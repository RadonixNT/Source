using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.CompilerServices;
using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.LinkManager;
using Core = NT.Controller.Core;

namespace RouterXYZTC.Simulation.Tests;

public sealed class EntryToolPreparationTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    public void ManualAndGCodeExchangeWaitForRollersBeforeAnyMotion(long mode)
    {
        using var f = new Fixture(currentTool: 0);
        Assert.True(f.Links.ChangeTool(2, mode));
        object pending = typeof(ToolChangerLinks).GetField("pending", Fixture.Hidden)!.GetValue(f.Links)!;
        var descriptions = (string?[])pending.GetType().GetProperty("BlockDescriptions", Fixture.Hidden)!.GetValue(pending)!;
        Assert.Equal("Waiting for enabled rollers to reach their free position.", descriptions[1]);
        f.Links.Cancel();
        Assert.False(f.Links.IsActive);
    }

    [Fact]
    public void SameToolWaitsForAcknowledgedOffsetBeforePositioningWithoutPhysicalExchange()
    {
        using var f = new Fixture(currentTool: 1);
        int positioned = 0;
        long moveGeneration = f.Core.ToolPaths[2].CoordinateResetGeneration;
        Assert.True(f.Links.PrepareStartTool(1, () =>
        {
            Assert.False(f.Links.IsActive);
            Assert.Equal(25, f.Core.ToolPaths[0].Offset[2]);
            positioned++;
            return true;
        }));
        Assert.Equal(1, f.Links.NewTool.Value);
        f.Links.Update();
        Assert.True(f.Core.ToolPaths[0].OffsetApplySnapshot.IsPending);
        Assert.Equal(0, positioned);
        f.AcknowledgeOffset(ToolPathOffsetApplyResult.Applied);
        f.Links.Update();
        Assert.Equal(1, positioned);
        Assert.Equal(moveGeneration, f.Core.ToolPaths[2].CoordinateResetGeneration);
        Assert.Equal(0, f.Command(0));
        Assert.Equal(0, f.M100.ActivationId);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("offset-rejected")]
    [InlineData("cursor-replaced")]
    public void InterruptedPreparationCannotPositionOrStartSpindle(string reason)
    {
        using var f = new Fixture(currentTool: 1);
        Assert.True(f.Links.PrepareStartTool(1, () => throw new Exception("Unexpected positioning")));
        f.Links.Update();
        if (reason == "cancel") f.Links.Cancel();
        if (reason == "cursor-replaced") f.Core.ToolPaths[0].SelectStartRow(1);
        f.AcknowledgeOffset(reason == "offset-rejected" ? ToolPathOffsetApplyResult.InvalidOffset : ToolPathOffsetApplyResult.Applied);
        f.Links.Update();
        Assert.False(f.Links.IsActive);
        Assert.Equal(0, f.M100.ActivationId);
        Assert.Equal(0, f.Command(0));
        Assert.Equal(0, f.Command(1));
    }

    [Fact]
    public void DifferentToolUsesCapturedRequestAndEndsWithdrawalAtSafeZBeforeEntryPositioning()
    {
        using var f = new Fixture(currentTool: 0);
        Assert.True(f.Links.PrepareStartTool(2, () => throw new Exception("Pickup has not completed")));
        Assert.Equal(2, f.Links.NewTool.Value);
        Assert.True(f.Links.IsActive);
        Assert.NotEqual(0, f.Command(2));
        Assert.Equal(0, f.Command(0));
        Assert.Equal(0, f.Command(1));
        object pending = typeof(ToolChangerLinks).GetField("pending", Fixture.Hidden)!.GetValue(f.Links)!;
        var programs = ((System.Collections.IEnumerable)pending.GetType().GetProperty("ActivePrograms", Fixture.Hidden)!
            .GetValue(pending)!).Cast<object>().ToArray();
        Assert.Equal("WaitRollers", ((PLCProgram)programs[0].GetType().GetProperty("Program", Fixture.Hidden | BindingFlags.Public)!
            .GetValue(programs[0])!).Name);
        Assert.Null(pending.GetType().GetProperty("Continuation", Fixture.Hidden)!.GetValue(pending));
        var route = ((System.Collections.IEnumerable)pending.GetType().GetProperty("ReturnRoute", Fixture.Hidden)!
            .GetValue(pending)!).Cast<object>().ToArray();
        Assert.Equal(2, route.Length); // Withdraw from holder, then lift; no stale entry destination.
        var final = (double[])route[^1].GetType().GetProperty("Point")!.GetValue(route[^1])!;
        Assert.Equal(new double[] { 300, 200, 500 }, final);
        f.Links.Cancel();
    }

    [Theory]
    [InlineData("PressureSensor")]
    [InlineData("SpindleThermalOK")]
    [InlineData("SpindleDriveReady")]
    [InlineData("SpindlePistonRetracted")]
    [InlineData("SpindleToolClamped")]
    public void OptionalInterlocksIgnoreAbsenceButRejectAnAssignedLowInput(string signal)
    {
        foreach (var state in new[] { (Target: "", Enabled: true, High: false, Expected: true),
            (Target: "DM0.IN0", Enabled: true, High: false, Expected: false),
            (Target: "DM0.IN0", Enabled: true, High: true, Expected: true),
            (Target: "DM0.IN0", Enabled: false, High: false, Expected: true) })
        {
            using var f = new Fixture(currentTool: 1, optionalSignal: signal,
                optionalTarget: state.Target, optionalEnabled: state.Enabled);
            f.SetDigitalInputs(state.High);
            object[] arguments = [string.Empty];
            bool available = (bool)typeof(ToolChangerLinks).GetMethod("SpindleExchangeAvailable", Fixture.Hidden)!
                .Invoke(f.Links, arguments)!;
            Assert.Equal(state.Expected, available);
            if (!state.Expected) Assert.Contains(signal is "SpindlePistonRetracted" or "SpindleToolClamped" ? "grip feedback" : signal,
                (string)arguments[0]);
        }
    }

    [Theory]
    [InlineData("", true, false, true)]
    [InlineData("DM0.IN0", false, true, true)]
    [InlineData("DM0.IN0", true, false, true)]
    [InlineData("DM0.IN0", true, true, false)]
    public void ReleaseInputRequiresOffOnlyWhenAssigned(string target, bool enabled, bool high, bool expected)
    {
        using var f = new Fixture(1, "SpindleToolReleased", target, enabled);
        f.SetDigitalInputs(high);
        object[] arguments = [string.Empty];
        Assert.Equal(expected, (bool)typeof(ToolChangerLinks).GetMethod("SpindleExchangeAvailable", Fixture.Hidden)!
            .Invoke(f.Links, arguments)!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingGripSensorsStillRequirePlcTimedConfirmationBeforeCommit(bool toolPresent)
    {
        using var f = new Fixture(1, noGripSensors: true);
        var check = typeof(ToolChangerLinks).GetMethod("ConfirmToolFeedback", Fixture.Hidden)!;
        Assert.False((bool)check.Invoke(f.Links, [toolPresent, true])!);
        f.ConfirmPlcStage(toolPresent ? "SpindleClampConfirmed" : "SpindleReleaseConfirmed");
        Assert.True((bool)check.Invoke(f.Links, [toolPresent, true])!);
    }

    [Fact]
    public void GCodeExchangeSplitsTheLastZMoveBeforeResuming()
    {
        using var f = new Fixture(currentTool: 0);
        Assert.True(f.Links.ChangeTool(2, 0));
        object pending = typeof(ToolChangerLinks).GetField("pending", Fixture.Hidden)!.GetValue(f.Links)!;
        var route = ((System.Collections.IEnumerable)pending.GetType().GetProperty("ReturnRoute", Fixture.Hidden)!
            .GetValue(pending)!).Cast<object>().Where(item => item.GetType().Name == "RoutePoint");
        var points = route.Select(item => (
            Point: (double[])item.GetType().GetProperty("Point")!.GetValue(item)!,
            Velocity: (double)item.GetType().GetProperty("Velocity")!.GetValue(item)!)).ToArray();
        Assert.Equal(10, points[^2].Point[2] - points[^1].Point[2], 8);
        Assert.Equal(100, points[^2].Velocity);
        Assert.Equal(5, points[^1].Velocity);
        f.Links.Cancel();
    }

    private sealed class Fixture : IDisposable
    {
        internal const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly object transport;
        private readonly MemoryMappedViewAccessor view;
        private readonly LinkGroup root = new("EntryToolTests", "EntryToolTests", null);
        internal Core Core { get; }
        internal ToolChangerLinks Links { get; }
        internal PLCProgram M100 { get; }
        internal Fixture(int currentTool, string? optionalSignal = null, string optionalTarget = "", bool optionalEnabled = true, bool noGripSensors = false)
        {
            Type memoryType = typeof(Core).Assembly.GetType("NT.Controller.NTSharedMemory")!;
            transport = Activator.CreateInstance(memoryType, Hidden, null,
                ["Local\\Radonix-EntryExchange-" + Guid.NewGuid().ToString("N")], null)!;
            Core = (Core)Activator.CreateInstance(typeof(Core), Hidden, null, [transport], null)!;
            view = (MemoryMappedViewAccessor)memoryType.GetProperty("Accessor", Hidden)!.GetValue(transport)!;
            // Build the normal facades over an isolated memory map, without creating
            // the production named map, connecting hardware or touching user settings.
            var wrapper = (ControllerWrapper)RuntimeHelpers.GetUninitializedObject(typeof(ControllerWrapper));
            void SetWrapper(string name, object value) => typeof(ControllerWrapper).GetField(name, Hidden)!.SetValue(wrapper, value);
            SetWrapper("controller", Core);
            var axes = Core.Axes.Take(3).Select((axis, index) => new AxisWrapper(wrapper, index, axis)).ToArray();
            SetWrapper("<Axes>k__BackingField", axes);
            var paths = Core.ToolPaths.Take(3).Select((path, index) => new ToolPathWrapper(index, path, axes)).ToArray();
            SetWrapper("<ToolPaths>k__BackingField", paths);
            SetWrapper("<PLC>k__BackingField", Activator.CreateInstance(typeof(PLCWrapper), Hidden, null, [Core.PLC], null)!);
            foreach (var path in paths) path.SetToolPathAxes("XYZ");
            foreach (var axis in axes)
            {
                axis.Step = .001;
                axis.MinCourse = -1000;
                axis.MaxCourse = axis.Name == "Z" ? 500 : 1000;
                WriteRegister(C("AxisSharedMemoryLayout", "AxisRegisterBase") + axis.Index * C("AxisSharedMemoryLayout", "AxisRegisterStride") +
                    C("AxisSharedMemoryLayout", "StatusSlot"), C("AxisStatusBits", "ReadyToMove") | C("AxisStatusBits", "Stopped"));
            }
            foreach (var path in paths)
                WriteRegister(C("ToolPathSharedMemoryLayout", "ToolPathRegisterBase") + path.Index * C("ToolPathSharedMemoryLayout", "ToolPathRegisterStride") +
                    C("ToolPathSharedMemoryLayout", "StatusSlot"), C("ToolPathStatusBits", "ReadyToMove") | C("ToolPathStatusBits", "Stopped"));
            WriteRegister(C("SharedRegisterIndex", "DetectedAxisCount"), 3);
            Links = new(wrapper, root, () => 100, () => new PositioningSettings(0, 10, 5), () => true, () => true);
            Links.Tools[0].SetMeasuredLength(25);
            var toolGroup = Links.ToolsGroup.Groups["T02"];
            ((VariableLink<PointD3>)toolGroup.Members["Position"]).Value = new(300, 200, 50);
            ((VariableLink<PointD3>)toolGroup.Members["PlaceVector"]).Value = new(-10, 0, 0);
            ((VariableLink<PointD3>)toolGroup.Members["PickVector"]).Value = new(0, 0, 20);
            Links.CurrentTool.Value = currentTool;
            Core.PLC.SharedVariables.Source = "LOGIC SpindleClampConfirmed := FALSE;\nLOGIC SpindleReleaseConfirmed := FALSE;";
            Core.PLC.ApplicationVariables.AddLogic("SpindleToolChangeConfigured", true);
            foreach (string signal in new[] { "PressureSensor", "SpindleThermalOK", "SpindleDriveReady", "SpindlePistonRetracted", "SpindleToolClamped", "SpindleToolReleased" })
                Core.PLC.IOMappings.Add(signal, PLCIOMappingType.DigitalInput).Target =
                    signal == "SpindleToolReleased" || signal == "SpindleToolClamped" && currentTool == 0 ? "FALSE" : "TRUE";
            if (noGripSensors)
                foreach (string sensor in new[] { "SpindleToolClamped", "SpindleToolReleased" })
                    Core.PLC.IOMappings[sensor].Target = "";
            var module = Core.PLC.Modules[0];
            module.Name = "DM0";
            module.Type = MicroModuleTypes.Digital;
            module.PlcIndex = 0;
            module.Mode = DeviceMode.Virtual;
            if (optionalSignal is not null)
            {
                Core.PLC.IOMappings[optionalSignal].Target = optionalTarget;
                Core.PLC.IOMappings[optionalSignal].Enabled = optionalEnabled;
            }
            foreach (string name in new[] { "M100", "WaitRollers", "StopSpindle", "WaitSpindle", "ReleaseTool", "ClampTool", "Background" })
                Core.PLC.Programs.Add(name, "END_FUNCTION;", cyclic: name == "Background");
            Assert.True(Core.PLC.Validate(), Core.PLC.LastError);
            Assert.True((bool)Core.PLC.GetType().GetMethod("DownloadCompiledPrograms", Hidden)!.Invoke(Core.PLC, [transport])!);
            foreach (PLCProgram program in Core.PLC.Programs)
            {
                long offset = (long)memoryType.GetMethod("PlcProgramHeaderCellOffset")!.Invoke(null,
                    [program.RuntimeIndex, (int)C("NTSharedMemory", "PlcStatusSlot")])!;
                view.Write(offset, C("PLC.PLCProgramStatusBits", "Loaded") |
                    (program.Name == "Background" ? C("PLC.PLCProgramStatusBits", "Running") : 0));
            }
            M100 = Core.PLC.Programs["M100"];
            Core.ToolPaths[0].AddLine(1, [10, 20, 30], 100);
            Core.ToolPaths[0].End();
            typeof(Core).GetField("connectionState", Hidden)!.SetValue(Core, ConnectionState.Online);
        }
        private static long C(string type, string field) => Convert.ToInt64(typeof(Core).Assembly
            .GetType("NT.Controller." + type)!.GetField(field)!.GetRawConstantValue());
        internal void SetDigitalInputs(bool high) => WriteRegister(C("SharedRegisterIndex", "C2MDigitalInput"), high ? 1 : 0);
        internal void ConfirmPlcStage(string name)
        {
            // Emulate PLC-owned feedback, not a queued managed write that needs a running Core.
            var variable = Core.PLC.SharedVariables[name];
            object definition = typeof(PLCVariable).GetField("deployedDefinition", Hidden)!.GetValue(variable)!;
            int index = (int)definition.GetType().GetProperty("BaseAddress", Hidden)!.GetValue(definition)!;
            long offset = C("NTSharedMemory", "PlcLogicValueOffset") + (index / 64) * 8;
            view.Write(offset, view.ReadInt64(offset) | (1L << (index % 64)));
        }
        private void WriteRegister(long index, long value) => view.Write(8 * index, value);
        internal long Command(int path) => view.ReadInt64(8 * (C("ToolPathSharedMemoryLayout", "ToolPathRegisterBase") +
            path * C("ToolPathSharedMemoryLayout", "ToolPathRegisterStride") + C("ToolPathSharedMemoryLayout", "CommandSlot")));
        internal void AcknowledgeOffset(ToolPathOffsetApplyResult result)
        {
            if (result == ToolPathOffsetApplyResult.Applied)
                for (int slot = 0; slot < 3; slot++)
                    view.Write(C("NTSharedMemory", "ToolPathOffsetAppliedOffset") + slot * 8,
                        view.ReadDouble(C("NTSharedMemory", "ToolPathOffsetRequestOffset") + slot * 8));
            WriteRegister(C("ToolPathSharedMemoryLayout", "OffsetApplyResultRegisterBase"), (long)result);
            WriteRegister(C("ToolPathSharedMemoryLayout", "OffsetCompletedGenerationRegisterBase"), Core.ToolPaths[0].OffsetApplySnapshot.RequestGeneration);
        }
        public void Dispose()
        {
            typeof(Core).GetField("connectionState", Hidden)!.SetValue(Core, ConnectionState.Offline);
            Links.Dispose();
            ((IDisposable)transport).Dispose();
        }
    }
}

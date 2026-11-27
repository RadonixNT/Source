using System.IO.MemoryMappedFiles;
using System.Reflection;
using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.LinkManager;
using Core = NT.Controller.Core;

namespace RouterXYZTC.Simulation.Tests;

public sealed class OperatorSwitchTests
{
    [Fact]
    public void PhysicalJogRequiresFreshPressAndReleasesOnInhibitRemapAndDisconnect()
    {
        using var f = new Fixture();
        f.Sample(10); // NC Pause closed, Jog already held at connection.
        Assert.Empty(f.Calls);
        f.Sample(2); f.Sample(10); f.Sample(10);
        Assert.Equal(new[] { "Begin" }, f.Calls);
        f.Sample(8); f.Sample(10); // Pause opens, then closes with Jog still held.
        Assert.Equal(new[] { "Begin", "End", "Pause" }, f.Calls);
        f.Sample(2); f.Sample(10);
        f.JogMapping.Target = "DM0.IN4";
        f.Switches.RefreshJogMappings();
        f.Sample(18); f.Sample(2); f.Sample(18);
        f.Switches.Poll(false); f.Sample(18);
        Assert.Equal(new[] { "Begin", "End", "Pause", "Begin", "End", "Begin", "End" }, f.Calls);
    }

    [Fact]
    public void NcPauseIsActiveAtConnectionAndReleaseNeverReplaysSuppressedRun()
    {
        using var f = new Fixture();
        f.Sample(0); f.Sample(0);
        Assert.Equal(new[] { "Pause" }, f.Calls);
        Assert.True(OperatorSwitches.IsPauseRequested(f.PauseInput));
        f.Sample(4); // Run pressed while the NC circuit is open.
        f.Sample(6); // Closing Pause while Run is held must not start.
        Assert.Single(f.Calls);
        Assert.False(OperatorSwitches.IsPauseRequested(f.PauseInput));
        f.Sample(2); f.Sample(6);
        Assert.Equal(new[] { "Pause", "Run" }, f.Calls);
        f.Switches.Poll(false);
        f.Sample(0); f.Sample(0);
        Assert.Equal(new[] { "Pause", "Run", "Pause" }, f.Calls);
    }

    [Fact]
    public void UnconfiguredPauseDoesNotInhibitRun()
    {
        using var f = new Fixture();
        foreach (string target in new[] { "", "FALSE", "TRUE" })
        {
            f.PauseInput.Target = target;
            Assert.False(OperatorSwitches.IsPauseRequested(f.PauseInput));
            f.Sample(0); f.Sample(4);
        }
        Assert.Equal(new[] { "Run", "Run", "Run" }, f.Calls);
        f.PauseInput.Target = "DM0.IN1";
        f.PauseInput.Enabled = false;
        Assert.False(OperatorSwitches.IsPauseRequested(f.PauseInput));
    }

    [Theory]
    [InlineData("", "F9")]
    [InlineData("F10", "F10")]
    public void ResetShortcutMigrationPreservesAssignmentsAndRunsOnlyOnce(string existing, string expected)
    {
        string path = @"Software\Radonix\Tests\ResetMigration-" + Guid.NewGuid().ToString("N");
        try
        {
            using var parent = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(path);
            using var keyboard = parent.CreateSubKey(Initialize.InterfaceName + @"\Keyboard");
            keyboard.SetValue("Stop Run", "F9"); keyboard.SetValue("Reset", existing);
            TransferNameMigration.Apply(parent);
            Assert.Equal(expected, keyboard.GetValue("Reset"));
            Assert.Equal("F9", keyboard.GetValue("Stop Run"));
            keyboard.SetValue("Reset", ""); TransferNameMigration.Apply(parent);
            Assert.Equal("", keyboard.GetValue("Reset"));
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(path, false); }
    }

    [Fact]
    public void AllThreeMappedInputsExecuteOncePerPressWithStopPriority()
    {
        using var f = new Fixture();
        f.Sample(2);
        f.Sample(6); f.Sample(6);
        Assert.Equal(new[] { "Run" }, f.Calls);
        f.Sample(2); f.Sample(0); f.Sample(0);
        f.Sample(2); f.Sample(3); f.Sample(3);
        Assert.Equal(new[] { "Run", "Pause", "Reset" }, f.Calls);
        f.Sample(2); f.Sample(5); f.Sample(6);
        Assert.Equal(new[] { "Run", "Pause", "Reset", "Reset" }, f.Calls);
        f.Sample(2); f.Sample(4); f.Sample(6);
        Assert.Equal("Pause", f.Calls[^1]);
        Assert.Equal(5, f.Calls.Count);
    }

    [Fact]
    public void HeldRunAtStartupReconnectAndRemappingRequiresRelease()
    {
        using var f = new Fixture();
        f.Sample(6); Assert.Empty(f.Calls);
        f.Sample(2); f.Sample(6); Assert.Single(f.Calls);
        f.Switches.Poll(false); f.Sample(6); Assert.Single(f.Calls);
        f.Sample(2); f.Sample(6); Assert.Equal(2, f.Calls.Count);
        f.RunInput.Target = "DM0.IN3";
        f.Sample(10); Assert.Equal(2, f.Calls.Count);
        f.Sample(2); f.Sample(10); Assert.Equal(3, f.Calls.Count);
    }

    [Fact]
    public void DisabledUnassignedAndConstantInputsCannotRunCommands()
    {
        using var f = new Fixture();
        foreach (string target in new[] { "", "TRUE", "FALSE", "DM0.IN2" })
        {
            f.RunInput.Target = target;
            f.RunInput.Enabled = target != "DM0.IN2";
            f.Sample(2); f.Sample(6);
        }
        Assert.Empty(f.Calls);
        f.RunInput.Enabled = true;
        f.Sample(6); Assert.Empty(f.Calls);
        f.Sample(2); f.Sample(6); Assert.Single(f.Calls);
    }

    [Fact]
    public void RejectedRunIsNotQueuedAndCommandExecutionKeepsAllGates()
    {
        using var f = new Fixture();
        bool ready = false;
        f.Run.QueryEnabled += (_, e) => e.Enabled &= ready;
        f.Sample(2); f.Sample(6);
        ready = true; f.Sample(6); Assert.Empty(f.Calls);
        f.Sample(2); f.Sample(6); Assert.Single(f.Calls);
        f.Run.Enabled = false; Assert.False(f.Run.TryExecute());
        f.Run.Enabled = true;
        var previous = UI.InteractionAuthorization;
        try { UI.InteractionAuthorization = _ => false; Assert.False(f.Run.TryExecute()); }
        finally { UI.InteractionAuthorization = previous; }
        Assert.Single(f.Calls);
        Assert.True(f.Run.TryExecute());
        Assert.Equal(2, f.Calls.Count);
        f.Run.Dispose();
        Assert.Throws<ObjectDisposedException>(() => f.Run.TryExecute());
    }

    private sealed class Fixture : IDisposable
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly IDisposable transport;
        private readonly MemoryMappedViewAccessor view;
        private readonly long inputOffset;
        private readonly CommandLink reset, pause;
        internal readonly CommandLink Run;
        internal readonly PLCIOMapping RunInput;
        internal readonly PLCIOMapping PauseInput;
        internal readonly OperatorSwitches Switches;
        internal readonly PLCIOMapping JogMapping;
        private readonly CommandLink jog;
        internal readonly List<string> Calls = [];

        internal Fixture()
        {
            var type = typeof(Core).Assembly.GetType("NT.Controller.NTSharedMemory")!;
            transport = (IDisposable)Activator.CreateInstance(type, Flags, null,
                ["Local\\Radonix-SwitchTest-" + Guid.NewGuid().ToString("N")], null)!;
            var core = (Core)Activator.CreateInstance(typeof(Core), Flags, null, [transport], null)!;
            view = (MemoryMappedViewAccessor)type.GetProperty("Accessor", Flags)!.GetValue(transport)!;
            inputOffset = 8 * Convert.ToInt64(typeof(Core).Assembly.GetType("NT.Controller.SharedRegisterIndex")!
                .GetField("C2MDigitalInput")!.GetRawConstantValue());
            var module = core.PLC.Modules[0];
            module.Name = "DM0"; module.Type = MicroModuleTypes.Digital;
            module.PlcIndex = 0; module.Mode = DeviceMode.Virtual;
            PLCIOMapping Input(string name, int bit)
            {
                var input = core.PLC.IOMappings.Add(name, PLCIOMappingType.DigitalInput);
                input.Target = "DM0.IN" + bit;
                return input;
            }
            var resetInput = Input("StopSwitch", 0);
            var pauseInput = PauseInput = Input("PauseSwitch", 1);
            RunInput = Input("RunSwitch", 2);
            CommandLink Command(string name)
            {
                var command = CommandLink.CreateCommand(name, name);
                command.ActionExecute += (_, _) => Calls.Add(name);
                return command;
            }
            reset = Command("Reset"); pause = Command("Pause"); Run = Command("Run");
            Switches = new(resetInput, pauseInput, RunInput, reset, pause, Run);
            JogMapping = Input("JogXPlus", 3);
            jog = CommandLink.CreateCommand("Jog X+", "Jog X+");
            jog.ActionBegin += (_, _) => Calls.Add("Begin");
            jog.ActionEnd += (_, _) => Calls.Add("End");
            Switches.ConfigureJog([new(JogMapping, jog, true)]);
        }
        internal void Sample(long bits) { view.Write(inputOffset, bits); Switches.Poll(true); }
        public void Dispose() { Switches.Disconnect(); jog.Dispose(); reset.Dispose(); pause.Dispose(); Run.Dispose(); transport.Dispose(); }
    }
}

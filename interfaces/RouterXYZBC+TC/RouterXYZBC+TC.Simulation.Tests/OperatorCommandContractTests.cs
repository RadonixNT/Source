using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller;
using NT.LinkManager;

namespace RouterXYZBCTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class OperatorCommandContractTests
{
    [Fact]
    public void SavedBackgroundMigrationPreservesEngineerOutputsAndIsIdempotent()
    {
        string source = "// custom\nCONTROLLER.HOLD := NOT EmergencyClosed;\nCoolant := TRUE;\n";
        string migrated = ControllerLinks.MigrateBackgroundHold(source);
        Assert.DoesNotContain("CONTROLLER.HOLD :=", migrated);
        Assert.Contains("// custom", migrated);
        Assert.Contains("Coolant := TRUE;", migrated);
        Assert.Equal(migrated, ControllerLinks.MigrateBackgroundHold(migrated));
    }
    [Fact]
    public void OperatorProgramsAreEditableAndStopHooksFollowMotionStop()
    {
        OnForm(form =>
        {
            var core = form.controller.MotionController;
            var links = (Dictionary<string, TextLink>)typeof(ControllerLinks)
                .GetField("plcSourceLinks", Hidden)!.GetValue(form.controllerLinks)!;
            foreach (string name in new[] { "Run", "Pause", "Reset" })
            {
                Assert.False(core.PLC.Programs[name].Cyclic);
                Assert.NotEqual(AccessView.ReadOnly, links[name].View);
            }
            // Reverse cyclic registration order: Background, NC gate. Zones are managed subscribers.
            var names = core.PLC.Programs.Select(p => p.Name).ToList();
            Assert.DoesNotContain("ZoneControl", names);
            Assert.True(names.IndexOf("OperatorInterlocks") < names.IndexOf("Background"));
            Assert.Equal(AccessView.ReadOnly, links["OperatorInterlocks"].View);
            string custom = "SpindleCW := FALSE; SpindleCCW := FALSE; Coolant := FALSE; END_FUNCTION;";
            links["Pause"].Value = custom;
            Assert.True(form.controllerLinks.ValidatePlcSettings(), core.PLC.LastError);
            Assert.Equal(custom, core.PLC.Programs["Pause"].Source);
            object memory = typeof(Core).GetField("sharedMemory", Hidden)!.GetValue(core)!;
            var view = (MemoryMappedViewAccessor)memory.GetType().GetProperty("Accessor", Hidden)!.GetValue(memory)!;
            Assert.True((bool)core.PLC.GetType().GetMethod("DownloadCompiledPrograms", Hidden)!.Invoke(core.PLC, [memory])!);
            long C(string type, string member) => Convert.ToInt64(typeof(Core).Assembly
                .GetType("NT.Controller." + type)!.GetField(member)!.GetRawConstantValue());
            long head = C("NTSharedMemory", "PlcSyncM2CHeadOffset");
            long queue = C("NTSharedMemory", "PlcSyncM2COffset");
            var state = typeof(Core).GetField("connectionState", Hidden)!;
            try
            {
                state.SetValue(core, ConnectionState.Online);
                foreach (string name in new[] { "Pause", "Reset" })
                {
                    long before = view.ReadInt64(head);
                    if (name == "Pause") form.controllerLinks.PauseMotion();
                    else Assert.True(form.controllerLinks.ResetLink.TryExecute());
                    long after = view.ReadInt64(head);
                    var rows = Enumerable.Range(0, checked((int)(after - before)))
                        .Select(i => queue + ((before + i) & 1023) * 16).ToArray();
                    int stop = Array.FindIndex(rows, r => view.ReadUInt32(r) == C("PlcM2CCommandCodes", "StopMotion"));
                    int hook = Array.FindIndex(rows, r => view.ReadUInt32(r) == C("PlcM2CCommandCodes", "RunProgram")
                        && view.ReadUInt32(r + 4) == core.PLC.Programs[name].RuntimeIndex);
                    Assert.True(stop >= 0 && hook > stop, $"{name} hook must follow all-motion Stop.");
                    Assert.DoesNotContain(rows, r => view.ReadUInt32(r) == C("PlcM2CCommandCodes", "SetDigitalOutput"));
                    Assert.DoesNotContain(rows, r => view.ReadUInt32(r) == C("PlcM2CCommandCodes", "SetAnalogOutput"));
                }
            }
            finally { state.SetValue(core, ConnectionState.Offline); }
        });
    }

    [Fact]
    public void LiveNcInputBlocksMainEntryAndPositioningBeforeTimerPoll()
    {
        OnForm(form =>
        {
            var core = form.controller.MotionController;
            var pause = core.PLC.IOMappings["PauseSwitch"];
            pause.Target = "DM0.IN4";
            object memory = typeof(Core).GetField("sharedMemory", Hidden)!.GetValue(core)!;
            var view = (MemoryMappedViewAccessor)memory.GetType().GetProperty("Accessor", Hidden)!.GetValue(memory)!;
            long offset = 8 * Convert.ToInt64(typeof(Core).Assembly.GetType("NT.Controller.SharedRegisterIndex")!
                .GetField("C2MDigitalInput")!.GetRawConstantValue());
            long previous = view.ReadInt64(offset);
            try
            {
                view.Write(offset, previous & ~(1L << 4));
                Assert.True(form.controllerLinks.IsPauseSwitchOpen);
                Assert.False(form.controllerLinks.RunLink.TryExecute());
                Assert.False(form.RunMainProgram());
                Assert.False(form.controllerLinks.RunOrPositionMainToolPath());
                Assert.False(form.controllerLinks.CanStartHomedMotion());
                view.Write(offset, previous | (1L << 4));
                Assert.False(form.controllerLinks.IsPauseSwitchOpen);
                Assert.False(core.ToolPaths[0].IsRunning);
            }
            finally { view.Write(offset, previous); }
        });
    }

    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void OnForm(Action<MainForm> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string path = @"Software\Radonix\Tests\OperatorCommands-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZBCTCTexts.Register();
                using var registry = Registry.CurrentUser.CreateSubKey(path);
                using var form = new MainForm();
                form.InitializeRuntime(registry, connectOnStartup: false);
                action(form);
            }
            catch (Exception e) { failure = e; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

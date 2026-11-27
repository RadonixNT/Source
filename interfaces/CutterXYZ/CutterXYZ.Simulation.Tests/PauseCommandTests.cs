using System.IO.MemoryMappedFiles;
using System.Reflection;
using Keys = System.Windows.Forms.Keys;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller;
using NT.LinkManager;
using CutterXYZ;

namespace CutterXYZ.Simulation.Tests;

[Collection("Router UI")]
public sealed class PauseCommandTests
{
    [Theory]
    [InlineData("Jogging")]
    [InlineData("Homing")]
    [InlineData("AuxiliaryPath")]
    [InlineData("MainProgram")]
    [InlineData("Jogging", "R_JogPause")]
    [InlineData("MainProgram", "R_JogPause")]
    [InlineData("Homing", "R_ReferencePause")]
    [InlineData("MainProgram", "R_ReferencePause")]
    [InlineData("Jogging", "Keyboard")]
    public void PauseButtonQueuesAllMotionStopWithoutSchedulingMainProgramReset(string motion, string controlName = "R_PauseProgram")
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterPause-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using var form = new MainForm();
                using (var axes = settings.CreateSubKey(Initialize.InterfaceName + "\\Controller\\Axes")) axes.SetValue("YG", "True");
                form.InitializeRuntime(settings, connectOnStartup: false);
                Core core = form.controller.MotionController;
                const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
                var state = typeof(Core).GetField("connectionState", fields)!;
                object memory = typeof(Core).GetField("sharedMemory", fields)!.GetValue(core)!;
                var view = (MemoryMappedViewAccessor)memory.GetType().GetProperty("Accessor", fields)!.GetValue(memory)!;
                long Constant(string type, string name) => Convert.ToInt64(typeof(Core).Assembly
                    .GetType("NT.Controller." + type)!.GetField(name)!.GetRawConstantValue());
                long headOffset = Constant("NTSharedMemory", "PlcSyncM2CHeadOffset");
                long queueOffset = Constant("NTSharedMemory", "PlcSyncM2COffset");
                bool keyboard = controlName == "Keyboard";
                var button = (NT.Tools.sButton)typeof(MainForm).GetField(keyboard ? "R_PauseProgram" : controlName, fields)!.GetValue(form)!;
                UI.Update();
                Assert.False(button.Enabled);
                long statusRegister = motion is "AuxiliaryPath" or "MainProgram"
                    ? Constant("ToolPathSharedMemoryLayout", "ToolPathRegisterBase")
                        + (motion == "AuxiliaryPath" ? 2 : 0) * Constant("ToolPathSharedMemoryLayout", "ToolPathRegisterStride")
                        + Constant("ToolPathSharedMemoryLayout", "StatusSlot")
                    : Constant("AxisSharedMemoryLayout", "AxisRegisterBase")
                        + Constant("AxisSharedMemoryLayout", "StatusSlot");
                long previousStatus = view.ReadInt64(statusRegister * 8);
                try
                {
                    state.SetValue(core, ConnectionState.Online);
                    if (motion is "AuxiliaryPath" or "MainProgram")
                    {
                        view.Write(statusRegister * 8, Constant("ToolPathStatusBits", "Running"));
                    }
                    else
                    {
                        view.Write(statusRegister * 8, Constant("AxisStatusBits", motion));
                    }
                    Assert.Equal(motion == "MainProgram", core.ToolPaths[0].IsRunning);
                    long cursorGeneration = core.ToolPaths[0].CoordinateResetGeneration;
                    UI.Update();
                    Assert.True(button.Enabled);
                    if (keyboard) VerifyStepShortcuts(form, core, view, Constant);
                    long before = view.ReadInt64(headOffset);
                    if (keyboard) PressShortcut(form, "Pause", Keys.F12);
                    else typeof(System.Windows.Forms.Control).GetMethod("OnClick", fields)!
                            .Invoke(button, [EventArgs.Empty]);
                    long after = view.ReadInt64(headOffset);
                    bool allMotionStop = false;
                    for (long row = before; row < after; row++)
                    {
                        long address = queueOffset + (row & 1023) * 16;
                        if (view.ReadUInt32(address) == 20 && // Current StopMotion queue opcode.
                            view.ReadUInt32(address + 4) == (uint)StopFlags.AllMotion)
                            allMotionStop = true;
                    }
                    Assert.True(allMotionStop, "Pause did not queue the native all-motion stop.");
                    var links = (ControllerLinks)typeof(MainForm).GetField("controllerLinks", fields)!.GetValue(form)!;
                    Assert.False((bool)typeof(ControllerLinks).GetField("resetMainPathAfterStop", fields)!.GetValue(links)!);
                    // Emulate the later stopped feedback tick that previously reset TP0.
                    view.Write(statusRegister * 8, 0L);
                    Assert.False(links.UpdateStopReset());
                    Assert.Equal(cursorGeneration, core.ToolPaths[0].CoordinateResetGeneration);
                }
                finally
                {
                    view.Write(statusRegister * 8, previousStatus);
                    state.SetValue(core, ConnectionState.Offline);
                }
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Offline Pause verification did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void PressShortcut(MainForm form, string name, Keys key)
    {
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        var keyboard = form.keyboardLinks;
        var shortcut = Assert.IsType<TextLink>(keyboard.KeyboardEnabled.Group!.Members[name]);
        shortcut.Value = key.ToString();
        Assert.True(keyboard.ValidateSettings());
        var manager = typeof(KeyboardLinks).GetField("shortcutManager", fields)!.GetValue(keyboard)!;
        var bindings = (System.Collections.IEnumerable)manager.GetType().GetField("bindings", fields)!.GetValue(manager)!;
        var binding = Assert.Single(bindings.Cast<object>().Where(item =>
            (bool)item.GetType().GetMethod("Matches", fields)!.Invoke(item, [key])!));
        binding.GetType().GetMethod("Press", fields)!.Invoke(binding, [key]);
    }

    private static void VerifyStepShortcuts(MainForm form, Core core, MemoryMappedViewAccessor view,
        Func<string, string, long> constant)
    {
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(MainForm).GetField("commandAvailability", fields)!.SetValue(form,
            new MachineCommandAvailability(true, false, false, false, false, false));
        int nextKey = (int)Keys.F1;
        foreach (string name in Initialize.MainAxes)
        {
            int index = form.controller.GetAxisByName(name).Index;
            long Address(string slot) => 8 * (constant("AxisSharedMemoryLayout", "AxisRegisterBase")
                + index * constant("AxisSharedMemoryLayout", "AxisRegisterStride") + constant("AxisSharedMemoryLayout", slot));
            long status = Address("StatusSlot"), command = Address("CommandSlot");
            long previousStatus = view.ReadInt64(status), previousCommand = view.ReadInt64(command);
            try
            {
                using var gantry = name == "Y" ? new GantryFeedback(form, view, constant) : null;
                view.Write(status, constant("AxisStatusBits", "ReadyToMove"));
                form.generalLinks.GetJogStepLink(index).Value = 2;
                foreach (int direction in new[] { -1, 1 })
                {
                    view.Write(command, 0L);
                    PressShortcut(form, $"Step Jog {name}{(direction < 0 ? "-" : "+")}", (Keys)nextKey++);
                    Assert.True((view.ReadInt64(command) & constant("AxisCommandBits", "Jog")) != 0,
                        $"Step Jog {name} direction {direction} did not publish a Jog command.");
                    Assert.Equal(core.Axes[index].CommandPosition + direction * 2, view.ReadDouble(Address("JogPositionSlot")));
                }
            }
            finally
            {
                view.Write(command, previousCommand);
                view.Write(status, previousStatus);
            }
        }
    }

    private sealed class GantryFeedback : IDisposable
    {
        private readonly MemoryMappedViewAccessor view;
        private readonly Dictionary<long, long> original = [];

        internal GantryFeedback(MainForm form, MemoryMappedViewAccessor view, Func<string, string, long> constant)
        {
            this.view = view;
            int master = form.controller.GetAxisByName("Y").Index;
            int slave = form.controller.GetAxisByName("YG").Index;
            long mask = (long)(form.controller.MotionController.Axes[master].Mask | form.controller.MotionController.Axes[slave].Mask);
            void Write(int index, string slot, long value)
            {
                long address = 8 * (constant("AxisSharedMemoryLayout", "AxisRegisterBase")
                    + index * constant("AxisSharedMemoryLayout", "AxisRegisterStride") + constant("AxisSharedMemoryLayout", slot));
                original.Add(address, view.ReadInt64(address));
                view.Write(address, value);
            }
            Write(master, "CoupledAxesSlot", mask);
            Write(master, "CouplingRoleSlot", (long)AxisCouplingRole.Master);
            Write(slave, "CoupledAxesSlot", mask);
            Write(slave, "CouplingRoleSlot", (long)AxisCouplingRole.Slave);
            Write(slave, "CouplingMasterIndexSlot", master);
            Write(slave, "StatusSlot", constant("AxisStatusBits", "ReadyToMove"));
        }

        public void Dispose()
        {
            foreach (var (address, value) in original) view.Write(address, value);
        }
    }
}

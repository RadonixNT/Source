using System.IO.MemoryMappedFiles;
using System.Reflection;
using Keys = System.Windows.Forms.Keys;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller;
using NT.LinkManager;
using RouterXYZACRTCP;

namespace RouterXYZACRTCP.Simulation.Tests;

[Collection("Router UI")]
public sealed class PauseCommandTests
{
    [Theory]
    [InlineData("")]
    [InlineData("INV0.SPEED")]
    [InlineData("AM0.OUT0")]
    [InlineData("AM0.OUT1")]
    public void SpindleButtonDisablesForLiveProgramActivityBeforeAvailabilitySnapshotRefresh(string speedTarget)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterSpindle-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZACRTCPTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using var form = new MainForm();
                using (var axes = settings.CreateSubKey(Initialize.InterfaceName + "\\Controller\\Axes")) axes.SetValue("YG", "True");
                form.InitializeRuntime(settings, connectOnStartup: false);
                const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
                Core core = form.controller.MotionController;
                var state = typeof(Core).GetField("connectionState", fields)!;
                object memory = typeof(Core).GetField("sharedMemory", fields)!.GetValue(core)!;
                var view = (MemoryMappedViewAccessor)memory.GetType().GetProperty("Accessor", fields)!.GetValue(memory)!;
                long Constant(string type, string name) => Convert.ToInt64(typeof(Core).Assembly
                    .GetType("NT.Controller." + type)!.GetField(name)!.GetRawConstantValue());
                long address = 8 * (Constant("ToolPathSharedMemoryLayout", "ToolPathRegisterBase")
                    + Constant("ToolPathSharedMemoryLayout", "StatusSlot"));
                long previousStatus = view.ReadInt64(address);
                using var button = (NT.Tools.sToggle)typeof(MainForm).GetField("R_Spindle", fields)!.GetValue(form)!;
                using var slider = (NT.Tools.sSlider)typeof(MainForm).GetField("R_SpindleSpeed", fields)!.GetValue(form)!;
                // Bindings intentionally skip hidden controls. Detach from the hidden test form.
                button.Parent = null;
                slider.Parent = null;
                var links = (ControllerLinks)typeof(MainForm).GetField("controllerLinks", fields)!.GetValue(form)!;
                var clockwise = core.PLC.IOMappings["SpindleCW"];
                var speed = core.PLC.IOMappings["SpindleSpeed"];
                Assert.Equal("DM0.OUT0", clockwise.Target);
                Assert.Equal("AM0.OUT0", speed.Target);
                bool useInverter = speedTarget == "INV0.SPEED";
                bool useAnalog = speedTarget.StartsWith("AM", StringComparison.Ordinal);
                core.Inverters[0].Configuration.Enabled = useInverter;
                clockwise.Target = useInverter ? "INV0.RUN" : useAnalog ? "DM0.OUT1" : string.Empty;
                speed.Target = speedTarget;
                core.PLC.IOMappings["SpindleCCW"].Target = "DM0.OUT0";
                var registers = typeof(Core).Assembly.GetType("NT.Controller.SharedRegisterIndex")!;
                long runAddress = 8L * (int)registers.GetMethod("InverterRun")!.Invoke(null, [0])!;
                long speedAddress = 8L * (int)registers.GetMethod("InverterOutput")!.Invoke(null, [0])!;
                long digitalAddress = 8 * Constant("SharedRegisterIndex", "M2CDigitalOutput");
                long analogAddress = 8 * Constant("SharedRegisterIndex", "M2CAnalogOutput");
                long previousAnalog = view.ReadInt64(analogAddress);
                int analogChannel = speedTarget == "AM0.OUT1" ? 1 : 0;
                long previousRun = view.ReadInt64(runAddress), previousSpeed = view.ReadInt64(speedAddress), previousDigital = view.ReadInt64(digitalAddress);
                try
                {
                    state.SetValue(core, ConnectionState.Online);
                    typeof(MainForm).GetField("commandAvailability", fields)!.SetValue(form,
                        new MachineCommandAvailability(true, false, false, false, false, false));
                    view.Write(address, 0L);
                    links.RefreshSpindleSpeed();
                    UI.Update();
                    Assert.Equal(speedTarget.Length != 0, button.Enabled);
                    if (useAnalog)
                    {
                        Assert.True(slider.Enabled);
                        var rpm = (RealLink)typeof(MainForm).GetField("spindleSpeedLink", fields)!.GetValue(form)!;
                        long head = Constant("NTSharedMemory", "PlcSyncM2CHeadOffset");
                        long before = view.ReadInt64(head);
                        rpm.Value = form.routerLinks.MaximumSpindleSpeed.Value * 0.5;
                        Assert.Same(form.routerLinks.CurrentSpindleSpeed, rpm);
                        Assert.Equal(12000, rpm.Value);
                        Assert.Equal(12000, slider.Value);
                        Assert.Equal(before + 1, view.ReadInt64(head));
                        long row = Constant("NTSharedMemory", "PlcSyncM2COffset") + (before & 1023) * 16;
                        Assert.Equal(Constant("PlcM2CCommandCodes", "SetAnalogOutput"), (long)view.ReadUInt32(row));
                        Assert.Equal((uint)analogChannel, view.ReadUInt32(row + 4));
                        Assert.Equal(5000L, view.ReadInt64(row + 8));
                    }
                    foreach (string status in new[] { "Running", "Stopping", "PausedForProgram" })
                    {
                        view.Write(address, Constant("ToolPathStatusBits", status));
                        Assert.True(core.ToolPaths[0].IsProgramActive);
                        UI.Update();
                        Assert.False(button.Enabled);
                        Assert.False(button.DimIndicatorWhenDisabled);
                        // Emulate consumed PLC M3/M4/M5 and S outputs, not a UI write.
                        foreach (var output in new[] { (CW: true, CCW: false, Speed: 0.751), (CW: false, CCW: true, Speed: 0.5), (CW: false, CCW: false, Speed: 0.0) })
                        {
                            // An unassigned spindle must ignore stale INV0 registers.
                            view.Write(runAddress, output.CW ? 1L : 0L);
                            view.Write(speedAddress, BitConverter.DoubleToInt64Bits(useAnalog ? 1 - output.Speed : output.Speed));
                            if (useAnalog)
                                view.Write(analogAddress, (long)Math.Round(output.Speed * 10000) << (16 * analogChannel));
                            view.Write(digitalAddress, (output.CCW ? 1L : 0L) | (useAnalog && output.CW ? 2L : 0L));
                            long commandHead = Constant("NTSharedMemory", "PlcSyncM2CHeadOffset");
                            long commandsBeforeSample = view.ReadInt64(commandHead);
                            links.RefreshLivePlcValues();
                            UI.Update();
                            Assert.Equal(commandsBeforeSample, view.ReadInt64(commandHead));
                            Assert.Equal((speedTarget.Length != 0 && output.CW) || output.CCW, button.Checked);
                            double minimum = form.routerLinks.MinimumSpindleSpeed.Value;
                            double maximum = form.routerLinks.MaximumSpindleSpeed.Value;
                            double expectedRpm = speedTarget.Length != 0 ? Math.Clamp(output.Speed * maximum, minimum, maximum) : minimum;
                            Assert.Equal(expectedRpm, slider.Value, 3);
                            Assert.Equal(expectedRpm, form.routerLinks.CurrentSpindleSpeed.Value, 3);
                            Assert.False(button.Enabled);
                        }
                    }
                    long commandHeadBeforeRangeEdit = view.ReadInt64(Constant("NTSharedMemory", "PlcSyncM2CHeadOffset"));
                    form.routerLinks.MinimumSpindleSpeed.Value = 1500;
                    Assert.Equal(1500, slider.Value);
                    Assert.Equal(1500, form.routerLinks.CurrentSpindleSpeed.Value);
                    Assert.Equal(commandHeadBeforeRangeEdit, view.ReadInt64(Constant("NTSharedMemory", "PlcSyncM2CHeadOffset")));
                    view.Write(address, 0L);
                    UI.Update();
                    Assert.Equal(speedTarget.Length != 0, button.Enabled);
                }
                finally
                {
                    view.Write(address, previousStatus);
                    view.Write(runAddress, previousRun);
                    view.Write(speedAddress, previousSpeed);
                    view.Write(digitalAddress, previousDigital);
                    view.Write(analogAddress, previousAnalog);
                    state.SetValue(core, ConnectionState.Offline);
                }
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Offline spindle presentation check did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Theory]
    [InlineData("Jogging")]
    [InlineData("Homing")]
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
                RouterXYZACRTCPTexts.Register();
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
                long statusRegister = motion == "MainProgram"
                    ? Constant("ToolPathSharedMemoryLayout", "ToolPathRegisterBase")
                        + Constant("ToolPathSharedMemoryLayout", "StatusSlot")
                    : Constant("AxisSharedMemoryLayout", "AxisRegisterBase")
                        + Constant("AxisSharedMemoryLayout", "StatusSlot");
                long previousStatus = view.ReadInt64(statusRegister * 8);
                try
                {
                    state.SetValue(core, ConnectionState.Online);
                    if (motion == "MainProgram")
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

using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller;
using NT.LinkManager;
using NT.Tools;
using CutterXYZ;

namespace CutterXYZ.Simulation.Tests;

[Collection("Router UI")]
public sealed class PositionJogCommandTests
{
    [Theory]
    [InlineData("X", 25.0)]
    [InlineData("Z", -25.0)]
    public void PositionJogRequiresWholeMachineHomeAndUsesPersistedDestination(string axisName, double destination)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterPositionJog-" + Guid.NewGuid().ToString("N");
            const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using (var form = new MainForm())
                {
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    Core core = form.controller.MotionController;
                    var state = typeof(Core).GetField("connectionState", fields)!;
                    object memory = typeof(Core).GetField("sharedMemory", fields)!.GetValue(core)!;
                    var view = (MemoryMappedViewAccessor)memory.GetType().GetProperty("Accessor", fields)!.GetValue(memory)!;
                    long Constant(string type, string name) => Convert.ToInt64(typeof(Core).Assembly
                        .GetType("NT.Controller." + type)!.GetField(name)!.GetRawConstantValue());
                    long Address(int index, string slot) => 8 * (Constant("AxisSharedMemoryLayout", "AxisRegisterBase")
                        + index * Constant("AxisSharedMemoryLayout", "AxisRegisterStride") + Constant("AxisSharedMemoryLayout", slot));
                    long ready = Constant("AxisStatusBits", "ReadyToMove");
                    long homed = Constant("AxisStatusBits", "Homed");
                    int index = form.controller.GetAxisByName(axisName).Index;
                    var links = (ControllerLinks)typeof(MainForm).GetField("controllerLinks", fields)!.GetValue(form)!;
                    var general = (GeneralLinks)typeof(MainForm).GetField("generalLinks", fields)!.GetValue(form)!;
                    var button = (sButton)typeof(MainForm).GetField("R_GoTo" + axisName, fields)!.GetValue(form)!;
                    var editor = (sTextBox)typeof(MainForm).GetField("R_Position" + axisName, fields)!.GetValue(form)!;
                    var onClick = typeof(System.Windows.Forms.Control).GetMethod("OnClick", fields)!;
                    general.GetJogPositionLink(index).Value = destination;
                    general.GetJogVelocityLink(index).Value = 40;
                    general.JogFeedPercentLink.Value = 50;
                    general.JogModeLink.Value = true;
                    UI.Update();
                    Assert.NotNull(editor);
                    Assert.False(button.Enabled);
                    try
                    {
                        state.SetValue(core, ConnectionState.Online);
                        typeof(MainForm).GetField("ethercatOperational", fields)!.SetValue(form, true);
                        typeof(MainForm).GetField("commandAvailability", fields)!.SetValue(form,
                            new MachineCommandAvailability(true, false, false, false, false, false));
                        foreach (var axis in core.Axes) view.Write(Address(axis.Index, "StatusSlot"), ready | homed);
                        // A different motor without Home must also inhibit absolute Jog.
                        int other = core.Axes.First(a => a.Index != index).Index;
                        view.Write(Address(other, "StatusSlot"), ready);
                        UI.Update();
                        Assert.False(button.Enabled);
                        onClick.Invoke(button, [EventArgs.Empty]);
                        Assert.Equal(0L, view.ReadInt64(Address(index, "CommandSlot")) & Constant("AxisCommandBits", "Jog"));
                        view.Write(Address(other, "StatusSlot"), ready | homed);
                        UI.Update();
                        Assert.True(button.Enabled);
                        // Recheck Home even if button state has not refreshed yet.
                        view.Write(Address(other, "StatusSlot"), ready);
                        onClick.Invoke(button, [EventArgs.Empty]);
                        Assert.Equal(0L, view.ReadInt64(Address(index, "CommandSlot")) & Constant("AxisCommandBits", "Jog"));
                        view.Write(Address(other, "StatusSlot"), ready | homed);
                        onClick.Invoke(button, [EventArgs.Empty]);
                        Assert.Equal(destination, view.ReadDouble(Address(index, "JogPositionSlot")));
                        Assert.Equal(20, view.ReadDouble(Address(index, "JogVelocitySlot")));
                        long jogCommand = view.ReadInt64(Address(index, "CommandSlot"));
                        Assert.NotEqual(0L, jogCommand & Constant("AxisCommandBits", "Jog"));
                        typeof(System.Windows.Forms.Control).GetMethod("OnMouseUp", fields)!.Invoke(button,
                            [new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left, 1, 5, 5, 0)]);
                        Assert.Equal(jogCommand, view.ReadInt64(Address(index, "CommandSlot")));
                    }
                    finally
                    {
                        state.SetValue(core, ConnectionState.Offline);
                        foreach (var axis in core.Axes)
                        {
                            view.Write(Address(axis.Index, "StatusSlot"), 0L);
                            view.Write(Address(axis.Index, "CommandSlot"), 0L);
                        }
                    }
                    // Application exit uses this same LinkGroup persistence entry point.
                    form.baseGroup.SaveRegistry();
                }
                using var reopened = new MainForm();
                reopened.InitializeRuntime(settings, connectOnStartup: false);
                var restored = (GeneralLinks)typeof(MainForm).GetField("generalLinks", fields)!.GetValue(reopened)!;
                Assert.Equal(destination, restored.GetJogPosition(reopened.controller.GetAxisByName(axisName).Index));
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Offline position Jog verification did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

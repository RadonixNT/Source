using NT_Interpreter.Shared.Router;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.LinkManager;
using NT_Interpreter.Interpreters.RouterXYZ;
using CutterXYZ;

namespace CutterXYZ.Simulation.Tests;

[Collection("Router UI")]
public sealed class RuntimePresentationTests
{
    [Fact]
    public void RootRefreshRateAppliesLiveAndRestoresTheSavedInterval()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterRefresh-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                static System.Windows.Forms.Timer Timer(MainForm form) => (System.Windows.Forms.Timer)
                    typeof(MainForm).GetField("refreshTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                using (var form = new MainForm())
                {
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    var general = form.baseGroup.Groups["General"];
                    Assert.Equal(new PositioningSettings(0, 10, 5), form.generalLinks.Positioning);
                    foreach (var (key, title, unit) in new[]
                    {
                        ("SafeZ", "Safe Z", "millimeters"),
                        ("ApproachDistance", "Approach Distance", "millimeters"),
                        ("ApproachVelocity", "Approach Velocity", "millimeters per second")
                    })
                    {
                        var link = Assert.IsType<RealLink>(general.Members[key]);
                        Assert.Equal(title, link.DisplayName);
                        Assert.Contains(unit, link.Description);
                    }
                    Assert.Equal(Initialize.MainAxes, general.Groups.Keys.ToArray());
                    Assert.Equal(Initialize.MainAxes, form.controllerLinks.AxisJogCommands.Select(command => command.AxisName).ToArray());
                    foreach (string axis in Initialize.MainAxes)
                    {
                        foreach (string parameter in new[] { "JogVelocity", "JogStep", "JogPosition", "ParkPosition" })
                        {
                            var link = Assert.IsType<RealLink>(general.Groups[axis].Members[parameter]);
                            link.Value = axis == "Z" ? 12 : 25;
                            Assert.Equal(link.Value, ((NT.Controller.PLC.REAL)form.controller.PLC.ApplicationVariables[$"General.{axis}.{parameter}"]).Value);
                        }
                    }
                    Assert.Equal(12, form.generalLinks.GetJogPosition(form.controller.GetAxisByName("Z").Index));
                    var commandPrograms = form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Command Programs"];
                    foreach (string program in new[] { "GoHome", "GoToPark", "GoToReference" })
                    {
                        Assert.False(commandPrograms.Members.ContainsKey(program));
                        Assert.False(form.controller.PLC.Programs.TryGetValue(program, out _));
                    }
                    Assert.Equal("GoToPark", form.controllerLinks.GoToParkLink.Name);
                    using (var positioning = new HeadPositioningCommands(form.controller, () => 100, () => form.generalLinks.Positioning, () => true,
                        form.generalLinks.GetParkPosition))
                    {
                        var resolve = typeof(HeadPositioningCommands).GetMethod("ResolveDestination", BindingFlags.Instance | BindingFlags.NonPublic)!;
                        var captured = (double[])resolve.Invoke(positioning, [HeadPositioningTarget.GoToPark])!;
                        Assert.Equal(new double[] { 25, 25, 12 }, captured);
                        ((RealLink)general.Groups["X"].Members["ParkPosition"]).Value = 30;
                        Assert.Equal(25, captured[0]);
                        var axes = form.controller.ToolPaths[1].Axes;
                        int zSlot = Array.FindIndex(axes, axis => axis.Name == "Z");
                        int xSlot = Array.FindIndex(axes, axis => axis.Name == "X");
                        double top = axes[zSlot].MaxCourse;
                        double bottom = axes[zSlot].MinCourse;
                        var current = new double[axes.Length];
                        current[zSlot] = top - 20;
                        var destination = current.ToArray();
                        destination[xSlot] = 25;
                        destination[zSlot] = top - 60;
                        var customClearance = new PositioningSettings(top - 30, 10, 5);
                        var parkRoute = positioning.BuildNormalRoute(HeadPositioningTarget.GoToPark, current, destination, 100, customClearance);
                        Assert.Equal(2, parkRoute.Count);
                        Assert.Equal(current[zSlot], parkRoute[0].Point[zSlot]);
                        Assert.Equal(100, parkRoute[^1].Velocity);
                        var returnRoute = positioning.BuildNormalRoute(HeadPositioningTarget.ReturnToWork, current, destination, 100, customClearance);
                        Assert.Equal(3, returnRoute.Count);
                        Assert.Equal(destination[zSlot] + 10, returnRoute[^2].Point[zSlot]);
                        Assert.Equal(destination[zSlot], returnRoute[^1].Point[zSlot]);
                        Assert.Equal(5, returnRoute[^1].Velocity);
                        Assert.Equal(top, (customClearance with { SafeZ = 0 }).GetTraverseHeight(current[zSlot], bottom, top));
                        Assert.Equal(top, (customClearance with { SafeZ = bottom - 1 }).GetTraverseHeight(current[zSlot], bottom, top));
                        current[zSlot] = top - 40;
                        var homeRoute = positioning.BuildNormalRoute(HeadPositioningTarget.GoHome, current, destination, 100, customClearance);
                        Assert.Equal(top - 30, homeRoute[0].Point[zSlot]);
                        Assert.Equal(top - 30, homeRoute[1].Point[zSlot]);
                        var validate = typeof(HeadPositioningCommands).GetMethod("ValidatePoint", BindingFlags.Instance | BindingFlags.NonPublic)!;
                        var x = form.controller.GetAxisByName("X");
                        foreach (double invalidX in new[] { x.MinCourse - 1, x.MaxCourse + 1 })
                        {
                            var error = Assert.Throws<TargetInvocationException>(() =>
                                validate.Invoke(positioning, [new double[] { invalidX, 0, 0 }, "Destination"]));
                            string message = Assert.IsType<InvalidOperationException>(error.InnerException).Message;
                            Assert.Contains("axis X", message);
                            Assert.Contains(FormattableString.Invariant($"{invalidX:G17} mm"), message);
                            Assert.Contains(FormattableString.Invariant($"[{x.MinCourse:G17}, {x.MaxCourse:G17}] mm"), message);
                            var entries = new List<string>();
                            void Capture(object? sender, NT.Diagnostics.NTLogEntry entry) => entries.Add(entry.Message);
                            NT.Diagnostics.NTLog.EntryWritten += Capture;
                            try
                            {
                                foreach (string operation in new[] { "GoToPark", "GoToReference", "ReturnToWork", "GoHome" })
                                {
                                    string rejection = $"{operation}: {message}";
                                    Assert.False((bool)typeof(HeadPositioningCommands).GetMethod("Reject", BindingFlags.Static | BindingFlags.NonPublic)!
                                        .Invoke(null, [rejection])!);
                                    Assert.Contains(rejection, entries);
                                }
                            }
                            finally { NT.Diagnostics.NTLog.EntryWritten -= Capture; }
                        }
                    }
                    var rate = Assert.IsType<TextLink>(form.baseGroup.Members["RefreshRate"]);
                    Assert.Same(form.generalLinks.RefreshRateLink, rate);
                    Assert.Equal(new[] { "High", "Low" }, rate.Items);
                    Assert.True(rate.ItemsOnly);
                    Assert.Equal("High", rate.Value);
                    var language = NT.Localization.NTLocalization.CurrentLanguage;
                    try
                    {
                        foreach (var supported in NT.Localization.NTLanguages.All)
                        {
                            NT.Localization.NTLocalization.SetLanguage(supported.Language);
                            Assert.Equal(NT.Localization.NTLocalization.Get(RouterXYZTCTexts.RefreshRate), rate.DisplayName);
                            Assert.Contains("40 ms", rate.Description);
                            Assert.Contains("100 ms", rate.Description);
                            Assert.Equal(new[] { "High", "Low" }, rate.Items);
                            Assert.Equal("High", rate.Value);
                        }
                    }
                    finally { NT.Localization.NTLocalization.SetLanguage(language); }
                    var timer = Timer(form);
                    Assert.True(timer.Enabled);
                    Assert.Equal(40, timer.Interval);
                    rate.Value = "Low";
                    Assert.Equal(100, timer.Interval);
                    rate.Value = "High";
                    Assert.Equal(40, timer.Interval);
                    rate.Value = "Low";
                    Assert.Same(timer, Timer(form));
                    Assert.True(timer.Enabled);
                    form.baseGroup.SaveRegistry();
                }
                using var reopened = new MainForm();
                reopened.InitializeRuntime(settings, connectOnStartup: false);
                Assert.Equal(30, reopened.generalLinks.GetParkPosition("X"));
                Assert.Equal(12, reopened.generalLinks.GetParkPosition("Z"));
                Assert.Equal("Low", reopened.generalLinks.RefreshRateLink.Value);
                Assert.Equal(100, Timer(reopened).Interval);
                Assert.True(Timer(reopened).Enabled);
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Offline refresh-rate verification did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Theory]
    [InlineData(OperationalState.Error, OperationalState.Warning, OperationalState.Error)]
    [InlineData(OperationalState.Warning, OperationalState.Active, OperationalState.Warning)]
    [InlineData(OperationalState.Active, OperationalState.Inactive, OperationalState.Active)]
    [InlineData(OperationalState.Inactive, OperationalState.Inactive, OperationalState.Inactive)]
    [InlineData(OperationalState.Inactive, OperationalState.Valid, OperationalState.Valid)]
    public void AggregateStatePreservesPriorityAndLatePendingChangesInOnePass(
        OperationalState firstState, OperationalState secondState, OperationalState expected)
    {
        using var group = new LinkGroup("Aggregate", "Aggregate", null);
        using var first = Link.CreateLogic("First", "First", false);
        using var second = Link.CreateLogic("Second", "Second", false);
        first.SetState(firstState);
        second.SetStateAndPendingChanges(secondState, pendingChanges: true);
        int enumerations = 0;
        IEnumerable<Access> Children()
        {
            Assert.Equal(1, ++enumerations);
            yield return first;
            yield return second;
        }

        var aggregate = typeof(ControllerLinks).GetMethod("SetAggregateState",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        aggregate.Invoke(null, [group, Children()]);
        Assert.Equal(expected, group.State);
        Assert.True(group.HasPendingChanges);
        aggregate.Invoke(null, [group, Array.Empty<Access>()]);
        Assert.Equal(OperationalState.Normal, group.State);
        Assert.False(group.HasPendingChanges);
    }

    [Theory]
    [InlineData(false, false, false, false, true)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    public void WindowCommandsRequireStoppedMotionAndNoActiveProgram(
        bool motion, bool program, bool home, bool positioning, bool allowed)
    {
        // Program-active with motion idle represents a paused/waiting program:
        // stopping its axes must not reopen Exit or Theme.
        var state = new MachineCommandAvailability(true, motion, program, false, home, positioning);
        Assert.Equal(allowed, state.CanUseWindowCommands);
    }

    [Fact]
    public void RevisedIoProfileCompilesAndEmergencyLampFollowsOpenNcCircuit()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterEmergency-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                form.ApplyControllerProperties();
                Assert.True(form.controller.PLC.Validate());
                var lamp = (NT.Tools.sSignal)typeof(MainForm).GetField("R_Emergency",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                var input = (TextLink)FindGroup(form.baseGroup, "EmergencySwitch")!.Members["Input"];
                input.Value = "TRUE";
                Assert.False(lamp.SignalOn);
                input.Value = "FALSE";
                Assert.True(lamp.SignalOn);
                input.Value = "TRUE";
                Assert.False(lamp.SignalOn);
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Offline emergency/profile test did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void MappingEnabledIsPersistedThroughExistingLinkGroupStorage()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterMapping-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                string selectedTarget;
                using (var settings = Registry.CurrentUser.CreateSubKey(registryPath))
                using (var form = new MainForm())
                {
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    foreach (var mapping in form.controller.PLC.IOMappings)
                        Assert.True(((LogicLink)FindGroup(form.baseGroup, mapping.Name)!.Members["Enabled"]).Value);
                    var signal = form.controller.PLC.IOMappings["EmergencySwitch"];
                    selectedTarget = signal.Target;
                    ((LogicLink)FindGroup(form.baseGroup, signal.Name)!.Members["Enabled"]).Value = false;
                    Assert.False(signal.Enabled);
                    Assert.True(signal.UsesIndependentStorage);
                    Assert.Equal(selectedTarget, signal.Target);
                    form.baseGroup.SaveRegistry();
                }
                using (var settings = Registry.CurrentUser.CreateSubKey(registryPath))
                using (var form = new MainForm())
                {
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    var signal = form.controller.PLC.IOMappings["EmergencySwitch"];
                    Assert.False(signal.Enabled);
                    Assert.Equal(selectedTarget, signal.Target);
                    ((LogicLink)FindGroup(form.baseGroup, signal.Name)!.Members["Enabled"]).Value = true;
                    Assert.True(signal.Enabled);
                    Assert.Equal(selectedTarget, signal.Target);
                }
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Offline mapping test did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static LinkGroup? FindGroup(LinkGroup root, string name)
    {
        if (root.Name == name) return root;
        foreach (LinkGroup child in root.Groups.Values)
            if (FindGroup(child, name) is { } result) return result;
        return null;
    }

    [Fact]
    public void WorkTransformTracksReferenceChangesWithBoundedIdleSampling()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterPresentation-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                var interpreter = (GCodeInterpreter)typeof(MainForm).GetField("gCodeInterpreter",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                form.referenceLinks.SetValue(0, 0, 12.5);
                form.referenceLinks.SetValue(0, 1, 3.25);
                Assert.Equal(new RouterPosition(12.5, 3.25, 0), interpreter.SynchronizeWorkCoordinateScene());
                for (int i = 0; i < 100; i++) interpreter.SynchronizeWorkCoordinateScene();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++) interpreter.SynchronizeWorkCoordinateScene();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                // Controller getters retain owned row snapshots. Budget for
                // three XYZ rows per sample, without extra per-axis snapshots.
                Assert.InRange(allocated, 0, 150_000);
                form.referenceLinks.SetValue(0, 0, 7);
                Assert.Equal(new RouterPosition(7, 3.25, 0), interpreter.SynchronizeWorkCoordinateScene());
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Offline presentation did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

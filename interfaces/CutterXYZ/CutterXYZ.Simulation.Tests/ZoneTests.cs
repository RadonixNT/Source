using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT;
using NT.ConfigurationManager;
using NT.Controller;
using NT.LinkManager;

namespace CutterXYZ.Simulation.Tests;

[Collection("Router UI")]
public sealed class ZoneTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void DefaultsAndCountsUseManagedPolicyWithoutCyclicPlcOrApplicationVariables()
    {
        OnUi(form =>
        {
            Assert.Equal(8, form.zoneLinks.Count.Value);
            Assert.Equal(8, form.zoneLinks.Zones.Count);
            Assert.False(form.zoneLinks.Enabled.Value);
            Assert.False(form.zoneLinks.Manager!.IsSubscribed);
            Assert.DoesNotContain(form.controller.PLC.Programs, p => p.Name == "ZoneControl");
            Assert.DoesNotContain(form.controller.PLC.ApplicationVariables.Definitions, d => d.Name.StartsWith("Zone", StringComparison.Ordinal));
            form.zoneLinks.Count.Value = 64;
            Assert.Equal(64, form.zoneLinks.Zones.Count);
            Assert.Equal(AccessView.ReadWrite, Mapping(form, 64).View);
            Assert.True(form.controllerLinks.ValidatePlcSettings(), form.controller.PLC.LastError);
            form.zoneLinks.Count.Value = 0;
            Assert.Empty(form.zoneLinks.Zones);
            Assert.Equal(AccessView.Hidden, Mapping(form, 1).View);
            Assert.False(form.controller.PLC.IOMappings["Zone1"].Enabled);
            Assert.True(form.controllerLinks.ValidatePlcSettings(), form.controller.PLC.LastError);
        });
    }

    [Fact]
    public void ReducedCountRetainsSettingsAndMappingAcrossSaveReloadAndReactivation()
    {
        OnUi(form =>
        {
            var zone = form.zoneLinks.Zones[7];
            zone.Mode.Value = "Workholding";
            zone.Selected.Value = true;
            zone.MinX.Value = -25;
            ((TextLink)Mapping(form, 8).Members["Output"]).Value = "DM1.OUT3";
            form.zoneLinks.Count.Value = 2;
            Assert.False(form.controller.PLC.IOMappings["Zone8"].Enabled);
            form.baseGroup.SaveRegistry();
            using var root = Registry.CurrentUser.OpenSubKey(form.registryRoot.Name["HKEY_CURRENT_USER\\".Length..], true)!;
            using var reload = new MainForm();
            reload.InitializeRuntime(root, connectOnStartup: false);
            Assert.Equal(2, reload.zoneLinks.Count.Value);
            reload.zoneLinks.Count.Value = 8;
            var restored = reload.zoneLinks.Zones[7];
            Assert.Equal("Workholding", restored.Mode.Value);
            Assert.True(restored.Selected.Value);
            Assert.Equal(-25, restored.MinX.Value);
            Assert.Equal("DM1.OUT3", reload.controller.PLC.IOMappings["Zone8"].Target);
            Assert.True(reload.controller.PLC.IOMappings["Zone8"].Enabled);
            Assert.False(reload.zoneLinks.Manager!.Clamped);
            Assert.False(reload.zoneLinks.Manager.Pending);
        });
    }

    [Fact]
    public void CancelRestoresCountHiddenEditsSelectionAndMapping()
    {
        OnUi(form =>
        {
            var zone = form.zoneLinks.Zones[7];
            var target = (TextLink)Mapping(form, 8).Members["Output"];
            using var dialog = new ConfigurationForm(form.baseGroup);
            var journal = (Dictionary<Link, object?>)typeof(ConfigurationForm).GetField("originalValues", Hidden)!.GetValue(dialog)!;
            journal.Add(form.zoneLinks.Count, form.zoneLinks.Count.Value);
            journal.Add(zone.MinX, zone.MinX.Value);
            journal.Add(zone.Selected, zone.Selected.Value);
            journal.Add(target, target.Value);
            zone.MinX.Value = 12;
            zone.Selected.Value = true;
            target.Value = "DM1.OUT3";
            form.zoneLinks.Count.Value = 2;
            Assert.Equal(AccessView.Hidden, zone.Group.View);
            typeof(ConfigurationForm).GetMethod("RestoreChanges", Hidden)!.Invoke(dialog, null);
            Assert.Equal(8, form.zoneLinks.Count.Value);
            Assert.Equal(0, zone.MinX.Value);
            Assert.False(zone.Selected.Value);
            Assert.Equal("Unassigned", target.Value);
            Assert.Equal(AccessView.ReadWrite, zone.Group.View);
            Assert.True(form.controller.PLC.IOMappings["Zone8"].Enabled);
        });
    }

    [Fact]
    public void ValidationRejectsMissingMappingBoundsAndCompetingPlcWriter()
    {
        OnUi(form =>
        {
            Configure(form);
            Assert.True(form.zoneLinks.ValidateSettings());
            Assert.True(form.controllerLinks.ValidatePlcSettings(), form.controller.PLC.LastError);
            var zone = form.zoneLinks.Zones[1];
            zone.MaxX.Value = zone.MinX.Value;
            Assert.False(form.zoneLinks.ValidateSettings());
            zone.MaxX.Value = 100;
            var links = (Dictionary<string, TextLink>)typeof(ControllerLinks).GetField("plcSourceLinks", Hidden)!.GetValue(form.controllerLinks)!;
            var source = links["M100"];
            string original = source.Value;
            source.Value = original + "\nZone1 := TRUE;";
            Assert.False(form.zoneLinks.ValidateSettings());
            Assert.False(form.controllerLinks.ValidatePlcSettings());
            source.Value = original + "\nDM1 . OUT0 := FALSE;";
            Assert.False(form.zoneLinks.ValidateSettings());
            source.Value = original + "\n// Zone1 := TRUE;\n(* nested (* DM1.OUT0 := FALSE; *) comment *)";
            Assert.True(form.zoneLinks.ValidateSettings());
            source.Value = original;
            ((TextLink)Mapping(form, 1).Members["Output"]).Value = "Unassigned";
            Assert.False(form.zoneLinks.ValidateSettings());
            form.zoneLinks.Enabled.Value = false;
            Assert.True(form.zoneLinks.ValidateSettings());
        });
    }

    [Fact]
    public void ActualCycleEventSubscribesOnlyForEnabledNonzeroZonesAndDisconnectDropsRuntime()
    {
        OnUi(form =>
        {
            var core = form.controller.MotionController;
            void State(ConnectionState state)
            {
                typeof(Core).GetMethod("SetConnectionStateLocked", Hidden)!.Invoke(core, [state]);
                typeof(ControllerWrapper).GetMethod("MotionControllerConnectionStateChanged", Hidden)!.Invoke(form.controller,
                    [core, new ConnectionStateChangedEventArgs(state, string.Empty)]);
            }
            try
            {
                State(ConnectionState.Online);
                Assert.False(form.zoneLinks.Manager!.IsSubscribed);
                State(ConnectionState.Offline);
                Configure(form);
                Assert.True(form.controllerLinks.ValidatePlcSettings(), core.PLC.LastError);
                State(ConnectionState.Online);
                Assert.True(form.zoneLinks.Manager.IsSubscribed);
                Assert.Throws<InvalidOperationException>(() => form.zoneLinks.Count.Value = 2);
                Assert.Throws<InvalidOperationException>(() => form.zoneLinks.Enabled.Value = false);
                Assert.Throws<InvalidOperationException>(() => form.zoneLinks.Zones[0].MinX.Value = 15);
                typeof(Core).GetMethod("DrainIncomingQueues", Hidden)!.Invoke(core, null);
                Assert.True(SpinWait.SpinUntil(() => form.zoneLinks.Manager.Ready, TimeSpan.FromSeconds(5)));
                State(ConnectionState.Disconnecting);
                Assert.False(form.zoneLinks.Manager.IsSubscribed);
                Assert.False(form.zoneLinks.Manager.Clamped);
                State(ConnectionState.Offline);
                form.zoneLinks.Count.Value = 0;
                State(ConnectionState.Online);
                Assert.False(form.zoneLinks.Manager.IsSubscribed);
            }
            finally { State(ConnectionState.Offline); }
        });
    }

    private static LinkGroup Mapping(MainForm form, int index) => form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Mapping"].Groups["Digital Outputs"].Groups[$"Zone{index}"];
    private static void Configure(MainForm form)
    {
        form.zoneLinks.Enabled.Value = true;
        for (int i = 0; i < 3; i++)
        {
            var zone = form.zoneLinks.Zones[i];
            zone.Enabled.Value = true;
            zone.Mode.Value = i == 1 ? "Extraction" : "Workholding";
            zone.Selected.Value = i != 1;
            zone.MaxX.Value = zone.MaxY.Value = 100;
            ((TextLink)Mapping(form, i + 1).Members["Output"]).Value = $"DM1.OUT{i}";
        }
    }

    private static void OnUi(Action<MainForm> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string path = @"Software\Radonix\Tests\ManagedZones-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(path);
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                action(form);
            }
            catch (Exception e) { failure = e; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

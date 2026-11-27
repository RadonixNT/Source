using Microsoft.Win32;
using NT;
using NT.ConfigurationManager;
using NT.Controller;
using NT.LinkManager;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Buffers.Binary;
using NT.Tools;
using System.Windows.Forms;

namespace RouterXYZTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class AxisTopologyTests
{
    [Fact]
    public void FreshDefaultsHaveThreeAxesAndGeneratedHoming()
        => Run(settings =>
        {
            using (var axes = settings.CreateSubKey(Initialize.InterfaceName + "\\Controller\\Axes")) axes.DeleteValue("YG", false);
            using var form = Create(settings);
            Assert.Equal(new[] { "X", "Y", "Z" }, form.controller.Axes.Select(a => a.Name));
            Assert.Equal("Z>X,Y", Axes(form).ReadText("HomeSequence"));
            Assert.True(form.controllerLinks.ValidatePlcSettings());
            Assert.True(HomeProgramGenerator.IsGenerated(HomeSource(form).Value));
            Assert.DoesNotContain("YG.", HomeSource(form).Value);
        });

    [Fact]
    public void SequenceAndGantryCancelTogetherThenPersistAndReload()
        => Run(settings =>
        {
            using (var form = Create(settings))
            {
                TextLink sequence = (TextLink)Axes(form).Members["HomeSequence"];
                string original = HomeSource(form).Value;
                using var dialog = new ConfigurationForm(form.baseGroup, null, null,
                    () => form.controllerLinks.ValidatePlcSettings(), form.RestoreConfigurationAfterCancel);
                var journal = (Dictionary<Link, object?>)typeof(ConfigurationForm).GetField("originalValues", Flags)!.GetValue(dialog)!;
                journal.Add(sequence, sequence.Value);
                journal.Add(Toggle(Axes(form), "YG"), true);
                sequence.Value = "X>Y>Z";
                Toggle(Axes(form), "YG").Value = false;
                Assert.True(form.controllerLinks.ValidatePlcSettings());
                Assert.NotEqual(original, HomeSource(form).Value);
                typeof(ConfigurationForm).GetMethod("RestoreChanges", Flags)!.Invoke(dialog, null);
                Assert.Equal(original, HomeSource(form).Value);
                Assert.Equal("Z>X,Y", sequence.Value);
                Assert.True(Toggle(Axes(form), "YG").Value);
                sequence.Value = "X,Y>Z";
                Toggle(Axes(form), "YG").Value = false;
                Assert.True(form.controllerLinks.ValidatePlcSettings());
                form.ApplyControllerProperties();
                Assert.Equal(2, form.controller.GetAxisIndex("Z"));
                Assert.Equal(new[] { "X", "Y", "Z" }, form.controller.ToolPaths[2].Axes.Select(a => a.Name));
                Assert.Same(form.baseGroup.Groups["General"].Groups["Z"].Members["JogPosition"], form.generalLinks.GetJogPositionLink(2));
                form.baseGroup.SaveRegistry();
            }
            using var reloaded = Create(settings);
            Assert.Equal("X,Y>Z", Axes(reloaded).ReadText("HomeSequence"));
            Assert.False(Toggle(Axes(reloaded), "YG").Value);
            Assert.True(reloaded.controllerLinks.ValidatePlcSettings());
            Assert.DoesNotContain("YG.", HomeSource(reloaded).Value);
        });

    [Fact]
    public void SensorMappingChoosesCoupledOrIndependentHomingAndCustomSourceSurvives()
        => Run(settings =>
        {
            using var form = Create(settings);
            var mapping = form.controller.PLC.IOMappings["HomeSensorYG"];
            var mapLinks = (System.Collections.IDictionary)typeof(ControllerLinks).GetField("plcIoMappingLinks", Flags)!.GetValue(form.controllerLinks)!;
            var links = mapLinks[mapping]!;
            var enabled = (LogicLink)links.GetType().GetProperty("Enabled")!.GetValue(links)!;
            Assert.Contains("AXES_DECOUPLE(Y, YG)", HomeSource(form).Value);
            enabled.Value = false;
            Assert.True(form.controllerLinks.ValidatePlcSettings());
            Assert.DoesNotContain("AXES_DECOUPLE(Y, YG)", HomeSource(form).Value);
            Assert.DoesNotContain("HomeSensorYG", HomeSource(form).Value);
            string custom = HomeSource(form).Value + "\n// Custom engineering source";
            HomeSource(form).Value = custom;
            enabled.Value = true;
            Assert.Equal(custom, HomeSource(form).Value);
        });

    private static TextLink HomeSource(MainForm form) => (TextLink)form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Command Programs"].Members[Initialize.HomePlcProgramName];

    [Fact]
    public void InvalidSequenceIsRejectedAndCancelRestoresSourceEditedAfterRegeneration()
        => Run(settings =>
        {
            using var form = Create(settings);
            var sequence = (TextLink)Axes(form).Members["HomeSequence"];
            string original = HomeSource(form).Value;
            sequence.Value = "Z>X,X";
            Assert.False(form.controllerLinks.AcceptGantryDraft(sequence));
            Assert.Equal(original, HomeSource(form).Value);
            sequence.Value = "Z>X,Y";
            using var dialog = new ConfigurationForm(form.baseGroup, null, null,
                () => form.controllerLinks.ValidatePlcSettings(), form.RestoreConfigurationAfterCancel);
            var journal = (Dictionary<Link, object?>)typeof(ConfigurationForm).GetField("originalValues", Flags)!.GetValue(dialog)!;
            journal.Add(sequence, sequence.Value);
            sequence.Value = "X>Y>Z";
            journal.Add(HomeSource(form), HomeSource(form).Value);
            HomeSource(form).Value += "\n// Draft custom change";
            typeof(ConfigurationForm).GetMethod("RestoreChanges", Flags)!.Invoke(dialog, null);
            Assert.Equal(original, HomeSource(form).Value);
            Toggle(Axes(form), "XG").Value = true;
            Assert.Empty(form.controllerLinks.ValidateCodeLink(HomeSource(form)));
            Assert.DoesNotContain(form.controller.Axes, axis => axis.Name == "XG");
        });

    [Fact]
    public void AxisDependenciesIncludeRawSlotsAndIgnoreNestedComments()
        => Assert.Equal(new[] { "XG", "YG", "AX04", "ZG" }, ControllerLinks.FindRawAxisReferences(
            "// XG\n(* YG (* AX05 *) ZG *) XG.Homed; AXES_STOP(YG); ax04.ReadyToMove; ZG; HomeSensorXG;"));

    [Fact]
    public void ExistingAxisSettingsAndPhysicalOrderAreRetained()
        => Run(settings =>
        {
            using (var z = settings.CreateSubKey(Initialize.InterfaceName + "\\Controller\\Axes\\Axis03"))
                z.SetValue("Step", "0.007");
            using var form = Create(settings);
            Assert.Equal(new[] { "X", "Y", "YG", "Z" }, form.controller.Axes.Select(axis => axis.Name));
            Assert.Equal(0.007, form.controller.GetAxisByName("Z").Step);
        });

    [Theory]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Online)]
    [InlineData(ConnectionState.Disconnecting)]
    public void ActiveSessionCannotEnableGantry(ConnectionState state)
        => Run(settings =>
        {
            using var form = Create(settings);
            var core = form.controller.MotionController;
            var field = core.GetType().GetField("connectionState", Flags)!;
            field.SetValue(core, state);
            try
            {
                Toggle(Axes(form), "XG").Value = true;
                Assert.False(Toggle(Axes(form), "XG").Value);
                Assert.False((bool)typeof(Link).GetMethod("CanEdit", Flags)!.Invoke(
                    Toggle(Axes(form), "YG"), [AccessPermission.Engineering, null])!);
                Assert.Throws<InvalidOperationException>(() => form.controller.ConfigureAxes(["X", "Y", "Z"]));
            }
            finally { field.SetValue(core, ConnectionState.Offline); }
        });

    [Fact]
    public void NativePropertyEditorAddsAxisAndCancelRestoresTree()
        => Run(settings =>
        {
            using var form = Create(settings);
            using var dialog = new ConfigurationForm(form.baseGroup)
            {
                CurrentPermission = AccessPermission.Engineering,
                WindowLayoutPersistenceProvider = () => false,
                StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000), ShowInTaskbar = false,
                ConfirmDraftEdit = form.controllerLinks.AcceptGantryDraft
            };
            dialog.Show();
            var toggle = Toggle(Axes(form), "XG");
            var tree = (TreeView)typeof(ConfigurationForm).GetField("ConfigurationTreeView", Flags)!.GetValue(dialog)!;
            var editor = (sLinkEditor)typeof(ConfigurationForm).GetField("ConfigurationLinkEditor", Flags)!.GetValue(dialog)!;
            static IEnumerable<TreeNode> Flatten(TreeNode node)
                => new[] { node }.Concat(node.Nodes.Cast<TreeNode>().SelectMany(Flatten));
            IEnumerable<TreeNode> Nodes() => tree.Nodes.Cast<TreeNode>().SelectMany(Flatten);
            tree.SelectedNode = Nodes().First(node => ReferenceEquals(node.Tag, Axes(form)));
            GridItem root = editor.PropertyGrid.SelectedGridItem;
            while (root.Parent is not null) root = root.Parent;
            GridItem? Find(GridItem item) => item.PropertyDescriptor?.Name == "XG" ? item
                : item.GridItems.Cast<GridItem>().Select(Find).FirstOrDefault(child => child is not null);
            var selected = Find(root)!;
            editor.PropertyGrid.SelectedGridItem = selected;
            typeof(ConfigurationForm).GetMethod("ObserveSelectedLink", Flags)!.Invoke(dialog, null);
            selected.PropertyDescriptor!.SetValue(editor.PropertyGrid.SelectedObject, true);
            typeof(ConfigurationForm).GetMethod("PropertyGridValueChanged", Flags)!.Invoke(dialog,
                [editor.PropertyGrid, new PropertyValueChangedEventArgs(selected, false)]);
            Application.DoEvents();
            Assert.Contains(toggle, dialog.ChangedLinks);
            Assert.Contains(Nodes(), node => ReferenceEquals(node.Tag, Axes(form).Groups["XG"]));
            typeof(ConfigurationForm).GetMethod("ConfigurationCancelButtonClick", Flags)!.Invoke(dialog, [null, EventArgs.Empty]);
            Assert.False(toggle.Value);
            Assert.Equal(AccessView.Hidden, Axes(form).Groups["XG"].View);
        });

    [Fact]
    public void GantryDraftAppliesStableNamesCouplingAndPersistsHiddenSettings()
        => Run(settings =>
        {
            using (var form = Create(settings))
            {
                var axes = Axes(form);
                var scale = (RealLink)axes.Groups["XG"].Members["Step"];
                scale.Value = 0.004;
                Toggle(axes, "XG").Value = true;
                Toggle(axes, "ZG").Value = true;
                Assert.Equal(4, form.controller.Axes.Length);
                Assert.True(form.controllerLinks.ValidateAxisTopology());
                form.controllerLinks.ApplyControllerProperties();
                Assert.Equal(new[] { "X", "Y", "YG", "Z", "XG", "ZG" }, form.controller.Axes.Select(a => a.Name));
                foreach (string name in new[] { "XG", "YG", "ZG" })
                    Assert.Same(form.controller.GetAxisByName(name[..1]), form.controller.GetAxisByName(name).CouplingMaster);
                Assert.Equal(new[] { "X", "Y", "Z" }, form.controller.ToolPaths[0].Axes.Select(a => a.Name));
                Toggle(axes, "XG").Value = false;
                form.controllerLinks.ApplyControllerProperties();
                Assert.Equal(AccessView.Hidden, axes.Groups["XG"].View);
                Assert.Equal(0.004, scale.Value);
                form.baseGroup.SaveRegistry();
            }
            using var reload = Create(settings);
            Assert.False(Toggle(Axes(reload), "XG").Value);
            Assert.True(Toggle(Axes(reload), "ZG").Value);
            Toggle(Axes(reload), "XG").Value = true;
            reload.controllerLinks.ApplyControllerProperties();
            Assert.Equal(0.004, reload.controller.GetAxisByName("XG").Step);
            Assert.Equal("Y", reload.controller.GetAxisByName("YG").CouplingMaster!.Name);
        });

    [Fact]
    public void CancelRestoresToggleAndEditedHiddenAxisWithoutApplying()
        => Run(settings =>
        {
            using var form = Create(settings);
            var axes = Axes(form);
            var toggle = Toggle(axes, "XG");
            var scale = (RealLink)axes.Groups["XG"].Members["Step"];
            double initial = scale.Value;
            using var dialog = new ConfigurationForm(form.baseGroup);
            var journal = (Dictionary<Link, object?>)typeof(ConfigurationForm).GetField("originalValues", Flags)!.GetValue(dialog)!;
            journal.Add(toggle, toggle.Value);
            toggle.Value = true;
            journal.Add(scale, scale.Value);
            scale.Value = 0.02;
            toggle.Value = false;
            typeof(ConfigurationForm).GetMethod("RestoreChanges", Flags)!.Invoke(dialog, null);
            Assert.False(toggle.Value);
            Assert.Equal(initial, scale.Value);
            Assert.Equal(AccessView.Hidden, axes.Groups["XG"].View);
            Assert.Equal(4, form.controller.Axes.Length);
            form.baseGroup.SaveRegistry();
        });

    [Fact]
    public void NormalModeWithoutVerifiedLicenseRejectsAdditionButSimulationAllowsIt()
        => Run(settings =>
        {
            using (var simulation = Create(settings))
            {
                Toggle(Axes(simulation), "XG").Value = true;
                Assert.True(simulation.controllerLinks.ValidateAxisTopology());
            }
            using (var group = settings.CreateSubKey(Initialize.InterfaceName + "\\Controller")) group.SetValue("Mode", "Normal");
            using var form = Create(settings);
            var axes = Axes(form);
            Toggle(axes, "XG").Value = true;
            Assert.False(form.controllerLinks.AcceptGantryDraft(Toggle(axes, "XG")));
            Assert.Contains("license", axes.StateMessage);
            Assert.Throws<InvalidOperationException>(() => form.controllerLinks.ApplyControllerProperties());
            Assert.Equal(4, form.controller.Axes.Length);
        });

    [Fact]
    public void ExistingPlcDependencyRejectsYGRemovalWithoutEditingToolPrograms()
        => Run(settings =>
        {
            using var form = Create(settings);
            var programs = form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Command Programs"];
            var source = (TextLink)programs.Members["MeasureToolLength"];
            string original = source.Value;
            ((TextLink)programs.Members["HomeY"]).Value += "\n// Engineer-owned homing.";
            Toggle(Axes(form), "YG").Value = false;
            Assert.False(form.controllerLinks.ValidateAxisTopology());
            Assert.Contains("HomeY", Axes(form).StateMessage);
            Assert.DoesNotContain("MeasureToolLength", Axes(form).StateMessage);
            Assert.Contains("TP2.Homed", source.Value);
            Assert.Contains("TP2.AxesIdle", source.Value);
            Assert.DoesNotContain("YG.", source.Value);
            Assert.Throws<InvalidOperationException>(() => form.controllerLinks.ApplyControllerProperties());
            Assert.Equal(original, source.Value);
            Assert.Equal("Y", form.controller.GetAxisByName("YG").CouplingMaster!.Name);
        });

    [Fact]
    public void CandidateCompilationRestoresAppliedCatalogAndCoupling()
        => Run(settings =>
        {
            using var form = Create(settings);
            Toggle(Axes(form), "XG").Value = true;
            Assert.True(form.controllerLinks.ValidatePlcSettings(), string.Join("\n", form.controller.PLC.Programs.SelectMany(p => p.CompilationDiagnostics).Select(d => d.ToString())) + Axes(form).StateMessage + form.baseGroup.Groups["Controller"].Groups["PLC"].StateMessage);
            Assert.Equal(new[] { "X", "Y", "YG", "Z" }, form.controller.Axes.Select(a => a.Name));
            Assert.Equal("Y", form.controller.GetAxisByName("YG").CouplingMaster!.Name);
            form.controllerLinks.ApplyControllerProperties();
            Assert.Equal("X", form.controller.GetAxisByName("XG").CouplingMaster!.Name);
        });

    [Fact]
    public void AllEightAxisSelectionsPreserveMainWrappersAndRejectPreparedPaths()
    {
        var controller = new ControllerWrapper(3, 3, "unused.xml");
        try
        {
            controller.SetAxisNames(["X", "Y", "Z"]);
            var x = controller.Axes[0];
            for (int bits = 0; bits < 8; bits++)
            {
                var names = new[] { "X", "Y", "Z" }.Concat(new[] { "XG", "YG", "ZG" }.Where((_, i) => (bits & (1 << i)) != 0)).ToArray();
                controller.ConfigureAxes(names);
                controller.ToolPaths[0].SetToolPathAxes("XYZ");
                Assert.Equal(names, controller.Axes.Select(a => a.Name));
                Assert.Same(x, controller.Axes[0]);
                Assert.Same(x, controller.ToolPaths[0].Axes[0]);
            }
            Assert.Throws<ArgumentException>(() => controller.ConfigureAxes(["X", "X"]));
            Assert.Equal(6, controller.Axes.Length);
            var path = controller.MotionController.ToolPaths[3];
            path.SetToolPathAxes("X");
            path.Begin();
            path.AddLine(1, [0.0], 1.0);
            path.End();
            Assert.Throws<InvalidOperationException>(() => controller.ConfigureAxes(["X", "Y", "Z"]));
        }
        finally { controller.Disconnect(); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(16)]
    public void CapacityRequiresExactLayoutAndValidSignature(int count)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] payload = new byte[60];
        "RNTL"u8.CopyTo(payload);
        payload[4] = 3;
        payload[5] = (byte)count;
        Guid.NewGuid().TryWriteBytes(payload.AsSpan(8, 16));
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(24, 8), 1700000000);
        SHA256.HashData(key.ExportSubjectPublicKeyInfo()).AsSpan(0, 16).CopyTo(payload.AsSpan(42));
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(58), 1000);
        byte[] signature = key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        byte[] envelope = payload.Concat(signature).ToArray();
        string Encode() => "RNT3." + Convert.ToBase64String(envelope).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var q = key.ExportParameters(false).Q;
        byte[] point = q.X!.Concat(q.Y!).ToArray();
        Assert.Equal(count, AxisLicenseCapacity.Read(Encode(), point));
        Assert.Null(AxisLicenseCapacity.Read(Encode())); // Untrusted issuer.
        envelope[5] ^= 1;
        Assert.Null(AxisLicenseCapacity.Read(Encode(), point));
        Assert.Null(AxisLicenseCapacity.Read("RNT3.invalid", point));
    }

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void MachineZeroDialogUsesConfiguredMotorNamesAndHomingMethods()
        => Run(settings =>
        {
            using var form = Create(settings);
            var companion = Axes(form).Groups.Values.Single(group =>
                group.Members.ContainsKey("Name") && group.ReadText("Name") == "YG");
            Assert.Null(companion.FindGroup("AbsoluteReference"));
            Assert.Equal(AccessView.Hidden, companion.GetTextLink("MotorMachineZero").View);
            companion.GetTextLink("HomingMethod").Value = nameof(HomingMethod.AbsoluteEncoder);
            form.controllerLinks.ApplyControllerProperties();
            using var dialog = form.controllerLinks.CreateMachineZeroForm();
            foreach (var axis in form.controller.Axes)
            {
                var button = Assert.IsType<sButton>(Assert.Single(dialog.Controls.Find("CaptureZero" + axis.Name, true)));
                Assert.Equal(axis.Name == "YG", button.Enabled);
            }
            Assert.Equal("Ctrl+Shift+Alt+0", form.keyboardLinks.KeyboardEnabled.Group!.ReadText("OpenMachineZero"));
            Assert.Contains(form.generalLinks.OpenMachineZeroLink, form.keyboardLinks.Commands);
        });

    [Fact]
    public void AbsoluteHomeUsesConfiguredMethodAndPreservesCustomPrograms()
        => Run(settings =>
        {
            using var form = Create(settings);
            var axes = Axes(form);
            LinkGroup Motor(string name) => axes.Groups.Values.Single(g =>
                g.Members.ContainsKey("Name") && g.ReadText("Name") == name);
            var source = HomeSource(form);
            var sequence = axes.GetTextLink("HomeSequence");
            string defaultSequence = sequence.Value;
            Motor("Z").GetTextLink("HomingMethod").Value = nameof(HomingMethod.AbsoluteEncoder);
            Assert.True(form.controllerLinks.ValidatePlcSettings());
            Assert.Contains("AXIS_JOG(Z := ZTarget", source.Value);
            Assert.DoesNotContain("HomeSensorZ", source.Value);
            string custom = source.Value + "\n// Engineer edit";
            source.Value = custom;
            sequence.Value = defaultSequence.Replace("Z>", "");
            Assert.True(form.controllerLinks.ValidatePlcSettings());
            Assert.Equal(custom, source.Value);
            source.Value = Initialize.GetPlcSource(Initialize.HomePlcProgramName);
            Assert.True(form.controllerLinks.ValidatePlcSettings());
            Assert.DoesNotContain("AXIS_JOG(Z", source.Value);
            Assert.Contains("Z.Homed", source.Value);
            Motor("Y").GetTextLink("HomingMethod").Value = nameof(HomingMethod.AbsoluteEncoder);
            Assert.False(form.controllerLinks.ValidatePlcSettings());
            Motor("YG").GetTextLink("HomingMethod").Value = nameof(HomingMethod.AbsoluteEncoder);
            Assert.True(form.controllerLinks.ValidatePlcSettings());
            Assert.Contains("YG.CouplingOffset", source.Value);
            Assert.DoesNotContain("AXES_DECOUPLE(Y, YG)", source.Value);
        });

    [Fact]
    public void RegisterZeroRejectionIsShownAndReportedForTheSameAxis()
        => Run(settings =>
        {
            using var form = Create(settings);
            Axes(form).Groups["Axis00"].GetTextLink("HomingMethod").Value = nameof(HomingMethod.AbsoluteEncoder);
            form.controllerLinks.ApplyControllerProperties();
            using var dialog = form.controllerLinks.CreateMachineZeroForm();
            dialog.Show();
            var button = Assert.IsType<sButton>(Assert.Single(dialog.Controls.Find("CaptureZeroX", true)));
            string? reported = null;
            void Observe(object? sender, NT.Diagnostics.NTLogEntry entry)
            {
                if (entry.Message.StartsWith("Register machine zero: X", StringComparison.Ordinal))
                    reported = entry.Message;
            }
            NT.Diagnostics.NTLog.EntryWritten += Observe;
            try
            {
                button.PerformClick();
                Assert.True(SpinWait.SpinUntil(() => { Application.DoEvents(); return reported != null; }, 3000));
                var status = (sLabel)typeof(ConfigurationSecurityDialog).GetField("Status", Flags)!.GetValue(dialog)!;
                Assert.Equal("Register machine zero: " + status.Text, reported);
                Assert.Contains("offline", status.Text, StringComparison.OrdinalIgnoreCase);
            }
            finally { NT.Diagnostics.NTLog.EntryWritten -= Observe; }
        });

    private static LinkGroup Axes(MainForm form) => form.baseGroup.Groups["Controller"].Groups["Axes"];
    private static LogicLink Toggle(LinkGroup axes, string name) => (LogicLink)axes.Members[name];
    private static MainForm Create(RegistryKey settings)
    {
        var form = new MainForm();
        form.InitializeRuntime(settings, connectOnStartup: false);
        return form;
    }
    private static void Run(Action<RegistryKey> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string path = @"Software\Radonix\Tests\AxisTopology-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(path);
                using (var axes = settings.CreateSubKey(Initialize.InterfaceName + "\\Controller\\Axes")) axes.SetValue("YG", "True");
                action(settings);
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Axis configuration test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

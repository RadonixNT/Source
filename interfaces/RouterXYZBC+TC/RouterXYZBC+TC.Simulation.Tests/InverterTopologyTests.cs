using NT.Tools;
using System.Windows.Forms;
using Microsoft.Win32;
using NT.LinkManager;
using NT.Controller;
using NT.ConfigurationManager;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace RouterXYZBCTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class InverterTopologyTests
{
    [Fact]
    public void DefaultIsZeroAndCountsPersistWithStableIdentity()
        => Run(settings =>
        {
            using (var form = Create(settings))
            {
                Assert.Equal(0, Count(form).Value);
                Assert.All(form.controller.MotionController.Inverters, i => Assert.False(i.Configuration.Enabled));
                Assert.Equal(AccessView.Hidden, Group(form).Groups["INV0"].View);
                Assert.Equal("DM0.OUT0", form.controller.PLC.IOMappings["SpindleCW"].Target);
                Assert.Equal("AM0.OUT0", form.controller.PLC.IOMappings["SpindleSpeed"].Target);
                var slot = form.controller.MotionController.Inverters[0];
                var child = Group(form).Groups["INV0"];
                Count(form).Value = 2;
                Assert.False(slot.Configuration.Enabled);
                Assert.True(form.controllerLinks.ValidatePlcSettings());
                Assert.False(slot.Configuration.Enabled);
                form.controllerLinks.ApplyControllerProperties();
                Assert.All(form.controller.MotionController.Inverters, i => Assert.True(i.Configuration.Enabled));
                Count(form).Value = 0;
                form.controllerLinks.ApplyControllerProperties();
                Count(form).Value = 2;
                form.controllerLinks.ApplyControllerProperties();
                Assert.Same(slot, form.controller.MotionController.Inverters[0]);
                Assert.Same(child, Group(form).Groups["INV0"]);
                form.baseGroup.SaveRegistry();
            }
            using (var reload = Create(settings))
            {
                Assert.Equal(2, Count(reload).Value);
                Assert.All(reload.controller.MotionController.Inverters, i => Assert.True(i.Configuration.Enabled));
                Count(reload).Value = 0;
                reload.controllerLinks.ApplyControllerProperties();
                reload.baseGroup.SaveRegistry();
            }
            using var zero = Create(settings);
            Assert.Equal(0, Count(zero).Value);
            Assert.All(zero.controller.MotionController.Inverters, i => Assert.False(i.Configuration.Enabled));
        });

    [Fact]
    public void MappingCandidateValidatesWithoutCommittingAndBlocksRemoval()
        => Run(settings =>
        {
            using var form = Create(settings);
            Count(form).Value = 2;
            var target = Target(form);
            Assert.Contains("INV1.RUN", target.Items);
            target.Value = "INV1.RUN";
            Assert.True(form.controllerLinks.ValidatePlcSettings());
            Assert.False(form.controller.MotionController.Inverters[1].Configuration.Enabled);
            form.controllerLinks.ApplyControllerProperties();
            Count(form).Value = 1;
            Assert.False(form.controllerLinks.ValidateInverterTopology());
            Assert.False(form.controllerLinks.ValidatePlcSettings(allowProgramErrors: true));
            Assert.Contains("SpindleCW", Group(form).StateMessage);
            Assert.Throws<InvalidOperationException>(() => form.controllerLinks.ApplyControllerProperties());
            Assert.True(form.controller.MotionController.Inverters[1].Configuration.Enabled);
            Assert.Equal("INV1.RUN", target.Value);
            var enabled = (LogicLink)form.baseGroup.Groups["Controller"].Groups["PLC"]
                .Groups["Mapping"].Groups["Digital Outputs"].Groups["SpindleCW"].Members["Enabled"];
            enabled.Value = false;
            Assert.False(form.controllerLinks.ValidateInverterTopology());
            target.Value = "Unassigned";
            Assert.True(form.controllerLinks.ValidatePlcSettings());
            form.controllerLinks.ApplyControllerProperties();
            Assert.False(form.controller.MotionController.Inverters[1].Configuration.Enabled);
        });

    [Fact]
    public void PlcDependenciesBlockRemovalIncludingErrorOverride()
        => Run(settings =>
        {
            using var form = Create(settings);
            Count(form).Value = 2;
            form.controllerLinks.ApplyControllerProperties();
            var source = form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Command Programs"].Members.Values.OfType<TextLink>().First();
            string original = source.Value;
            source.Value = "INV1.RUN := FALSE;";
            Count(form).Value = 1;
            Assert.False(form.controllerLinks.ValidateInverterTopology());
            Assert.Contains("PLC", Group(form).StateMessage);
            Assert.False(form.controllerLinks.ValidatePlcSettings(allowProgramErrors: true));
            source.Value = "// INV1.RUN\n(* INV1.SPEED *)\n" + original;
            Assert.True(form.controllerLinks.ValidateInverterTopology());
        });

    [Fact]
    public void CancelRestoresCountMappingAndVisibilityWithoutApplying()
        => Run(settings =>
        {
            using var form = Create(settings);
            using var dialog = new ConfigurationForm(form.baseGroup);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var journal = (Dictionary<Link, object?>)typeof(ConfigurationForm).GetField("originalValues", flags)!.GetValue(dialog)!;
            journal.Add(Count(form), Count(form).Value);
            journal.Add(Target(form), Target(form).Value);
            Count(form).Value = 2;
            Target(form).Value = "INV1.RUN";
            Assert.Equal(AccessView.ReadWrite, Group(form).Groups["INV1"].View);
            typeof(ConfigurationForm).GetMethod("RestoreChanges", flags)!.Invoke(dialog, null);
            Assert.Equal(0, Count(form).Value);
            Assert.Equal("DM0.OUT0", Target(form).Value);
            Assert.Equal(AccessView.Hidden, Group(form).Groups["INV1"].View);
            Assert.All(form.controller.MotionController.Inverters, i => Assert.False(i.Configuration.Enabled));
            Assert.True(form.controllerLinks.ValidateInverterTopology());
            form.baseGroup.SaveRegistry();
            using var reload = Create(settings);
            Assert.Equal(0, Count(reload).Value);
            Assert.Equal("DM0.OUT0", Target(reload).Value);
        });

    [Theory]
    [InlineData(ConnectionState.Online)]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Disconnecting)]
    public void ActiveConnectionRejectsCountEdits(ConnectionState state)
        => Run(settings =>
        {
            using var form = Create(settings);
            var core = form.controller.MotionController;
            var field = typeof(Core).GetField("connectionState", BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(core, state);
            try
            {
                Assert.Throws<InvalidOperationException>(() => Count(form).Value = 1);
                Assert.Equal(0, Count(form).Value);
            }
            finally { field.SetValue(core, ConnectionState.Offline); }
        });

    [Fact]
    public void ScannerIgnoresCommentsStringsAndOtherIdentifiers()
        => Assert.Equal(new[] { "INV0", "INV1" }, ControllerLinks.FindRawInverterReferences(
            "// INV1.RUN\n(* INV0.RUN (* INV1.RUN *) *)\n'INV1.RUN'; MyINV1.RUN; Other.INV1.RUN; inv0 . SPEED; INV1.STATE;"));

    [Fact]
    public void PreparedPathRejectsCountChanges()
        => Run(settings =>
        {
            using var form = Create(settings);
            var path = form.controller.MotionController.ToolPaths[3];
            path.SetToolPathAxes("X");
            path.Begin();
            path.AddLine(1, [0.0], 1.0);
            path.End();
            Assert.Throws<InvalidOperationException>(() => Count(form).Value = 1);
            Assert.Equal(0, Count(form).Value);
        });

    [Fact]
    public void SavedOrphanMappingIsRetainedForCorrection()
        => Run(settings =>
        {
            using (var form = Create(settings))
            {
                Count(form).Value = 2;
                Target(form).Value = "INV1.RUN";
                Count(form).Value = 0;
                form.baseGroup.SaveRegistry();
            }
            using var reload = Create(settings);
            Assert.Equal(0, Count(reload).Value);
            Assert.Equal("INV1.RUN", Target(reload).Value);
            Assert.False(reload.controllerLinks.ValidateInverterTopology());
            Count(reload).Value = 2;
            Assert.True(reload.controllerLinks.ValidatePlcSettings());
        });

    [Fact]
    public void PropertyEditorCountChangeUpdatesTreeAndCancelRestoresIt()
        => Run(settings =>
        {
            using var form = Create(settings);
            using var dialog = new ConfigurationForm(form.baseGroup)
            {
                CurrentPermission = AccessPermission.Engineering,
                WindowLayoutPersistenceProvider = () => false,
                StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000), ShowInTaskbar = false
            };
            dialog.Show();
            var count = Count(form);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var tree = (TreeView)typeof(ConfigurationForm).GetField("ConfigurationTreeView", flags)!.GetValue(dialog)!;
            var editor = (sLinkEditor)typeof(ConfigurationForm).GetField("ConfigurationLinkEditor", flags)!.GetValue(dialog)!;
            static IEnumerable<TreeNode> Flatten(TreeNode node)
                => new[] { node }.Concat(node.Nodes.Cast<TreeNode>().SelectMany(Flatten));
            IEnumerable<TreeNode> Nodes() => tree.Nodes.Cast<TreeNode>().SelectMany(Flatten);
            tree.SelectedNode = Nodes().First(n => ReferenceEquals(n.Tag, Group(form)));
            GridItem root = editor.PropertyGrid.SelectedGridItem;
            while (root.Parent is not null) root = root.Parent;
            GridItem? Find(GridItem item) => item.PropertyDescriptor?.Name == count.Name ? item
                : item.GridItems.Cast<GridItem>().Select(Find).FirstOrDefault(child => child is not null);
            var selected = Find(root)!;
            editor.PropertyGrid.SelectedGridItem = selected;
            typeof(ConfigurationForm).GetMethod("ObserveSelectedLink", flags)!.Invoke(dialog, null);
            object owner = editor.PropertyGrid.SelectedObject;
            selected.PropertyDescriptor!.SetValue(owner, 2L);
            typeof(ConfigurationForm).GetMethod("PropertyGridValueChanged", flags)!.Invoke(dialog,
                [editor.PropertyGrid, new PropertyValueChangedEventArgs(selected, 0L)]);
            Application.DoEvents();
            Assert.Contains(count, dialog.ChangedLinks);
            Assert.Contains(Nodes(), n => ReferenceEquals(n.Tag, Group(form).Groups["INV1"]));
            typeof(ConfigurationForm).GetMethod("ConfigurationCancelButtonClick", flags)!.Invoke(dialog, [null, EventArgs.Empty]);
            Assert.Equal(0, count.Value);
            Assert.Equal(AccessView.Hidden, Group(form).Groups["INV1"].View);
        });

    private static MainForm Create(RegistryKey settings)
    {
        var form = new MainForm();
        form.InitializeRuntime(settings, connectOnStartup: false);
        return form;
    }
    private static LinkGroup Group(MainForm form) => form.baseGroup.Groups["Controller"].Groups["Inverters"];
    private static IntegerLink Count(MainForm form) => (IntegerLink)Group(form).Members["InverterCount"];
    private static TextLink Target(MainForm form) => (TextLink)form.baseGroup.Groups["Controller"].Groups["PLC"]
        .Groups["Mapping"].Groups["Digital Outputs"].Groups["SpindleCW"].Members["Output"];
    private static void Run(Action<RegistryKey> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string path = @"Software\Radonix\Tests\InverterTopology-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZBCTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(path);
                action(settings);
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Inverter topology test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

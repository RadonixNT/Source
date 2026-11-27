using Microsoft.Win32;
using NT.LinkManager;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NT.ConfigurationManager;
using NT.Controller;
using NT.Tools;
using System.Windows.Forms;

namespace CutterXYZ.Simulation.Tests;

[Collection("Router UI")]
public sealed class ModuleTopologyTests
{
    [Fact]
    public void CountsPersistAndLogicalIdentitySurvivesCompactSlotChanges()
        => Run(settings =>
        {
            using (var form = Create(settings))
            {
                LinkGroup modules = Modules(form);
                Assert.Equal(new[] { "DM0", "DM1", "AM0" }, Names(form));
                Assert.Equal(2, Count(modules, "DigitalCount").Value);
                var analogUid = (IntegerLink)modules.Groups["AM0"].Members["UID"];
                analogUid.Value = 0x12345678;
                Count(modules, "DigitalCount").Value = 3;
                Count(modules, "EncoderCount").Value = 1;
                Assert.Equal(new[] { "DM0", "DM1", "AM0" }, Names(form)); // Draft only.
                Assert.True(form.controllerLinks.ValidateModuleTopology());
                form.controllerLinks.ApplyControllerProperties();
                Assert.True(form.controllerLinks.RefreshModuleRuntimeStatesIfChanged());
                Assert.Equal(new[] { "DM0", "DM1", "DM2", "AM0", "EM0" }, Names(form));
                Assert.Equal(0x12345678u, form.controller.PLC.Modules[3].Uid);
                Count(modules, "DigitalCount").Value = 2;
                form.controllerLinks.ApplyControllerProperties();
                Assert.Equal(0x12345678u, form.controller.PLC.Modules[2].Uid);
                Assert.Same(analogUid, modules.Groups["AM0"].Members["UID"]);
                Count(modules, "EncoderCount").Value = 0;
                form.controllerLinks.ApplyControllerProperties();
                form.baseGroup.SaveRegistry();
            }
            using var reload = Create(settings);
            Assert.Equal(new[] { "DM0", "DM1", "AM0" }, Names(reload));
            Assert.Equal(0, Count(Modules(reload), "EncoderCount").Value);
            Assert.Equal(0x12345678u, reload.controller.PLC.Modules[2].Uid);
        });

    [Fact]
    public void RemovalRejectsMappingAndRawSourceWithoutChangingAppliedTopology()
        => Run(settings =>
        {
            using var form = Create(settings);
            LinkGroup modules = Modules(form);
            ((TextLink)form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Mapping"]
                .Groups["Digital Inputs"].Groups["HomeSensorX"].Members["Input"]).Value = "DM1.IN6";
            Count(modules, "DigitalCount").Value = 1;
            Assert.False(form.controllerLinks.ValidateModuleTopology());
            Assert.Contains("HomeSensorX", modules.StateMessage);
            Assert.Throws<InvalidOperationException>(() => form.controllerLinks.ApplyControllerProperties());
            Assert.Equal(new[] { "DM0", "DM1", "AM0" }, Names(form));
            Count(modules, "DigitalCount").Value = 2;
            LinkGroup programs = form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Command Programs"];
            var source = (TextLink)programs.Members.Values.First();
            string original = source.Value;
            source.Value = "IF EM0.IN0 > 0 THEN END_FUNCTION 0; END_IF;";
            Assert.False(form.controllerLinks.ValidateModuleTopology());
            Assert.Contains("EM0", modules.StateMessage);
            source.Value = "// EM0.IN0\n(* DM7.OUT0 *)\n" + original;
            Assert.True(form.controllerLinks.ValidateModuleTopology());
        });

    [Fact]
    public void ExistingDialogJournalRestoresCountsAndHiddenModuleEdits()
        => Run(settings =>
        {
            using var form = Create(settings);
            LinkGroup modules = Modules(form);
            var digital = Count(modules, "DigitalCount");
            var uid = (IntegerLink)modules.Groups["DM2"].Members["UID"];
            using var dialog = new ConfigurationForm(form.baseGroup);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var journal = (Dictionary<Link, object?>)typeof(ConfigurationForm)
                .GetField("originalValues", flags)!.GetValue(dialog)!;
            journal.Add(digital, digital.Value);
            digital.Value = 3;
            journal.Add(uid, uid.Value);
            uid.Value = 123;
            digital.Value = 2;
            Assert.Equal(AccessView.Hidden, modules.Groups["DM2"].View);
            typeof(ConfigurationForm).GetMethod("RestoreChanges", flags)!.Invoke(dialog, null);
            Assert.Equal(2, digital.Value);
            Assert.Equal(0, uid.Value);
            Assert.Same(uid, modules.Groups["DM2"].Members["UID"]);
            Assert.Equal(new[] { "DM0", "DM1", "AM0" }, Names(form));
        });

    [Fact]
    public void NewModuleAndMappingValidateTogetherWithoutPrematureTopologyCommit()
        => Run(settings =>
        {
            using var form = Create(settings);
            var modules = Modules(form);
            Count(modules, "DigitalCount").Value = 3;
            var signal = form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Mapping"]
                .Groups["Digital Outputs"].Groups["Alarm"];
            var target = (TextLink)signal.Members["Output"];
            target.Value = "DM2.OUT0";
            Assert.Equal("DM2.OUT0", target.Value);
            Assert.True(form.controllerLinks.ValidatePlcSettings(allowProgramErrors: true));
            Assert.Equal(new[] { "DM0", "DM1", "AM0" }, Names(form));
            form.controllerLinks.ApplyControllerProperties();
            Assert.Equal(new[] { "DM0", "DM1", "DM2", "AM0" }, Names(form));
            Assert.True(form.controllerLinks.ValidatePlcSettings(allowProgramErrors: true));
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
            var count = Count(Modules(form), "DigitalCount");
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var tree = (TreeView)typeof(ConfigurationForm).GetField("ConfigurationTreeView", flags)!.GetValue(dialog)!;
            var editor = (sLinkEditor)typeof(ConfigurationForm).GetField("ConfigurationLinkEditor", flags)!.GetValue(dialog)!;
            static IEnumerable<TreeNode> Flatten(TreeNode node)
                => new[] { node }.Concat(node.Nodes.Cast<TreeNode>().SelectMany(Flatten));
            IEnumerable<TreeNode> Nodes() => tree.Nodes.Cast<TreeNode>().SelectMany(Flatten);
            tree.SelectedNode = Nodes().First(n => ReferenceEquals(n.Tag, Modules(form)));
            GridItem root = editor.PropertyGrid.SelectedGridItem;
            while (root.Parent is not null) root = root.Parent;
            GridItem? Find(GridItem item) => item.PropertyDescriptor?.Name == count.Name ? item
                : item.GridItems.Cast<GridItem>().Select(Find).FirstOrDefault(child => child is not null);
            var selected = Find(root)!;
            editor.PropertyGrid.SelectedGridItem = selected;
            typeof(ConfigurationForm).GetMethod("ObserveSelectedLink", flags)!.Invoke(dialog, null);
            object owner = editor.PropertyGrid.SelectedObject;
            selected.PropertyDescriptor!.SetValue(owner, 3L);
            typeof(ConfigurationForm).GetMethod("PropertyGridValueChanged", flags)!.Invoke(dialog,
                [editor.PropertyGrid, new PropertyValueChangedEventArgs(selected, 2L)]);
            Application.DoEvents();
            Assert.Contains(count, dialog.ChangedLinks);
            Assert.Contains(Nodes(), n => ReferenceEquals(n.Tag, Modules(form).Groups["DM2"]));
            typeof(ConfigurationForm).GetMethod("ConfigurationCancelButtonClick", flags)!.Invoke(dialog, [null, EventArgs.Empty]);
            Assert.Equal(2, count.Value);
            Assert.Equal(AccessView.Hidden, Modules(form).Groups["DM2"].View);
        });

    [Theory]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Online)]
    [InlineData(ConnectionState.Disconnecting)]
    public void ActiveSessionRejectsCountChanges(ConnectionState state)
        => Run(settings =>
        {
            using var form = Create(settings);
            var core = form.controller.MotionController;
            var field = core.GetType().GetField("connectionState", BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(core, state); // Admission test only; never starts a runtime.
            try
            {
                Count(Modules(form), "EncoderCount").Value = 1;
                var canEdit = typeof(Link).GetMethod("CanEdit", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Assert.False((bool)canEdit.Invoke(Count(Modules(form), "EncoderCount"), [AccessPermission.Engineering, null])!);
                Assert.False(form.controllerLinks.ValidateModuleTopology());
                Assert.Equal(new[] { "DM0", "DM1", "AM0" }, Names(form));
            }
            finally { field.SetValue(core, ConnectionState.Offline); }
        });

    [Fact]
    public void InvalidSavedTargetIsPreservedForCorrectionInsteadOfRemapping()
        => Run(settings =>
        {
            using (var form = Create(settings))
            {
                ((TextLink)form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Mapping"]
                    .Groups["Digital Inputs"].Groups["HomeSensorX"].Members["Input"]).Value = "DM1.IN6";
                Count(Modules(form), "DigitalCount").Value = 1;
                // Deliberately simulate an inconsistent imported/persisted draft.
                form.baseGroup.SaveRegistry();
            }
            using var reload = Create(settings);
            var target = (TextLink)reload.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Mapping"]
                .Groups["Digital Inputs"].Groups["HomeSensorX"].Members["Input"];
            Assert.Equal("DM1.IN6", target.Value);
            Assert.False(reload.controllerLinks.ValidateModuleTopology());
        });

    [Fact]
    public void ZeroCountsReloadWithoutFactoryModulesReappearing()
        => Run(settings =>
        {
            using (var form = Create(settings))
            {
                var mapping = form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Mapping"];
                foreach (var category in mapping.Groups.Values)
                    foreach (var signal in category.Groups.Values)
                        foreach (var target in signal.Members.Values.OfType<TextLink>().Where(l => l.Name is "Input" or "Output"))
                            target.Value = "Unassigned";
                foreach (string name in new[] { "DigitalCount", "AnalogCount", "EncoderCount" })
                    Count(Modules(form), name).Value = 0;
                Assert.True(form.controllerLinks.ValidateModuleTopology());
                form.controllerLinks.ApplyControllerProperties();
                Assert.Empty(Names(form));
                form.baseGroup.SaveRegistry();
            }
            using var reload = Create(settings);
            Assert.Empty(Names(reload));
        });

    [Fact]
    public void DependencyScannerIgnoresCommentsAndQualifiedSubstrings()
        => Assert.Equal(new[] { "DM2", "AM1", "EM0" }, ControllerLinks.FindRawModuleReferences(
            "// DM7.IN0\n(* DM6.IN0 (* AM0.IN0 *) *)\ndm2.OUT0 := AM1.IN0; EM0.IN0; Other.DM3.IN0; MyDM4.IN0;"));

    [Fact]
    public void DependencyScannerHandlesSpacedMembersAndIgnoresQuotedText()
        => Assert.Equal(new[] { "DM2", "AM1", "EM0" }, ControllerLinks.FindRawModuleReferences(
            "'DM7.IN0'; \"AM0.OUT0\"; (* EM1.IN0 *) dm2 . OUT0 := AM1 . IN0; EM0\n.IN0;"));

    private static MainForm Create(RegistryKey settings)
    {
        var form = new MainForm();
        form.InitializeRuntime(settings, connectOnStartup: false);
        return form;
    }
    private static LinkGroup Modules(MainForm form)
        => form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Modules"];
    private static IntegerLink Count(LinkGroup group, string name) => (IntegerLink)group.Members[name];
    private static string[] Names(MainForm form)
        => form.controller.PLC.Modules.Where(m => m.IsConfigured).Select(m => m.Name).ToArray();

    private static void Run(Action<RegistryKey> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string path = @"Software\Radonix\Tests\ModuleTopology-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(path);
                action(settings);
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Module topology test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

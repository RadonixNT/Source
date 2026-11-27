using Microsoft.Win32;
using NT.ConfigurationManager;
using NT.Controller;
using NT.LinkManager;
using NT_Interpreter.Shared.Router;
using NT_Interpreter.Shared.Router.Controller;
using NT_Interpreter.Interpreters.RouterXYZBCTC;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;

namespace RouterXYZBCTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class CustomCommandProgramTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void AddEditSaveReloadDeleteSaveReload()
        => Run(settings =>
        {
            using (var form = Create(settings))
            using (var dialog = Dialog(form))
            {
                var defaultProgram = form.controller.PLC.Programs["M3"];
                Select(dialog, form.controllerLinks.CommandProgramEditor.Group);
                var source = (TextLink)Invoke(dialog, "AddCommandProgram", "m015")!;
                Assert.Equal("M15", source.Name);
                Invoke(dialog, "EditProgramValue", source, "END_FUNCTION(Result := 15);\n");
                Assert.True(form.controllerLinks.ValidatePlcSettings());
                Assert.Contains("M15", Catalog(form));
                Assert.Same(defaultProgram, form.controller.PLC.Programs["M3"]);
                Assert.False(form.controller.PLC.Programs["M15"].Cyclic);
                Invoke(dialog, "ConfigurationOkButtonClick", null, EventArgs.Empty);
                Assert.Equal(DialogResult.OK, dialog.DialogResult);
            }
            using (var form = Create(settings))
            using (var dialog = Dialog(form))
            {
                var source = (TextLink)form.controllerLinks.CommandProgramEditor.Group.Members["M15"];
                Assert.Equal("END_FUNCTION(Result := 15);\n", source.Value);
                Assert.Contains("M15", Catalog(form));
                Select(dialog, source);
                Invoke(dialog, "RemoveCommandProgram", source);
                Assert.False(form.controller.PLC.Programs.TryGetValue("M15", out _));
                Assert.DoesNotContain("M15", Catalog(form));
                Invoke(dialog, "ConfigurationOkButtonClick", null, EventArgs.Empty);
            }
            using var reload = Create(settings);
            Assert.False(reload.controller.PLC.Programs.TryGetValue("M15", out _));
            Assert.False(reload.controllerLinks.CommandProgramEditor.Group.Members.ContainsKey("M15"));
        });

    [Fact]
    public void CancelAdditionDoesNotPersistEvenWhenApplicationSavesOnExit()
        => Run(settings =>
        {
            using (var form = Create(settings))
            using (var dialog = Dialog(form))
            {
                Select(dialog, form.controllerLinks.CommandProgramEditor.Group);
                var source = (TextLink)Invoke(dialog, "AddCommandProgram", "M15")!;
                Invoke(dialog, "EditProgramValue", source, "END_FUNCTION(Result := 9);");
                Invoke(dialog, "ConfigurationCancelButtonClick", null, EventArgs.Empty);
                Assert.False(form.controller.PLC.Programs.TryGetValue("M15", out _));
                Assert.Equal(AccessView.Hidden, source.View);
                form.baseGroup.SaveRegistry();
            }
            using var reload = Create(settings);
            Assert.False(reload.controller.PLC.Programs.TryGetValue("M15", out _));
        });

    [Fact]
    public void CancelDeleteReaddAndSourceEditsRestoresOriginalIdentityAndText()
        => Run(settings =>
        {
            using var form = Create(settings);
            var library = form.controllerLinks.CommandProgramEditor;
            var original = library.Add("M15", (l, v) => l.Value = v);
            original.Value = "END_FUNCTION(Result := 7);";
            form.baseGroup.SaveRegistry();
            using var dialog = Dialog(form);
            Select(dialog, original);
            Invoke(dialog, "EditProgramValue", original, "END_FUNCTION(Result := 8);");
            Invoke(dialog, "RemoveCommandProgram", original);
            var readded = (TextLink)Invoke(dialog, "AddCommandProgram", "m15")!;
            Assert.Same(original, readded);
            Invoke(dialog, "RemoveCommandProgram", readded);
            Invoke(dialog, "ConfigurationCancelButtonClick", null, EventArgs.Empty);
            Assert.Same(original, library.Group.Members["M15"]);
            Assert.Equal(AccessView.ReadWrite, original.View);
            Assert.Equal("END_FUNCTION(Result := 7);", original.Value);
            Assert.Equal(original.Value, form.controller.PLC.Programs["M15"].Source);
            Assert.Contains("M15", Catalog(form));
        });

    [Fact]
    public void MainToolbarSelectionDefaultsDuplicatesAndReferences()
        => Run(settings =>
        {
            using var form = Create(settings);
            using var dialog = Dialog(form);
            var library = form.controllerLinks.CommandProgramEditor;
            Select(dialog, form.baseGroup);
            Assert.False(Button(dialog, "addProgramButton").Enabled);
            Select(dialog, library.Group);
            Assert.True(Button(dialog, "addProgramButton").Enabled);
            Assert.False(Button(dialog, "removeProgramButton").Enabled);
            var factory = (TextLink)library.Group.Members["M3"];
            Select(dialog, factory);
            Assert.True(Button(dialog, "addProgramButton").Enabled);
            Assert.False(Button(dialog, "removeProgramButton").Enabled);
            Assert.Throws<InvalidOperationException>(() => library.Remove(factory, (l, v) => l.Value = v));
            foreach (string invalid in new[] { "M03", "M6", "M30", "", "../M15", "bad name" })
                Assert.Throws<InvalidOperationException>(() => library.Add(invalid, (l, v) => l.Value = v));
            var custom = (TextLink)Invoke(dialog, "AddCommandProgram", "M15")!;
            Assert.True(Button(dialog, "removeProgramButton").Enabled);
            if (Environment.GetEnvironmentVariable("RADONIX_CUSTOM_PROGRAM_CAPTURE") is { Length: > 0 } capture)
            {
                dialog.ClientSize = new(1200, 720);
                Application.DoEvents();
                using var image = new System.Drawing.Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(image, dialog.ClientRectangle);
                image.Save(capture, System.Drawing.Imaging.ImageFormat.Png);
            }
            Assert.Throws<InvalidOperationException>(() => library.Add("m015", (l, v) => l.Value = v));
            var other = library.Add("M16", (l, v) => l.Value = v);
            other.Value = "M15();";
            Assert.Throws<InvalidOperationException>(() => library.Remove(custom, (l, v) => l.Value = v));
            other.Value = "// M15()\n(* M15 *) END_FUNCTION(Result := 0);";
            library.Remove(custom, (l, v) => l.Value = v);
        });

    [Theory]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Online)]
    [InlineData(ConnectionState.Disconnecting)]
    public void RejectsActiveSession(ConnectionState state)
        => Run(settings =>
        {
            using var form = Create(settings);
            var core = form.controller.MotionController;
            var field = core.GetType().GetField("connectionState", Private)!;
            field.SetValue(core, state);
            try { Assert.Throws<InvalidOperationException>(() => form.controllerLinks.CommandProgramEditor.Add("M15", (l, v) => l.Value = v)); }
            finally { field.SetValue(core, ConnectionState.Offline); }
        });

    [Fact]
    public void AddedMCodeCompilesAndPreparesUsingRefreshedCatalog()
        => Run(settings =>
        {
            using var form = Create(settings);
            form.controllerLinks.CommandProgramEditor.Add("M15", (l, v) => l.Value = v);
            Assert.True(form.controllerLinks.ValidatePlcSettings());
            var capture = typeof(GCodeInterpreter).GetMethod("CaptureContext", Private)!;
            var (compiler, context) = ((RouterCompiler, RouterMachineContext))capture.Invoke(form.gCodeInterpreter, null)!;
            var result = compiler.Compile("M15\nG1 X10 F600", context);
            Assert.True(result.Success);
            var builder = (RouterToolPathBuilder)typeof(GCodeInterpreter).GetField("builder", Private)!.GetValue(form.gCodeInterpreter)!;
            var prepared = builder.Build(result.Program!);
            Assert.DoesNotContain(prepared.Diagnostics, d => d.IsBlocking);
            Assert.DoesNotContain(prepared.Diagnostics, d => d.CommandCode == "M15");
            Assert.True(builder.Path.BlockCount > 0);
            builder.Path.Begin(0);
        });

    [Fact]
    public void EmptyCommandGroupCanAddFirstProgramAndToolbarRespectsPermission()
        => Run(_ =>
        {
            using var root = new LinkGroup("Test", "Test", null);
            var library = new EmptyLibrary(root.AddGroup("Commands", "Command Programs"));
            library.Group.MemberEditorKey = NT.Tools.NTCodeEditorProfiles.PlcEditorKey;
            using var dialog = new ConfigurationForm(root)
            {
                CommandPrograms = library, CurrentPermission = AccessPermission.Engineering,
                WindowLayoutPersistenceProvider = () => false,
                StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000), ShowInTaskbar = false
            };
            dialog.Show(); Select(dialog, library.Group);
            Assert.True(Button(dialog, "addProgramButton").Enabled);
            Assert.False(Button(dialog, "removeProgramButton").Enabled);
            var first = (TextLink)Invoke(dialog, "AddCommandProgram", "M15")!;
            Assert.Equal("M15", first.Name);
            Assert.True(Button(dialog, "removeProgramButton").Enabled);
            dialog.CurrentPermission = AccessPermission.Administrator;
            Assert.False(Button(dialog, "addProgramButton").Enabled);
            Assert.False(Button(dialog, "removeProgramButton").Enabled);
        });

    private sealed class EmptyLibrary(LinkGroup group) : IConfigurationCommandPrograms
    {
        public LinkGroup Group => group;
        public string EditRestriction => string.Empty;
        public bool CanRemove(TextLink program) => ReferenceEquals(program.Group, group);
        public TextLink Add(string name, Action<TextLink, string> edit) => group.AddText(name, name, "END_FUNCTION(Result := 0);");
        public void Remove(TextLink program, Action<TextLink, string> edit) => program.View = AccessView.Hidden;
    }

    [Fact]
    public void UnwrappedFourthToolPathAlsoBlocksStructuralEdits()
        => Run(settings =>
        {
            using var form = Create(settings);
            var path = form.controller.MotionController.ToolPaths[3];
            path.SetToolPathAxes("X");
            path.Begin(0); path.AddLine(1, [1d], 1d); path.End();
            Assert.Throws<InvalidOperationException>(() => form.controllerLinks.CommandProgramEditor.Add("M15", (l, v) => l.Value = v));
            path.Begin(0);
        });

    private static ConfigurationForm Dialog(MainForm form)
    {
        var dialog = new ConfigurationForm(form.baseGroup, form.controllerLinks.CreateCodeEditorProfile,
            form.controllerLinks.ValidateCodeLink, form.ValidateSettings, form.RestoreConfigurationAfterCancel)
        {
            CommandPrograms = form.controllerLinks.CommandProgramEditor,
            CurrentPermission = AccessPermission.Engineering,
            WindowLayoutPersistenceProvider = () => false,
            StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000), ShowInTaskbar = false
        };
        dialog.BindController(form.controller, form.generalLinks.SelectedRuntimeMode);
        dialog.ConfigurationChanged += (_, change) =>
        {
            if (change.Tag == ConfigurationChangeTag.ConfigurationApplied)
                form.generalLinks.ApplyAcceptedConfiguration(change.Links);
        };
        dialog.Show();
        return dialog;
    }
    private static ToolStripButton Button(ConfigurationForm dialog, string name)
        => (ToolStripButton)typeof(ConfigurationForm).GetField(name, Private)!.GetValue(dialog)!;
    private static void Select(ConfigurationForm dialog, object target)
    {
        var tree = (TreeView)typeof(ConfigurationForm).GetField("ConfigurationTreeView", Private)!.GetValue(dialog)!;
        static IEnumerable<TreeNode> Flatten(TreeNode node) => new[] { node }.Concat(node.Nodes.Cast<TreeNode>().SelectMany(Flatten));
        tree.SelectedNode = tree.Nodes.Cast<TreeNode>().SelectMany(Flatten).First(n => ReferenceEquals(n.Tag, target));
        Invoke(dialog, "RefreshProgramActions");
    }
    private static object? Invoke(ConfigurationForm dialog, string method, params object?[] args)
        => typeof(ConfigurationForm).GetMethod(method, Private)!.Invoke(dialog, args);
    private static string[] Catalog(MainForm form)
        => ((RouterAuxiliaryDefinition[])typeof(GCodeInterpreter).GetField("auxiliaryDefinitions", Private)!
            .GetValue(form.gCodeInterpreter)!).Select(d => d.Code.ToString()).ToArray();
    private static MainForm Create(RegistryKey settings)
    {
        var form = new MainForm(); form.InitializeRuntime(settings, connectOnStartup: false); return form;
    }
    private static void Run(Action<RegistryKey> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string path = @"Software\Radonix\Tests\CustomPrograms-" + Guid.NewGuid().ToString("N");
            try { RouterXYZBCTCTexts.Register(); using var settings = Registry.CurrentUser.CreateSubKey(path); action(settings); }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Custom command program test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

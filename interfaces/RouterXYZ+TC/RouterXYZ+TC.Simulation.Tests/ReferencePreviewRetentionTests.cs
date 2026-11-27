using NT_Interpreter.Shared.Router;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Microsoft.Win32;
using NT.Tools;
using NT_Interpreter.Interpreters.RouterXYZTC;
using NT.LinkManager;
using NT.ConfigurationManager;
using System.IO.MemoryMappedFiles;

namespace RouterXYZTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class ReferencePreviewRetentionTests
{
    [Fact]
    public void MappingEditorsRejectEveryRetainedPathAndRecoverAfterExplicitClear()
        => Run("G21\nG90\nG1 X10 F60", (form, _) =>
        {
            static IEnumerable<LinkGroup> Groups(LinkGroup group)
                => new[] { group }.Concat(group.Groups.Values.SelectMany(Groups));
            LinkGroup mapping = Groups(form.baseGroup).First(group => group.Members.Values.Any(link => link.Name == "FilterMode"));
            Link[] links = mapping.Members.Values.Where(link => link.Name is "Input" or "Enabled" or "FilterMode").ToArray();
            Assert.Equal(3, links.Length);
            var canEdit = typeof(Link).GetMethod("CanEdit", BindingFlags.Instance | BindingFlags.NonPublic)!;
            bool Editable(Link link) => (bool)canEdit.Invoke(link, [AccessPermission.Engineering, null])!;
            using var dialog = ConfigurationDialog(form, _ => true);
            foreach (var path in form.controller.MotionController.ToolPaths)
            {
                InvokeSettings(dialog, "ClearToolPath", null, EventArgs.Empty);
                Assert.All(links, link => Assert.True(Editable(link)));
                path.SetToolPathAxes("X");
                path.Begin();
                path.AddLine(1, [1d], 1d);
                path.End();
                Assert.All(links, link =>
                {
                    Assert.False(Editable(link));
                    Assert.Contains("ToolPaths", link.Description);
                });
                var enabled = (LogicLink)links.Single(link => link.Name == "Enabled");
                bool original = enabled.Value;
                EditSetting(dialog, enabled, !original);
                Assert.Equal(original, enabled.Value);
                foreach (var text in links.OfType<TextLink>())
                {
                    string previous = text.Value;
                    EditSetting(dialog, text, text.Name == "FilterMode"
                        ? (previous == "HighSpeed" ? "Normal" : "HighSpeed")
                        : (previous == "TRUE" ? "FALSE" : "TRUE"));
                    Assert.Equal(previous, text.Value);
                }
            }
            InvokeSettings(dialog, "ClearToolPath", null, EventArgs.Empty);
            Assert.All(links, link => Assert.True(Editable(link)));
        });

    [Fact]
    public void SettingsClearUsesControllerAndRefreshesPreviewWithoutClearingTwice()
        => Run("G21\nG90\nG1 X10 F60", (form, scene) =>
        {
            using var dialog = ConfigurationDialog(form, _ => true);
            var path = form.controller.MotionController.ToolPaths[0];
            long generation = path.PreparationGeneration;
            Assert.NotNull(form.gCodeInterpreter.Compilation);
            Assert.Single(scene.Scene.Root.Children.OfType<S3DPath>());
            InvokeSettings(dialog, "ClearToolPath", null, EventArgs.Empty);
            Assert.Equal(generation + 1, path.PreparationGeneration);
            Assert.Equal(0, path.BlockCount);
            Assert.Null(form.gCodeInterpreter.Compilation);
            Assert.Null(form.gCodeInterpreter.Geometry);
            Assert.Empty(scene.Scene.Root.Children.OfType<S3DPath>());
            InvokeSettings(dialog, "ConfigurationCancelButtonClick", null, EventArgs.Empty);
            Assert.Equal(0, path.BlockCount); // Clear is immediate, not a draft setting.
        });
    [Theory]
    [InlineData("axis", true)]
    [InlineData("path", true)]
    [InlineData("traverse", true)]
    [InlineData("axis", false)]
    public void SettingsConfirmOnlyTheMotionDraftAndResetOnlyAfterOk(string kind, bool accept)
        => Run("G21\nG90\nG1 X10 F60\nG1 X20", (form, _) =>
        {
            MarkPartialProgress(form);
            var geometry = form.gCodeInterpreter.Geometry;
            var path = form.controller.ToolPaths[0];
            long generation = path.CoordinateResetGeneration;
            RealLink motion = MotionSetting(form, kind);
            double original = motion.Value, controllerAcceleration = form.controller.Axes[0].Acceleration;
            int confirmations = 0;
            using var dialog = ConfigurationDialog(form, links =>
            {
                Assert.Same(motion, Assert.Single(links)); confirmations++; return accept;
            });
            EditSetting(dialog, form.generalLinks.RefreshRateLink, "Low");
            Assert.Equal(0, confirmations);
            EditSetting(dialog, motion, original + (kind == "path" ? -.1 : 1));
            Assert.Equal(1, confirmations);
            Assert.Equal(accept ? original + (kind == "path" ? -.1 : 1) : original, motion.Value);
            Assert.Equal(controllerAcceleration, form.controller.Axes[0].Acceleration);
            Assert.Equal(generation, path.CoordinateResetGeneration);
            Assert.Equal(3, path.CurrentBlock);
            Assert.Same(geometry, form.gCodeInterpreter.Geometry);

            InvokeSettings(dialog, "ConfigurationOkButtonClick", null, EventArgs.Empty);
            Assert.Equal(DialogResult.OK, dialog.DialogResult);
            Assert.Equal(1, confirmations); // OK must not ask again for the same cursor/value.
            if (accept)
            {
                Assert.True(Pump(form.gCodeInterpreter.PendingLoad));
                Assert.NotSame(geometry, form.gCodeInterpreter.Geometry);
                Assert.NotEqual(generation, path.CoordinateResetGeneration);
                Assert.Equal(0, path.CurrentBlock);
                Assert.False(path.IsRunning);
            }
            else
            {
                Assert.Same(geometry, form.gCodeInterpreter.Geometry);
                Assert.Equal(generation, path.CoordinateResetGeneration);
                Assert.Equal(3, path.CurrentBlock);
                Assert.Equal("Low", form.generalLinks.RefreshRateLink.Value);
            }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelOrRevertingMotionDraftPreservesThePausedProgram(bool revertThenOk)
        => Run("G21\nG90\nG1 X10 F60\nG1 X20", (form, _) =>
        {
            MarkPartialProgress(form);
            var geometry = form.gCodeInterpreter.Geometry;
            long generation = form.controller.ToolPaths[0].CoordinateResetGeneration;
            RealLink motion = MotionSetting(form, "axis"); double original = motion.Value;
            bool accept = true;
            int confirmations = 0;
            using var dialog = ConfigurationDialog(form, _ => { confirmations++; return accept; });
            EditSetting(dialog, motion, original + 10);
            accept = false;
            EditSetting(dialog, motion, original + 20);
            Assert.Equal(original + 10, motion.Value); // No restores the last accepted edit, not the opening value.
            EditSetting(dialog, form.generalLinks.RefreshRateLink, "Low");
            if (revertThenOk)
            {
                EditSetting(dialog, motion, original);
                Assert.Equal(2, confirmations); // Restoring the opening value needs no reset consent.
                Assert.DoesNotContain(motion, dialog.ChangedLinks);
                InvokeSettings(dialog, "ConfigurationOkButtonClick", null, EventArgs.Empty);
                    Assert.Equal("Low", form.generalLinks.RefreshRateLink.Value);
            }
            else
            {
                InvokeSettings(dialog, "ConfigurationCancelButtonClick", null, EventArgs.Empty);
                Assert.Equal("High", form.generalLinks.RefreshRateLink.Value);
            }
            Assert.Equal(original, motion.Value);
            Assert.Equal(generation, form.controller.ToolPaths[0].CoordinateResetGeneration);
            Assert.Equal(3, form.controller.ToolPaths[0].CurrentBlock);
            Assert.Same(geometry, form.gCodeInterpreter.Geometry);
        });

    [Fact]
    public void UnconfirmedMotionEditCanRefuseOkWithoutSavingAndStillCancel()
        => Run("G21\nG90\nG1 X10 F60\nG1 X20", (form, _) =>
        {
            MarkPartialProgress(form);
            RealLink motion = MotionSetting(form, "axis"); double original = motion.Value;
            form.baseGroup.SaveRegistry();
            object? persisted = motion.Group!.Registry!.GetValue(motion.Name);
            int confirmations = 0;
            using var dialog = ConfigurationDialog(form, _ => { confirmations++; return false; });
            // Exercise the acceptance safety net independently of the per-field prompt.
            dialog.ConfirmDraftEdit = null;
            EditSetting(dialog, motion, original + 10);
            InvokeSettings(dialog, "ConfigurationOkButtonClick", null, EventArgs.Empty);
            Assert.Equal(1, confirmations);
            Assert.Equal(DialogResult.None, dialog.DialogResult);
            Assert.False(dialog.IsDisposed);
            Assert.Equal(persisted, motion.Group.Registry.GetValue(motion.Name));
            Assert.Equal(original, form.controller.Axes[0].Acceleration);
            Assert.Equal(3, form.controller.ToolPaths[0].CurrentBlock);
            InvokeSettings(dialog, "ConfigurationCancelButtonClick", null, EventArgs.Empty);
            Assert.Equal(original, motion.Value);
        });

    private static RealLink MotionSetting(MainForm form, string kind) => kind switch
    {
        "axis" => (RealLink)form.baseGroup.Groups["Controller"].Groups["Axes"].Groups["Axis00"].Members["Acceleration"],
        "path" => (RealLink)form.baseGroup.Groups["Controller"].Groups["ToolPaths"].Groups["ToolPath0"].Members["AccelerationCoefficient"],
        _ => form.generalLinks.TraverseVelocityLink
    };

    private static ConfigurationForm ConfigurationDialog(MainForm form, Func<IReadOnlyList<Link>, bool> confirm)
    {
        var dialog = new ConfigurationForm(form.baseGroup)
        {
            CurrentPermission = AccessPermission.Engineering,
            WindowLayoutPersistenceProvider = () => false,
            StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000), ShowInTaskbar = false
        };
        form.generalLinks.ConfigureConfigurationAcceptance(dialog, confirm);
        dialog.BindController(form.controller, form.generalLinks.SelectedRuntimeMode);
        dialog.ConfigurationChanged += (_, change) =>
        {
            if (change.Tag == ConfigurationChangeTag.ConfigurationApplied)
            {
                form.generalLinks.ApplyAcceptedConfiguration(change.Links);
                dialog.PendingControllerWork = form.PendingProgramPreparation;
            }
            if (change.Tag == ConfigurationChangeTag.ToolPathCleared && change.ToolPathIndex == 0)
                form.RefreshClearedConfigurationToolPath();
        };
        dialog.Show();
        return dialog;
    }

    private static void EditSetting(ConfigurationForm dialog, Link link, object value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var tree = (TreeView)typeof(ConfigurationForm).GetField("ConfigurationTreeView", flags)!.GetValue(dialog)!;
        var editor = (sLinkEditor)typeof(ConfigurationForm).GetField("ConfigurationLinkEditor", flags)!.GetValue(dialog)!;
        static TreeNode Find(TreeNodeCollection nodes, LinkGroup group)
            => nodes.Cast<TreeNode>().SelectMany(Flatten).First(node => ReferenceEquals(node.Tag, group));
        static IEnumerable<TreeNode> Flatten(TreeNode node)
            => new[] { node }.Concat(node.Nodes.Cast<TreeNode>().SelectMany(Flatten));
        tree.SelectedNode = Find(tree.Nodes, link.Group!);
        var grid = editor.PropertyGrid;
        GridItem root = grid.SelectedGridItem;
        while (root.Parent is not null) root = root.Parent;
        GridItem FindItem(GridItem item) => item.PropertyDescriptor?.Name == link.Name ? item
            : item.GridItems.Cast<GridItem>().Select(FindItem).FirstOrDefault(child => child is not null)!;
        GridItem selected = FindItem(root);
        grid.SelectedGridItem = selected;
        InvokeSettings(dialog, "ObserveSelectedLink");
        var descriptor = selected.PropertyDescriptor!;
        object? old = descriptor.GetValue(grid.SelectedObject);
        descriptor.SetValue(grid.SelectedObject, value);
        InvokeSettings(dialog, "PropertyGridValueChanged", grid, new PropertyValueChangedEventArgs(selected, old));
    }

    private static void InvokeSettings(ConfigurationForm dialog, string method, params object?[] args)
        => typeof(ConfigurationForm).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, args);

    private static void MarkPartialProgress(MainForm form)
    {
        // Synthetic native feedback only; no controller process or hardware is started.
        var core = form.controller.MotionController;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object memory = core.GetType().GetField("sharedMemory", flags)!.GetValue(core)!;
        var view = (MemoryMappedViewAccessor)memory.GetType().GetProperty("Accessor", flags)!.GetValue(memory)!;
        var layout = core.GetType().Assembly.GetType("NT.Controller.ToolPathSharedMemoryLayout")!;
        long Constant(string name) => Convert.ToInt64(layout.GetField(name)!.GetRawConstantValue());
        view.Write(8 * (Constant("ToolPathRegisterBase") + Constant("CurrentBlockSlot")), 3L);
        Assert.True(form.HasToolPathProgress);
    }

    [Fact]
    public void ReferenceReportRecordsAcceptedChangesWithoutDuplicateOrRejectedEntries()
        => Run("G21\nG90\nG0 X0 Y0 Z0\nG1 X2 F60", (form, _) =>
        {
            var messages = new List<string>();
            void Capture(object? sender, NT.Diagnostics.NTLogEntry entry) => messages.Add(entry.Message);
            NT.Diagnostics.NTLog.EntryWritten += Capture;
            try
            {
                form.referenceLinks.SetValue(1, 0, 12);
                form.referenceLinks.SetValue(1, 0, 12);
                form.referenceLinks.CurrentReference.Value = 1;
                form.referenceLinks.CurrentReference.Value = 1;
                Assert.Throws<ArgumentOutOfRangeException>(() => form.referenceLinks.SetValue(1, 0, double.NaN));
                Assert.Single(messages.Where(message => message.StartsWith("Reference coordinate changed:")));
                Assert.Single(messages.Where(message => message.StartsWith("Reference selected:")));
                Assert.Contains(messages, message => message.Contains("G55 X 0 -> 12"));
                Assert.Contains(messages, message => message.Contains("G54 -> G55") && message.Contains("X=12"));
            }
            finally { NT.Diagnostics.NTLog.EntryWritten -= Capture; }
        });

    [Theory]
    [InlineData(NT.Controller.ToolPathStopReason.Completed, "Program completed naturally")]
    [InlineData(NT.Controller.ToolPathStopReason.StopRequested, "Program stopped after a requested stop")]
    [InlineData(NT.Controller.ToolPathStopReason.Fault, "observed axis, coupling or PLC fault")]
    [InlineData(NT.Controller.ToolPathStopReason.Unknown, "unknown stop origin")]
    [InlineData(NT.Controller.ToolPathStopReason.Completed, "Program completed naturally", true)]
    [InlineData(NT.Controller.ToolPathStopReason.StopRequested, "Program stopped after a requested stop", true)]
    [InlineData(NT.Controller.ToolPathStopReason.Fault, "observed axis, coupling or PLC fault", true)]
    [InlineData(NT.Controller.ToolPathStopReason.Unknown, "unknown stop origin", true)]
    public void StopEventReportsOnceAndRejectsObsoleteCursor(NT.Controller.ToolPathStopReason reason, string text, bool parkEnabled = false)
        => Run("G21\nG90\nG0 X0 Y0 Z0\nG1 X2 F60", (form, _) =>
        {
            const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var core = form.controller.MotionController;
            var path = core.ToolPaths[0];
            Assert.False(form.generalLinks.ParkEnabledLink.Value);
            form.generalLinks.ParkEnabledLink.Value = parkEnabled;
            var state = core.GetType().GetField("connectionState", fields)!;
            typeof(NT.Controller.ToolPath).GetField("executionId", fields)!.SetValue(path, 10L);
            var constructor = typeof(NT.Controller.ToolPathStoppedEventArgs).GetConstructors(fields).Single();
            object Stop(long execution, long coordinates) => constructor.Invoke(
                [0, execution, path.PreparationGeneration, coordinates, 1, reason, null]);
            var receive = typeof(MainForm).GetMethod("ControllerToolPathStopped", fields)!;
            var refresh = typeof(MainForm).GetMethod("RefreshProgramExecution", fields)!;
            var messages = new List<string>();
            void Capture(object? sender, NT.Diagnostics.NTLogEntry entry) => messages.Add(entry.Message);
            NT.Diagnostics.NTLog.EntryWritten += Capture;
            try
            {
                state.SetValue(core, NT.Controller.ConnectionState.Online);
                typeof(MainForm).GetField("ethercatOperational", fields)!.SetValue(form, true);
                receive.Invoke(form, [core, Stop(9, path.CoordinateResetGeneration)]);
                refresh.Invoke(form, null);
                Assert.Empty(messages);
                receive.Invoke(form, [core, Stop(10, path.CoordinateResetGeneration)]);
                receive.Invoke(form, [core, Stop(9, path.CoordinateResetGeneration)]);
                refresh.Invoke(form, null);
                refresh.Invoke(form, null);
                Assert.Contains(text, messages[0]);
                bool expectPark = parkEnabled && reason == NT.Controller.ToolPathStopReason.Completed;
                Assert.Equal(expectPark ? 1 : 0,
                    messages.Count(message => message.StartsWith("Positioning requested: GoToPark;")));
                // This fixture has no ready axes: admission rejects the request once,
                // without requiring spindle mappings or scheduling a later retry.
                int acceptedMessageCount = messages.Count;
                if (!expectPark) Assert.Single(messages);
                if (reason != NT.Controller.ToolPathStopReason.Completed)
                    Assert.DoesNotContain(messages, message => message.Contains("completed naturally"));
                typeof(NT.Controller.ToolPath).GetField("executionId", fields)!.SetValue(path, 11L);
                receive.Invoke(form, [core, Stop(11, path.CoordinateResetGeneration - 2)]);
                refresh.Invoke(form, null);
                Assert.Equal(acceptedMessageCount, messages.Count);
            }
            finally
            {
                state.SetValue(core, NT.Controller.ConnectionState.Offline);
                NT.Diagnostics.NTLog.EntryWritten -= Capture;
            }
        });

    [Fact]
    public void OperationReportWritesOnlyOnObservedRuntimeEdges()
        => Run("G21\nG90\nG0 X0 Y0 Z0\nG1 X2 F60", (form, _) =>
        {
            const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var core = form.controller.MotionController;
            var state = core.GetType().GetField("connectionState", fields)!;
            object memory = core.GetType().GetField("sharedMemory", fields)!.GetValue(core)!;
            var view = (System.IO.MemoryMappedFiles.MemoryMappedViewAccessor)memory.GetType().GetProperty("Accessor", fields)!.GetValue(memory)!;
            long Constant(string type, string name) => Convert.ToInt64(core.GetType().Assembly.GetType("NT.Controller." + type)!.GetField(name)!.GetRawConstantValue());
            long pathStatus = 8 * (Constant("ToolPathSharedMemoryLayout", "ToolPathRegisterBase") + Constant("ToolPathSharedMemoryLayout", "StatusSlot"));
            var refresh = typeof(MainForm).GetMethod("RefreshOperationReport", fields)!;
            var messages = new List<string>();
            void Capture(object? sender, NT.Diagnostics.NTLogEntry entry) => messages.Add(entry.Message);
            NT.Diagnostics.NTLog.EntryWritten += Capture;
            try
            {
                state.SetValue(core, NT.Controller.ConnectionState.Online);
                typeof(MainForm).GetField("ethercatOperational", fields)!.SetValue(form, true);
                refresh.Invoke(form, null);
                refresh.Invoke(form, null);
                Assert.Single(messages);
                view.Write(pathStatus, Constant("ToolPathStatusBits", "Running"));
                refresh.Invoke(form, null);
                refresh.Invoke(form, null);
                Assert.Single(messages.Where(message => message.Contains("Program execution running")));
                view.Write(pathStatus, Constant("ToolPathStatusBits", "Stopped"));
                refresh.Invoke(form, null);
                refresh.Invoke(form, null);
                Assert.Single(messages.Where(message => message.Contains("Program execution no longer running")));
            }
            finally
            {
                state.SetValue(core, NT.Controller.ConnectionState.Offline);
                NT.Diagnostics.NTLog.EntryWritten -= Capture;
            }
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PositionMismatchIsAcceptedOnlyWhenAuxiliaryReturnIsAdmitted(bool m100Loaded)
        => Run("G1 X10 F60", (form, _) =>
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var core = form.controller.MotionController;
            object memory = core.GetType().GetField("sharedMemory", fields)!.GetValue(core)!;
            var view = (System.IO.MemoryMappedFiles.MemoryMappedViewAccessor)memory.GetType()
                .GetProperty("Accessor", fields)!.GetValue(memory)!;
            long Constant(string type, string name) => Convert.ToInt64(core.GetType().Assembly
                .GetType("NT.Controller." + type)!.GetField(name)!.GetRawConstantValue());
            long AxisAddress(int index, string slot) => 8 * (Constant("AxisSharedMemoryLayout", "AxisRegisterBase")
                + index * Constant("AxisSharedMemoryLayout", "AxisRegisterStride") + Constant("AxisSharedMemoryLayout", slot));
            var links = (ControllerLinks)typeof(MainForm).GetField("controllerLinks", fields)!.GetValue(form)!;
            var state = core.GetType().GetField("connectionState", fields)!;
            Assert.True((bool)core.PLC.GetType().GetMethod("DownloadCompiledPrograms", fields)!.Invoke(core.PLC, [memory])!);
            var m100 = core.PLC.Programs["M100"];
            var offsetMethod = memory.GetType().GetMethod("PlcProgramHeaderCellOffset",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
            long header = Convert.ToInt64(offsetMethod.Invoke(null,
                [m100.RuntimeIndex, (int)Constant("NTSharedMemory", "PlcStatusSlot")]));
            view.Write(header, m100Loaded ? Constant("PLC.PLCProgramStatusBits", "Loaded") : 0L);
            view.Write(Constant("SharedRegisterIndex", "DetectedAxisCount") * 8, (long)core.Axes.Count);
            foreach (var axis in core.Axes)
            {
                view.Write(AxisAddress(axis.Index, "StatusSlot"), Constant("AxisStatusBits", "ReadyToMove")
                    | Constant("AxisStatusBits", "Stopped") | Constant("AxisStatusBits", "Homed"));
                view.Write(AxisAddress(axis.Index, "CommandPositionSlot"), axis.Name == "X" ? 20.0 : 0.0);
            }
            // Supply the normal coupled-gantry feedback without connecting a runtime.
            int y = form.controller.GetAxisByName("Y").Index;
            int y1 = form.controller.GetAxisByName("YG").Index;
            long mask = (1L << y) | (1L << y1);
            foreach (int index in new[] { y, y1 })
            {
                view.Write(AxisAddress(index, "CoupledAxesSlot"), mask);
                view.Write(AxisAddress(index, "CouplingMasterIndexSlot"), (long)y);
                view.Write(AxisAddress(index, "CouplingRoleSlot"), (long)(index == y
                    ? NT.Controller.AxisCouplingRole.Master : NT.Controller.AxisCouplingRole.Slave));
            }
            foreach (var path in core.ToolPaths)
                view.Write(8 * (Constant("ToolPathSharedMemoryLayout", "ToolPathRegisterBase")
                    + path.Index * Constant("ToolPathSharedMemoryLayout", "ToolPathRegisterStride")
                    + Constant("ToolPathSharedMemoryLayout", "StatusSlot")),
                    Constant("ToolPathStatusBits", "ReadyToMove") | Constant("ToolPathStatusBits", "Stopped"));
            typeof(MainForm).GetField("ethercatOperational", fields)!.SetValue(form, true);
            try
            {
                state.SetValue(core, NT.Controller.ConnectionState.Online);
                Assert.Equal(m100Loaded, links.RunOrPositionMainToolPath());
                long pathCommand(int index) => view.ReadInt64(8 * (Constant("ToolPathSharedMemoryLayout", "ToolPathRegisterBase")
                    + index * Constant("ToolPathSharedMemoryLayout", "ToolPathRegisterStride")
                    + Constant("ToolPathSharedMemoryLayout", "CommandSlot")));
                Assert.Equal(0L, pathCommand(0));
                Assert.Equal(m100Loaded ? Constant("ToolPathCommandBits", "Run") : 0L, pathCommand(1));
                Assert.Equal(m100Loaded, links.IsHeadPositioningActive);
                Assert.False(links.RunOrPositionMainToolPath());
            }
            finally
            {
                state.SetValue(core, NT.Controller.ConnectionState.Offline);
                links.CancelHeadPositioningContinuation();
            }
        });

    [Fact]
    public void FeedProgressUsesOnlyPathAxesAndRefreshesItsCachedLimit()
        => Run("G1 X10 F60", (form, _) =>
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var progress = (sProgressBar)typeof(MainForm).GetField("R_MachineFeedValue", fields)!.GetValue(form)!;
            form.controller.GetAxisByName("X").VelocityLimit = 3;
            form.controller.GetAxisByName("Y").VelocityLimit = 4;
            form.controller.GetAxisByName("Z").VelocityLimit = 12;
            form.controller.GetAxisByName("YG").VelocityLimit = 1000;
            var initialize = typeof(MainForm).GetMethod("InitializeMachineView", fields)!;
            initialize.Invoke(form, null);
            form.DisplayMachineFeed(6.5);
            Assert.Equal(50, progress.Value, 9);
            Assert.Contains("006.5", progress.Text);
            form.DisplayMachineFeed(26);
            Assert.Equal(100, progress.Value);
            form.controller.GetAxisByName("Z").VelocityLimit = Math.Sqrt(651);
            initialize.Invoke(form, null);
            form.DisplayMachineFeed(6.5);
            Assert.Equal(25, progress.Value, 9);
            form.DisplayMachineFeed(0);
            Assert.Equal(0, progress.Value);
            Assert.Contains("000.0", progress.Text);
        });

    [Fact]
    public void FeedAndJogUseTheDesignerRangesAndDefaults()
        => Run("G1 X10 F60", (form, _) =>
        {
            var slider = (sSlider)typeof(MainForm).GetField("R_FeedOverride",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            var link = (NT.LinkManager.RealLink)typeof(MainForm).GetField("velocityFactorLink",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            var jog = (sSlider)typeof(MainForm).GetField("R_JogFeedOverride",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            Assert.Equal((0, 120, 100, 10), (slider.Minimum, slider.Maximum, slider.Value, slider.Step));
            Assert.Equal((5, 100, 100, 5), (jog.Minimum, jog.Maximum, jog.Value, jog.Step));
            Assert.Equal(100, form.generalLinks.JogFeedPercentLink.Value);
            Assert.Null(form.generalLinks.JogFeedPercentLink.Group);
            form.generalLinks.JogFeedPercentLink.Value = 0;
            Assert.Equal(5, form.generalLinks.JogFeedPercentLink.Value);
            Assert.Equal(1, form.controller.ToolPaths[0].VelocityFactor);
            link.Value = 0;
            Assert.Equal(0, form.controller.ToolPaths[0].VelocityFactor);
            link.Value = 130;
            Assert.Equal(1.2, form.controller.ToolPaths[0].VelocityFactor);
            Assert.Throws<ArgumentOutOfRangeException>(() => form.controller.ToolPaths[0].VelocityFactor = -1);
        });

    [Fact]
    public void SelectedStartShowsWholeProgramTimingBeforeRun()
        => Run("G1 X10 F60\nG1 X20\nG1 X30", (form, _) =>
        {
            var path = form.controller.MotionController.ToolPaths[0];
            long total = (long)Math.Ceiling(path.EstimatedDurationMilliseconds / 1000.0);
            long elapsed = path.GetEstimatedCumulativeTimeMilliseconds(2) / 1000L;
            Assert.True(elapsed > 0 && elapsed < total);
            var progress = (sProgressBar)typeof(MainForm).GetField("R_ProgramProgress",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            form.gCodeInterpreter.MarkCompletedBefore(3);
            Assert.Contains("Elapsed: " + GCodeInterpreter.FormatDuration(elapsed), progress.Text);
            Assert.Contains("Remaining ≈ " + GCodeInterpreter.FormatDuration(total - elapsed), progress.Text);
            Assert.Contains("Total ≈ " + GCodeInterpreter.FormatDuration(total), progress.Text);
            path.VelocityFactor = .5;
            form.gCodeInterpreter.RefreshExecutionTiming();
            Assert.Contains("Total ≈ " + GCodeInterpreter.FormatDuration(total), progress.Text);
            form.gCodeInterpreter.MarkCompletedBefore(1);
            Assert.Contains("Elapsed: 00:00:00", progress.Text);
        });

    [Theory]
    [InlineData("M30")]
    [InlineData("M2")]
    [InlineData("M5")]
    [InlineData("G1 X30")]
    public void CoalescedFinalBlockAndCompletionSelectTheReportedSourceLine(string finalLine)
        => Run("G1 X10 F60\nG1 X20\n" + finalLine + "\n(comment after execution)", (form, _) =>
        {
            var view = (sGCodeView)typeof(MainForm).GetField("R_GCodeView",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            var path = form.controller.ToolPaths[0];
            long generation = path.CoordinateResetGeneration;
            int block = path.CurrentBlock;
            form.gCodeInterpreter.UpdateExecution(2);
            Assert.Equal(1, view.SelectedLineIndex);
            // The UI never observes block 3 while running; it arrives with completion.
            form.gCodeInterpreter.UpdateExecution(3, completed: true);
            Assert.Equal(2, view.SelectedLineIndex);
            Assert.Equal(-1, view.CurrentLineIndex);
            Assert.Equal(view.LineCount - 1, view.DoneIndex);
            Assert.Equal(generation, path.CoordinateResetGeneration);
            Assert.Equal(block, path.CurrentBlock);
            // Repeated completion must not undo subsequent operator inspection.
            view.SelectedLineIndex = 0;
            form.gCodeInterpreter.UpdateExecution(3, completed: true);
            Assert.Equal(0, view.SelectedLineIndex);
            form.gCodeInterpreter.UpdateExecution(0);
            Assert.Equal(-1, view.CurrentLineIndex);
            Assert.Equal(-1, view.DoneIndex);
            Assert.Equal(0, view.SelectedLineIndex);
            form.gCodeInterpreter.UpdateExecution(2);
            Assert.Equal(1, view.CurrentLineIndex);
            Assert.Equal(1, view.SelectedLineIndex);
            Assert.Equal(0, view.DoneIndex);
        });

    [Fact]
    public void ProgramInformationAndFinalLoadingPercentArePublished()
        => Run("G54 G0 X10 Y0 Z5\nT1 M6\nG1 X20 Y5 Z0 F100\nT2 M6\nG55 G1 X30 Y-5\nG0 X1000 Y1000 Z1000",
            (form, scene) =>
            {
                Assert.Contains("X: 20.0 mm", scene.StatusBarText);
                Assert.Contains("Y: 10.0 mm", scene.StatusBarText);
                Assert.Contains("Z: 5.0 mm", scene.StatusBarText);
                Assert.Contains("Tools: 2 | References: 2", scene.StatusBarText);
                var progress = (sProgressBar)typeof(MainForm).GetField("R_ProgramProgress",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                Assert.Equal(0, progress.Value);
                form.gCodeInterpreter.UpdateExecution(0);
                Assert.Equal(0, progress.Value);
                Assert.Contains("Elapsed: 00:00:00", progress.Text);
                Assert.Contains("Remaining ≈", progress.Text);
                form.gCodeInterpreter.UpdateExecution(form.gCodeInterpreter.LastSourceBlock, completed: true);
                Assert.Equal(100, progress.Value);
                Assert.Contains("Remaining ≈ 00:00:00", progress.Text);
                form.gCodeInterpreter.RefreshExecutionTiming();
                Assert.Contains("Remaining ≈ 00:00:00", progress.Text);
            }, requireFinalProgress: true);

    [Theory]
    [InlineData("G0 X100", "X: —", "References: 1")]
    [InlineData("G1 X10 F100\nG54 G1 X20", "X: 20.0 mm", "References: 1")]
    public void EmptyCuttingBoundsAndInitialReferenceAreReportedCorrectly(string source, string dimensions, string references)
        => Run(source, (_, scene) =>
        {
            Assert.Contains(dimensions, scene.StatusBarText);
            Assert.Contains(references, scene.StatusBarText);
        });

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(90061, "25:01:01")]
    public void DurationDoesNotWrapAtOneDay(long seconds, string expected)
        => Assert.Equal(expected, GCodeInterpreter.FormatDuration(seconds));

    [Theory]
    [InlineData("G0 X1\nG1 X2 F100", false)]
    [InlineData("G55 G0 X1\nG1 X2 F100", true)]
    public void SingleReferenceRetainsPreparedPathAndScene(string source, bool explicitReference)
        => Run(source, (form, scene) =>
        {
            var interpreter = form.gCodeInterpreter;
            var program = interpreter.Compilation;
            var geometry = interpreter.Geometry;
            var map = interpreter.PreviewMap;
            var preview = Assert.Single(scene.Scene.Root.Children.OfType<S3DPath>());
            int row = explicitReference ? 1 : 0;
            form.referenceLinks.SetValue(row, 0, 10);
            Assert.False(interpreter.IsLoading);
            Assert.Same(program, interpreter.Compilation);
            Assert.Same(geometry, interpreter.Geometry);
            Assert.Same(map, interpreter.PreviewMap);
            Assert.Same(preview, Assert.Single(scene.Scene.Root.Children.OfType<S3DPath>()));
            Assert.Equal(10, interpreter.GetLogicalReference(0));
            Assert.Equal(10f, preview.Transform.M41);
            Assert.Equal(12, geometry!.GetPoint(geometry.PointCount - 1)[0] + interpreter.GetLogicalReference(0));
            Assert.Equal(1200, interpreter.Compilation!.Document.SourceEncoding!.Value.CodePage);
            form.referenceLinks.CurrentReference.Value = 2;
            Assert.Same(geometry, interpreter.Geometry);
            Assert.Equal(explicitReference ? 10 : 0, interpreter.GetLogicalReference(0));
        });

    [Theory]
    [InlineData("G54 G0 X1\nG55 G1 X2 F100")]
    [InlineData("G54 G0 X1\nG53 G0 X2")]
    public void RelevantComplexReferenceChangesReplaceGeometry(string source)
        => Run(source, (form, _) =>
        {
            var interpreter = form.gCodeInterpreter;
            var geometry = interpreter.Geometry;
            form.referenceLinks.SetValue(5, 0, 10);
            Assert.False(interpreter.IsLoading);
            Assert.Same(geometry, interpreter.Geometry);
            form.referenceLinks.SetValue(0, 0, 10);
            Assert.True(Pump(interpreter.PendingLoad));
            Assert.NotSame(geometry, interpreter.Geometry);
            var updated = interpreter.Geometry!;
            Assert.Equal(2, updated.GetPoint(updated.PointCount - 1)[0] + interpreter.GetLogicalReference(0));
        });

    [Theory]
    [InlineData(NT.Controller.ConnectionState.Offline)]
    [InlineData(NT.Controller.ConnectionState.Online)]
    [InlineData(NT.Controller.ConnectionState.Failed)]
    public void ClearToolPathShortcutRemovesRowsAndPreviewRegardlessOfConnection(NT.Controller.ConnectionState connection)
        => Run("G21\nG90\nG1 X10 F60", (form, scene) =>
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var core = form.controller.MotionController;
            var state = core.GetType().GetField("connectionState", fields)!;
            state.SetValue(core, connection);
            try
            {
                Assert.True(form.CanClearMainToolPath);
                Assert.NotNull(form.gCodeInterpreter.Geometry);
                Assert.Single(scene.Scene.Root.Children.OfType<S3DPath>());
                Assert.Contains(form.keyboardLinks.Commands, c => ReferenceEquals(c, form.ClearToolPathLink));
                var shortcut = (TextLink)form.keyboardLinks.KeyboardEnabled.Group!.Members["ClearToolPath"];
                Assert.True(NTKeyboardShortcut.TryNormalize(shortcut.Value, out _, out Keys keys, out _));
                Assert.Equal(Keys.Control | Keys.Shift | Keys.Alt | Keys.C, keys);
                var manager = typeof(KeyboardLinks).GetField("shortcutManager", fields)!.GetValue(form.keyboardLinks)!;
                var bindings = (System.Collections.IEnumerable)manager.GetType().GetField("bindings", fields)!.GetValue(manager)!;
                var binding = Assert.Single(bindings.Cast<object>().Where(b =>
                    (bool)b.GetType().GetMethod("Matches", fields)!.Invoke(b, [keys])!));
                binding.GetType().GetMethod("Press", fields)!.Invoke(binding, [keys]);
                Assert.Null(form.gCodeInterpreter.Compilation);
                Assert.Null(form.gCodeInterpreter.Geometry);
                Assert.Null(form.gCodeInterpreter.PreviewMap);
                Assert.Empty(scene.Scene.Root.Children.OfType<S3DPath>());
                Assert.Equal(0, core.ToolPaths[0].BlockCount);
                Assert.False(core.ToolPaths[0].HasBlocksAvailableForRun);
                Assert.False(form.HasPendingProgramStart);
                form.ClearMainToolPath(); // Empty clear is harmless.
            }
            finally { state.SetValue(core, NT.Controller.ConnectionState.Offline); }
        });

    [Fact]
    public void ClearToolPathRejectsPendingRunProgramWaitAndAxisMotionWithoutLosingPreview()
        => Run("G21\nG90\nG1 X10 F60", (form, scene) =>
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var core = form.controller.MotionController;
            object memory = core.GetType().GetField("sharedMemory", fields)!.GetValue(core)!;
            var view = (System.IO.MemoryMappedFiles.MemoryMappedViewAccessor)memory.GetType().GetProperty("Accessor", fields)!.GetValue(memory)!;
            long Constant(string type, string name) => Convert.ToInt64(core.GetType().Assembly.GetType("NT.Controller." + type)!.GetField(name)!.GetRawConstantValue());
            long pathBase = Constant("ToolPathSharedMemoryLayout", "ToolPathRegisterBase");
            long status = 8 * (pathBase + Constant("ToolPathSharedMemoryLayout", "StatusSlot"));
            long command = 8 * (pathBase + Constant("ToolPathSharedMemoryLayout", "CommandSlot"));
            long axis = 8 * (Constant("AxisSharedMemoryLayout", "AxisRegisterBase") + Constant("AxisSharedMemoryLayout", "StatusSlot"));
            var geometry = form.gCodeInterpreter.Geometry;
            void Reject(long address, long value)
            {
                long previous = view.ReadInt64(address);
                try
                {
                    view.Write(address, value);
                    Assert.False(form.CanClearMainToolPath);
                    form.ClearMainToolPath();
                    Assert.Same(geometry, form.gCodeInterpreter.Geometry);
                    Assert.Single(scene.Scene.Root.Children.OfType<S3DPath>());
                }
                finally { view.Write(address, previous); }
            }
            Reject(command, Constant("ToolPathCommandBits", "Run"));
            Reject(status, Constant("ToolPathStatusBits", "Running"));
            Reject(status, Constant("ToolPathStatusBits", "Stopping"));
            Reject(status, Constant("ToolPathStatusBits", "PausedForProgram"));
            Reject(axis, Constant("AxisStatusBits", "Jogging"));
            Reject(axis, Constant("AxisStatusBits", "Homing"));
            Assert.True((bool)core.PLC.GetType().GetMethod("DownloadCompiledPrograms", fields)!.Invoke(core.PLC, [memory])!);
            var m100 = core.PLC.Programs["M100"];
            var offset = memory.GetType().GetMethod("PlcProgramHeaderCellOffset",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
            long header = Convert.ToInt64(offset.Invoke(null,
                [m100.RuntimeIndex, (int)Constant("NTSharedMemory", "PlcStatusSlot")]));
            Reject(header, Constant("PLC.PLCProgramStatusBits", "Loaded") | Constant("PLC.PLCProgramStatusBits", "Running"));
            Assert.True(form.CanClearMainToolPath);
        });

    private static bool Pump(Task<bool> task)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            ((TestContext)SynchronizationContext.Current!).Drain();
            Thread.Sleep(1);
        }
        Assert.True(task.IsCompleted, "Program preparation timed out.");
        return task.GetAwaiter().GetResult();
    }

    private static void Run(string source, Action<MainForm, s3DView> check, bool requireFinalProgress = false)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string id = Guid.NewGuid().ToString("N");
            string registryPath = @"Software\Radonix\Tests\ReferencePreview-" + id;
            string directory = Path.Combine(Path.GetTempPath(), "RouterReference-" + id);
            try
            {
                Directory.CreateDirectory(directory);
                string file = Path.Combine(directory, "program.nc");
                File.WriteAllText(file, source, System.Text.Encoding.Unicode);
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using var form = new MainForm();
                using (var axes = settings.CreateSubKey(Initialize.InterfaceName + "\\Controller\\Axes")) axes.SetValue("YG", "True");
                form.InitializeRuntime(settings, connectOnStartup: false);
                if (source.StartsWith("G55", StringComparison.Ordinal))
                    form.referenceLinks.SetValue(1, 0, 4);
                // Create a UI dispatcher without opening the operator window or startup dialogs.
                SynchronizationContext.SetSynchronizationContext(new TestContext());
                var scene = typeof(MainForm).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(field => field.FieldType == typeof(s3DView))
                    .Select(field => (s3DView)field.GetValue(form)!).Single();
                bool finalProgressObserved = false;
                var progress = (sProgressBar)typeof(MainForm).GetField("R_ProgramProgress",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                progress.ValueChanged += (_, _) =>
                {
                    if (form.gCodeInterpreter.IsLoading && progress.Value == 100)
                        finalProgressObserved = true;
                };
                Assert.True(Pump(form.gCodeInterpreter.OpenFileAsync(file)));
                if (requireFinalProgress) Assert.True(finalProgressObserved, "Loading must display 100% before publishing the scene.");
                check(form, scene);
                ((TestContext)SynchronizationContext.Current!).Drain();
                form.gCodeInterpreter.Dispose();
            }
            catch (Exception error) { failure = error; }
            finally
            {
                Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "Reference preview check timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class TestContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new();
        public override void Post(SendOrPostCallback callback, object? state) => queue.Enqueue((callback, state));
        internal void Drain()
        {
            while (queue.TryDequeue(out var work)) work.Callback(work.State);
        }
    }
}

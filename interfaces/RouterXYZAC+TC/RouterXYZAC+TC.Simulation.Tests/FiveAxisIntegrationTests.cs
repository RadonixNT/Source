using Microsoft.Win32;
using NT.LinkManager;
using NT_Interpreter.Interpreters.RouterXYZACTC;
using NT_Interpreter.Shared.Router;
using NT_Interpreter.Shared.Router.Controller;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace RouterXYZACTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class FiveAxisIntegrationTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void ProfileHasIndependentRotariesAndCompleteGeneratedHome()
        => Run((form, _) =>
        {
            Assert.Equal("RouterXYZAC+TC", Initialize.InterfaceName);
            Assert.Equal("RouterXYZAC+TC", typeof(MainForm).Assembly.GetName().Name);
            Assert.Equal(new[] { "X", "Y", "Z", "A", "C" }, form.controller.Axes.Select(a => a.Name));
            foreach (var path in form.controller.ToolPaths)
                Assert.Equal("XYZAC", string.Concat(path.Axes.Select(a => a.Name)));
            var axes = form.baseGroup.Groups["Controller"].Groups["Axes"];
            Assert.False(axes.Members.ContainsKey("AG"));
            Assert.False(axes.Members.ContainsKey("CG"));
            foreach (string rotary in new[] { "A", "C" })
            {
                Assert.Equal("degree", axes.Groups[rotary].ReadText("Unit"));
                Assert.Contains(form.controllerLinks.AxisJogCommands, command => command.AxisName == rotary);
                Assert.Contains("HomeSensor" + rotary, form.controller.PLC.Programs["Home" + rotary].Source);
                Assert.Contains("AXIS_HOME(" + rotary, form.controller.PLC.Programs[Initialize.HomePlcProgramName].Source);
            }
            Assert.True(form.controllerLinks.ValidatePlcSettings(), form.controller.PLC.LastError);
        });

    [Fact]
    public void AngularReferencesParkAndToolStationsPersistIndependently()
        => Run((form, settings) =>
        {
            form.referenceLinks.SetValue(0, 3, 25);
            form.referenceLinks.SetValue(0, 4, 270);
            ((RealLink)form.baseGroup.Groups["General"].Groups["A"].Members["ParkPosition"]).Value = -45;
            ((RealLink)form.baseGroup.Groups["General"].Groups["C"].Members["ParkPosition"]).Value = 180;
            form.toolChangerLinks.Tools[0].PositionALink.Value = 30;
            form.toolChangerLinks.Tools[0].PositionCLink.Value = 90;
            form.toolChangerLinks.Tools[1].PositionALink.Value = -30;
            form.toolChangerLinks.Tools[1].PositionCLink.Value = 270;
            var (_, context) = Capture(form);
            Assert.Equal(new RouterPosition(0, 0, 0, A: 25, C: 270), context.G28Position);
            Assert.Equal(new RouterPosition(0, 0, 0, A: -45, C: 180), context.G30Position);
            Assert.Equal(new double[] { 0, 0, 0, 25, 270 }, form.controller.ToolPaths[0].Reference.ToArray());
            form.baseGroup.SaveRegistry();
            form.Dispose();
            using var reloaded = new MainForm();
            reloaded.InitializeRuntime(settings, connectOnStartup: false);
            Assert.Equal(25, reloaded.referenceLinks.GetValue(0, 3));
            Assert.Equal(270, reloaded.referenceLinks.GetValue(0, 4));
            Assert.Equal(30, reloaded.toolChangerLinks.Tools[0].PositionA);
            Assert.Equal(90, reloaded.toolChangerLinks.Tools[0].PositionC);
            Assert.Equal(-30, reloaded.toolChangerLinks.Tools[1].PositionA);
            Assert.Equal(270, reloaded.toolChangerLinks.Tools[1].PositionC);
            Assert.Equal(-45, reloaded.generalLinks.GetParkPosition("A"));
        });

    [Theory]
    [InlineData("G1 A30 C90 F600", 30, 90)]
    [InlineData("G1 X10 A-20 C270 F600", -20, 270)]
    [InlineData("G1 C0.00001 F60", 0, 0.00001)]
    public void CompilationAndPreparationRetainEveryRotaryCoordinate(string text, double a, double c)
        => Run((form, _) =>
        {
            var (compiler, context) = Capture(form);
            var compilation = compiler.Compile(text, context);
            Assert.True(compilation.Success, string.Join("\n", compilation.Diagnostics.Select(d => d.Message)));
            Assert.Equal(a, compilation.Program!.Commands.Last().Motion.End.A, 10);
            Assert.Equal(c, compilation.Program.Commands.Last().Motion.End.C, 10);
            var builder = (RouterToolPathBuilder)typeof(GCodeInterpreter).GetField("builder", Hidden)!.GetValue(form.gCodeInterpreter)!;
            double minimum = builder.Path.MinimumLength;
            builder.Build(compilation.Program);
            Assert.Equal(minimum, builder.Path.MinimumLength);
            Assert.True(builder.Path.BlockCount > 0);
            var rows = (List<double>)typeof(NT.Controller.ToolPath).GetField("buffer", Hidden)!.GetValue(builder.Path)!;
            int stride = builder.Path.Axes.Length + 4;
            Assert.Equal(a, rows[rows.Count - stride + 3], 10);
            Assert.Equal(c, rows[rows.Count - stride + 4], 10);
        });

    [Fact]
    public void ReferenceTranslationIncludesACWithoutPuttingCInB()
    {
        var compiler = new RouterCompiler();
        var origins = new RouterPosition[6];
        origins[0] = new(1, 2, 3, A: 10, C: 20);
        var result = compiler.Compile("G1 A30 C90 F600", new(origins[0], 100, origins));
        Assert.True(result.Success);
        origins[0] = new(4, 5, 6, A: -15, C: 270);
        Assert.Equal(origins[0], GCodeInterpreter.GetReferenceTranslation(result.Program!, 0, 0, origins));
    }

    [Fact]
    public void HostRoutesKeepSmallRotaryClearanceLegsAndRestoreSettings()
        => Run((form, _) =>
        {
            var path = form.controller.ToolPaths[2];
            path.MinimumLength = 1;
            path.Begin(2);
            AxisLayout.AddStoppedLine(path, 1, new double[5], [0, 0, 0, 0.005, 0], 10);
            AxisLayout.AddStoppedLine(path, 2, [0, 0, 0, 0.005, 0], [0, 0, -10, 0.005, 0], 10);
            path.End();
            Assert.Equal(1, path.MinimumLength);
            Assert.Equal(2, path.BlockCount);
            Assert.Equal(50, AxisLayout.PathVelocity(path.Axes, new double[5], [3, 0, 0, 4, 0], 30), 10);
            Assert.Equal(30, AxisLayout.PathVelocity(path.Axes, new double[5], [0, 0, 0, 3, 4], 30), 10);
        });

    [Fact]
    public void RotaryPresentationAndJogPagesUseFiveAxisRuntime()
        => Run((form, settings) =>
        {
            T Field<T>(string name) => (T)typeof(MainForm).GetField(name, Hidden)!.GetValue(form)!;
            Assert.Equal(new[] { "X", "Y", "Z", "A", "C" },
                Field<NT.AxisWrapper[]>("positionAxes").Select(axis => axis.Name));
            Assert.Equal(5, Field<double[]>("lastCommandPositions").Length);
            Assert.Equal(5, Field<double[]>("lastWorkPositions").Length);
            var format = typeof(MainForm).GetMethod("FormatAxisPosition", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (string axis in new[] { "A", "C" })
            {
                Assert.EndsWith(" °", (string)format.Invoke(null, [axis, -25.5])!);
                Assert.Equal(axis + "+", Field<NT.Tools.sButton>("R_JogP" + axis).Text);
                Assert.Equal(axis + "-", Field<NT.Tools.sButton>("R_JogN" + axis).Text);
                Assert.Equal(axis, Field<NT.Tools.sSignal>("R_" + axis + "Axis").Text);
            }
            Assert.EndsWith(" mm", (string)format.Invoke(null, ["X", 25.5])!);
            var pages = Field<NT.Tools.sPagePanel>("R_JogPagePanel");
            Assert.Equal(2, pages.PageCount);
            var click = typeof(System.Windows.Forms.Control).GetMethod("OnClick", Hidden)!;
            click.Invoke(Field<NT.Tools.sButton>("R_GotoJogProperties"), [EventArgs.Empty]);
            UI.Update();
            Assert.Equal(1, pages.SelectedPage);
            click.Invoke(Field<NT.Tools.sButton>("R_GotoJog"), [EventArgs.Empty]);
            UI.Update();
            Assert.Equal(0, pages.SelectedPage);
            click.Invoke(Field<NT.Tools.sButton>("R_GotoJogProperties"), [EventArgs.Empty]);
            UI.Update();
            Assert.Equal(1, pages.SelectedPage);
            Assert.False(form.baseGroup.Groups["General"].Members.ContainsKey("JogPage"));
            form.baseGroup.SaveRegistry();
            form.Dispose();
            // Even a legacy saved selection must not determine the startup page.
            using var general = settings.CreateSubKey(Initialize.InterfaceName + @"\General");
            general.SetValue("JogPage", "1");
            using var reopened = new MainForm();
            reopened.InitializeRuntime(settings, connectOnStartup: false);
            var reopenedPages = (NT.Tools.sPagePanel)typeof(MainForm).GetField("R_JogPagePanel", Hidden)!.GetValue(reopened)!;
            Assert.Equal(0, reopenedPages.SelectedPage);
        });

    [Fact]
    public void JogPageRequestsRefreshAndEveryAxisControlUsesItsOwnLink()
        => Run((form, _) =>
        {
            T Field<T>(string name) => (T)typeof(MainForm).GetField(name, Hidden)!.GetValue(form)!;
            foreach (string axis in Initialize.MainAxes)
            {
                int index = form.controller.GetAxisByName(axis).Index;
                var commands = form.controllerLinks.AxisJogCommands.Single(c => c.AxisName == axis);
                void Bound(string controlName, Link expected)
                {
                    Assert.True(UI.TryGetLink(Field<System.Windows.Forms.Control>(controlName), out var actual));
                    Assert.Same(expected, actual);
                }
                Bound("R_JogN" + axis, commands.Negative);
                Bound("R_JogP" + axis, commands.Positive);
                Bound("R_GoTo" + axis, commands.GoToPosition);
                Bound("R_JogVelocity" + axis, form.generalLinks.GetJogVelocityLink(index));
                Bound("R_Step" + axis, form.generalLinks.GetJogStepLink(index));
                Bound("R_Position" + axis, form.generalLinks.GetJogPositionLink(index));
                Assert.False(commands.StepPositive.TryExecute());
                Assert.False(commands.StepNegative.TryExecute());
            }
            Assert.False(form.controllerLinks.HomeALink.TryExecute());
            Assert.False(form.controllerLinks.HomeCLink.TryExecute());
            var pending = typeof(MainForm).GetField("runtimeUiRefreshPending", Hidden)!;
            pending.SetValue(form, 0);
            Field<NT.Tools.sPagePanel>("R_JogPagePanel").SelectedPage = 1;
            Assert.Equal(1, (int)pending.GetValue(form)!);
            pending.SetValue(form, 0);
            Field<NT.Tools.sPagePanel>("R_JogPagePanel").SelectedPage = 0;
            Assert.Equal(1, (int)pending.GetValue(form)!);
        });

    [Theory]
    [InlineData("A", "Positive")]
    [InlineData("A", "Negative")]
    [InlineData("C", "Positive")]
    [InlineData("C", "Negative")]
    public void RotaryHomeSimulationFollowsConfiguredSearchDirection(string name, string direction)
        => Run((form, _) =>
        {
            var axis = form.controller.GetAxisByName(name);
            var group = form.baseGroup.Groups["Controller"].Groups["Axes"].Groups[name];
            ((TextLink)group.Members["HomeDirection"]).Value = direction;
            var sensor = form.CreateHomeSwitchDefinition(axis);
            Assert.Equal(name, sensor.AxisName);
            Assert.Equal("HomeSensor" + name, sensor.InputSignal);
            Assert.Equal(axis.ActualPosition + (direction == "Positive" ? 2 : -2), sensor.TriggerPosition);
            Assert.Equal(direction == "Positive" ? Simulation.PositionSwitchSide.AtOrAbove : Simulation.PositionSwitchSide.AtOrBelow, sensor.ActiveSide);
            Assert.Contains(name + ".HomeDirection", form.controller.PLC.Programs["Home" + name].Source);
            Assert.Contains("HomeSensor" + name, form.controller.PLC.Programs["Home" + name].Source);
        });

    [Fact]
    public void StepJogIsSessionOnlyAndIgnoresLegacySavedMode()
        => Run((form, settings) =>
        {
            Assert.False(form.generalLinks.JogModeLink.Value);
            Assert.False(form.baseGroup.Groups["General"].Members.ContainsKey("JogMode"));
            form.generalLinks.JogModeLink.Value = true;
            form.baseGroup.SaveRegistry();
            form.Dispose();
            using var general = settings.CreateSubKey(Initialize.InterfaceName + @"\General");
            general.SetValue("JogMode", "True");
            using var reopened = new MainForm();
            reopened.InitializeRuntime(settings, connectOnStartup: false);
            Assert.False(reopened.generalLinks.JogModeLink.Value);
            foreach (string axis in Initialize.MainAxes)
            {
                var signal = (NT.Tools.sSignal)typeof(MainForm).GetField("R_" + axis + "Axis", Hidden)!.GetValue(reopened)!;
                Assert.Equal(axis, signal.Text);
            }
        });

    [Fact]
    public void LivePointerUsesCOnlyWithoutChangingXYZOrPhysicalHead()
        => Run((form, _) =>
        {
            var view = (NT.Tools.s3DView)typeof(MainForm).GetField("R_ProgramView3D", Hidden)!.GetValue(form)!;
            var positions = (double[])typeof(MainForm).GetField("lastCommandPositions", Hidden)!.GetValue(form)!;
            var update = typeof(MainForm).GetMethod("UpdateMachinePositionPose", Hidden)!;
            positions[0] = 10; positions[1] = 20; positions[2] = -30;
            positions[3] = 90; positions[4] = 0;
            update.Invoke(form, null);
            Assert.True(view.DirectionalPositionPointer);
            var tilted = OpenTK.Mathematics.Vector3.TransformVector(-OpenTK.Mathematics.Vector3.UnitZ,
                OpenTK.Mathematics.Matrix4.CreateFromQuaternion(view.PositionPointerOrientation));
            Assert.True((tilted + OpenTK.Mathematics.Vector3.UnitZ).Length < 0.00001f);
            positions[4] = 90;
            update.Invoke(form, null);
            var rotated = OpenTK.Mathematics.Vector3.TransformVector(OpenTK.Mathematics.Vector3.UnitX,
                OpenTK.Mathematics.Matrix4.CreateFromQuaternion(view.PositionPointerOrientation));
            Assert.True((rotated - OpenTK.Mathematics.Vector3.UnitY).Length < 0.00001f);
            var orientation = view.PositionPointerOrientation;
            positions[3] = -45; update.Invoke(form, null);
            Assert.Equal(orientation, view.PositionPointerOrientation);
            var head = (NT.Tools.S3DHeadModel)typeof(MainForm).GetField("machineHeadModel", Hidden)!.GetValue(form)!;
            Assert.NotEqual(head.EvaluateOrientation(90, 90), head.EvaluateOrientation(90, -45));
            positions[3] = 90;
            Assert.Equal(new double[] { 10, 20, -30, 90, 90 }, positions);
            int sceneChanges = 0;
            view.Scene.Changed += (_, _) => sceneChanges++;
            update.Invoke(form, null);
            Assert.Equal(0, sceneChanges);
        });

    [Fact]
    public void HostUsesDedicatedToolChangeInterpreterAndDirectAngularUnits()
        => Run((form, _) =>
        {
            var (compiler, context) = Capture(form);
            Assert.IsType<RouterCompiler>(compiler);
            Assert.IsType<NT_Interpreter.Interpreters.RouterXYZACTC.Controller.RouterExecutionSession>(
                typeof(GCodeInterpreter).GetField("execution", Hidden)!.GetValue(form.gCodeInterpreter));
            var result = compiler.Compile("G20 G90 G1 X1 A30 C370 F60", context);
            Assert.True(result.Success);
            var end = result.Program!.Commands.Last().Motion.End;
            Assert.Equal(25.4, end.X, 8);
            Assert.Equal(30, end.A);
            Assert.Equal(370, end.C);
            Assert.False(compiler.Compile("G1 B30 F600", context).Success);
            Assert.False(compiler.Compile("G43.4", context).Success);
            var tools = compiler.Compile("T2 M6\nG1 A30 C90 F600", context);
            Assert.True(tools.Success, string.Join("\n", tools.Diagnostics.Select(d => d.Message)));
        });

    private static (RouterCompiler, RouterMachineContext) Capture(MainForm form)
        => ((RouterCompiler, RouterMachineContext))typeof(GCodeInterpreter).GetMethod("CaptureContext", Hidden)!.Invoke(form.gCodeInterpreter, null)!;

    private static void Run(Action<MainForm, RegistryKey> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string name = @"Software\Radonix\Tests\XYZAC-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZACTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(name);
                using var controller = settings.CreateSubKey(Initialize.InterfaceName + @"\Controller");
                controller.SetValue("Mode", "Simulation");
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                action(form, settings);
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(name, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "XYZAC offline verification timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}



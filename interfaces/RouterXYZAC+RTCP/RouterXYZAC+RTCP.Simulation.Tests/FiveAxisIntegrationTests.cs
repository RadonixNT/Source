using Microsoft.Win32;
using NT.LinkManager;
using NT.Tools;
using NT_Interpreter.Interpreters.RouterXYZAC;
using NT_Interpreter.Shared.Router;
using NT_Interpreter.Shared.Router.Controller;
using OpenTK.Mathematics;
using System.Reflection;
using System.IO.MemoryMappedFiles;
using System.Runtime.ExceptionServices;

namespace RouterXYZACRTCP.Simulation.Tests;

[Collection("Router UI")]
public sealed class FiveAxisIntegrationTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void RtcpSetZeroUsesTheRotatedWorkTcpAndBothOffsets()
        => Run((form, _) =>
        {
            var calibration = EnableRtcpCalibration(form);
            ((TextLink)calibration.Members["MachineType"]).Value = "TableTable";
            form.ApplyControllerProperties(rebuildProgram: false);
            var path = form.controller.ToolPaths[0];
            path.SetLiveOffset(1, 3);

            WithRtcpMode(form, (view, constant) =>
            {
                long AxisPosition(string axis) => 8 * (constant("AxisSharedMemoryLayout", "AxisRegisterBase")
                    + form.controller.GetAxisByName(axis).Index * constant("AxisSharedMemoryLayout", "AxisRegisterStride")
                    + constant("AxisSharedMemoryLayout", "CommandPositionSlot"));
                (long Address, double Value)[] registers =
                [
                    (AxisPosition("X"), 30), (AxisPosition("Y"), 20), (AxisPosition("Z"), -10),
                    (AxisPosition("C"), 90), (constant("NTSharedMemory", "ToolPathOffsetAppliedOffset") + 8, 2)
                ];
                double[] original = registers.Select(register => view.ReadDouble(register.Address)).ToArray();
                try
                {
                    foreach (var (address, value) in registers) view.Write(address, value);
                    var setZero = typeof(MainForm).GetMethod("SetCurrentAxisReference", Hidden)!;
                    setZero.Invoke(form, [1]);
                    // C=90 maps machine (30,20) to work (20,-30). The origin
                    // also subtracts the applied offset (2) and live offset (3).
                    Assert.Equal(-35, form.referenceLinks.GetValue(0, 1), 8);
                    Assert.Equal(-35, path.Reference[1], 8);

                    long modeAddress = 8 * constant("SharedRegisterIndex", "RtcpModeState");
                    view.Write(modeAddress, 3L); // RTCP with a pending handover.
                    setZero.Invoke(form, [0]);
                    Assert.Equal(0, path.Reference[0]);
                }
                finally
                {
                    for (int i = 0; i < registers.Length; i++) view.Write(registers[i].Address, original[i]);
                    path.SetLiveOffset(1, 0);
                }
            });
        });

    [Theory]
    [InlineData("G53 G0 Z-20")]
    [InlineData("G28")]
    [InlineData("G30")]
    public void RtcpRejectsMachineCoordinateProgramsBeforeSelectingOrStarting(string machineMove)
        => Run((form, _) =>
        {
            var (compiler, context) = Capture(form);
            var program = compiler.Compile("G1 Z-5 F60\n" + machineMove, context).Program!;
            var execution = (NT_Interpreter.Interpreters.RouterXYZAC.Controller.RouterExecutionSession)
                typeof(GCodeInterpreter).GetField("execution", Hidden)!.GetValue(form.gCodeInterpreter)!;
            execution.Load(program);
            Assert.True(form.gCodeInterpreter.CanRunInCurrentCoordinateMode);
            long generation = form.controller.ToolPaths[0].CoordinateResetGeneration;

            WithRtcpMode(form, (_, _) =>
            {
                Assert.False(form.gCodeInterpreter.CanRunInCurrentCoordinateMode);
                var error = Assert.Throws<InvalidOperationException>(() =>
                    form.gCodeInterpreter.Start(RouterStartRequest.FromSourceBlock(2, false),
                        () => throw new Exception("Unexpected Run."),
                        () => throw new Exception("Unexpected positioning.")));
                Assert.Contains("Line 2", error.Message);
                Assert.Contains("Normal mode", error.Message);
                Assert.Equal(generation, form.controller.ToolPaths[0].CoordinateResetGeneration);
            });
        });

    [Fact]
    public void RtcpPreparationReportsMachineTravelUsingToolLengthAndPathOffsets()
        => Run((form, _) =>
        {
            EnableRtcpCalibration(form);
            form.ApplyControllerProperties(rebuildProgram: false);
            form.referenceLinks.SetValue(0, 2, -100);
            var (compiler, context) = Capture(form);
            var builder = (RouterToolPathBuilder)typeof(GCodeInterpreter).GetField("builder", Hidden)!.GetValue(form.gCodeInterpreter)!;
            var valid = compiler.Compile("G1 Z-450 F60", context).Program!;
            var invalid = compiler.Compile("G1 Z50 F60", context).Program!;
            Assert.True(builder.Build(valid).IsOutsideConfiguredTravel);
            builder.Path.SetLiveOffset(2, 5);

            WithRtcpMode(form, (view, constant) =>
            {
                long offsetAddress = constant("NTSharedMemory", "ToolPathOffsetAppliedOffset") + 16;
                double original = view.ReadDouble(offsetAddress);
                try
                {
                    view.Write(offsetAddress, 10.0);
                    Assert.False(builder.Build(valid).IsOutsideConfiguredTravel);
                    var warning = Assert.Single(builder.Build(invalid).Diagnostics,
                        item => item.Code == RouterDiagnosticCode.OutsideConfiguredTravel);
                    Assert.Equal(1, warning.Source.LineNumber);
                    Assert.Contains("15 mm in RTCP machine coordinates", warning.Message);
                }
                finally
                {
                    view.Write(offsetAddress, original);
                    builder.Path.SetLiveOffset(2, 0);
                }
            });
        });

    [Fact]
    public void DisablingRtcpClearsRejectedToolLengthConfirmation()
        => Run((form, _) =>
        {
            var calibration = EnableRtcpCalibration(form);
            form.ApplyControllerProperties(rebuildProgram: false);
            var source = typeof(MainForm).GetField("rtcpToolLengthSource", Hidden)!.GetValue(form)!;
            source.GetType().GetMethod("Reject", Hidden)!.Invoke(source, ["Test rejection."]);
            Assert.True(form.ToolLengthBlocksProgram);

            ((LogicLink)calibration.Members["Configured"]).Value = false;
            form.ApplyControllerProperties(rebuildProgram: false);
            Assert.Null(form.controller.MotionController.RTCP.Geometry);
            Assert.False(form.ToolLengthBlocksProgram);
            Assert.Equal(50, ((RealLink)calibration.Members["ToolLength"]).Value);
        });

    private static LinkGroup EnableRtcpCalibration(MainForm form)
    {
        var calibration = form.baseGroup.Groups["Controller"].Groups["RTCP"];
        ((LogicLink)calibration.Members["Configured"]).Value = true;
        ((RealLink)calibration.Members["ToolLength"]).Value = 50;
        return calibration;
    }

    private static void WithRtcpMode(MainForm form, Action<MemoryMappedViewAccessor, Func<string, string, long>> action)
    {
        var core = form.controller.MotionController;
        var state = core.GetType().GetField("connectionState", Hidden)!;
        object memory = core.GetType().GetField("sharedMemory", Hidden)!.GetValue(core)!;
        var view = (MemoryMappedViewAccessor)memory.GetType().GetProperty("Accessor", Hidden)!.GetValue(memory)!;
        long Constant(string type, string field) => Convert.ToInt64(core.GetType().Assembly
            .GetType("NT.Controller." + type)!.GetField(field)!.GetRawConstantValue());
        long modeAddress = 8 * Constant("SharedRegisterIndex", "RtcpModeState");
        var operational = typeof(MainForm).GetField("ethercatOperational", Hidden)!;
        try
        {
            state.SetValue(core, NT.Controller.ConnectionState.Online);
            operational.SetValue(form, true);
            view.Write(modeAddress, 1L);
            action(view, Constant);
        }
        finally
        {
            view.Write(modeAddress, 0L);
            operational.SetValue(form, false);
            state.SetValue(core, NT.Controller.ConnectionState.Offline);
        }
    }

    [Fact]
    public void ProfileHasIndependentRotariesAndCompleteGeneratedHome()
        => Run((form, _) =>
        {
            Assert.Equal("RouterXYZAC+RTCP", Initialize.InterfaceName);
            Assert.Equal("RouterXYZAC+RTCP", typeof(MainForm).Assembly.GetName().Name);
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
    public void AngularReferencesAndParkPersistIndependently()
        => Run((form, settings) =>
        {
            form.referenceLinks.SetValue(0, 3, 25);
            form.referenceLinks.SetValue(0, 4, 270);
            ((RealLink)form.baseGroup.Groups["General"].Groups["A"].Members["ParkPosition"]).Value = -45;
            ((RealLink)form.baseGroup.Groups["General"].Groups["C"].Members["ParkPosition"]).Value = 180;
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

    [Theory]
    [InlineData(NT.Controller.RtcpMachineType.HeadHead, 0.5, -0.5, 0.7071067811865476)]
    [InlineData(NT.Controller.RtcpMachineType.HeadTable, 0, 0, 1)]
    [InlineData(NT.Controller.RtcpMachineType.TableTable, 0, 0, 1)]
    public void AxialReturnEntryUsesOnlyJointsThatCarryTheSpindle(NT.Controller.RtcpMachineType layout,
        double x, double y, double z)
    {
        var geometry = new NT.Controller.RtcpMachineGeometry
        {
            MachineType = layout,
            LinearAxes = new(0, 1, 2),
            FirstRotary = new(4, default, new(0, 0, 1), 10),
            SecondRotary = new(3, default, new(1, 0, 0), -5),
            SpindleGaugePointMm = default,
            ToolDirection = new(0, 0, -1),
            ToolLengthMm = 60
        };
        // Outer C rotates 45 degrees and inner A rotates 45 degrees from their calibration pose.
        var outward = RtcpReturnApproach.OutwardDirection(geometry, 415, 40);
        Assert.Equal(x, outward.X, 12);
        Assert.Equal(y, outward.Y, 12);
        Assert.Equal(z, outward.Z, 12);
        var target = new NT.Controller.RtcpVector3(160, 210, -165);
        var entry = RtcpReturnApproach.PointAtDistance(target, outward, 25);
        var tolerance = new NT.Controller.RtcpVector3(0.001, 0.001, 0.001);
        Assert.True(RtcpReturnApproach.IsOnAxis(entry, target, outward, tolerance));
        Assert.False(RtcpReturnApproach.IsOnAxis(entry with { X = entry.X + 0.5 }, target, outward, tolerance));
        Assert.Equal(25.0, Math.Sqrt(Math.Pow(entry.X - target.X, 2) +
            Math.Pow(entry.Y - target.Y, 2) + Math.Pow(entry.Z - target.Z, 2)), 12);
    }

    [Theory]
    [InlineData(5, 10, 0.6, 0, 16, 0, 5)]
    [InlineData(30, 10, 0.6, 0, 16, 0, 10)]
    [InlineData(30, 10, -0.5, 4, 20, 1, 14)]
    [InlineData(30, 10, 0.5, 4, 14, 4, 0)]
    [InlineData(30, 10, 0, 4, 10, 0, 30)]
    public void AxialReturnDistanceUsesTheOutwardPlaneOfEachAxis(double requested, double target,
        double direction, double minimum, double maximum, double couplingOffset, double expected)
    {
        double result = RtcpReturnApproach.LimitDistance(requested, target, direction, minimum, maximum, couplingOffset);
        Assert.Equal(expected, result, 12);
        Assert.InRange(Math.FusedMultiplyAdd(direction, result, target) + couplingOffset, minimum, maximum);
    }

    [Fact]
    public void LongerToolMovesTheRetainedTargetAndCanShortenTheAxialEntry()
        => Run((form, settings) =>
        {
            LinkGroup calibration = form.baseGroup.Groups["Controller"].Groups["RTCP"];
            ((LogicLink)calibration.Members["Configured"]).Value = true;
            var toolLength = (RealLink)calibration.Members["ToolLength"];
            var distance = (RealLink)calibration.Members["AxialReturnDistance"];
            const double zMaximum = -20;
            distance.Value = 45;
            var rtcp = form.controller.MotionController.RTCP;
            var work = new NT.Controller.RtcpVector3(20, 50, -80);

            toolLength.Value = 30;
            form.ApplyControllerProperties(rebuildProgram: false);
            Assert.True(rtcp.TryGetMachinePosition(work, 0, 45, out var originalTarget));
            var outward = RtcpReturnApproach.OutwardDirection(rtcp.Geometry!, 0, 45);
            Assert.Equal(distance.Value, RtcpReturnApproach.LimitDistance(distance.Value, originalTarget.Z,
                outward.Z, -200, zMaximum, 0));

            toolLength.Value = 50;
            form.ApplyControllerProperties(rebuildProgram: false);
            Assert.True(rtcp.TryGetMachinePosition(work, 0, 45, out var newTarget));
            Assert.Equal(originalTarget.Y + outward.Y * 20, newTarget.Y, 10);
            Assert.Equal(originalTarget.Z + outward.Z * 20, newTarget.Z, 10);
            double clipped = RtcpReturnApproach.LimitDistance(distance.Value, newTarget.Z, outward.Z, -200, zMaximum, 0);
            var entry = RtcpReturnApproach.PointAtDistance(newTarget, outward, clipped);
            Assert.True(clipped > 0 && clipped < distance.Value);
            Assert.Equal(zMaximum, entry.Z, 10);
            Assert.True(RtcpReturnApproach.IsOnAxis(entry, newTarget, outward, new(1e-9, 1e-9, 1e-9)));

            form.baseGroup.SaveRegistry();
            form.Dispose();
            using var reopened = new MainForm();
            reopened.InitializeRuntime(settings, connectOnStartup: false);
            var saved = Assert.IsType<RealLink>(reopened.baseGroup.Groups["Controller"].Groups["RTCP"].Members["AxialReturnDistance"]);
            Assert.Equal(45, saved.Value);
        });

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
            Assert.Equal(3, pages.PageCount);
            var click = typeof(System.Windows.Forms.Control).GetMethod("OnClick", Hidden)!;
            click.Invoke(Field<NT.Tools.sButton>("R_GotoJogProperties"), [EventArgs.Empty]);
            UI.Update();
            Assert.Equal(1, pages.SelectedPage);
            click.Invoke(Field<NT.Tools.sButton>("R_GotoJogPositions"), [EventArgs.Empty]);
            UI.Update();
            Assert.Equal(2, pages.SelectedPage);
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
    public void HeadPagesNavigateWithSessionLinkAndRefreshTheirControls()
        => Run((form, settings) =>
        {
            T Field<T>(string name) => (T)typeof(MainForm).GetField(name, Hidden)!.GetValue(form)!;
            var pages = Field<sPagePanel>("R_HeadPagePanel");
            var spindlePage = Field<sPage>("R_HeadSpindlePage");
            var rtcpPage = Field<sPage>("R_HeadRtcpPage");
            Assert.Equal(2, pages.PageCount);
            var designerDefault = pages.SelectedPageControl;
            Assert.True(designerDefault == rtcpPage || designerDefault == spindlePage);
            Assert.Same(spindlePage, Field<sSlider>("R_SpindleSpeed").Parent);
            Assert.Same(rtcpPage, Field<sTextBox>("R_ToolLength").Parent);
            Assert.Same(rtcpPage, Field<sToggle>("R_RtcpEnabled").Parent);
            Assert.Equal(NT.Localization.NTLocalization.Get(RouterXYZACRTCPTexts.RtcpPart),
                Field<sLabel>("R_RtcpPageTitle").Text);
            Assert.Equal(NT.Localization.NTLocalization.Get(RouterXYZACRTCPTexts.SpindleProperties),
                Field<sLabel>("R_SpindlePageTitle").Text);
            Assert.True(UI.TryGetLink(pages, out var binding));
            var pageLink = Assert.IsType<IntegerLink>(binding);
            Assert.Null(pageLink.Group);
            var pendingRefresh = typeof(MainForm).GetField("runtimeUiRefreshPending", Hidden)!;
            var click = typeof(System.Windows.Forms.Control).GetMethod("OnClick", Hidden)!;

            void Open(string button, sPage page)
            {
                pendingRefresh.SetValue(form, 0);
                click.Invoke(Field<sButton>(button), [EventArgs.Empty]);
                Assert.Same(page, pages.SelectedPageControl);
                Assert.Equal(pages.Pages.IndexOf(page), pageLink.Value);
                Assert.Equal(1, (int)pendingRefresh.GetValue(form)!);
            }

            if (designerDefault == rtcpPage)
                Open("R_GotoSpindlePage", spindlePage);
            Open("R_GotoRtcpPage", rtcpPage);
            Open("R_GotoSpindlePage", spindlePage);
            if (designerDefault == spindlePage)
                Open("R_GotoRtcpPage", rtcpPage);
            form.baseGroup.SaveRegistry();
            form.Dispose();
            using var reopened = new MainForm();
            var reopenedPages = (sPagePanel)typeof(MainForm).GetField("R_HeadPagePanel", Hidden)!.GetValue(reopened)!;
            sPage? reopenedDesignerDefault = reopenedPages.SelectedPageControl;
            reopened.InitializeRuntime(settings, connectOnStartup: false);
            Assert.Same(reopenedDesignerDefault, reopenedPages.SelectedPageControl);
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
            LinkGroup general = form.baseGroup.Groups["General"];
            foreach (string axis in new[] { "AP", "CP", "ZT" })
            {
                foreach (var (controlPrefix, linkName) in new[]
                         { ("R_JogVelocity", "JogVelocity"), ("R_Step", "JogStep"), ("R_Position", "JogPosition") })
                {
                    Assert.True(UI.TryGetLink(Field<System.Windows.Forms.Control>(controlPrefix + axis), out var actual));
                    Assert.Same(general.Groups[axis].Members[linkName], actual);
                }
                foreach (string prefix in new[] { "R_JogN", "R_JogP", "R_GoTo" })
                {
                    Assert.True(UI.TryGetLink(Field<System.Windows.Forms.Control>(prefix + axis), out var actual));
                    var command = Assert.IsType<CommandLink>(actual);
                    if (prefix == "R_GoTo") Assert.False(command.TryExecute());
                }
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

    [Fact]
    public void RtcpCalibrationUsesLogicalSlotsAndRejectsInvalidDirection()
        => Run((form, _) =>
        {
            var rtcp = form.controller.MotionController.RTCP;
            Assert.Null(rtcp.Geometry);
            LinkGroup calibration = form.baseGroup.Groups["Controller"].Groups["RTCP"];
            var enabled = (LogicLink)calibration.Members["Configured"];
            Assert.Equal("Enabled", enabled.DisplayName);
            enabled.Value = true;
            ((TextLink)calibration.Members["FirstJoint"]).Value = "A";
            ((RealLink)calibration.Members["ToolLength"]).Value = 42;
            Assert.True(form.ValidateSettings());
            form.ApplyControllerProperties(rebuildProgram: false);
            var geometry = Assert.IsType<NT.Controller.RtcpMachineGeometry>(rtcp.Geometry);
            Assert.Equal(form.controller.GetAxisByName("A").Index, geometry.FirstRotary.AxisIndex);
            Assert.Equal(form.controller.GetAxisByName("C").Index, geometry.SecondRotary.AxisIndex);
            Assert.Equal(42, geometry.ToolLengthMm);
            ((RealLink)calibration.Groups["ToolDirection"].Members["Z"]).Value = 0;
            Assert.False(form.ValidateSettings());
            Assert.Same(geometry, rtcp.Geometry);
        });

    [Fact]
    public void FixedTipSamplePlacesWorkZeroAtTheCalibratedTableDepth()
        => Run((form, _) =>
        {
            var rtcp = form.controller.MotionController.RTCP;
            rtcp.Geometry = new NT.Controller.RtcpMachineGeometry
            {
                MachineType = NT.Controller.RtcpMachineType.HeadHead,
                LinearAxes = new(0, 1, 2),
                FirstRotary = new(4, new(), new(0, 0, 1)),
                SecondRotary = new(3, new(), new(1, 0, 0)),
                SpindleGaugePointMm = new(0, 0, -40),
                TableDatumMm = new(0, 0, -100),
                ToolDirection = new(0, 0, -1),
                ToolLengthMm = 45
            };
            var reference = new RouterPosition(500, 500, -200);
            Assert.True(WorkCoordinatePresentation.TryRtcp(rtcp, rtcp.Geometry, reference, 150.065, 45, out var frame));
            Assert.Equal(new Vector3d(500, 500, -300), frame.PreviewToScene.Row3.Xyz);
            var readout = frame.GetWorkPosition(new(529.993, 552.086, -239.896));
            Assert.InRange(readout.Length, 0, 0.0005);
        });

    [Theory]
    [InlineData(NT.Controller.RtcpMachineType.HeadHead, 18, 20, -300, 3, 4, 5)]
    [InlineData(NT.Controller.RtcpMachineType.HeadTable, 10, 28, -300, -4, 3, 5)]
    [InlineData(NT.Controller.RtcpMachineType.TableTable, -290, 28, 0, 5, 3, 4)]
    public void RtcpPreviewAndReadoutUseTheSameWorkpieceRotation(
        NT.Controller.RtcpMachineType layout, double x, double y, double z, double dx, double dy, double dz)
        => Run((form, _) =>
        {
            var rtcp = form.controller.MotionController.RTCP;
            var a = new NT.Controller.RtcpRotaryAxis(3, new(0, 20, 0), new(1, 0, 0), 10);
            var c = new NT.Controller.RtcpRotaryAxis(4, new(10, 20, 0), new(0, 0, 1), 20);
            var (first, second, firstAngle, secondAngle) = layout == NT.Controller.RtcpMachineType.HeadTable
                ? (a, c, 100d, 110d) : (c, a, 110d, 100d);
            var geometry = new NT.Controller.RtcpMachineGeometry
            {
                MachineType = layout,
                LinearAxes = new(0, 1, 2),
                FirstRotary = first,
                SecondRotary = second,
                SpindleGaugePointMm = new(0, 0, -40),
                TableDatumMm = new(0, 0, -100),
                ToolDirection = new(0, 0, -1),
                ToolLengthMm = 45
            };
            rtcp.Geometry = geometry;
            var reference = new RouterPosition(18, 20, -200);
            var local = new Vector3d(3, 4, 5);
            Assert.True(WorkCoordinatePresentation.TryRtcp(rtcp, geometry, reference, firstAngle, secondAngle, out var frame));
            Assert.InRange((frame.PreviewToScene.Row3.Xyz - new Vector3d(x, y, z)).Length, 0, 1e-9);
            var world = Vector3d.TransformPosition(local, frame.PreviewToScene);
            Assert.InRange((world - new Vector3d(x + dx, y + dy, z + dz)).Length, 0, 1e-9);

            Assert.True(rtcp.TryGetMachinePosition(new(21, 24, -195), firstAngle, secondAngle, out var carriage));
            var work = frame.GetWorkPosition(new(carriage.X, carriage.Y, carriage.Z));
            Assert.InRange((work - local).Length, 0, 1e-9);
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
    public void LivePointerFollowsPhysicalTipWithCOnlyOrientation()
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
            AssertPointerAtTip(view);
            var head = (NT.Tools.S3DHeadModel)typeof(MainForm).GetField("machineHeadModel", Hidden)!.GetValue(form)!;
            Assert.NotEqual(head.EvaluateOrientation(90, 90), head.EvaluateOrientation(90, -45));
            positions[3] = 90;
            Assert.Equal(new double[] { 10, 20, -30, 90, 90 }, positions);
            int sceneChanges = 0;
            view.Scene.Changed += (_, _) => sceneChanges++;
            update.Invoke(form, null);
            Assert.Equal(0, sceneChanges);
        });

    [Theory]
    [InlineData("HeadHead", "C", S3DRotaryChannel.C, S3DRotaryChannel.A)]
    [InlineData("HeadHead", "A", S3DRotaryChannel.A, S3DRotaryChannel.C)]
    [InlineData("HeadTable", "A", S3DRotaryChannel.A, null)]
    [InlineData("TableTable", "C", null, null)]
    public void RoundSpindleUsesTheAcceptedHeadJointsAndToolLength(
        string layout, string first, S3DRotaryChannel? outer, S3DRotaryChannel? inner)
        => Run((form, _) =>
        {
            var view = (s3DView)typeof(MainForm).GetField("R_ProgramView3D", Hidden)!.GetValue(form)!;
            Assert.Equal(S3DHeadStyle.RoundSpindle, view.HeadDefinition!.Style);
            var calibration = form.baseGroup.Groups["Controller"].Groups["RTCP"];
            ((LogicLink)calibration.Members["Configured"]).Value = true;
            ((TextLink)calibration.Members["MachineType"]).Value = layout;
            ((TextLink)calibration.Members["FirstJoint"]).Value = first;
            ((RealLink)calibration.Groups["A"].Members["ReferenceAngle"]).Value = 10;
            ((RealLink)calibration.Groups["C"].Members["ReferenceAngle"]).Value = -15;
            ((RealLink)calibration.Groups["GaugePoint"].Members["X"]).Value = 2;
            ((RealLink)calibration.Groups["GaugePoint"].Members["Y"]).Value = 3;
            ((RealLink)calibration.Groups["GaugePoint"].Members["Z"]).Value = -40;
            var length = (RealLink)calibration.Members["ToolLength"];
            length.Value = 45;
            var refresh = typeof(MainForm).GetMethod("RefreshRtcpControls", Hidden)!;
            form.ApplyControllerProperties(rebuildProgram: false);
            refresh.Invoke(form, null);
            var definition = Assert.IsType<S3DHeadDefinition>(view.HeadDefinition);
            Assert.Equal(outer, definition.Outer?.Channel);
            Assert.Equal(inner, definition.Inner?.Channel);

            var positions = (double[])typeof(MainForm).GetField("lastCommandPositions", Hidden)!.GetValue(form)!;
            new double[] { 100, 200, -30, 10, -15 }.CopyTo(positions, 0);
            typeof(MainForm).GetMethod("UpdateMachinePositionPose", Hidden)!.Invoke(form, null);
            Assert.Equal(new Vector3(102, 203, -115), view.HeadTipPosition);
            AssertPointerAtTip(view);
            refresh.Invoke(form, null);
            Assert.Same(definition, view.HeadDefinition);

            length.Value = 55;
            form.ApplyControllerProperties(rebuildProgram: false);
            refresh.Invoke(form, null);
            Assert.Equal(new Vector3(102, 203, -125), view.HeadTipPosition);
            AssertPointerAtTip(view);
        });

    private static void AssertPointerAtTip(s3DView view)
    {
        var options = typeof(s3DView).GetField("options", Hidden)!.GetValue(view)!;
        var pointer = (Vector3)options.GetType().GetField("Pointer", Hidden)!.GetValue(options)!;
        Assert.Equal(view.HeadTipPosition!.Value, pointer);
    }

    [Fact]
    public void HostUsesDirectAngularUnitsWithoutToolExchange()
        => Run((form, _) =>
        {
            var (compiler, context) = Capture(form);
            Assert.IsType<RouterCompiler>(compiler);
            Assert.IsType<NT_Interpreter.Interpreters.RouterXYZAC.Controller.RouterExecutionSession>(
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
            Assert.DoesNotContain(tools.Program!.Commands, command => command.Kind == RouterCommandKind.MachineFunction);
            Assert.NotEmpty(tools.Diagnostics);
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
                RouterXYZACRTCPTexts.Register();
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

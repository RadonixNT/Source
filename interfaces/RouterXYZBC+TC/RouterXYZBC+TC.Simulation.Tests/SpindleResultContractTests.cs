using NT_Interpreter.Shared.Router.Controller;
using NT_Interpreter.Shared.Router;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller;
using NT_Interpreter.Interpreters.RouterXYZBCTC;
using NT_Interpreter.Interpreters.RouterXYZBCTC.Controller;

namespace RouterXYZBCTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class SpindleResultContractTests
{
    [Fact]
    public void SpeedRowsDoNotAddAnExactStopAndUnloadUsesIndependentSettings()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registry = @"Software\Radonix\Tests\ResultContract-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZBCTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registry);
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                Assert.Equal(100, form.routerLinks.UnloaderVelocity.Value);
                Assert.Equal(1000, form.routerLinks.UnloaderUpTime.Value);
                Assert.Equal(1000, form.routerLinks.UnloaderDownTime.Value);
                const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
                Assert.Same(form.controller.ToolPaths[1], typeof(MaterialTransferMacro).GetField("path", hidden)!.GetValue(form.unloadMacro));
                var builder = (RouterToolPathBuilder)typeof(GCodeInterpreter).GetField("builder", hidden)!.GetValue(form.gCodeInterpreter)!;
                var catalog = typeof(GCodeInterpreter).GetMethod("CreateAuxiliaryCatalog", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [form.controller.MotionController])!;
                var definitions = (RouterAuxiliaryDefinition[])catalog.GetType().GetField("Item1")!.GetValue(catalog)!;
                var context = new RouterMachineContext(default, 100, new RouterPosition[6], 10);
                // Use the interface's real auxiliary definitions and bindings.
                var program = new RouterCompiler(new RouterProfile(auxiliaryDefinitions: definitions))
                    .Compile("G1 X100 F600\nS12000\nS18000\nG1 X200", context);
                Assert.True(program.Success);
                builder.Build(program.Program!);
                var rows = (List<double>)typeof(ToolPath).GetField("buffer", hidden)!.GetValue(builder.Path)!;
                int stride = builder.Path.Axes.Length + 4;
                int speedRows = 0;
                for (int row = 1; row <= builder.Path.BlockCount; row++)
                {
                    ulong operation = BitConverter.DoubleToUInt64Bits(rows[(row + 1) * stride - 1]) >> 32;
                    if (operation == 0) continue;
                    speedRows++;
                    Assert.Equal(1UL, operation >> 16); // PLC, no Wait flag.
                    Assert.NotEqual(0, rows[(row + 1) * stride - 2]);
                }
                Assert.Equal(2, speedRows);
                foreach (string name in new[] { "M3", "M4", "M5", "M2", "StopSpindle", "Background", "MeasureToolLength" })
                    Assert.DoesNotContain("SpindleSpeed :=", Initialize.GetPlcSource(name));
                Assert.True(form.controllerLinks.ValidatePlcSettings(), form.controller.PLC.LastError);
            }
            catch (Exception e) { failure = e; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registry, false); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void ResultDescriptionsAreScopedToProgramAndUnknownFailuresRemainErrors()
    {
        Assert.NotEqual(PlcResultCatalog.Code("M100", 3), PlcResultCatalog.Code("RaiseUnloader", 3));
        Assert.Contains("aligned", PlcResultCatalog.Describe("M100", 3));
        Assert.Contains("up sensor", PlcResultCatalog.Describe("RaiseUnloader", 3));
        Assert.Contains("Undefined", PlcResultCatalog.Describe("Custom", 77));
        Assert.Equal(1, MaterialTransferMacro.NextActivation(long.MaxValue));
    }

    [Fact]
    public void UnloadRejectsStopEventsFromAnotherPathExecutionOrCursor()
    {
        var constructor = typeof(ToolPathStoppedEventArgs).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var stop = (ToolPathStoppedEventArgs)constructor.Invoke([1, 12L, 23L, 34L, 6, ToolPathStopReason.Completed, null]);
        Assert.True(MaterialTransferMacro.IsMatchingStop(stop, 12, 23, 34));
        Assert.False(MaterialTransferMacro.IsMatchingStop(stop, 11, 23, 34));
        Assert.False(MaterialTransferMacro.IsMatchingStop(stop, 12, 22, 34));
        Assert.False(MaterialTransferMacro.IsMatchingStop(stop, 12, 23, 33));
        var toolChange = (ToolPathStoppedEventArgs)constructor.Invoke([2, 12L, 23L, 34L, 6, ToolPathStopReason.Completed, null]);
        Assert.False(MaterialTransferMacro.IsMatchingStop(toolChange, 12, 23, 34));
    }
}

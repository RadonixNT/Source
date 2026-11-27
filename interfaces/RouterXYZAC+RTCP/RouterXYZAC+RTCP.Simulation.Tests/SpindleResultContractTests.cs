using NT_Interpreter.Shared.Router.Controller;
using NT_Interpreter.Shared.Router;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller;
using NT_Interpreter.Interpreters.RouterXYZAC;
using NT_Interpreter.Interpreters.RouterXYZAC.Controller;

namespace RouterXYZACRTCP.Simulation.Tests;

[Collection("Router UI")]
public sealed class SpindleResultContractTests
{
    [Fact]
    public void SpeedRowsDoNotAddAnExactStop()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registry = @"Software\Radonix\Tests\ResultContract-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZACRTCPTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registry);
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
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
                foreach (string name in new[] { "M3", "M4", "M5", "M2", "Background" })
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
        Assert.NotEqual(PlcResultCatalog.Code(Initialize.RunTP0PlcProgramName, 3), PlcResultCatalog.Code("M5", 3));
        Assert.Contains("aligned", PlcResultCatalog.Describe(Initialize.RunTP0PlcProgramName, 3));
        Assert.Contains("Spindle stop feedback", PlcResultCatalog.Describe("M5", 3));
        Assert.Contains("Undefined", PlcResultCatalog.Describe("Custom", 77));
    }
}
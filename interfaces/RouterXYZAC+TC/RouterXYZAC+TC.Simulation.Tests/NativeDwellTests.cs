using NT_Interpreter.Shared.Router.Controller;
using NT_Interpreter.Shared.Router;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller;
using NT.LinkManager;
using NT_Interpreter.Interpreters.RouterXYZACTC;
using NT_Interpreter.Interpreters.RouterXYZACTC.Controller;

namespace RouterXYZACTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class NativeDwellTests
{
    [Theory]
    [InlineData(82)]
    [InlineData(89)]
    public void G4IsInternalAndCyclesPrepareWithoutPlcRegistration(int code)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterDwell-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZACTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                Assert.False(form.controller.PLC.Programs.TryGetValue("G4", out _));
                const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
                var links = (Dictionary<string, TextLink>)typeof(ControllerLinks)
                    .GetField("plcSourceLinks", hidden)!.GetValue(form.controllerLinks)!;
                Assert.False(links.ContainsKey("G4"));
                Assert.DoesNotContain(typeof(MainForm).Assembly.GetManifestResourceNames(),
                    name => name.EndsWith(".G4.plc", StringComparison.OrdinalIgnoreCase));
                var builder = (RouterToolPathBuilder)typeof(GCodeInterpreter)
                    .GetField("builder", hidden)!.GetValue(form.gCodeInterpreter)!;
                var compiled = new RouterCompiler().Compile($"G4 P0.01\nG{code} Z-2 R2 P0.02\nG85 X2",
                    new RouterMachineContext(default, 100, new RouterPosition[6], 10));
                Assert.True(compiled.Success);
                builder.Build(compiled.Program!);
                var rows = (List<double>)typeof(ToolPath).GetField("buffer", hidden)!.GetValue(builder.Path)!;
                int stride = builder.Path.Axes.Length + 4;
                Assert.Equal(2, Enumerable.Range(1, builder.Path.BlockCount).Count(row =>
                    BitConverter.DoubleToUInt64Bits(rows[(row + 1) * stride - 1]) >> 32 == 0x80080000U));
                Assert.True(builder.Path.EstimatedDurationMilliseconds >= 30);
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Offline dwell preparation did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

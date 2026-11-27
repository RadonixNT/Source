using NT_Interpreter.Shared.Router;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT_Interpreter.Interpreters.RouterXYZ;

namespace CutterXYZ.Simulation.Tests;

[Collection("Router UI")]
public sealed class ChipBreakingSettingsTests
{
    [Fact]
    public void CutterUsesInterpreterDefaultWithoutRetractSetting()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterG73-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using (var form = new MainForm())
                {
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    Assert.False(form.cutterLinks.Group.Members.ContainsKey("G73RetractDistance"));
                    var capture = typeof(GCodeInterpreter).GetMethod("CaptureContext", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    var (compiler, context) = ((RouterCompiler, RouterMachineContext))capture.Invoke(form.gCodeInterpreter, null)!;
                    AssertLift(compiler, context, .254);
                }
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Offline G73 settings check did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void AssertLift(RouterCompiler compiler, RouterMachineContext context, double expected)
    {
        var result = compiler.Compile("G99 G73 Z-5 R2 Q3 F600", context);
        Assert.True(result.Success);
        var feeds = result.Program!.Commands.Where(c => c.Kind == RouterCommandKind.LinearMove).ToArray();
        Assert.Equal(expected, feeds[1].Motion.Start.Z - feeds[0].Motion.End.Z, 10);
    }
}

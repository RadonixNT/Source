using NT_Interpreter.Shared.Router;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT_Interpreter.Interpreters.RouterXYZTC;

namespace RouterXYZTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class ChipBreakingSettingsTests
{
    [Fact]
    public void RetractLinkPersistsAndIsCapturedBeforeCompilation()
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
                    var link = form.routerLinks.G73RetractDistance;
                    Assert.Same(link, form.routerLinks.Group.Members["G73RetractDistance"]);
                    Assert.Equal(.254, link.Value);
                    Assert.Equal(.001, link.Minimum);
                    Assert.Equal(1000, link.Maximum);
                    Assert.Throws<ArgumentOutOfRangeException>(() => link.Value = double.NaN);
                    link.Value = .5;
                    var capture = typeof(GCodeInterpreter).GetMethod("CaptureContext", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    var (compiler, context) = ((RouterCompiler, RouterMachineContext))capture.Invoke(form.gCodeInterpreter, null)!;
                    link.Value = .75;
                    AssertLift(compiler, context, .5); // A worker never reads the changed live Link.
                    var (next, nextContext) = ((RouterCompiler, RouterMachineContext))capture.Invoke(form.gCodeInterpreter, null)!;
                    AssertLift(next, nextContext, .75);
                    form.baseGroup.SaveRegistry();
                }
                using (var reloaded = new MainForm())
                {
                    reloaded.InitializeRuntime(settings, connectOnStartup: false);
                    Assert.Equal(.75, reloaded.routerLinks.G73RetractDistance.Value);
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

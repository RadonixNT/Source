using NT_Interpreter.Shared.Router;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT_Interpreter.Interpreters.RouterXYZBCTC;

namespace RouterXYZBCTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class StoredReturnSettingsTests
{
    [Fact]
    public void ExistingReferenceAndParkAreCapturedWithoutDuplicateSettings()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string path = @"Software\Radonix\Tests\RouterReturn-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZBCTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(path);
                using (var form = new MainForm())
                {
                    form.InitializeRuntime(settings, connectOnStartup:false);
                    Assert.False(form.routerLinks.Group.Groups.ContainsKey("G28"));
                    Assert.False(form.routerLinks.Group.Groups.ContainsKey("G30"));
                    foreach (var axis in form.controller.ToolPaths[0].Axes)
                    {
                        axis.MinCourse = -1000;
                        axis.MaxCourse = 1000;
                    }
                    var reference = form.referenceLinks;
                    reference.SetValue(0,0,10);
                    reference.SetValue(0,1,20);
                    reference.SetValue(0,2,30);
                    var general = form.baseGroup.Groups["General"];
                    ((NT.LinkManager.RealLink)general.Groups["Z"].Members["ParkPosition"]).Value = 60;
                    var snapshot = Capture(form);
                    Assert.Equal(new RouterPosition(10,20,30), snapshot.G28Position);
                    Assert.Equal(new RouterPosition(0,0,60), snapshot.G30Position);
                    reference.SetValue(0,2,40);
                    ((NT.LinkManager.RealLink)general.Groups["Z"].Members["ParkPosition"]).Value = 70;
                    Assert.Equal(new RouterPosition(10,20,30), snapshot.G28Position);
                    Assert.Equal(new RouterPosition(0,0,60), snapshot.G30Position);
                    Assert.Equal(new RouterPosition(10,20,40), Capture(form).G28Position);
                    Assert.Equal(new RouterPosition(0,0,70), Capture(form).G30Position);
                    reference.SetValue(1,0,11);
                    reference.SetValue(1,1,22);
                    reference.SetValue(1,2,33);
                    reference.CurrentReference.Value = 1;
                    Assert.Equal(new RouterPosition(11,22,33), Capture(form).G28Position);
                    reference.CurrentReference.Value = 0;
                    var program = new RouterCompiler().Compile("G28\nG30", snapshot).Program!;
                    Assert.Equal(snapshot.G28Position, program.GetCommand(0).Motion.End);
                    Assert.Equal(snapshot.G30Position, program.GetCommand(1).Motion.End);
                    var origins = Enumerable.Range(0,6).Select(snapshot.GetWorkOrigin).ToArray();
                    origins[0] = new(1,0,0);
                    Assert.Null(GCodeInterpreter.GetReferenceTranslation(program,0,0,origins));
                    form.baseGroup.SaveRegistry();
                }
                using var reloaded = new MainForm();
                reloaded.InitializeRuntime(settings, connectOnStartup:false);
                Assert.Equal(new RouterPosition(10,20,40), Capture(reloaded).G28Position);
                Assert.Equal(new RouterPosition(0,0,70), Capture(reloaded).G30Position);
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(path,throwOnMissingSubKey:false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Offline stored-return settings check timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static RouterMachineContext Capture(MainForm form)
    {
        var pair = ((RouterCompiler, RouterMachineContext))typeof(GCodeInterpreter)
            .GetMethod("CaptureContext",BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form.gCodeInterpreter,null)!;
        return pair.Item2;
    }
}

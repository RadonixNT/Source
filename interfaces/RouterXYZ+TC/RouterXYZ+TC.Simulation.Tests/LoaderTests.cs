using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller.PLC;

namespace RouterXYZTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class LoaderTests
{
    [Fact]
    public void LoaderHasIndependentPersistedSettingsMappingsAndCommands()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterLoader-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using (var form = new MainForm())
                {
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    var loader = form.routerLinks.Loader;
                    Assert.Same(loader.Group, form.routerLinks.Group.Groups["Loader"]);
                    Assert.Equal(1000, loader.UpTime.Value);
                    Assert.Equal(1000, loader.DownTime.Value);
                    Assert.Equal(1000, loader.PickDelay.Value);
                    Assert.Equal(1000, loader.ReleaseDelay.Value);
                    Assert.Equal(100, loader.Velocity.Value);
                    foreach (string command in new[] { "Unload", "Load", "ReleaseLoaderVacuum" })
                        Assert.Contains(form.keyboardLinks.Commands, c => c.Name == command);
                    Assert.Same(form.loadMacro.VacuumOffLink, loader.Group.Members["ReleaseLoaderVacuum"]);
                    foreach (string signal in new[] { "LoaderDown", "LoaderVacuum", "LoaderUpSensor", "LoaderDownSensor" })
                        Assert.False(form.controller.PLC.IOMappings[signal].IsPhysicalTarget);
                    form.controller.PLC.IOMappings["LoaderDown"].Target = "DM0.OUT0";
                    form.controller.PLC.IOMappings["LoaderVacuum"].Target = "DM0.OUT1";
                    for (int mask = 0; mask < 4; mask++)
                    {
                        form.controller.PLC.IOMappings["LoaderDownSensor"].Target = (mask & 1) != 0 ? "DM0.IN4" : "";
                        form.controller.PLC.IOMappings["LoaderUpSensor"].Target = (mask & 2) != 0 ? "DM0.IN5" : "";
                        Assert.True(form.controllerLinks.ValidatePlcSettings(), form.controller.PLC.LastError);
                    }
                    loader.DownTime.Value = 234;
                    loader.PickDelay.Value = 567;
                    loader.ReleaseDelay.Value = 789;
                    loader.StartPosition.Value = 120;
                    Assert.Equal(234, ((INTEGER)form.controller.PLC.ApplicationVariables["LoaderDownTime"]).Value);
                    Assert.Equal(567, ((INTEGER)form.controller.PLC.ApplicationVariables["LoaderPickDelay"]).Value);
                    Assert.Equal(789, ((INTEGER)form.controller.PLC.ApplicationVariables["LoaderReleaseDelay"]).Value);
                    Assert.Equal(1000, form.routerLinks.UnloaderDownTime.Value);
                    Assert.Equal(0, form.routerLinks.UnloaderStartPosition.Value);
                    form.baseGroup.SaveRegistry();
                    form.gCodeInterpreter.Dispose();
                }
                using (var form = new MainForm())
                {
                    using var reopened = Registry.CurrentUser.OpenSubKey(registryPath, writable: true)!;
                    form.InitializeRuntime(reopened, connectOnStartup: false);
                    Assert.Equal(567, form.routerLinks.Loader.PickDelay.Value);
                    Assert.Equal(789, form.routerLinks.Loader.ReleaseDelay.Value);
                    Assert.Equal(120, form.routerLinks.Loader.StartPosition.Value);
                    Assert.Equal(1000, form.routerLinks.UnloaderDownTime.Value);
                    form.gCodeInterpreter.Dispose();
                }
            }
            catch (Exception e) { failure = e; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}


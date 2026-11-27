using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller.PLC;

namespace RouterXYZBCTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class FreshSettingsTests
{
    [Fact]
    public void EmptyRegistryLoadsCurrentFactoryProgramsAndSurvivesSaveAndReopen()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string key = @"Software\Radonix\Tests\RouterFresh-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZBCTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(key);
                Assert.Empty(settings.GetValueNames());
                Assert.Empty(settings.GetSubKeyNames());
                for (int opening = 0; opening < 2; opening++)
                {
                    using var form = new MainForm();
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    Assert.False(form.controller.IsConnected);
                    Assert.Equal("Simulation", form.generalLinks.ModeLink.Value);
                    Assert.Equal(100, form.routerLinks.UnloaderVelocity.Value);
                    Assert.Equal(1000, form.routerLinks.UnloaderUpTime.Value);
                    Assert.Equal(1000, form.routerLinks.UnloaderDownTime.Value);
                    Assert.Equal(100, ((REAL)form.controller.PLC.ApplicationVariables["UnloaderVelocity"]).Value);
                    foreach (string name in new[] { "UnloaderDown", "UnloaderUpSensor", "UnloaderDownSensor" })
                        Assert.False(form.controller.PLC.IOMappings[name].IsPhysicalTarget);
                    foreach (string name in new[] { "S", "M3", "M4", "M5", "LowerUnloader", "RaiseUnloader" })
                    {
                        var program = form.controller.PLC.Programs[name];
                        Assert.True(program.Enabled);
                        Assert.False(program.Cyclic);
                        Assert.Equal(Initialize.GetPlcSource(name), program.Source);
                    }
                    Assert.True(form.controllerLinks.ValidatePlcSettings(), form.controller.PLC.LastError);
                    form.baseGroup.SaveRegistry();
                }
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Fresh settings verification did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

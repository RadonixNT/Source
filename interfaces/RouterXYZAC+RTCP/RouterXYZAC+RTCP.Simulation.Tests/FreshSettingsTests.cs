using System.Runtime.ExceptionServices;
using Microsoft.Win32;

namespace RouterXYZACRTCP.Simulation.Tests;

[Collection("Router UI")]
public sealed class FreshSettingsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentFactoryProgramsSurviveSaveAndReopen(bool hasLegacyReturnProgram)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string key = @"Software\Radonix\Tests\RouterFresh-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZACRTCPTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(key);
                Assert.Empty(settings.GetValueNames());
                Assert.Empty(settings.GetSubKeyNames());
                string returnProgramName = Initialize.RunTP0PlcProgramName;
                string returnSource = Initialize.GetPlcSource(returnProgramName);
                string commandPath = $@"{Initialize.InterfaceName}\Controller\PLC\Command Programs";
                if (hasLegacyReturnProgram)
                {
                    returnSource = "// Saved positioning customization\n" + returnSource;
                    using var commands = settings.CreateSubKey(commandPath);
                    commands.SetValue("M100", returnSource);
                }
                for (int opening = 0; opening < 2; opening++)
                {
                    using var form = new MainForm();
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    Assert.False(form.controller.IsConnected);
                    Assert.Equal("Simulation", form.generalLinks.ModeLink.Value);
                    foreach (string name in new[] { "S", "M3", "M4", "M5" })
                    {
                        var program = form.controller.PLC.Programs[name];
                        Assert.True(program.Enabled);
                        Assert.False(program.Cyclic);
                        Assert.Equal(Initialize.GetPlcSource(name), program.Source);
                    }
                    Assert.False(form.controller.PLC.Programs.TryGetValue("M100", out _));
                    Assert.Equal(returnSource, form.controller.PLC.Programs[returnProgramName].Source);
                    Assert.True(form.controllerLinks.ValidatePlcSettings(), form.controller.PLC.LastError);
                    form.baseGroup.SaveRegistry();
                    using var savedCommands = settings.OpenSubKey(commandPath, writable: true);
                    Assert.Equal(returnSource, savedCommands!.GetValue(returnProgramName));
                    if (hasLegacyReturnProgram && opening == 0)
                        savedCommands.SetValue("M100", "// Legacy content must not override RunTP0.");
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

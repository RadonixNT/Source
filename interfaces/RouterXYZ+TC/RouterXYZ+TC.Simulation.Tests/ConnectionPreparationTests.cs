using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using RouterXYZTC;

namespace RouterXYZTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class ConnectionPreparationTests
{
    [Theory]
    [InlineData("Simulation")]
    [InlineData("Normal")]
    public void LockedConnectionPreparationDoesNotCommitReferenceUsingStaleUiPermission(string mode)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterConnection-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                settings.SetValue("Mode", mode);
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                Assert.True(form.CanEditReference);
                // The UI snapshot still permits offline reference edits while
                // the live connection gate has just closed. No native runtime starts.
                typeof(MainForm).GetField("<IsConnectionTransition>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, true);
                try
                {
                    form.ApplyControllerProperties();
                    Assert.False(form.CaptureCommandAvailability().CanEditReference);
                }
                finally
                {
                    typeof(MainForm).GetField("<IsConnectionTransition>k__BackingField",
                        BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, false);
                }
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Offline preparation did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

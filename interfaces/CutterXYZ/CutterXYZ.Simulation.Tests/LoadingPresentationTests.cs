using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Microsoft.Win32;
using NT.Tools;

namespace CutterXYZ.Simulation.Tests;

[Collection("Router UI")]
public sealed class LoadingPresentationTests
{
    [Theory]
    [InlineData(1, 93, 46)]
    [InlineData(1, 100, 50)]
    [InlineData(2, 0, 50)]
    [InlineData(2, 100, 100)]
    [InlineData(3, 0, 100)]
    [InlineData(3, 53, 100)]
    [InlineData(3, 100, 100)]
    public void LoadingPercentUsesOneContinuousWholeLoadScale(int stage, int stagePercent, int expected)
        => Assert.Equal(expected, GCodeInterpreter.GetLoadProgressPercent(stage, stagePercent));

    [Fact]
    public void LoadingBlocksInteractionAndRestoresCursorsAndCommandAccess()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterLoading-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var interpreter = form.gCodeInterpreter;
                var loading = typeof(GCodeInterpreter).GetField("<IsLoading>k__BackingField", flags)!;
                var update = typeof(MainForm).GetMethod("ProgramLoadingStateChanged", flags)!;
                T Control<T>(string name) => (T)typeof(MainForm).GetField(name, flags)!.GetValue(form)!;
                var button = Control<sButton>("R_OpenFile");
                var progress = Control<sProgressBar>("R_ProgramProgress");
                Cursor originalCursor = form.Cursor;
                Cursor originalButtonCursor = button.Cursor;
                bool originalJogReadOnly = Control<sTextBox>("R_JogVelocityX").ReadOnly;
                try
                {
                    loading.SetValue(interpreter, true);
                    typeof(GCodeInterpreter).GetField("loadingFileName", flags)!.SetValue(interpreter, @"H:\Example\sample.nc");
                    typeof(GCodeInterpreter).GetField("loadProgress", flags)!.SetValue(interpreter, 1093);
                    interpreter.RefreshLoadingProgress();
                    update.Invoke(form, null);
                    Assert.Equal(46, progress.Value);
                    Assert.Contains("46%", progress.Text);
                    Assert.Contains(@"Opening G-code: H:\Example\sample.nc", progress.Text);
                    Assert.DoesNotContain("93%", progress.Text);
                    typeof(GCodeInterpreter).GetField("loadProgress", flags)!.SetValue(interpreter, 2000);
                    interpreter.RefreshLoadingProgress();
                    Assert.Equal(50, progress.Value);
                    Assert.DoesNotContain("Opening G-code", progress.Text);
                    Assert.Contains("Preparing ToolPath", progress.Text);
                    typeof(GCodeInterpreter).GetField("loadProgress", flags)!.SetValue(interpreter, 3000);
                    interpreter.RefreshLoadingProgress();
                    Assert.Equal(100, progress.Value);
                    Assert.DoesNotContain("%", progress.Text);
                    Assert.Contains("Preparing preview", progress.Text);
                    Assert.Equal("Cancel", button.Text);
                    Assert.True(button.Enabled);
                    Assert.Equal(Cursors.Default, button.Cursor);
                    Assert.Equal(Cursors.AppStarting, form.Cursor);
                    Assert.True(Control<sTextBox>("R_JogVelocityX").ReadOnly);
                    Assert.False(Control<sButton>("R_Configuration").Enabled);
                    Assert.False(Control<sButton>("R_Exit").Enabled);
                    Assert.False(Control<sButton>("R_PauseProgram").Enabled);
                    var availability = new MachineCommandAvailability(true, false, false, true, false, false);
                    Assert.False(availability.CanJog);
                    Assert.False(availability.CanUseWindowCommands);
                    Assert.False(availability.CanEditReference);
                }
                finally
                {
                    loading.SetValue(interpreter, false);
                    update.Invoke(form, null);
                }
                Assert.Equal(originalCursor, form.Cursor);
                Assert.Equal(originalButtonCursor, button.Cursor);
                Assert.Equal(originalJogReadOnly, Control<sTextBox>("R_JogVelocityX").ReadOnly);
                Assert.True(button.Enabled);
                Assert.NotEqual("Cancel", button.Text);
                Assert.True(Control<sButton>("R_Exit").Enabled);
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Offline loading presentation check did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

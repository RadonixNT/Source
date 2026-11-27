using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.LinkManager;
using NT.Tools.Bindings;

namespace CutterXYZ.Simulation.Tests;

[Collection("Router UI")]
public sealed class KeyboardEnableTests
{
    [Fact]
    public void DisableReleasesHeldCommandsAndPersistsAcrossRestart()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterKeyboard-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var managerField = typeof(KeyboardLinks).GetField("shortcutManager", flags)!;
                using (var form = new MainForm())
                {
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    var keyboard = form.keyboardLinks;
                    var enabled = keyboard.KeyboardEnabled;
                    Assert.True(enabled.Value);
                    Assert.Equal("KeyboardEnabled", enabled.Name);
                    Assert.Equal("Enabled", enabled.DisplayName);
                    Assert.Equal("Keyboard", enabled.Group!.Name);
                    var reportShortcut = Assert.IsType<TextLink>(enabled.Group.Members["OpenReportFile"]);
                    Assert.Equal("Ctrl+Shift+Alt+PrintScreen", reportShortcut.Value);
                    var manager = Assert.IsType<NTKeyboardShortcutManager>(managerField.GetValue(keyboard));
                    using var testGroup = new LinkGroup("Probe", "Probe", null);
                    var command = testGroup.AddCommand("Held", "Held");
                    int begins = 0, ends = 0;
                    command.ActionBegin += (_, _) => begins++;
                    command.ActionEnd += (_, _) => ends++;
                    var shortcut = testGroup.AddText("Shortcut", "Shortcut", "F12");
                    manager.Add(command, shortcut);
                    var bindings = (IList)typeof(NTKeyboardShortcutManager).GetField("bindings", flags)!.GetValue(manager)!;
                    object binding = bindings[bindings.Count - 1]!;
                    binding.GetType().GetMethod("Press", flags)!.Invoke(binding, [System.Windows.Forms.Keys.F12]);
                    Assert.Equal(1, begins);
                    enabled.Value = false;
                    Assert.Equal(1, ends);
                    Assert.Null(managerField.GetValue(keyboard));
                    Assert.Contains(form.generalLinks.OpenFileLink, keyboard.Commands);
                    enabled.Value = true;
                    Assert.NotSame(manager, managerField.GetValue(keyboard));
                    enabled.Value = false;
                    enabled.Flush();
                }
                using (var form = new MainForm())
                {
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    Assert.False(form.keyboardLinks.KeyboardEnabled.Value);
                    Assert.Null(managerField.GetValue(form.keyboardLinks));
                    form.keyboardLinks.KeyboardEnabled.Value = true;
                    Assert.NotNull(managerField.GetValue(form.keyboardLinks));
                }
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Offline keyboard enable check did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.LinkManager;
using NT.Localization;
using NT.ConfigurationManager;
using RouterXYZBCTC;

namespace RouterXYZBCTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class TranslationIntegrationTests
{
    [Fact]
    public void ActualInterfaceOutputRetainsTheResolvedSharedLibrariesAndDocumentation()
    {
        string output = typeof(TranslationIntegrationTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "InterfaceOutputDirectory").Value!;
        string packageRoot = typeof(TranslationIntegrationTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "BinaryReferenceRoot").Value!;
        // Project-reference tests resolve packages independently, so their successful UI creation alone
        // cannot detect an AfterBuild runtime copy replacing a DLL in the actual application directory.
        foreach (string name in new[] { "NT-Helper", "NT-LinkManager", "NT-Controller", "NT-ToolsWF",
            "NT-Wrapper", "NT-Diag", "NT-ConfigurationManager", "NT-Interpreter" })
        {
            foreach (string extension in new[] { ".dll", ".xml" })
            {
                string expected = Path.Combine(packageRoot, name + extension);
                string actual = Path.Combine(output, name + extension);
                Assert.True(File.Exists(actual), "Missing deployed dependency: " + actual);
                Assert.Equal(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(expected)),
                    System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(actual)));
            }
        }
    }

    [Fact]
    public void ExistingProviderRefreshesManualCaptionsAndComposesOperatorEditor()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string path = @"Software\Radonix\Tests\Translations-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZBCTCTexts.Register();
                ConfigurationManagerLocalization.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(path);
                using (var keyboard = settings.CreateSubKey(Initialize.InterfaceName + @"\Keyboard"))
                    keyboard.SetValue("OpenSettings", "Ctrl+Shift+F12");
                using (var main = new MainForm())
                {
                    main.InitializeRuntime(settings, connectOnStartup: false);
                    var clamped = main.controller.PLC.IOMappings.Single(mapping => mapping.Name == "SpindleToolClamped");
                    Assert.Equal("Spindle Tool Clamped (S1)", main.controllerLinks.GetSignalDisplayName(clamped));
                    Assert.Equal("SpindleToolClamped", clamped.Name);
                    Assert.Equal("DM1.IN0", clamped.Target);
                    var reset = main.controller.PLC.IOMappings.Single(mapping => mapping.Name == "StopSwitch");
                    Assert.Equal("Reset Switch", main.controllerLinks.GetSignalDisplayName(reset));
                    Assert.Equal("OpenConfiguration", main.generalLinks.OpenConfigurationLink.Name);
                    var keyboardGroup = Field<LinkGroup>(main.keyboardLinks, "keyboardGroup");
                    var configurationShortcut = (TextLink)keyboardGroup.Members["OpenConfiguration"];
                    Assert.Equal("Ctrl+Shift+F12", configurationShortcut.Value);
                    configurationShortcut.Value = "Ctrl+Shift+F11";
                    keyboardGroup.SaveRegistry();
                    var provider = Field<LinkLocalizationOverrideProvider>(main, "localizationOverrides");
                    var run = Field<System.Windows.Forms.Control>(main, "R_RunProgram");
                    provider.SetOverride(NTLocalization.CurrentLanguage, RouterXYZBCTCTexts.Run, "Custom run caption");
                    Assert.Equal("Custom run caption", run.Text);
                    provider.RemoveOverride(NTLocalization.CurrentLanguage, RouterXYZBCTCTexts.Run);
                    Assert.Equal(NTLocalization.GetDefaultText(NTLocalization.CurrentLanguage, RouterXYZBCTCTexts.Run), run.Text);

                    using var dialog = new ConfigurationForm(main.baseGroup) { CurrentPermission = AccessPermission.Public };
                    main.AttachDialogTheme(dialog);
                    Assert.Same(provider, Field<LinkLocalizationOverrideProvider>(dialog, "translationProvider"));
                    Assert.Same(main.generalLinks.UiGroup, Field<LinkGroup>(dialog, "translationUiGroup"));
                    var button = Field<System.Windows.Forms.ToolStripButton>(dialog, "translationsButton");
                    Assert.False(button.Enabled);
                    dialog.CurrentPermission = AccessPermission.Operator;
                    Assert.True(button.Enabled);
                    dialog.Show();
                    System.Windows.Forms.Application.DoEvents();
                    bool translationOpened = false;
                    using (var closeChild = new System.Windows.Forms.Timer { Interval = 50 })
                    {
                        closeChild.Tick += (_, _) =>
                        {
                            var child = System.Windows.Forms.Application.OpenForms.OfType<TranslationEditorForm>()
                                .FirstOrDefault(form => form.Visible && form.Opacity > 0.9);
                            if (child is null) return;
                            translationOpened = true;
                            child.Close();
                        };
                        closeChild.Start();
                        button.PerformClick();
                    }
                    Assert.True(translationOpened, "Toolbar click opens the visible translation editor");
                    dialog.CurrentPermission = AccessPermission.Public;
                    Assert.False(button.Enabled);
                    dialog.Close();
                    Assert.False(main.controller.IsConnected);
                    provider.SetOverride(NTLocalization.CurrentLanguage, RouterXYZBCTCTexts.Configuration, "Operator settings");
                    provider.SaveOverrides();
                    // Dispose the borrowed-control presenter while native test controls are still alive.
                    main.gCodeInterpreter.Dispose();
                }
                using var reopened = new MainForm();
                using var reopenedSettings = Registry.CurrentUser.OpenSubKey(path, writable: true)!;
                reopened.InitializeRuntime(reopenedSettings, connectOnStartup: false);
                Assert.Equal("Ctrl+Shift+F11", ((TextLink)Field<LinkGroup>(reopened.keyboardLinks,
                    "keyboardGroup").Members["OpenConfiguration"]).Value);
                Assert.Equal("Operator settings", Field<System.Windows.Forms.Control>(reopened, "R_Configuration").Text);
                Field<LinkLocalizationOverrideProvider>(reopened, "localizationOverrides")
                    .RemoveOverride(NTLocalization.CurrentLanguage, RouterXYZBCTCTexts.Configuration);
                reopened.gCodeInterpreter.Dispose();
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Offline translation integration completes");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void TransferAndKeyboardCaptionsFollowAllLanguagesWithoutRenamingSettings()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string path = @"Software\Radonix\Tests\LinkTranslations-" + Guid.NewGuid().ToString("N");
            var original = NTLocalization.CurrentLanguage;
            try
            {
                RouterXYZBCTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(path);
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                var clear = (TextLink)form.baseGroup.Groups["Keyboard"].Members["ClearToolPath"];
                string shortcut = clear.Value;
                Assert.Single(form.keyboardLinks.Commands.Where(c => c.Name == "Reset"));
                Assert.DoesNotContain(form.keyboardLinks.Commands, c => c.Name == "Stop Run");
                foreach (NTLanguage language in Enum.GetValues<NTLanguage>())
                {
                    NTLocalization.SetLanguage(language);
                    Assert.Equal(NTLocalization.Get(new NTTextId("Radonix.RouterXYZBCTC.Links.ClearToolPath")), clear.DisplayName);
                    Assert.Equal(clear.DisplayName, form.ClearToolPathLink.DisplayName);
                    Assert.Equal("ClearToolPath", clear.Name);
                    Assert.Equal(shortcut, clear.Value);
                    Assert.Equal("LoaderPickDelay", form.routerLinks.Loader.PickDelay.Name);
                    Assert.Equal(NTLocalization.Get(new NTTextId("Radonix.RouterXYZBCTC.Main.Stop")),
                        form.controllerLinks.ResetLink.DisplayName);
                    Assert.Equal(NTLocalization.Get(new NTTextId("Radonix.RouterXYZBCTC.Main.SetTool")),
                        form.toolChangerLinks.MeasureToolOffsetLink.DisplayName);
                }
                NTLocalization.SetLanguage(original);
                // Deliberately let normal form disposal release the presenter after
                // borrowed controls: this previously crashed the test host.
            }
            catch (Exception error) { failure = error; }
            finally
            {
                NTLocalization.SetLanguage(original);
                Registry.CurrentUser.DeleteSubKeyTree(path, false);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static T Field<T>(object instance, string name) => (T)instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
}


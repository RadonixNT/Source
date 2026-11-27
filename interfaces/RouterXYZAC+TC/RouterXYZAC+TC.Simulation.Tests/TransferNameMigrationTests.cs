using Microsoft.Win32;
using System.Runtime.ExceptionServices;
using NT.LinkManager;

namespace RouterXYZACTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class TransferNameMigrationTests
{
    [Fact]
    public void SourceRewritePreservesCommentsStringsParametersAndCustomLogic()
    {
        const string source = "// Loader UnloaderOn\n(* UnloadUpSensor *)\nLoader := Unloader OR UnloadUpSensor;\nLoaderPickDelay := 173;\nMessage := 'Loader stays';\nCALL('LoaderOn');\nCustomLoader := FALSE;";
        string rewritten = TransferNameMigration.RewriteSource(source);
        Assert.Equal("// Loader UnloaderOn\n(* UnloadUpSensor *)\nLoaderDown := UnloaderDown OR UnloaderUpSensor;\nLoaderPickDelay := 173;\nMessage := 'Loader stays';\nCALL('LowerLoader');\nCustomLoader := FALSE;", rewritten);
        Assert.Equal(rewritten, TransferNameMigration.RewriteSource(rewritten));
    }

    [Fact]
    public void ExistingProfileRestoresMappingsCustomSourcesDelaysAndShortcut()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string path = @"Software\Radonix\Tests\TransferRename-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZACTCTexts.Register();
                using var parent = Registry.CurrentUser.CreateSubKey(path);
                using (var root = parent.CreateSubKey(Initialize.InterfaceName))
                {
                    Set(root, @"Controller\PLC\Mapping\Digital Outputs\Loader", "Output", "DM1.OUT0");
                    Set(root, @"Controller\PLC\Mapping\Digital Outputs\Loader", "Enabled", "False");
                    Set(root, @"Controller\PLC\Mapping\Digital Outputs\Unloader", "Output", "DM1.OUT1");
                    Set(root, @"Controller\PLC\Mapping\Digital Inputs\UnloadUpSensor", "Input", "DM0.IN6");
                    Set(root, @"Controller\PLC\Mapping\Digital Inputs\UnloadUpSensor", "FilterMode", "HighSpeed");
                    Set(root, @"Controller\PLC\Command Programs", "LoaderOn", "// custom lowering\nLoader := TRUE; END_FUNCTION(Result := 0);");
                    Set(root, @"Controller\PLC\Command Programs", "UnloaderOff", "Unloader := FALSE; END_FUNCTION(Result := 0);");
                    Set(root, @"Router\Loader", "LoaderPickDelay", "2345");
                    Set(root, @"Router\Unloader", "UnloaderDownTime", "3456");
                    Set(root, "Keyboard", "LoaderVacuumOff", "Ctrl+F8");
                }
                using (var form = new MainForm())
                {
                    form.InitializeRuntime(parent, connectOnStartup: false);
                    Assert.Equal("DM1.OUT0", form.controller.PLC.IOMappings["LoaderDown"].Target);
                    Assert.False(form.controller.PLC.IOMappings["LoaderDown"].Enabled);
                    Assert.Equal("DM1.OUT1", form.controller.PLC.IOMappings["UnloaderDown"].Target);
                    Assert.Equal("DM0.IN6", form.controller.PLC.IOMappings["UnloaderUpSensor"].Target);
                    Assert.Equal("HighSpeed", form.controller.PLC.IOMappings["UnloaderUpSensor"].FilterMode.ToString());
                    Assert.Equal(2345, form.routerLinks.Loader.PickDelay.Value);
                    Assert.Equal(3456, form.routerLinks.UnloaderDownTime.Value);
                    Assert.Equal("// custom lowering\nLoaderDown := TRUE; END_FUNCTION(Result := 0);", form.controller.PLC.Programs["LowerLoader"].Source);
                    Assert.Contains("UnloaderDown := FALSE", form.controller.PLC.Programs["RaiseUnloader"].Source);
                    Assert.Equal("Ctrl+F8", ((TextLink)form.baseGroup.Groups["Keyboard"].Members["ReleaseLoaderVacuum"]).Value);
                    Assert.Equal("Transfer Velocity", form.routerLinks.Loader.Velocity.DisplayName);
                    Assert.Equal("Up Time", form.routerLinks.UnloaderUpTime.DisplayName);
                    Assert.True(form.controllerLinks.ValidatePlcSettings(), form.controller.PLC.LastError);
                    form.baseGroup.SaveRegistry();
                    form.gCodeInterpreter.Dispose();
                }
                using var reopened = Registry.CurrentUser.OpenSubKey(path, writable: true)!;
                using var rootAgain = reopened.OpenSubKey(Initialize.InterfaceName, writable: true)!;
                Set(rootAgain, @"Controller\PLC\Mapping\Digital Outputs\LoaderDown", "Output", "DM1.OUT4");
                Set(rootAgain, @"Controller\PLC\Command Programs", "LowerLoader", "LoaderDown := FALSE; END_FUNCTION(Result := 0);");
                Set(rootAgain, "Keyboard", "ReleaseLoaderVacuum", "Ctrl+F9");
                TransferNameMigration.Apply(reopened);
                using var output = rootAgain.OpenSubKey(@"Controller\PLC\Mapping\Digital Outputs\LoaderDown")!;
                Assert.Equal("DM1.OUT4", output.GetValue("Output"));
                using var programs = rootAgain.OpenSubKey(@"Controller\PLC\Command Programs")!;
                Assert.Equal("LoaderDown := FALSE; END_FUNCTION(Result := 0);", programs.GetValue("LowerLoader"));
                Assert.Contains("Loader := TRUE", (string)programs.GetValue("LoaderOn")!);
                using var keyboard = rootAgain.OpenSubKey("Keyboard")!;
                Assert.Equal("Ctrl+F9", keyboard.GetValue("ReleaseLoaderVacuum"));
            }
            catch (Exception e) { failure = e; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static void Set(RegistryKey root, string path, string name, string value)
    {
        using var key = root.CreateSubKey(path);
        key.SetValue(name, value);
    }
}

using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller.PLC;

namespace CutterXYZ.Simulation.Tests;

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
                RouterXYZTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(key);
                Assert.Empty(settings.GetValueNames());
                Assert.Empty(settings.GetSubKeyNames());
                for (int opening = 0; opening < 2; opening++)
                {
                    using var form = new MainForm();
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    Assert.False(form.controller.IsConnected);
                    Assert.False(form.baseGroup.Groups["Controller"].Groups.ContainsKey("Inverters"));
                    Assert.All(form.controller.MotionController.Inverters, inverter => Assert.False(inverter.Configuration.Enabled));
                    var outputs = form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Mapping"].Groups["Digital Outputs"];
                    foreach (var output in outputs.Groups.Values)
                        Assert.DoesNotContain(((NT.LinkManager.TextLink)output.Members["Output"]).Items, target => target.StartsWith("INV", StringComparison.OrdinalIgnoreCase));
                    Assert.True(form.baseGroup.Groups["Zone"].Members.ContainsKey("ZoneClamp"));
                    Assert.True(form.baseGroup.Groups["Zone"].Members.ContainsKey("ZoneRelease"));
                    foreach (string removedSignal in new[] { "Coolant", "Lubricant" })
                    {
                        Assert.DoesNotContain(form.controller.PLC.IOMappings, mapping => mapping.Name == removedSignal);
                        Assert.False(form.baseGroup.Groups["Controller"].Groups["PLC"].Groups["Mapping"].Groups["Digital Outputs"].Groups.ContainsKey(removedSignal));
                        Assert.DoesNotContain(form.keyboardLinks.Commands, command => command.Name == removedSignal);
                        Assert.DoesNotContain(form.controller.PLC.Programs, program => program.Name == removedSignal || program.Source.Contains(removedSignal));
                    }
                    Assert.DoesNotContain(form.controller.PLC.IOMappings, mapping => mapping.Name.Contains("Spindle") || mapping.Name == "PressureSensor");
                    Assert.DoesNotContain(form.cutterLinks.Group.Members.Keys, name => name.Contains("Spindle"));
                    Assert.DoesNotContain(form.controller.PLC.Programs, program => program.Name is "S" or "M3" or "M4" or "M5");
                    foreach (string name in new[] { "Run", "Pause", "Reset", "M2", "Background", "Shared" })
                        Assert.DoesNotContain("Spindle", Initialize.GetPlcSource(name));
                    Assert.Empty(form.Controls.Find("SpindlePanel", true));
                    var capture = typeof(GCodeInterpreter).GetMethod("CaptureContext", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                    var (compilerWithoutSpindle, context) = ((NT_Interpreter.Interpreters.RouterXYZ.RouterCompiler, NT_Interpreter.Shared.Router.RouterMachineContext))capture.Invoke(form.gCodeInterpreter, null)!;
                    var ignoredSpindle = compilerWithoutSpindle.Compile("S12000 M3\nM4\nM5\nG1 X10 F600\nM2", context);
                    Assert.True(ignoredSpindle.Success);
                    var builder = (NT_Interpreter.Shared.Router.Controller.RouterToolPathBuilder)typeof(GCodeInterpreter).GetField("builder", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form.gCodeInterpreter)!;
                    var prepared = builder.Build(ignoredSpindle.Program!);
                    Assert.Equal(4, prepared.Diagnostics.Count(d => d.Code == NT_Interpreter.Shared.Router.RouterDiagnosticCode.IgnoredAuxiliaryCode));
                    builder.Path.Begin(0);
                    Assert.True(form.baseGroup.Groups.ContainsKey("Cutter"));
                    Assert.False(form.baseGroup.Groups.ContainsKey("Router"));
                    Assert.DoesNotContain(form.cutterLinks.Group.Groups.Keys, name => name is "Loader" or "Unloader");
                    Assert.False(form.cutterLinks.Group.Members.ContainsKey("OnBridgeAxis"));
                    Assert.DoesNotContain(form.controller.PLC.IOMappings, mapping => mapping.Name.Contains("Loader") || mapping.Name.Contains("Unloader"));
                    Assert.DoesNotContain(form.controller.PLC.Programs, program => program.Name.Contains("Loader") || program.Name.Contains("Unloader"));
                    Assert.DoesNotContain(form.keyboardLinks.Commands, command => command.Name is "Load" or "Unload" or "ReleaseLoaderVacuum");
                    Assert.DoesNotContain(form.cutterLinks.Group.Groups.Keys, name => name.Contains("Tool") || name.Contains("Roller"));
                    Assert.DoesNotContain(form.controller.PLC.IOMappings, mapping =>
                        mapping.Name.Contains("Roller") || mapping.Name.Contains("SpindleTool") ||
                        mapping.Name is "ToolHeightSensor" or "ToolChanging" or "SpindlePistonRetracted" or "SpindleTaperBlow");
                    Assert.DoesNotContain(form.keyboardLinks.Commands, command => command.Name is "ChangeTool" or "MeasureToolOffset");
                    Assert.DoesNotContain(form.controller.PLC.Programs, program =>
                        program.Name is "WaitRollers" or "StopSpindle" or "WaitSpindle" or "ReleaseTool" or "ClampTool" or "MeasureToolLength");
                    Assert.DoesNotContain(form.controller.MotionController.Macros.Programs, program => program.Name == "M6");
                    var compiler = new NT_Interpreter.Interpreters.RouterXYZ.RouterCompiler();
                    var programWithoutExchange = compiler.Compile("T1 M6\nG1 X10 F600", new NT_Interpreter.Shared.Router.RouterMachineContext(new(0, 0, 0), 100, Enumerable.Repeat(new NT_Interpreter.Shared.Router.RouterPosition(0, 0, 0), 6).ToArray()));
                    Assert.True(programWithoutExchange.Success);
                    Assert.Empty(programWithoutExchange.Program!.Summary.RequiredTools);
                    Assert.Equal("Simulation", form.generalLinks.ModeLink.Value);
                    foreach (string name in new[] { "Run", "Pause", "Reset", "M2" })
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

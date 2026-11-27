using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller.PLC;

namespace RouterXYZBCTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class UnloadTests
{
    [Theory]
    [InlineData("X", 10, 100, 500, 10, 200, 500)]
    [InlineData("Y", 100, 20, 500, 200, 20, 500)]
    public void PositionsRaiseZBeforeMovingThePerpendicularAxis(string bridge,
        double sx, double sy, double sz, double ex, double ey, double ez)
    {
        double[] current = [10, 20, 30];
        var points = MaterialTransferMacro.ResolvePositions(bridge, ["X", "Y", "Z"], current, 100, 200, 500);
        Assert.Equal(new double[] { 10, 20, 500 }, points.Raised);
        Assert.Equal(new double[] { sx, sy, sz }, points.Start);
        Assert.Equal(new double[] { ex, ey, ez }, points.End);
        Assert.Equal(new double[] { 10, 20, 30 }, current);
    }

    [Fact]
    public void PositionsUseCompactAxisOrderAndRejectInvalidInput()
    {
        var points = MaterialTransferMacro.ResolvePositions("X", ["Z", "Y", "X"], [30, 20, 10], -50, 70, 500);
        Assert.Equal(new double[] { 500, 20, 10 }, points.Raised);
        Assert.Equal(new double[] { 500, -50, 10 }, points.Start);
        Assert.Equal(new double[] { 500, 70, 10 }, points.End);
        Assert.Throws<ArgumentException>(() => MaterialTransferMacro.ResolvePositions("Z", ["X", "Y", "Z"], [0, 0, 0], 0, 1, 500));
        Assert.Throws<ArgumentException>(() => MaterialTransferMacro.ResolvePositions("X", ["X", "Z"], [0, 0], 0, 1, 500));
        Assert.Throws<ArgumentException>(() => MaterialTransferMacro.ResolvePositions("X", ["X", "Y"], [0, 0], 0, 1, 500));
        Assert.Throws<ArgumentException>(() => MaterialTransferMacro.ResolvePositions("X", ["X", "Y", "Z"], [0, 0, 0], double.NaN, 1, 500));
        Assert.Throws<ArgumentException>(() => MaterialTransferMacro.ResolvePositions("X", ["X", "Y", "Z"], [0, 0, 0], 0, 1, double.NaN));
    }

    [Fact]
    public void InterfaceRegistersProgramsCommandAndRealMappingBindings()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterUnload-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZBCTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                Assert.Contains(form.keyboardLinks.Commands, command => command.Name == "Unload");
                Assert.True(form.controller.PLC.Programs.TryGetValue("LowerUnloader", out _));
                Assert.True(form.controller.PLC.Programs.TryGetValue("RaiseUnloader", out _));
                form.controller.PLC.IOMappings["UnloaderDown"].Target = "DM0.OUT0";
                foreach (bool assigned in new[] { false, true })
                {
                    form.controller.PLC.IOMappings["UnloaderDownSensor"].Target = assigned ? "DM0.IN4" : "";
                    form.controller.PLC.IOMappings["UnloaderUpSensor"].Target = assigned ? "DM0.IN5" : "";
                    Assert.True(form.controllerLinks.ValidatePlcSettings(), form.controller.PLC.LastError);
                }
                form.routerLinks.UnloaderDownTime.Value = 123;
                Assert.Equal(123, ((INTEGER)form.controller.PLC.ApplicationVariables["UnloaderDownTime"]).Value);
            }
            catch (Exception exception) { failure = exception; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

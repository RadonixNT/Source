using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT.Controller;
using NT.Controller.PLC;

namespace RouterXYZBCTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class RollerBoundsTests
{
    [Fact]
    public void CuttingBoundsExcludeTraverseAndTransformsAndClearForReplacement()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterRoller-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZBCTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using var form = new MainForm();
                form.InitializeRuntime(settings, connectOnStartup: false);
                Assert.True(form.controller.PLC.Validate(), form.controller.PLC.LastError);
                var variables = form.controller.PLC.ApplicationVariables;
                LOGIC available = variables["RollerCuttingBoundsAvailable"];
                LOGIC parallelX = variables["RollerParallelX"];
                REAL minX = variables["RollerCuttingMinimumX"];
                REAL maxX = variables["RollerCuttingMaximumX"];
                REAL minY = variables["RollerCuttingMinimumY"];
                REAL maxY = variables["RollerCuttingMaximumY"];
                Assert.False(available.Value);
                Assert.True(parallelX.Value);
                Assert.Same(form.routerLinks.OnBridgeAxis, form.routerLinks.Group.Members["OnBridgeAxis"]);
                Assert.False(form.routerLinks.RollerGroup.Members.ContainsKey("RollerAxis"));
                Assert.True(form.routerLinks.OnBridgeAxis.ItemsOnly);
                Assert.False(form.routerLinks.OnBridgeAxis.Validator!("Z"));
                form.routerLinks.OnBridgeAxis.Value = "Y";
                Assert.False(parallelX.Value);
                form.routerLinks.OnBridgeAxis.Value = "X";
                Assert.True(parallelX.Value);

                ToolPath path = form.controller.MotionController.ToolPaths[0];
                path.Begin();
                path.Reference = [100, 200, 300, 0, 0];
                path.SetLiveOffset(1, 3);
                path.AddLine(1, [10, 0, 5, 0, 0], 10, ToolPathMotionKind.Traverse);
                path.AddLine(2, [20, 5, 0, 0, 0], 10, ToolPathMotionKind.Cutting);
                path.AddLine(3, [30, -5, 0, 0, 0], 10, ToolPathMotionKind.Cutting);
                path.AddLine(4, [1000, 1000, 1000, 0, 0], 10, ToolPathMotionKind.Traverse);
                path.End();
                form.routerLinks.SetCuttingBounds(path.CaptureGeometry());
                Assert.True(available.Value);
                Assert.Equal(10, minX.Value);
                Assert.Equal(30, maxX.Value);
                Assert.Equal(-5, minY.Value);
                Assert.Equal(5, maxY.Value);
                form.routerLinks.SetCuttingBounds(null);
                Assert.False(available.Value);
                path.Begin();
                path.AddLine(1, [50, 60, 70, 0, 0], 10, ToolPathMotionKind.Traverse);
                path.End();
                form.routerLinks.SetCuttingBounds(path.CaptureGeometry());
                Assert.False(available.Value);
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Offline roller check did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}


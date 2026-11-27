using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Win32;
using NT;
using NT.LinkManager;
using RouterXYZACTC;

namespace RouterXYZACTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class ToolLengthMeasurementTests
{
    [Fact]
    public void MeasuredLengthPersistsIndependentlyAndCompensationAddsOffsetZ()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string registryPath = @"Software\Radonix\Tests\RouterToolLength-" + Guid.NewGuid().ToString("N");
            try
            {
                RouterXYZACTCTexts.Register();
                using var settings = Registry.CurrentUser.CreateSubKey(registryPath);
                using (var form = new MainForm())
                {
                    form.InitializeRuntime(settings, connectOnStartup: false);
                    Tool tool = form.toolChangerLinks.Tools[0];
                    var group = form.toolChangerLinks.ToolsGroup.Groups["T01"];
                    Assert.True(group.Members.ContainsKey("Length"));
                    ((VariableLink<PointD3>)group.Members["Offset"]).Value = new(1.25, -2.5, 3);
                    // This fixture disposes the hidden form without a normal FormClosing save.
                    group.Members["Offset"].Flush();
                    tool.SetMeasuredLength(47.5);
                    Assert.Equal(47.5, tool.Length);
                    Assert.Equal(new PointD3(1.25, -2.5, 3), tool.Offset);
                    Assert.Throws<ArgumentOutOfRangeException>(() => tool.SetMeasuredLength(double.NaN));
                    Assert.Throws<ArgumentOutOfRangeException>(() => tool.SetMeasuredLength(0));
                    Assert.Equal(47.5, tool.Length);
                    Assert.Equal(new PointD3(1.25, -2.5, 3), tool.Offset);
                    var compensation = (double[])typeof(ToolChangerLinks).GetMethod("GetWorkOffset",
                        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form.toolChangerLinks, [1L])!;
                    Assert.Equal(new double[] { 1.25, -2.5, 50.5, 0, 0 }, compensation);
                    form.ApplyControllerProperties();
                    Assert.True(form.controller.PLC.Validate());
                }
                using var reopened = new MainForm();
                reopened.InitializeRuntime(settings, connectOnStartup: false);
                Assert.Equal(47.5, reopened.toolChangerLinks.Tools[0].Length);
                Assert.Equal(new PointD3(1.25, -2.5, 3), reopened.toolChangerLinks.Tools[0].Offset);
            }
            catch (Exception error) { failure = error; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Offline tool-length verification did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

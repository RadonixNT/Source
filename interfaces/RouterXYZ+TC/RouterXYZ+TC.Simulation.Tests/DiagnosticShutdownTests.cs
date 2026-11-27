using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Microsoft.Win32;
using NT.Diag;
using OpenTK.Windowing.Desktop;

namespace RouterXYZTC.Simulation.Tests;

[Collection("Router UI")]
public sealed class DiagnosticShutdownTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void ShutdownDisposesDiagnosticsBeforeTheirBorrowedTheme(bool allMonitors, bool closeMonitorFirst, bool disposeOwner)
    {
        Exception? failure = null;
        string stage = "starting";
        var thread = new Thread(() =>
        {
            string path = @"Software\Radonix\Tests\Shutdown-" + Guid.NewGuid().ToString("N");
            bool checkMainThread = GLFWProvider.CheckForMainThread;
            try
            {
                // The test owns one STA UI thread, rather than the test runner's entry thread.
                GLFWProvider.CheckForMainThread = false;
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: true);
                using var settings = Registry.CurrentUser.CreateSubKey(path);
                using var main = new MainForm();
                stage = "initializing";
                main.InitializeRuntime(settings, connectOnStartup: false);
                main.Show();
                stage = "opening diagnostics";
                main.OpenIoDiagnostics();
                if (allMonitors)
                {
                    main.OpenPlcPrograms();
                    main.OpenAxisVelocity();
                    main.OpenCycleTiming();
                    main.OpenPlcMemory();
                }
                DiagnosticForm[] monitors = main.OwnedForms.OfType<DiagnosticForm>().ToArray();
                Assert.Equal(allMonitors ? 5 : 1, monitors.Length);
                Assert.All(monitors, monitor => Assert.True(monitor.Visible));
                if (closeMonitorFirst) monitors[0].Close();
                stage = "closing owner";
                if (disposeOwner) main.Dispose();
                else main.Close();
                Assert.True(main.IsDisposed);
                Assert.All(monitors, monitor => Assert.True(monitor.IsDisposed));
            }
            catch (Exception error) { failure = error; }
            finally
            {
                GLFWProvider.CheckForMainThread = checkMainThread;
                Registry.CurrentUser.DeleteSubKeyTree(path, false);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Shutdown must not hang: " + stage);
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

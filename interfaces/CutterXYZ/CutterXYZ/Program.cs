using NT.Diagnostics;

namespace CutterXYZ;

/// <summary>
/// Process entry: establish reporting and localization before constructing any
/// controls. The shared theme host owns the single-instance WinForms message loop;
/// MainForm_Load owns settings access and optional controller connection.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        NTLog.ReportPath = Initialize.ReportPath;
        NTLog.Report(Initialize.InterfaceName);
        RouterXYZTCTexts.Register();
        NT.ConfigurationManager.ConfigurationManagerLocalization.Register();
        Application.ThreadException += (_, e) => NTLog.Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
                NTLog.Report(exception);
            NTLog.CloseReportFile();
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            NTLog.Report(e.Exception);
            e.SetObserved();
        };

        try { NT.Tools.sTheme.Run(() => new MainForm(), singleInstance: true); }
        finally { NTLog.CloseReportFile(); }
    }
}

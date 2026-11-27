using NT.Tools;
using NT.Diagnostics;
using NT.Controller.PLC;

namespace RouterXYZACRTCP;

/// <summary>
/// Reports operator requests separately from observed machine outcomes. This
/// UI-rate observer records transitions, never every position or G-code row.
/// Spindle output/setpoint records describe commands, not physical rotation.
/// </summary>
public partial class MainForm
{
    private readonly Dictionary<string, long> reportedPlcFailures = new(StringComparer.Ordinal);

    private void PlcProgramResultChanged(object? sender, PLCProgramRuntimeChangedEventArgs e)
    {
        if (!e.HasCompleted || e.IsRunning || e.Succeeded ||
            e.LastExitReason is not (PLCProgramExitReason.Completed or PLCProgramExitReason.RuntimeFault)) return;
        PostRuntimeUi(() =>
        {
            if (runtimeDisposed || !controller.IsConnected || e.Program.ActivationId != e.ActivationId) return;
            if (reportedPlcFailures.GetValueOrDefault(e.Program.Name) == e.ActivationId) return;
            reportedPlcFailures[e.Program.Name] = e.ActivationId;
            if (e.LastExitReason == PLCProgramExitReason.Completed && e.LastResult == 4 && reportHomeNames.Contains(e.Program.Name))
            {
                string message = controllerLinks.HomeFailureMessage(e.Program);
                string detail = $"[{PlcResultCatalog.Code(e.Program.Name, 4)}] {message} Program={e.Program.Name}; Activation={e.ActivationId}; Result=4.";
                NTLog.Error(detail);
                sMessage.Show(this, new SMessageRequest(message)
                {
                    Title = T(RouterXYZACRTCPTexts.Home), Severity = SMessageSeverity.Warning, TechnicalDetails = detail
                }, RuntimeTheme);
            }
            else NTLog.Error(PlcResultCatalog.Format(e.Program.Name, e.ActivationId, e.LastExitReason, e.LastResult, e.LastErrorDetail));
        });
    }

    private void PathProgramFailureReported(object? sender, NT.Controller.ToolPathStoppedEventArgs e)
    {
        if (e.ProgramFailure is not { } failure) return;
        PostRuntimeUi(() =>
        {
            if (runtimeDisposed || !controller.IsConnected || controller.ToolPaths[e.ToolPathIndex].ExecutionId != e.ExecutionId) return;
            string name = controller.PLC.Programs.FirstOrDefault(p => p.Index == failure.ProgramIndex)?.Name
                ?? $"Index{failure.ProgramIndex}";
            NTLog.Error($"TP{e.ToolPathIndex}; Execution={e.ExecutionId}; Block={failure.BlockIndex}; " +
                PlcResultCatalog.Format(name, failure.ActivationId, failure.ExitReason, failure.Result));
        });
    }
    private long reportedStopExecution;
    private bool reportOnline;
    private bool reportedRunning, reportedStopping;
    private bool reportedSpindleCW, reportedSpindleCCW;
    private double reportedSpindleSpeed;
    private bool[] reportedHomed = [];
    private readonly Dictionary<string, (long Activation, bool Completed)> reportedHomePrograms = new();
    private static readonly string[] reportHomeNames = [Initialize.HomePlcProgramName, "HomeX", "HomeY", "HomeZ", "HomeA", "HomeC"];

    internal void ReportOperation(string message)
        => NTLog.Report($"RouterXYZAC+RTCP: {message}; File={currentFile ?? "<none>"}; SourceLine={mainToolPath.CurrentBlock}");

    internal void ObserveHomeRequest(string name, PLCProgram program)
        => reportedHomePrograms[name] = (program.ActivationId, program.HasCompleted && !program.IsRunning);

    // UI-side observation only. Unchanged ticks allocate no messages and write no logs.
    private void RefreshOperationReport()
    {
        if (!IsMachineOnline)
        {
            if (reportOnline && reportedRunning) ReportOperation("Program observation interrupted: machine offline");
            reportOnline = false;
            reportedHomePrograms.Clear();
            reportedPlcFailures.Clear();
            return;
        }
        bool running = mainToolPath.IsRunning;
        bool stopping = mainToolPath.IsStopping;
        bool cw = controller.PLC.IOMappings["SpindleCW"].GetLogic();
        bool ccw = controller.PLC.IOMappings["SpindleCCW"].GetLogic();
        double rpm = spindleSpeedLink.Value;
        if (reportedHomed.Length != controller.Axes.Length) reportedHomed = new bool[controller.Axes.Length];
        for (int index = 0; index < reportedHomed.Length; index++)
        {
            var axis = controller.Axes[index];
            bool homed = axis.IsHomed;
            if (reportOnline && homed != reportedHomed[index])
                ReportOperation($"Axis home state changed: {axis.Name}; Homed={homed}; Position={axis.CommandPosition}");
            reportedHomed[index] = homed;
        }
        if (reportOnline)
        {
            if (running != reportedRunning)
                ReportOperation(running ? "Program execution running" : "Program execution no longer running");
            if (stopping && !reportedStopping) ReportOperation("Program controlled stopping observed");
            if (cw != reportedSpindleCW || ccw != reportedSpindleCCW)
                ReportOperation($"Spindle output changed: CW={cw}; CCW={ccw}; RPM={rpm}");
            if (rpm != reportedSpindleSpeed)
                ReportOperation($"Spindle speed setpoint changed: {reportedSpindleSpeed} -> {rpm} rpm");
        }
        else ReportOperation($"Online observation started: SpindleCW={cw}; SpindleCCW={ccw}; RPM={rpm}");
        reportedRunning = running;
        reportedStopping = stopping;
        reportedSpindleCW = cw;
        reportedSpindleCCW = ccw;
        reportedSpindleSpeed = rpm;
        reportOnline = true;

        foreach (string name in reportHomeNames)
        {
            if (!controller.PLC.Programs.TryGetValue(name, out PLCProgram? program) || program is null) continue;
            long activation = program.ActivationId;
            bool completed = program.HasCompleted && !program.IsRunning;
            if (reportedHomePrograms.TryGetValue(name, out var previous))
            {
                if (activation != previous.Activation)
                    ReportOperation($"Home PLC activated: {name}; Activation={activation}");
                if (completed && (activation != previous.Activation || !previous.Completed))
                {
                    string result = $"Home PLC ended: {name}; Activation={activation}; Exit={program.LastExitReason}; Result={program.LastResult}; Succeeded={program.Succeeded}";
                    if (program.Succeeded) ReportOperation(result);
                    else NTLog.Warning(result);
                }
            }
            reportedHomePrograms[name] = (activation, completed);
        }
    }
}

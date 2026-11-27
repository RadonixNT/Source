using NT;
using NT.Controller;
using NT.Controller.Macros;

namespace RouterXYZTC;

/// <summary>
/// Bridges an M6 macro callback to the UI-owned exchange state machine. The queued
/// request retains its source path, block and generation so cancellation or a new
/// cursor cannot accidentally continue a different machining program.
/// </summary>
internal sealed partial class ToolChangerLinks
{
    private PendingM6? pendingM6;

    // Managed macro callbacks run on a dispatcher worker. QueueM6 only validates
    // and captures the immutable request; UpdateM6 performs Link writes and motion
    // submission on the interface thread after Core has paused TP0 and released axes.
    private void QueueM6(object sender, MacrosArg arg)
    {
        lock (gate)
        {
            if (disposed || arg.IsCancellationRequested) return;
            if (!ReferenceEquals(arg.ToolPath, controller.MotionController.ToolPaths[0]) ||
                !double.IsFinite(arg.ProgramArg) || arg.ProgramArg != Math.Truncate(arg.ProgramArg) ||
                arg.ProgramArg < 0 || arg.ProgramArg > Initialize.ToolCount)
                throw new InvalidOperationException(
                    $"M6 requires a TP0 request with an integer tool number in 0..{Initialize.ToolCount}.");
            if (pendingM6 is not null)
                throw new InvalidOperationException("An M6 request is already pending.");
            pendingM6 = new(arg, (long)arg.ProgramArg);
        }
    }

    private void UpdateM6()
    {
        if (pendingM6 is not { } request) return;
        ToolPath source = request.Arg.ToolPath!;
        if (disposed || !controller.IsConnected || request.Arg.IsCancellationRequested || source.IsProgramRequestCancelled ||
            source.CoordinateResetGeneration != request.Arg.ToolPathGeneration ||
            source.CurrentBlock != request.Arg.ToolPathBlockIndex ||
            controller.PLC.IsApplying)
        {
            pendingM6 = null;
            RejectChange("M6 was cancelled or its source program was replaced.");
            return;
        }
        // A previous change may still be observing M100 completion. Also wait for
        // this callback to return so consecutive M6 rows cannot overlap one active
        // managed-program invocation.
        if (pending is not null || request.Arg.Program.IsRunning) return;
        // The published C2M boundary guarantees that RT has paused TP0 and released
        // its axes before this request becomes visible to the managed dispatcher.
        pendingM6 = null;
        if (prepareGCodeContinuation is not null && !prepareGCodeContinuation(source.CurrentBlock))
        {
            RejectChange("M6 continuation could not prepare the next cutting entry.");
            return;
        }
        NewTool.Value = request.Tool;
        // Mode zero identifies a G-code request. Same-tool requests still perform
        // the offset handshake; a terminal M6 omits M100 because no blocks remain.
        ChangeTool(request.Tool, GCodeChangeMode);
    }

    private sealed record PendingM6(MacrosArg Arg, long Tool);
}

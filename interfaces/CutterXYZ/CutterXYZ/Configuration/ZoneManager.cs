using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;

namespace CutterXYZ;

/// <summary>One CycleUpdated subscriber, independent of the form refresh timer. No PLC program owns zone policy.</summary>
internal sealed class ZoneManager : IDisposable
{
    // Serialize cycle-worker access with connection changes and UI clamp/release requests.
    // The callback reads runtime state only; Link edits are captured at the Online boundary.
    private readonly object gate = new();
    private readonly ControllerWrapper controller;
    private readonly Func<ZoneDefinition[]?> configuration;
    private readonly PLCIOMapping emergency;
    private ZoneRuntime? runtime;
    private bool subscribed, disposed, reportedWriteFailure;
    private long session;
    private ControllerCycleEventArgs? currentCycle;
    private readonly Func<ulong, bool, bool> writeOutput;
    internal bool IsSubscribed { get { lock (gate) return subscribed; } }
    internal bool Clamped { get { lock (gate) return controller.IsConnected && runtime?.Clamped == true; } }
    internal bool Pending { get { lock (gate) return runtime?.Pending == true; } }
    internal bool Ready { get { lock (gate) return controller.IsConnected && runtime?.Initialized == true; } }

    internal ZoneManager(ControllerWrapper controller, Func<ZoneDefinition[]?> configuration)
    {
        this.controller = controller;
        this.configuration = configuration;
        writeOutput = Write;
        emergency = controller.PLC.IOMappings["EmergencySwitch"];
        controller.ConnectionStateChanged += ConnectionChanged;
    }

    private void ConnectionChanged(object? sender, ConnectionStateChangedEventArgs args)
    {
        lock (gate)
        {
            if (disposed) return;
            StopSubscription();
            if (args.State != ConnectionState.Online || !controller.IsConnected) return;
            ZoneDefinition[]? definitions = configuration();
            if (definitions is null || definitions.Length == 0) return;
            runtime = new ZoneRuntime(definitions);
            controller.CycleUpdated += Cycle;
            subscribed = true;
        }
    }

    internal bool Clamp(ulong selection)
    { lock (gate) return controller.IsConnected && runtime?.RequestClamp(selection) == true; }
    internal bool Release()
    { lock (gate) return controller.IsConnected && runtime?.RequestRelease() == true; }

    private void Cycle(object? sender, ControllerCycleEventArgs args)
    {
        lock (gate)
        {
            if (disposed || !subscribed || runtime is null || !args.IsCurrent || !controller.IsConnected) return;
            if (session != 0 && session != args.SessionId) return; // A fresh Online notification must capture the new configuration.
            session = args.SessionId;
            if (controller.PLC.IsApplying) return;
            if (runtime.Initialized && !runtime.Pending && !runtime.HasExtraction) return;
            var path = controller.ToolPaths[0];
            
            bool idle = path.AreAxesIdle && controller.ToolPaths.All(p => p.IsIdle && !p.IsProgramActive);
            bool extract = runtime.HasExtraction && path.IsRunning && !path.IsStopRequested &&
                !controller.ToolPaths[1].IsProgramActive && !controller.ToolPaths[2].IsProgramActive &&
                emergency.GetLogic() && !controller.Hold &&
                (controller.Status & ControllerStatus.EvaluationExpired) == 0;
            double x = 0, y = 0;
            if (extract)
            {
                var axisX = controller.GetAxisByName("X");
                var axisY = controller.GetAxisByName("Y");
                extract = axisX.IsHomed && axisY.IsHomed;
                if (extract) { x = axisX.ActualPosition; y = axisY.ActualPosition; }
            }
            currentCycle = args;
            runtime.Tick(new(idle, extract, x, y), writeOutput);
        }
    }

    private bool Write(ulong mask, bool value)
    {
        var args = currentCycle!;
        if (!args.IsCurrent || !controller.IsConnected || controller.PLC.IsApplying) return false;
        try
        {
            if (value) controller.MotionController.PLC.SetDigitalOutput(mask);
            else controller.MotionController.PLC.ClearDigitalOutput(mask);
            reportedWriteFailure = false;
            return true;
        }
        catch (InvalidOperationException error)
        {
            // Queue-full / Apply / disconnect races are retried on a later cycle, not quarantined forever.
            if (!reportedWriteFailure && args.IsCurrent)
                NTLog.Warning($"Zone output command was not accepted; will retry: {error.Message}");
            reportedWriteFailure = true;
            return false;
        }
    }

    private void StopSubscription()
    {
        if (subscribed) controller.CycleUpdated -= Cycle;
        subscribed = false;
        runtime = null;
        currentCycle = null;
        session = 0;
        reportedWriteFailure = false;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            controller.ConnectionStateChanged -= ConnectionChanged;
            StopSubscription();
        }
    }
}

using NT;
using NT.Controller;
using NT.Diagnostics;
using NT.Tools;

namespace RouterXYZBCTC;

/// <summary>
/// Serializes connection and runtime-mode transitions on the UI context. Mode is
/// a persisted choice; connectionRuntimeMode records the captured request. Native
/// process lifetime and axis activation belong exclusively to ControllerWrapper.
/// </summary>
public partial class MainForm
{
    private readonly CancellationTokenSource connectionLifetime = new();
    private Task? connectionTransition;
    internal bool IsConnectionTransition { get; private set; }
    private ControllerRuntimeMode connectionRuntimeMode = ControllerRuntimeMode.HardSimulation;
    private string lastSelectedMode = "Simulation";

    internal bool CanChangeMode => runtimeInitialized && !runtimeDisposed && !IsDisposed &&
        !IsConnectionTransition && generalLinks.AccessSession.CanOperate && !IsProgramLoading &&
        (controller.ConnectionState is ConnectionState.Online or ConnectionState.Offline or ConnectionState.Failed) &&
        !IsMotionOrProgramActive() && toolChangerLinks?.IsActive != true;

    internal bool CanAcceptModeValue(string value) => !runtimeDisposed &&
        (!runtimeInitialized || value == generalLinks.ModeLink.Value || CanChangeMode);

    private const string MissingLicenseMessage = "No license file imported.";

    private bool MissingMachineLicense => generalLinks.SelectedRuntimeMode == ControllerRuntimeMode.Normal
        && !controllerLinks.HasLicenseFile;

    public void ConnectController()
    {
        if (!runtimeInitialized || runtimeDisposed || IsConnectionTransition ||
            !controller.ConnectionState.AllowsOfflineConfiguration()) return;
        connectionTransition = ChangeControllerConnectionAsync(connect: true);
    }

    internal void DisconnectController()
    {
        if (!runtimeInitialized || runtimeDisposed || IsConnectionTransition || IsProgramLoading) return;
        connectionTransition = ChangeControllerConnectionAsync(connect: false);
    }

    internal void RuntimeModeChanged()
    {
        if (!runtimeInitialized || runtimeDisposed) return;
        NTLog.Report($"Controller mode change requested: {lastSelectedMode} -> {generalLinks.ModeLink.Value}.");
        lastSelectedMode = generalLinks.ModeLink.Value;
        connectionTransition = ChangeControllerConnectionAsync(connect: true, reconnect: true);
    }

    private void RefreshConnectionIndicator()
    {
        // Runtime identity is intentionally invariant across UI languages.
        bool simulation = runtimeInitialized && controller.IsConnected
            ? controller.MotionController.SimulationMode == SimulationModes.Hard
            : generalLinks?.SelectedRuntimeMode != ControllerRuntimeMode.Normal;
        R_Connected.Text = simulation ? "Simulation" : "Online";
        R_Connected.SignalOn = runtimeInitialized && (simulation ? IsMachineOnline : controller.IsMmOnline);
    }

    /// <summary>
    /// Sole application owner of Connect/Disconnect requests, including mode
    /// changes and shutdown. ControllerWrapper retains native lifecycle ownership.
    /// </summary>
    private async Task ChangeControllerConnectionAsync(bool connect, bool reconnect = false, bool shutdown = false)
    {
        if (controller is null) return;
        if (shutdown)
        {
            NTLog.Report("Controller disconnect requested: application shutdown.");
            controller.Disconnect();
            return;
        }
        if (IsConnectionTransition || runtimeDisposed || IsProgramLoading ||
            !generalLinks.AccessSession.CanOperate) return;
        if (reconnect && !CanChangeMode) return;

        IsConnectionTransition = true;
        RequestRuntimeStateRefresh();
        CancellationToken token = connectionLifetime.Token;
        try
        {
            if (!connect || reconnect)
            {
                if (!controller.ConnectionState.AllowsOfflineConfiguration())
                {
                    controllerLinks.RefreshSpindleSpeed(captureOnly: true);
                    NTLog.Report("Controller disconnect requested.");
                    ConnectionState disconnected = await ConnectionStateAwaiter.RunAsync(controller,
                        () => controller.Disconnect(),
                        state => state.AllowsOfflineConfiguration(), token, TimeSpan.FromSeconds(60));
                    NTLog.Report($"Controller disconnect completed: {disconnected}.");
                    if (disconnected != ConnectionState.Offline) return;
                }
                simulationHost?.Dispose();
                simulationHost = null;
            }
            if (!connect) return;
            token.ThrowIfCancellationRequested();
            if (!controller.ConnectionState.AllowsOfflineConfiguration()) return;
            if (MissingMachineLicense)
            {
                R_ProgramProgress.Text = MissingLicenseMessage;
                NTLog.Warning(MissingLicenseMessage);
                sMessage.Show(this, new SMessageRequest(MissingLicenseMessage)
                {
                    Title = Initialize.InterfaceName,
                    Severity = SMessageSeverity.Warning
                }, RuntimeTheme);
                return;
            }
            // This owner holds the transition gate; validate the candidate
            // without permitting Settings OK to apply a competing candidate.
            if (!ValidateSettingsCandidate())
            {
                NTLog.Warning("Controller connection rejected: invalid machine settings.");
                sMessage.Show(this, SMessageRequest.FromError(NTErrorCode.InterfaceInvalidConfiguration,
                    SMessageSeverity.Warning) with { Title = "RouterXYZBC+TC" }, RuntimeTheme);
                return;
            }
            ApplyControllerProperties();
            await gCodeInterpreter.PendingLoad.WaitAsync(token);
            token.ThrowIfCancellationRequested();
            connectionRuntimeMode = generalLinks.SelectedRuntimeMode;
            token.ThrowIfCancellationRequested();
            if (!generalLinks.AccessSession.CanOperate || runtimeDisposed || IsDisposed) return;
            RecreateMachineSimulation();
            NTLog.Report($"Controller connect requested: {generalLinks.ModeLink.Value} ({connectionRuntimeMode}).");
            ConnectionState connected = await ConnectionStateAwaiter.RunAsync(controller,
                () => controller.Connect(connectionRuntimeMode),
                state => state is ConnectionState.Online or ConnectionState.Failed or ConnectionState.Offline,
                token, TimeSpan.FromMinutes(5));
            NTLog.Report($"Controller connection completed: {connected}, mode = {generalLinks.ModeLink.Value}." +
                (connected == ConnectionState.Failed ? $" {controller.LastError}" : ""));
        }
        catch (OperationCanceledException) when (connectionLifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            NTLog.Error($"Controller connection transition failed: {error}");
            // A failed/timed-out transition must not leave a pending startup that
            // could later come Online under an apparently idle mode selector.
            controller.Disconnect();
            if (!runtimeDisposed && !IsDisposed)
                sMessage.Show(this, new SMessageRequest(T(RouterXYZBCTCTexts.ControllerConnectionFailed))
                {
                    Title = T(RouterXYZBCTCTexts.ControllerTitle),
                    Severity = SMessageSeverity.Warning,
                    TechnicalDetails = error.Message
                }, RuntimeTheme);
        }
        finally
        {
            IsConnectionTransition = false;
            if (!runtimeDisposed && !IsDisposed)
            {
                RefreshConnectionIndicator();
                RequestRuntimeStateRefresh();
            }
        }
    }

}

/// <summary>Subscribes before a lifecycle request and releases the subscription on every exit.</summary>
internal static class ConnectionStateAwaiter
{
    internal static async Task<ConnectionState> RunAsync(ControllerWrapper controller, Action request,
        Func<ConnectionState, bool> terminal, CancellationToken token, TimeSpan timeout)
    {
        var completion = new TaskCompletionSource<ConnectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(object? sender, ConnectionStateChangedEventArgs e)
        {
            if (terminal(e.State)) completion.TrySetResult(e.State);
        }
        controller.ConnectionStateChanged += Changed;
        try
        {
            token.ThrowIfCancellationRequested();
            request();
            if (terminal(controller.ConnectionState)) completion.TrySetResult(controller.ConnectionState);
            return await completion.Task.WaitAsync(timeout, token);
        }
        finally { controller.ConnectionStateChanged -= Changed; }
    }
}

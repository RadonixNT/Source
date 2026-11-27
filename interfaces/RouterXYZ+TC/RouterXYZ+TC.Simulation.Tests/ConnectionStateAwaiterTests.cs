using System.Reflection;
using NT;
using NT.Controller;
using RouterXYZTC;

namespace RouterXYZTC.Simulation.Tests;

public sealed class ConnectionStateAwaiterTests
{
    private static readonly FieldInfo State = typeof(Core).GetField("connectionState", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Handlers = typeof(ControllerWrapper).GetField("ConnectionStateChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public async Task DisconnectWaitsForTerminalStateAndRemovesItsHandler()
    {
        var controller = new ControllerWrapper(1, 1, "unused.xml");
        Task<ConnectionState> pending = ConnectionStateAwaiter.RunAsync(controller,
            () => State.SetValue(controller.MotionController, ConnectionState.Disconnecting),
            state => state.AllowsOfflineConfiguration(), CancellationToken.None, TimeSpan.FromSeconds(3));
        Assert.False(pending.IsCompleted);
        Publish(controller, ConnectionState.Disconnecting);
        Assert.False(pending.IsCompleted);
        Publish(controller, ConnectionState.Offline);
        Assert.Equal(ConnectionState.Offline, await pending);
        Assert.Null(Handlers.GetValue(controller));
    }

    [Fact]
    public async Task SynchronousFailureIsObservedBecauseSubscriptionPrecedesRequest()
    {
        var controller = new ControllerWrapper(1, 1, "unused.xml");
        ConnectionState result = await ConnectionStateAwaiter.RunAsync(controller,
            () => Publish(controller, ConnectionState.Failed),
            state => state is ConnectionState.Online or ConnectionState.Failed,
            CancellationToken.None, TimeSpan.FromSeconds(3));
        Assert.Equal(ConnectionState.Failed, result);
        Assert.Null(Handlers.GetValue(controller));
    }

    [Fact]
    public async Task ShutdownCancellationReleasesWaitWithoutIssuingAnotherRequest()
    {
        var controller = new ControllerWrapper(1, 1, "unused.xml");
        using var cancellation = new CancellationTokenSource();
        int requests = 0;
        Task<ConnectionState> pending = ConnectionStateAwaiter.RunAsync(controller,
            () => { requests++; State.SetValue(controller.MotionController, ConnectionState.Connecting); },
            state => state == ConnectionState.Online, cancellation.Token, TimeSpan.FromSeconds(3));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, requests);
        Assert.Null(Handlers.GetValue(controller));
    }

    private static void Publish(ControllerWrapper controller, ConnectionState state)
    {
        State.SetValue(controller.MotionController, state);
        ((EventHandler<ConnectionStateChangedEventArgs>?)Handlers.GetValue(controller))?.Invoke(
            controller, new ConnectionStateChangedEventArgs(state, ""));
    }
}

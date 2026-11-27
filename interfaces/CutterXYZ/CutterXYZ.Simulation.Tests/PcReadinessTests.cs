using NT;
using NT.Config;

namespace CutterXYZ.Simulation.Tests;

public sealed class PcReadinessTests
{
    [Theory]
    [InlineData(ControllerRuntimeMode.HardSimulation)]
    [InlineData(ControllerRuntimeMode.EngineeringNdis)]
    public async Task NonRealtimeModesNeverInspectWindows(ControllerRuntimeMode mode)
    {
        var result = await PcConnectionReadiness.InspectAsync(mode,
            () => throw new InvalidOperationException("Must not inspect Windows"), CancellationToken.None);
        Assert.True(result.Ready);
    }

    [Fact]
    public async Task PhysicalConnectionsRecheckLiveStateEveryTime()
    {
        int calls = 0;
        PcReadiness.Result Inspect() => new(++calls == 1, false, "fixture");
        Assert.True((await PcConnectionReadiness.InspectAsync(ControllerRuntimeMode.Normal, Inspect, CancellationToken.None)).Ready);
        Assert.False((await PcConnectionReadiness.InspectAsync(ControllerRuntimeMode.Normal, Inspect, CancellationToken.None)).Ready);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CancellationCannotAdmitACompletedInspection()
    {
        using var cancellation = new CancellationTokenSource();
        var pending = PcConnectionReadiness.InspectAsync(ControllerRuntimeMode.Normal,
            () => { cancellation.Cancel(); return new(true, false, "fixture"); }, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
    }
}

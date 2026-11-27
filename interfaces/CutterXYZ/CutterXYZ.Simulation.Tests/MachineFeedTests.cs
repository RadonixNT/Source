using CutterXYZ;

namespace CutterXYZ.Simulation.Tests;

public sealed class MachineFeedTests
{
    [Fact]
    public void ResultantUsesActualIntervalAndReturnsZeroAfterMotionStops()
    {
        var monitor = new MachineFeedMonitor();
        Assert.True(monitor.TrySample(100, 200, 300, 10, true, out double speed));
        Assert.Equal(0, speed);
        Assert.True(monitor.TrySample(103, 204, 312, 10.5, true, out speed));
        Assert.Equal(26, speed, 9);
        Assert.True(monitor.TrySample(106, 208, 324, 11.5, true, out speed));
        Assert.Equal(13, speed, 9);
        Assert.False(monitor.TrySample(106, 208, 324, 11.6, true, out _));
        Assert.True(monitor.TrySample(106, 208, 324, 11.8, true, out speed));
        Assert.Equal(0, speed);
        Assert.True(monitor.TrySample(0, 0, 0, 12, false, out speed));
        Assert.Equal(0, speed);
        Assert.True(monitor.TrySample(500, 600, 700, 13, true, out speed));
        Assert.Equal(0, speed);
    }

    [Fact]
    public void EarlyTicksPreserveBaselineUntilMoreThanTwoTenthsOfASecond()
    {
        var monitor = new MachineFeedMonitor();
        Assert.True(monitor.TrySample(0, 0, 0, 0, true, out _));
        Assert.False(monitor.TrySample(1, 0, 0, 0.1, true, out _));
        Assert.False(monitor.TrySample(2, 0, 0, 0.2, true, out _));
        Assert.True(monitor.TrySample(3, 4, 0, 0.25, true, out double speed));
        Assert.Equal(20, speed, 9);
        Assert.False(monitor.TrySample(6, 8, 0, 0.4, true, out _));
        Assert.True(monitor.TrySample(6, 8, 0, 0.5, true, out speed));
        Assert.Equal(20, speed, 9);
    }

}

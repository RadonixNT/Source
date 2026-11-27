namespace CutterXYZ.Simulation.Tests;

public sealed class ZoneRuntimeTests
{
    private static ZoneRuntime Create() => new(new[]
    {
        new ZoneDefinition(1, true, true, 0, 0, 0, 0),
        new ZoneDefinition(2, true, false, 0, 100, 0, 100),
        new ZoneDefinition(4, false, true, 0, 0, 0, 0),
        new ZoneDefinition(8, true, true, 0, 0, 0, 0)
    });

    [Fact]
    public void WorkholdingRetainsThroughMotionPauseStopAndToolExchange_ReleaseIsExplicit()
    {
        var runtime = Create();
        var commands = new List<(ulong, bool)>();
        bool Write(ulong mask, bool value) { commands.Add((mask, value)); return true; }
        runtime.Tick(new(true, false, 0, 0), Write);
        Assert.Equal(new[] { (15UL, false) }, commands);
        Assert.True(runtime.RequestClamp(13)); // Disabled zone 4 must not be clamped.
        runtime.Tick(new(true, false, 0, 0), Write);
        Assert.Equal((9UL, true), commands.Last());
        Assert.True(runtime.Clamped);
        commands.Clear();
        runtime.Tick(new(false, false, 500, 500), Write);
        runtime.Tick(new(true, false, 500, 500), Write);
        Assert.Empty(commands);
        Assert.True(runtime.Clamped);
        Assert.True(runtime.RequestRelease());
        runtime.Tick(new(true, false, 0, 0), Write);
        Assert.Equal(new[] { (9UL, false) }, commands);
        Assert.False(runtime.Clamped);
    }

    [Fact]
    public void ExtractionUsesInclusiveBoundsAndOnlyQueuesChanges()
    {
        var runtime = Create();
        var commands = new List<(ulong, bool)>();
        bool Write(ulong mask, bool value) { commands.Add((mask, value)); return true; }
        runtime.Tick(new(true, false, 0, 0), Write);
        commands.Clear();
        runtime.Tick(new(false, true, 0, 100), Write);
        runtime.Tick(new(false, true, 100, 0), Write);
        Assert.Equal(new[] { (2UL, true) }, commands);
        runtime.Tick(new(false, true, 100.01, 0), Write);
        Assert.Equal((2UL, false), commands.Last());
        runtime.Tick(new(false, true, 50, 50), Write);
        runtime.Tick(new(false, false, 50, 50), Write); // Stop, Hold, emergency, M6 or program wait gate.
        Assert.Equal((2UL, false), commands.Last());
        int count = commands.Count;
        runtime.Tick(new(false, true, double.NaN, 50), Write);
        Assert.Equal(count, commands.Count);
    }

    [Fact]
    public void QueueRejectionRetriesWithoutClaimingClamp_AndAdmissionIsRechecked()
    {
        var runtime = Create();
        runtime.Tick(new(true, false, 0, 0), (_, _) => false);
        Assert.False(runtime.Initialized);
        Assert.False(runtime.RequestClamp(1));
        runtime.Tick(new(true, false, 0, 0), (_, _) => true);
        Assert.True(runtime.RequestClamp(1));
        runtime.Tick(new(true, false, 0, 0), (_, _) => false);
        Assert.True(runtime.Pending);
        Assert.False(runtime.Clamped);
        runtime.Tick(new(false, false, 0, 0), (_, _) => throw new Exception("Must not clamp after motion starts"));
        Assert.False(runtime.Pending);
        Assert.False(runtime.Clamped);
        Assert.True(runtime.RequestClamp(1));
        runtime.Tick(new(true, false, 0, 0), (_, _) => true);
        Assert.True(runtime.Clamped);
        Assert.True(runtime.RequestRelease());
        runtime.Tick(new(true, false, 0, 0), (_, _) => false);
        Assert.True(runtime.Clamped);
        Assert.True(runtime.Pending);
        runtime.Tick(new(true, false, 0, 0), (_, _) => true);
        Assert.False(runtime.Clamped);
    }

    [Fact]
    public void ExtractionRetriesRejectedChangesAndCancelsObsoleteDesiredState()
    {
        var runtime = Create();
        runtime.Tick(new(true, false, 0, 0), (_, _) => true);
        int writes = 0;
        runtime.Tick(new(false, true, 50, 50), (_, _) => { writes++; return false; });
        runtime.Tick(new(false, false, 50, 50), (_, _) => { writes++; return true; });
        Assert.Equal(1, writes); // The rejected ON must not be retried after Stop.
        runtime.Tick(new(false, true, 50, 50), (_, _) => true);
        runtime.Tick(new(false, false, 50, 50), (_, _) => { writes++; return false; });
        runtime.Tick(new(false, false, 50, 50), (_, _) => { writes++; return true; });
        Assert.Equal(3, writes);
    }

    [Fact]
    public void All64ZonesAreBoundedAndStableTicksAllocateNothing()
    {
        var runtime = new ZoneRuntime(Enumerable.Range(0, 64).Select(i => new ZoneDefinition(1UL << i, true, false, 0, 100, 0, 100)));
        ulong observed = 0;
        Func<ulong, bool, bool> write = (mask, value) => { if (value) observed = mask; return true; };
        runtime.Tick(new(false, true, 50, 50), write);
        Assert.Equal(ulong.MaxValue, observed);
        Assert.Equal(1UL << 63, ZoneLinks.OutputMask("DM7.OUT7"));
        Assert.Equal(0UL, ZoneLinks.OutputMask("INV0.RUN"));
        for (int i = 0; i < 100; i++) runtime.Tick(new(false, true, 50, 50), write);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) runtime.Tick(new(false, true, 50, 50), write);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}

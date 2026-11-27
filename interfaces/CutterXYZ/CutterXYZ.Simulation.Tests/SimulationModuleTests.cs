using CutterXYZ.Simulation;

namespace CutterXYZ.Simulation.Tests;

public sealed class SimulationModuleTests
{
    [Fact]
    public void PositionSwitch_UsesConfiguredSideAndHysteresis()
    {
        var runtime = new FakeRuntime();
        runtime.Axes["X"] = 1.0;
        var module = new PositionSwitchSimulation(
            "Home",
            [new("X", "HomeSensorX", 0.0, PositionSwitchSide.AtOrBelow, 0.05)]);

        module.Update(runtime, TimeSpan.FromMilliseconds(10));
        Assert.False(runtime.Inputs["HomeSensorX"]);

        runtime.Axes["X"] = -0.01;
        module.Update(runtime, TimeSpan.FromMilliseconds(10));
        Assert.True(runtime.Inputs["HomeSensorX"]);

        runtime.Axes["X"] = 0.04;
        module.Update(runtime, TimeSpan.FromMilliseconds(10));
        Assert.True(runtime.Inputs["HomeSensorX"]);

        runtime.Axes["X"] = 0.06;
        module.Update(runtime, TimeSpan.FromMilliseconds(10));
        Assert.False(runtime.Inputs["HomeSensorX"]);
    }

    [Fact]
    public void DigitalFeedback_AppliesIndependentOnAndOffTimes()
    {
        var runtime = new FakeRuntime();
        runtime.Outputs["Clamp"] = false;
        var module = new DigitalFeedbackSimulation(
            "ClampFeedback",
            [new("Clamp", "Locked", TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(20))]);

        module.Update(runtime, TimeSpan.FromMilliseconds(20));
        Assert.False(runtime.Inputs["Locked"]);

        runtime.Outputs["Clamp"] = true;
        module.Update(runtime, TimeSpan.FromMilliseconds(90));
        Assert.False(runtime.Inputs["Locked"]);
        module.Update(runtime, TimeSpan.FromMilliseconds(10));
        Assert.True(runtime.Inputs["Locked"]);

        runtime.Outputs["Clamp"] = false;
        module.Update(runtime, TimeSpan.FromMilliseconds(20));
        Assert.False(runtime.Inputs["Locked"]);
    }

    [Fact]
    public void PredicateInput_PublishesHealthyEmergencyCircuit()
    {
        var runtime = new FakeRuntime();
        var module = new PredicateInputSimulation(
            "EmergencyCircuit",
            "EmergencySwitch",
            () => true);

        module.Update(runtime, TimeSpan.Zero);

        Assert.True(runtime.Inputs["EmergencySwitch"]);
    }

    [Fact]
    public void Host_RunsOnlyWhileHardSimulationIsAvailable()
    {
        var runtime = new FakeRuntime();
        var module = new CountingModule();
        using var host = new SimulationHost(runtime, [module], TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);
        Assert.Equal(0, module.UpdateCount);

        runtime.SetAvailable(true);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref module.UpdateCount) > 0, 1000));
        Assert.True(host.IsRunning);

        runtime.SetAvailable(false);
        Assert.True(SpinWait.SpinUntil(() => !host.IsRunning, 1000));
        int stoppedCount = Volatile.Read(ref module.UpdateCount);
        Thread.Sleep(10);
        Assert.Equal(stoppedCount, Volatile.Read(ref module.UpdateCount));
        Assert.True(runtime.ResetCount > 0);
    }

    [Fact]
    public void Host_RejectsMultipleOwnersForOneInput()
    {
        var runtime = new FakeRuntime();
        var first = new PredicateInputSimulation("First", "SharedSensor", () => false);
        var second = new PredicateInputSimulation("Second", "SharedSensor", () => true);

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => new SimulationHost(runtime, [first, second]));
        Assert.Contains("SharedSensor", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CountingModule : ISimulationModule
    {
        internal int UpdateCount;
        public string Name => "Counter";
        public IReadOnlyCollection<string> DrivenInputs => [];
        public void Reset() { }
        public void Update(ISimulationRuntime runtime, TimeSpan elapsed)
            => Interlocked.Increment(ref UpdateCount);
    }

    private sealed class FakeRuntime : ISimulationRuntime
    {
        internal Dictionary<string, double> Axes { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, bool> Inputs { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, bool> Outputs { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal int ResetCount { get; private set; }
        internal int InputWrites { get; private set; }
        public event EventHandler? AvailabilityChanged;
        public bool IsHardSimulationOnline { get; private set; }
        public double GetAxisPosition(string axisName) => Axes[axisName];
        public bool GetDigitalOutput(string signalName) => Outputs[signalName];
        public void SetDigitalInput(string signalName, bool value)
        {
            Inputs[signalName] = value;
            InputWrites++;
        }
        public void ResetDrivenInputs()
        {
            foreach (string key in Inputs.Keys.ToArray()) Inputs[key] = false;
            ResetCount++;
        }
        internal void SetAvailable(bool available)
        {
            IsHardSimulationOnline = available;
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

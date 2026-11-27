using RouterXYZTC.Simulation;

namespace RouterXYZTC.Simulation.Tests;

public sealed class SimulationModuleTests
{
    [Fact]
    public void RollerFreeSensorFollowsDeenergizedOutputAfterActuatorDelay()
    {
        var runtime = new FakeRuntime();
        runtime.Outputs["Roller1"] = true;
        var module = new DigitalFeedbackSimulation("Roller",
            [new("Roller1", "Roller1Sensor", TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300), Inverted: true)]);
        module.Update(runtime, TimeSpan.FromMilliseconds(300));
        Assert.False(runtime.Inputs["Roller1Sensor"]);
        runtime.Outputs["Roller1"] = false;
        module.Update(runtime, TimeSpan.FromMilliseconds(299));
        Assert.False(runtime.Inputs["Roller1Sensor"]);
        module.Update(runtime, TimeSpan.FromMilliseconds(1));
        Assert.True(runtime.Inputs["Roller1Sensor"]);
    }

    [Fact]
    public void SpindlePublishesInitialValuesThenOnlyEdgesAndRepublishesAfterReset()
    {
        var runtime = new FakeRuntime();
        runtime.Outputs["SpindleToolRelease"] = false;
        runtime.Outputs["SpindleCW"] = false;
        runtime.Outputs["SpindleCCW"] = false;
        var module = new SpindleSimulation(() => true, _ => true,
            TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20),
            ["SpindleToolClamped", "SpindleToolReleased", "SpindlePistonRetracted", "PressureSensor"]);

        module.Update(runtime, TimeSpan.Zero);
        Assert.Equal(4, runtime.InputWrites);
        Assert.True(runtime.Inputs["SpindleToolClamped"]);
        Assert.False(runtime.Inputs["SpindleToolReleased"]);
        for (int index = 0; index < 100; index++)
            module.Update(runtime, TimeSpan.FromMilliseconds(10));
        Assert.Equal(4, runtime.InputWrites);

        runtime.Outputs["SpindleToolRelease"] = true;
        module.Update(runtime, TimeSpan.FromMilliseconds(10));
        Assert.False(runtime.Inputs["SpindlePistonRetracted"]);
        Assert.False(runtime.Inputs["SpindleToolReleased"]);
        Assert.Equal(5, runtime.InputWrites);
        module.Update(runtime, TimeSpan.FromMilliseconds(10));
        Assert.False(runtime.Inputs["SpindleToolClamped"]);
        Assert.True(runtime.Inputs["SpindleToolReleased"]);
        Assert.Equal(7, runtime.InputWrites);

        module.Reset();
        module.Update(runtime, TimeSpan.Zero);
        Assert.Equal(11, runtime.InputWrites);
    }

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

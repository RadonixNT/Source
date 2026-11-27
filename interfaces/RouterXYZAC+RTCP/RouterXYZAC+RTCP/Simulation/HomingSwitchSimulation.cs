namespace RouterXYZACRTCP.Simulation;

/// <summary>
/// Short-travel training sensor. Captures a nearby switch once when its owning Home
/// program starts; search, stop, release and coordinate reset remain PLC operations.
/// </summary>
internal sealed class HomingSwitchSimulation : ISimulationModule
{
    private readonly string inputSignal;
    private readonly Func<bool> homeActive;
    private readonly Func<PositionSwitchDefinition> captureSwitch;
    private PositionSwitchSimulation? activeSwitch;
    private bool publishedInactive;

    internal HomingSwitchSimulation(string axisName, Func<bool> homeActive,
        Func<PositionSwitchDefinition> captureSwitch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(axisName);
        this.homeActive = homeActive ?? throw new ArgumentNullException(nameof(homeActive));
        this.captureSwitch = captureSwitch ?? throw new ArgumentNullException(nameof(captureSwitch));
        inputSignal = "HomeSensor" + axisName;
        Name = "RouterXYZACRTCP.HomeSwitch." + axisName;
        DrivenInputs = [inputSignal];
    }

    public string Name { get; }
    public IReadOnlyCollection<string> DrivenInputs { get; }

    // Reconnect and cancellation discard the old coordinate frame.
    public void Reset()
    {
        activeSwitch = null;
        publishedInactive = false;
    }

    public void Update(ISimulationRuntime runtime, TimeSpan elapsed)
    {
        if (!homeActive())
        {
            if (activeSwitch is not null || !publishedInactive)
                runtime.SetDigitalInput(inputSignal, false);
            activeSwitch = null;
            publishedInactive = true;
            return;
        }

        // Allocate only at Home admission, never during search or release ticks.
        // The threshold stays fixed through stopped phases and coordinate resets.
        if (activeSwitch is null)
        {
            PositionSwitchDefinition definition = captureSwitch();
            if (definition.InputSignal != inputSignal)
                throw new InvalidOperationException("Home sensor ownership changed.");
            activeSwitch = new PositionSwitchSimulation(Name, [definition]);
        }
        publishedInactive = false;
        activeSwitch.Update(runtime, elapsed);
    }
}

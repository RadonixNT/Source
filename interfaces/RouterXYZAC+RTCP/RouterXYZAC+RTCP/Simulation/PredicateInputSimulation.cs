namespace RouterXYZACRTCP.Simulation;

/// <summary>Publishes an interface-supplied machine-state predicate as one simulated input.</summary>
public sealed class PredicateInputSimulation : ISimulationModule
{
    private readonly string inputSignal;
    private readonly string[] drivenInputs;
    private readonly Func<bool> valueProvider;
    private bool? published;

    public PredicateInputSimulation(string name, string inputSignal, Func<bool> valueProvider)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Module name is required.", nameof(name))
            : name.Trim();
        this.inputSignal = string.IsNullOrWhiteSpace(inputSignal)
            ? throw new ArgumentException("Input signal name is required.", nameof(inputSignal))
            : inputSignal.Trim();
        this.valueProvider = valueProvider ?? throw new ArgumentNullException(nameof(valueProvider));
        drivenInputs = [this.inputSignal];
    }

    public string Name { get; }
    public IReadOnlyCollection<string> DrivenInputs => drivenInputs;

    public void Reset() => published = null;

    public void Update(ISimulationRuntime runtime, TimeSpan elapsed)
    {
        bool value = valueProvider();
        if (published == value)
            return;
        runtime.SetDigitalInput(inputSignal, value);
        published = value;
    }
}

namespace RouterXYZACRTCP.Simulation;

/// <summary>
/// One output-to-input mechanical relationship. Delays qualify the desired input
/// state continuously; Inverted reverses the output command before qualification.
/// </summary>
public sealed record DigitalFeedbackDefinition(
    string OutputSignal,
    string InputSignal,
    TimeSpan ActivationTime,
    TimeSpan DeactivationTime,
    bool Inverted = false);

/// <summary>Applies delayed actuator-output feedback to simulated input signals.</summary>
public sealed class DigitalFeedbackSimulation : ISimulationModule
{
    private readonly DigitalFeedbackDefinition[] definitions;
    private readonly string[] drivenInputs;
    private readonly Dictionary<string, FeedbackState> states = new(StringComparer.OrdinalIgnoreCase);

    public DigitalFeedbackSimulation(string name, IEnumerable<DigitalFeedbackDefinition> definitions)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Module name is required.", nameof(name)) : name.Trim();
        this.definitions = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
        if (this.definitions.Length == 0)
            throw new ArgumentException("At least one feedback definition is required.", nameof(definitions));
        foreach (DigitalFeedbackDefinition item in this.definitions)
        {
            if (string.IsNullOrWhiteSpace(item.OutputSignal) || string.IsNullOrWhiteSpace(item.InputSignal) ||
                item.ActivationTime < TimeSpan.Zero || item.DeactivationTime < TimeSpan.Zero)
            {
                throw new ArgumentException("Feedback definitions must contain valid names and nonnegative delays.", nameof(definitions));
            }
        }
        drivenInputs = this.definitions.Select(item => item.InputSignal).ToArray();
    }

    public string Name { get; }
    public IReadOnlyCollection<string> DrivenInputs => drivenInputs;

    public void Reset() => states.Clear();

    public void Update(ISimulationRuntime runtime, TimeSpan elapsed)
    {
        foreach (DigitalFeedbackDefinition item in definitions)
        {
            bool requested = runtime.GetDigitalOutput(item.OutputSignal);
            bool desired = item.Inverted ? !requested : requested;
            if (!states.TryGetValue(item.InputSignal, out FeedbackState? state))
            {
                state = new FeedbackState(desired);
                states.Add(item.InputSignal, state);
            }

            if (state.Target != desired)
            {
                state.Target = desired;
                state.Pending = TimeSpan.Zero;
            }

            if (state.HasPublished && state.Published == desired)
                continue;

            state.Pending += elapsed;
            TimeSpan delay = desired ? item.ActivationTime : item.DeactivationTime;
            if (state.Pending < delay)
                continue;

            runtime.SetDigitalInput(item.InputSignal, desired);
            state.Published = desired;
            state.HasPublished = true;
            state.Pending = TimeSpan.Zero;
        }
    }

    private sealed class FeedbackState(bool target)
    {
        internal bool Target { get; set; } = target;
        internal bool Published { get; set; }
        internal bool HasPublished { get; set; }
        internal TimeSpan Pending { get; set; }
    }
}

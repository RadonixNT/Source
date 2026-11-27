namespace CutterXYZ.Simulation;

/// <summary>Which side of the configured machine-coordinate threshold closes the sensor.</summary>
public enum PositionSwitchSide
{
    AtOrBelow,
    AtOrAbove
}

/// <summary>
/// One axis sensor in engineering units. Hysteresis extends the active side on
/// release only, preventing repeated toggles near the trigger coordinate.
/// </summary>
public sealed record PositionSwitchDefinition(
    string AxisName,
    string InputSignal,
    double TriggerPosition,
    PositionSwitchSide ActiveSide,
    double Hysteresis = 0.05);

/// <summary>Simulates one or more physical position switches from live axis feedback.</summary>
public sealed class PositionSwitchSimulation : ISimulationModule
{
    private readonly PositionSwitchDefinition[] switches;
    private readonly string[] drivenInputs;
    private readonly Dictionary<string, bool> states = new(StringComparer.OrdinalIgnoreCase);

    public PositionSwitchSimulation(string name, IEnumerable<PositionSwitchDefinition> switches)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Module name is required.", nameof(name)) : name.Trim();
        this.switches = switches?.ToArray() ?? throw new ArgumentNullException(nameof(switches));
        if (this.switches.Length == 0)
            throw new ArgumentException("At least one position switch is required.", nameof(switches));
        foreach (PositionSwitchDefinition item in this.switches)
        {
            if (string.IsNullOrWhiteSpace(item.AxisName) || string.IsNullOrWhiteSpace(item.InputSignal) ||
                !double.IsFinite(item.TriggerPosition) || !double.IsFinite(item.Hysteresis) || item.Hysteresis < 0)
            {
                throw new ArgumentException("Position switch definitions must contain valid names and finite limits.", nameof(switches));
            }
        }
        drivenInputs = this.switches.Select(item => item.InputSignal).ToArray();
    }

    public string Name { get; }
    public IReadOnlyCollection<string> DrivenInputs => drivenInputs;

    public void Reset() => states.Clear();

    public void Update(ISimulationRuntime runtime, TimeSpan elapsed)
    {
        foreach (PositionSwitchDefinition item in switches)
        {
            double position = runtime.GetAxisPosition(item.AxisName);
            bool active = states.GetValueOrDefault(item.InputSignal);
            bool next = item.ActiveSide switch
            {
                PositionSwitchSide.AtOrBelow => active
                    ? position <= item.TriggerPosition + item.Hysteresis
                    : position <= item.TriggerPosition,
                PositionSwitchSide.AtOrAbove => active
                    ? position >= item.TriggerPosition - item.Hysteresis
                    : position >= item.TriggerPosition,
                _ => throw new ArgumentOutOfRangeException(nameof(item.ActiveSide))
            };
            if (states.TryGetValue(item.InputSignal, out bool previous) && previous == next)
                continue;
            runtime.SetDigitalInput(item.InputSignal, next);
            states[item.InputSignal] = next;
        }
    }
}

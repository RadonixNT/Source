using RouterXYZACRTCP.Simulation;

namespace RouterXYZACRTCP;

/// <summary>Models spindle rotation feedback and coast-down with a fitted cutter.</summary>
internal sealed class SpindleSimulation(
    IReadOnlyCollection<string> drivenInputs) : ISimulationModule
{
    private bool rotation;
    private TimeSpan coastTime, pulseTime;
    private readonly bool[] publishedInputs = new bool[drivenInputs.Count];
    private bool inputsPublished;

    public string Name => "RouterXYZACRTCP.Spindle";
    public IReadOnlyCollection<string> DrivenInputs => drivenInputs;

    public void Reset()
    {
        rotation = false;
        coastTime = pulseTime = TimeSpan.Zero;
        inputsPublished = false;
    }

    public void Update(ISimulationRuntime runtime, TimeSpan elapsed)
    {
        bool running = runtime.GetDigitalOutput("SpindleCW") || runtime.GetDigitalOutput("SpindleCCW");
        coastTime = running ? TimeSpan.FromSeconds(1) : TimeSpan.FromTicks(Math.Max(0, (coastTime - elapsed).Ticks));
        if (running || coastTime > TimeSpan.Zero)
        {
            pulseTime += elapsed;
            if (pulseTime >= TimeSpan.FromMilliseconds(100))
            {
                rotation = !rotation;
                pulseTime = TimeSpan.Zero;
            }
        }
        else pulseTime = TimeSpan.Zero;

        int inputIndex = 0;
        foreach (string name in DrivenInputs)
        {
            bool value = name switch
            {
                "SpindleToolClamped" => true,
                "SpindleToolReleased" => false,
                "SpindlePistonRetracted" => true,
                "SpindleRotationSensor" => rotation,
                _ => true // Healthy thermal, pressure and drive-ready feedback.
            };
            if (!inputsPublished || publishedInputs[inputIndex] != value)
            {
                runtime.SetDigitalInput(name, value);
                publishedInputs[inputIndex] = value;
            }
            inputIndex++;
        }
        inputsPublished = true;
    }
}

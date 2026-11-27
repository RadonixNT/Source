using RouterXYZBCTC.Simulation;

namespace RouterXYZBCTC;

/// <summary>
/// Models release/clamp delay, physical pocket pickup and rotation coast-down.
/// Grip feedback precedes CurrentTool commits so the same PLC confirmation path
/// is exercised in simulation. The host gives this module exclusive ownership
/// of DrivenInputs; unchanged values therefore need no repeated controller write.
/// </summary>
internal sealed class SpindleSimulation(
    Func<bool> initialToolPresent,
    Func<ISimulationRuntime, bool> toolAtPickup,
    TimeSpan releaseTime,
    TimeSpan clampTime,
    IReadOnlyCollection<string> drivenInputs) : ISimulationModule
{
    private bool initialized, released, targetRelease, toolPresent, rotation;
    private TimeSpan actuatorTime, coastTime, pulseTime;
    private readonly bool[] publishedInputs = new bool[drivenInputs.Count];
    private bool inputsPublished;

    public string Name => "RouterXYZBCTC.Spindle";
    public IReadOnlyCollection<string> DrivenInputs => drivenInputs;

    public void Reset()
    {
        initialized = released = targetRelease = toolPresent = rotation = false;
        actuatorTime = coastTime = pulseTime = TimeSpan.Zero;
        inputsPublished = false;
    }

    public void Update(ISimulationRuntime runtime, TimeSpan elapsed)
    {
        if (!initialized)
        {
            toolPresent = initialToolPresent();
            initialized = true;
        }
        bool release = runtime.GetDigitalOutput("SpindleToolRelease");
        if (release != targetRelease)
        {
            targetRelease = release;
            actuatorTime = TimeSpan.Zero;
        }
        actuatorTime += elapsed;
        if (released != targetRelease && actuatorTime >= (targetRelease ? releaseTime : clampTime))
        {
            released = targetRelease;
            // Presence is established by mechanics at the pocket, not by the
            // recorded tool number (which is committed only after PLC success).
            toolPresent = !released && toolAtPickup(runtime);
        }

        bool running = runtime.GetDigitalOutput("SpindleCW") || runtime.GetDigitalOutput("SpindleCCW");
        coastTime = running ? TimeSpan.FromSeconds(1) : coastTime - elapsed;
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
                "SpindleToolClamped" => toolPresent && !released,
                "SpindleToolReleased" => released,
                "SpindlePistonRetracted" => !released && !targetRelease,
                "SpindleRotationSensor" => rotation,
                "SpindleToolReleaseButton" => false,
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

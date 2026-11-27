using NT;
using RouterXYZTC.Simulation;

namespace RouterXYZTC;

/// <summary>
/// Hard-simulation model for the tool setter. Simulated physical tool lengths
/// are captured when the machine simulation is created, independently of the
/// measured Tool.Length values that the measurement command later writes.
/// </summary>
internal sealed class ToolProbeSimulation(
    Func<PointD3> probePosition,
    Func<long> currentTool,
    IReadOnlyList<double> physicalToolLengths) : ISimulationModule
{
    private const double HorizontalTolerance = 0.5;
    private const double ReleaseHysteresis = 0.05;
    private static readonly string[] drivenInputs = ["ToolHeightSensor"];
    private bool active;
    private bool? published;

    public string Name => "RouterXYZTC.ToolProbe";
    public IReadOnlyCollection<string> DrivenInputs => drivenInputs;

    public void Reset()
    {
        active = false;
        published = null;
    }

    public void Update(ISimulationRuntime runtime, TimeSpan elapsed)
    {
        PointD3 probe = probePosition();
        long toolNumber = currentTool();
        double toolLength = toolNumber is >= 1 && toolNumber <= int.MaxValue
            && toolNumber <= physicalToolLengths.Count
                ? physicalToolLengths[(int)toolNumber - 1]
                : 0.0;
        double contactZ = probe.Z + toolLength;
        bool overProbe = Math.Abs(runtime.GetAxisPosition("X") - probe.X) <= HorizontalTolerance
            && Math.Abs(runtime.GetAxisPosition("Y") - probe.Y) <= HorizontalTolerance;
        double threshold = contactZ + (active ? ReleaseHysteresis : 0.0);
        active = overProbe && runtime.GetAxisPosition("Z") <= threshold;
        if (published != active)
        {
            runtime.SetDigitalInput("ToolHeightSensor", active);
            published = active;
        }
    }
}

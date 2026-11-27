namespace RouterXYZBCTC.Simulation;

/// <summary>
/// Runtime boundary used by simulation modules. Implementations own controller-specific
/// addressing; modules operate only on semantic signal and axis names.
/// </summary>
public interface ISimulationRuntime
{
    /// <summary>Notifies the host to start/reset modules when connection availability changes.</summary>
    event EventHandler? AvailabilityChanged;
    /// <summary>True only for an Online Hard Simulation session, never a hardware session.</summary>
    bool IsHardSimulationOnline { get; }
    /// <summary>Reads actual axis position in the machine's configured engineering units.</summary>
    double GetAxisPosition(string axisName);
    /// <summary>Reads a semantic output command; it is not physical feedback.</summary>
    bool GetDigitalOutput(string signalName);
    /// <summary>Publishes an owned semantic input; reject writes outside Hard Simulation.</summary>
    void SetDigitalInput(string signalName, bool value);
    /// <summary>Clears inputs driven by this runtime when the host stops or is disposed.</summary>
    void ResetDrivenInputs();
}

/// <summary>One independent piece of simulated machine behavior.</summary>
public interface ISimulationModule
{
    /// <summary>Stable diagnostic identity; unique within one host.</summary>
    string Name { get; }
    /// <summary>Fixed input ownership for this module; duplicates across modules are rejected.</summary>
    IReadOnlyCollection<string> DrivenInputs { get; }
    /// <summary>Clears history so subsequent updates republish initial state after any configured delay.</summary>
    void Reset();
    /// <summary>Performs bounded work with measured elapsed time; must not wait or access UI controls.</summary>
    void Update(ISimulationRuntime runtime, TimeSpan elapsed);
}

/// <summary>Identifies the module whose failure stopped the entire simulated machine host.</summary>
public sealed class SimulationFaultEventArgs(string moduleName, Exception error) : EventArgs
{
    public string ModuleName { get; } = moduleName;
    public Exception Error { get; } = error;
}

using NT;
using NT.Controller;
using NT.Controller.PLC;

namespace RouterXYZACTC.Simulation;

/// <summary>
/// Controller adapter for this interface's machine simulation. Physical inputs use the existing
/// Hard Simulation input image; unassigned inputs use their independent PLC storage.
/// </summary>
public sealed class ControllerSimulationRuntime : ISimulationRuntime, IDisposable
{
    private readonly ControllerWrapper controller;
    private readonly Dictionary<string, LOGIC> independentSignals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> drivenInputs = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    public ControllerSimulationRuntime(ControllerWrapper controller)
    {
        this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
        controller.ConnectionStateChanged += ConnectionStateChanged;
    }

    public event EventHandler? AvailabilityChanged;

    public bool IsHardSimulationOnline =>
        !disposed &&
        controller.ConnectionState == ConnectionState.Online &&
        controller.MotionController.SimulationMode == SimulationModes.Hard;

    public double GetAxisPosition(string axisName)
    {
        ThrowIfDisposed();
        return controller.GetAxisByName(axisName).ActualPosition;
    }

    public bool GetDigitalOutput(string signalName)
    {
        ThrowIfDisposed();
        PLCIOMapping mapping = GetMapping(signalName, PLCIOMappingType.DigitalOutput);
        return mapping.UsesIndependentStorage
            ? GetIndependentLogic(mapping.Name).Value
            : mapping.GetLogic();
    }

    public void SetDigitalInput(string signalName, bool value)
    {
        ThrowIfDisposed();
        if (!IsHardSimulationOnline)
            throw new InvalidOperationException("Simulation inputs can only change while Hard Simulation is online.");

        PLCIOMapping mapping = GetMapping(signalName, PLCIOMappingType.DigitalInput);
        if (mapping.IsPhysicalTarget && TryParseDigitalInput(mapping.Target, out int bitNumber))
        {
            ulong mask = 1UL << bitNumber;
            if (value) controller.PLC.Advanced.SetDigitalInput(mask);
            else controller.PLC.Advanced.ClearDigitalInput(mask);
        }
        else if (!mapping.Enabled || mapping.Target.Length == 0)
        {
            GetIndependentLogic(mapping.Name).Value = value;
        }
        else
        {
            throw new InvalidOperationException(
                $"Simulation input '{mapping.Name}' uses fixed or unsupported target '{mapping.Target}'.");
        }

        drivenInputs[mapping.Name] = value;
    }

    public void ResetDrivenInputs()
    {
        if (disposed)
            return;

        if (IsHardSimulationOnline)
        {
            foreach (string signalName in drivenInputs.Where(item => item.Value).Select(item => item.Key).ToArray())
            {
                try
                {
                    SetDigitalInput(signalName, false);
                }
                catch (InvalidOperationException)
                {
                    // A concurrent disconnect resets the complete runtime input image.
                    break;
                }
            }
        }

        drivenInputs.Clear();
    }

    public void Dispose()
    {
        if (disposed)
            return;
        ResetDrivenInputs();
        disposed = true;
        controller.ConnectionStateChanged -= ConnectionStateChanged;
    }

    private void ConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        => AvailabilityChanged?.Invoke(this, EventArgs.Empty);

    private PLCIOMapping GetMapping(string signalName, PLCIOMappingType expectedType)
    {
        if (string.IsNullOrWhiteSpace(signalName) ||
            !controller.PLC.IOMappings.TryGet(signalName.Trim(), out PLCIOMapping? mapping) ||
            mapping is null)
        {
            throw new KeyNotFoundException($"PLC I/O signal '{signalName}' is not defined.");
        }

        if (mapping.Type != expectedType)
            throw new InvalidOperationException($"PLC I/O signal '{mapping.Name}' is not a {expectedType}.");
        return mapping;
    }

    private LOGIC GetIndependentLogic(string signalName)
    {
        if (!independentSignals.TryGetValue(signalName, out LOGIC? value))
        {
            value = controller.PLC.SharedVariables[signalName];
            independentSignals.Add(signalName, value);
        }
        return value;
    }

    private static bool TryParseDigitalInput(string target, out int bitNumber)
    {
        bitNumber = -1;
        if (string.IsNullOrWhiteSpace(target) || !target.StartsWith("DM", StringComparison.OrdinalIgnoreCase))
            return false;
        int separator = target.IndexOf(".IN", StringComparison.OrdinalIgnoreCase);
        return separator > 2 &&
            int.TryParse(target.AsSpan(2, separator - 2), out int module) &&
            int.TryParse(target.AsSpan(separator + 3), out int channel) &&
            module is >= 0 and < 8 && channel is >= 0 and < 8 &&
            (bitNumber = (module * 8) + channel) < 64;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}

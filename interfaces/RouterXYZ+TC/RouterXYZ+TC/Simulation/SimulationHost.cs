using Timer = System.Threading.Timer;

namespace RouterXYZTC.Simulation;

/// <summary>
/// Owns the lifecycle and serialized periodic execution of removable simulation modules.
/// The host remains dormant unless its runtime reports an online Hard Simulation session.
/// </summary>
public sealed class SimulationHost : IDisposable
{
    private readonly object gate = new();
    private readonly ISimulationRuntime runtime;
    private readonly ISimulationModule[] modules;
    private readonly TimeSpan interval;
    private Timer? timer;
    private long previousTick;
    private bool disposed;

    public SimulationHost(
        ISimulationRuntime runtime,
        IEnumerable<ISimulationModule> modules,
        TimeSpan? interval = null)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.modules = modules?.ToArray() ?? throw new ArgumentNullException(nameof(modules));
        if (this.modules.Any(module => module is null))
            throw new ArgumentException("A simulation module cannot be null.", nameof(modules));
        if (this.modules.Select(module => module.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != this.modules.Length)
            throw new ArgumentException("Simulation module names must be unique.", nameof(modules));
        string[] duplicateInputs = this.modules
            .SelectMany(module => module.DrivenInputs)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateInputs.Length != 0)
            throw new ArgumentException(
                $"A simulated input can have only one owner: {string.Join(", ", duplicateInputs)}.",
                nameof(modules));

        this.interval = interval ?? TimeSpan.FromMilliseconds(10);
        if (this.interval < TimeSpan.FromMilliseconds(1))
            throw new ArgumentOutOfRangeException(nameof(interval), "Simulation interval must be at least one millisecond.");

        runtime.AvailabilityChanged += RuntimeAvailabilityChanged;
        EvaluateAvailability();
    }

    public event EventHandler<SimulationFaultEventArgs>? Faulted;

    public bool IsRunning
    {
        get
        {
            lock (gate)
                return timer is not null;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            runtime.AvailabilityChanged -= RuntimeAvailabilityChanged;
            StopLocked();
        }

        if (runtime is IDisposable disposableRuntime)
            disposableRuntime.Dispose();
    }

    private void RuntimeAvailabilityChanged(object? sender, EventArgs e)
        => EvaluateAvailability();

    private void EvaluateAvailability()
    {
        lock (gate)
        {
            if (disposed)
                return;
            if (runtime.IsHardSimulationOnline)
                StartLocked();
            else
                StopLocked();
        }
    }

    private void StartLocked()
    {
        if (timer is not null)
            return;
        foreach (ISimulationModule module in modules)
            module.Reset();
        previousTick = Environment.TickCount64;
        timer = new Timer(Update, null, TimeSpan.Zero, interval);
    }

    private void StopLocked()
    {
        timer?.Dispose();
        timer = null;
        foreach (ISimulationModule module in modules)
            module.Reset();
        runtime.ResetDrivenInputs();
    }

    private void Update(object? state)
    {
        lock (gate)
        {
            if (disposed || timer is null || !runtime.IsHardSimulationOnline)
            {
                if (!disposed)
                    StopLocked();
                return;
            }

            long currentTick = Environment.TickCount64;
            TimeSpan elapsed = TimeSpan.FromMilliseconds(Math.Max(0, currentTick - previousTick));
            previousTick = currentTick;
            foreach (ISimulationModule module in modules)
            {
                try
                {
                    module.Update(runtime, elapsed);
                }
                catch (Exception error)
                {
                    StopLocked();
                    Faulted?.Invoke(this, new SimulationFaultEventArgs(module.Name, error));
                    return;
                }
            }
        }
    }
}

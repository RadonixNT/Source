using NT;
using NT.Controller.PLC;
using NT.LinkManager;
using PlcCore = NT.PLCWrapper;

namespace CutterXYZ;

/// <summary>
/// A Link source whose controller-owned value is sampled by the UI timer.
/// Publishing an edge asks LinkManager to read the source; it does not persist
/// or duplicate the value. Never raise these UI-facing events from the RT loop.
/// </summary>
internal interface ILiveRuntimeSource
{
    void PublishValueChanged();
}

/// <summary>
/// Marks live PLC commands/observations that Configuration Cancel must not restore.
/// Keep this distinction: rolling back a physical output is a new machine command.
/// </summary>
internal interface ILivePlcSource : ILiveRuntimeSource;

/// <summary>
/// Connects application-owned Links to PLC application variables without
/// introducing a dependency between NT-LinkManager and NT-Controller.
/// </summary>
internal static class PLCVariableBridge
{
    /// <summary>Exposes a persisted Positive/Negative selection as read-only +1/-1.</summary>
    internal static void AddDirectionApplicationVariable(PlcCore plc, TextLink link, string symbolName)
    {
        ArgumentNullException.ThrowIfNull(plc);
        ArgumentNullException.ThrowIfNull(link);
        link.Source = new PlcDirectionSource(plc.ApplicationVariables.AddInteger(
            symbolName, PlcDirectionSource.ToInteger(link.Value), PLCVariableAccess.ReadOnly));
    }

    private sealed class PlcDirectionSource(INTEGER variable) : ITextSource
    {
        public event EventHandler? ValueChanged { add { } remove { } }

        public string GetValue() => variable.Value switch
        {
            1 => "Positive",
            -1 => "Negative",
            _ => throw new InvalidOperationException("PLC direction must be +1 or -1.")
        };

        public void SetValue(string value) => variable.Value = ToInteger(value);

        internal static long ToInteger(string value) => value switch
        {
            "Positive" => 1,
            "Negative" => -1,
            _ => throw new ArgumentException("Direction must be Positive or Negative.", nameof(value))
        };
    }

    /// <summary>
    /// Registers a scalar Link under its own name and attaches the resulting typed
    /// PLC handle as the Link's live value source.
    /// </summary>
    internal static void AddApplicationVariable(
        PlcCore plc,
        Link link,
        PLCVariableAccess access = PLCVariableAccess.ReadWrite)
        => AddApplicationVariable(plc, link, link?.Name ?? string.Empty, access);

    /// <summary>
    /// Registers a scalar Link under an explicit PLC symbol name. Command Links
    /// never represent persistent PLC storage and are intentionally rejected.
    /// </summary>
    internal static void AddApplicationVariable(
        PlcCore plc,
        Link link,
        string symbolName,
        PLCVariableAccess access = PLCVariableAccess.ReadWrite)
    {
        ArgumentNullException.ThrowIfNull(plc);
        ArgumentNullException.ThrowIfNull(link);

        switch (link)
        {
            case IntegerLink integerLink:
                integerLink.Source = new PlcIntegerSource(
                    plc.ApplicationVariables.AddInteger(symbolName, integerLink.Value, access));
                break;
            case RealLink realLink:
                realLink.Source = new PlcRealSource(
                    plc.ApplicationVariables.AddReal(symbolName, realLink.Value, access));
                break;
            case LogicLink logicLink when logicLink is not CommandLink:
                logicLink.Source = new PlcLogicSource(
                    plc.ApplicationVariables.AddLogic(symbolName, logicLink.Value, access));
                break;
            default:
                throw new NotSupportedException(
                    $"Link type '{link.GetType().Name}' cannot be exposed as a PLC application variable.");
        }
    }

    /// <summary>
    /// Registers a calibration value with an application-side write guard in
    /// addition to its UI availability rule. Direct Link writes (including
    /// import/reset) must not bypass the offline-only contract.
    /// </summary>
    internal static void AddOfflineApplicationVariable(
        PlcCore plc,
        RealLink link,
        string symbolName,
        Func<bool> canWrite)
    {
        ArgumentNullException.ThrowIfNull(plc);
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(canWrite);
        link.Source = new PlcRealSource(
            plc.ApplicationVariables.AddReal(symbolName, link.Value, PLCVariableAccess.ReadOnly),
            canWrite);
    }

    /// <summary>
    /// Expands one persisted point into three read-only REAL symbols. The Link keeps
    /// ownership of the point and pushes accepted changes to stable PLC handles.
    /// </summary>
    internal static void AddApplicationVariable(
        PlcCore plc,
        VariableLink<PointD3> link,
        string symbolName)
    {
        ArgumentNullException.ThrowIfNull(plc);
        ArgumentNullException.ThrowIfNull(link);

        PointD3 initialValue = link.Value;
        REAL x = plc.ApplicationVariables.AddReal(
            $"{symbolName}.X",
            initialValue.X,
            PLCVariableAccess.ReadOnly);
        REAL y = plc.ApplicationVariables.AddReal(
            $"{symbolName}.Y",
            initialValue.Y,
            PLCVariableAccess.ReadOnly);
        REAL z = plc.ApplicationVariables.AddReal(
            $"{symbolName}.Z",
            initialValue.Z,
            PLCVariableAccess.ReadOnly);

        link.Changed += (_, _) =>
        {
            PointD3 value = link.Value;
            x.Value = value.X;
            y.Value = value.Y;
            z.Value = value.Z;
        };
    }

    private sealed class PlcIntegerSource : IIntegerSource
    {
        private readonly INTEGER variable;

        internal PlcIntegerSource(INTEGER variable)
        {
            this.variable = variable;
        }

        public event EventHandler? ValueChanged
        {
            // PLC handles do not publish events. LinkManager reads them during its
            // regular UI refresh, so retaining handlers would be misleading.
            add { }
            remove { }
        }

        public long GetValue() => variable.Value;

        public void SetValue(long value) => variable.Value = value;
    }

    private sealed class PlcRealSource : IRealSource
    {
        private readonly REAL variable;
        private readonly Func<bool>? canWrite;

        internal PlcRealSource(REAL variable, Func<bool>? canWrite = null)
        {
            this.variable = variable;
            this.canWrite = canWrite;
        }

        public event EventHandler? ValueChanged
        {
            add { }
            remove { }
        }

        public double GetValue() => variable.Value;

        public void SetValue(double value)
        {
            // Cancel may restore an unchanged calibration Link while online.
            // A no-op must neither fail nor publish a PLC write.
            if (canWrite is not null)
            {
                if (variable.Value == value) return;
                if (!canWrite())
                    throw new InvalidOperationException("This calibration setting can only be changed while the controller is offline.");
            }

            variable.Value = value;
        }
    }

    private sealed class PlcLogicSource : ILogicSource
    {
        private readonly LOGIC variable;

        internal PlcLogicSource(LOGIC variable)
        {
            this.variable = variable;
        }

        public event EventHandler? ValueChanged
        {
            add { }
            remove { }
        }

        public bool GetValue() => variable.Value;

        public void SetValue(bool value) => variable.Value = value;
    }
}

using NT.Controller;
using NT.LinkManager;

namespace RouterXYZACTC;

/// <summary>
/// Exposes the profile's stable INV0/INV1 slots through live typed Link sources.
/// SPEED is normalized to 0..1; RUN requests operation and STATE observes the
/// device state machine. These values are not evidence of attained spindle RPM.
/// Controller/Core own command ordering and non-RT device communication.
/// </summary>
internal sealed partial class ControllerLinks
{
    private void CreateInverterLinks()
    {
        inverterGroup = controllerGroup.AddGroup("Inverters", "Inverters", "Configured variable-speed devices.");
        inverterCount = inverterGroup.AddInteger("InverterCount", "Inverter Count", Initialize.DefaultInverterCount, 0, 2,
            "Number of inverters: 0 = none, 1 = INV0, 2 = INV0 and INV1. Disconnect and clear every ToolPath before changing. Referenced devices cannot be removed.");
        inverterCount.Permission = AccessPermission.Engineering;
        inverterCount.UnauthorizedView = AccessView.ReadOnly;
        inverterCount.Source = new InverterCountSource(inverterCount.Value, CanEditInverterCount);
        inverterCount.QueryEnabled += (_, e) => e.Enabled &= CanEditInverterCount();
        for (int index = 0; index < 2; index++)
        {
            string name = $"INV{index}";
            Inverter inverter = controller.MotionController.Inverters[index];
            inverter.Configuration.Enabled = index < inverterCount.Value;
            LinkGroup child = inverterGroup.AddGroup(name, name, "Speed and operating request.");
            RealLink output = child.AddReal("SPEED", "SPEED", 0, 0, 1, 4, "Requested speed: 0 is zero, 0.5 is half and 1 is full speed.");
            output.Source = RegisterLiveRuntimeSource(new InverterSpeedSource(inverter));
            LogicLink run = child.AddLogic("RUN", "RUN", false, "True requests operation; false requests disabling. Device readiness is reported separately.");
            run.Source = RegisterLiveRuntimeSource(new InverterRunSource(inverter));
            IntegerLink state = child.AddInteger("STATE", "STATE", 0, 0, 7,
                "Observed device state; the same values as PLC INVERTERSTATE constants. Enabled does not confirm arrival at speed.");
            state.View = AccessView.ReadOnly;
            state.Source = RegisterLiveRuntimeSource(new InverterStateSource(inverter));
            output.QueryEnabled += InverterCanWrite;
            run.QueryEnabled += InverterCanWrite;
        }
        inverterCount.Changed += (_, _) => RefreshInverterVisibility();
        RefreshInverterVisibility();
    }

    private void InverterCanWrite(object? sender, QueryEventArgs e)
        => e.Enabled &= controller.IsConnected && !controller.PLC.IsApplying;

    private sealed class InverterSpeedSource(Inverter inverter) : IRealSource, ILivePlcSource
    {
        public event EventHandler? ValueChanged;
        public void PublishValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);
        public double GetValue() => inverter.Speed;
        public void SetValue(double value) => inverter.Speed = value;
    }

    private sealed class InverterStateSource(Inverter inverter) : IIntegerSource, ILivePlcSource
    {
        public event EventHandler? ValueChanged;
        public void PublishValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);
        public long GetValue() => (long)inverter.State;
        public void SetValue(long value) => throw new InvalidOperationException("Device state is read-only.");
    }
    private sealed class InverterRunSource(Inverter inverter) : ILogicSource, ILivePlcSource
    {
        public event EventHandler? ValueChanged;
        public void PublishValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);
        public bool GetValue() => inverter.Run;
        public void SetValue(bool value) => inverter.Run = value;
    }
}

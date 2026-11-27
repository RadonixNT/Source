using NT.Controller;
using NT.Controller.PLC;
using NT.Diagnostics;
using NT.LinkManager;

namespace RouterXYZACRTCP;

/// <summary>
/// Resolves configured spindle capabilities into stable PLC application flags.
/// The forward run output enables spindle operation. Optional feedback is checked live by Background.
/// </summary>
internal sealed partial class ControllerLinks
{
    private LOGIC? spindleFeedbackConfigured;
    private LOGIC? spindleReverseConfigured;

    private RetainedSpindleSpeedSource? spindleSpeedSource;
    private RealLink? spindleSpeedSetting;

    internal void BindSpindleSpeed(RealLink setting, IntegerLink speedStep)
    {
        var mapping = GetOutputMapping("SpindleSpeed", PLCIOMappingType.AnalogOutput);
        spindleSpeedSource = new(this, mapping, setting, speedStep);
        spindleSpeedSetting = setting;
        setting.Source = spindleSpeedSource;
        setting.QueryEnabled += (_, e) => e.Enabled &= spindleSpeedSource.CanEdit;
    }

    internal bool IsLiveSpindleSpeed(Link link)
        => controller.IsConnected && ReferenceEquals(link, spindleSpeedSetting);

    internal void RefreshSpindleSpeed(bool captureOnly = false) => spindleSpeedSource?.Refresh(captureOnly);

    private sealed class RetainedSpindleSpeedSource(
        ControllerLinks owner, PLCIOMapping output, RealLink setting, IntegerLink speedStep) : IRealSource
    {
        // AM outputs quantize a normalized command to 0..10000. Preserve the
        // selected RPM when the reported output is the quantized echo of it.
        private const double AnalogHalfStep = 0.0000501;
        private double value = setting.Value;
        private double scale;
        private bool restored;
        private long retryAfter;
        private bool writeFailureReported;
        private (double Before, double Requested, long Deadline)? pending;

        public event EventHandler? ValueChanged;
        // A range edit can clamp the retained value without issuing an output command.
        public double GetValue() => Math.Clamp(value, setting.Minimum, setting.Maximum);

        private bool HasOutput => output.IsPhysicalTarget || output.UsesIndependentStorage;
        private bool ManualOutputAvailable => output.IsPhysicalTarget ||
            (output.UsesIndependentStorage && owner.controller.MotionController.SimulationMode == SimulationModes.Hard);

        internal bool CanEdit => owner.controller.ConnectionState.AllowsOfflineConfiguration()
            ? !owner.mainForm.IsConnectionTransition && !owner.mainForm.IsProgramLoading
            : owner.controller.IsConnected && ManualOutputAvailable && !owner.controller.PLC.IsApplying
                && owner.mainForm.CanAdjustSpindleSpeed;

        public void SetValue(double requested)
        {
            if (requested == value) return;
            double minimum = setting.Minimum;
            double maximum = setting.Maximum;
            if (requested > minimum && requested < maximum)
                requested = minimum + Math.Round((requested - minimum) / speedStep.Value,
                    MidpointRounding.AwayFromZero) * speedStep.Value;
            requested = Math.Clamp(requested, minimum, maximum);
            if (value == requested) return;
            if (owner.controller.ConnectionState.AllowsOfflineConfiguration())
            {
                value = requested;
                return;
            }
            if (!CanEdit) return;
            Queue(requested, output.GetReal());
        }

        internal void Refresh(bool captureOnly)
        {
            if (!owner.controller.IsConnected)
            {
                pending = null;
                restored = false;
                retryAfter = 0;
                writeFailureReported = false;
                return;
            }
            if (owner.controller.PLC.IsApplying || !HasOutput) return;

            double actual = output.GetReal();
            if (!double.IsFinite(actual)) return;
            double maximum = setting.Maximum;
            if (!restored || scale != maximum)
            {
                if (captureOnly || Environment.TickCount64 < retryAfter) return;
                try
                {
                    double before = value;
                    Queue(Math.Clamp(value, setting.Minimum, maximum), actual);
                    if (value != before) ValueChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (InvalidOperationException) when (!owner.controller.IsConnected || owner.controller.PLC.IsApplying)
                {
                    // A disconnect or PLC Apply won the admission race.
                }
                catch (InvalidOperationException error)
                {
                    if (!writeFailureReported)
                        NTLog.Warning($"Could not restore the retained spindle speed: {error.Message}");
                    writeFailureReported = true;
                    retryAfter = Environment.TickCount64 + 500;
                }
                return;
            }

            if (pending is { } request)
            {
                if (Matches(actual, request.Requested))
                {
                    pending = null;
                    return;
                }
                if (actual == request.Before && Environment.TickCount64 < request.Deadline) return;
                pending = null;
            }
            if (Matches(actual, value / maximum)) return;

            double requestedRpm = Math.Clamp(actual * maximum, setting.Minimum, maximum);
            if (value == requestedRpm) return;
            value = requestedRpm;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Queue(double requested, double actual)
        {
            double maximum = setting.Maximum;
            double normalized = requested / maximum;
            if (!Matches(actual, normalized) || pending is not null)
            {
                output.SetReal(normalized);
                pending = (actual, normalized, Environment.TickCount64 + 500);
            }
            value = requested;
            scale = maximum;
            restored = true;
            writeFailureReported = false;
        }

        private bool Matches(double actual, double normalized)
        {
            double tolerance = output.IsPhysicalTarget &&
                !PLCIOMappingCollection.TryParseInverterTarget(output.Target, out _) ? AnalogHalfStep : 1e-12;
            return Math.Abs(actual - normalized) <= tolerance;
        }
    }

    private void CreateSpindleSettings()
    {
        spindleFeedbackConfigured = controller.PLC.ApplicationVariables.AddLogic(
            "SpindleFeedbackConfigured", false, PLCVariableAccess.ReadOnly);
        spindleReverseConfigured = controller.PLC.ApplicationVariables.AddLogic(
            "SpindleReverseConfigured", false, PLCVariableAccess.ReadOnly);
        RefreshSpindleConfiguration();
    }

    // Mapping changes are allowed offline. Publish resolved configuration once,
    // never search signal names or evaluate the Link graph in the RT scan.
    private void RefreshSpindleConfiguration()
    {
        if (spindleFeedbackConfigured is null) return;
        bool simulation = controller.ConnectionState == NT.Controller.ConnectionState.Online
            && controller.MotionController.SimulationMode == NT.Controller.SimulationModes.Hard;
        bool Output(string name, bool allowVirtual = false)
        {
            PLCIOMapping mapping = controller.PLC.IOMappings[name];
            return mapping.IsPhysicalTarget || (simulation && mapping.UsesIndependentStorage)
                || (mapping.Enabled && allowVirtual && mapping.Target is
                    PLCIOMapping.VirtualTrueTarget or PLCIOMapping.VirtualFalseTarget);
        }

        spindleFeedbackConfigured.Value = Output("SpindleCW");
        // INVn.SPEED is gated by INVn.RUN and the current EtherCAT contract has
        // no reverse object. Merely assigning a CCW digital wire cannot add it.
        PLCIOMapping speed = controller.PLC.IOMappings["SpindleSpeed"];
        spindleReverseConfigured!.Value = Output("SpindleCCW")
            && !(speed.IsPhysicalTarget && PLCIOMappingCollection.TryParseInverterTarget(speed.Target, out _));
    }
}

/// <summary>Matches PLC ASSIGNED: optional interlocks apply only to enabled physical mappings.
/// Unavailable configured hardware is a mapping validation failure, not an absent sensor.
/// Constant/virtual targets and disabled mappings do not count as physical inputs.
/// </summary>
internal static class SpindleInterlocks
{
    internal static bool OptionalInputSatisfied(PLCIOMapping mapping, bool expected = true)
        => !mapping.IsPhysicalTarget || mapping.GetLogic() == expected;
}

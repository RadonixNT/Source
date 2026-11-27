using System.Globalization;
using NT.Controller;
using NT.LinkManager;
using NT.Tools;

namespace RouterXYZACRTCP;

public partial class MainForm
{
    private RtcpSettings rtcpSettings = null!;
    private RtcpModeSource rtcpModeSource = null!;
    private RtcpToolLengthSource? rtcpToolLengthSource;
    private CommandLink? activeRtcpJog;
    private RtcpSwitchStatus lastRtcpSwitchStatus;
    private RtcpJogStatus lastRtcpJogStatus;

    internal bool IsRtcpSelectedOrPending => controller.MotionController.RTCP.Mode == RtcpMode.RTCP
        || controller.MotionController.RTCP.SwitchStatus == RtcpSwitchStatus.Pending;

    internal bool ToolLengthBlocksProgram => rtcpToolLengthSource?.NeedsConfirmation == true;

    private bool CanSwitchRtcp() => IsMachineOnline && generalLinks.AccessSession.CanOperate
        && controller.MotionController.RTCP.Geometry is not null
        && controller.MotionController.RTCP.SwitchStatus != RtcpSwitchStatus.Pending
        && activeRtcpJog is null && !IsProgramLoading && !IsMotionOrProgramActive();

    private bool CanEditRtcpToolLength() => IsMachineOnline && generalLinks.AccessSession.CanOperate
        && controller.MotionController.RTCP.Geometry is not null
        && controller.MotionController.RTCP.SwitchStatus != RtcpSwitchStatus.Pending
        && activeRtcpJog is null && !IsProgramLoading && !IsMotionOrProgramActive()
        && mainToolPath.IsStopped && mainToolPath.Axes.All(axis => axis.IsStopped && axis.IsReadyToMove);

    private bool CanJogRtcp() => IsMachineOnline && generalLinks.AccessSession.CanOperate
        && CaptureCommandAvailability().CanJog && !generalLinks.JogModeLink.Value
        && !controllerLinks.IsPauseSwitchOpen
        && controller.MotionController.RTCP.Mode == RtcpMode.RTCP
        && controller.MotionController.RTCP.SwitchStatus != RtcpSwitchStatus.Pending
        && mainToolPath.IsHomed && mainToolPath.AreAxesReady;

    private void BindRtcpControls()
    {
        var rtcp = controller.MotionController.RTCP;
        rtcpModeSource = new(rtcp, CanSwitchRtcp);
        var toggle = Link.CreateLogic("RtcpEnabled", "RTCP Enabled", false, "Off", "On");
        toggle.Source = rtcpModeSource;
        toggle.QueryEnabled += (_, e) => e.Enabled &= CanSwitchRtcp();
        runtimeLinks.Add(toggle);
        toggle.Bind(R_RtcpEnabled);
        BindRtcpToolLength(rtcp);
        generalLinks.JogModeLink.Changed += (_, _) => StopRtcpJog();

        BindRtcpAxis("AP", "A", rtcpSettings.AP, R_JogNAP, R_JogPAP,
            R_JogVelocityAP, R_StepAP, R_PositionAP, R_GoToAP);
        BindRtcpAxis("CP", "C", rtcpSettings.CP, R_JogNCP, R_JogPCP,
            R_JogVelocityCP, R_StepCP, R_PositionCP, R_GoToCP);
        BindRtcpAxis("ZT", null, rtcpSettings.ZT, R_JogNZT, R_JogPZT,
            R_JogVelocityZT, R_StepZT, R_PositionZT, R_GoToZT);
    }

    private const string ToolLengthFormat = "0.######";

    private void BindRtcpToolLength(RTCP rtcp)
    {
        var setting = rtcpSettings.ToolLength;
        // The main form is available to operators. The underlying calibration
        // Link retains its engineering access and existing registry identity.
        var editorLink = Link.CreateReal("ActiveToolLength", setting.DisplayName,
            setting.Value, double.MinValue, double.MaxValue, setting.Precision);
        rtcpToolLengthSource = new(this, rtcp, setting);
        editorLink.Source = rtcpToolLengthSource;
        editorLink.QueryEnabled += (_, e) => e.Enabled &= CanEditRtcpToolLength();
        runtimeLinks.Add(editorLink);
        editorLink.Bind(R_ToolLength, options =>
        {
            options.DisplayFormat = ToolLengthFormat;
            options.CommitOnValidated = false;
        });
        RestoreToolLengthEditor();
    }

    private void UpdateToolLengthCaption()
    {
        R_ToolLength.AccessibleName = T(RouterXYZACRTCPTexts.ToolLengthMm);
        R_ToolLengthLabel.Text = T(rtcpToolLengthSource is { IsPending: true }
            ? RouterXYZACRTCPTexts.ToolLengthApplying
            : ToolLengthBlocksProgram ? RouterXYZACRTCPTexts.ToolLengthConfirm : RouterXYZACRTCPTexts.ToolLengthMm);
    }

    private void RestoreToolLengthEditor()
    {
        R_ToolLength.Text = rtcpSettings.ToolLength.Value.ToString(ToolLengthFormat, CultureInfo.CurrentCulture);
        UpdateToolLengthCaption();
        RequestRuntimeUiRefresh();
    }

    private void BindRtcpAxis(string name, string? rotary, RtcpSettings.JogLinks settings,
        sButton negative, sButton positive, sTextBox velocity, sTextBox step, sTextBox position, sButton goTo)
    {
        settings.Velocity.Bind(velocity, options => options.DisplayFormat = "0.###");
        settings.Step.Bind(step, options => options.DisplayFormat = "0.###");
        settings.Position.Bind(position, options => options.DisplayFormat = "0.###");
        BindJogDirection(negative, "Minus", -1);
        BindJogDirection(positive, "Plus", 1);

        // The Controller currently supports continuous compensated Jog only.
        CommandLink goToCommand = CommandLink.CreateCommand($"GoTo{name}", goTo.Text);
        goToCommand.ActionExecute += (_, _) => { };
        goToCommand.QueryEnabled += (_, e) => e.Enabled = false;
        runtimeLinks.Add(goToCommand);
        goToCommand.Bind(goTo);

        void BindJogDirection(sButton button, string suffix, int direction)
        {
            CommandLink command = CommandLink.CreateCommand($"Jog{name}{suffix}", button.Text);
            runtimeLinks.Add(command);
            command.QueryEnabled += (_, e) => e.Enabled &= CanJogRtcp()
                && (activeRtcpJog is null || activeRtcpJog == command);
            command.ActionBegin += (_, _) =>
            {
                if (activeRtcpJog is not null || !CanJogRtcp() || IsMotionOrProgramActive()) return;
                var rtcp = controller.MotionController.RTCP;
                if (rtcp.Geometry is not { } geometry) return;
                RtcpJogAxis axis = rotary is null ? RtcpJogAxis.ToolAxis
                    : controller.GetAxisIndex(rotary) == geometry.FirstRotary.AxisIndex
                        ? RtcpJogAxis.FirstRotary : RtcpJogAxis.SecondRotary;
                double speed = direction * settings.Velocity.Value * generalLinks.JogFeedPercentLink.Value / 100;
                if (rtcp.Jog(axis, speed)) activeRtcpJog = command;
            };
            command.ActionEnd += (_, _) =>
            {
                if (activeRtcpJog == command) StopRtcpJog();
            };
            command.Bind(button);
        }
    }

    internal void StopRtcpJog()
    {
        if (activeRtcpJog is null) return;
        activeRtcpJog = null;
        controller.MotionController.RTCP.StopJog();
    }

    private bool RefreshRtcpControls()
    {
        var rtcp = controller.MotionController.RTCP;
        if (activeRtcpJog is not null && !CanJogRtcp()) StopRtcpJog();
        RtcpSwitchStatus status = rtcp.SwitchStatus;
        RtcpJogStatus jogStatus = rtcp.JogStatus;
        bool changed = rtcpToolLengthSource?.Refresh(status) == true;
        changed |= rtcpModeSource.Refresh() || status != lastRtcpSwitchStatus || jogStatus != lastRtcpJogStatus;
        lastRtcpSwitchStatus = status;
        lastRtcpJogStatus = jogStatus;
        // Build a new display definition only when the accepted profile changes.
        RefreshMachineHead(rtcp.Geometry);
        return changed;
    }

    private sealed class RtcpToolLengthSource(MainForm form, RTCP rtcp, RealLink setting) : IRealSource
    {
        private double? requested;
        private double published = setting.Value;
        public event EventHandler? ValueChanged;
        internal bool IsPending => requested.HasValue;
        internal bool NeedsConfirmation { get; private set; }
        public double GetValue() => setting.Value;

        internal void ClearConfirmation()
        {
            if (!NeedsConfirmation) return;
            requested = null;
            NeedsConfirmation = false;
            form.RestoreToolLengthEditor();
        }

        public void SetValue(double value)
        {
            if (!form.CanEditRtcpToolLength())
            {
                form.RestoreToolLengthEditor();
                return;
            }

            // Reject out-of-range input instead of rounding it to a different tool length.
            if (value < setting.Minimum || value > setting.Maximum)
            {
                Reject("The requested tool length is outside the configured range.");
                return;
            }

            if (rtcp.Geometry?.ToolLengthMm == value)
            {
                Accept(value);
                return;
            }

            NeedsConfirmation = true;
            requested = value;
            form.RestoreToolLengthEditor();
            try
            {
                if (!rtcp.RequestToolLength(value))
                    Reject("The controller did not accept the tool length request.");
            }
            catch (ArgumentException error)
            {
                Reject(error.Message);
            }
        }

        internal bool Refresh(RtcpSwitchStatus status)
        {
            if (requested is double length && status != RtcpSwitchStatus.Pending)
            {
                // Geometry is the last acknowledged profile, including a
                // successful receipt observed as the connection is closing.
                if (rtcp.Geometry?.ToolLengthMm == length)
                    Accept(length);
                else
                    Reject($"The controller did not apply the tool length ({status}).");
                return true;
            }

            return PublishSetting();
        }

        private void Accept(double length)
        {
            requested = null;
            NeedsConfirmation = false;
            if (setting.Value != length)
            {
                setting.Value = length;
                form.ReportOperation(FormattableString.Invariant($"RTCP tool length applied: {length:G17} mm."));
            }
            PublishSetting();
            form.RestoreToolLengthEditor();
        }

        private void Reject(string reason)
        {
            requested = null;
            NeedsConfirmation = true;
            form.ReportOperation($"RTCP tool length requires confirmation. {reason}");
            form.RestoreToolLengthEditor();
        }

        private bool PublishSetting()
        {
            double length = setting.Value;
            if (published == length) return false;
            published = length;
            ValueChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }

    private sealed class RtcpModeSource(RTCP rtcp, Func<bool> canSwitch) : ILogicSource
    {
        private bool lastValue;
        public event EventHandler? ValueChanged;
        public bool GetValue() => rtcp.Mode == RtcpMode.RTCP;

        public void SetValue(bool value)
        {
            if (canSwitch()) rtcp.RequestMode(value ? RtcpMode.RTCP : RtcpMode.Normal);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        internal bool Refresh()
        {
            bool value = GetValue();
            if (value == lastValue) return false;
            lastValue = value;
            ValueChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}

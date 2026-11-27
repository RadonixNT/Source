using System.Diagnostics;
using NT;
using NT.Controller;
using NT.Diagnostics;
using NT.Localization;
using NT.Tools;

namespace RouterXYZACTC;

public partial class MainForm
{
    private static string PreparationText(string key) =>
        NTLocalization.Get(new NTTextId("Radonix.RouterXYZACTC.Preparation." + key));

    private async Task<bool> CheckPcReadinessAsync(ControllerRuntimeMode mode, CancellationToken token)
    {
        // NDIS has its own controller admission checks; it must not require RTACC setup.
        NT.Config.PcReadiness.Result result;
        try
        {
            result = await PcConnectionReadiness.InspectAsync(mode, NT.Config.PcReadiness.Inspect, token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            NTLog.Error($"PC readiness inspection failed: {error}");
            result = new(false, false, error.Message);
        }
        token.ThrowIfCancellationRequested();
        if (runtimeDisposed || IsDisposed) return false;
        if (result.Ready) return true;
        NTLog.Warning($"Hardware connection deferred: {result.Detail}");
        using var dialog = new sForm
        {
            Text = PreparationText("Title"), StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(560, 210), FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi
        };
        dialog.RightToLeft = NTLocalization.CurrentLanguageInfo.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
        dialog.RightToLeftLayout = NTLocalization.CurrentLanguageInfo.IsRightToLeft;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, AutoSize = false,
            Text = PreparationText(result.ResumeRequired ? "Resume" : "Needed") + "\n\n" + PreparationText("Consent") }, 0, 0);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var later = new sButton { Text = PreparationText("Later"), DialogResult = DialogResult.Cancel, Size = new Size(216, 36), CornerRadius = 10 };
        var open = new sButton { Text = PreparationText(result.ResumeRequired ? "Continue" : "Open"), DialogResult = DialogResult.OK, Size = new Size(216, 36), CornerRadius = 10 };
        actions.Controls.Add(later); actions.Controls.Add(open);
        layout.Controls.Add(actions, 0, 1); dialog.Controls.Add(layout);
        dialog.AcceptButton = later; dialog.CancelButton = later;
        AttachDialogTheme(dialog);
        dialog.CompleteFormInitialization();
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        token.ThrowIfCancellationRequested();
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "NT-Config.exe");
            if (!File.Exists(path)) throw new FileNotFoundException("NT-Config.exe is missing. Repair the application installation.", path);
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory });
        }
        catch (Exception error)
        {
            sMessage.Show(this, new SMessageRequest(PreparationText("LaunchFailed"))
            { Severity = SMessageSeverity.Warning, TechnicalDetails = error.Message }, RuntimeTheme);
        }
        // Closing the setup tool is not proof of readiness. A new Connect request rechecks it.
        return false;
    }
}

internal static class PcConnectionReadiness
{
    internal static async Task<NT.Config.PcReadiness.Result> InspectAsync(ControllerRuntimeMode mode,
        Func<NT.Config.PcReadiness.Result> inspect, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (mode is ControllerRuntimeMode.HardSimulation or ControllerRuntimeMode.EngineeringNdis)
            return new(true, false, string.Empty);
        if (mode != ControllerRuntimeMode.Normal) throw new ArgumentOutOfRangeException(nameof(mode));
        var result = await Task.Run(inspect, token).WaitAsync(token);
        token.ThrowIfCancellationRequested();
        return result;
    }
}

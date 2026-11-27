using NT.Diag;

namespace RouterXYZACTC;

/// <summary>
/// Owns modeless diagnostic windows and their shared theme/layout integration.
/// Each window is created on demand and reused while open; the ordinary operator
/// screen pays no capture or polling cost for unopened diagnostics.
/// </summary>
public partial class MainForm
{
    // Only window slots exist during normal runtime. No capture, timer, metadata
    // snapshot, or I/O polling is created until the corresponding command runs.
    private IoMonitorForm? ioDiagnostics;
    private PlcProgramsForm? plcProgramsDiagnostics;
    private AxisVelocityForm? axisVelocityDiagnostics;
    private CycleTimingForm? cycleTimingDiagnostics;
    private PlcMemoryForm? plcMemoryDiagnostics;

    internal void OpenIoDiagnostics()
    {
        if (runtimeDisposed) return;
        if (ActivateDiagnostic(ioDiagnostics)) return;
        var form = new IoMonitorForm(controller.MotionController, CaptureDiagnosticSignals);
        ioDiagnostics = form;
        form.FormClosed += (_, _) => ioDiagnostics = null;
        ShowDiagnostic(form);
    }

    internal void OpenPlcPrograms()
    {
        if (runtimeDisposed || ActivateDiagnostic(plcProgramsDiagnostics)) return;
        var form = new PlcProgramsForm(controller.MotionController);
        plcProgramsDiagnostics = form;
        form.FormClosed += (_, _) => plcProgramsDiagnostics = null;
        ShowDiagnostic(form);
    }

    internal void OpenAxisVelocity()
    {
        if (runtimeDisposed || ActivateDiagnostic(axisVelocityDiagnostics)) return;
        var form = new AxisVelocityForm(controller.MotionController);
        axisVelocityDiagnostics = form;
        form.FormClosed += (_, _) => axisVelocityDiagnostics = null;
        ShowDiagnostic(form);
    }

    internal void OpenCycleTiming()
    {
        if (runtimeDisposed || ActivateDiagnostic(cycleTimingDiagnostics)) return;
        var form = new CycleTimingForm(controller.MotionController);
        cycleTimingDiagnostics = form;
        form.FormClosed += (_, _) => cycleTimingDiagnostics = null;
        ShowDiagnostic(form);
    }

    internal void OpenPlcMemory()
    {
        if (runtimeDisposed || ActivateDiagnostic(plcMemoryDiagnostics)) return;
        var form = new PlcMemoryForm(controller.MotionController);
        plcMemoryDiagnostics = form;
        form.FormClosed += (_, _) => plcMemoryDiagnostics = null;
        ShowDiagnostic(form);
    }

    private void ShowDiagnostic(DiagnosticForm form)
    {
        try
        {
            form.BindLayout(generalLinks.UiGroup, form.GetType().Name, () => generalLinks.SaveWindowLayoutLink.Value);
            AttachDialogTheme(form);
            form.Show(this);
        }
        catch { form.Dispose(); throw; }
    }

    private static bool ActivateDiagnostic(DiagnosticForm? form)
    {
        if (form is null || form.IsDisposed) return false;
        if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
        if (!form.Visible) form.Show();
        form.Activate();
        return true;
    }

    private IReadOnlyList<DiagnosticIoSignal> CaptureDiagnosticSignals()
        => controller.PLC.IOMappings.Where(mapping => mapping.IsPhysicalTarget)
            .Select(mapping => new DiagnosticIoSignal(controllerLinks.GetSignalDisplayName(mapping), mapping.Target, mapping.IsDirty)).ToArray();

    private void RefreshDiagnosticMetadata() => ioDiagnostics?.RefreshMetadata();

    private void CloseDiagnosticWindows()
    {
        ioDiagnostics?.Close();
        plcProgramsDiagnostics?.Close();
        axisVelocityDiagnostics?.Close();
        cycleTimingDiagnostics?.Close();
        plcMemoryDiagnostics?.Close();
    }

    private void DisposeDiagnosticWindows()
    {
        // WinForms raises owned FormClosed events before disposing the owner.
        // Those events clear our slots, but the windows still need their borrowed
        // theme while disposing. Release them before the owner's components.
        foreach (DiagnosticForm form in OwnedForms.OfType<DiagnosticForm>())
            form.Dispose();
    }
}

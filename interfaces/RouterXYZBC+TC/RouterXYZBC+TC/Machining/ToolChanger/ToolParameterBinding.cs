using NT.Diagnostics;
using NT.LinkManager;
using NT.Localization;
using NT.Tools;
using System.Globalization;

namespace RouterXYZBCTC;

/// <summary>
/// Adapts the RouterXYZBC+TC enabled-tool selector to the existing manual tool-change command.
/// Enter, list selection, or an explicit TryCommit starts the request. CurrentTool remains
/// the only value presented after the request has been accepted.
/// </summary>
internal sealed class ToolParameterBinding : IDisposable
{
    private readonly ToolChangerLinks toolChanger;
    private readonly sParameterBox control;
    private readonly Func<bool> canInteract;
    private readonly Func<bool> canCommit;
    private readonly Action<string>? availabilityChanged;
    private string lastAvailability = string.Empty;
    private bool toolItemsDirty = true;
    private bool disposed;

    internal ToolParameterBinding(
        ToolChangerLinks toolChanger,
        sParameterBox control,
        Func<bool> canInteract,
        Func<bool> canCommit,
        Action<string>? availabilityChanged = null)
    {
        ArgumentNullException.ThrowIfNull(toolChanger);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(canInteract);
        ArgumentNullException.ThrowIfNull(canCommit);
        this.toolChanger = toolChanger;
        this.control = control;
        this.canInteract = canInteract;
        this.canCommit = canCommit;
        this.availabilityChanged = availabilityChanged;

        // Follow the Reksis command boundary: accepted Enter and list-selection
        // commits start the request. Focus changes do not start machine motion.
        control.InputMode = SParameterInputMode.DropDownList;
        control.CommitOnEnter = true;
        control.CommitOnValidated = false;
        control.CancelOnEscape = true;
        control.ValueValidating += ValidateRequest;
        control.ValueCommitted += CommitRequest;
        foreach (Tool tool in toolChanger.Tools)
            tool.EnabledLink.Changed += ToolAvailabilityChanged;
        Refresh();
    }

    /// <summary>
    /// Refreshes enabled choices, command availability and confirmed physical tool presentation.
    /// An idle modified draft is preserved until commit or Escape; an active exchange always
    /// replaces it with CurrentTool so the ReleaseTool empty and ClampTool picked states are visible.
    /// </summary>
    internal void Refresh()
    {
        if (disposed || control.IsDisposed) return;

        RefreshToolItems();

        bool active = toolChanger.IsActive;
        string reason = string.Empty;
        bool canEdit = !active && canInteract() && toolChanger.CanAcceptManualRequest(out reason);
        if (!canEdit && reason.Length == 0)
            reason = NTLocalization.Get(RouterXYZBCTCTexts.ToolManualBlocked);
        if (active && reason.Length == 0)
            reason = NTLocalization.Get(RouterXYZBCTCTexts.ToolOperationPending);
        if (active || !canEdit || !control.IsModified)
            ShowCurrentTool();
        control.DisabledText = reason.Length == 0
            ? NTLocalization.Get(RouterXYZBCTCTexts.ToolDisabled)
            : reason;
        control.AccessibleDescription = reason;
        control.Enabled = canEdit;

        string availability = canEdit
            ? NTLocalization.Get(RouterXYZBCTCTexts.ToolChangeReady)
            : reason;
        if (availability != lastAvailability)
        {
            lastAvailability = availability;
            NTLog.Report($"R_ToolSelector: {availability}");
            availabilityChanged?.Invoke(availability);
        }
    }

    private void ValidateRequest(object? sender, SParameterValidatingEventArgs e)
    {
        if (!TryParseTool(e.ProposedValue, out long tool))
        {
            e.Cancel = true;
            e.ErrorText = NTLocalization.Format(
                RouterXYZBCTCTexts.ToolSelectRange, Initialize.ToolCount);
            return;
        }

        if (!toolChanger.IsToolEnabled(tool))
        {
            e.Cancel = true;
            e.ErrorText = NTLocalization.Format(RouterXYZBCTCTexts.ToolUnavailable, tool);
            return;
        }

        e.ProposedValue = FormatTool(tool);
    }

    private void CommitRequest(object? sender, SParameterCommittedEventArgs e)
    {
        if (!canCommit())
        {
            ShowCurrentTool();
            control.ErrorText = NTLocalization.Get(RouterXYZBCTCTexts.ToolManualBlocked);
            Refresh();
            return;
        }

        // ValueValidating already accepted and normalized this text.
        if (!TryParseTool(e.Value, out long requestedTool))
        {
            ShowCurrentTool();
            control.ErrorText = NTLocalization.Get(RouterXYZBCTCTexts.ToolInvalidRequested);
            Refresh();
            return;
        }

        if (!toolChanger.IsToolEnabled(requestedTool))
        {
            ShowCurrentTool();
            control.ErrorText = NTLocalization.Format(
                RouterXYZBCTCTexts.ToolUnavailable, requestedTool);
            return;
        }

        if (!toolChanger.StartManualChange(requestedTool, out string reason))
        {
            ShowCurrentTool();
            control.ErrorText = reason;
            availabilityChanged?.Invoke(NTLocalization.Format(
                RouterXYZBCTCTexts.ToolChangeRejected, reason));
            Refresh();
            return;
        }

        // ChangeTool captured NewTool. From this point the editor is a disabled
        // CurrentTool display until the operation finishes.
        ShowCurrentTool();
        control.Enabled = false;
    }

    private void ToolAvailabilityChanged(object? sender, LinkChangedEventArgs e)
        => toolItemsDirty = true;

    private void RefreshToolItems()
    {
        if (!toolItemsDirty) return;
        toolItemsDirty = false;
        var items = new List<string>(Initialize.ToolCount + 1) { string.Empty };
        for (int tool = 1; tool <= Initialize.ToolCount; ++tool)
            if (toolChanger.IsToolEnabled(tool))
                items.Add(FormatTool(tool));
        control.Items.Clear();
        control.Items.AddRange(items);
    }

    private static bool TryParseTool(string value, out long tool)
    {
        value = (value ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            tool = 0;
            return true;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out tool) &&
            tool is >= 0 and <= Initialize.ToolCount;
    }

    private void ShowCurrentTool()
    {
        string value = FormatTool(toolChanger.CurrentTool.Value);
        if (control.Value != value || control.AcceptedValue != value)
            control.Value = value;
    }

    private static string FormatTool(long tool)
        => tool == 0 ? string.Empty : tool.ToString(CultureInfo.CurrentCulture);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        control.ValueValidating -= ValidateRequest;
        control.ValueCommitted -= CommitRequest;
        foreach (Tool tool in toolChanger.Tools)
            tool.EnabledLink.Changed -= ToolAvailabilityChanged;
    }
}

using NT;
using NT.Diagnostics;
using NT.LinkManager;
using NT.Tools;
using System.Globalization;
using System.Windows.Forms;

namespace RouterXYZTC;

/// <summary>Connects existing parameter boxes to the selected row without persisting proxy values.</summary>
internal sealed class ReferenceEditorBinding : IDisposable
{
    private const string DisplayFormat = "0.###";

    private readonly ReferenceLinks references;
    private readonly sTextBox[] controls;
    private readonly sComboBox selector;
    private readonly RealLink[] links;
    private readonly AxisSource[] sources;
    private readonly TextLink selectionLink;
    private readonly ReferenceNameSource selectionSource;
    private readonly Action<string>? reportError;

    internal ReferenceEditorBinding(ReferenceLinks references, sTextBox[] controls, sComboBox selector,
        Action<string>? reportError = null)
    {
        if (controls.Length != references.AxisCount)
            throw new ArgumentException("One reference parameter box is required for each TP0 axis.", nameof(controls));

        this.references = references;
        this.controls = controls;
        this.selector = selector;
        this.reportError = reportError;
        links = new RealLink[controls.Length];
        sources = new AxisSource[controls.Length];
        for (int axisIndex = 0; axisIndex < controls.Length; axisIndex++)
        {
            controls[axisIndex].Text = references.GetAxisName(axisIndex);
            RealLink link = links[axisIndex] = Link.CreateReal($"CurrentReference.{references.GetAxisName(axisIndex)}", $"CurrentReference.{references.GetAxisName(axisIndex)}",
                0.0, double.MinValue, double.MaxValue, ReferenceLinks.Precision);
            link.Source = sources[axisIndex] = new AxisSource(this, axisIndex);
            link.QueryEnabled += QueryCanEdit;
            link.Bind(controls[axisIndex], options => options.DisplayFormat = DisplayFormat);
            // Bindings can defer pushes while their form is hidden or the editor
            // holds focus. Establish the accepted baseline before the first Show.
            ResetEditor(axisIndex);
        }

        selectionLink = Link.CreateText("CurrentReferenceName", "CurrentReferenceName", ReferenceLinks.GetReferenceName(references.SelectedIndex));
        selectionLink.Items = Enumerable.Range(0, ReferenceLinks.ReferenceCount).Select(ReferenceLinks.GetReferenceName).ToArray();
        selectionLink.ItemsOnly = true;
        selectionLink.Source = selectionSource = new ReferenceNameSource(this);
        selectionLink.QueryEnabled += QueryCanEdit;
        selectionLink.Bind(selector, options => options.IgnorePushWhileFocused = false);
        ResetSelector();
        references.SelectionChanged += OnSelectionChanged;
        references.ValueChanged += OnValueChanged;
    }

    private void QueryCanEdit(object? sender, QueryEventArgs e) => e.Enabled &= references.CanEdit;

    private void OnValueChanged(object? sender, EventArgs e)
    {
        foreach (AxisSource source in sources)
            source.NotifyChanged();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        OnValueChanged(sender, e);
        selectionSource.NotifyChanged();
        // Changing the address cancels any unfinished text, even if the editor
        // still has focus or both rows contain the same value. A later Validated
        // event can therefore never write text from the previous reference.
        for (int axisIndex = 0; axisIndex < controls.Length; axisIndex++)
            ResetEditor(axisIndex);
        ResetSelector();
    }

    private void ResetEditor(int axisIndex)
        => controls[axisIndex].Text = references.GetValue(references.SelectedIndex, axisIndex)
            .ToString(DisplayFormat, CultureInfo.CurrentCulture);

    private void ResetSelector()
        => selector.Text = ReferenceLinks.GetReferenceName(references.SelectedIndex);

    private void ReportError(string message)
    {
        NTLog.Warning(message);
        reportError?.Invoke(message);
    }

    public void Dispose()
    {
        references.SelectionChanged -= OnSelectionChanged;
        references.ValueChanged -= OnValueChanged;
        selectionLink.Dispose();
        foreach (RealLink link in links)
            link.Dispose();
    }

    private sealed class AxisSource(ReferenceEditorBinding owner, int axisIndex) : IRealSource
    {
        public event EventHandler? ValueChanged;
        public double GetValue() => owner.references.GetValue(owner.references.SelectedIndex, axisIndex);
        internal void NotifyChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);

        public void SetValue(double value)
        {
            try
            {
                owner.references.SetValue(owner.references.SelectedIndex, axisIndex, value);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                owner.ResetEditor(axisIndex);
                owner.ReportError($"Reference could not be committed: {exception.Message}");
            }
        }
    }

    private sealed class ReferenceNameSource(ReferenceEditorBinding owner) : ITextSource
    {
        public event EventHandler? ValueChanged;
        public string GetValue() => ReferenceLinks.GetReferenceName(owner.references.SelectedIndex);
        internal void NotifyChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);

        public void SetValue(string value)
        {
            int index = Array.IndexOf(owner.selectionLink.Items, value);
            if (index < 0)
                return;
            try
            {
                owner.references.CurrentReference.Value = index;
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                owner.ReportError($"Reference could not be selected: {exception.Message}");
            }
        }
    }
}

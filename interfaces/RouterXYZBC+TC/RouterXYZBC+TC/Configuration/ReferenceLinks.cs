using NT;
using NT.LinkManager;
using NT.Diagnostics;

namespace RouterXYZBCTC;

/// <summary>
/// The six persisted work references for TP0, in its configured compact axis order.
/// All writes, including PropertyGrid writes, pass through sources before a Link
/// publishes a new value. An active-row write must first be accepted by ToolPath.
/// </summary>
internal sealed class ReferenceLinks
{
    internal const int ReferenceCount = 6;
    internal const int Precision = 6;

    private readonly ToolPathWrapper toolPath;
    private readonly Func<bool>? canEditUi;
    private readonly Func<bool>? canCommit;
    private readonly string[] axisNames;
    private readonly RealLink[,] values;
    private readonly double[] referenceRow;
    private int currentReference;

    internal ReferenceLinks(
        LinkGroup parentGroup,
        ToolPathWrapper toolPath,
        Func<bool>? canEditUi = null,
        Func<bool>? canCommit = null)
    {
        ArgumentNullException.ThrowIfNull(parentGroup);
        this.toolPath = toolPath ?? throw new ArgumentNullException(nameof(toolPath));
        this.canEditUi = canEditUi;
        this.canCommit = canCommit;
        axisNames = toolPath.Axes.Select(axis => axis.Name).ToArray();
        if (axisNames.Length == 0)
            throw new ArgumentException("Configure TP0 axes before creating reference links.", nameof(toolPath));

        Group = parentGroup.AddGroup("Reference", "Reference", "Work coordinate references for ToolPath 0.");
        Group.IconKey = "shared";
        CurrentReference = Group.AddInteger("CurrentReference", "Current Reference", 0, 0, ReferenceCount - 1,
            "Active work reference: 0 = G54, 1 = G55, 2 = G56, 3 = G57, 4 = G58, 5 = G59.");
        currentReference = checked((int)CurrentReference.Value);

        values = new RealLink[ReferenceCount, AxisCount];
        referenceRow = new double[AxisCount];
        for (int referenceIndex = 0; referenceIndex < ReferenceCount; referenceIndex++)
        {
            string rowName = GetReferenceName(referenceIndex);
            LinkGroup row = Group.AddGroup(rowName, rowName, "Reference coordinates in TP0 axis order.");
            for (int axisIndex = 0; axisIndex < AxisCount; axisIndex++)
            {
                RealLink link = row.AddReal(axisNames[axisIndex], axisNames[axisIndex], 0.0, double.MinValue, double.MaxValue,
                    Precision, $"{row.Name} reference coordinate for axis {axisNames[axisIndex]}, in its configured axis unit.");
                values[referenceIndex, axisIndex] = link;
                link.Source = new CoordinateSource(this, referenceIndex, axisIndex, link.Value);
                link.QueryEnabled += QueryCanEdit;
                link.Changed += (_, _) =>
                {
                    ValidateReferences(out _);
                    // A compiled program may use any G54-G59 row, not only the selected one.
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                };
            }
        }

        CurrentReference.Source = new SelectionSource(this);
        CurrentReference.QueryEnabled += QueryCanEdit;
        CurrentReference.Changed += (_, _) =>
        {
            ValidateReferences(out _);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
        ApplyCurrentReference();
    }

    internal LinkGroup Group { get; }
    internal IntegerLink CurrentReference { get; }
    internal int AxisCount => axisNames.Length;
    internal int SelectedIndex => currentReference;
    internal bool ProgramPreparationActive { get; set; }
    internal bool CanEdit => canEditUi?.Invoke() ??
        (!ProgramPreparationActive && !toolPath.IsProgramActive);
    internal event EventHandler? SelectionChanged;
    internal event EventHandler? ValueChanged;

    internal string GetAxisName(int axisIndex) => axisNames[axisIndex];

    internal static string GetReferenceName(int referenceIndex)
    {
        ValidateReferenceIndex(referenceIndex);
        return $"G{54 + referenceIndex}";
    }

    public double GetValue(int referenceIndex, int axisIndex)
    {
        ValidateAddress(referenceIndex, axisIndex);
        return values[referenceIndex, axisIndex].Value;
    }

    public void SetValue(int referenceIndex, int axisIndex, double value)
    {
        ValidateAddress(referenceIndex, axisIndex);
        ValidateCoordinate(axisIndex, value);
        values[referenceIndex, axisIndex].Value = value;
    }

    /// <summary>
    /// Retains invalid saved coordinates for explicit correction and marks them
    /// as errors. Nothing is clamped or silently replaced with a new origin.
    /// </summary>
    internal bool ValidateReferences(out string error)
    {
        error = string.Empty;
        for (int referenceIndex = 0; referenceIndex < ReferenceCount; referenceIndex++)
        {
            string rowError = string.Empty;
            for (int axisIndex = 0; axisIndex < AxisCount; axisIndex++)
            {
                RealLink link = values[referenceIndex, axisIndex];
                string coordinateError = GetCoordinateError(axisIndex, link.Value);
                link.SetState(coordinateError.Length == 0 ? OperationalState.Normal : OperationalState.Error, coordinateError);
                if (rowError.Length == 0 && coordinateError.Length != 0)
                    rowError = $"{GetReferenceName(referenceIndex)}: {coordinateError}";
            }
            Group.Groups[GetReferenceName(referenceIndex)].SetState(
                rowError.Length == 0 ? OperationalState.Normal : OperationalState.Error, rowError);
            if (error.Length == 0 && rowError.Length != 0)
                error = rowError;
        }
        Group.SetState(error.Length == 0 ? OperationalState.Normal : OperationalState.Error, error);
        return error.Length == 0;
    }

    /// <summary>Republishes the selected row after application configuration is applied.</summary>
    internal void ApplyCurrentReference()
    {
        // Keep the selected stored origin visible even if changed course limits
        // have invalidated it. Controller.RunToolPath refuses to execute it.
        ApplyRow(currentReference);
        ValidateReferences(out _);
    }

    private void ApplyRow(int referenceIndex, int changedAxis = -1, double changedValue = 0.0)
    {
        EnsureCanEdit();
        if (toolPath.Axes.Length != AxisCount ||
            toolPath.Axes.Where((axis, index) => axis.Name != axisNames[index]).Any())
            throw new InvalidOperationException("TP0 axis layout no longer matches its reference table.");

        for (int axisIndex = 0; axisIndex < referenceRow.Length; axisIndex++)
            referenceRow[axisIndex] = axisIndex == changedAxis ? changedValue : GetValue(referenceIndex, axisIndex);

        // ToolPath also rejects pending Run/Stop/Reset commands under its producer
        // lock. Publish first, so a rejected write leaves the table unchanged.
        toolPath.Reference = referenceRow;
    }

    private void QueryCanEdit(object? sender, QueryEventArgs e) => e.Enabled &= CanEdit;

    private void EnsureCanEdit()
    {
        bool allowed = canCommit?.Invoke() ?? canEditUi?.Invoke() ??
            (!ProgramPreparationActive && !toolPath.IsProgramActive);
        if (!allowed)
            throw new InvalidOperationException("References cannot be changed while machine activity owns coordinates.");
    }

    private static void ValidateReferenceIndex(int referenceIndex)
    {
        if ((uint)referenceIndex >= ReferenceCount)
            throw new ArgumentOutOfRangeException(nameof(referenceIndex));
    }

    private void ValidateAddress(int referenceIndex, int axisIndex)
    {
        ValidateReferenceIndex(referenceIndex);
        if ((uint)axisIndex >= (uint)AxisCount)
            throw new ArgumentOutOfRangeException(nameof(axisIndex));
    }

    private static void ValidateValue(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Reference coordinates must be finite.");
    }

    private string GetCoordinateError(int axisIndex, double value)
    {
        if (toolPath.Axes.Length != AxisCount || toolPath.Axes[axisIndex].Name != axisNames[axisIndex])
            return "TP0 axis layout no longer matches its reference table.";
        AxisWrapper axis = toolPath.Axes[axisIndex];
        double minimum = axis.MinCourse;
        double maximum = axis.MaxCourse;
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum > maximum)
            return $"Axis {axis.Name} has invalid course limits [{minimum:G17}, {maximum:G17}].";
        return double.IsFinite(value) && value >= minimum && value <= maximum ? string.Empty
            : $"Axis {axis.Name}: reference {value:G17} is outside course [{minimum:G17}, {maximum:G17}].";
    }

    private void ValidateCoordinate(int axisIndex, double value)
    {
        ValidateValue(value);
        string error = GetCoordinateError(axisIndex, value);
        if (error.Length != 0)
            throw new ArgumentOutOfRangeException(nameof(value), error);
    }

    private void ValidateRow(int referenceIndex)
    {
        ValidateReferenceIndex(referenceIndex);
        for (int axisIndex = 0; axisIndex < AxisCount; axisIndex++)
            ValidateCoordinate(axisIndex, GetValue(referenceIndex, axisIndex));
    }

    private sealed class CoordinateSource(ReferenceLinks owner, int referenceIndex, int axisIndex, double initialValue) : IRealSource
    {
        private double value = initialValue;

        // Writes are synchronous; RealLink refreshes from the source on commit.
        public event EventHandler? ValueChanged { add { } remove { } }
        public double GetValue() => value;

        public void SetValue(double candidate)
        {
            ValidateValue(candidate);
            if (candidate == value)
                return;
            owner.ValidateCoordinate(axisIndex, candidate);
            owner.EnsureCanEdit();
            if (referenceIndex == owner.currentReference)
                owner.ApplyRow(referenceIndex, axisIndex, candidate);
            NTLog.Report(FormattableString.Invariant($"Reference coordinate changed: {GetReferenceName(referenceIndex)} {owner.axisNames[axisIndex]} {value:G17} -> {candidate:G17}"));
            value = candidate;
        }
    }

    private sealed class SelectionSource(ReferenceLinks owner) : IIntegerSource
    {
        public event EventHandler? ValueChanged { add { } remove { } }
        public long GetValue() => owner.currentReference;

        public void SetValue(long value)
        {
            int index = checked((int)value);
            ValidateReferenceIndex(index);
            if (index == owner.currentReference)
                return;
            owner.ValidateRow(index);
            owner.ApplyRow(index);
            NTLog.Report($"Reference selected: {GetReferenceName(owner.currentReference)} -> {GetReferenceName(index)}; " +
                string.Join("; ", owner.axisNames.Select((axis, slot) => FormattableString.Invariant($"{axis}={owner.GetValue(index, slot):G17}"))));
            owner.currentReference = index;
        }
    }
}

using NT;
using NT.Controller.PLC;
using NT.LinkManager;
using PlcCore = NT.PLCWrapper;

namespace RouterXYZACTC;

/// <summary>
/// Defines one persisted tool station and the compensation applied while its
/// tool is in the spindle. PLC receives XYZ components and separate A/C station angles.
/// </summary>
internal sealed class Tool
{
    private readonly int number;
    private readonly Func<long> currentToolProvider;
    private readonly VariableLink<PointD3> positionLink;
    private readonly VariableLink<PointD3> placeVectorLink;
    private readonly VariableLink<PointD3> pickVectorLink;
    private readonly VariableLink<PointD3> offsetLink;

    internal Tool(PlcCore plc, LinkGroup toolsGroup, int number, Func<long> currentToolProvider)
    {
        ArgumentNullException.ThrowIfNull(plc);
        ArgumentNullException.ThrowIfNull(toolsGroup);
        ArgumentNullException.ThrowIfNull(currentToolProvider);
        if (number is < 1 or > 99)
            throw new ArgumentOutOfRangeException(nameof(number));

        this.number = number;
        this.currentToolProvider = currentToolProvider;
        Name = $"T{number:00}";
        LinkGroup toolGroup = toolsGroup.AddGroup(
            Name, Name,
            $"Station position, placement/pickup vectors, and XYZ compensation for tool {number:00}.");
        toolGroup.IconKey = "toolpath";

        EnabledLink = toolGroup.AddLogic(
            "Enabled", "Enabled",
            true,
            "Disabled",
            "Enabled",
            $"Whether tool {number:00} is installed and may be selected for a tool change.");
        EnabledLink.QueryEnabled += QueryEnabled;
        EnabledLink.Changed += EnabledChanged;
        EnsureCurrentToolRemainsEnabled();

        positionLink = toolGroup.AddValue(
            "Position", "Position",
            PointD3.Zero,
            "Tool position in machine coordinates, in millimeters.");
        placeVectorLink = toolGroup.AddValue(
            "PlaceVector", "Place Vector",
            PointD3.Zero,
            "Offset in millimeters from the tool position to the placement exit and pickup entry point.");
        pickVectorLink = toolGroup.AddValue(
            "PickVector", "Pick Vector",
            PointD3.Zero,
            "Offset in millimeters from the tool position to the placement entry and pickup exit point.");
        offsetLink = toolGroup.AddValue(
            "Offset", "Offset",
            PointD3.Zero,
            "XYZ correction in millimeters added to the measured tool length; Z is added to Length before TP0 compensation.");
        PositionALink = toolGroup.AddReal("A", "A", 0.0, -1000000000.0, 1000000000.0, 6,
            "Holder A machine angle in unwrapped degrees, retained during insertion and withdrawal.");
        PositionCLink = toolGroup.AddReal("C", "C", 0.0, -1000000000.0, 1000000000.0, 6,
            "Holder C machine angle in unwrapped degrees, retained during insertion and withdrawal.");
        PLCVariableBridge.AddApplicationVariable(plc, PositionALink, $"{Name}.A", PLCVariableAccess.ReadOnly);
        PLCVariableBridge.AddApplicationVariable(plc, PositionCLink, $"{Name}.C", PLCVariableAccess.ReadOnly);
        LengthLink = toolGroup.AddReal(
            "Length", "Length", 0.0, 0.0, 1000000.0, 6,
            $"Measured physical length of tool {number:00}, in millimeters, relative to the empty-spindle reference.");

        // Point Links remain the persisted application authority. The bridge expands
        // each point into stable read-only .X/.Y/.Z PLC application variables.
        PLCVariableBridge.AddApplicationVariable(plc, positionLink, Name);
        PLCVariableBridge.AddApplicationVariable(plc, placeVectorLink, $"{Name}.PlaceVector");
        PLCVariableBridge.AddApplicationVariable(plc, pickVectorLink, $"{Name}.PickVector");
        PLCVariableBridge.AddApplicationVariable(plc, offsetLink, $"{Name}.Offset");
        PLCVariableBridge.AddApplicationVariable(plc, LengthLink, $"{Name}.Length", PLCVariableAccess.ReadOnly);
    }

    internal string Name { get; }

    /// <summary>Persisted selection and magazine-presence authority for this tool.</summary>
    internal LogicLink EnabledLink { get; }

    internal bool Enabled => EnabledLink.Value;
    internal RealLink PositionALink { get; }
    internal RealLink PositionCLink { get; }
    internal double PositionA => PositionALink.Value;
    internal double PositionC => PositionCLink.Value;
    internal RealLink LengthLink { get; }
    internal double Length { get => LengthLink.Value; set => LengthLink.Value = value; }

    /// <summary>A configured tool is absent from its pocket only while it is in the spindle.</summary>
    internal bool IsPresent(long currentTool) => Enabled && currentTool != number;

    /// <summary>Complete holder position in machine coordinates.</summary>
    internal PointD3 Position
        => positionLink.Value;

    /// <summary>Placement exit and pickup entry relative to Position.</summary>
    internal PointD3 PlaceVector
        => placeVectorLink.Value;

    /// <summary>Placement entry and pickup exit relative to Position.</summary>
    internal PointD3 PickVector
        => pickVectorLink.Value;

    /// <summary>XYZ compensation selected for TP0 while this tool is current.</summary>
    internal PointD3 Offset
        => offsetLink.Value;

    /// <summary>Stores the measured length independently of the configured XYZ correction.</summary>
    internal void SetMeasuredLength(double length)
    {
        if (!double.IsFinite(length) || length <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(length));
        Length = length;
        LengthLink.Flush();
    }

    private void QueryEnabled(object? sender, QueryEventArgs e)
        => e.Enabled &= currentToolProvider() != number;

    private void EnabledChanged(object? sender, LinkChangedEventArgs e)
        => EnsureCurrentToolRemainsEnabled();

    internal void EnsureCurrentToolRemainsEnabled()
    {
        if (currentToolProvider() != number || EnabledLink.Value)
            return;

        // QueryEnabled intentionally makes the settings editor read-only for the
        // spindle tool. Temporarily remove that UI gate for this invariant repair.
        EnabledLink.QueryEnabled -= QueryEnabled;
        try { EnabledLink.Value = true; }
        finally { EnabledLink.QueryEnabled += QueryEnabled; }
    }
}

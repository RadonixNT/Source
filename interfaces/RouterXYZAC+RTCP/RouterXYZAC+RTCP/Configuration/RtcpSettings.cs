using NT;
using NT.Controller;
using NT.LinkManager;

namespace RouterXYZACRTCP;

/// <summary>Stores the interface's calibration and jog settings in the existing Link graph.</summary>
internal sealed class RtcpSettings
{
    private readonly ControllerWrapper controller;
    private readonly LinkGroup calibration;
    private readonly LogicLink configured;
    private readonly TextLink machineType;
    private readonly TextLink firstJoint;
    private readonly VectorLinks gauge;
    private readonly VectorLinks datum;
    private readonly VectorLinks toolDirection;
    private readonly JointLinks a;
    private readonly JointLinks c;

    internal JogLinks AP { get; }
    internal JogLinks CP { get; }
    internal JogLinks ZT { get; }
    internal RealLink ToolLength { get; }
    internal RealLink AxialReturnDistance { get; }
    internal bool IsConfigured => configured.Value;

    internal RtcpSettings(ControllerWrapper controller, LinkGroup controllerGroup, LinkGroup generalGroup)
    {
        this.controller = controller;
        calibration = controllerGroup.AddGroup("RTCP", "RTCP", "Five-axis machine calibration. Measure all points at the same machine reference pose.");
        calibration.Permission = AccessPermission.Engineering;
        configured = calibration.AddLogic("Configured", "Enabled", false,
            "Enable only after entering measured calibration data.");
        machineType = calibration.AddText("MachineType", "Machine Type", nameof(RtcpMachineType.HeadHead),
            Enum.GetNames<RtcpMachineType>(),
            "HeadHead: both joints move the spindle. TableTable: both move the workpiece. HeadTable: one on each side.");
        machineType.ItemsOnly = true;
        firstJoint = calibration.AddText("FirstJoint", "First Rotary Axis", "C", ["C", "A"],
            "Outer joint for a nested assembly or spindle-side joint for HeadTable.");
        firstJoint.ItemsOnly = true;
        gauge = AddVector(calibration, "GaugePoint", "Spindle Reference Point", new(), unit: "mm");
        datum = AddVector(calibration, "TableDatum", "Table Reference Point", new(), unit: "mm");
        toolDirection = AddVector(calibration, "ToolDirection", "Gauge-to-Tip Unit Direction", new(0, 0, -1), 1);
        ToolLength = calibration.AddReal("ToolLength", "Tool Length", 0, 0, 1e9, 6,
            "Distance from the spindle gauge point to the active tool tip along the tool direction, in millimeters.");
        AxialReturnDistance = calibration.AddReal("AxialReturnDistance", "Axial Return Distance", 0, 0, 1e9, 3,
            "Requested tool-axis distance in millimeters for entry and withdrawal. The first travel boundary limits the distance. Zero disables automatic RTCP return and skips axial withdrawal for Home, Reference and Park.");
        configured.QueryEnabled += OfflineOnly;
        machineType.QueryEnabled += OfflineOnly;
        firstJoint.QueryEnabled += OfflineOnly;
        ToolLength.QueryEnabled += OfflineOnly;
        AxialReturnDistance.QueryEnabled += OfflineOnly;
        a = AddJoint("A", new(1, 0, 0));
        c = AddJoint("C", new(0, 0, 1));
        AP = AddJog(generalGroup, "AP", "deg");
        CP = AddJog(generalGroup, "CP", "deg");
        ZT = AddJog(generalGroup, "ZT", "mm");
    }

    internal bool Validate()
    {
        bool valid = !configured.Value || Enum.TryParse(machineType.Value, out RtcpMachineType type)
            && Enum.IsDefined(type) && firstJoint.Value is "A" or "C"
            && IsUnit(a.Direction.Read()) && IsUnit(c.Direction.Read()) && IsUnit(toolDirection.Read());
        calibration.SetState(valid ? OperationalState.Normal : OperationalState.Error,
            valid ? string.Empty : "Select a valid arrangement and enter unit vectors for A, C and the tool direction.");
        return valid;
    }

    internal void Apply()
    {
        if (!controller.ConnectionState.AllowsOfflineConfiguration()) return;
        if (!Validate()) throw new InvalidOperationException(calibration.StateMessage);

        var rtcp = controller.MotionController.RTCP;
        if (!configured.Value)
        {
            if (rtcp.Geometry is not null) rtcp.Geometry = null;
            return;
        }

        RtcpRotaryAxis jointA = a.Read(controller.GetAxisIndex("A"));
        RtcpRotaryAxis jointC = c.Read(controller.GetAxisIndex("C"));
        bool aFirst = firstJoint.Value == "A";
        var geometry = new RtcpMachineGeometry
        {
            MachineType = Enum.Parse<RtcpMachineType>(machineType.Value),
            LinearAxes = new(controller.GetAxisIndex("X"), controller.GetAxisIndex("Y"), controller.GetAxisIndex("Z")),
            FirstRotary = aFirst ? jointA : jointC,
            SecondRotary = aFirst ? jointC : jointA,
            SpindleGaugePointMm = gauge.Read(),
            TableDatumMm = datum.Read(),
            ToolDirection = toolDirection.Read(),
            ToolLengthMm = ToolLength.Value
        };
        if (rtcp.Geometry != geometry) rtcp.Geometry = geometry;
    }

    private static bool IsUnit(RtcpVector3 direction)
        => Math.Abs(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z - 1) <= 1e-9;

    private JointLinks AddJoint(string name, RtcpVector3 direction)
    {
        LinkGroup group = calibration.AddGroup(name, $"Axis {name}", "Rotary geometry at the machine reference pose.");
        var angle = group.AddReal("ReferenceAngle", "Reference Angle", 0, -1e9, 1e9, 9,
            "Machine angle of this joint at the common calibration pose, in degrees.");
        angle.QueryEnabled += OfflineOnly;
        return new(AddVector(group, "Pivot", "Point on Rotation Axis", new(), unit: "mm"),
            AddVector(group, "Direction", "Rotation Axis Direction", direction, 1), angle);
    }

    private VectorLinks AddVector(LinkGroup parent, string name, string label, RtcpVector3 value, double limit = 1e9, string? unit = null)
    {
        LinkGroup group = parent.AddGroup(name, label, unit is null ? "Dimensionless unit vector." : $"Coordinates in {unit}.");
        RealLink AddComponent(string axis, double component)
        {
            RealLink link = group.AddReal(axis, axis, component, -limit, limit, 12,
                unit is null ? $"{axis} component of the dimensionless unit vector." : $"{axis} coordinate in {unit}.");
            link.QueryEnabled += OfflineOnly;
            return link;
        }

        return new(AddComponent("X", value.X), AddComponent("Y", value.Y), AddComponent("Z", value.Z));
    }

    private void OfflineOnly(object? sender, QueryEventArgs e)
        => e.Enabled &= controller.ConnectionState.AllowsOfflineConfiguration();

    private static JogLinks AddJog(LinkGroup parent, string axis, string unit)
    {
        LinkGroup group = parent.AddGroup(axis, axis, $"Compensated {axis} motion settings.");
        return new(
            group.AddReal("JogVelocity", "Jog Velocity", 1, 0.001, 1000, 3, $"Compensated jog velocity for {axis} in {unit}/s."),
            group.AddReal("JogStep", "Jog Step", 1, 0.001, 1e9, 3, $"Compensated jog step for {axis} in {unit}."),
            group.AddReal("JogPosition", "Jog Position", 0, -1e9, 1e9, 3, $"Compensated jog destination for {axis} in {unit}."));
    }

    internal sealed record JogLinks(RealLink Velocity, RealLink Step, RealLink Position);

    private sealed record VectorLinks(RealLink X, RealLink Y, RealLink Z)
    {
        internal RtcpVector3 Read() => new(X.Value, Y.Value, Z.Value);
    }

    private sealed record JointLinks(VectorLinks Pivot, VectorLinks Direction, RealLink ReferenceAngle)
    {
        internal RtcpRotaryAxis Read(int slot) => new(slot, Pivot.Read(), Direction.Read(), ReferenceAngle.Value);
    }
}

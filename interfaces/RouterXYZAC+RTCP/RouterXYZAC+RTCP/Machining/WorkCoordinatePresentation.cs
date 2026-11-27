using NT.Controller;
using NT_Interpreter.Shared.Router;
using OpenTK.Mathematics;

namespace RouterXYZACRTCP;

/// <summary>The work frame used to draw TP0 and express its live Cartesian coordinates.</summary>
/// <remarks>PreviewToScene places the program on the workpiece. CarriageAtOrigin is the physical
/// XYZ required to put the tool tip at that scene origin with the sampled rotary positions.</remarks>
internal readonly record struct WorkCoordinatePresentation(
    RouterPosition Origin, Matrix4d PreviewToScene, Vector3d CarriageAtOrigin)
{
    internal static WorkCoordinatePresentation Normal(RouterPosition origin)
    {
        Vector3d point = new(origin.X, origin.Y, origin.Z);
        return new(origin, Matrix4d.CreateTranslation(point), point);
    }

    internal static bool TryRtcp(RTCP rtcp, RtcpMachineGeometry geometry,
        RouterPosition origin, double firstDegrees, double secondDegrees, out WorkCoordinatePresentation frame)
    {
        frame = default;
        // The controller already supplies forward kinematics with the accepted tool length.
        if (!rtcp.TryGetMachinePosition(new(origin.X, origin.Y, origin.Z), firstDegrees, secondDegrees, out var carriage)
            || !ReferenceEquals(geometry, rtcp.Geometry))
            return false;

        var datum = geometry.TableDatumMm;
        var preview = Matrix4d.CreateTranslation(datum.X + origin.X, datum.Y + origin.Y, datum.Z + origin.Z);
        // Only table joints move the work frame. In a nested table the inner rotation is applied first.
        if (geometry.MachineType != RtcpMachineType.HeadHead)
            preview *= TableRotation(geometry.SecondRotary, secondDegrees);
        if (geometry.MachineType == RtcpMachineType.TableTable)
            preview *= TableRotation(geometry.FirstRotary, firstDegrees);
        frame = new(origin, preview, new(carriage.X, carriage.Y, carriage.Z));
        return true;
    }

    internal Vector3d GetWorkPosition(Vector3d carriage)
    {
        // At fixed joint angles a carriage displacement is the same displacement of the TCP.
        // Dotting it against the world-space work axes applies the inverse table rotation.
        Vector3d delta = carriage - CarriageAtOrigin;
        return new(Vector3d.Dot(delta, PreviewToScene.Row0.Xyz),
            Vector3d.Dot(delta, PreviewToScene.Row1.Xyz), Vector3d.Dot(delta, PreviewToScene.Row2.Xyz));
    }

    private static Matrix4d TableRotation(RtcpRotaryAxis joint, double angle)
    {
        var axis = new Vector3d(joint.Direction.X, joint.Direction.Y, joint.Direction.Z).Normalized();
        double phase = Math.IEEERemainder(angle % 360 - joint.ReferenceAngleDegrees % 360, 360);
        var rotation = Matrix4d.CreateFromAxisAngle(axis, phase * (Math.PI / 180));
        var pivot = new Vector3d(joint.PivotMm.X, joint.PivotMm.Y, joint.PivotMm.Z);
        rotation.Row3 = new(pivot - Vector3d.TransformVector(pivot, rotation), 1);
        return rotation;
    }
}

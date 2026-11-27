using NT.Controller;

namespace RouterXYZACRTCP;

/// <summary>Machine-space approach geometry at a fixed RTCP orientation.</summary>
internal static class RtcpReturnApproach
{
    internal static RtcpVector3 OutwardDirection(RtcpMachineGeometry geometry, double firstDegrees, double secondDegrees)
    {
        // Same joint order as the native ToolAxis jog. Table joints carry the
        // workpiece, so they do not rotate the spindle direction in machine space.
        RtcpVector3 direction = Unit(geometry.ToolDirection);
        if (geometry.MachineType == RtcpMachineType.HeadHead)
            direction = Rotate(direction, geometry.SecondRotary, secondDegrees);
        if (geometry.MachineType != RtcpMachineType.TableTable)
            direction = Rotate(direction, geometry.FirstRotary, firstDegrees);
        return new(-direction.X, -direction.Y, -direction.Z);
    }

    internal static RtcpVector3 PointAtDistance(RtcpVector3 target, RtcpVector3 outward, double distance) =>
        new(Math.FusedMultiplyAdd(outward.X, distance, target.X),
            Math.FusedMultiplyAdd(outward.Y, distance, target.Y),
            Math.FusedMultiplyAdd(outward.Z, distance, target.Z));

    internal static double LimitDistance(double distance, double target, double direction,
        double minimum, double maximum, double couplingOffset)
    {
        if (direction == 0) return distance;

        double boundary = direction > 0 ? maximum : minimum;
        double intersection = (boundary - (target + couplingOffset)) / direction;
        double limit = Math.Min(distance, intersection);
        bool IsInside(double length)
        {
            double position = Math.FusedMultiplyAdd(direction, length, target) + couplingOffset;
            return position >= minimum && position <= maximum;
        }

        if (IsInside(limit)) return limit;

        // The computed intersection can round outside the limit after the axis
        // coordinate and coupling offset are applied. Reduce the same distance
        // for all axes so the approach remains on the tool axis.
        double inside = 0;
        double outside = limit;
        for (int i = 0; i < 64; i++)
        {
            double midpoint = inside + (outside - inside) / 2;
            if (midpoint == inside || midpoint == outside) break;
            if (IsInside(midpoint)) inside = midpoint;
            else outside = midpoint;
        }
        return inside;
    }

    internal static bool IsOnAxis(RtcpVector3 position, RtcpVector3 target, RtcpVector3 outward, RtcpVector3 tolerance)
    {
        double distance = (position.X - target.X) * outward.X +
            (position.Y - target.Y) * outward.Y + (position.Z - target.Z) * outward.Z;
        var projected = PointAtDistance(target, outward, distance);
        return Math.Abs(position.X - projected.X) <= tolerance.X && Math.Abs(position.Y - projected.Y) <= tolerance.Y &&
            Math.Abs(position.Z - projected.Z) <= tolerance.Z;
    }

    private static RtcpVector3 Rotate(RtcpVector3 direction, RtcpRotaryAxis rotary, double degrees)
    {
        double phase = Math.IEEERemainder(degrees % 360 - rotary.ReferenceAngleDegrees % 360, 360);
        var (sinHalf, cosHalf) = Math.SinCos(phase * (Math.PI / 360));
        double sine = 2 * sinHalf * cosHalf;
        double oneMinusCosine = 2 * sinHalf * sinHalf;
        var axis = Unit(rotary.Direction);
        var cross = Cross(axis, direction);
        var doubleCross = Cross(axis, cross);
        return new(direction.X + cross.X * sine + doubleCross.X * oneMinusCosine,
            direction.Y + cross.Y * sine + doubleCross.Y * oneMinusCosine,
            direction.Z + cross.Z * sine + doubleCross.Z * oneMinusCosine);
    }

    private static RtcpVector3 Cross(RtcpVector3 a, RtcpVector3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static RtcpVector3 Unit(RtcpVector3 direction)
    {
        double length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
        return new(direction.X / length, direction.Y / length, direction.Z / length);
    }
}

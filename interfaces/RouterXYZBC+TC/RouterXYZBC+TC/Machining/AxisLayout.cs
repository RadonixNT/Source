using NT;

namespace RouterXYZBCTC;

internal static class AxisLayout
{
    // Host positioning uses the same direct-angle metric as the interpreter:
    // XYZ mm/s on mixed legs, resultant degrees/s on pure rotary legs.
    internal static double PathVelocity(AxisWrapper[] axes, ReadOnlySpan<double> from,
        ReadOnlySpan<double> to, double velocity)
    {
        double linear = 0, rotary = 0;
        for (int i = 0; i < axes.Length; i++)
        {
            double delta = to[i] - from[i];
            if (axes[i].Name is "B" or "C") rotary += delta * delta;
            else linear += delta * delta;
        }
        double result = linear > 0 ? velocity * Math.Sqrt((linear + rotary) / linear) : velocity;
        if (!double.IsFinite(result) || result <= 0)
            throw new InvalidOperationException("Positioning velocity must be finite and positive.");
        return result;
    }

    internal static void AddStoppedLine(ToolPathWrapper path, int block, double[] from,
        double[] to, double velocity)
    {
        double effectiveVelocity = PathVelocity(path.Axes, from, to, velocity);
        double minimum = path.MinimumLength;
        try
        {
            // Never merge a small angular clearance leg into the subsequent Z descent.
            path.MinimumLength = double.Epsilon;
            path.AddLine(block, to, effectiveVelocity, stop: true);
        }
        finally { path.MinimumLength = minimum; }
    }

    // Topology changes can move physical slots while retaining the same path coordinates.
    // Check the coordinate contract without allocating in command-availability checks.
    internal static bool Matches(AxisWrapper[] axes, string[] names)
    {
        if (axes.Length != names.Length) return false;
        for (int i = 0; i < axes.Length; i++)
            if (axes[i].Name != names[i]) return false;
        return true;
    }
}

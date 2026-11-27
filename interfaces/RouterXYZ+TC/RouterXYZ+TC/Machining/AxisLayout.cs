using NT;

namespace RouterXYZTC;

internal static class AxisLayout
{
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

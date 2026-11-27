namespace RouterXYZTC;

internal readonly record struct ZoneDefinition(ulong Output, bool Enabled, bool Workholding,
    double MinX, double MaxX, double MinY, double MaxY);

internal readonly record struct ZoneFrame(bool Idle, bool ExtractionActive, double X, double Y);

/// <summary>Single-owner output policy. The caller serializes ticks and requests; writes are scan-boundary commands.</summary>
internal sealed class ZoneRuntime
{
    private readonly ZoneDefinition[] extraction;
    private readonly ulong owned, workholding;
    private ulong held, extractionState, selected;
    private int request;
    internal bool Initialized { get; private set; }
    internal bool Clamped => held != 0;
    internal bool Pending => request != 0;
    internal bool HasExtraction => extraction.Length != 0;

    internal ZoneRuntime(IEnumerable<ZoneDefinition> definitions)
    {
        var zones = definitions.ToArray();
        extraction = zones.Where(z => z.Enabled && !z.Workholding && z.Output != 0).ToArray();
        foreach (var zone in zones)
        {
            owned |= zone.Output;
            if (zone.Enabled && zone.Workholding) workholding |= zone.Output;
        }
    }

    internal bool RequestClamp(ulong selection)
    {
        if (!Initialized || Pending || Clamped || (selection & workholding) == 0) return false;
        selected = selection & workholding;
        request = 1;
        return true;
    }

    internal bool RequestRelease()
    {
        if (!Initialized || Pending || !Clamped) return false;
        request = 2;
        return true;
    }

    internal void Tick(ZoneFrame frame, Func<ulong, bool, bool> write)
    {
        // Establish a known command state on every connection. Never restore a saved latch.
        if (!Initialized)
        {
            if (owned != 0 && !write(owned, false)) return;
            Initialized = true;
        }
        if (request != 0)
        {
            if (!frame.Idle) request = 0; // Recheck admission at execution, not only at the UI click.
            else if (write(request == 1 ? selected : held, request == 1))
            {
                held = request == 1 ? selected : 0;
                request = 0;
            }
        }

        ulong desired = 0;
        if (frame.ExtractionActive && double.IsFinite(frame.X) && double.IsFinite(frame.Y))
            foreach (var zone in extraction)
                if (frame.X >= zone.MinX && frame.X <= zone.MaxX && frame.Y >= zone.MinY && frame.Y <= zone.MaxY)
                    desired |= zone.Output;
        ulong clear = extractionState & ~desired;
        if (clear != 0 && write(clear, false)) extractionState &= ~clear;
        ulong set = desired & ~extractionState;
        if (set != 0 && write(set, true)) extractionState |= set;
    }
}

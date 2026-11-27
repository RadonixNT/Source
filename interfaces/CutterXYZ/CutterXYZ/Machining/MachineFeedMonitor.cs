namespace CutterXYZ;

/// <summary>
/// Measures resultant XYZ command displacement in mm/s using a monotonic timestamp.
/// Samples only after more than 200 ms and retains the previous baseline on early
/// ticks. Offline/non-finite samples invalidate the baseline, so reconnect cannot
/// interpret a coordinate discontinuity as feed. This is presentation, not feedback
/// for motion control, and it intentionally excludes the coupled YG motor.
/// </summary>
internal sealed class MachineFeedMonitor
{
    private bool sampled;
    private double lastX, lastY, lastZ, lastTime;

    internal bool TrySample(double x, double y, double z, double timeSeconds, bool online, out double speed)
    {
        speed = 0;
        if (!online || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)
            || !double.IsFinite(timeSeconds))
        {
            sampled = false;
            return true;
        }
        if (sampled)
        {
            double elapsed = timeSeconds - lastTime;
            // Skipped ticks must retain both the coordinate and time baseline.
            if (elapsed <= 0.2) return false;
            double dx = x - lastX, dy = y - lastY, dz = z - lastZ;
            speed = Math.Sqrt(dx * dx + dy * dy + dz * dz) / elapsed;
        }
        lastX = x; lastY = y; lastZ = z; lastTime = timeSeconds;
        sampled = true;
        return true;
    }
}

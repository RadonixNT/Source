using NT;
using NT.Controller.PLC;
using NT.LinkManager;

namespace RouterXYZBCTC;

internal sealed class LoaderLinks
{
    internal LinkGroup Group { get; }
    internal IntegerLink UpTime { get; }
    internal IntegerLink DownTime { get; }
    internal IntegerLink PickDelay { get; }
    internal IntegerLink ReleaseDelay { get; }
    internal RealLink StartPosition { get; }
    internal RealLink EndPosition { get; }
    internal RealLink Velocity { get; }
    internal LoaderLinks(PLCWrapper plc, LinkGroup parent)
    {
        Group = parent.AddGroup("Loader", "Loader", "Loading route and vacuum settling delays. Stop or fault preserves vacuum; release it explicitly after recovery.");
        Group.IconKey = "parameter";
        UpTime = Group.AddInteger("LoaderUpTime", "Up Time", 1000, 0, 60000, "Up delay in milliseconds; an assigned sensor uses three times this value as its deadline.");
        DownTime = Group.AddInteger("LoaderDownTime", "Down Time", 1000, 0, 60000, "Down delay in milliseconds; an assigned sensor uses three times this value as its deadline.");
        PickDelay = Group.AddInteger("LoaderPickDelay", "Vacuum Pick Delay", 1000, 0, 60000, "Milliseconds to wait after vacuum turns on before the transfer stroke.");
        ReleaseDelay = Group.AddInteger("LoaderReleaseDelay", "Vacuum Release Delay", 1000, 0, 60000, "Milliseconds to wait after vacuum turns off before raising the loader.");
        StartPosition = Group.AddReal("LoaderStartPosition", "Start Position", 0, -1e9, 1e9, 3, "Loader start position in millimeters.");
        EndPosition = Group.AddReal("LoaderEndPosition", "End Position", 0, -1e9, 1e9, 3, "Loader end position in millimeters.");
        Velocity = Group.AddReal("LoaderVelocity", "Transfer Velocity", 100, 0.001, 1000000, 3, "Transfer stroke velocity in mm/s. Approach and Z lift use traverse velocity.");
        foreach (Link link in new Link[] { UpTime, DownTime, PickDelay, ReleaseDelay, StartPosition, EndPosition, Velocity })
            PLCVariableBridge.AddApplicationVariable(plc, link, PLCVariableAccess.ReadOnly);
    }
}

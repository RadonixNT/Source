using NT;
using NT.Controller;
using NT.Controller.PLC;
using NT.LinkManager;

namespace CutterXYZ;

internal sealed class CutterLinks
{
    internal LinkGroup Group { get; }
    internal CutterLinks(PLCWrapper plc, LinkGroup parentGroup)
    {
        ArgumentNullException.ThrowIfNull(plc);
        ArgumentNullException.ThrowIfNull(parentGroup);
        Group = parentGroup.AddGroup("Cutter", "Cutter", "Cutter-specific machine settings.");
        Group.IconKey = "parameter";
    }
}

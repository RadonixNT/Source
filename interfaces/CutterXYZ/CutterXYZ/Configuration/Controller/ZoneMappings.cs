using NT.Diagnostics;
using NT.LinkManager;
using System.Text.RegularExpressions;

namespace CutterXYZ;

internal sealed partial class ControllerLinks
{
    private int zoneMappingCount = Initialize.DefaultZoneCount;

    private bool IsActiveZoneMapping(string name) => !TryZoneIndex(name, out int index) || index <= zoneMappingCount;
    private static bool TryZoneIndex(string name, out int index)
    {
        index = 0;
        return name.StartsWith("Zone", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(4), out index);
    }

    internal void SetZoneMappingCount(int count)
    {
        if (count is < 0 or > Initialize.MaximumZoneCount) throw new ArgumentOutOfRangeException(nameof(count));
        zoneMappingCount = count;
        foreach (var (mapping, links) in plcIoMappingLinks)
            if (TryZoneIndex(mapping.Name, out int index))
                links.Group.View = index <= count ? AccessView.ReadWrite : AccessView.Hidden;
        ApplyPlcIoMappingProperties();
    }

    internal bool ValidateZoneOutputOwnership(int count)
    {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i <= count; i++)
        {
            var mapping = controller.PLC.IOMappings[$"Zone{i}"];
            if (!mapping.IsPhysicalTarget) continue;
            owned.Add(mapping.Name);
            owned.Add(mapping.Target);
        }
        if (owned.Count == 0) return true;
        foreach (var (name, source) in plcSourceLinks.Where(item => item.Value.View != AccessView.Hidden))
        {
            foreach (Match assignment in Regex.Matches(WithoutComments(source.Value), @"\b([A-Za-z_][A-Za-z_0-9]*(?:\s*\.\s*[A-Za-z_][A-Za-z_0-9]*)?)\s*:=", RegexOptions.CultureInvariant))
                if (owned.Contains(Regex.Replace(assignment.Groups[1].Value, @"\s", "")))
                {
                    NTLog.Warning($"PLC {name} writes {assignment.Groups[1].Value}, which is owned by application zone control.");
                    return false;
                }
        }
        return true;
    }
}

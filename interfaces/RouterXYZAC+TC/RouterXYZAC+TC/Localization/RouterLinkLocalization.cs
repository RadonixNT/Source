using NT.LinkManager;
using NT.Localization;

namespace RouterXYZACTC;

/// <summary>Captures stable text IDs once; refreshes captions only on language/text changes.</summary>
internal sealed class RouterLinkLocalization
{
    private readonly List<(Access Target, NTTextId Text)> captions = [];

    internal RouterLinkLocalization(LinkGroup root, IEnumerable<CommandLink> commands)
    {
        var texts = NTLocalization.GetTextIds()
            .Where(id => id.Value.StartsWith("Radonix.RouterXYZACTC.Links.", StringComparison.Ordinal))
            .ToDictionary(id => NTLocalization.GetDefaultText(NTLanguage.English, id), id => id, StringComparer.Ordinal);
        var seen = new HashSet<Access>();
        void Capture(Access item)
        {
            if (seen.Add(item) && texts.TryGetValue(item.DisplayName, out var id)) captions.Add((item, id));
        }
        void Visit(LinkGroup group)
        {
            Capture(group);
            foreach (var link in group.Members.Values) Capture(link);
            foreach (var child in group.Groups.Values) Visit(child);
        }
        Visit(root);
        foreach (var command in commands) Capture(command);
    }

    internal void Refresh()
    {
        foreach (var (target, text) in captions) target.DisplayName = NTLocalization.Get(text);
    }
}

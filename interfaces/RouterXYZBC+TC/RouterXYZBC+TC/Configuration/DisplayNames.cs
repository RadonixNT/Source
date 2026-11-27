
namespace RouterXYZBCTC;

/// <summary>Construction-time English fallback captions; never changes Link identities.</summary>
internal static class DisplayNames
{
    internal static string FromIdentifier(string name)
    {
        return NT.DisplayNameFormatter.FromIdentifier(name);
    }
}

using System.Text;
using System.Text.RegularExpressions;

namespace RouterXYZACTC;

internal sealed partial class ControllerLinks
{
    // Configuration-time scans also accept incomplete PLC drafts. Ignore comments and
    // literals so documentation and quoted text cannot create hardware dependencies.
    private static IEnumerable<string> FindRawDeviceReferences(string source, string devicePattern)
        => Regex.Matches(WithoutComments(source), @"(?<![A-Za-z_0-9.])" + devicePattern + @"(?=\s*\.)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Value.ToUpperInvariant());

    private static string WithoutComments(string source)
    {
        var result = new StringBuilder(source.Length);
        int depth = 0;
        bool line = false;
        char quote = '\0';
        for (int i = 0; i < source.Length; i++)
        {
            char current = source[i], next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (line) { if (current is '\r' or '\n') { line = false; result.Append(' '); } continue; }
            if (quote != '\0')
            {
                if (current == quote) { if (next == quote) i++; else quote = '\0'; }
                continue;
            }
            if (current == '(' && next == '*') { depth++; i++; result.Append(' '); continue; }
            if (depth > 0) { if (current == '*' && next == ')') { depth--; i++; } continue; }
            if (current == '/' && next == '/') { line = true; i++; result.Append(' '); continue; }
            if (current is '\'' or '"') { quote = current; result.Append(' '); continue; }
            result.Append(current);
        }
        return result.ToString();
    }
}

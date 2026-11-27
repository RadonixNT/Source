using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace RouterXYZACTC;

/// <summary>Upgrade transfer and operator command names; retain values and custom PLC logic.</summary>
internal static class TransferNameMigration
{
    private static readonly Dictionary<string, string> Programs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LoaderOn"] = "LowerLoader", ["LoaderOff"] = "RaiseLoader",
        ["UnloaderOn"] = "LowerUnloader", ["UnloaderOff"] = "RaiseUnloader",
        ["LoaderPick"] = "PickWithLoader", ["LoaderRelease"] = "ReleaseFromLoader"
    };
    private static readonly Dictionary<string, string> Symbols = new(Programs, StringComparer.OrdinalIgnoreCase)
    {
        ["Loader"] = "LoaderDown", ["Unloader"] = "UnloaderDown",
        ["UnloadUpSensor"] = "UnloaderUpSensor"
    };
    // Comments and string contents are not identifiers. Only exact quoted program
    // names are renamed, supporting PLC calls without altering operator messages.
    private static readonly Regex Tokens = new(
        "//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|\\(\\*[\\s\\S]*?\\*\\)|\"(?:\"\"|[^\"])*\"|'(?:''|[^'])*'|[A-Za-z_][A-Za-z0-9_]*",
        RegexOptions.CultureInvariant);

    internal static string RewriteSource(string source) => Tokens.Replace(source, match =>
    {
        string token = match.Value;
        if (Symbols.TryGetValue(token, out string? symbol)) return symbol;
        if (token.Length >= 2 && token[0] is '\'' or '"' &&
            Programs.TryGetValue(token[1..^1], out string? program))
            return token[0] + program + token[^1];
        return token;
    });

    internal static void Apply(RegistryKey parent)
    {
        using RegistryKey? root = parent.OpenSubKey(Initialize.InterfaceName, writable: true);
        if (root is null) return;
        CopyGroup(root, @"Controller\PLC\Mapping\Digital Inputs", "UnloadUpSensor", "UnloaderUpSensor");
        CopyGroup(root, @"Controller\PLC\Mapping\Digital Outputs", "Loader", "LoaderDown");
        CopyGroup(root, @"Controller\PLC\Mapping\Digital Outputs", "Unloader", "UnloaderDown");
        using (RegistryKey? keyboard = root.OpenSubKey("Keyboard", writable: true))
        {
            CopyValue(keyboard, "LoaderVacuumOff", "ReleaseLoaderVacuum");
            // Run once: clearing Reset later must not resurrect the old shortcut.
            // A previously assigned Reset takes precedence; retain the legacy value.
            if (keyboard is not null && keyboard.GetValue("ResetCommandMigration") is null)
            {
                if (string.IsNullOrWhiteSpace(keyboard.GetValue("Reset") as string) &&
                    keyboard.GetValue("Stop Run") is string shortcut)
                    keyboard.SetValue("Reset", shortcut);
                keyboard.SetValue("ResetCommandMigration", 1, RegistryValueKind.DWord);
            }
        }
        using (RegistryKey? commands = root.OpenSubKey(@"Controller\PLC\Command Programs", writable: true))
            foreach (var rename in Programs) CopyValue(commands, rename.Key, rename.Value);
        foreach (string group in new[] { "Shared Variables", "Cyclic Programs", "Command Programs" })
        {
            using RegistryKey? key = root.OpenSubKey(@"Controller\PLC\" + group, writable: true);
            if (key is null) continue;
            foreach (string name in key.GetValueNames())
            {
                // Keep the old source values as a recoverable record. New names win
                // on subsequent starts; never copy stale legacy edits over them.
                if (Programs.ContainsKey(name) || key.GetValue(name) is not string source) continue;
                string rewritten = RewriteSource(source);
                if (rewritten != source) key.SetValue(name, rewritten, key.GetValueKind(name));
            }
        }
    }

    private static void CopyValue(RegistryKey? key, string oldName, string newName)
    {
        if (key is null || key.GetValue(newName) is not null || key.GetValue(oldName) is not object value) return;
        key.SetValue(newName, value, key.GetValueKind(oldName));
    }

    private static void CopyGroup(RegistryKey root, string path, string oldName, string newName)
    {
        using RegistryKey? parent = root.OpenSubKey(path, writable: true);
        using RegistryKey? oldKey = parent?.OpenSubKey(oldName);
        using RegistryKey? existing = parent?.OpenSubKey(newName);
        if (oldKey is null || existing is not null) return;
        using RegistryKey target = parent!.CreateSubKey(newName);
        CopyTree(oldKey, target);
    }

    private static void CopyTree(RegistryKey source, RegistryKey target)
    {
        foreach (string name in source.GetValueNames())
            target.SetValue(name, source.GetValue(name)!, source.GetValueKind(name));
        foreach (string name in source.GetSubKeyNames())
        {
            using RegistryKey child = source.OpenSubKey(name)!;
            using RegistryKey destination = target.CreateSubKey(name);
            CopyTree(child, destination);
        }
    }
}

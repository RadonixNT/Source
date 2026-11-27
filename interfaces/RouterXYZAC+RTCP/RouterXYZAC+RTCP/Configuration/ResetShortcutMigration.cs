using Microsoft.Win32;

namespace RouterXYZACRTCP;

/// <summary>Preserves the existing Reset shortcut upgrade independently of equipment settings.</summary>
internal static class ResetShortcutMigration
{
    internal static void Apply(RegistryKey parent)
    {
        using RegistryKey? keyboard = parent.OpenSubKey(Initialize.InterfaceName + @"\Keyboard", writable: true);
        if (keyboard is null || keyboard.GetValue("ResetCommandMigration") is not null) return;

        // Run once so clearing Reset later does not resurrect the old shortcut.
        if (string.IsNullOrWhiteSpace(keyboard.GetValue("Reset") as string) &&
            keyboard.GetValue("Stop Run") is string shortcut)
            keyboard.SetValue("Reset", shortcut);
        keyboard.SetValue("ResetCommandMigration", 1, RegistryValueKind.DWord);
    }
}

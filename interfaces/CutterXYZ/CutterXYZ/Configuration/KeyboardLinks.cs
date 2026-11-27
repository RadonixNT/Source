using NT;
using NT.LinkManager;
using NT.Tools;
using NT.Tools.Bindings;

namespace CutterXYZ;

/// <summary>
/// Publishes the application's explicit command catalog as keyboard-shortcut
/// TextLinks and routes valid application-local shortcuts to those commands.
/// </summary>
internal sealed class KeyboardLinks : IDisposable
{
    private readonly LinkGroup keyboardGroup;
    private readonly MainForm mainForm;
    private NTKeyboardShortcutManager? shortcutManager;
    private readonly Dictionary<TextLink, CommandLink> commandsByShortcut = [];
    private bool disposed;
    internal IEnumerable<CommandLink> Commands => commandsByShortcut.Values;
    internal LogicLink KeyboardEnabled { get; }

    internal KeyboardLinks(
        MainForm mainForm,
        LinkGroup keyboardGroup,
        ControllerLinks controllerLinks,
        GeneralLinks generalLinks,
        CommandLink pauseLink)
    {
        ArgumentNullException.ThrowIfNull(mainForm);
        this.keyboardGroup = keyboardGroup ?? throw new ArgumentNullException(nameof(keyboardGroup));
        ArgumentNullException.ThrowIfNull(controllerLinks);
        ArgumentNullException.ThrowIfNull(generalLinks);

        this.mainForm = mainForm;
        KeyboardEnabled = keyboardGroup.AddLogic("KeyboardEnabled", "Enabled", true,
            "Enables application keyboard shortcuts.");
        KeyboardEnabled.Changed += EnabledChanged;

        // Registration is explicit. CommandLink.Name is the single identity used
        // by Settings, registry persistence, conflict messages, and execution.
        AddCommand(controllerLinks.ConnectLink);
        AddCommand(controllerLinks.DisconnectLink);
        AddCommand(controllerLinks.RunLink);
        AddCommand(pauseLink);
        AddCommand(controllerLinks.ResetLink);
        AddCommand(mainForm.ClearToolPathLink, "Ctrl+Shift+Alt+C");
        AddCommand(controllerLinks.ReturnToWorkLink);
        AddCommand(controllerLinks.GoToReferenceLink);
        AddCommand(controllerLinks.GoHomeLink);
        AddCommand(controllerLinks.GoToParkLink);
        AddCommand(mainForm.zoneLinks.ClampLink);
        AddCommand(mainForm.zoneLinks.ReleaseLink);
        AddCommand(controllerLinks.HomeLink);
        AddCommand(controllerLinks.HomeXLink);
        AddCommand(controllerLinks.HomeYLink);
        AddCommand(controllerLinks.HomeZLink);
        foreach (AxisJogCommandLinks jogCommands in controllerLinks.AxisJogCommands)
        {
            AddCommand(jogCommands.Negative);
            AddCommand(jogCommands.Positive);
            AddCommand(jogCommands.StepNegative);
            AddCommand(jogCommands.StepPositive);
            AddCommand(jogCommands.GoToPosition);
        }
        AddCommand(controllerLinks.XYJogCommands.NegativeXPositiveY);
        AddCommand(controllerLinks.XYJogCommands.PositiveXPositiveY);
        AddCommand(controllerLinks.XYJogCommands.NegativeXNegativeY);
        AddCommand(controllerLinks.XYJogCommands.PositiveXNegativeY);

        AddCommand(generalLinks.OpenFileLink);
        AddCommand(generalLinks.ChangeModeLink, "Ctrl+Shift+Alt+Space");
        AddCommand(generalLinks.SelectStartLineLink, "Ctrl+Shift+Alt+N");
        AddCommand(generalLinks.EditGCodeLink, "Ctrl+Shift+Alt+E");
        AddCommand(generalLinks.OpenConfigurationLink);
        AddCommand(generalLinks.OpenReportLink, "Ctrl+Shift+Alt+PrintScreen");
        AddCommand(generalLinks.OpenScopeLink);
        AddCommand(generalLinks.OpenPlcMonitorLink);
        AddCommand(generalLinks.OpenIoDiagnosticsLink, "Ctrl+Shift+Alt+Delete");
        AddCommand(generalLinks.OpenPlcProgramsLink, "Ctrl+Shift+Alt+P");
        AddCommand(generalLinks.OpenMachineZeroLink, "Ctrl+Shift+Alt+0");
        AddCommand(generalLinks.OpenAxisVelocityLink, "Ctrl+Shift+Alt+V");
        AddCommand(generalLinks.OpenCycleTimingLink, "Ctrl+Shift+Alt+J");
        AddCommand(generalLinks.OpenPlcMemoryLink, "Ctrl+Shift+Alt+M");

        UpdateShortcutManager();
        ValidateSettings();
    }

    internal bool ValidateSettings()
    {
        var parsed = new Dictionary<TextLink, (string Text, Keys Keys)>();
        var groups = new Dictionary<Keys, List<TextLink>>();

        foreach (TextLink link in commandsByShortcut.Keys)
        {
            if (!NTKeyboardShortcut.TryNormalize(
                    link.Value,
                    out string normalized,
                    out Keys shortcut,
                    out string error))
            {
                link.SetState(OperationalState.Error, error);
                continue;
            }

            parsed.Add(link, (normalized, shortcut));
            if (shortcut == Keys.None)
                continue;

            if (!groups.TryGetValue(shortcut, out List<TextLink>? assigned))
            {
                assigned = [];
                groups.Add(shortcut, assigned);
            }
            assigned.Add(link);
        }

        bool valid = parsed.Count == commandsByShortcut.Count;
        foreach ((TextLink link, (string text, Keys shortcut)) in parsed)
        {
            if (shortcut == Keys.None)
            {
                link.SetState(OperationalState.Inactive, "No keyboard shortcut is assigned.");
                continue;
            }

            List<TextLink> assigned = groups[shortcut];
            if (assigned.Count > 1)
            {
                valid = false;
                string conflicts = string.Join(", ", assigned
                    .Where(other => !ReferenceEquals(other, link))
                    .Select(other => other.DisplayName));
                link.SetState(
                    OperationalState.Error,
                    $"Shortcut {text} is also assigned to {conflicts}.");
                continue;
            }

            link.SetState(OperationalState.Valid, $"Shortcut {text} is assigned.");
        }

        if (!KeyboardEnabled.Value)
        {
            keyboardGroup.SetState(OperationalState.Inactive, "Keyboard shortcuts are disabled.");
        }
        else if (!valid)
        {
            keyboardGroup.SetState(
                OperationalState.Error,
                "One or more keyboard shortcuts are invalid or duplicated.");
        }
        else if (parsed.Values.All(item => item.Keys == Keys.None))
        {
            keyboardGroup.SetState(
                OperationalState.Inactive,
                "No keyboard shortcuts are assigned.");
        }
        else
        {
            keyboardGroup.SetState(OperationalState.Valid, "Keyboard shortcuts are valid.");
        }

        return valid;
    }

    internal string? GetConflict(TextLink source, string candidateText)
    {
        if (!NTKeyboardShortcut.TryNormalize(
                candidateText,
                out string normalized,
                out Keys candidate,
                out string error))
        {
            return error;
        }

        if (candidate == Keys.None)
            return null;

        foreach (TextLink other in commandsByShortcut.Keys)
        {
            if (ReferenceEquals(other, source))
                continue;
            if (NTKeyboardShortcut.TryNormalize(other.Value, out _, out Keys assigned, out _)
                && assigned == candidate)
            {
                return $"Shortcut {normalized} is already assigned to {other.DisplayName}.";
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        KeyboardEnabled.Changed -= EnabledChanged;
        foreach (TextLink link in commandsByShortcut.Keys)
            link.Changed -= ShortcutChanged;
        shortcutManager?.Dispose();
        shortcutManager = null;
        commandsByShortcut.Clear();
    }

    private void AddCommand(CommandLink command, string defaultShortcut = NTKeyboardShortcut.NoneText)
    {
        // Preserve an existing shortcut when upgrading the command name.
        if (command.Name == "OpenConfiguration" &&
            keyboardGroup.Registry?.GetValue(command.Name) is null &&
            keyboardGroup.Registry?.GetValue("OpenSettings") is string configurationShortcut)
            defaultShortcut = configurationShortcut;
        // Carry forward the saved shortcut only when the renamed command has no own setting.
        if (command.Name == "SelectStartLine" &&
            keyboardGroup.Registry?.GetValue(command.Name) is null &&
            keyboardGroup.Registry?.GetValue("RunFromLine") is string previousShortcut)
            defaultShortcut = previousShortcut;

        TextLink shortcut = keyboardGroup.AddText(
            command.Name, command.DisplayName,
            defaultShortcut,
            $"Keyboard shortcut for the '{command.DisplayName}' command.");
        if (NTKeyboardShortcut.TryNormalize(
                shortcut.Value,
                out string normalized,
                out _,
                out _)
            && !string.Equals(shortcut.Value, normalized, StringComparison.Ordinal))
        {
            shortcut.Value = normalized;
        }

        commandsByShortcut.Add(shortcut, command);
        shortcut.Changed += ShortcutChanged;
    }

    private void ShortcutChanged(object? sender, LinkChangedEventArgs e)
        => ValidateSettings();

    private void EnabledChanged(object? sender, LinkChangedEventArgs e)
    {
        UpdateShortcutManager();
        ValidateSettings();
    }

    private void UpdateShortcutManager()
    {
        // Disposal balances held jog commands before removing keyboard routing.
        shortcutManager?.Dispose();
        shortcutManager = null;
        if (disposed || !KeyboardEnabled.Value)
            return;

        shortcutManager = new NTKeyboardShortcutManager(mainForm);
        foreach ((TextLink shortcut, CommandLink command) in commandsByShortcut)
            shortcutManager.Add(command, shortcut);
    }
}


using NT.Controller;
using NT.LinkManager;

namespace RouterXYZBCTC;

internal sealed partial class ControllerLinks
{
    private IntegerLink digitalModuleCount = null!;
    private IntegerLink analogModuleCount = null!;
    private IntegerLink encoderModuleCount = null!;
    private LinkGroup[] allModuleGroups = [];

    private LinkGroup[] CreateModuleSettings(LinkGroup parent)
    {
        digitalModuleCount = AddCount("DigitalCount", "Digital Count", Initialize.DefaultDigitalModuleCount, 8);
        analogModuleCount = AddCount("AnalogCount", "Analog Count", Initialize.DefaultAnalogModuleCount, 2);
        encoderModuleCount = AddCount("EncoderCount", "Encoder Count", Initialize.DefaultEncoderModuleCount, 2);
        // Stable bounded Link objects preserve the dialog journal, even when a
        // count reduction hides a group edited earlier in the same dialog.
        allModuleGroups = CreateModuleGroups(parent,
            Enumerable.Range(0, 8).Select(i => $"DM{i}")
                .Concat(Enumerable.Range(0, 2).Select(i => $"AM{i}"))
                .Concat(Enumerable.Range(0, 2).Select(i => $"EM{i}")).ToArray());
        RefreshModuleVisibility();
        return SelectedModuleGroups();

        IntegerLink AddCount(string name, string caption, int initial, int maximum)
        {
            IntegerLink link = parent.AddInteger(name, caption, initial, 0, maximum,
                "Number of modules. Edit while disconnected; changes apply after validation and before the next connection.");
            link.Permission = AccessPermission.Engineering;
            link.UnauthorizedView = AccessView.ReadOnly;
            ConfigureReadOnlyWhileControllerSessionActive(link);
            link.Changed += (_, _) => RefreshModuleVisibility();
            return link;
        }
    }

    private LinkGroup[] SelectedModuleGroups()
        => allModuleGroups.Where(group => IsModuleSelected(group.Name)).ToArray();

    private bool IsModuleSelected(string name)
    {
        var (_, _, index, _) = ParseModuleProfile(name);
        long count = name.StartsWith("DM", StringComparison.Ordinal) ? digitalModuleCount.Value
            : name.StartsWith("AM", StringComparison.Ordinal) ? analogModuleCount.Value : encoderModuleCount.Value;
        return index < count;
    }

    private void RefreshModuleVisibility()
    {
        foreach (LinkGroup group in allModuleGroups)
        {
            group.View = IsModuleSelected(group.Name) ? AccessView.ReadWrite : AccessView.Hidden;
            if (group.View != AccessView.Hidden && (moduleGroups is null || !moduleGroups.Contains(group)))
                RefreshModuleState(group, null);
        }
        RefreshMappingTargetChoices();
    }

    private void RefreshModuleSelection()
    {
        moduleGroups = SelectedModuleGroups();
        moduleOnlineStates = null;
        moduleConfigurationStates = null;
        RefreshModuleVisibility();
    }

    internal bool ValidateModuleTopology()
    {
        string[] selected = SelectedModuleGroups().Select(g => g.Name).ToArray();
        string? error = null;
        if (!controller.ConnectionState.AllowsOfflineConfiguration()
            && !selected.SequenceEqual(moduleGroups.Select(g => g.Name)))
            error = "Disconnect the controller completely before changing module counts.";
        var selectedSet = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = allModuleGroups.Select(g => g.Name).Where(n => !selectedSet.Contains(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependencies = new List<string>();
        foreach (var links in plcIoMappingLinks.Values)
        {
            if (!IsActiveZoneMapping(links.Group.Name)) continue;
            string target = FromDisplayTarget(links.Target.Value);
            int dot = target.IndexOf('.');
            // Disabled mappings retain their target and also require correction.
            if (dot > 0 && missing.Contains(target[..dot]))
                dependencies.Add($"Mapping {links.Group.Name}: {target}");
        }
        foreach (var (name, source) in plcSourceLinks.Where(pair => pair.Value.View != AccessView.Hidden))
            foreach (string module in FindRawModuleReferences(source.Value).Where(missing.Contains).Distinct())
                dependencies.Add($"PLC {name}: {module}");
        if (dependencies.Count != 0)
            error = "Module counts exclude referenced modules. Update these dependencies first:\n" + string.Join("\n", dependencies);
        if (error is null) SetAggregateState(modulesGroup, SelectedModuleGroups());
        else modulesGroup.SetState(OperationalState.Error, error);
        return error is null;
    }

    private bool ValidatePlcWithModuleCandidate()
    {
        LinkGroup[] candidate = SelectedModuleGroups();
        if (!controller.ConnectionState.AllowsOfflineConfiguration() || candidate.SequenceEqual(moduleGroups))
            return ValidatePlcWithInverterCandidate();

        // Mapping validation uses Controller's configured modules. Evaluate the
        // complete offline candidate so adding a module and mapping it can be one
        // edit. Restore the exact applied definitions even on validation failure;
        // only acceptance may replace the applied topology.
        var modules = controller.PLC.Modules;
        var previous = modules.Select(m =>
            (m.IsConfigured, m.Name, m.Uid, m.PlcIndex, m.Type, m.InternalConfiguration, m.Mode)).ToArray();
        try
        {
            WriteModuleProperties(candidate);
            return ValidatePlcWithInverterCandidate();
        }
        finally
        {
            modules.Clear();
            for (int i = 0; i < previous.Length; i++)
            {
                var saved = previous[i];
                if (!saved.IsConfigured) continue;
                var module = modules[i];
                module.Name = saved.Name;
                module.Uid = saved.Uid;
                module.PlcIndex = saved.PlcIndex;
                module.Type = saved.Type;
                module.InternalConfiguration = saved.InternalConfiguration;
                module.Mode = saved.Mode;
            }
        }
    }

    internal static IEnumerable<string> FindRawModuleReferences(string source)
        => FindRawDeviceReferences(source, @"(?:DM|AM|EM)[0-9]");
}

using NT.Controller;
using NT.LinkManager;

namespace RouterXYZACRTCP;

internal sealed partial class ControllerLinks
{
    private LinkGroup inverterGroup = null!;
    private IntegerLink inverterCount = null!;

    private IEnumerable<string> SelectedInverterNames() => Enumerable.Range(0, (int)inverterCount.Value).Select(i => $"INV{i}");
    private bool CanEditInverterCount() => controller.ConnectionState.AllowsOfflineConfiguration()
        && controller.PLC.IOMappings.CanModify && !mainForm.IsProgramLoading && !mainForm.IsConnectionTransition;
    private bool InverterTopologyMatches() => controller.MotionController.Inverters
        .All(inverter => inverter.Configuration.Enabled == (inverter.Index < inverterCount.Value));

    private void RefreshInverterVisibility()
    {
        for (int i = 0; i < 2; i++)
            inverterGroup.Groups[$"INV{i}"].View = i < inverterCount.Value ? AccessView.ReadWrite : AccessView.Hidden;
        RefreshMappingTargetChoices();
    }

    internal bool ValidateInverterTopology()
    {
        string? error = null;
        // The connection flow marks its transition before applying an already accepted draft.
        // Core's offline/empty-path gate is authoritative for application; UI edit gates are stricter.
        if (!InverterTopologyMatches() && (!controller.ConnectionState.AllowsOfflineConfiguration() || !controller.PLC.IOMappings.CanModify))
            error = "Disconnect the controller completely and clear every ToolPath before changing inverter count.";
        var missing = Enumerable.Range((int)inverterCount.Value, 2 - (int)inverterCount.Value)
            .Select(i => $"INV{i}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependencies = new List<string>();
        foreach (var links in plcIoMappingLinks.Values)
        {
            string target = FromDisplayTarget(links.Target.Value);
            int dot = target.IndexOf('.');
            // Disabled mappings retain their assignment and must not silently become dangling targets.
            if (dot > 0 && missing.Contains(target[..dot])) dependencies.Add($"Mapping {links.Group.Name}: {target}");
        }
        foreach (var (name, source) in plcSourceLinks.Where(item => item.Value.View != AccessView.Hidden))
            foreach (string inverter in FindRawInverterReferences(source.Value).Where(missing.Contains).Distinct())
                dependencies.Add($"PLC {name}: {inverter}");
        if (dependencies.Count != 0)
            error = "Inverter count excludes referenced devices. Update these dependencies first:\n" + string.Join("\n", dependencies);
        inverterGroup.SetState(error is null ? OperationalState.Normal : OperationalState.Error, error ?? string.Empty);
        return error is null;
    }

    internal static IEnumerable<string> FindRawInverterReferences(string source)
        => FindRawDeviceReferences(source, @"INV[01]");

    private void ApplyInverterProperties()
    {
        if (InverterTopologyMatches()) return;
        if (!ValidateInverterTopology()) throw new InvalidOperationException(inverterGroup.StateMessage);
        WriteInverterProperties();
    }

    private void WriteInverterProperties()
    {
        foreach (var inverter in controller.MotionController.Inverters)
            inverter.Configuration.Enabled = inverter.Index < inverterCount.Value;
    }

    // Validate the whole draft against temporary membership, then restore the applied
    // configuration even on failure. Only accepting settings commits membership.
    private bool ValidatePlcWithInverterCandidate()
    {
        if (!ValidateInverterTopology()) return false;
        if (InverterTopologyMatches()) return ValidateCurrentPlcConfiguration();
        bool[] previous = controller.MotionController.Inverters.Select(inverter => inverter.Configuration.Enabled).ToArray();
        try
        {
            WriteInverterProperties();
            return ValidateCurrentPlcConfiguration();
        }
        finally
        {
            for (int i = 0; i < previous.Length; i++) controller.MotionController.Inverters[i].Configuration.Enabled = previous[i];
        }
    }

    private sealed class InverterCountSource(long value, Func<bool> canEdit) : IIntegerSource
    {
        public event EventHandler? ValueChanged { add { } remove { } }
        public long GetValue() => value;
        public void SetValue(long next)
        {
            if (next != value && !canEdit()) throw new InvalidOperationException("Disconnect and clear every ToolPath before changing inverter count.");
            value = next;
        }
    }
}

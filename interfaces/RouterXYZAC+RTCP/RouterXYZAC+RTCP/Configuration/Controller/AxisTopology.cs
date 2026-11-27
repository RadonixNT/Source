using NT;
using NT.Controller;
using NT.LinkManager;

namespace RouterXYZACRTCP;

internal sealed partial class ControllerLinks
{
    private readonly Dictionary<string, LogicLink> gantryAxisLinks = new(StringComparer.Ordinal);
    private LinkGroup[] allAxisGroups = [];
    private readonly Dictionary<LinkGroup, string> axisGroupNames = new();
    private IntegerLink configuredAxesLink = null!;
    private IntegerLink licensedAxesLink = null!;

    private void CreateGantrySettings()
    {
        foreach (var axis in Initialize.AxisProfile.Where(axis => axis.GantryOrder.HasValue))
        {
            string name = axis.Name + "G";
            LogicLink link = axesGroup.AddLogic(name, $"{axis.Name} Gantry", axis.GantryByDefault,
                "Disabled", "Enabled",
                $"Enable gantry operation for {axis.Name} by adding its companion axis {name}. Requires a free licensed axis and an offline controller. Settings are retained when disabled.");
            link.Permission = AccessPermission.Engineering;
            link.UnauthorizedView = AccessView.ReadOnly;
            ConfigureReadOnlyWhileControllerSessionActive(link);
            gantryAxisLinks.Add(name, link);
            link.Changed += (_, _) => RefreshGantryVisibility();
        }
        CreateHomeSequenceSetting();
        configuredAxesLink = axesGroup.AddInteger("ConfiguredCount", "Configured Axis Count", 0, 0, ControllerWrapper.MaxAxisCount);
        configuredAxesLink.View = AccessView.ReadOnly;
        licensedAxesLink = axesGroup.AddInteger("LicensedCount", "Axis Capacity", 0, 0, ControllerWrapper.MaxAxisCount,
            "Verified license axis capacity, or the simulator limit when no license is supplied.");
        licensedAxesLink.View = AccessView.ReadOnly;
        licenseKeyLink.Changed += (_, _) => RefreshGantryVisibility();
        controllerGroup.Members["Mode"].Changed += (_, _) => RefreshGantryVisibility();
    }

    private bool IsAxisSelected(string name)
        => !gantryAxisLinks.TryGetValue(name, out LogicLink? link) || link.Value;

    private LinkGroup[] SelectedAxisGroups()
        => allAxisGroups.Where(group => IsAxisSelected(axisGroupNames[group])).ToArray();

    private void RefreshGantryVisibility()
    {
        int index = 0;
        foreach (LinkGroup group in allAxisGroups)
        {
            bool selected = IsAxisSelected(axisGroupNames[group]);
            group.View = selected ? AccessView.ReadWrite : AccessView.Hidden;
            // Match the compact controller/PLC slot order of the current draft.
            if (selected) group.DisplayName = $"{axisGroupNames[group]} [AX{index++:00}]";
        }
        if (configuredAxesLink is not null) configuredAxesLink.Value = Initialize.MainAxes.Length + gantryAxisLinks.Values.Count(link => link.Value);
        if (licensedAxesLink is not null) licensedAxesLink.Value = AxisCapacity;
        RefreshHomeSources();
    }

    private int AxisCapacity => controllerGroup.ReadText("Mode") == "Simulation"
        && string.IsNullOrWhiteSpace(licenseKeyLink.Value)
        ? ControllerWrapper.MaxAxisCount : AxisLicenseCapacity.Read(licenseKeyLink.Value) ?? 0;

    internal bool ValidateAxisTopology()
    {
        if (!RefreshHomeSources()) { axesGroup.SetState(OperationalState.Error, homeSequenceLink.StateMessage); return false; }
        LinkGroup[] candidate = SelectedAxisGroups();
        string? error = null;
        if (candidate.Length > AxisCapacity)
            error = $"The configuration requires {candidate.Length} axes; the verified license permits {AxisCapacity}. Import a valid license or reduce the axis count.";
        if (!candidate.SequenceEqual(axisGroups) && (!controller.ConnectionState.AllowsOfflineConfiguration()
            || mainForm.IsProgramLoading || mainForm.IsConnectionTransition
            || controller.MotionController.ToolPaths.Any(path => path.BlockCount != 0)))
            error = "Disconnect the controller completely, finish pending work and clear every ToolPath before changing gantry axes.";
        string[] candidateNames = candidate.Select(group => axisGroupNames[group]).ToArray();
        var missing = allAxisGroups.Where(group => !IsAxisSelected(axisGroupNames[group])).Select(group => axisGroupNames[group]).ToArray();
        var dependencies = new List<string>();
        foreach (var (name, link) in plcSourceLinks.Where(pair => pair.Value.View != AccessView.Hidden))
        {
            foreach (string reference in FindRawAxisReferences(link.Value).Distinct())
            {
                bool affected = missing.Contains(reference);
                if (reference.StartsWith("AX", StringComparison.Ordinal))
                {
                    int slot = int.Parse(reference.AsSpan(2), System.Globalization.CultureInfo.InvariantCulture);
                    affected = slot >= candidateNames.Length || slot >= controller.Axes.Length
                        || candidateNames[slot] != controller.Axes[slot].Name;
                }
                if (affected) dependencies.Add($"PLC {name}: {reference}");
            }
        }
        if (dependencies.Count != 0)
            error = "Axes are still referenced by PLC programs. Update these dependencies before disabling them:\n" + string.Join("\n", dependencies);
        axesGroup.SetState(error is null ? OperationalState.Normal : OperationalState.Error, error ?? string.Empty);
        return error is null;
    }

    internal bool AcceptGantryDraft(Link link)
    {
        if (!gantryAxisLinks.Values.Contains(link) && !ReferenceEquals(link, homeSequenceLink)) return true;
        return ValidateAxisTopology();
    }

    internal static IEnumerable<string> FindRawAxisReferences(string source)
    {
        for (int i = 0; i < source.Length;)
        {
            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] is not ('\r' or '\n')) i++;
                continue;
            }
            if (i + 1 < source.Length && source[i] == '(' && source[i + 1] == '*')
            {
                i += 2;
                int depth = 1;
                while (i < source.Length && depth > 0)
                {
                    if (i + 1 < source.Length && source[i] == '(' && source[i + 1] == '*') { depth++; i += 2; }
                    else if (i + 1 < source.Length && source[i] == '*' && source[i + 1] == ')') { depth--; i += 2; }
                    else i++;
                }
                continue;
            }
            if (char.IsLetter(source[i]) || source[i] == '_')
            {
                int start = i++;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                string word = source[start..i].ToUpperInvariant();
                if (word is "XG" or "YG" or "ZG"
                    || word.Length == 4 && word.StartsWith("AX", StringComparison.Ordinal)
                        && char.IsAsciiDigit(word[2]) && char.IsAsciiDigit(word[3])
                        && int.Parse(word.AsSpan(2), System.Globalization.CultureInfo.InvariantCulture) < ControllerWrapper.MaxAxisCount)
                    yield return word;
            }
            else i++;
        }
    }

    private bool ValidatePlcWithAxisCandidate()
    {
        string[] names = SelectedAxisGroups().Select(group => axisGroupNames[group]).ToArray();
        string[] previous = controller.Axes.Select(axis => axis.Name).ToArray();
        if (names.SequenceEqual(previous)) return ValidatePlcWithModuleCandidate();
        var masters = controller.Axes.Select(axis => axis.CouplingMaster?.Name).ToArray();
        try
        {
            controller.ConfigureAxes(names);
            return ValidatePlcWithModuleCandidate();
        }
        finally
        {
            controller.ConfigureAxes(previous);
            for (int index = 0; index < previous.Length; index++)
                controller.Axes[index].CouplingMaster = masters[index] is { } name ? controller.GetAxisByName(name) : null;
        }
    }
}

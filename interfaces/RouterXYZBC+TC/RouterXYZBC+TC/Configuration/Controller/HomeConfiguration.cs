using NT;
using NT.LinkManager;

namespace RouterXYZBCTC;

internal sealed partial class ControllerLinks
{
    private TextLink homeSequenceLink = null!;
    private bool refreshingHomeSources;

    private void CreateHomeSequenceSetting()
    {
        homeSequenceLink = axesGroup.AddText("HomeSequence", "Home Sequence", "Z>B,C>X,Y",
            "Include sensor axes once. Include absolute axes to move to Home Position, or omit them to retain position. > starts the next stage; , runs sensor search/release in parallel. Example: Z>B,C>X,Y or Z>B>C>X>Y. Absolute positioning finishes before sensor search in a shared stage. Sensor displacements finish sequentially. A gantry with its own enabled physical home sensor homes independently; otherwise it remains coupled to its master.");
        homeSequenceLink.Permission = AccessPermission.Engineering;
        homeSequenceLink.UnauthorizedView = AccessView.ReadOnly;
        ConfigureReadOnlyWhileControllerSessionActive(homeSequenceLink);
        homeSequenceLink.QueryEnabled += (_, e) => e.Enabled &= !mainForm.IsProgramLoading
            && controller.MotionController.ToolPaths.All(path => path.BlockCount == 0);
        homeSequenceLink.Changed += (_, _) => RefreshHomeSources();
    }

    private bool RefreshHomeSources()
    {
        if (refreshingHomeSources || homeSequenceLink is null || plcSourceLinks.Count == 0) return true;
        refreshingHomeSources = true;
        try
        {
            bool IsAbsolute(string axis) => axisGroupNames.Single(pair => pair.Value == axis).Key.ReadText("HomingMethod")
                == nameof(NT.Controller.HomingMethod.AbsoluteEncoder);
            var groups = Initialize.MainAxes.Select(axis =>
            {
                bool absolute = IsAbsolute(axis);
                string? companion = gantryAxisLinks.TryGetValue(axis + "G", out var enabled) && enabled.Value ? axis + "G" : null;
                if (companion is not null && IsAbsolute(companion) != absolute)
                    throw new InvalidOperationException($"Generated gantry homing requires matching Homing methods for {axis} and {companion}.");
                if (!absolute && !HasHomeSensor(axis))
                    throw new InvalidOperationException($"Assign an enabled physical home sensor for {axis} before using generated homing.");
                return new HomeProgramGenerator.Group(axis, companion, !absolute && companion is not null && HasHomeSensor(companion), absolute);
            }).ToArray();
            string[][] sequence = HomeProgramGenerator.ParseSequence(homeSequenceLink.Value, Initialize.MainAxes,
                groups.Where(group => group.Absolute).Select(group => group.Master).ToArray());
            var sources = new Dictionary<string, string>(StringComparer.Ordinal);
            var axisNames = Initialize.AxisDefinitions.Select(definition => definition.Name).ToArray();
            foreach (string axis in Initialize.MainAxes)
                sources.Add("Home" + axis, HomeProgramGenerator.Generate([[axis]], groups, axisNames));
            sources.Add(Initialize.HomePlcProgramName, HomeProgramGenerator.Generate(sequence, groups, axisNames, fullHome: true));
            foreach (var (name, source) in sources)
            {
                if (!plcSourceLinks.TryGetValue(name, out TextLink? link)) continue;
                // Only untouched generated or current embedded factory text is application-owned.
                // Edited PLC text is never silently replaced.
                if (HomeProgramGenerator.IsGenerated(link.Value) || link.Value == Initialize.GetPlcSource(name))
                {
                    if (link.Value != source) link.Value = source;
                    link.Description = "Generated from Axes/Home Sequence, gantry selection and physical home-sensor mappings. Editing this text makes it engineer-owned and stops automatic replacement.";
                }
                else link.Description = "Engineer-owned homing source. Home Sequence and sensor changes do not replace this text; update and validate it for the selected axes.";
            }
            homeSequenceLink.SetState(OperationalState.Normal, string.Empty);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            homeSequenceLink.SetState(OperationalState.Error, error.Message);
            return false;
        }
        finally { refreshingHomeSources = false; }
    }

    private bool HasHomeSensor(string axis)
    {
        var pair = plcIoMappingLinks.Single(pair => pair.Key.Name == "HomeSensor" + axis);
        if (!pair.Value.Enabled.Value) return false;
        string target = FromDisplayTarget(pair.Value.Target.Value);
        if (target.Length == 0) return false;
        if (!pair.Key.IsPhysicalTarget)
            throw new InvalidOperationException($"HomeSensor{axis} must select a physical input, not a constant.");
        return true;
    }
}

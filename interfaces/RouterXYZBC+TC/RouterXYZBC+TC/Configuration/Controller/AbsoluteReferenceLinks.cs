using System.Text.Json;
using NT.Controller;
using NT.LinkManager;

namespace RouterXYZBCTC;

internal sealed partial class ControllerLinks
{
    private void CreateAbsoluteReferenceLinks(LinkGroup group)
    {
        var method = group.AddText("HomingMethod", "Homing method", nameof(HomingMethod.HomeSensor),
            Enum.GetNames<HomingMethod>(), "Reference source for this axis.");
        method.Permission = AccessPermission.Engineering;
        ConfigureReadOnlyWhileControllerSessionActive(method);
        method.Changed += (_, _) => RefreshHomeSources();
        var saved = group.AddText("MotorMachineZero", "Machine zero", string.Empty);
        saved.View = AccessView.Hidden;
        group.AddText("SimulationMachineZero", "Simulation machine zero", string.Empty).View = AccessView.Hidden;
    }

    internal void OpenMachineZeroForm()
    {
        var access = generalLinks.AccessSession;
        access.OpenConfiguration(mainForm, mainForm.AttachDialogTheme);
        if (!access.CanEngineer) return;
        using var dialog = CreateMachineZeroForm();
        mainForm.AttachDialogTheme(dialog);
        dialog.ShowDialog(mainForm);
    }

    internal NT.ConfigurationManager.MachineZeroForm CreateMachineZeroForm() => new(controller,
        axis => axisGroups[axis.Index].GetTextLink("MotorMachineZero"),
        () => generalLinks.AccessSession.CanEngineer,
        axis => axisGroups[axis.Index].GetTextLink("SimulationMachineZero"));

    internal string HomeFailureMessage(NT.Controller.PLC.PLCProgram program)
    {
        if (!HomeProgramGenerator.IsGenerated(program.Source))
            return NT.Localization.NTLocalization.Get(RouterXYZBCTCTexts.HomeUnspecified);
        string suffix = program.Name == Initialize.HomePlcProgramName ? "All" : program.Name[4..];
        NT.Controller.PLC.INTEGER diagnostic = controller.PLC.ApplicationVariables["HomeFailure" + suffix];
        long code = diagnostic.Value;
        long index = code / 10 - 1;
        if (index < 0 || index >= Initialize.AxisDefinitions.Length)
            return NT.Localization.NTLocalization.Get(RouterXYZBCTCTexts.HomeUnspecified);
        string name = Initialize.AxisDefinitions[index].Name;
        var key = (code % 10) switch
        {
            1 => RouterXYZBCTCTexts.HomeDriveFault,
            2 => RouterXYZBCTCTexts.HomeCouplingFault,
            3 => ReferenceFailure(name),
            4 => RouterXYZBCTCTexts.HomeMoving,
            5 => RouterXYZBCTCTexts.HomeOutside,
            _ => RouterXYZBCTCTexts.HomeUnspecified
        };
        return NT.Localization.NTLocalization.Format(key, name);
    }

    private NT.Localization.NTTextId ReferenceFailure(string name)
    {
        var axis = controller.Axes.Single(a => a.Name == name);
        var group = axisGroups[axis.Index];
        bool simulation = controller.MotionController.SimulationMode == SimulationModes.Hard;
        string key = simulation && !string.IsNullOrEmpty(group.ReadText("SimulationMachineZero"))
            ? "SimulationMachineZero" : "MotorMachineZero";
        if (string.IsNullOrWhiteSpace(group.ReadText(key)))
            return simulation ? RouterXYZBCTCTexts.HomeSimulationZero : RouterXYZBCTCTexts.HomeMissingZero;
        var zero = ReadZero(group, key);
        return zero is null || zero.Step != controller.MotionController.Axes[axis.Index].Step
            ? RouterXYZBCTCTexts.HomeInvalidZero : RouterXYZBCTCTexts.HomeReferenceLost;
    }

    private static void ApplyAbsoluteReference(LinkGroup group, Axis axis)
    {
        axis.HomingMethod = Enum.Parse<HomingMethod>(group.ReadText("HomingMethod"));
        axis.ConfigureAbsoluteReference(ReadZero(group, "MotorMachineZero"));
        axis.ConfigureSimulationAbsoluteReference(ReadZero(group, "SimulationMachineZero"));
    }

    private static AbsoluteMachineZero? ReadZero(LinkGroup group, string key)
    {
        AbsoluteMachineZero? zero = null;
        string saved = group.ReadText(key);
        if (saved.Length != 0)
        {
            try { zero = JsonSerializer.Deserialize<AbsoluteMachineZero>(saved); }
            catch (JsonException) { group.SetState(OperationalState.Warning, "Invalid stored machine zero. Capture a new zero."); }
        }
        return zero;
    }
}

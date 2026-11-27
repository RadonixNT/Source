using NT.Localization;

namespace RouterXYZACRTCP;

/// <summary>Application-owned translation catalog and stable text identities.</summary>
internal static class RouterXYZACRTCPTexts
{
    private const string Prefix = "Radonix.RouterXYZACRTCP.";
    private static bool registered;
    internal static readonly NTTextId OpeningGCode = Id("Progress.OpeningGCode");
    internal static readonly NTTextId ProgramCounts = Id("Progress.ProgramCounts");
    internal static readonly NTTextId ExecutionTiming = Id("Progress.ExecutionTiming");

    internal static readonly NTTextId Online = Id("Main.Online");
    internal static readonly NTTextId XAxis = Id("Main.XAxis");
    internal static readonly NTTextId YAxis = Id("Main.YAxis");
    internal static readonly NTTextId ZAxis = Id("Main.ZAxis");
    internal static readonly NTTextId AAxis = Id("Main.AAxis");
    internal static readonly NTTextId CAxis = Id("Main.CAxis");
    internal static readonly NTTextId Alarm = Id("Main.Alarm");
    internal static readonly NTTextId Emergency = Id("Main.Emergency");
    internal static readonly NTTextId Configuration = Id("Main.Settings");
    internal static readonly NTTextId RefreshRate = Id("Settings.RefreshRate");
    internal static readonly NTTextId RefreshRateDescription = Id("Settings.RefreshRateDescription");
    internal static readonly NTTextId ConfigurationPathResetQuestion = Id("Message.SettingsPathResetQuestion");
    internal static readonly NTTextId ConfigurationMotionBusy = Id("Message.SettingsMotionBusy");
    internal static readonly NTTextId NoProgramLoaded = Id("Main.NoProgramLoaded");
    internal static readonly NTTextId MachinePosition = Id("Main.MachinePosition");
    internal static readonly NTTextId WorkPosition = Id("Main.WorkPosition");
    internal static readonly NTTextId PositionMm = Id("Main.PositionMm");
    internal static readonly NTTextId StepMm = Id("Main.StepMm");
    internal static readonly NTTextId VelocityMmPerSecond = Id("Main.VelocityMmPerSecond");
    internal static readonly NTTextId MachineFeedPlaceholder = Id("Main.MachineFeedPlaceholder");
    internal static readonly NTTextId StepJog = Id("Main.StepJog");
    internal static readonly NTTextId Stop = Id("Main.Stop");
    internal static readonly NTTextId Home = Id("Main.Home");
    internal static readonly NTTextId HomeMissingZero = Id("Message.HomeMissingZero");
    internal static readonly NTTextId HomeSimulationZero = Id("Message.HomeSimulationZero");
    internal static readonly NTTextId HomeDriveFault = Id("Message.HomeDriveFault");
    internal static readonly NTTextId HomeCouplingFault = Id("Message.HomeCouplingFault");
    internal static readonly NTTextId HomeReferenceLost = Id("Message.HomeReferenceLost");
    internal static readonly NTTextId HomeMoving = Id("Message.HomeMoving");
    internal static readonly NTTextId HomeOutside = Id("Message.HomeOutside");
    internal static readonly NTTextId HomeInvalidZero = Id("Message.HomeInvalidZero");
    internal static readonly NTTextId HomeUnspecified = Id("Message.HomeUnspecified");
    internal static readonly NTTextId Homed = Id("Main.Homed");
    internal static readonly NTTextId Reference = Id("Main.Reference");
    internal static readonly NTTextId GoHome = Id("Main.GoHome");
    internal static readonly NTTextId GoReference = Id("Main.GoReference");
    internal static readonly NTTextId SetXZero = Id("Main.SetXZero");
    internal static readonly NTTextId SetYZero = Id("Main.SetYZero");
    internal static readonly NTTextId SetZZero = Id("Main.SetZZero");
    internal static readonly NTTextId Run = Id("Main.Run");
    internal static readonly NTTextId Pause = Id("Main.Pause");
    internal static readonly NTTextId OpenFile = Id("Main.OpenFile");
    internal static readonly NTTextId EditGCode = Id("Main.EditGCode");
    internal static readonly NTTextId StartLine = Id("Main.StartLine");
    internal static readonly NTTextId ReferenceMm = Id("Main.ReferenceMm");
    internal static readonly NTTextId SpindleProperties = Id("Main.SpindleProperties");
    internal static readonly NTTextId RtcpPart = Id("Main.RtcpPart");
    internal static readonly NTTextId ToolLengthMm = Id("Main.ToolLengthMm");
    internal static readonly NTTextId ToolLengthApplying = Id("Main.ToolLengthApplying");
    internal static readonly NTTextId ToolLengthConfirm = Id("Main.ToolLengthConfirm");
    internal static readonly NTTextId Spindle = Id("Main.Spindle");
    internal static readonly NTTextId DesignPreview = Id("Main.DesignPreview");
    internal static readonly NTTextId SwitchToDarkTheme = Id("Main.SwitchToDarkTheme");
    internal static readonly NTTextId SwitchToLightTheme = Id("Main.SwitchToLightTheme");
    internal static readonly NTTextId ControllerConnectionFailed = Id("Message.ControllerConnectionFailed");
    internal static readonly NTTextId SimulationModeNotice = Id("Message.SimulationModeNotice");
    internal static readonly NTTextId ConfigurationSaveFailed = Id("Message.SettingsSaveFailed");
    internal static readonly NTTextId ProgramLoadFailed = Id("Message.ProgramLoadFailed");
    internal static readonly NTTextId ConfirmExitUnfinishedProgram = Id("Message.ConfirmExitUnfinishedProgram");
    internal static readonly NTTextId ProgramLoadCancelled = Id("Message.ProgramLoadCancelled");
    internal static readonly NTTextId ProgramLoadingCancelled = Id("Message.ProgramLoadingCancelled");
    internal static readonly NTTextId CancelLoad = Id("Main.CancelLoad");
    internal static readonly NTTextId Cancelling = Id("Main.Cancelling");
    internal static readonly NTTextId ControllerTitle = Id("Title.Controller");
    internal static readonly NTTextId ConfigurationTitle = Id("Title.Settings");
    internal static readonly NTTextId GCodeTitle = Id("Title.GCode");
    internal static readonly NTTextId ProgramTitle = Id("Title.Program");
    internal static readonly NTTextId LicenseTitle = Id("Title.License");
    internal static readonly NTTextId PlcTitle = Id("Title.Plc");
    internal static readonly NTTextId ActivationImportFailed = Id("Message.ActivationImportFailed");
    internal static readonly NTTextId PlcConfigurationInvalid = Id("Message.PlcConfigurationInvalid");
    internal static readonly NTTextId PlcValidationFailed = Id("Message.PlcValidationFailed");
    internal static readonly NTTextId OpenGCodeTitle = Id("Dialog.OpenGCodeTitle");
    internal static readonly NTTextId GCodeFilter = Id("Dialog.GCodeFilter");
    internal static readonly NTTextId ImportLicenseTitle = Id("Dialog.ImportLicenseTitle");
    internal static readonly NTTextId LicenseFilter = Id("Dialog.LicenseFilter");
    internal static readonly NTTextId EditGCodeTitle = Id("Dialog.EditGCodeTitle");
    internal static readonly NTTextId SelectStartLineTitle = Id("Dialog.SelectStartLineTitle");
    internal static readonly NTTextId SelectStartLineFileTitle = Id("Dialog.SelectStartLineFileTitle");
    internal static readonly NTTextId LineCount = Id("Dialog.LineCount");
    internal static readonly NTTextId Select = Id("Dialog.Select");
    internal static readonly NTTextId Save = Id("Dialog.Save");
    internal static readonly NTTextId GCodeSaveFailed = Id("Message.GCodeSaveFailed");
    internal static readonly NTTextId ReadingProgram = Id("Progress.ReadingProgram");
    internal static readonly NTTextId InterpretingProgram = Id("Progress.InterpretingProgram");
    internal static readonly NTTextId PreparingToolPath = Id("Progress.PreparingToolPath");
    internal static readonly NTTextId PreparingPreview = Id("Progress.PreparingPreview");
    internal static readonly NTTextId ReloadRequired = Id("Progress.ReloadRequired");
    internal static readonly NTTextId PreparationFailed = Id("Progress.PreparationFailed");
    internal static readonly NTTextId OfflineReviewConfiguration = Id("Status.OfflineReviewSettings");
    internal static readonly NTTextId ConfigurationNeedsAttention = Id("Status.ConfigurationNeedsAttention");
    internal static readonly NTTextId ControllerState = Id("Status.ControllerState");
    internal static readonly NTTextId HardSimulationFailed = Id("Status.HardSimulationFailed");
    internal static readonly NTTextId ProgramRejected = Id("Status.ProgramRejected");

    internal static void Register()
    {
        if (registered) return;
        NTLocalization.RegisterCatalog(NTTextCatalog.LoadEmbeddedJson(
            typeof(RouterXYZACRTCPTexts).Assembly,
            "Radonix.RouterXYZACRTCP",
            "RouterXYZACRTCP.Localization.Catalogs.RouterXYZACRTCP.json"));
        registered = true;
    }

    private static NTTextId Id(string suffix) => new(Prefix + suffix);
}

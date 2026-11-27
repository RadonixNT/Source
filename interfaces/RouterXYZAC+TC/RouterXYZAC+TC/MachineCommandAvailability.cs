namespace RouterXYZACTC;

/// <summary>
/// One UI-thread snapshot of the independent states that control operator interaction.
/// Derived axis and ToolPath flags are intentionally not duplicated here.
/// </summary>
internal readonly record struct MachineCommandAvailability(
    bool IsConnected,
    bool MotionBusy,
    bool ProgramActive,
    bool ProgramLoading,
    bool HomeActive,
    bool HeadPositioningActive,
    bool ToolOperationActive)
{
    private bool ManagedOperationActive =>
        HomeActive || HeadPositioningActive || ToolOperationActive;

    internal bool CanEditReference =>
        !ProgramLoading &&
        (!IsConnected || !MotionBusy && !ProgramActive && !ManagedOperationActive);

    internal bool CanSetReference =>
        IsConnected && CanEditReference;

    internal bool CanEditJogParameters =>
        !ProgramLoading && !MotionBusy && !ProgramActive && !ManagedOperationActive;

    internal bool CanUseWindowCommands =>
        !ProgramLoading && !MotionBusy && !ProgramActive && !ManagedOperationActive;

    // Jog commands intentionally stay available while an ordinary Jog owns motion.
    internal bool CanJog =>
        IsConnected && !ProgramLoading && !ProgramActive && !ManagedOperationActive;

    // Manual spindle operation remains available during ordinary Jog.
    internal bool CanUseManualSpindle =>
        IsConnected && !ProgramLoading && !ProgramActive && !ManagedOperationActive;

    internal bool CanAdjustSpindleSpeed =>
        IsConnected && !ProgramLoading && !ManagedOperationActive;

    internal bool CanPrepareManualToolChange =>
        IsConnected && !ProgramLoading && !MotionBusy && !ProgramActive &&
        !ManagedOperationActive;
}

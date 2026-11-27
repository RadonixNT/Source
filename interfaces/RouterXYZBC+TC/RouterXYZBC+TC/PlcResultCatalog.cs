using NT.Controller.PLC;

namespace RouterXYZBCTC;

/// <summary>Interface-owned meanings. Result zero is always success, never application data.</summary>
internal static class PlcResultCatalog
{
    internal static string Code(string program, long result) => $"Router.PLC.{program}.{result}";

    internal static string Describe(string program, long result) => (program, result) switch
    {
        (_, 0) => "Operation completed successfully.",
        ("M100", 1) => "The source or main path is busy, stopped or invalid.",
        ("M100", 2) => "The axes are unavailable for transfer to the main path.",
        ("M100", 3) => "The head is not aligned with the main path continuation position.",
        ("M3" or "M4", 2) => "Spindle readiness or the requested direction is unavailable.",
        ("M5", 3) => "Spindle stop feedback did not arrive before the deadline.",
        ("LowerLoader" or "RaiseLoader" or "PickWithLoader" or "ReleaseFromLoader", 1) => "The main machining program is active.",
        ("LowerLoader" or "RaiseLoader", 2) => "The loader output has no enabled physical mapping.",
        ("PickWithLoader" or "ReleaseFromLoader", 2) => "The loader vacuum output has no enabled physical mapping.",
        ("LowerLoader", 3) => "The loader down sensor did not confirm before the deadline.",
        ("RaiseLoader", 3) => "The loader up sensor did not confirm before the deadline.",
        ("LowerLoader" or "RaiseLoader" or "PickWithLoader" or "ReleaseFromLoader", 4) => "The loading path was interrupted; vacuum was not released by cancellation.",
        ("LowerUnloader" or "RaiseUnloader", 1) => "The main machining program is active.",
        ("LowerUnloader" or "RaiseUnloader", 2) => "The unloader output has no enabled physical mapping.",
        ("LowerUnloader", 3) => "The unloader down sensor did not confirm before the deadline.",
        ("RaiseUnloader", 3) => "The unloader up sensor did not confirm before the deadline.",
        ("LowerUnloader" or "RaiseUnloader", 4) => "The unloading path was interrupted.",
        ("WaitRollers" or "StopSpindle" or "WaitSpindle" or "ReleaseTool" or "ClampTool", 1)
            => "The tool-change operation or its path is no longer active.",
        ("StopSpindle" or "WaitSpindle" or "ReleaseTool" or "ClampTool", 2)
            => "A required tool-change interlock is unavailable.",
        ("StopSpindle" or "WaitSpindle" or "ReleaseTool", 3) => "Spindle stop was not confirmed.",
        ("ReleaseTool", 4) => "Tool release feedback or settling timed out.",
        ("ClampTool", 5) => "The tool release state was not confirmed before clamping.",
        ("ClampTool", 6) => "Tool clamping feedback or settling timed out.",
        ("WaitRollers", 7) => "The rollers did not confirm the free position or lost confirmation.",
        ("ReturnToWork", 1) => "Axes or path are busy or not ready.",
        ("ReturnToWork", 2) => "Travel, step or velocity settings are invalid.",
        ("ReturnToWork", 3) => "The destination is outside the configured travel.",
        ("ReturnToWork", 4) => "RunAfterReturn must be zero or one.",
        ("ReturnToWork", 5) => "The work destination changed during positioning.",
        ("HomeX" or "HomeY" or "HomeZ" or "HomeB" or "HomeC" or "Home XYZBC", 1) => "Home search, sensor release or stopping timed out.",
        ("HomeX" or "HomeY" or "HomeZ" or "HomeB" or "HomeC" or "Home XYZBC", 2) => "HomeDirection must be minus one or plus one.",
        ("HomeX" or "HomeY" or "HomeZ" or "HomeB" or "HomeC" or "Home XYZBC", 3) => "Gantry topology is invalid or coupling timed out.",
        ("HomeX" or "HomeY" or "HomeZ" or "HomeB" or "HomeC" or "Home XYZBC", 4) => "Check the absolute axis reference and drive faults. A positioned axis must remain stopped at its Home Position.",
        ("MeasureToolLength", 1) => "The machine or a path is unavailable for measurement.",
        ("MeasureToolLength", 2) => "Measurement settings or destination are invalid.",
        ("MeasureToolLength", 3) => "Spindle stop timed out before measurement.",
        ("MeasureToolLength", 4) => "The coarse probe search timed out.",
        ("MeasureToolLength", 5) => "The probe did not release before the deadline.",
        ("MeasureToolLength", 6) => "The precision search or final stop timed out.",
        ("MeasureToolLength", 7) => "Spindle health was lost during measurement.",
        ("MeasureToolLength", 8) => "Return to the measurement start position timed out.",
        _ => "Undefined PLC failure code. Review the deployed program and its result contract."
    };

    internal static string Format(string program, long activation, PLCProgramExitReason reason, long result,
        long runtimeError = 0) => reason == PLCProgramExitReason.Completed
        ? $"[{Code(program, result)}] {Describe(program, result)} Program={program}; Activation={activation}; Result={result}."
        : $"[Router.PLC.{program}.{reason}] Program={program}; Activation={activation}; Exit={reason}; Result={result}; NativeError={runtimeError}.";
}

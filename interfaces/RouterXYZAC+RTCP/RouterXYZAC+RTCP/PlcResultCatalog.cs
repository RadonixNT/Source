using NT.Controller.PLC;

namespace RouterXYZACRTCP;

/// <summary>Interface-owned meanings. Result zero is always success, never application data.</summary>
internal static class PlcResultCatalog
{
    internal static string Code(string program, long result) => $"Router.PLC.{program}.{result}";

    internal static string Describe(string program, long result) => (program, result) switch
    {
        (_, 0) => "Operation completed successfully.",
        (Initialize.RunTP0PlcProgramName, 1) => "The source or main path is busy, stopped or invalid.",
        (Initialize.RunTP0PlcProgramName, 2) => "The axes are unavailable for transfer to the main path.",
        (Initialize.RunTP0PlcProgramName, 3) => "The head is not aligned with the main path continuation position.",
        (Initialize.PositionReturnPlcProgramName or Initialize.PositionAxialReturnPlcProgramName or
            Initialize.PositionRtcpApproachPlcProgramName or Initialize.PositionRtcpDeparturePlcProgramName, 1)
            => "The main path or its homed axes are not available for positioning.",
        ("M3" or "M4", 2) => "Spindle readiness or the requested direction is unavailable.",
        ("M5", 3) => "Spindle stop feedback did not arrive before the deadline.",
        ("ReturnToWork", 1) => "Axes or path are busy or not ready.",
        ("ReturnToWork", 2) => "Travel, step or velocity settings are invalid.",
        ("ReturnToWork", 3) => "The destination is outside the configured travel.",
        ("ReturnToWork", 4) => "RunAfterReturn must be zero or one.",
        ("ReturnToWork", 5) => "The work destination changed during positioning.",
        ("HomeX" or "HomeY" or "HomeZ" or "HomeA" or "HomeC" or "Home XYZAC", 1) => "Home search, sensor release or stopping timed out.",
        ("HomeX" or "HomeY" or "HomeZ" or "HomeA" or "HomeC" or "Home XYZAC", 2) => "HomeDirection must be minus one or plus one.",
        ("HomeX" or "HomeY" or "HomeZ" or "HomeA" or "HomeC" or "Home XYZAC", 3) => "Gantry topology is invalid or coupling timed out.",
        ("HomeX" or "HomeY" or "HomeZ" or "HomeA" or "HomeC" or "Home XYZAC", 4) => "Check the absolute axis reference and drive faults. A positioned axis must remain stopped at its Home Position.",
        _ => "Undefined PLC failure code. Review the deployed program and its result contract."
    };

    internal static string Format(string program, long activation, PLCProgramExitReason reason, long result,
        long runtimeError = 0) => reason == PLCProgramExitReason.Completed
        ? $"[{Code(program, result)}] {Describe(program, result)} Program={program}; Activation={activation}; Result={result}."
        : $"[Router.PLC.{program}.{reason}] Program={program}; Activation={activation}; Exit={reason}; Result={result}; NativeError={runtimeError}.";
}

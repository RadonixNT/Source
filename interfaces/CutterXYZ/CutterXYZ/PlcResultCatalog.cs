using NT.Controller.PLC;

namespace CutterXYZ;

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
        ("ReturnToWork", 1) => "Axes or path are busy or not ready.",
        ("ReturnToWork", 2) => "Travel, step or velocity settings are invalid.",
        ("ReturnToWork", 3) => "The destination is outside the configured travel.",
        ("ReturnToWork", 4) => "RunAfterReturn must be zero or one.",
        ("ReturnToWork", 5) => "The work destination changed during positioning.",
        ("HomeX" or "HomeY" or "HomeZ" or Initialize.HomePlcProgramName, 1) => "Home search, sensor release or stopping timed out.",
        ("HomeX" or "HomeY" or "HomeZ" or Initialize.HomePlcProgramName, 2) => "HomeDirection must be minus one or plus one.",
        ("HomeX" or "HomeY" or "HomeZ" or Initialize.HomePlcProgramName, 3) => "Gantry topology is invalid or coupling timed out.",
        ("HomeX" or "HomeY" or "HomeZ" or Initialize.HomePlcProgramName, 4) => "Check the absolute axis reference and drive faults. A positioned axis must remain stopped at its Home Position.",
        _ => "Undefined PLC failure code. Review the deployed program and its result contract."
    };

    internal static string Format(string program, long activation, PLCProgramExitReason reason, long result,
        long runtimeError = 0) => reason == PLCProgramExitReason.Completed
        ? $"[{Code(program, result)}] {Describe(program, result)} Program={program}; Activation={activation}; Result={result}."
        : $"[Router.PLC.{program}.{reason}] Program={program}; Activation={activation}; Exit={reason}; Result={result}; NativeError={runtimeError}.";
}

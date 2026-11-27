using System.Security.Cryptography;
using System.Text;

namespace RouterXYZBCTC;

/// <summary>Configuration-time source generation. No managed work runs during homing.</summary>
internal static class HomeProgramGenerator
{
    internal sealed record Group(string Master, string? Companion, bool SeparateSensor, bool Absolute = false)
    {
        internal string[] Motors => Companion is null ? [Master] : [Master, Companion];
        internal string[] SearchAxes => Absolute ? [] : SeparateSensor ? Motors : [Master];
    }

    private const string Stamp = "// Router generated home SHA256: ";

    internal static string[][] ParseSequence(string text, IReadOnlyList<string> axes, IReadOnlyCollection<string>? optional = null)
    {
        string[][] stages = string.IsNullOrWhiteSpace(text) ? [] : text.ToUpperInvariant().Split('>')
            .Select(stage => stage.Split(',').Select(axis => axis.Trim()).ToArray()).ToArray();
        string[] ordered = stages.SelectMany(stage => stage).ToArray();
        if (ordered.Distinct().Count() != ordered.Length || axes.Any(axis => !ordered.Contains(axis) && optional?.Contains(axis) != true)
            || ordered.Any(axis => !axes.Contains(axis)))
            throw new ArgumentException("Include every sensor axis once. Absolute axes are optional. Use > between stages and , for parallel axes, for example Z>B,C>X,Y.");
        return stages;
    }

    internal static bool IsGenerated(string source)
    {
        int newline = source.IndexOf('\n');
        return source.StartsWith(Stamp, StringComparison.Ordinal) && newline >= 0
            && source[..newline].TrimEnd('\r') == Stamp + Hash(source[(newline + 1)..]);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(value.Replace("\r\n", "\n").Replace('\r', '\n'))));

    internal static string Generate(string[][] stages, IReadOnlyList<Group> groups, string[] axisNames, bool fullHome = false)
    {
        var text = new StringBuilder();
        void L(string line = "") => text.Append(line.StartsWith("INTEGER ") || line.StartsWith("REAL ") || line.StartsWith("LOGIC ") ? line.Replace("; ", ";\n") : line).Append('\n');
        string Join(IEnumerable<string> axes) => string.Join(", ", axes);
        string Stopped(IEnumerable<string> axes) => string.Join(" AND ", axes.Select(axis => axis + ".Stopped"));
        Group[] selected = stages.SelectMany(stage => stage).Select(name => groups.Single(group => group.Master == name)).ToArray();
        string[] motors = selected.SelectMany(group => group.Motors).ToArray();
        string[] search = selected.SelectMany(group => group.SearchAxes).ToArray();
        Group[] absolute = selected.Where(g => g.Absolute).ToArray();
        // Full Home also requires valid references on absolute motors omitted from the motion sequence.
        string[] references = (fullHome ? groups.AsEnumerable() : selected).Where(g => g.Absolute)
            .SelectMany(g => g.Motors).ToArray();
        string Healthy(string axis) => $"{axis}.Homed AND NOT {axis}.Fault AND NOT {axis}.CouplingFault";
        string diagnostic = fullHome ? "HomeFailureAll" : "HomeFailure" + selected[0].Master;
        string CommandAtTarget(string axis) => $"{axis}.CommandPosition >= {axis}Target - {axis}Tolerance AND {axis}.CommandPosition <= {axis}Target + {axis}Tolerance";

        void ConfirmStoppedPositions(IEnumerable<string> axes, string advance, bool checkTarget)
        {
            string[] names = axes.ToArray();
            foreach (string axis in names)
            {
                string command = checkTarget ? CommandAtTarget(axis) + " AND " : "";
                L($"IF NOT {axis}Confirmed AND {axis}.Stopped AND {command}AXIS_IN_POSITION({axis}) THEN {axis}Confirmed := TRUE; END_IF;");
            }
            L($"IF {string.Join(" AND ", names.Select(axis => axis + "Confirmed"))} THEN {advance}");
            L("ELSIF PositionTimeout.Q THEN Result := 4;");
            foreach (string axis in names)
                L($"IF {diagnostic} = 0 AND NOT {axis}Confirmed THEN {diagnostic} := {AxisCode(axis) + 5}; END_IF;");
            L("END_IF;");
        }
        L("// Generated from Axes/HomeSequence, gantry selection and home-sensor mappings.");
        L("// Editing this source makes it engineer-owned; automatic regeneration then stops.");
        L("// Result: 0 success; 1 sensor/stop timeout; 2 invalid direction; 3 coupling failure; 4 invalid absolute reference or clearance; 5 invalid absolute destination or speed.");
        int AxisCode(string axis) => 10 * (1 + Array.IndexOf(axisNames, axis));
        void RecordReferenceFailures()
        {
            foreach (string axis in references)
            {
                int code = AxisCode(axis);
                L($"IF {diagnostic} = 0 THEN");
                L($"IF {axis}.Fault THEN {diagnostic} := {code + 1};");
                L($"ELSIF {axis}.CouplingFault THEN {diagnostic} := {code + 2};");
                L($"ELSIF NOT {axis}.Homed THEN {diagnostic} := {code + 3}; END_IF; END_IF;");
            }
        }
        if (stages.Length == 0)
        {
            L($"{diagnostic} := 0;");
            RecordReferenceFailures();
            L($"IF {diagnostic} = 0 THEN END_FUNCTION(Result := 0); ELSE END_FUNCTION(Result := 4); END_IF;");
            string idle = text.ToString();
            return Stamp + Hash(idle) + "\n" + idle;
        }
        L("INTEGER Stage := 0; INTEGER Phase := 0; INTEGER Result := 0;");
        L("INTEGER TimeoutMs := 120000; TIMER SearchTimeout; TIMER ReleaseTimeout; TIMER CouplingTimeout;");
        L("REAL DisplaceVelocity := 0.0;");
        foreach (string axis in search)
        {
            L($"REAL {axis}Position := 0.0; REAL {axis}Displace := 0.0;");
            L($"REAL {axis}Velocity := 0.0; REAL {axis}Return := 0.0; INTEGER {axis}Direction := -1;");
            L($"LOGIC {axis}Seen := FALSE; LOGIC {axis}Released := FALSE;");
        }
        foreach (string axis in absolute.SelectMany(g => g.Motors))
            L($"REAL {axis}Target := 0.0; REAL {axis}Tolerance := 0.0;");
        foreach (Group g in absolute) L($"REAL {g.Master}Speed := 0.0;");
        foreach (string axis in motors) L($"LOGIC {axis}Confirmed := FALSE;");
        L("TIMER PositionTimeout;");
        L("IF SF.FirstScan THEN");
        L($"{diagnostic} := 0;");
        foreach (string axis in motors) L($"{axis}Confirmed := FALSE;");
        L("Stage := 0; Phase := 0; Result := 0; TimeoutMs := Controller.HomeTimeout * 1000;");
        foreach (string axis in search)
        {
            L($"{axis}Position := {axis}.HomePosition; {axis}Displace := {axis}.HomeDisplace;");
            L($"{axis}Velocity := {axis}.HomeVelocity; {axis}Return := {axis}.HomeReturnVelocity; {axis}Direction := {axis}.HomeDirection;");
            L($"{axis}Seen := FALSE; {axis}Released := FALSE;");
            L($"IF {axis}Direction <> -1 AND {axis}Direction <> 1 THEN Result := 2; END_IF;");
        }
        foreach (Group g in absolute)
        {
            foreach (string axis in g.Motors)
            {
                L($"{axis}Target := {axis}.HomePosition; {axis}Tolerance := {axis}.Step;");
                L($"IF {axis}Tolerance < 0.0 THEN {axis}Tolerance := -{axis}Tolerance; END_IF;");
                L($"IF NOT ({axis}Tolerance > 0.0 AND {axis}Target >= {axis}.MinCourse AND {axis}Target <= {axis}.MaxCourse) THEN Result := 5; END_IF;");
            }
            L($"{g.Master}Speed := {g.Master}.HomeVelocity;");
            L($"IF NOT ({g.Master}Speed > 0.0) THEN Result := 5; END_IF;");
        }
        L("END_IF;");
        L("SearchTimeout(TYPE := TON, IN := (Phase = 1 OR Phase = 2) AND Result = 0, PT := TimeoutMs);");
        L("ReleaseTimeout(TYPE := TON, IN := (Phase = 3 OR Phase = 4) AND Result = 0, PT := TimeoutMs);");
        L("CouplingTimeout(TYPE := TON, IN := Phase = 0 AND Result = 0, PT := 60000);");
        L("IF SearchTimeout.Q OR ReleaseTimeout.Q THEN Result := 1; END_IF;");
        L("IF CouplingTimeout.Q THEN Result := 3; END_IF;");
        if (motors.Length > 0)
            L($"IF {string.Join(" OR ", motors.Select(axis => axis + ".Fault OR " + axis + ".CouplingFault"))} THEN Result := 3; END_IF;");
        if (references.Length != 0)
            L($"IF NOT ({string.Join(" AND ", references.Select(Healthy))}) THEN Result := 4; END_IF;");
        L("PositionTimeout(TYPE := TON, IN := Phase = 7 OR (Phase >= 8 AND Phase MOD 2 = 0), PT := TimeoutMs);");
        L("IF Result <> 0 THEN");
        if (references.Length != 0)
        {
            L($"IF Result = 4 AND {diagnostic} = 0 THEN");
            RecordReferenceFailures();
            L("END_IF;");
        }
        L($"AXES_STOP({Join(motors)});");
        L($"IF {Stopped(motors)} THEN");
        foreach (Group group in selected.Where(group => group.SeparateSensor))
            L($"IF {group.Master}.CouplingRole = 0 AND {group.Companion}.CouplingRole = 0 AND {group.Master}.ReadyToMove AND {group.Companion}.ReadyToMove AND NOT {group.Master}.CouplingFault AND NOT {group.Companion}.CouplingFault THEN AXES_COUPLE({Join(group.Motors)}); END_IF;");
        L("END_FUNCTION(Result := Result);");
        L("END_IF;");
        for (int index = 0; index < stages.Length; index++)
        {
            Group[] stage = stages[index].Select(name => selected.Single(group => group.Master == name)).ToArray();
            string[] stageMotors = stage.SelectMany(group => group.Motors).ToArray();
            string[] stageAxes = stage.SelectMany(group => group.SearchAxes).ToArray();
            L($"ELSIF Stage = {index} THEN");
            L("IF Phase = 0 THEN");
            foreach (Group group in stage.Where(group => group.Companion is not null))
            {
                L($"IF {group.Master}.CouplingRole = 1 AND {group.Companion}.CouplingRole = 2 AND {group.Master}.CouplingMasterIndex = {group.Companion}.CouplingMasterIndex THEN");
                if (group.SeparateSensor) L($"AXES_DECOUPLE({Join(group.Motors)});");
                else L("// This companion stays coupled throughout homing.");
                L($"ELSIF {group.Master}.CouplingRole = 0 AND {group.Companion}.CouplingRole = 0 THEN");
                if (!group.SeparateSensor) L($"AXES_COUPLE({Join(group.Motors)});");
                else L("// Already independent.");
                L("ELSE Result := 3; END_IF;");
            }
            Group[] positioning = stage.Where(g => g.Absolute).ToArray();
            string advance = index == stages.Length - 1 ? "END_FUNCTION(Result := 0);" : $"Stage := {index + 1}; Phase := 0;";
            L(positioning.Length == 0 ? "IF Result = 0 THEN Phase := 1; END_IF;" : "IF Result = 0 THEN Phase := 6; END_IF;");
            if (positioning.Length != 0)
            {
                L("ELSIF Phase = 6 THEN");
                foreach (Group g in positioning.Where(g => g.Companion is not null))
                {
                    string slave = g.Companion!;
                    string target = $"{g.Master}Target + {slave}.CouplingOffset";
                    L($"IF NOT ({target} >= {slave}.MinCourse AND {target} <= {slave}.MaxCourse AND {target} >= {slave}Target - {slave}Tolerance AND {target} <= {slave}Target + {slave}Tolerance) THEN Result := 5; END_IF;");
                }
                L("IF Result = 0 THEN");
                foreach (Group g in positioning)
                    L($"IF NOT ({string.Join(" AND ", g.Motors.Select(CommandAtTarget))}) THEN AXIS_JOG({g.Master} := {g.Master}Target, Velocity := {g.Master}Speed); END_IF;");
                L("Phase := 7; END_IF;");
                L("ELSIF Phase = 7 THEN");
                ConfirmStoppedPositions(positioning.SelectMany(g => g.Motors), stageAxes.Length == 0 ? advance : "Phase := 1;", true);
            }
            if (stageAxes.Length == 0)
            {
                L("END_IF;");
                continue;
            }
            L("ELSIF Phase = 1 THEN");
            foreach (string axis in stageAxes)
                L($"IF {axis}Seen OR HomeSensor{axis} THEN {axis}Seen := TRUE; AXES_STOP({axis}); ELSE AXIS_HOME({axis} := {axis}Direction, Velocity := {axis}Velocity); END_IF;");
            L($"IF {string.Join(" AND ", stageAxes.Select(axis => axis + "Seen"))} THEN Phase := 2; END_IF;");
            L("ELSIF Phase = 2 THEN");
            L($"AXES_STOP({Join(stageMotors)}); IF {Stopped(stageMotors)} THEN Phase := 3; END_IF;");
            L("ELSIF Phase = 3 THEN");
            foreach (string axis in stageAxes)
                L($"IF {axis}Released OR NOT HomeSensor{axis} THEN {axis}Released := TRUE; AXES_STOP({axis}); ELSE AXIS_HOME({axis} := -{axis}Direction, Velocity := {axis}Return); END_IF;");
            L($"IF {string.Join(" AND ", stageAxes.Select(axis => axis + "Released"))} THEN Phase := 4; END_IF;");
            L("ELSIF Phase = 4 THEN");
            L($"AXES_STOP({Join(stageMotors)}); IF {Stopped(stageMotors)} THEN");
            foreach (string axis in stageAxes) L($"AXIS_SET_HOME(Axis := {axis}, Position := {axis}Position - {axis}Displace);");
            L("Phase := 5; END_IF;");
            // One internal path belongs to this activation: displacements are serialized.
            Group[] sensors = stage.Where(g => !g.Absolute).ToArray();
            for (int i = 0; i < sensors.Length; i++)
            {
                Group group = sensors[i];
                int confirmPhase = 8 + 2 * i;
                L($"ELSIF Phase = {(i == 0 ? 5 : confirmPhase - 1)} THEN");
                L($"IF {string.Join(" OR ", group.SearchAxes.Select(axis => axis + "Displace <> 0.0"))} THEN");
                L($"DisplaceVelocity := {group.Master}Velocity;");
                if (group.SeparateSensor) L($"IF {group.Companion}Velocity < DisplaceVelocity THEN DisplaceVelocity := {group.Companion}Velocity; END_IF;");
                L($"TOOLPATH_SYNC_RUN(Homing := TRUE, ConfirmInPlc := TRUE, {Join(group.SearchAxes.Select(axis => axis + " := " + axis + "Position"))}, Velocity := DisplaceVelocity);");
                L("END_IF;");
                L($"Phase := {confirmPhase};");
                L($"ELSIF Phase = {confirmPhase} THEN");
                string next = i == sensors.Length - 1 ? advance : $"Phase := {confirmPhase + 1};";
                if (group.SeparateSensor) next = $"AXES_COUPLE({Join(group.Motors)}); " + next;
                ConfirmStoppedPositions(group.Motors, next, false);
            }
            L("END_IF;");
        }
        L("END_IF;");
        string body = text.ToString();
        return Stamp + Hash(body) + "\n" + body;
    }
}

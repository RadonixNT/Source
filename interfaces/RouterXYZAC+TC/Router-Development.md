# RouterXYZAC+TC

Five-axis direct-angle router with host-owned tool exchange. X/Y/Z use millimeters; A/C use unwrapped degrees. The controller receives the commanded joint angles. There is no RTCP, shortest-turn selection, rotating tool-length compensation or collision solver.

## Operator interface

The designer owns the complete main-window layout. Machine/work position and readiness cover XYZAC. The first Jog page contains direction buttons; its arrow opens velocity, step and absolute-position editors for all five axes. The other arrow returns to the direction page. Every launch starts on the direction page with Step Jog off (continuous Jog). Neither page nor Jog mode is persisted. Velocity, step, destination, work references and machine configuration remain LinkManager settings.

A/C velocity and step values use degrees/s and degrees. XYZ uses mm/s and mm. Goto uses machine coordinates and requires the complete machine to be homed. Continuous Jog stops on release; step and absolute moves continue to their accepted destination. Keyboard actions use the same command owners and admission checks as buttons.

## Machine and homing

TP0 runs the job; TP1 owns positioning/transfer; TP2 owns tool exchange. All three use XYZAC. Logical coordinates are mapped independently of motor slots. XG/YG/ZG are optional companions; A/C have no invented gantry axes. Default angular travel is A=-180..180 and C=-360..360 degrees.

HomeA/HomeC use HomeSensorA/HomeSensorC (factory DM0.IN4/DM0.IN5). Generated full Home defaults to `Z>A,C>X,Y`: Z first, then A/C, then X/Y. Each axis retains its configured search direction, velocities, home coordinate and displacement. Engineer-edited PLC sources are preserved.

Hard Simulation captures a virtual sensor near the current actual position at each Home-program activation: 5 mm for linear motors and 2 degrees for A/C. The threshold stays fixed through search and release; a new activation captures a new threshold. Cancellation, completion and reconnect reset this training model. Physical settings and PLC homing stages are not accelerated or bypassed. Configured low speeds, large home displacement or custom PLC waits can still lengthen a run.

## Interpreter, positioning and display

The host uses `NT_Interpreter.Interpreters.RouterXYZACTC.RouterCompiler`, its `RouterProfile`, and the tool-aware `Controller.RouterExecutionSession`. T/M6 handling remains host-owned. XYZAC is retained through work-origin edits, loading, selected starts, restart and tool-change continuation. B and RTCP commands are rejected by this dialect. Inch conversion affects linear values only; C370 remains 370 degrees.

G94 requests linear feed for mixed XYZ/rotary moves and resultant degrees/minute for pure rotary moves. Host positioning lifts Z, traverses/reorients at safe Z, then descends. Nonzero rotary waypoints are retained. Limits and real-machine clearance remain machine-specific.

The preview draws the XYZ path projection. The live position marker uses `S3DHeadModel(XYZAC, CParent)` and `SetPositionPose`: C carries A, with no position compensation. Pure rotary movement changes marker orientation without rebuilding path geometry. The top V readout remains linear XYZ speed in mm/s.

## Tool stations and references

Each tool stores XYZ Position, A/C station angles, XYZ placement/pickup vectors, XYZ Offset and measured Length. Exchange captures the request before moving, preserves station angles during insertion/withdrawal, and returns all five coordinates. Probing is Z-only and requires A=C=0. Angular compensation remains zero.

G54-G59 contain five-coordinate references. Main-window reference shortcuts remain XYZ; A/C reference values are edited in Configuration. G28/G30 and park destinations retain angular components. LinkManager owns commit, Escape restoration and persistence; there is no separate settings store.

## Build, tests and delivery

Open `RouterXYZAC+TC.sln`. Binary Radonix packages are the default. `NuGet.Config` isolates this interface's cache from old testing packages; the package version remains 1.0.0 during the testing freeze. Shared-library updates require the complete matched package refresh described in `NT-Packaging/PACKAGING-RULES.txt`. Source references are an explicit opt-in.

```powershell
dotnet build RouterXYZAC+TC/RouterXYZAC+TC.csproj -c Release -p:SkipNtRuntimeCopy=true
dotnet test RouterXYZAC+TC.Simulation.Tests/RouterXYZAC+TC.Simulation.Tests.csproj -c Release -p:SkipNtRuntimeCopy=true
./Setup/BuildSetup.ps1
```

Close the machine interface before offline tests that manipulate controller memory; a concurrent instance can interfere. Tests do not connect hardware. BuildSetup stages a Release x64 payload, the current XYZAC operator manual, inherited reference manuals and prerequisites, then generates a SHA-256 manifest and installer. See [Setup](Setup/README.md), [simulation](RouterXYZAC+TC/Simulation/README.md) and [operator manual](Reference/index.html).

## Source ownership

`MainForm.Bindings` connects designer controls; `MainForm.Runtime` refreshes state and composes simulation; `MainForm.Machining` owns program presentation and the head marker. `Configuration/Controller` owns axis topology, homing and command admission. `Machining/GCodeInterpreter` integrates the dedicated interpreter. `Machining/HeadPositioningCommands` and `Machining/ToolChanger` own host routes. Factory programs live in `PLCPrograms`.

Offline verification covers command admission, rotary motion, persistence, references, tool preparation, interpreter contracts, marker orientation and simulated input lifecycles. It does not certify hardware timing, mechanical clearance or an installed machine.

## Position-pointer orientation policy

The interface selects `S3DPointerOrientationMode.COnly` explicitly. The marker
uses calibrated C about world Z and supplied XYZ; A/B tilt does not tilt the marker.
`S3DHeadModel.EvaluateOrientation` remains the independent full physical head
orientation for future machine-part presentation. No RTCP or motion contract changes.


## Customer development delivery

In the manufacturer repository, `Setup/BuildSetup.ps1` builds the production installer. In the customer source delivery, it builds an installer from the customer's edited interface. For customization, extract the matching `RouterXYZAC+TC.zip` from `NT-Interfaces For Publish` and open `RouterXYZAC+TC.sln`. The delivery README covers offline restore, Release x64 build, tests, publish and compiling the included Setup source. The customer copy includes the application, its simulation, PLC sources, operator manual, public NuGet dependencies and matching Radonix runtime binaries. Maintainers can regenerate this model using the workflow in `NT-Interfaces For Publish/README.txt`.

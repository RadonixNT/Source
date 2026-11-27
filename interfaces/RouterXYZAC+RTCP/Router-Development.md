# RouterXYZAC+RTCP

Interface project for the planned RTCP variant. The current program interpreter runs in direct-angle mode. X/Y/Z use millimeters and A/C use unwrapped degrees.

## Operator interface

The designer owns the complete main-window layout. Machine/work position and readiness cover XYZAC. The arrows cycle through the Jog direction, velocity/step, and position pages. Every launch starts on the direction page with Step Jog off (continuous Jog). The selected page and Jog mode are session-only. Velocity, step, destination, work references and machine configuration remain LinkManager settings.

A/C velocity and step values use degrees/s and degrees. XYZ uses mm/s and mm. Goto uses machine coordinates and requires the complete machine to be homed. Continuous Jog stops on release; step and absolute moves continue to their accepted destination. Keyboard actions use the same command owners and admission checks as buttons.

## RTCP configuration and manual jog

Enter measured geometry in Configuration > Controller > RTCP while disconnected. Set machine type and joint order, then enter the joint pivots, unit directions and reference angles, the spindle gauge point, table datum, tool direction and tool length. All geometry uses one common reference pose. Set Enabled after entering the measurements. Each connection starts in Normal mode. The RTCP Enabled toggle displays the mode confirmed by the Controller.

In confirmed RTCP mode, AP and CP jog the labeled joint around a fixed tool tip and ZT jogs along the tool direction. Configuration > General lists AP, CP and ZT alongside the machine axes, each with Jog Velocity, Jog Step and Jog Position. The fields use degrees for AP/CP and millimeters for ZT. The Jog Feed control scales the requested velocity. Release the button to stop. Compensated Step and Goto movement require Controller support. Their setting fields are available, and their motion actions are disabled. Switch back to Normal for sensor homing. In RTCP, Go Home, Go Ref and Go To Park withdraw along the current tool axis before traversing at the General Safe Z clearance. Work entry uses Approach Distance and Approach Velocity for the final axial leg.

## Machine and homing

TP0 runs the job and TP1 handles positioning. The existing XYZAC path configuration retains TP2 as a spare. Logical coordinates are mapped independently of motor slots. XG/YG/ZG are optional companions; A/C have no invented gantry axes. Default angular travel is A=-180..180 and C=-360..360 degrees.

HomeA/HomeC use HomeSensorA/HomeSensorC (factory DM0.IN4/DM0.IN5). Generated full Home defaults to `Z>A,C>X,Y`: Z first, then A/C, then X/Y. Each axis retains its configured search direction, velocities, home coordinate and displacement. Engineer-edited PLC sources are preserved.

Hard Simulation captures a virtual sensor near the current actual position at each Home-program activation: 5 mm for linear motors and 2 degrees for A/C. The threshold stays fixed through search and release; a new activation captures a new threshold. Cancellation, completion and reconnect reset this training model. Physical settings and PLC homing stages are not accelerated or bypassed. Configured low speeds, large home displacement or custom PLC waits can still lengthen a run.

## Interpreter, positioning and display

The host uses the XYZAC compiler, profile and execution session from NT-Interpreter. Valid T and M6 words are omitted with source warnings. XYZAC is retained through work-origin edits, loading, selected starts and restart. B and RTCP commands are rejected by the current dialect. Inch conversion affects linear values only. C370 remains 370 degrees.

Programs containing machine-coordinate moves (G53, G28 or G30) require Normal mode. In RTCP, the Set X/Y/Z Zero buttons use the current tool tip in the work frame. RTCP preparation checks retained waypoints in machine coordinates using the current calibration. If the preview is simplified, this check covers the retained subset. Core checks compensated motion as it runs.

G94 requests linear feed for mixed XYZ/rotary moves and resultant degrees/minute for pure rotary moves. Host positioning lifts Z, traverses/reorients at safe Z, then descends. Nonzero rotary waypoints are retained. Limits and real-machine clearance remain machine-specific.

The preview draws the XYZ path projection and an articulated round spindle. The live marker follows the displayed tool tip on physical axis feedback and on accepted geometry or tool-length changes at standstill. The top V readout remains linear XYZ speed in mm/s.

## Work references

G54-G59 contain five-coordinate references. Main-window reference shortcuts remain XYZ; A/C reference values are edited in Configuration. G28/G30 and park destinations retain angular components. LinkManager owns commit, Escape restoration and persistence; there is no separate settings store.

## Build, tests and delivery

Open `RouterXYZAC+RTCP.sln`. This development interface defaults to repository source references for the current RTCP Controller API. Use only version-matched shared binaries and NT-Core for a runtime deployment.

```powershell
dotnet build RouterXYZAC+RTCP/RouterXYZAC+RTCP.csproj -c Release -p:SkipNtRuntimeCopy=true
dotnet test RouterXYZAC+RTCP.Simulation.Tests/RouterXYZAC+RTCP.Simulation.Tests.csproj -c Release -p:SkipNtRuntimeCopy=true
./Setup/BuildSetup.ps1
```

Close the machine interface before offline tests that manipulate controller memory; a concurrent instance can interfere. Tests do not connect hardware. BuildSetup stages a Release x64 payload, the current XYZAC operator manual and prerequisites, then generates a SHA-256 manifest and installer. See [Setup](Setup/README.md), [simulation](RouterXYZAC+RTCP/Simulation/README.md) and [operator manual](Reference/index.html).

## Source ownership

`MainForm.Bindings` connects designer controls; `MainForm.Runtime` refreshes state and composes simulation; `MainForm.Machining` owns program presentation and the head marker. `Configuration/Controller` owns axis topology, homing and command admission. `Machining/GCodeInterpreter` integrates the dedicated interpreter. `Machining/HeadPositioningCommands` owns positioning routes. Factory programs live in `PLCPrograms`.

Offline verification covers command admission, rotary motion, persistence, references, interpreter contracts, marker orientation and simulated input lifecycles. It does not certify hardware timing, mechanical clearance or an installed machine.

## Position-pointer orientation policy

The interface selects `S3DPointerOrientationMode.COnly` explicitly. The marker
rotates about world Z using C. Its centre follows the full physical head chain,
including tilt, pivot offsets and tool length. The axis readouts retain the physical machine coordinates.


## Customer development delivery

In the manufacturer repository, `Setup/BuildSetup.ps1` builds the production installer. In the customer source delivery, it builds an installer from the customer's edited interface. For customization, extract the matching `RouterXYZAC+RTCP.zip` from `NT-Interfaces For Publish` and open `RouterXYZAC+RTCP.sln`. The delivery README covers offline restore, Release x64 build, tests, publish and compiling the included Setup source. The customer copy includes the application, its simulation, PLC sources, operator manual, public NuGet dependencies and matching Radonix runtime binaries. Maintainers can regenerate this model using the workflow in `NT-Interfaces For Publish/README.txt`.

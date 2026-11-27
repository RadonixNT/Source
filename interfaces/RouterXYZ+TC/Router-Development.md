# RouterXYZ+TC

## Custom PLC command programs

The main Configuration toolbar contains Add PLC program and Remove PLC program.
Add is enabled for Command Programs or one of its source items, including an empty
group. Remove is enabled only for an active custom source. Both require Engineering,
a fully offline controller, no pending preparation and empty TP0–TP3 (including
paths not exposed by the interface wrapper). Default programs cannot be removed.
Default source-editing policies are unchanged. Custom programs are noncyclic.

Names use 1–64 ASCII letters/digits/underscores, starting with a letter/underscore.
Auxiliary names are canonicalized (`m015` becomes `M15`); duplicates, default names
and Macro names (including M6 and M30) are rejected. New source is the valid no-op
`END_FUNCTION(Result := 0);`. Remaining active PLC documents are checked for name
references outside comments before deletion, including incomplete drafts.

LinkManager persists membership in the hidden `Controller/PLC/CustomPrograms/Names`
TextLink and source under the existing Command Programs group. Membership is the
authoritative list: stale source registry values never resurrect a deleted program.
Removed source Links remain hidden for the current application's lifetime so Cancel
can restore their exact identity/text. Re-adding is a new program with no-op source;
Cancel still restores the pre-dialog source. Source and membership use the same
dialog journal, including repeated add/delete and delete/re-add sequences.

Structural edits update only custom Controller programs, preserving factory object
identities/order. The G-code auxiliary catalog/session is refreshed while paths are
empty; load the G-code again afterward. No native, shared-memory or cyclic changes
are involved. The existing selective `.ntconfig` transfer edits already-existing
visible Links; it does not create custom program membership on another machine.

`CustomCommandProgramTests` exercises toolbar selection and empty-group behavior,
default/name protection, source dependencies, all-path/offline gates, M15 compilation
and preparation, persistence across fresh forms, deletion across restart, and Cancel
after structural/source edits. These are offline tests, not physical PLC qualification.

## Configurable module counts

`Controller/PLC/Modules` contains Engineering-only `DigitalCount` (0..8),
`AnalogCount` (0..2) and `EncoderCount` (0..2). The compiled defaults are
`Initialize.DefaultDigitalModuleCount = 2`, `DefaultAnalogModuleCount = 1` and
`DefaultEncoderModuleCount = 0`. LinkManager restores saved counts, including zero.
There is no ordered string-array topology. Logical order is always DM, AM, EM,
ascending within each family; physical CAN assignment compacts the physical modules
in this order under the existing Controller contract and process-image limits.

Disconnect completely before editing counts. Draft counts reveal/hide the stable
module groups; OK validates and saves, and the Configuration Connect action accepts
settings before connecting. Cancel restores journaled counts and module values.
Controller slots change only during offline application, not during a draft edit.
The next connection uses the accepted topology. No runtime loop work is added.

Module settings are keyed by logical identity (`Modules/DM0`, `Modules/AM0`, etc.),
never compact row number. Changing the digital count cannot move AM0's UID/Mode to
another module. Inactive groups remain hidden, retain their settings and participate
in the existing dialog journal and LinkManager persistence. Re-adding the same
logical module restores its settings; verify its UID when replacing hardware.
This is the current settings layout: former `ModuleNN` registry entries are not
migrated or read as aliases. Configure UIDs/Mode under the named groups when first
using this layout; obsolete entries are not deleted automatically.

Acceptance and connection reject missing-module references in Mapping (including
retained disabled targets) and direct qualified PLC source references. Comments do
not count. Correct those references explicitly before reducing counts; signals and
PLC documents are never rewritten automatically. Draft Mapping choices retain
previous options to preserve Cancel; accepted changes refresh choices from the
new topology. I/O Monitor metadata refreshes through the existing apply path.

Offline tests in `ModuleTopologyTests` cover persistence/zero counts, stable UID
identity, dependency rejection, session gates, native PropertyGrid/tree updates and
Cancel. Physical reconnect/CAN assignment still requires a machine bench check.

## Operator documentation and maintenance review (6 November 2026)

The reviewed operational guide is in
`NT-References/User Manual/RouterXYZ+TC/RouterXYZTC-User-Manual.html` and its PDF
edition. It covers access display, program commands, Clear Tool Path, spindle
speed/run separation, Load/Unload, vacuum retention, transfer mappings and recovery.
The adjacent `Codes/RouterXYZTC-GCode-Reference` remains the geometric programming
reference. Setup includes both guides in both formats.

Transfer and command captions now use the eight-language application catalog.
Language changes affect display names only: Link names, PLC identifiers, registry
keys, mappings and keyboard assignments remain stable. Captions are captured once
at runtime initialization and refreshed on language/text changes, not on UI timers.

I/O Monitor uses the same signal captions as Configuration, including explicit
labels such as Reset Switch and sensor suffixes. Settings/property views and PLC
program monitors separate identifier words when no explicit caption exists;
physical addresses and uppercase acronyms remain intact. Truncated tree labels,
I/O names and PLC program names have full hover tooltips. These are presentation
changes only; PLC identifiers, mapping targets and saved Link keys are unchanged.

The invisible gap at the right edge of the first I/O module panel is a shared
horizontal resize grip, including when only one module is present. Dragging it
changes all module widths together from 75% to 200% of the default width. Panel
height stays fixed and overflow uses horizontal scrolling. The selected width
survives metadata/presence refreshes. With Save Window Layout enabled, its
DPI-independent percentage is saved and restored through the existing
UI/Diagnostics/IoMonitorForm/ColumnWidthPercent Link (default 100, range 75–200).

Runtime disposal preserves injected registry roots owned by the caller. The
program presenter also tolerates controls already disposed by its host while
asynchronous shutdown releases retained program state.

## Binary libraries and the Visual Studio toolbox

RouterXYZ+TC defaults to binary NuGet references even inside the full repository.
The solution contains only the router and its offline tests; shared library source
is not required to edit or build the interface. `NuGet.Config` beside the solution
selects the existing `NT-Packaging/Packages` feed and nuget.org. All eight Radonix
libraries are pinned to the matching testing version 1.0.0.

Open `RouterXYZ+TC.sln`, restore packages and rebuild, then open `MainForm.cs`
with **View Designer**. In Visual Studio, enable **Tools > Options > Windows Forms
Designer > General > AutoToolboxPopulate**. Close and reopen the designer after
restoring/replacing packages. The public ToolsWF controls are intended for the
toolbox, including `sButton`, `sPanel`, `sParameterBox`, `sGCodeView`, `s3DView`
and `sTheme`. `sPage` is intentionally hidden and is added through `sPagePanel`.
Designer load failures must be resolved before judging toolbox population.

For a customer source delivery, use the export workflow in the repository. It collects the interface sources, matched Radonix binaries, public NuGet dependencies and installer sources into a self-contained ZIP.

Ordinary package-mode builds do not build NT-EniBuilder from source. Within the
repository they can still copy available previously built native runtime files.
Shared-source development is an explicit opt-in using
`-p:RadonixUseProjectReferences=true`; it requires the full source tree.
Refreshing the unpublished 1.0.0 package set also requires refreshing the consumer
cache as described in `NT-Packaging/PACKAGING-RULES.txt`.

### Main-axis settings and parking

The main-program completion presentation and termination report consume
`ControllerWrapper.ToolPathStopped`. A background callback retains the newest TP0
notification and requests the existing UI refresh; no additional timer is created.
The UI checks execution, preparation and coordinate identities and current idle/online
state before accepting it. Completed alone marks final progress; requested, fault and
unknown termination are reported separately. The former UI-side buffer/end predicate
is removed. Block progress still follows normal feedback. Motion admission, axis-stop
checks and PLC handoff monitoring remain live checks, not event-derived permissions.
`General.ParkEnabled` (caption `Park Enabled`) defaults to false. When enabled,
an accepted `Completed` notification requests the existing `GoToPark` command once.
Requested stops, faults, unknown outcomes and stale notifications do not trigger parking.
The existing motion admission and travel validation apply; a rejected request is reported
and is not retried later. No spindle state is checked and no spindle command is issued:
spindle sequencing belongs to the main G-code program.

`General` exposes axis groups only for `Initialize.MainAxes` (X, Y and Z).
The coupled YG motor retains its technical Controller/Axis settings but has no
independent General group or operator jog command. Jog consumers retain actual
controller indices; Z must not be confused with YG when indexing the controller.

Each main-axis group contains `JogVelocity`, `JogStep`, `JogPosition` and
`ParkPosition`. All four are exposed read-only to PLC as
`General.<axis>.<parameter>` through `PLCVariableBridge`. LinkManager remains
the persistence and synchronization owner; no shared-memory layout change or
additional settings store is introduced. `ParkPosition` defaults to zero and
uses absolute machine coordinates, independently of the work reference and offsets.

The `GoToPark` CommandLink uses the existing TP1 positioning implementation and
is available in the keyboard command catalog without an assigned default shortcut.
It snapshots all park coordinates, validates travel and the existing homing,
axis readiness and idle-path requirements, then uses the traverse velocity.
When XY travel is needed it raises Z to Safe Z if needed, traverses XY and moves Z to
the parking height; otherwise only Z moves. Parking never appends M100 or starts TP0.

This is the editable starting application for the router family. It combines an
XYZ router, optional X/Y/Z gantry motors, spindle control, tool exchange, tool-length
measurement and machine-specific simulation. It is a concrete machine profile,
not a generic motion framework: derive a new interface by adapting this application's
profile, layout and machine operations while retaining the shared controller contracts.

## Developer reading order

1. Read the repository's `Read me.html`, especially **Shayan's rules** and the
   Controller/Wrapper, ToolPath, PLC and simulation contracts.
2. Read `Configuration/Initialize.cs` to identify the compiled machine assumptions.
3. Follow `MainForm.Runtime.cs::InitializeRuntime` to see construction order and ownership.
4. Read `MainForm.Connection.cs` and `MachineCommandAvailability.cs` before adding commands.
5. Follow `MainForm.Machining.cs` into `Machining/GCodeInterpreter.cs` and the
   `Machining/ToolChanger/` partials for execution and asynchronous completion.
6. Review the relevant PLC source and simulation model together, then run the offline tests.

Paths below are relative to the `RouterXYZ+TC/` application directory unless stated otherwise.

## Zones

`Zone / Zone Count` is editable while disconnected with empty ToolPaths, defaults to 8,
and accepts 0..64 (the controller's packed digital-output capacity). Physical assignments
stay under Controller/PLC/Mapping. Settings and mappings use stable Zone1..Zone64 Link
objects: reducing the count hides and disables inactive mappings without erasing saved
values; increasing it restores them. LinkManager owns persistence and Configuration Cancel,
including hidden edits. Existing assignments reduce the number of free physical outputs.

`Zone.Enabled` defaults to false. `ZoneManager` subscribes to `Controller.CycleUpdated`
only for an enabled, nonzero configuration in an online session. Disabled/zero-count
machines have no zone cycle callback or position scans. Disconnect and disposal detach
the subscription; reconnect captures a fresh immutable configuration and starts released.
There is no cyclic `ZoneControl` PLC program or per-zone application-variable catalog.
The existing PLC scan-boundary Set/Clear digital-output commands execute output changes.
No new timer, dedicated thread, native code or shared-memory contract is introduced.

Each zone has Enabled, Mode (Extraction or Workholding), Selected, MinX/MaxX, MinY/MaxY
and read-only State. Configuration is offline-only, including backend setter guards.
Enabled zones require a unique enabled DM output; extraction bounds must satisfy
MinX < MaxX and MinY < MaxY. PLC assignment to a managed zone alias or its raw output
is rejected during validation to avoid two owners. Do not rely on output retention
across controller disconnect, restart or power loss.

Extraction uses homed actual X/Y machine coordinates in millimeters and inclusive
bounds. It follows TP0 motion outside Stop, Hold, emergency, evaluation expiry and
M6/auxiliary program activity. Overlapping zones may all turn on. Only changed output
masks are queued; rejected writes retry against the latest state. The coalesced managed
notification is nominally 50 Hz, not a real-time guarantee: narrow zones and physical
valve response must be qualified against machine speed. This is not a safety interlock.

Workholding ignores position. Clamp Zones captures selected enabled workholding outputs;
Release Zones explicitly clears them. UI admission checks stopped machine operations,
and the worker rechecks idle axes/paths and tool exchange before queueing a request.
Requests that become busy are discarded; a full command queue retries while still idle.
Accepted workholding commands remain through pause, Stop, completion and M6. Idle
workholding-only configurations skip coordinate and extraction evaluation entirely.

Selected is persisted operator intent. Offline selection edits participate in Cancel;
online selection is allowed only while idle, released and without pending requests,
and is excluded from dialog rollback. Command requests and holding state are never saved.
Clamped means an ON command was accepted, not confirmed vacuum. There are no vacuum
sensors, qualification timers, vacuum-loss interlocks or zone prerequisites for TP0 Run
or ReturnToWork. Existing Background emergency/spindle interlocks are unchanged.

Offline verification: ZoneTests covers defaults, 0/8/64 counts, mapping visibility,
save/reload, hidden-setting retention, Cancel, competing PLC writes, online edit guards
and the real CycleUpdated subscription lifecycle. ZoneRuntimeTests covers inclusive
bounds, output-change batching, queue rejection/retry, busy-request rejection,
workholding retention and allocation-free steady ticks. OperatorCommandContractTests
checks that Background and OperatorInterlocks retain their execution order.

## Source map and responsibility boundaries

| Area | Responsibility and extension point |
| --- | --- |
| `Program.cs` | Reporting, localization and the shared single-instance theme/message-loop host. |
| `MainForm.cs` / `MainForm.Designer.cs` | Form construction, localized chrome and designer-owned layout. Construction does not connect hardware. |
| `MainForm.Runtime.cs` | Composition, disposal, the single configurable UI refresh timer and machine-simulation composition. |
| `MainForm.Connection.cs` | One serialized connect/disconnect/mode-change flow, including cancellation and terminal-state observation. |
| `MainForm.Bindings.cs` | Attach existing Links/commands to controls; own non-persisted feed/spindle/reference command bindings. |
| `MainForm.Machining.cs` | Open/edit/select/run intent, tool-start/spindle checks, work-coordinate and progress presentation. |
| `MainForm.Reporting.cs` | Event-based operation reports; distinguish requested, accepted and physically confirmed outcomes. |
| `MainForm.Diagnostics.cs` | Lazy diagnostic windows, metadata, shared themes and persisted window bounds. |
| `MachineCommandAvailability.cs` | One immutable UI availability snapshot; live command admission remains in each operation owner. |
| `Configuration/Initialize.cs` | Fixed axis/path/module/signal catalog, tool count, auxiliary priorities and resource/path identities. |
| `Configuration/GeneralLinks.cs` | Operator preferences, application commands, window bounds and shared settings-access composition. |
| `Configuration/KeyboardLinks.cs` | Explicit shortcut catalog, conflict validation, held-command release and keyboard enable/disable. |
| `Configuration/ReferenceLinks.cs` | Six persisted work origins and guarded commits to the configured TP0 axis order. |
| `Configuration/RouterLinks.cs` | Router/roller settings and publication of accepted cutting bounds to PLC. |
| `Configuration/Controller/ControllerLinks.cs` | Root controller settings composition, application of settings and aggregate status. |
| `Configuration/Controller/AxesLinks.cs` | Axis settings, coupling, Home and single/diagonal/absolute Jog commands. |
| `Configuration/Controller/ToolPathLinks.cs` | TP settings, Run/Pause/Stop/Reset policy and positioning ownership. |
| `Configuration/Controller/PLCLinks.cs` | PLC branches, settings baselines and bounded live-source refresh. |
| `Configuration/Controller/PLCPrograms.cs` | Embedded defaults, saved source drafts, validation, diagnostics and editor integration. |
| `Configuration/Controller/PLCMapping.cs` / `PLCModules.cs` | Semantic I/O and physical module topology; no duplicate runtime symbol resolver. |
| `Configuration/Controller/PLCVariableBridge.cs` | Typed Link sources and application-variable registration; live PLC values are excluded from settings rollback. |
| `Configuration/Controller/SpindleLinks.cs` / `InverterLinks.cs` | Resolved spindle capabilities and configured inverter Links. |
| `Configuration/Controller/LicenseFileEditor.cs` | Bounded activation-file import and property-grid presentation. |
| `Machining/GCodeInterpreter.cs` | Machine context, auxiliary metadata, background preparation, restart session and preview transforms. Standard parsing stays in NT-Interpreter. |
| `Machining/HeadPositioningCommands.cs` | Safe-Z TP1 positioning and observed M100 handoff to TP0. |
| `Machining/ReferenceEditorBinding.cs` | Selected-origin editor adapters; no persistent proxy coordinate values. |
| `Machining/MachineFeedMonitor.cs` | Independently testable displacement/time sampler; preserves the baseline on skipped UI ticks. |
| `Machining/UnloadMacro.cs` | Direct Unload macro, exclusive TP1 sequence, perpendicular-axis travel validation and cancellation. |
| `Machining/ToolChanger/ToolChangerLinks.cs` | Tool settings, shared operation state, command registration and tool availability. |
| `Machining/ToolChanger/ToolChangerRoutes.cs` | Capture an exchange request and generate its finite TP2 stage routes. |
| `Machining/ToolChanger/ToolChangerCommands.cs` | Admission, completion/offset acknowledgements, cancellation and reconnect compensation. |
| `Machining/ToolChanger/ToolChangerMacro.cs` | Transfer M6 from the macro worker to the UI-owned operation state machine. |
| `Machining/ToolChanger/ToolOffsetMeasurement.cs` | Probe settings, activation/result tracking and confirmed calibration commits. |
| `Machining/ToolChanger/Tool.cs` / `ToolParameterBinding.cs` | One persisted tool station and the operator's temporary selection/commit presentation. |
| `PLCPrograms/` | Embedded factory sources; saved operator sources remain authoritative. |
| `Simulation/` | Interface-owned mechanical models, host and controller adapter. See its README. |
| `Localization/` / `Properties/` / `Resources/` | Localized text identifiers/catalogs and designer resources. Generated designer files remain tool-owned. |
| Sibling `RouterXYZ+TC.Simulation.Tests/` | Offline regression tests, including UI, reference, connection and tool-start behavior. |
| Sibling `Setup/` | Production installer construction and payload verification; independent of managed-only development builds. |

`MainForm`, `ControllerLinks` and `ToolChangerLinks` are each one object split
into cohesive partial files. A partial file is not an additional service, lifetime
or synchronization boundary. Keep related private state with its operation; use a
separate class when it owns an independent lifetime, interface adapter or testable
algorithm. Do not create one-method services solely to move code between files.
Conversely, simulation modules and editor bindings retain their own cleanup and
contracts and should not be flattened into the form.

## Building a new router interface

1. Create a new application folder from the source, solution, resources and required
   setup files. Exclude `bin`, `obj`, `.vs`, `Setup/Output` and per-user project files.
   Keep the source-based tests with the new interface and update their project reference.
2. Choose the identity deliberately: update the solution/project, assembly/product,
   namespace, `Initialize.InterfaceName`, localization registration and installer
   identity together. `InternalsVisibleTo` must match the new test assembly.
   PLC resource logical names and `Initialize.GetPlcSource` must agree exactly.
3. Configure axes, ToolPaths, module order, inverter slots, semantic signals and tool
   count in `Initialize`. Review axis defaults in `AxesLinks`; these are sample
   machine values, not discovered travel limits or calibrated production settings.
4. Adapt Home PLC, tool-holder geometry, probing and safe-Z motion to the actual
   mechanics. This base assumes three XYZ ToolPaths, Z-positive withdrawal and a
   Y/YG gantry. Changing those assumptions also requires reviewing positioning,
   tool exchange, reference presentation, rollers and simulation. Renaming an axis
   in one table alone does not create a different kinematic machine.
5. Add machine settings through the existing Link groups and typed PLC bridge.
   Use stable semantic I/O names in PLC; map them to physical channels in settings.
   Register application variables before validating the complete PLC candidate.
6. Edit the layout in the designer and wire controls in `MainForm.Bindings.cs`.
   Add shortcuts to `KeyboardLinks` using the same command instances. Preserve
   live admission checks at dispatch; a disabled button is not a motion interlock.
7. Add operator-facing text through `RouterXYZTCTexts` and its catalog. Keep
   engineering descriptions and source comments explicit about units, ownership,
   timing and failure behavior. Do not localize persisted symbol identities.
8. Adapt simulation alongside the machine logic, run the offline suite, then perform
   the physical checks below before delivering a production configuration.

The current registry branch is selected by `Initialize.InterfaceName` beneath
`HKCU\Software\Radonix`. Report and ENI paths are derived from `NTLog.AppDataPath`,
which uses the application's assembly Company/Product metadata under common
application data. Give a new product its own identities unless sharing machine
settings and runtime files is explicitly intended. Do not add migration readers
or historical aliases: this repository targets one current contract.

## Ownership, threads and coordinate conventions

`MainForm` constructs the stable Wrapper, Link graph, editor bindings, interpreter
and operation owners once. Configuration changes update that model; reconnecting does
not build a second graph. LinkManager owns synchronization, edit commits and
persistence. A `Source` adapter reads/writes the authoritative controller handle;
it is not a second settings store. Configuration Cancel must not replay live output
commands. Most preferences save through the normal group save; confirmed physical
tool state and calibration use explicit flushes at their documented commit points.

The UI thread owns controls, settings edits and managed operation advancement.
File preparation captures a machine context and runs on a worker; `PendingLoad`
retains ownership until cancellation or publication completes. Connection events
marshal presentation through `PostRuntimeUi`. M6 callbacks capture a request under
the tool-changer gate and return; the UI timer advances it after Core releases TP0.
Simulation uses its own serialized worker timer and never accesses controls.
The native 1 ms loop owns actual interpolation, PLC scans and physical I/O.

| Quantity | Meaning |
| --- | --- |
| Controller axes | X, Y, Z by default; XG/YG/ZG companions are selected under Axes. |
| TP0 / TP1 / TP2 | Retained machining program / head positioning and unloading / tool exchange. |
| Compact path slots | The configured ToolPath axis order; never assume Controller axis indexes equal path slots. |
| Reference | Selected/prepared work origin; changing it does not itself move axes. |
| Offset | Acknowledged tool compensation; Z combines measured Length and independent Offset.Z. |
| LiveOffset | Fine adjustment added to final transformed output. |
| Work display | Command position minus Reference, Offset and LiveOffset. |
| Tool numbers | 1-based magazine numbers; zero is the empty spindle. |
| Source blocks | 1-based G-code line/block numbers; UI indexes may be zero-based. |
| Units | Motion values use mm and mm/s in this XYZ profile; S uses RPM; delays use the units declared by each Link/PLC source. |

The roller PLC currently requires X/Y in TP0 slots 0/1. It uses accepted cutting
bounds published once per preparation, not a geometry scan on each tick.

## Execution and extension invariants

Opening, editing or selecting a source line only prepares intent. Run goes through
the execution session and current machine admission. A required entry M6 prepares
the tool on TP2, confirms physical grip, acknowledges TP0 compensation and withdraws
before TP1 resolves the entry position. M100 then starts TP0. Numeric auxiliary
priorities cannot bypass the tool-ready prerequisite.

Tool exchange stops at physical commit boundaries. PLC activation IDs, path cursor
generations and offset generations distinguish this operation's completion from an
older one. Keep those checks when changing routes. Stop/cancel revokes continuation;
an elapsed timeout never counts as success. Manual exchange never resumes TP0.
Pause preserves the machining cursor after controlled stop; explicit Stop resets
it after motion has stopped. Home and GoHome are different operations.

The UI refresh drains each change latch once and bounds runtime-message processing
to 64 per tick. Reuse its availability and transform samples. Avoid per-tick sorting,
temporary collections, redundant whole-graph refreshes and repeated unchanged text
writes. Simulation inputs have one owner and publish initial state plus changes.
Optimizations must retain initialization, reconnect, cancellation and error paths.

## Development verification

### Direct managed macro commands

The macro facade accepts a direct argument and optional controller-owned path:
`controller.MotionController.Macros.RunProgram(name, programArg, toolPath)`.
The registration-index overload has the same inputs. Omitting the path creates a
path-independent invocation; the original single-input overloads still supply
zero and no path. A path from another controller is rejected before admission.
True means asynchronous admission, not operation completion. Hold, duplicate-run
rejection and cooperative cancellation use the existing macro runtime.

A `CommandLink.ActionExecute` handler can call this API after its live operation
admission checks and report false with `Macros.LastError`. The supplied path is
context only: the call does not pause it, release axes, create a native program
request or continue machining. The handler must keep UI work on the UI thread.
Share operation logic between manual commands and path callbacks, while keeping
their distinct start and continuation policies. In particular, the Router's
`ChangeToolLink` continues through `StartManualChange`; do not replace it with a
direct M6 call, whose handler expects the native TP0 handoff and continuation.

`MacroDirectInvocationTests` verifies argument/context delivery, unchanged motion
commands, foreign-path rejection, single-input behavior, duplicate admission,
cancellation and Hold without starting native hardware.

### PLC program catalog

The internal exchange stages are named `StopSpindle`, `WaitSpindle`,
`ReleaseTool` and `ClampTool`. They are named PLC programs, not G-code auxiliary
codes. Standard M6 still starts the exchange. Saved source is keyed by program
name: machine-specific edits saved under the former numbered stage names must
be reviewed and applied under these new names explicitly; no automatic migration
or alternate-name registration is provided.

All sources are embedded with explicit logical resource names. `PLCPrograms.cs`
selects and registers the factory catalog; `Shared.plc` supplies shared declarations.
An existing saved source is not replaced just because an embedded comment or
implementation changed. Review/apply updated source explicitly through Configuration.

| Source | Owner and completion contract |
| --- | --- |
| `Shared.plc` | Session declarations for scan-owned confirmation and interlock flags. |
| `Background.plc` | Cyclic spindle interlocks/quiet qualification and roller outputs. OperatorInterlocks owns Emergency/Pause Hold. Never infer physical stop from its lamp alone. |
| `Home XYZ.plc` | Z-first machine homing followed by X and independently sensed Y/YG; restores gantry coupling. |
| `HomeX.plc`, `HomeY.plc`, `HomeZ.plc` | Explicit axis/gantry Home operations, with search/release/displacement phases and bounded failures. |
| `ReturnToWork.plc` | Standalone PLC return program. The main-window positioning command uses the managed TP1 route builder. |
| Native `G4` / `G82` dwell | Internal `ToolPath.AddDwell` timer in Core; no PLC program, source editor or registration. P is seconds rounded up to 1 ms cycles. |
| `S.plc` | RPM-to-normalized-speed conversion; writes the retained setpoint without a path wait. |
| `M3.plc`, `M4.plc` | Forward/reverse requests qualified by readiness and minimum acceleration delay; reverse must be explicitly supported. |
| `M5.plc` | Clear spindle outputs and wait for delay plus qualified quiet state. |
| `M2.plc` | Clear spindle run outputs and finish immediately, without a timer or stopped-feedback wait; also used by managed M30. |
| `M100.plc` | Non-waiting TP1/acknowledged post-exchange handoff to TP0; alignment and path admission are rechecked. |
| `StopSpindle.plc`, `WaitSpindle.plc` | Start spindle stopping during approach, then wait for this exchange's stop confirmation. |
| `ReleaseTool.plc`, `ClampTool.plc` | Confirm release/clamp feedback before managed CurrentTool and compensation commits. |
| `MeasureToolLength.plc` | Stop spindle, position, coarse search, release, fine search and retract; publishes contact Z only on the successful sequence. |

M6 and M30 are managed macros registered by `ToolChangerLinks` and
`GCodeInterpreter`; they are not missing PLC files. M6 queues exchange, while
M30 waits for the M2 activation and source-path completion before Reset. Keep
M2/M5 as separately editable PLC documents: M2 requests spindle shutdown without
confirming standstill; M5 waits for qualified stop. Neither clears the speed setpoint.

### Current spindle and tool-change sensor behavior

`Router > Spindle` owns `MaximumSpindleSpeed` (default 24000 RPM, range 1..1000000)
and `SpindleFeedbackTimeout` (default 5000 ms, range 1..60000). Their unchanged
read-only PLC symbols use the existing variable bridge. The spindle RPM control
uses the same maximum-speed Link. `CurrentSpindleSpeed` saves the last RPM
setpoint selected manually or by an executed `S` command.

Spindle timing is configured under `Router > Spindle`: `SpindleOnTime` and
`SpindleOffTime` appear as **Spindle On Time** and **Spindle Off Time**. Both
default to 3000 ms, with an allowed range of 0..60000 ms. The stop sequence still
adds its separate 1000 ms quiet check. M3/M4 wait after issuing the start command;
the manual spindle toggle continues to write its output without an M-code wait.
ToolChanger uses `ReleaseTime` / `ClampTime` (displayed as **Release Time** /
**Clamp Time**), exposed to PLC as `ToolReleaseTime` / `ToolClampTime`.

The current settings and PLC symbol contract uses these names only; old Delay
keys are not migrated or used as aliases. Existing saved PLC documents must use
the new symbols before validation: update Background, M2/M3/M4/M5, StopSpindle/WaitSpindle,
ReleaseTool/ClampTool and MeasureToolLength, preserving any machine-specific customizations.
These parameters use the existing LinkManager persistence and PLC variable bridge.

The current factory sources distinguish required command outputs from optional
sensor feedback. `SpindleFeedbackConfigured` requires SpindleCW and SpindleSpeed mappings.
`SpindleToolChangeConfigured` additionally requires SpindleToolRelease. Neither
flag proves that the live inputs are healthy. Hard Simulation can satisfy the
mapping capability checks through independent simulated storage.

| Signal | Current behavior |
| --- | --- |
| SpindleToolClamped / SpindleToolReleased | Independently optional grip feedback. ReleaseTool waits for each assigned input (released ON, clamped OFF), then ReleaseTime. ClampTool requires the preceding release confirmation, then each assigned input (released OFF and, when loading, clamped ON) before ClampTime. Without assigned inputs, the full delay follows the stage command. The unload-only case does not require clamped ON. |
| SpindleRotationSensor | Optional. Stop qualification always waits SpindleOffTime followed by a 1000 ms quiet interval. An assigned sensor edge restarts the quiet interval; an unassigned sensor uses timing alone. A static signal is not proof of wiring health. |
| SpindlePistonRetracted | Optional. When assigned it must be ON for readiness, applicable grip checks and the ClampTool settling timer. When absent it does not block those checks. |
| PressureSensor / SpindleThermalOK / SpindleDriveReady | Optional level interlocks. Assigned inputs must be ON at the applicable admission and cyclic checks; absent inputs are omitted. These checks do not introduce individual replacement timers. |
| SpindleTaperBlow | Optional output, written only when assigned. Its absence no longer prevents tool exchange. |

PLC `ASSIGNED` and managed `SpindleInterlocks.OptionalInputSatisfied` use physical
mapping presence, not the live signal level. Disabled/unassigned mappings and
constant or virtual targets do not count as assigned optional hardware. An assigned
LOW input still fails its applicable check. Invalid configured hardware remains a
mapping-validation error; lack of live feedback does not dynamically remove a
mapping. Emergency and motion admission remain separate checks. Tool-height probing
still requires contact/release feedback and bounded search; it has no timer-only
measurement alternative.

### Sensor fallback delay and feedback timeout rule

Mandatory engineering rule confirmed by Shayan on 2026-10-09: whenever a
configurable delay substitutes for an absent sensor, sensor feedback replaces
that delay when the sensor is present.

- With the sensor absent (disabled or unassigned), use the parameter's current
  configured value as the fallback delay after issuing the operation command.
- With the sensor present, wait for its required feedback and continue immediately
  when it confirms. Do not impose the fallback delay before or after confirmation.
  Use `FeedbackTimeout = 3 * DelayParameterCurrentValue`, in the same units,
  solely to prevent indefinite waiting for the sensor. Use the current configured
  value, not the compiled default. For example, a configured delay of 500 ms gives
  a sensor timeout of 1500 ms, but confirmation after 100 ms allows continuation
  after 100 ms without an additional delay.
- Use this derived value wherever a timeout is needed for that sensor-backed
  operation; do not introduce an independently adjustable timeout for it.
- A present sensor that fails to confirm before the deadline causes a fault and
  stops the operation. Timeout expiry never substitutes for successful feedback
  and never changes a present sensor into an absent one.

This rule supersedes the earlier sensor-plus-delay policy below for operations
where a delay substitutes for an absent sensor. Adding this rule to the README
does not change existing PLC sources or runtime behavior; implementations that
still impose that delay with a present sensor require a separate code change.

### Earlier tool-change sensor and delay policy (implementation history)

Requirement confirmed by Shayan on 2026-10-06 and superseded where applicable by
the mandatory rule above on 2026-10-09. This section records the earlier behavior
implemented in the factory grip stages and managed admission,
commit and program-start checks. This does not establish which saved PLC sources
are installed on another machine. Timed release/clamp confirmations remain required
before CurrentTool commits; omitting a sensor never bypasses the stage timer.

- Determine sensor presence independently for each tool-change sensor from its
  configuration. A disabled or unassigned sensor is absent. An enabled, assigned
  sensor remains present even when its signal is OFF or it fails to respond.
- When a sensor is present, first confirm its required state, then run the
  corresponding configured delay before allowing the stage to continue.
- When a sensor is absent, run the corresponding configured delay after issuing
  the stage command, without waiting for that sensor, then continue the stage.
- For mixed installations, retain every present sensor's required confirmation;
  absent sensors must not block the stage. Start the stage delay once all of its
  present sensors confirm their required states, or after the command when none
  of those sensors is present.
- If a present sensor does not confirm within the allowed feedback timeout,
  report the fault and stop. Never reinterpret a missing response, OFF signal or
  disconnected wire as configuration-level absence or switch silently to timing.

When updating tool-change admission, PLC stage waits and cyclic monitoring, apply
the mandatory rule above rather than extending this historical delay policy.
Preserve emergency and motion admission and
the configured checks for present auxiliary inputs described above. Verify all-present,
all-absent, mixed, disabled, unassigned and present-but-unresponsive cases before
marking this requirement implemented. Saved PLC sources remain authoritative and
must be considered separately from updated factory defaults during deployment.

For an existing machine, update the saved Background, ReleaseTool and ClampTool sources along
with the application; replacing the executable alone leaves saved PLC logic intact.
Review local PLC customizations before applying the corresponding factory changes.
Normal mode requires the spindle run output for spindle operation and the release output for tool exchange. Speed control through an AM or inverter output is optional.

`RouterGripPlcTests` in NT-Controller.Tests compiles the factory ReleaseTool/ClampTool stages
for all eight S1/S2/S5 presence combinations. Set ROUTER_GRIP_FIXTURES to export
bytecode and GripSymbols.h. GripPlcRuntimeTests.cpp runs those fixtures against
PlcRuntime.cpp with an isolated clock (no controller connection), covering settling,
absent/stuck sensors and bouncing feedback. Managed Router tests cover admission,
disabled inputs and retained PLC confirmation at the CurrentTool commit boundary.

### Build and test

Run from this README's directory. The installed .NET SDK must support the project's
Windows target; managed-only builds deliberately skip native runtime deployment.

```powershell
dotnet build "RouterXYZ+TC.sln" -c Release -m:1 -p:SkipNtRuntimeCopy=true
dotnet test "RouterXYZ+TC.Simulation.Tests/RouterXYZ+TC.Simulation.Tests.csproj" -m:1 -p:SkipNtRuntimeCopy=true
```

The tests use temporary `HKCU\Software\Radonix\Tests` keys and need permission to
create/delete those isolated keys. UI tests run in STA contexts; they do not require
a physical controller. Registry-denied failures from a restricted runner do not
qualify or disqualify machine behavior. The solution includes both application and
test projects so tests are discoverable in Visual Studio.

Before machine delivery, verify Home/coupling, emergency Hold and release, manual
and G-code tool exchange, sensor timeout/failure, same-tool entry, selected-line
entry with a different tool length, Pause/Stop during every auxiliary operation,
tool-length measurement, disconnect/reconnect, and both access levels. Offline
tests cover software behavior; they do not qualify physical travel, spindle timing
or sensor wiring. Follow `Setup/README.md` for the installer build.

## Operator reporting and retained files

Operator reports use the existing timestamped NTLog file. File loading records
requests and successful/cancelled/rejected results. Run, Pause, Stop, Reset and
start-line selection include the file and TP0 source block. The UI observes
execution state, natural completion, spindle output/setpoint changes, axis home
state and Home PLC activation/results once per transition, without logging jog
ticks or every G-code block. Head positioning records accepted coordinates and
completion/cancellation, including the M100 handoff. Absolute position jog records
its origin, destination, speed and admission. Reference commits record the old
and new selection/coordinate only after acceptance. Output observations describe
commanded outputs, not physical spindle feedback; PLC results and home state
are reported separately from request admission.

The root `RecentFile` text link retains the full path only after a successful
G-code load, using the existing group persistence on application exit. Cancelled
or rejected loads preserve its previous value. When that file still exists, the
Open File dialog starts in its directory with its filename selected.

## Position Jog

The feed progress bar samples XYZ command displacement only when more than
0.2 seconds have elapsed since the preceding accepted sample; skipped refresh
ticks retain the original coordinate/time baseline. It divides the resultant
displacement by the actual monotonic interval and shows one decimal mm/s.
The graphic is 100 times measured speed divided by the cached square root of
the sum of squared TP0 member-axis VelocityLimits, clamped to 0..100 percent.
Applying an axis profile recomputes the limit. No further displacement returns
the display to zero; offline clears the baseline. YG is excluded from the XYZ
resultant and from the TP0 limit.
TP0 feed override ranges from 0 to 100 percent; zero decelerates the existing
running path to rest and increasing it resumes feed without resetting its cursor.
Spindle RPM remains editable during G-code execution, while loading, Home and
tool-operation gates remain active; the spindle On/Off control retains its gate.
The spindle lamp follows both CW and CCW output commands, including G-code changes,
and stays fully legible when manual interaction is disabled. It indicates a RUN
command, not confirmed shaft rotation. The RPM slider follows the live mapped
speed setpoint. Manual On selects CW; Off clears both direction commands.
The Pause buttons beside Home and Jog use the same all-motion controlled stop as
the program Pause button and preserve the TP0 cursor. Only the program Reset button
requests a fresh-start reset after stopping.

The axis GoTo buttons execute absolute machine-coordinate Jog commands using
the axis JogVelocity and JogFeedPercent override, independently of Step Jog mode.
Every configured axis must be homed; ordinary Jog readiness and machine-operation
gates also apply and are rechecked at dispatch. Releasing a GoTo button does not
cancel its finite move; Pause/Stop retain their existing controlled-stop behavior.
Destinations use the existing persisted General/<axis>/JogPosition Links bound
to R_PositionX/Y/Z. GoTo commands are available for optional Keyboard assignment.

## Unloader sequence

`Router/Unloader` contains `UnloaderUpTime` and `UnloaderDownTime`, displayed as
`Up Time` and `Down Time`. Both are persisted integer settling delays in milliseconds
(default 1000, range 0..60000), exposed read-only to PLC under their full names.
`UnloaderEndPosition` and `UnloaderStartPosition` appear as `End Position` and
`Start Position`. Both are persisted REAL positions in millimeters (default zero,
three decimal places), exposed read-only to PLC under their full names.
The semantic digital output `UnloaderDown` and digital inputs `UnloaderUpSensor` and
`UnloaderDownSensor` default to Unassigned. The output must have an enabled physical
mapping before Unload is admitted; either sensor is independently optional.

The direct managed macro `Unload` and its CommandLink are available in the Keyboard
catalog without a default shortcut. It is rejected while TP0 `IsProgramActive` is
true, including program waits and pending Run. Existing Operator access, Online,
Home, stopped/ready axes, travel/PDO limits and exclusive-operation admission also
apply. It cannot be called from a ToolPath program row.

Unload captures the positions, traverse velocity and independent `UnloaderVelocity` (default 100 mm/s). It switches spindle RUN/direction off without waiting for rotation to stop, then prepares this TP1 route:

1. Run waiting `RaiseUnloader` and confirm the raised state before any motion.
2. Raise Z to its configured `MaxCourse`, keeping X/Y fixed, and stop there.
3. Move to `UnloaderStartPosition`.
4. Run waiting `LowerUnloader`.
5. Move to `UnloaderEndPosition` at `UnloaderVelocity`.
6. Run waiting `RaiseUnloader` again.

Positions are absolute machine coordinates on the axis perpendicular to
`Router/OnBridgeAxis`: X selects motion on Y; Y selects motion on X. The approach and initial Z lift
use `General.TraverseVelocity`; the unloading stroke uses `UnloaderVelocity`. The remaining sequence
keeps Z at `MaxCourse`; the other horizontal axis retains its captured position.
No horizontal motion or Unloader On row precedes completion of the Z lift.
Spindle speed remains unchanged. TP2 remains dedicated to the tool changer.

`LowerUnloader` energizes `UnloaderDown` and uses `UnloaderDownSensor`/`UnloaderDownTime`.
`RaiseUnloader` de-energizes it and uses `UnloaderUpSensor`/`UnloaderUpTime`. Each PLC
stage captures its current delay parameter on activation. With an assigned sensor,
confirmation completes the stage immediately, without settling delay; lack of
confirmation faults at three times the captured value. With a disabled/unassigned
sensor, the stage waits the captured delay and succeeds. Zero therefore means
immediate timer completion or immediate sensor timeout unless feedback is already
confirmed. Result 0 means success, 1 TP0 active, 2 output unassigned, 3 sensor timeout;
Both stages return 4 if TP1 is inactive or interrupted.

Core faults the calling TP1 on a nonzero PLC result before the next movement. Stop/Pause, loss of connection
or replacement of the route cancels continuation and requests the output off;
Unloader On also exits and clears the output if TP1 is interrupted. No automatic
retry or motion resume is issued. Unload blocks competing machine operations and
its geometry, speed and timing edits until it finishes. Completion requires a TP1
`ToolPathStopped(Completed)` event matching execution, preparation and coordinate
identities, source block 6, successful On and the second successful Off activation.
The UI timer consumes this event and rechecks stopped axes; no polling-only success is inferred.

Saved PLC sources remain authoritative. New installations receive these embedded
factory programs; review/apply any later source changes through the PLC editor.
No dedicated unloader sensor simulation is supplied. Offline coverage is in
`UnloadTests`, `RouterUnloadPlcTests` and `UnloadPlcRuntimeTests.cpp`; physical
verification must cover both bridge orientations, both sensor-presence modes,
sensor timeout and Stop during each leg before machine delivery.

## Transfer naming compatibility

Transfer program names describe actions: LowerLoader/RaiseLoader,
LowerUnloader/RaiseUnloader, PickWithLoader and ReleaseFromLoader. Mechanical
outputs are LoaderDown and UnloaderDown (TRUE lowers); feedback is LoaderUpSensor,
LoaderDownSensor, UnloaderUpSensor and UnloaderDownSensor. Load/Unload remain the
complete cycle commands; ReleaseLoaderVacuum is the explicit manual release.

Before constructing Links, the interface migrates legacy mapping groups, the manual
vacuum shortcut and saved PLC program names. Existing canonical values take precedence;
legacy keys are retained. Saved PLC sources are rewritten by identifier token without
replacing custom logic, parameter names, comments or descriptive strings. Exact quoted
legacy program names are updated for calls. Delay/position/velocity persistence keys
are unchanged. UI titles use Transfer Velocity, Up Time, Down Time,
Vacuum Pick Delay and Vacuum Release Delay. The descriptions specify milliseconds.

## Loader sequence

`Load` uses the same `MaterialTransferMacro` TP1 engine and admission, axis, travel,
Stop/Pause and completion rules as `Unload`. Only one transfer can be active.
`Router/Loader` has independent LoaderUpTime, LoaderDownTime, LoaderStartPosition,
LoaderEndPosition and LoaderVelocity settings. Defaults match Unloader (1000 ms
up/down, zero start/end coordinates, 100 mm/s stroke). Both use Router/OnBridgeAxis.
LoaderPickDelay and LoaderReleaseDelay are independent persisted millisecond values
(default 1000, range 0..60000), captured when their waiting PLC stage begins.

Map LoaderDown and LoaderVacuum digital outputs before running Load. LoaderUpSensor and
LoaderDownSensor are independently optional and default to Unassigned. Assigned
sensors confirm immediately or fail at three times the corresponding up/down delay;
unassigned sensors use the delay. There is no continuous down-sensor check during
travel and no vacuum sensor. Spindle RUN/direction is switched off without waiting
for rotation; its analog speed remains unchanged.

The eight waiting/motion blocks are: RaiseLoader; Z lift to MaxCourse; approach to
LoaderStartPosition; LowerLoader; PickWithLoader; stroke to LoaderEndPosition;
ReleaseFromLoader; RaiseLoader. PickWithLoader turns LoaderVacuum on only after the down stage
succeeds, then waits Pick Delay before movement. At the destination ReleaseFromLoader
turns vacuum off and waits Release Delay before the final raise. The loader stays
lowered during the transfer stroke. Every PLC stage must succeed, including both
RaiseLoader activations, before the matching block-8 Completed event is accepted.

Stop, Pause, cancellation and path faults never write LoaderVacuum off. They stop
motion and request the mechanism up, preserving the vacuum's current state. If
normal release has already started, interruption does not turn vacuum back on.
The operator can use **Release Loader Vacuum** in Router/Loader or assign the
ReleaseLoaderVacuum command in Keyboard. It requires Operator access, Online and stopped
motion, but does not require Home or fault-free axes. Load also appears in Keyboard
without a default shortcut. Physical output retention through power/bus loss depends
on hardware; this software rule concerns cancellation and motion/path faults.

Tests: LoaderTests verifies defaults, independent persistence, command registration,
and optional sensor mappings. RouterLoadPlcTests in NT-Controller.Tests compiles all
16 output/sensor assignment combinations. Set ROUTER_LOAD_FIXTURES to an OS temporary
directory to export its bytecode, then compile LoaderPlcRuntimeTests.cpp with
NT-Core/NT-Core/PlcRuntime.cpp, C++20 and that fixture include directory. Pass the
fixture directory to the executable. It exercises normal pick/release timing,
independent sensors, missing outputs, timeout and interrupted-stage vacuum retention
with the production VM and an isolated clock. These are offline tests, not physical
machine commissioning. Saved PLC sources remain authoritative.

## Roller control

A TP0 position mismatch that successfully starts TP1 ReturnToWork positioning
is an accepted operator Run request. It does not display MotionCommandRejected;
the existing M100 program still owns continuation to TP0. Failed positioning
or a rejected direct Run continues to report an error.

`Router/OnBridgeAxis` selects X (default) or Y as the axis travelling on the bridge.
Rollers are parallel to that same axis: X uses Y offsets/bounds and Y uses X.
The existing PLC `RollerParallelX` flag is derived from this setting, without a
second selector. The former `Router/Roller/RollerAxis` setting is removed; select
the machine's actual orientation explicitly under the new setting.
`Router/Roller` owns independent Enabled and signed spindle-relative Offset
settings. Background controls Roller1/Roller2
only while the spindle command is on and TP0 is Running without a Stop request.
`ToolChangeActive` also inhibits both roller outputs for the entire exchange,
including stopped offset stages. Background remains their only writer.
An inhibited spindle or unavailable cutting bounds forces both outputs off.
Parallel X compares Y; parallel Y compares X, with inclusive cutting limits.
Actual spindle feedback minus TP0 Reference, applied Offset and LiveOffset,
plus the roller offset, is compared with accepted untransformed cutting bounds.
Bounds are cleared before each load/rebuild and published only after successful
preparation; traverse-only or rejected files leave them unavailable. Publication
does not add per-tick geometry scans. TP0 uses the fixed profile's XYZ slot order.

The bundled Background source includes this policy. Existing saved PLC sources
remain authoritative: load the updated `PLCPrograms/Background.plc` into the
Background settings editor and Apply before testing an existing installation.
Assign the two retained Unassigned output mappings to the intended channels.

`Roller1Sensor` and `Roller2Sensor` are Unassigned digital inputs whose HIGH
level means the roller is free for exchange. Each enabled roller requires an
enabled physical input mapping and HIGH feedback; disabled rollers are ignored.
`Router/Roller/ReleaseTimeout` defaults to 5000 ms (range 1..60000).
Every physical exchange, manual or M6/entry preparation, places waiting PLC
`WaitRollers` before its first motion. Initial LOW feedback is an expected wait,
not admission rejection. Background publishes `RollersReady`; WaitRollers skips
its activation scan, confirms outputs are off and readiness is current, then
sets `ToolChangeRollersConfirmed`. Managed admission clears that confirmation
before activating each exchange; Background clears it outside exchanges.
Timeout or feedback loss after confirmation sets fault/result 7, stops TP2 and
revokes continuation through the existing failure path. Release/clamp admission
also requires the current confirmation/readiness. Ordinary cancellation never
confirms success. Same-tool compensation without a physical exchange remains
the existing offset-only operation. No native cycle or shared-memory layout
change is introduced. Hard Simulation uses inverted output-to-input feedback
with a 300 ms actuator delay; real-machine timing must be verified on the bench.

Existing saved PLC sources are authoritative: explicitly review/apply updated
Shared and Background sources and the new WaitRollers program before deployment.
`RollerInterlockTests` exports production expressions for
`RollerPlcRuntimeTests.cpp`: the real VM checks 64 mapping/enable/input combinations,
output ownership, initial wait, delayed confirmation, timeout, feedback loss,
cancellation and stale confirmation. The complete Router suite checks profile
compilation and first-stage composition.

Operator `Pause` uses the existing all-motion controlled stop: it cancels
positioning, tool exchange and length measurement, stops Jog/Home and every
ToolPath, preserving the main program cursor and progress for the next Run.
Cancelled auxiliary operations must be requested again. Explicit Reset resets
the main program after motion has stopped. Pause remains available throughout an Online session,
including auxiliary motion and pending PLC stages; it does not disable servos
or stop cyclic health monitoring.

I/O mapping edits require Offline or Failed with no PLC Apply in progress.
Every ToolPath must be empty, including retained prepared paths after disconnect.
Use Clear Tool Path for TP0 and clear any retained auxiliary programs before
editing mappings. PLC source/program-list edits and Apply have independent gates.

`Measure Tool` runs the existing `MeasureToolLength` probe sequence. An empty
spindle measures the confirmed reference; a current tool requires that reference
and stores ContactZ minus ReferenceOffsetZ in its persisted `Length`, preserving
the independent XYZ `Offset` correction. Only successful PLC completion changes
the measured length, and
the existing stopped TP0 offset acknowledgement applies the compensation.
TP0 Z compensation is `Length + Offset.Z`. PLC exposes each tool's read-only
Length symbol alongside its Offset components. The tool selector
continues to request manual exchanges independently of the measurement button.
Active machining programs, including paused program blocks, inhibit measurement.

`Controller / Inverters / Inverter Count` selects 0..2 devices (default 0),
with stable identities `INV0` and `INV1`. Change the count while disconnected
with every ToolPath cleared. Counts are persisted by LinkManager; Cancel restores
the draft count, visibility and mapping edits. Removal is rejected while any
mapping (including a disabled mapping) or PLC source references the removed slot.
Module and inverter dependency scans share the configuration-time PLC text scanner; comments and string literals are excluded and whitespace before member access is accepted. Mapping choices follow the current draft while retaining a selected unresolved target for correction.

Fresh spindle mappings are `SpindleCW -> DM0.OUT0` and `SpindleSpeed -> AM0.OUT0`.
Persisted selections remain authoritative. For an inverter spindle, configure
the count and select its `INV0.RUN` / `INV0.SPEED` (or INV1) targets explicitly.
The existing manual controls and PLC M3/M5/S programs use these semantic mappings.
Core requires RUN before applying inverter speed. SPEED and RUN remain live
commands, are not replayed from saved settings and are not undone by Cancel.

Open `RouterXYZ+TC.sln` in Visual Studio with the .NET desktop development workload.
Build the **Debug | x64** configuration, then right-click `MainForm.cs` and choose
**View Designer** (Shift+F7).

`MainForm.Designer.cs` owns the existing control layout. Move, resize and edit
controls in the designer; keep the names of controls used by runtime bindings.
The other `MainForm.*.cs` files are nested code files, not separate forms.
Designer construction and the design-time Load event do not initialize the
controller, settings registry or runtime timer.

The machine profile, PLC programs, tool changer, simulation, settings and motion
bindings remain part of the application. Startup retains automatic connection;
do not launch it just to preview the layout. The persisted `Controller/Mode` TextLink
selects exactly `Simulation` (default, Hard Simulation) or `Normal` (physical RT).
There is no compiled runtime-mode selector in this interface.

`ChangeMode` toggles that same Link. Its default application-local keyboard
shortcut is `Ctrl+Shift+Alt+Space`, configurable through the existing Keyboard
Links. Both direct Mode edits and the command require Operator access, no motion,
no active G-code/PLC motion operation, no program preparation, Home, positioning
or tool operation, and no pending connection transition.
Mode edits are immediate live connection operations and are excluded from the
Configuration Cancel journal, like existing live PLC controls. Configuration OK waits for
the transition to finish; it cannot apply a competing settings candidate mid-connect.

`Keyboard/KeyboardEnabled` is a persisted LogicLink displayed as `Enabled`,
defaulting to True. False removes all application shortcut routing, including
Stop shortcuts, without disabling on-screen commands or ordinary text input.
Disabling immediately releases held keyboard commands. Re-enabling registers
the current shortcut assignments again; persistence uses the normal Link save.

`OpenReportFile` opens the current session report in Notepad. The progress bar's
double-click uses the same command, and its default shortcut is
`Ctrl+Shift+Alt+PrintScreen`. Report access stays available during motion and
program preparation, subject to the ordinary settings-access authorization.
KeyboardEnabled affects the shortcut, not the progress-bar gesture.

`MainForm.ChangeControllerConnectionAsync` owns all application connection
requests, including startup, Configuration commands, mode changes, failure cleanup and
shutdown. Mode changes await terminal Offline before preparing and connecting the
new runtime. A failed disconnect prevents reconnection. Failed startup is logged
without selecting another mode or resuming motion. `NT-Wrapper` retains native
runtime selection and post-Online activation of configured physical axes through
`ControllerWrapper.Connect(ControllerRuntimeMode)`.

The Online indicator follows the verified certificate identity of the physical MM.
Controller/PLC settings display its eight-digit UID, including before license import
and with no drives connected. In Simulation mode the same indicator reads Simulation and lights when the simulated session is operational. Mode requests and connection results are
written to NTLog. Mode and shortcut persistence use the ordinary Link group save.

The assembly and resource namespace are `RouterXYZ+TC` and `RouterXYZTC`.
Storage follows the current profile and assembly identities described above.

For a managed-only build without native runtime deployment:

```powershell
dotnet build "RouterXYZ+TC.sln" -c Debug -p:Platform=x64 -p:SkipNtRuntimeCopy=true
```

Ordinary builds retain `RuntimeDeployment.targets` for the controller runtime.

## Source and binary delivery boundary

The machine-specific `Simulation/` sources compile directly into this interface
and remain editable with it. They are not supplied by a shared simulation project
or DLL. The sibling `RouterXYZ+TC.Simulation.Tests` project tests those local
modules and is not a runtime dependency.

This project defaults to version-matched `Radonix.NT.*` binary packages, including
inside the full Radonix source tree. Shared-source development explicitly opts in
with `RadonixUseProjectReferences=true`. This keeps the interface and its
simulation source editable while shared library source, including `NT-ToolsWF`,
is delivered only as compiled assemblies. Restore and build before reopening the
WinForms designer so Visual Studio can populate the packaged controls in Toolbox.
The direct `Scintilla.NET.WinForms` package reference is intentional: that package
copies native Scintilla/Lexilla DLLs through non-transitive build assets. A solely
transitive managed dependency does not supply those files to a clean consumer.

The Select Start Line and Edit G-code windows are shared by `NT-Interpreter` as
`GCodeStartLineDialog` and `GCodeEditorDialog`. RouterXYZ+TC continues to own their
CommandLinks, machine-state admission, localized captions, source-file encoding,
save diagnostics, reload behavior, and bounds persisted under `Internal/UI`.
Program files honor a Unicode BOM, otherwise use strict UTF-8 first and the
interface's configured GB18030 fallback only when UTF-8 decoding fails. The
detected code page and BOM policy remain attached to the document so Edit G-code
saves with the same representation.

## Program loading and travel status

The compiler records explicit G54-G59 systems actually used by motion, motion
that inherits the initial UI-selected system, and G53 machine-coordinate use.
For a single effective work system without G53, reference edits retain TP0 rows,
preview geometry, source mapping, execution selection and camera framing; only
the TP0 reference and scene translation change. A single explicitly selected
system starts at its own origin and is independent of the UI selector. Files
without an explicit system follow the selector. Switching the selector can turn
an inherited-plus-explicit program into a multiple-system program.
Multiple-system programs rebuild retained source only when a used origin or
their inherited system changes. Unused reference edits do not rebuild. G53
programs also account for the initial-position dependency so machine endpoints
stay fixed. Reference rebuilds preserve the original source encoding metadata.

While loading or rebuilding, the Open file command becomes Cancel. The cursor
shows background work throughout the form, with a normal pointer on Cancel.
Link interaction, including keyboard dispatch, is denied except Stop and Cancel;
motion-command release remains available. Completion, cancellation and failure
restore the prior cursors and reevaluate access against current machine state.
Loading progress assigns 0..50% to file reading/interpretation and 50..100% to
ToolPath preparation. These weights describe stages, not elapsed-time estimates.
After path preparation the UI explicitly draws the full bar before starting
detached preview/LOD/picking work. Subsequent preparation changes text only and
keeps the graphical bar at 100%; there is no artificial completion delay.
Cancellation and command gates remain active until publication and fitting finish.
After successful publication and fitting, the bar resets to zero and returns to
execution progress; the initial elapsed/remaining/total caption is then refreshed.
The viewport status shows cutting-only XYZ extents to one decimal place and unique
required tool/work-reference counts. An implicitly used initial reference is counted
once; G53 is not a work reference. Missing cutting bounds display a dash.
Execution progress keeps its physical-line percentage and shows the original whole
program's nominal timeline: elapsed is cumulative motion/known-dwell time before the
active source block, including the skipped prefix on a selected start. Total remains
the original full-program estimate at 100% feed; remaining is total minus elapsed.
These values are independent of wall-clock session duration or the selected suffix.
Unknown tool/operator waits and partial progress within a source block are not
predicted. Text formatting is cached by seconds and percentage.
The first half of loading displays "Opening G-code" and the full source path; at
50% the caption switches to ToolPath preparation. The first startup report entry
is the interface name, written before form/control initialization.

G-code compilation and ToolPath preparation retain the complete accepted path,
text document and preview when a point lies outside configured axis travel. The
load result exposes `IsOutsideConfiguredTravel` and one nonblocking warning per
affected axis. The interface displays the warning and remains responsible for
the Run decision; the interpreter does not reject the file solely for this
load-time status. Runtime controller motion limits remain authoritative, and the
interface must reevaluate changes to references, transforms or limits before Run.

## Theme and 3D overlay scaling

The shared `sThemeButton` owns the moon/sun artwork and tooltip, does not take
keyboard focus, and delegates the persisted theme change to this application.
Startup, theme changes and language changes use the shared `sTheme` fade
transition. The 3D overlay keeps `OverlayScale` at its default value of `1`:
the form's `BoundsAndFonts` mode already scales the control font and layout, so a
larger overlay value would be an intentional second enlargement.

## Manual tool selection

`R_ToolSelector` uses the preset-list mode of `sParameterBox`. Its popup contains the
empty-spindle value and the currently enabled magazine tools. Enter, choosing a
list row, or the separate `Set tool` button commits the selected value through the
same manual tool-change path. Losing focus does not start machine motion. While an
exchange is active, the selector is disabled and shows only the confirmed
`CurrentTool` transitions. Escape restores the confirmed tool. The input well
follows the shared palette `Surface` color used by the standard text inputs.

## Localization and structured messages

Startup registers the application-owned embedded catalog before any form or runtime
notification is created. The fixed language list comes from `NT-Helper`; the main
window uses explicit `sLocalization` bindings and `sTheme.ChangeLanguage`, so visible
attached windows fade out, update once and fade back in. Units, axis identities and
technical tokens retain their invariant spelling.

RefreshRate captions and help text follow the selected language while its stored
High/Low choices stay invariant. All eight catalogs retain matching text identities,
format arguments and file-filter patterns. Operator preview messages describe the
visible preparation step without internal renderer terminology.

Manufacturer edits are sparse `TextLink` values under the hidden
`Localization/Overrides/<culture>` branch of the base settings group. Normal registry
save and settings export/import remain authoritative. No separate translation file is
written beside the machine settings.

Configuration exposes the shared Translations editor to Operator access through the existing
application override provider. The read-only original English text and editable selected-language
text are keyed by stable identities. Editing language does not switch the application language;
Apply validates and persists only translations, while Cancel discards unapplied drafts. Explicitly
applied translations survive cancellation of the parent Configuration dialog. Manual main-window captions
listen to override refreshes as well as language changes. The editor shares the application theme,
native DPI lifecycle and SaveWindowLayout preference, retaining its geometry and column split in
Internal/UI/TranslationEditor. No machine connection or runtime polling is required.

`RuntimeDeployment.targets` copies XML documentation from the interface's resolved references
in a separate managed target, even with `SkipNtRuntimeCopy=true`.
The runtime deployment target excludes those reference filenames
and their XML companions from ENI-tool/native-runtime bulk copies. Those folders may contain older
shared libraries and must not overwrite the SDK-deployed package/source reference set after Build.
The translation integration checks verify both an actual toolbar click showing the modal editor and
SHA-256 equality of all eight deployed DLL/XML pairs against the test's resolved libraries.

Runtime modal notifications use `sMessage`. Concise operator text is visible first;
the report description, recommended action, error code, technical evidence and
exception are collapsed under Details and remain copyable. Call sites that already
record the same diagnostic disable the dialog's duplicate report write.

## Configuration command availability

Opening Configuration and every exit path request the existing UI refresh so the
OpenConfiguration command is reevaluated after OK, Cancel, access-dialog cancellation
or an exception. The refresh stays on the UI timer; it adds no cyclic Core work.
Time-lock administration is always visible, and enabled only with Engineering
access and an authenticated board. Debug and Release outputs must both carry the
current ConfigurationManager assembly when switching launch configurations.

Deferred edits under Axes and ToolPaths, and edits to TraverseVelocity, require a
ToolPath rebuild. If the loaded program has execution progress, committing one of
these edits asks for confirmation and identifies the affected setting. No restores
only that edit; Yes accepts the draft. Controller application and rebuilding wait
until OK. Cancel restores the opening values. Returning a setting to its opening
value removes its reset requirement. UI-only changes, including RefreshRate, save
without reapplying controller properties or rebuilding the loaded program.

The final acceptance gate runs before saving and checks motion admission again.
It also confirms affected edits that did not pass through the property-grid prompt,
or whose confirmation belongs to an older execution-cursor generation. Refusing
this gate leaves Configuration open. These checks use the shared dialog's existing edit
journal and existing controller state; no new runtime class or cyclic work is
needed. Live reference editing retains its existing coordinate-update policy.

## Main command availability

Cable loss and controller-process disconnection are distinct. The main window
consumes the existing EtherCAT runtime events: link loss, OP loss and bring-up
retry close machine admission and refresh the status lamps and bindings; link-up
alone does not reopen admission. Completed OP recovery requests a full refresh.
Axis lamps show readiness independently of Home: the network must be operational,
the axis must be ready to move, and neither a drive fault nor a coupling fault may
be present. `R_Homed` shows completed Home for all configured axes, including the
second gantry axis; the 3D Home indication follows that same aggregate.
`R_Emergency` inverts the existing `EmergencySwitch` State Link for presentation:
the healthy NC circuit is high (lamp off), and an open circuit is low (red lamp on).
The cyclic `Background.plc` assigns `CONTROLLER.HOLD := NOT EmergencyClosed`.
Core applies the existing all-motion stop after the PLC scan and before motion
interpolation, blocks further axis/path/PLC motion, and preserves background logic.
Spindle shutdown, brake and tool-change interlocks remain separate PLC logic.
PLC and managed code write the same existing Hold bit and generation. There is no
independent PLC request, extra register or extra status flag. Release never resumes
interrupted motion. The shared-memory contract remains version 14.
`Core.Hold` and `HoldRequested` show the request observed on the managed feedback
cycle; `HoldActive` confirms the native lock, and `Stopping` means its braking or
command drainage is still pending. `MotionBlocked` can remain set after the request
becomes false. Read `StatusChanged` once per refresh; it is a shared read-and-clear
latch. The emergency lamp shows the input circuit, not proof of stopped motion.

Motion admission reads the shared Hold directly, without waiting for UI refresh.
A Jog or Run that passed admission immediately before Hold may still return true
from C#, but native execution rejects a late start and preserves controlled braking
for existing motion. This also covers tracking mode and early request release while
the native lock remains active. After release, only a new admitted command may run.
The Background assignment runs every PLC scan and can change a managed Hold request;
these are two writers of one request, not two independent locks.

For installations with a saved Background source, apply this source edit through
the existing PLC editor and validate/apply it; saved operator source remains
authoritative and is never silently replaced by the embedded default.
Core remapping clears Home. No automatic Home or motion
resume is issued by this presentation policy.

The UI timer captures one shared command-availability snapshot from independent
machine states. Reference editors and Set-reference commands, manual tool selection,
jog parameters and manual spindle controls consume that snapshot instead of each
repeating axis, ToolPath and PLC-program scans. Command execution rechecks live state
before publishing a controller request.

Reference editing is allowed offline, but connected reference edits and Set-reference
commands are disabled during motion, a TP0 program, program loading, Home, head
positioning or a tool operation. Manual tool selection and Set tool additionally
require an Online controller. Jog parameter editors are disabled while motion or a
managed operation owns their values. Jog commands deliberately remain enabled during
ordinary Jog so another Jog request can be issued; they are blocked only by a TP0
program, Home, head positioning, tool operation, disconnection or axis readiness.
The `R_JogMode` `sToggle` selects the two operator modes: checked issues one move
using the configured axis step, while unchecked moves continuously until release.
Every single-axis and diagonal Jog captures its effective velocity as the configured
axis Jog velocity multiplied by `R_JogFeedOverride / 100`.
Manual spindle controls remain available during ordinary Jog and are blocked by TP0,
Home, head positioning or tool-operation ownership. Stop remains available whenever
the controller is Online, and feed override remains live.

`PLC.IsApplying` is not part of main-window command policy. PLC application occurs
before Online connection or inside the modal Configuration flow, where main-window commands
cannot be reached. Controller and active-operation checks retain their own defensive
guards at the request boundary.

## Shared application access

Router composes one `NT.ConfigurationManager.ConfigurationAccessSession` and calls it before
startup connection and before opening Configuration. The shared session owns password
prompts and remembered access; no password policy is duplicated in the interface.
A configured Operator password is requested at startup. Cancellation or an incorrect
password leaves controller actions disabled and Configuration entirely read-only, with
its toolbar disabled. Opening Configuration permits retrying the Operator login.
A configured Engineering password is requested only on entry to Configuration; failure
keeps the existing Operator access and engineering fields read-only. Unconfigured
local accounts require no prompt, regardless of board connection state.
Successful checks remain valid until application shutdown. Closing Configuration,
cancelling edits, disconnecting or reconnecting does not forget them. Restart does.

Configuration Connect/Disconnect bindings additionally require Engineering, without
changing the main-window connection command's Operator requirement. Time Lock is
visible but disabled without Engineering or an authenticated board. LinkManager
checks authorization at command dispatch, including keyboard shortcuts; stop and
release remain available. Local authentication uses a worker for password derivation
and never transfers board data or adds work to the 1 ms loop.

Bench check in NonRt: configure both local passwords, restart, cancel Operator login,
verify main controller controls and all Configuration edits/toolbar are disabled; retry
with the correct Operator password, reject Engineering, verify engineering edits,
Configuration connection actions and Time Lock remain disabled; authenticate Engineering,
close and reopen Configuration repeatedly and verify no further prompt; restart to verify
both confirmations are forgotten. Also check the uniform login background in both
themes. Builds and automated tests do not substitute for this UI/controller check.

## Time-lock and board-information window

The shared window shows the full lock list on the left and board information above
lock editing on the right. It reads immediately on open, every minute, and after
successful mutations; no manual Refresh button is required. Runtime is board-accepted
total time, not a PC-derived estimate. A failed lock read and an empty list have distinct
presentations. The read-only information fields remain independent of lock-read failures.
If a read still fails on the bench, the report now includes `Micro-MM read failed`, the
operation number and hexadecimal transport result. Capture that report for diagnosis;
the UI screenshot alone does not identify the original transport failure.

The compact lock window is 720 by 524 logical pixels with two bordered right-hand
panels. Core notification registration now occurs after ENI/bus configuration, as
required by Acontis. This fixes the stale notification client behind the observed
0x9811000C mailbox-read rejection despite successful board authentication and OP.
Restart the application to load the corrected NT-Core executable; no MCU programming
is required. Confirm lock/clock and board-information reads on the physical board.

## Semantic I/O assignment

Each semantic digital/analog input/output has a persisted `Enabled` Link,
defaulting to true. Changes use the existing offline/failed mapping edit gate.
Disabling a signal retains its selected target, but compiles it exactly like
Unassigned typed internal storage, initially false/zero. Inputs remain read-only
to PLC source; outputs remain writable without addressing physical I/O.
Disabled mappings neither claim physical channels nor configure input filters.

`ASSIGNED(SignalName)` accepts one address-bound symbol and returns a LOGIC
compile-time literal. It is true for physical I/O (including inverter OUT/RUN)
and false for Unassigned, fixed TRUE/FALSE, virtual outputs and disabled mappings.
It checks configuration, not cable state or the current value. No new bytecode,
shared-memory layout or cyclic lookup is introduced. Example:

```pascal
IF ASSIGNED(ClampOpenSensor) THEN
    // Check sensor feedback.
ELSE
    // Use the operation's configured timer.
END_IF;
```

## Entry tool preparation and program priorities

`Configuration/Initialize.cs` owns `ProgramPriorities`, a programmer-editable
compiled table for registered PLC/macro auxiliary codes. Lower numbers execute
first; unknown codes default to zero. Defaults are M6=0, S=10, M3/M4/M5=20 and
M2/M30=100. No priority Link, settings editor or registry value is created.
The table controls same-block ordering and restart preambles; tool readiness
before entry positioning is a separate prerequisite that cannot be bypassed by
changing a numeric priority.

Fresh first-cutting-entry and selected-line plans prepare their retained M6
before TP1 positioning or spindle replay. `PrepareStartTool` reuses the staged
TP2 exchange, captures NewTool for the spindle PLC programs, waits for physical
grip and TP0 offset acknowledgement, and finishes withdrawal at safe Z. It does
not return to a destination calculated with the old tool. Its completion selects
the same entry without that fulfilled M6, then invokes ordinary ReturnToWork;
TP1 resolves the destination with the new offset and M100 starts the remaining
preamble/program. A same-tool entry performs the offset/feedback handshake with
no physical exchange. T selection alone does not change the required acquired
tool. Disabled automatic exchange retains its existing bypass behavior.

Stop, exchange failure and replaced cursors cannot invoke entry positioning.
Cancellation before completion retains the M6 for a subsequent explicit Run;
rejected positioning restores it as well. In-program M6 and manual exchanges
retain their existing continuation behavior.

Offline regression coverage is in `RouterEntryToolPreparationTests` in the
interpreter tests and `EntryToolPreparationTests` in the interface tests.
On the machine, verify a different-tool start, the same-tool case, a selected
cutting line with a different tool length, and Stop during exchange: no entry
traverse or spindle start may precede confirmed tool preparation. These offline
tests do not qualify physical spindle feedback or machine travel.

## Main-window runtime refresh

Tool-change routes carry optional text annotations alongside their existing
one-based TP2 block numbers. The active operation samples CurrentBlock without
consuming BlockChanged a second time, reports reached annotations once in order,
and includes intermediate annotations when UI ticks coalesce block transitions.
Annotations describe entry into a step, not success. Successful PLC results,
tool-state commits, acknowledged offsets and final completion are reported only
after their existing confirmation checks. Cancellation is reported once; failure
continues through the existing error report without a duplicate cancellation line.
No PLC logging instruction, bytecode change or shared-memory field is added.

The root `RefreshRate` preference selects the nominal UI runtime interval:
`High` (the default) is 40 ms / 25 updates per second; `Low` is 100 ms / 10 updates
per second. Accepted changes update the existing timer immediately. The regular
LinkGroup save on exit persists the choice, and startup restores it before starting
the timer. This is UI-side polling and presentation cadence, including application
sequences advanced by that tick; it does not change native motion or PLC cycle
periods. WinForms scheduling can delay ticks when the UI thread is busy.

Each UI tick drains each Axis/ToolPath change latch once. Axis alarm includes
drive faults and coupling faults on every configured motor, including YG, and is
updated only on an axis status edge or an explicit full refresh. Readiness and Home
remain separate indicators. Command availability changes trigger binding refresh;
the Jog gate is already part of that snapshot and needs no second comparison.

Position, module and live PLC sampling remain bounded because their changes need
not produce an axis status edge. Work-coordinate presentation reads Reference,
Offset and LiveOffset once each per tick and shares the resulting XYZ translation
between the 3D scene and numeric labels. Controller-owned snapshot copies remain
intact; extra per-axis snapshots and LINQ allocations are avoided. Unchanged
position/translation skips numeric work, and unchanged values skip label writes.

### G73 chip-breaking retract

Configuration > Router > G73 Retract Distance is the persisted RealLink at Router/G73RetractDistance. Default 0.254 mm, range 0.001..1000 mm. It uses the existing LinkManager save/load path and is not a PLC variable. CaptureContext snapshots it into RouterProfile before the load worker starts. Changes apply on the next G-code load/reload; prepared paths retain their captured value. G20/G21 and G51 do not change this physical lift; G73 caps it at R. Q sets successive cutting depths; G98/G99 select the final return level. Offline coverage: ChipBreakingSettingsTests verifies persistence and immutable capture, RouterChipBreakingTests verifies expansion and limits.

### Return destinations (G28/G30)

G28 captures the existing selected `Reference` row at G-code load. G30 captures `General/X/ParkPosition`, `General/Y/ParkPosition` and `General/Z/ParkPosition`, using the same settings provider as manual GoToPark. There are no separate Router/G28 or Router/G30 groups, Configured flags or duplicated XYZ settings. Existing LinkManager storage owns Reference and Park values. Reload G-code after editing these destinations; an already prepared program retains its captured targets. A later G54-G59 word in the file does not replace the captured G28 destination.

`G28`/`G30` without axis words move XYZ together to the captured target. Axis words first produce a rapid to an intermediate point in the current work frame and G90/G91 mode, then return only the named machine axes. `G91 G28 Z0` returns Z directly; `G90 G28 Z10` first goes to work Z10. Rotation may couple axes on the intermediate leg. No automatic Z-first withdrawal or homing is inferred. These are compiled ToolPath moves, not calls to the manual positioning commands: tool compensation and LiveOffset remain active under the existing G53 contract. Consequently G30 uses the Park coordinate values but does not guarantee the same uncompensated physical endpoint as manual GoToPark when compensation is nonzero. Both legs stop exactly, retain the physical source line and use existing travel diagnostics and restart selection. G28.1/G30.1 remain unsupported; see the Interpreter README for block restrictions.

Offline checks cover absence of duplicate groups, selected-reference mapping, existing Park values, immutable capture, persistence and work-reference invalidation. Physical machine motion has not been verified by these tests.

### Project package cache

The solution-local NuGet.Config sets globalPackagesFolder to a package-set revision directory under `%LOCALAPPDATA%/Radonix/NuGet/RouterXYZTC`. The revision is the first 16 hexadecimal characters of the package manifest SHA-256. Ordinary Visual Studio and dotnet restores use this persistent project cache instead of old same-version Radonix packages. No source-reference override or temporary command-line path is required. Run restore from this solution or its projects so this configuration is discovered; explicitly passing another NuGet config overrides it. While package versions remain frozen at 1.0.0, update this configured revision when refreshing the package set so ordinary restores consume the new binaries. Existing caches are retained. `--force` alone does not replace an already cached same-version package.

After updating the cache revision, restore both projects before building. From the RouterXYZ+TC solution directory run the following as separate steps:

```powershell
dotnet restore "RouterXYZ+TC.sln" --force -p:Platform=x64
dotnet build "RouterXYZ+TC.sln" --no-restore -c Debug -p:Platform=x64
```

Check that each project's `obj/project.assets.json` resolves packages from the new revision. When the solution is open in Visual Studio, also verify Build Solution in the active Debug | x64 configuration and check the Error List. Building the application alone can leave the simulation test project on the previous cache revision.

### G9/G09 one-block exact stop

G9/G09 with a motion uses the existing ToolPath stop flag for that block only. Linear and rapid moves stop at their endpoint; arcs stop at their final endpoint. It adds no timed dwell or PLC program and does not change the Core cycle. The prior modal path policy resumes on the next block. Standalone G9 and duplicate, dwell or coordinate-declaration combinations are rejected. Example: `G9 G1 X150 F1000`, followed by `G1 Y50`.

## PLC results and spindle command contract

`END_FUNCTION(Result := 0)` is successful completion. Every nonzero result is an
error, never a discriminator for successful application data. `ExitReason`
separates normal completion, explicit cancellation, VM fault and rejected start.
`PlcResultCatalog` assigns stable `Router.PLC.<program>.<result>` report codes and
specific descriptions to factory errors; undefined nonzero values still log as errors.
Runtime and admission failures include their exit reason even when Result is zero.

Core binds each ToolPath PLC activation to the dispatching path execution and cursor.
Completed nonzero, VM fault and rejected admission stop that path with `Fault`;
an explicitly cancelled activation prevents continuation without claiming success.
A manual PLC activation or an old execution cannot fault an unrelated/new path.
The existing Wait flags remain authoritative: successful non-waiting rows do not
introduce a stop, wait or global PLC barrier. M100 keeps its existing non-waiting
handoff. Background interlocks and PLC-owned cleanup sequences remain in force.
See `NT-Core/PLC-RESULT-CONTRACT.md` for the native/managed contract and tests.

`S` alone sets/clamps the normalized spindle speed (manual speed editing also
writes that setpoint). `M3`, `M4`, `M5`, end-of-program and interlock shutdown change
RUN/direction without zeroing or restoring speed. Thus S before or after M3 works,
and M5 followed by M3 retains the selected speed. S uses a non-waiting path row;
M3/M4/M5 retain their existing readiness/feedback waits. Saved custom PLC source is
authoritative and must be reviewed/applied against this contract before operation.

## Clear the main ToolPath

`ClearToolPath` (displayed as Clear ToolPath) is available in Keyboard with default
shortcut Ctrl+Shift+Alt+C. It clears TP0's prepared rows, retained source/compilation,
3D path, restart intent, cutting bounds and progress. It leaves the source file and
other scene objects intact. Admission requires idle TP0 with no pending Run or active
program wait, no axis motion, no running non-cyclic PLC program and no active macro.
Loading/preparation must finish first. Online/offline state, homing and faults are
not admission conditions. Both command availability and execution recheck the gate.


## Operator commands and panel pushbuttons

The main Stop control is now named **Reset**. The single `Reset` CommandLink stops
all motion and cancels auxiliary continuations, then rewinds TP0 after it becomes
idle. Pause continues to preserve the program cursor. The measurement button and
keyboard caption are **Measure Tool**; its persisted `MeasureToolOffset` identity
and measurement behavior are unchanged.

Physical panel inputs are sampled on the UI thread every 20 ms, independently of
the display refresh rate. `StopSwitch` (shown as **Reset Switch**), `PauseSwitch`
and `RunSwitch` invoke the same CommandLinks as the UI/keyboard, including access
and readiness checks. Existing mapping identities are preserved. Only enabled,
physically mapped inputs participate. NO Run/Reset must be observed released before
the next press after reconnect/remapping. NC Pause is checked immediately when
Online begins; OFF is an active request, including on the first sample. Reset takes priority over
Pause, and both suppress Run; rejected presses are consumed, never queued.
A pulse must span a UI sample; a busy UI can delay sampling. These operator
pushbuttons do not replace a hardware emergency-stop circuit.

The former Stop Run keyboard assignment is migrated once when Reset is unassigned.
An existing Reset assignment wins. The legacy registry value is retained for
recovery; clearing Reset later does not restore the old shortcut automatically.

### Operator PLC extensions and NC Pause

`Run`, `Pause`, and `Reset` are editable, non-cyclic PLC documents under Command
Programs. The interface queues the matching extension after accepting the operator
command, for buttons, shortcuts and assigned physical switches. Acceptance is not
physical completion. Pause/Reset queue all-motion Stop before their extension;
Reset still rewinds TP0 only after idle. The Run extension follows an accepted
start/preparation request, not each internal TP1/M100/TP2 continuation.

Factory Pause/Reset clear SpindleCW and SpindleCCW in one PLC scan without timers
or feedback waits. They preserve the analog/inverter speed setpoint and loader
vacuum. Factory Run is empty and does not restart the spindle. Engineers must
account for this when defining restart policy. Rejected PLC requests are logged.

Assigned, enabled physical PauseSwitch is normally closed: ON is released, OFF
requests Pause and inhibits motion starts. It is checked on entering Online and
on every physical-input poll; direct main Run admission reads the live mapping.
Returning ON never queues Run or replays a held Run button. Unassigned, disabled
and constant fallback Pause mappings do not apply the physical interlock.

The application-owned, read-only OperatorInterlocks cyclic source combines
EmergencySwitch and NC Pause in one controller Hold assignment. Its reverse scan
order is Background, OperatorInterlocks; application zones observe cycle feedback. Native Hold admission covers
managed and PLC starts and cancels pending motion continuations; ordinary operator
Pause still preserves the machining cursor. This is software control, not a
safety-rated emergency-stop circuit.

On loading saved Background source, only the former factory assignment
`CONTROLLER.HOLD := NOT EmergencyClosed;` is migrated to a comment. All other
engineer source is preserved. OperatorInterlocks owns Hold assertion/release;
custom PLC code must not introduce a competing Hold writer. Saved Run/Pause/Reset
extensions remain authoritative and editable across restarts.

## Configurable gantry axes and generated homing

`Controller / Axes` exposes Engineering-only `X Gantry`, `Y Gantry`, `Z Gantry`
and `Home Sequence`. All three gantries default to disabled; saved selections remain
in force. Retained order X/Y/YG/Z/XG/ZG and Axis00..Axis03 / named XG/ZG storage
preserve each logical motor's settings when it is disabled and later added again.
Changes require a disconnected controller and empty ToolPaths. Normal mode verifies
RNT3 signed capacity for every selected motor; unlicensed Simulation permits 16.

`Home Sequence` defaults to `Z>X,Y`: finish Z, then search/release X and Y together.
`>` separates sequential stages; `,` groups simultaneous sensor operations. Each sensor axis must appear once. Absolute axes are optional. Included absolute motors move to HomePosition at HomeVelocity and must stop within one encoder step before the sensor operations in that stage. Omitted absolute motors retain their position and must have a valid reference. Both motors of an absolute gantry stay coupled and require compatible destinations. A mixed-method gantry is rejected. `Z>X>Y`, `X,Y>Z`, and `X,Y,Z` are valid.
Only XYZ appear in the sequence. A selected companion with an enabled physical home
sensor decouples, finds/releases its own sensor, displaces, and recouples. An absent
or disabled companion sensor selects master-only homing with continuous coupling;
the companion retains its coupling offset and follows the master's coordinate and
displacement. A constant input is not a home sensor. Each sensor primary needs a physical
home input. Invalid/missing module targets remain subject to ordinary PLC validation.

One configuration-time generator creates HomeX/HomeY/HomeZ and the full Home program
(the coordinated home program is named `Home XYZ`). Captured settings,
per-phase timeout, stop-before-coordinate assignment, signed displacement and coupling
recovery run entirely in PLC/Core. Within a parallel stage, displacement paths finish
sequentially because an activation owns one internal ToolPath. The next stage starts
only after all displacement and coupling operations finish.

Generated source carries a body checksum. Untouched generated text and the current
embedded factory text are regenerated; engineering edits are preserved and described
as engineer-owned in the editor. Such custom programs must be updated manually when
changing their axis dependencies. The existing LinkManager values own persistence;
Cancel restores edited inputs/source and regenerates the corresponding automatic text.
No alternate configuration store or runtime source generator is introduced.

Jog links resolve logical names; positioning, tool transfer and tool-change axis views
follow the current ToolPath arrays while retaining coordinate-layout checks. This
permits Z to move between physical slots when Y gantry changes. Tool-change routes,
ordering and measurement logic are unchanged. Raw AXnn/custom PLC references still
block topology edits when their physical meaning would change.

Verification: all 13 stage orders and 27 gantry/sensor combinations compile; native
GeneratedHomeTests verifies stage barriers, independent sensors, continuous sensorless
coupling, displacement and timeout recovery. AxisTopologyTests covers defaults,
licensed admission, retained settings, custom-source preservation, save/reload and
Cancel. Tests use isolated settings and offline memory; no hardware qualification.
Native fixtures are exported by NT-Controller.Tests/HomeGenerationTests with
`NT_GENERATED_HOME_FIXTURES_DIR`; both HomeDisplaceTests and GeneratedHomeTests consume
the directory. Compile each native test with Axis.cpp and winmm.lib using MSVC x64.

## Composable ToolPath readiness

`ToolPath` and `ToolPathWrapper` expose `IsHomed`, `AreAxesIdle`, and `AreAxesReady`.
Their PLC equivalents are read-only `TPn.Homed`, `TPn.AxesIdle`, and `TPn.AxesReady`.
They examine the selected path axes plus configured gantry motors (even when runtime
coupling is temporarily removed) and any additional runtime-coupled motors. Empty
paths return false; unrelated controller axes are excluded.

`IsReadyToMove` / `TPn.ReadyToMove` retains the existing native path readiness bit.
`IsIdle` / new `TPn.Idle` means no Running/Stopping and no Run/Stop/ResetRequest;
it does not imply stationary axes or an inactive paused macro. `ProgramActive`
remains the separate session check. `AreAxesReady` checks every effective motor's
ReadyToMove and absence of drive/coupling faults; it does not validate ownership,
travel, homing or establishment of a configured coupling. `AreAxesIdle` additionally
observes pending Run/Stop/Jog/Home/Track commands, not only published Stopped bits.
For stopped, homed admission, compose `IsIdle && AreAxesIdle && AreAxesReady && IsHomed`
with the operation's existing connection, ownership and machine interlocks.

Factory positioning, measurement preflight, M100, Background and zone admission use
these aggregates. Tool-change coupling/ownership/tolerance checks and phase-specific
single-axis waits remain in their existing algorithms. Persisted engineering PLC
sources remain authoritative and are not rewritten automatically.

Shared-memory contract 18 publishes configured coupling masks in registers 967..982.
PLC format 18 and scan-cost model 11 add computed read-only addresses: live reads see
axis commands from earlier in the same scan. Traversals are bounded to sixteen motors,
allocate nothing and are charged conservatively in the scan budget. Controller and
native Core must be deployed together; mixed contract versions are rejected.

Verification (2026-11-08): 63 targeted Controller tests passed, plus the earlier
32-test readiness/coupling/axis-order/compiler run. All 203 Router tests passed
against freshly rebuilt Release 1.0.0 packages; all nine published hashes and the
deployed native Core hash were checked. Native ToolPathReadiness, PlcRuntimeLanguage,
PlcControllerOrdering, ToolPathAxisOrder, ToolPathResume, CoupledMotionLimits and
HomeDisplace checks passed, including repeated homing and stuck-sensor recovery.
The separate native ProgramArg test stops at its pre-existing queue-capacity
assertion (line 181): it expects rejection after 1024 posted messages, while
PostedMessageLength is already 8192 in the unchanged implementation. That queue
and test behavior were not changed. No physical-machine timing qualification was run.

The compiled axis definition now has one source: `Initialize.AxisProfile` contains
X/Y/Z, each primary/companion ordering position, and its gantry default. `MainAxes`,
`AxisDefinitions` and `DefaultAxisCount` are derived from it; the separately maintained
`ControllerAxes`, `AvailableAxes` and `GantryAxisNames` arrays have been removed.
Ordering remains X/Y/YG/Z/XG/ZG independently of the defaults. Existing Axis00..Axis03
and named XG/ZG storage keys remain unchanged; accepted configuration selects the
actual controller membership through the existing LinkManager workflow.

## Gantry names in PLC

XG, YG and ZG are the actual companion-axis identifiers and appear as, for example, YG [AX02]. HomeSensorXG/HomeSensorYG/HomeSensorZG use the matching sensor names. Compact path strings treat XG/YG/ZG as indivisible names: XYGZ selects X, YG, Z. Numeric G-code coordinates are unchanged. Physical order and calibration are unchanged; current saved companion and mapping groups were renamed once, without a permanent alias or migration loader.


## Customer development delivery

In the manufacturer repository, `Setup/BuildSetup.ps1` builds the production installer. In the customer source delivery, it builds an installer from the customer's edited interface. For customization, extract the matching `RouterXYZ+TC.zip` from `NT-Interfaces For Publish` and open `RouterXYZ+TC.sln`. The delivery README covers offline restore, Release x64 build, tests, publish and compiling the included Setup source. The customer copy includes the application, its simulation, PLC sources, operator manual, public NuGet dependencies and matching Radonix runtime binaries. Maintainers can regenerate this model using the workflow in `NT-Interfaces For Publish/README.txt`.

# RouterXYZ+TC simulation

These sources belong to this interface and compile into its application assembly. There is no shared simulation library or runtime project. Evolve the modules and MainForm runtime composition with this machine's configuration.

SimulationHost activates only during an online Hard Simulation session, serializes bounded module updates, resets driven inputs on shutdown, and stops on module faults. Production PLC programs and controller admission remain authoritative. Never enable simulation modules for Production RT or Engineering Non-RT NDIS sessions.

The sibling RouterXYZ+TC.Simulation.Tests project exercises this interface's simulation with a fake runtime; it is not a runtime dependency.

## Extending the simulated machine

`MainForm.RecreateMachineSimulation` is the composition point. It runs after the
machine settings are applied and creates one host for the next connection.
`ControllerSimulationRuntime` translates semantic signal names into the existing
Hard Simulation input image or independent PLC storage. Modules should never
parse physical addresses, open settings, touch controls or start their own timers.

| Model | Behavior |
| --- | --- |
| `PredicateInputSimulation` | Publishes a simple machine predicate, currently the healthy emergency circuit. |
| `PositionSwitchSimulation` | Axis threshold sensors with release hysteresis for Home. |
| `DigitalFeedbackSimulation` | Reusable output-to-input feedback with independent on/off delays. |
| `SpindleSimulation` | Release/clamp mechanics, holder pickup, pressure/health feedback and rotation coast-down. |
| `ToolProbeSimulation` | Probe contact based on actual XYZ, captured physical tool length and hysteresis. |

Every input has exactly one module owner, declared by `DrivenInputs`. Keep the
collection and its order stable for the host lifetime. `Reset` clears mechanical
history and publication state; subsequent updates must publish initial values even
if they are false, after any configured feedback delay. Later updates publish edges only. Simulated mechanics use
the measured elapsed interval supplied by the host, not an assumed exact tick.

Spindle pickup is inferred from holder position, not from a premature CurrentTool
write. Probe tool lengths are captured separately from the length subsequently
measured by PLC. These distinctions let the production confirmation sequence run
without making the simulation prove its result by reading that same result.

Test each new model with an `ISimulationRuntime` fake: initial state, both edges,
delay/hysteresis, reset/reconnect and fault behavior. Host-level tests must retain
the duplicate-owner rejection and inactive-hardware-session behavior. Simulation
cannot qualify wiring, mechanical clearance, real spindle stopping time or RT jitter.

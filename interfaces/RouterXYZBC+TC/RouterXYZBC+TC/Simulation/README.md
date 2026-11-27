# RouterXYZBC+TC simulation

Simulation is compiled into this interface, not a shared library. `MainForm.RecreateMachineSimulation` builds the module set after configuration is applied. `SimulationHost` runs serialized 10 ms updates only while Hard Simulation is online. Hardware and Engineering NDIS sessions never activate these input models.

## Short Home cycle

`HomingSwitchSimulation` watches the full Home program or the axis's individual Home program. Gantry companions watch their primary axis program. At activation, it captures a `PositionSwitchDefinition` relative to actual position in the configured search direction: 5 mm for X/Y/Z and companions; 2 degrees for B/C. It reuses `PositionSwitchSimulation` for threshold crossing and 0.05-unit release hysteresis.

The threshold is captured once per activation, not once per movement phase. Search, controlled stop, reverse release, home-coordinate assignment and configured displacement still execute in the ordinary PLC/controller path. Completion/cancellation discards the threshold; reconnect resets all module state. Repeated Home therefore stays short even after the coordinate frame has been reset. These virtual sensors are a training convenience, not a physical end-switch model. Physical travel, speed, direction and home settings are unchanged.

## Other input models

- `PredicateInputSimulation`: healthy emergency circuit.
- `DigitalFeedbackSimulation`: delayed roller/output feedback.
- `SpindleSimulation`: release/clamp mechanics, pressure/health and coast-down. Pickup requires enabled holder XYZBC alignment, not a premature CurrentTool write.
- `ToolProbeSimulation`: XYZ probe contact and independently captured physical tool length. The host admits probing only at B=C=0.

`ControllerSimulationRuntime` translates semantic names to the Hard Simulation input image or independent PLC storage. Each input has exactly one module owner. Modules publish initial values and subsequent edges, never write axis positions or issue motion commands. They do not use UI controls or private timers. Reset/disposal clears driven inputs; a module fault stops the complete simulation host.

The per-tick path is bounded. Home threshold allocation occurs only at activation. Tests use an `ISimulationRuntime` fake and cover both directions, capture stability, repeated Home, cancellation, reconnect, hysteresis and duplicate-input rejection. Interface tests must run with other controller applications closed to avoid shared-memory interference.

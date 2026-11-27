using NT.Controller.PLC;
using NT.LinkManager;

namespace RouterXYZACTC;

/// <summary>UI-thread physical pushbuttons. No command work runs on the RT thread.</summary>
internal sealed class OperatorSwitches
{
    private readonly Input reset;
    private readonly Input pause;
    private readonly Input run;
    private readonly CommandLink resetCommand, pauseCommand, runCommand;

    internal OperatorSwitches(PLCIOMapping reset, PLCIOMapping pause, PLCIOMapping run,
        CommandLink resetCommand, CommandLink pauseCommand, CommandLink runCommand)
    {
        this.reset = new(reset);
        this.pause = new(pause, normallyClosed: true);
        this.run = new(run);
        this.resetCommand = resetCommand;
        this.pauseCommand = pauseCommand;
        this.runCommand = runCommand;
    }

    private JogInput[] jogInputs = [];
    private JogInput[] assignedJogInputs = [];

    internal void ConfigureJog(IEnumerable<JogInput> inputs)
    {
        Disconnect();
        jogInputs = inputs.ToArray();
        RefreshJogMappings();
    }

    internal void RefreshJogMappings()
    {
        foreach (JogInput input in jogInputs) input.RefreshTarget();
        assignedJogInputs = jogInputs.Where(input => input.Assigned).ToArray();
    }

    internal void Disconnect()
    {
        reset.Disarm();
        pause.Disarm();
        run.Disarm();
        foreach (JogInput input in assignedJogInputs) input.Disarm();
    }

    // Read the mapping directly, not the previous UI-timer sample.
    internal static bool IsPauseRequested(PLCIOMapping mapping)
        => mapping.IsPhysicalTarget && !mapping.GetLogic();

    internal void Poll(bool online)
    {
        if (!online) { Disconnect(); return; }
        // Sample all edges before dispatch: a rejected/suppressed Run is consumed,
        // never queued for a later permission or readiness change.
        bool resetEdge = reset.Sample();
        bool pauseEdge = pause.Sample();
        bool runEdge = run.Sample();
        bool inhibitJog = reset.High || pause.High || run.High;
        // Complete releases before admitting another direction in the same sample.
        foreach (JogInput input in assignedJogInputs) input.Sample(inhibitJog);
        if (reset.High) { if (resetEdge) resetCommand.TryExecute(); }
        else if (pause.High) { if (pauseEdge) pauseCommand.TryExecute(); }
        else if (runEdge) runCommand.TryExecute();
        foreach (JogInput input in assignedJogInputs) input.Dispatch();
    }

    internal sealed class JogInput(PLCIOMapping mapping, CommandLink command, bool hold)
    {
        private string? target;
        private bool armed;
        private bool pending;
        internal bool Assigned => target != null;

        internal void RefreshTarget()
        {
            string? next = mapping.IsPhysicalTarget ? mapping.Target : null;
            if (target == next) return;
            Disarm();
            target = next;
        }

        internal void Disarm()
        {
            pending = false;
            armed = false;
            // This physical source owns the programmatic begin/end pair. Mouse and
            // keyboard bindings independently retain their own source identities.
            if (hold && command.Value) command.Value = false;
        }

        internal void Sample(bool inhibited)
        {
            bool high = mapping.GetLogic();
            if (!high || inhibited)
            {
                Disarm();
                armed = !high;
                return;
            }
            pending = armed;
            armed = false;
        }

        internal void Dispatch()
        {
            if (!pending) return;
            pending = false;
            if (!hold) command.TryExecute();
            else if (command.Enabled && command.CanWrite(UI.CurrentPermission))
                command.Value = true; // Value also checks authorization and dynamic readiness.
        }
    }

    private sealed class Input(PLCIOMapping mapping, bool normallyClosed = false)
    {
        private string? target;
        private bool armed;
        // Logical request state: NC Pause is active when the physical input is LOW.
        internal bool High { get; private set; }
        internal void Disarm() { armed = false; High = false; target = null; }
        internal bool Sample()
        {
            if (!mapping.IsPhysicalTarget) { Disarm(); return false; }
            // NC Pause is level-sensitive on connection/remap as well as on
            // subsequent opening edges. NO Run/Reset still require a release.
            if (target != mapping.Target) { armed = normallyClosed; target = mapping.Target; }
            High = normallyClosed ? !mapping.GetLogic() : mapping.GetLogic();
            if (!High) { armed = true; return false; }
            bool edge = armed;
            armed = false;
            return edge;
        }
    }
}

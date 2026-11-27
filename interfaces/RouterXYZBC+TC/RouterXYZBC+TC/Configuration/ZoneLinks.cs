using NT;
using NT.Controller;
using NT.Diagnostics;
using NT.LinkManager;

namespace RouterXYZBCTC;

/// <summary>LinkManager owns settings; ZoneManager owns runtime state and output commands.</summary>
internal sealed class ZoneLinks : IDisposable
{
    private readonly MainForm form;
    private readonly List<Zone> allZones = [];
    private readonly List<StatusSource> statusSources = [];
    internal ZoneManager? Manager { get; private set; }
    internal LogicLink Enabled { get; }
    internal IntegerLink Count { get; }
    internal LinkGroup Group { get; }
    internal CommandLink ClampLink { get; }
    internal CommandLink ReleaseLink { get; }
    internal IReadOnlyList<Zone> Zones => allZones.Take((int)Count.Value).ToArray();
    internal sealed record Zone(LinkGroup Group, LogicLink Enabled, TextLink Mode, LogicLink Selected,
        RealLink MinX, RealLink MaxX, RealLink MinY, RealLink MaxY);

    internal ZoneLinks(MainForm form, LinkGroup parent)
    {
        this.form = form;
        Group = parent.AddGroup("Zone", "Zone", "Application-managed machine-coordinate zones. Assign physical channels in PLC Mapping.");
        Group.IconKey = "parameter";
        Enabled = Group.AddLogic("Enabled", "Enabled", false, "Enable application zone control. Offline configuration only; zones never gate TP0 Run.");
        Enabled.Source = new GuardedLogic(Enabled.Value, CanConfigure);
        Count = Group.AddInteger("ZoneCount", "Zone Count", Initialize.DefaultZoneCount, 0, Initialize.MaximumZoneCount,
            "Number of zones. Change while disconnected. Hidden zone settings and mappings are retained for reuse.");
        Count.Permission = AccessPermission.Engineering;
        Count.UnauthorizedView = AccessView.ReadOnly;
        Count.Source = new GuardedInteger(Count.Value, () => CanConfigure() && form.controller.PLC.IOMappings.CanModify);
        Count.QueryEnabled += (_, e) => e.Enabled &= form.controller.PLC.IOMappings.CanModify;
        foreach (Link link in new Link[] { Enabled, Count }) link.QueryEnabled += (_, e) => e.Enabled &= CanConfigure();
        AddStatus(Group, "Clamped", () => Manager?.Clamped == true,
            "At least one workholding ON command was accepted. No vacuum feedback or Run interlock.");
        ClampLink = Group.AddCommand("ZoneClamp", "Clamp Zones", "Turn on selected workholding outputs while stopped. TP0 does not require clamping.");
        ReleaseLink = Group.AddCommand("ZoneRelease", "Release Zones", "Release all workholding zones while motion and tool operations are stopped.");
        ClampLink.QueryEnabled += (_, e) => e.Enabled &= CanCommand && SelectedMask() != 0 && Manager?.Clamped != true;
        ReleaseLink.QueryEnabled += (_, e) => e.Enabled &= CanCommand && Manager?.Clamped == true;
        ClampLink.ActionExecute += (_, _) => { if (CanCommand && Manager!.Clamp(SelectedMask())) NTLog.Report("Zone clamp requested."); };
        ReleaseLink.ActionExecute += (_, _) => { if (CanCommand && Manager!.Release()) NTLog.Report("Zone release requested."); };
        for (int i = 1; i <= Initialize.MaximumZoneCount; i++)
        {
            string name = $"Zone{i}";
            var group = Group.AddGroup(name, $"Zone {i}", "Extraction uses machine X/Y bounds. Workholding remains on through pause, stop and tool exchange until Release Zones.");
            var enabled = group.AddLogic("Enabled", "Enabled", false);
            enabled.Source = new GuardedLogic(enabled.Value, CanConfigure);
            var mode = group.AddText("Mode", "Mode", "Extraction", new[] { "Extraction", "Workholding" });
            mode.ItemsOnly = true;
            mode.Validator = value => value is "Extraction" or "Workholding";
            mode.Source = new GuardedText(mode.Value, CanConfigure);
            var selected = group.AddLogic("Selected", "Selected", false, "Select this workholding zone before Clamp Zones.");
            selected.Source = new GuardedLogic(selected.Value, () => CanSelect);
            var minX = group.AddReal("MinX", "Minimum X", 0, -1e9, 1e9, 3, "Minimum X machine coordinate of the zone, in millimeters.");
            var maxX = group.AddReal("MaxX", "Maximum X", 0, -1e9, 1e9, 3, "Maximum X machine coordinate of the zone, in millimeters.");
            var minY = group.AddReal("MinY", "Minimum Y", 0, -1e9, 1e9, 3, "Minimum Y machine coordinate of the zone, in millimeters.");
            var maxY = group.AddReal("MaxY", "Maximum Y", 0, -1e9, 1e9, 3, "Maximum Y machine coordinate of the zone, in millimeters.");
            foreach (RealLink real in new[] { minX, maxX, minY, maxY }) real.Source = new GuardedReal(real.Value, CanConfigure);
            foreach (Link link in new Link[] { enabled, mode, minX, maxX, minY, maxY }) link.QueryEnabled += (_, e) => e.Enabled &= CanConfigure();
            selected.QueryEnabled += (_, e) => e.Enabled &= CanSelect;
            var output = form.controller.PLC.IOMappings[name];
            int zoneIndex = i;
            AddStatus(group, "State", () => Enabled.Value && zoneIndex <= Count.Value && form.controller.IsConnected && output.GetLogic());
            allZones.Add(new(group, enabled, mode, selected, minX, maxX, minY, maxY));
        }
        Count.Changed += (_, _) => RefreshVisibility();
        RefreshVisibility();
        form.controllerLinks.RegisterLiveRuntimeSource(new StatusRefresh(statusSources,
            () => Enabled.Value && form.controller.IsConnected ? (int)Count.Value : 0));
    }

    internal void StartRuntime() => Manager = new ZoneManager(form.controller, CaptureConfiguration);
    private bool CanConfigure() => form.controller.ConnectionState.AllowsOfflineConfiguration();
    private bool Idle => !form.controller.IsBusy && !form.IsProgramLoading && !form.IsTransferActive
        && form.toolChangerLinks?.IsActive != true && !form.controllerLinks.IsHeadPositioningActive
        && !form.controllerLinks.IsHomingActive() && form.controller.ToolPaths.All(p => !p.IsProgramActive);
    private bool CanSelect => CanConfigure() || form.IsMachineOnline && Idle && Manager?.Clamped != true && Manager?.Pending != true;
    private bool CanCommand => Enabled.Value && form.IsMachineOnline && Idle && Manager?.Ready == true && !Manager.Pending;

    private void RefreshVisibility()
    {
        for (int i = 0; i < allZones.Count; i++) allZones[i].Group.View = i < Count.Value ? AccessView.ReadWrite : AccessView.Hidden;
        form.controllerLinks.SetZoneMappingCount((int)Count.Value);
    }

    internal static ulong OutputMask(string target)
    {
        if (target.Length == 8 && target.StartsWith("DM", StringComparison.OrdinalIgnoreCase)
            && target.AsSpan(3, 4).Equals(".OUT", StringComparison.OrdinalIgnoreCase)
            && target[2] is >= '0' and <= '7' && target[7] is >= '0' and <= '7')
            return 1UL << ((target[2] - '0') * 8 + target[7] - '0');
        return 0;
    }

    private ulong SelectedMask()
    {
        ulong selected = 0;
        for (int i = 0; i < Count.Value; i++)
        {
            var zone = allZones[i];
            if (zone.Enabled.Value && zone.Mode.Value == "Workholding" && zone.Selected.Value)
                selected |= OutputMask(form.controller.PLC.IOMappings[$"Zone{i + 1}"].Target);
        }
        return selected;
    }

    private ZoneDefinition[]? CaptureConfiguration()
    {
        if (!Enabled.Value || Count.Value == 0 || !ValidateSettings()) return null;
        var definitions = new ZoneDefinition[(int)Count.Value];
        for (int i = 0; i < definitions.Length; i++)
        {
            var zone = allZones[i];
            var mapping = form.controller.PLC.IOMappings[$"Zone{i + 1}"];
            definitions[i] = new(mapping.IsPhysicalTarget ? OutputMask(mapping.Target) : 0,
                zone.Enabled.Value, zone.Mode.Value == "Workholding", zone.MinX.Value, zone.MaxX.Value, zone.MinY.Value, zone.MaxY.Value);
        }
        return definitions;
    }

    internal bool ValidateSettings()
    {
        if (!Enabled.Value) return true;
        for (int i = 0; i < Count.Value; i++)
        {
            var zone = allZones[i];
            if (!zone.Enabled.Value) continue;
            var mapping = form.controller.PLC.IOMappings[$"Zone{i + 1}"];
            if (!mapping.IsPhysicalTarget || OutputMask(mapping.Target) == 0 ||
                zone.Mode.Value == "Extraction" && (zone.MinX.Value >= zone.MaxX.Value || zone.MinY.Value >= zone.MaxY.Value))
            {
                NTLog.Warning($"Zone{i + 1}: assign an enabled DM output and valid extraction bounds before enabling the zone.");
                return false;
            }
        }
        return form.controllerLinks.ValidateZoneOutputOwnership((int)Count.Value);
    }

    internal bool ExcludeFromRollback(Link link) => !CanConfigure() && allZones.Any(z => ReferenceEquals(z.Selected, link));

    private void AddStatus(LinkGroup group, string name, Func<bool> read, string description = "Live mapped output state; not vacuum feedback.")
    {
        var link = group.AddLogic(name, name, false, description);
        link.View = AccessView.ReadOnly;
        var source = new StatusSource(read);
        statusSources.Add(source);
        link.Source = source;
    }
    private sealed class StatusRefresh(List<StatusSource> sources, Func<int> activeCount) : ILiveRuntimeSource
    {
        private int previousCount;
        public void PublishValueChanged()
        {
            int count = activeCount();
            // Root status plus active zones; publish removed entries once to clear stale presentation.
            for (int i = 0; i <= Math.Max(previousCount, count); i++) sources[i].PublishValueChanged();
            previousCount = count;
        }
    }
    private sealed class StatusSource(Func<bool> read) : ILogicSource, ILivePlcSource
    {
        public event EventHandler? ValueChanged;
        public bool GetValue() => read();
        public void SetValue(bool value) { }
        public void PublishValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);
    }
    private static void RequireEditable(bool changed, Func<bool> canEdit)
    { if (changed && !canEdit()) throw new InvalidOperationException("Zone setting cannot change during this controller operation."); }
    private sealed class GuardedLogic(bool value, Func<bool> canEdit) : ILogicSource
    {
        public event EventHandler? ValueChanged { add { } remove { } }
        public bool GetValue() => value;
        public void SetValue(bool next) { RequireEditable(next != value, canEdit); value = next; }
    }
    private sealed class GuardedInteger(long value, Func<bool> canEdit) : IIntegerSource
    {
        public event EventHandler? ValueChanged { add { } remove { } }
        public long GetValue() => value;
        public void SetValue(long next) { RequireEditable(next != value, canEdit); value = next; }
    }
    private sealed class GuardedText(string value, Func<bool> canEdit) : ITextSource
    {
        public event EventHandler? ValueChanged { add { } remove { } }
        public string GetValue() => value;
        public void SetValue(string next) { RequireEditable(next != value, canEdit); value = next; }
    }
    private sealed class GuardedReal(double value, Func<bool> canEdit) : IRealSource
    {
        public event EventHandler? ValueChanged { add { } remove { } }
        public double GetValue() => value;
        public void SetValue(double next) { RequireEditable(next != value, canEdit); value = next; }
    }
    public void Dispose() => Manager?.Dispose();
}

using NT.ConfigurationManager;
using NT.Controller;
using NT.Controller.PLC;
using NT.LinkManager;
using NT_Interpreter.Shared.Router;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CutterXYZ;

/// <summary>Persists custom command membership through Links and preserves source identities for dialog undo.</summary>
internal sealed partial class ControllerLinks : IConfigurationCommandPrograms
{
    private TextLink customProgramNames = null!;
    private readonly HashSet<string> factoryProgramNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextLink> customProgramSources = new(StringComparer.OrdinalIgnoreCase);
    private string[] activeCustomPrograms = [];
    internal IConfigurationCommandPrograms CommandProgramEditor => this;
    LinkGroup IConfigurationCommandPrograms.Group => plcGroup.Groups[CommandProgramsGroupName];

    public string EditRestriction => !controller.ConnectionState.AllowsOfflineConfiguration()
        ? "Disconnect the controller completely before adding or removing PLC programs."
        : mainForm.IsProgramLoading || mainForm.IsConnectionTransition || !controller.PLC.IOMappings.CanModify
            ? "Clear loaded programs from every ToolPath and wait for controller work to finish."
            : string.Empty;

    private void InitializeCustomCommandPrograms()
    {
        factoryProgramNames.UnionWith(plcSourceLinks.Keys);
        var metadata = plcGroup.AddGroup("CustomPrograms", "Custom Programs");
        metadata.View = AccessView.Hidden;
        customProgramNames = metadata.AddText("Names", "Names", "[]");
        SynchronizeCustomCommandPrograms();
        customProgramNames.Changed += (_, _) => SynchronizeCustomCommandPrograms();
    }

    private string NormalizeCustomProgramName(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 64 || !Regex.IsMatch(name, @"\A[A-Za-z_][A-Za-z0-9_]*\z"))
            throw new InvalidOperationException("Use 1–64 letters, digits or underscores, starting with a letter or underscore.");
        if (RouterAuxiliaryCode.TryParse(name, out var code)) name = code.ToString();
        if (factoryProgramNames.Contains(name) || name.Equals("M6", StringComparison.OrdinalIgnoreCase)
            || name.Equals("M30", StringComparison.OrdinalIgnoreCase)
            || controller.MotionController.Macros.Programs.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"'{name}' is reserved by a default program or Macro.");
        return name;
    }

    private string[] ReadCustomProgramNames()
    {
        string[] names = JsonSerializer.Deserialize<string[]>(customProgramNames.Value)
            ?? throw new InvalidOperationException("Custom program membership must be an array.");
        if (names.Length + factoryProgramNames.Count - 1 > PLCProgramCollection.MaximumCount)
            throw new InvalidOperationException($"The PLC supports at most {PLCProgramCollection.MaximumCount} programs.");
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in names)
            if (name is null || NormalizeCustomProgramName(name) != name || !unique.Add(name))
                throw new InvalidOperationException("Custom program names must be unique canonical names.");
        return names;
    }

    private bool ValidateCustomCommandPrograms()
    {
        try
        {
            if (!ReadCustomProgramNames().SequenceEqual(activeCustomPrograms))
                throw new InvalidOperationException("Custom program membership has not been applied to the offline PLC draft.");
            return true;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            plcGroup.Groups[CommandProgramsGroupName].SetState(OperationalState.Error, error.Message);
            return false;
        }
    }

    bool IConfigurationCommandPrograms.CanRemove(TextLink program)
        => customProgramSources.TryGetValue(program.Name, out var source) && ReferenceEquals(source, program)
            && activeCustomPrograms.Contains(program.Name, StringComparer.OrdinalIgnoreCase);

    TextLink IConfigurationCommandPrograms.Add(string name, Action<TextLink, string> edit)
    {
        if (EditRestriction.Length != 0) throw new InvalidOperationException(EditRestriction);
        name = NormalizeCustomProgramName(name);
        if (activeCustomPrograms.Contains(name, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Program '{name}' already exists.");
        if (controller.PLC.Programs.Count >= PLCProgramCollection.MaximumCount)
            throw new InvalidOperationException("The PLC program limit has been reached.");
        TextLink source = GetOrCreateCustomSource(name);
        // Re-adding is a new program. The dialog retains the previous text for undo.
        edit(source, "END_FUNCTION(Result := 0);\n");
        edit(customProgramNames, JsonSerializer.Serialize(activeCustomPrograms.Append(source.Name)));
        return source;
    }

    void IConfigurationCommandPrograms.Remove(TextLink program, Action<TextLink, string> edit)
    {
        if (EditRestriction.Length != 0) throw new InvalidOperationException(EditRestriction);
        if (!((IConfigurationCommandPrograms)this).CanRemove(program))
            throw new InvalidOperationException("Default programs cannot be removed.");
        foreach (var source in plcSourceLinks.Values.Where(s => s.View != AccessView.Hidden && !ReferenceEquals(s, program)))
            if (SourceIdentifiers(source.Value).Contains(program.Name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"'{program.Name}' is referenced by PLC document '{source.Name}'. Remove that reference first.");
        edit(customProgramNames, JsonSerializer.Serialize(activeCustomPrograms.Where(n => n != program.Name)));
    }

    private TextLink GetOrCreateCustomSource(string name)
    {
        if (customProgramSources.TryGetValue(name, out var existing)) return existing;
        TextLink link = plcGroup.Groups[CommandProgramsGroupName].AddText(name, name, "END_FUNCTION(Result := 0);\n",
            $"Source for custom command program '{name}'.");
        link.IconKey = "command";
        link.Changed += PlcSourceLinkChanged;
        customProgramSources.Add(name, link);
        plcSourceLinks.Add(name, link);
        return link;
    }

    private void SynchronizeCustomCommandPrograms()
    {
        string[] names = ReadCustomProgramNames();
        if (names.SequenceEqual(activeCustomPrograms)) return;
        if (EditRestriction.Length != 0) throw new InvalidOperationException(EditRestriction);
        // Check the complete candidate before mutating any controller program.
        foreach (string name in names) GetOrCreateCustomSource(name);
        foreach (string name in activeCustomPrograms)
        {
            var program = controller.PLC.Programs[name];
            program.RuntimeStateChanged -= ProgramRuntimeStateChanged;
            plcDocumentsByLink.Remove(customProgramSources[name]);
            controller.PLC.Programs.Remove(name);
        }
        foreach (var source in customProgramSources.Values) source.View = AccessView.Hidden;
        foreach (string name in names)
        {
            var source = customProgramSources[name];
            source.View = AccessView.ReadWrite;
            PLCProgram program = controller.PLC.Programs.Add(name, source.Value, cyclic: false);
            plcDocumentsByLink[source] = program;
            program.RuntimeStateChanged += ProgramRuntimeStateChanged;
        }
        activeCustomPrograms = names;
        mainForm.gCodeInterpreter?.RefreshAuxiliaryCatalog();
    }

    // Scan identifiers outside PLC comments, including incomplete drafts. Do not
    // rewrite user sources or depend on successful compilation to detect references.
    private static IEnumerable<string> SourceIdentifiers(string source)
    {
        for (int i = 0; i < source.Length;)
        {
            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '/')
            { while (i < source.Length && source[i] != '\n' && source[i] != '\r') i++; continue; }
            if (i + 1 < source.Length && source[i] == '(' && source[i + 1] == '*')
            {
                i += 2; int depth = 1;
                while (i < source.Length && depth > 0)
                {
                    if (i + 1 < source.Length && source[i] == '(' && source[i + 1] == '*') { depth++; i += 2; }
                    else if (i + 1 < source.Length && source[i] == '*' && source[i + 1] == ')') { depth--; i += 2; }
                    else i++;
                }
                continue;
            }
            if (char.IsLetter(source[i]) || source[i] == '_')
            {
                int start = i++;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                yield return source[start..i];
            }
            else i++;
        }
    }
}

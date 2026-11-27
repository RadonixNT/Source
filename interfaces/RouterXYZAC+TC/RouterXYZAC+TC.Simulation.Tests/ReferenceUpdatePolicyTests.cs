using NT_Interpreter.Shared.Router;
using NT_Interpreter.Interpreters.RouterXYZACTC;

namespace RouterXYZACTC.Simulation.Tests;

public sealed class ReferenceUpdatePolicyTests
{
    private static RouterProgram Compile(string text)
    {
        var result = new RouterCompiler().Compile(text,
            new RouterMachineContext(default, 100, new RouterPosition[6]));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return result.Program!;
    }

    [Theory]
    [InlineData("G0 X1\nX2", 0, true, false)]
    [InlineData("G55\nG0 X1\nG55 X2", 2, false, false)]
    [InlineData("G0 X1\nG55 X2", 2, true, false)]
    [InlineData("G54 X1\nG55 X2", 3, false, false)]
    [InlineData("%\n(G59)\nG54 X1\nG55\n%\nG58 X2", 1, false, false)]
    [InlineData("G54 X1\nG53 G0 X0", 1, false, true)]
    public void CompilerRecordsEffectiveReferences(string text, int mask, bool initial, bool machine)
    {
        RouterProgram program = Compile(text);
        Assert.Equal(mask, program.ExplicitReferenceMask);
        Assert.Equal(initial, program.UsesInitialReference);
        Assert.Equal(machine, program.UsesMachineCoordinates);
    }

    [Fact]
    public void ImplicitReferenceFollowsSelectionAndRepeatedEditsWithoutRebuild()
    {
        var program = Compile("G0 X1\nY2");
        var origins = new RouterPosition[6];
        origins[0] = new(10, 20, 30);
        Assert.Equal(origins[0], GCodeInterpreter.GetReferenceTranslation(program, 0, 0, origins));
        origins[3] = new(-5, 8, 12);
        Assert.Equal(origins[3], GCodeInterpreter.GetReferenceTranslation(program, 0, 3, origins));
        origins[3] = new(90, 0, 1);
        Assert.Equal(origins[3], GCodeInterpreter.GetReferenceTranslation(program, 0, 3, origins));
    }

    [Fact]
    public void ExplicitSingleReferenceIgnoresSelectorAndUnusedEdits()
    {
        var program = Compile("G55 G0 X1\nY2");
        var origins = new RouterPosition[6];
        origins[1] = new(10, 20, 30);
        origins[4] = new(999, 999, 999);
        Assert.Equal(origins[1], GCodeInterpreter.GetReferenceTranslation(program, 0, 4, origins));
    }

    [Theory]
    [InlineData("G54 G0 X1\nG55 X2", 1)]
    [InlineData("G54 G0 X1\nG53 G0 X0", 0)]
    public void RelevantChangesRequireRebuildButUnusedRowsDoNot(string text, int changed)
    {
        var program = Compile(text);
        var origins = new RouterPosition[6];
        origins[5] = new(100, 200, 300);
        Assert.Equal(default(RouterPosition), GCodeInterpreter.GetReferenceTranslation(program, 0, 0, origins));
        origins[changed] = new(10, 20, 30);
        Assert.Null(GCodeInterpreter.GetReferenceTranslation(program, 0, 0, origins));
    }

    [Fact]
    public void SelectingAnotherInitialReferenceCanSplitPreviouslySingleReference()
    {
        var program = Compile("G0 X1\nG54 X2");
        Assert.Null(GCodeInterpreter.GetReferenceTranslation(program, 0, 1, new RouterPosition[6]));
    }
}

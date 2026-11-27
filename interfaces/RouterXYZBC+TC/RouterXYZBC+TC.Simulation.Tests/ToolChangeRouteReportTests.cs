using RouterXYZBCTC;

namespace RouterXYZBCTC.Simulation.Tests;

public sealed class ToolChangeRouteReportTests
{
    [Fact]
    public void CoalescedBlocksReportAnnotationsInOrderOnlyOnce()
    {
        string?[] descriptions = [null, "Stop requested", null, "Release started", "Clamp started"];
        int lastReported = 0;
        var messages = new List<string>();
        ToolChangerLinks.ReportReachedRouteBlocks(3, descriptions, ref lastReported, messages.Add);
        ToolChangerLinks.ReportReachedRouteBlocks(3, descriptions, ref lastReported, messages.Add);
        ToolChangerLinks.ReportReachedRouteBlocks(4, descriptions, ref lastReported, messages.Add);
        Assert.Equal([
            "ChangeTool: TP2 block 1: Stop requested",
            "ChangeTool: TP2 block 3: Release started",
            "ChangeTool: TP2 block 4: Clamp started"
        ], messages);
    }

    [Fact]
    public void InterruptedRouteDoesNotReportUnreachedAnnotations()
    {
        string?[] descriptions = [null, "Release started", null, "Clamp started"];
        int lastReported = 0;
        var messages = new List<string>();
        ToolChangerLinks.ReportReachedRouteBlocks(1, descriptions, ref lastReported, messages.Add);
        ToolChangerLinks.ReportReachedRouteBlocks(0, descriptions, ref lastReported, messages.Add);
        ToolChangerLinks.ReportReachedRouteBlocks(99, descriptions, ref lastReported, messages.Add);
        Assert.Equal("ChangeTool: TP2 block 1: Release started", Assert.Single(messages));
        Assert.Equal(1, lastReported);
    }

    [Fact]
    public void NewStageCanReuseBlockNumbersWithItsOwnDescriptions()
    {
        int lastReported = 0;
        var messages = new List<string>();
        ToolChangerLinks.ReportReachedRouteBlocks(1, [null, "Old tool route"], ref lastReported, messages.Add);
        lastReported = 0;
        ToolChangerLinks.ReportReachedRouteBlocks(1, [null, "New tool route"], ref lastReported, messages.Add);
        Assert.Equal(2, messages.Count);
        Assert.EndsWith("New tool route", messages[1]);
    }
}

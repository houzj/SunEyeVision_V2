using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class GraphCopyTests
{
    [Fact]
    public void Copy_preserves_admitted_configuration_and_does_not_mutate_source()
    {
        var flow = new SunEyeVision.Workflow.Workflow("flow", "flow");
        var original = RootCanvasFixture.Node("original");
        RootCanvasFixture.SetValue(original, 7);
        flow.Nodes.Add(original);
        var copy = WorkflowFragmentCopy.Prepare(flow, new[] { original.Id }, flow.Id,
            _ => null, (source, _) => RootCanvasFixture.Node("copy"));
        var cloned = Assert.Single(copy.Nodes);
        Assert.Equal(7, RootCanvasFixture.Value(cloned));
        RootCanvasFixture.SetValue(cloned, 9);
        Assert.Equal(7, RootCanvasFixture.Value(original));
        Assert.Single(flow.Nodes);
    }
}

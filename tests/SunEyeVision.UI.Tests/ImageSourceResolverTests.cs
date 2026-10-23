using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.UI.Services;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public class ImageSourceResolverTests
{
    [Fact]
    public void ParentInputRequiresUniqueCurrentCallSiteAndNeverReusesAnUnrelatedParent()
    {
        var parent = new SunEyeVision.Workflow.Workflow { Id = "parent" };
        var source = Node("source", true);
        var call = Node("call"); call.SubroutineId = "child";
        parent.Nodes.Add(source); parent.Nodes.Add(call); parent.Connections.Add(Edge("source", "call"));
        var none = new ImageSourceResolution(ImageSourceResolutionKind.None);
        Assert.Same(source, ImageSourceResolver.InheritParentInput(none, "child", new[] { parent.Id }, _ => parent).Source);
        var second = Node("other-call"); second.SubroutineId = "child"; parent.Nodes.Add(second);
        Assert.Equal(ImageSourceResolutionKind.Ambiguous,
            ImageSourceResolver.InheritParentInput(none, "child", new[] { parent.Id }, _ => parent).Kind);
        Assert.Null(ImageSourceResolver.InheritParentInput(none, "unrelated-child", new[] { parent.Id }, _ => parent).Source);
        var explicitInvalid = new ImageSourceResolution(ImageSourceResolutionKind.InvalidSelection);
        Assert.Equal(explicitInvalid, ImageSourceResolver.InheritParentInput(explicitInvalid, "child", new[] { parent.Id }, _ => parent));
    }
    private static WorkflowNodeBase Node(string id, bool source = false) => new(id, id, id, source ? "ImageLoad" : "Process");
    private static WorkflowConnection Edge(string from, string to) => new() { SourceNodeId = from, TargetNodeId = to };

    [Fact]
    public void MultipleInputsRequireExplicitBranchAndIgnoreConnectionOrder()
    {
        var a = Node("a", true); var b = Node("b", true); var middle = Node("middle"); var target = Node("target");
        var nodes = new[] { a, b, middle, target };
        var edges = new[] { Edge("b", "target"), Edge("a", "middle"), Edge("middle", "target") };
        Assert.Equal(ImageSourceResolutionKind.Ambiguous, ImageSourceResolver.Resolve("target", nodes, edges).Kind);
        target.SelectedDisplaySource = new ImageSourceReference { SourceNodeId = "middle" };
        Assert.Same(a, ImageSourceResolver.Resolve("target", nodes, edges).Source);
        Assert.Same(a, ImageSourceResolver.Resolve("target", nodes, edges.Reverse()).Source);
    }

    [Fact]
    public void RemovedOrUnrelatedExplicitSelectionDoesNotFallBackToAnotherInput()
    {
        var a = Node("a", true); var unrelated = Node("other", true); var target = Node("target");
        target.SelectedDisplaySource = new ImageSourceReference { SourceNodeId = "other" };
        var nodes = new[] { a, unrelated, target };
        Assert.Equal(ImageSourceResolutionKind.InvalidSelection,
            ImageSourceResolver.Resolve("target", nodes, new[] { Edge("a", "target") }).Kind);
    }

    [Fact]
    public void CyclesTerminateAndSourceNodeResolvesItself()
    {
        var a = Node("a"); var b = Node("b"); var source = Node("source", true);
        a.SelectedDisplaySource = new ImageSourceReference { SourceNodeId = "b" };
        b.SelectedDisplaySource = new ImageSourceReference { SourceNodeId = "a" };
        var nodes = new[] { a, b, source };
        var edges = new[] { Edge("a", "b"), Edge("b", "a") };
        Assert.Equal(ImageSourceResolutionKind.InvalidSelection, ImageSourceResolver.Resolve("a", nodes, edges).Kind);
        Assert.Same(source, ImageSourceResolver.Resolve("source", nodes, edges).Source);
        Assert.Equal(ImageSourceResolutionKind.None, ImageSourceResolver.Resolve("missing", nodes, edges).Kind);
    }
}

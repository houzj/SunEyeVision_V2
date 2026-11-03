using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.TestFixtures;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using Xunit;

namespace SunEyeVision.CanvasProjection.Tests;

public sealed class DocumentCanvasProjectionTests
{
    [Fact]
    public void Actual_session_changes_reconcile_projections_and_history_by_entity_identity()
    {
        // 中立测试组件只用于验证会话和投影，不代表正式工具载荷通过。
        var component = new ToolComponentRegistration(new ToolComponentMetadata("canvas-probe", "ResultWorkspaceProbe",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(WorkspaceProbeDefinition));
        var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot)
            .GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { new[] { component } })!;
        using var session = DocumentSession.CreateNew(directory);
        var workflow = session.GetWorkflows().Single();
        using var canvas = new DocumentCanvasProjection(session, workflow.Id);

        var first = canvas.CreateNode("ResultWorkspaceProbe", new CanvasPoint(100, 100));
        var second = canvas.CreateNode("ResultWorkspaceProbe", new CanvasPoint(300, 100));
        var firstView = Assert.Single(canvas.Nodes.Where(item => ReferenceEquals(item.Definition, first)));
        var secondView = Assert.Single(canvas.Nodes.Where(item => ReferenceEquals(item.Definition, second)));
        canvas.Connect(new ConnectionDefinition("edge", first.Id, second.Id, "RightPort", "LeftPort"));
        var edge = Assert.Single(canvas.Connections);
        canvas.Connect(new ConnectionDefinition("duplicate", first.Id, second.Id, "BottomPort", "TopPort"));
        Assert.Same(edge, Assert.Single(canvas.Connections));
        Assert.Equal("BottomPort", edge.SourcePort);
        Assert.True(canvas.Undo());
        Assert.Equal("RightPort", edge.SourcePort);
        var inserted = canvas.CreateNode("ResultWorkspaceProbe", new CanvasPoint(200, 100), edge);
        Assert.Equal(2, canvas.Connections.Count);
        Assert.True(canvas.Undo());
        Assert.DoesNotContain(canvas.Nodes, item => ReferenceEquals(item.Definition, inserted));
        Assert.Same(edge.Definition, Assert.Single(canvas.Connections).Definition);
        edge = Assert.Single(canvas.Connections);
        canvas.MoveNodes(new Dictionary<CanvasNodeProjection, CanvasPoint> { [firstView] = new(120, 140) });
        Assert.Same(firstView, canvas.Nodes.Single(item => ReferenceEquals(item.Definition, first)));
        Assert.Equal(new CanvasPoint(120, 140), first.Position);
        Assert.Same(edge, Assert.Single(canvas.Connections));
        Assert.True(canvas.Undo());
        Assert.Equal(new CanvasPoint(100, 100), first.Position);
        Assert.True(canvas.Redo());
        canvas.Delete(new[] { secondView }, Array.Empty<CanvasConnectionProjection>());
        Assert.Empty(canvas.Connections);
        Assert.True(canvas.Undo());
        Assert.Same(second, canvas.Nodes.Single(item => ReferenceEquals(item.Definition, second)).Definition);
        Assert.Same(edge.Definition, Assert.Single(canvas.Connections).Definition);
    }
}

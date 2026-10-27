using System.Windows;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shared;
using Xunit;
using Connection = SunEyeVision.Workflow.WorkflowConnection;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class VmPlacementTests
{
    [Fact] public void Guides_cover_left_right_center_and_use_inverse_zoom_width()
    {
        var result = VmPlacementCalculator.Calculate(new Rect(100, 170, 110, 30), new[] { new VmPlacementNode("a", new Rect(100, 100, 110, 30)) }, .5, null);
        Assert.Equal(3, result.Guides.Count); Assert.All(result.Guides, g => Assert.True(g.Vertical));
        Assert.Equal(new[] { 100d, 210d, 155d }, result.Guides.Select(g => g.Axis));
        Assert.All(result.Guides, g => { Assert.Equal(90d, g.Start); Assert.Equal(210d, g.End); });
        Assert.Equal(new Point(100, 170), result.Hot!.TopLeft);
    }
    [Fact] public void Variable_height_hot_region_and_retained_hysteresis_match_restored_formula()
    {
        var others = new[] { new VmPlacementNode("parent", new Rect(100, 100, 110, 40)) };
        var first = VmPlacementCalculator.Calculate(new Rect(100, 170, 110, 30), others, 1, null);
        Assert.Equal(new Point(100, 175), first.Hot!.TopLeft);
        var retained = VmPlacementCalculator.Calculate(new Rect(115, 185, 110, 30), others, 1, first.Hot);
        Assert.Equal(first.Hot, retained.Hot);
        Assert.Null(VmPlacementCalculator.Calculate(new Rect(125, 185, 110, 30), others, 1, first.Hot).Hot);
    }
    [Fact] public void Move_clears_incident_manual_path_and_undo_restores_saved_array()
    {
        var node = new WorkflowNode { Id = "a", Position = new Point(50, 50) };
        var path = new[] { new Point(50,65), new Point(50,150), new Point(250,150) };
        var connection = new Connection { SourceNodeId = "a", TargetNodeId = "b", ManualPathPoints = path };
        var command = new MoveNodesSnapshotCommand(new[] { (node, node.Position, new Point(100,100)) }, new[] { connection });
        command.Execute(null); Assert.Null(connection.ManualPathPoints);
        path[0] = new Point(999,999); command.Undo(); Assert.Equal(new Point(50,65), connection.ManualPathPoints![0]);
        command.Execute(null); Assert.Null(connection.ManualPathPoints);
    }
    [Fact] public void Insertion_preserves_ports_and_undo_restores_edge_position_and_binding_configuration()
    {
        var source = new WorkflowNode { Id = "source" };
        var target = new WorkflowNode { Id = "target", Parameters = new SunEyeVision.Plugin.SDK.Execution.Parameters.GenericToolParameters() };
        var inserted = new WorkflowNode { Id = "inserted", Position = new Point(100, 100) };
        target.Parameters.RebindingRequired["ImageSource"] = "before";
        var old = new Connection { Id = "original", SourceNodeId = source.Id, TargetNodeId = target.Id, SourcePort = "LeftPort", TargetPort = "RightPort", ManualPathPoints = new[] { new Point(1,1), new Point(2,2) } };
        var edges = new System.Collections.ObjectModel.ObservableCollection<Connection> { old };
        var history = new SunEyeVision.UI.Features.WorkflowUI.Commands.CommandManager(new(), edges);
        var configureCalls = 0;
        var command = new InsertNodeIntoConnectionCommand(edges, new[] { source, target, inserted }, old, inserted, new Point(200,200),
            () => { ++configureCalls; target.Parameters.RebindingRequired["ImageSource"] = "after"; }, () => { });
        history.Execute(command);
        Assert.Equal(2, edges.Count); Assert.All(edges, c => { Assert.Equal("LeftPort", c.SourcePort); Assert.Equal("RightPort", c.TargetPort); });
        Assert.Contains(edges, c => c.SourceNodeId == "source" && c.TargetNodeId == "inserted");
        Assert.Contains(edges, c => c.SourceNodeId == "inserted" && c.TargetNodeId == "target");
        Assert.Equal(new Point(200,200), inserted.Position); Assert.Equal("after", target.Parameters.RebindingRequired["ImageSource"]);
        history.Undo(); Assert.Same(old, Assert.Single(edges)); Assert.Equal(new Point(100,100), inserted.Position);
        Assert.Equal("before", target.Parameters.RebindingRequired["ImageSource"]); Assert.NotNull(old.ManualPathPoints);
        history.Redo(); Assert.Equal(2, edges.Count); Assert.Equal("after", target.Parameters.RebindingRequired["ImageSource"]); Assert.Equal(1, configureCalls);
    }
    [Fact] public void Invalid_insertion_cycle_or_own_endpoint_leaves_graph_unchanged()
    {
        var source = new WorkflowNode { Id = "a" }; var target = new WorkflowNode { Id = "b" }; var inserted = new WorkflowNode { Id = "c" };
        var old = new Connection { Id = "ab", SourceNodeId = "a", TargetNodeId = "b" };
        var back = new Connection { Id = "ca", SourceNodeId = "c", TargetNodeId = "a" };
        var edges = new System.Collections.ObjectModel.ObservableCollection<Connection> { old, back };
        Assert.Throws<InvalidOperationException>(() => new InsertNodeIntoConnectionCommand(edges, new[] { source,target,inserted }, old, inserted, new Point(200,200), () => { }, () => { }));
        Assert.Throws<InvalidOperationException>(() => new InsertNodeIntoConnectionCommand(edges, new[] { source,target,inserted }, old, source, new Point(200,200), () => { }, () => { }));
        Assert.Equal(new[] { old, back }, edges);
    }
    [Fact] public void Auto_link_drop_is_one_undo_redo_and_incomplete_transaction_rolls_back()
    {
        var nodes = new System.Collections.ObjectModel.ObservableCollection<SunEyeVision.Workflow.WorkflowNodeBase>();
        var connections = new System.Collections.ObjectModel.ObservableCollection<Connection>();
        var history = new SunEyeVision.UI.Features.WorkflowUI.Commands.CommandManager(nodes, connections);
        var child = new WorkflowNode { Id = "child" };
        var link = new Connection { Id = "link", SourceNodeId = "parent", TargetNodeId = child.Id, SourcePort = "BottomPort", TargetPort = "TopPort" };
        using (var transaction = history.BeginTransaction())
        { history.Execute(new AddNodeCommand(nodes, child)); history.Execute(new AddConnectionCommand(connections, link)); transaction.Complete(); }
        Assert.Single(nodes); Assert.Single(connections); history.Undo(); Assert.Empty(nodes); Assert.Empty(connections); Assert.False(history.CanUndo);
        history.Redo(); Assert.Single(nodes); Assert.Single(connections);
        using (history.BeginTransaction()) { history.Execute(new AddNodeCommand(nodes, new WorkflowNode { Id = "cancelled" })); }
        Assert.Single(nodes); Assert.Single(connections);
    }
}
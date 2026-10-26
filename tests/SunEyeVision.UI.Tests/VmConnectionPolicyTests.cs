using System.Collections.ObjectModel;
using SunEyeVision.Workflow;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using Xunit;
namespace SunEyeVision.UI.Tests;
public sealed class VmConnectionPolicyTests
{
    private static WorkflowConnection Edge(string from, string to, string sourcePort = "BottomPort", string targetPort = "TopPort") =>
        new() { Id = Guid.NewGuid().ToString("N"), SourceNodeId = from, TargetNodeId = to, SourcePort = sourcePort, TargetPort = targetPort };
    public static IEnumerable<object[]> PortPairs() => new[] { "TopPort", "BottomPort", "LeftPort", "RightPort" }
        .SelectMany(source => new[] { "TopPort", "BottomPort", "LeftPort", "RightPort" }.Select(target => new object[] { source, target }));
    [Theory] [MemberData(nameof(PortPairs))]
    public void Same_directed_node_pair_is_rejected_for_every_alternative_port_pair(string sourcePort, string targetPort)
    {
        var original = Edge("a", "b"); var candidate = Edge("a", "b", sourcePort, targetPort);
        var edges = new ObservableCollection<WorkflowConnection> { original };
        Assert.NotNull(ExecutionConnectionPolicy.Rejection(edges, candidate));
        Assert.Throws<InvalidOperationException>(() => new AddConnectionCommand(edges, candidate).Execute(null));
        Assert.Same(original, Assert.Single(edges));
    }
    [Theory] [MemberData(nameof(PortPairs))]
    public void Native_repeat_connect_retargets_same_edge_and_undo_restores_ports_and_manual_path(string sourcePort, string targetPort)
    {
        var edge = Edge("a", "b"); var id = edge.Id;
        edge.ManualPathPoints = new[] { new System.Windows.Point(10,10), new System.Windows.Point(20,20) };
        var edges = new ObservableCollection<WorkflowConnection> { edge };
        var history = new SunEyeVision.UI.Features.WorkflowUI.Commands.CommandManager(new(), edges);
        history.Execute(new EditConnectionPortsCommand(edge, sourcePort, targetPort));
        Assert.Same(edge, Assert.Single(edges)); Assert.Equal(id, edge.Id); Assert.Equal(sourcePort, edge.SourcePort); Assert.Equal(targetPort, edge.TargetPort);
        Assert.Null(edge.ManualPathPoints);
        history.Undo(); Assert.Equal("BottomPort", edge.SourcePort); Assert.Equal("TopPort", edge.TargetPort); Assert.Equal(2, edge.ManualPathPoints!.Length);
        history.Redo(); Assert.Equal(sourcePort, edge.SourcePort); Assert.Equal(targetPort, edge.TargetPort); Assert.Same(edge, Assert.Single(edges));
    }
    [Fact] public void Reverse_direct_and_indirect_cycles_and_self_connection_are_rejected()
    {
        var edges = new[] { Edge("a", "b"), Edge("b", "c") };
        Assert.NotNull(ExecutionConnectionPolicy.Rejection(edges, Edge("b", "a")));
        Assert.NotNull(ExecutionConnectionPolicy.Rejection(edges, Edge("c", "a")));
        Assert.NotNull(ExecutionConnectionPolicy.Rejection(edges, Edge("a", "a")));
        Assert.Null(ExecutionConnectionPolicy.Rejection(edges, Edge("a", "c", "LeftPort", "TopPort")));
    }
    [Fact] public void Rejected_duplicate_does_not_replace_original_edge_or_its_manual_path()
    {
        var original = Edge("a", "b"); original.ManualPathPoints = new[] { new System.Windows.Point(10,10), new System.Windows.Point(20,20) };
        var edges = new ObservableCollection<WorkflowConnection> { original };
        var history = new SunEyeVision.UI.Features.WorkflowUI.Commands.CommandManager(new(), edges);
        Assert.Throws<InvalidOperationException>(() => history.Execute(new AddConnectionCommand(edges, Edge("a", "b", "RightPort", "LeftPort"))));
        Assert.False(history.CanUndo); Assert.Same(original, Assert.Single(edges)); Assert.Equal(2, original.ManualPathPoints.Length);
    }
    [Fact] public void Removing_then_recreating_pair_on_other_ports_is_allowed_and_undoable()
    {
        var edges = new ObservableCollection<WorkflowConnection>(); var history = new SunEyeVision.UI.Features.WorkflowUI.Commands.CommandManager(new(), edges);
        var first = Edge("a", "b"); history.Execute(new AddConnectionCommand(edges, first)); history.Undo();
        var second = Edge("a", "b", "LeftPort", "BottomPort"); history.Execute(new AddConnectionCommand(edges, second));
        Assert.Same(second, Assert.Single(edges)); history.Undo(); Assert.Empty(edges); history.Redo(); Assert.Same(second, Assert.Single(edges));
    }
}
using System.Collections.ObjectModel;
using SunEyeVision.Workflow;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using Xunit;
namespace SunEyeVision.UI.Tests;
public sealed class VmDeleteRepairTests
{
    private static WorkflowConnection Edge(string a,string b,string sp="BottomPort",string tp="TopPort") => new(){Id=Guid.NewGuid().ToString("N"),SourceNodeId=a,TargetNodeId=b,SourcePort=sp,TargetPort=tp};
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Consecutive_nodes_bridge_with_outer_ports_and_restore_identity_on_undo(bool reverse)
    {
        var a=new WorkflowNodeBase{Id="a"}; var b=new WorkflowNodeBase{Id="b"}; var c=new WorkflowNodeBase{Id="c"}; var d=new WorkflowNodeBase{Id="d"};
        var nodes=new ObservableCollection<WorkflowNodeBase>{a,b,c,d};
        var ab=Edge("a","b","RightPort"); var bc=Edge("b","c"); var cd=Edge("c","d",tp:"LeftPort");
        var edges=new ObservableCollection<WorkflowConnection>{ab,bc,cd};
        var command=new BatchDeleteNodesAndConnectionsCommand(nodes,edges,reverse?new(){c,b}:new(){b,c},new());
        command.Execute(null); var bridge=Assert.Single(edges); Assert.Equal("a",bridge.SourceNodeId); Assert.Equal("d",bridge.TargetNodeId); Assert.Equal("RightPort",bridge.SourcePort); Assert.Equal("LeftPort",bridge.TargetPort);
        command.Undo(); Assert.Equal(new[]{a,b,c,d},nodes); Assert.Equal(new[]{ab,bc,cd},edges);
        command.Execute(null); Assert.Same(bridge,Assert.Single(edges));
    }
    [Fact]
    public void Existing_pair_is_retargeted_without_duplication_and_undo_restores_manual_geometry()
    {
        var a=new WorkflowNodeBase{Id="a"};var b=new WorkflowNodeBase{Id="b"};var c=new WorkflowNodeBase{Id="c"};
        var nodes=new ObservableCollection<WorkflowNodeBase>{a,b,c};var ac=Edge("a","c");ac.ManualPathPoints=new[]{new System.Windows.Point(1,2),new System.Windows.Point(3,4)};
        var edges=new ObservableCollection<WorkflowConnection>{Edge("a","b","LeftPort"),Edge("b","c",tp:"RightPort"),ac};
        var command=new BatchDeleteNodesAndConnectionsCommand(nodes,edges,new(){b},new()); command.Execute(null);
        Assert.Same(ac,Assert.Single(edges));Assert.Equal("LeftPort",ac.SourcePort);Assert.Equal("RightPort",ac.TargetPort);Assert.Null(ac.ManualPathPoints);
        command.Undo();Assert.Equal("BottomPort",ac.SourcePort);Assert.Equal(2,ac.ManualPathPoints!.Length);
    }
    [Fact]
    public void Deleting_only_an_edge_does_not_create_a_bridge()
    {
        var nodes=new ObservableCollection<WorkflowNodeBase>();var ab=Edge("a","b");var bc=Edge("b","c");var edges=new ObservableCollection<WorkflowConnection>{ab,bc};
        var command=new BatchDeleteNodesAndConnectionsCommand(nodes,edges,new(),new(){ab});command.Execute(null);Assert.Same(bc,Assert.Single(edges));command.Undo();Assert.Equal(new[]{ab,bc},edges);
    }
    [Fact]
    public void Branches_bridge_every_predecessor_to_every_successor()
    {
        var b=new WorkflowNodeBase{Id="b"};var nodes=new ObservableCollection<WorkflowNodeBase>{b};
        var edges=new ObservableCollection<WorkflowConnection>{Edge("a","b"),Edge("x","b"),Edge("b","c"),Edge("b","d")};
        var command=new BatchDeleteNodesAndConnectionsCommand(nodes,edges,new(){b},new());command.Execute(null);
        Assert.Equal(4,edges.Count);Assert.Equal(4,edges.Select(e=>(e.SourceNodeId,e.TargetNodeId)).Distinct().Count());Assert.DoesNotContain(edges,e=>e.SourceNodeId=="b"||e.TargetNodeId=="b");
    }
}
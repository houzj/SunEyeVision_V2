using System.Collections.ObjectModel;
using WorkflowNodeBase=SunEyeVision.Workflow.WorkflowNodeBase;
using System.Text.Json;
using System.Windows;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.UI.Services.Connection;
using SunEyeVision.UI.Services.PathCalculators;
using SunEyeVision.UI.Services.Rendering;
using SunEyeVision.UI.Shared;
using Xunit;
using Connection=SunEyeVision.Workflow.WorkflowConnection;
namespace SunEyeVision.UI.Tests;
public sealed class VmWireEditingTests
{
    private static Point[] Path()=>new[]{new Point(210,115),new Point(260,115),new Point(260,315),new Point(400,315)};
    [Theory]
    [InlineData(357.3,281.8,355,280)]
    [InlineData(355,280,355,280)]
    public void Drop_preview_and_creation_use_the_same_five_unit_grid(double x,double y,double expectedX,double expectedY)
    {
        Assert.Equal(new Point(expectedX,expectedY),SunEyeVision.UI.Services.Interaction.WorkflowDragDropHandler.SnapVmPoint(new Point(x,y)));
    }    [Fact]
    public void Manual_path_roundtrips_but_preview_and_render_cache_do_not()
    {
        var connection=new Connection{Id="wire",SourceNodeId="a",TargetNodeId="b",SourcePort="RightPort",TargetPort="LeftPort",ManualPathPoints=Path(),PreviewPathPoints=new[]{new Point(1,1),new Point(2,2)}};
        var json=JsonSerializer.Serialize(connection);
        var reopened=JsonSerializer.Deserialize<Connection>(json)!;
        Assert.Equal(Path(),reopened.ManualPathPoints);Assert.Null(reopened.PreviewPathPoints);Assert.Empty(reopened.PathPoints);
        Assert.DoesNotContain("PreviewPathPoints",json);Assert.DoesNotContain("PathUpdateCounter",json);
    }
    [Fact]
    public void Edit_command_undo_redo_preserves_automatic_and_manual_states()
    {
        var connection=new Connection();var after=Path();int refreshes=0;
        var history=new CommandManager(new(),new());
        history.Execute(new EditConnectionPathCommand(connection,null,after,()=>refreshes++));
        after[1]=new Point(999,999);Assert.Equal(Path(),connection.ManualPathPoints);
        history.Undo();Assert.Null(connection.ManualPathPoints);
        history.Redo();Assert.Equal(Path(),connection.ManualPathPoints);Assert.Equal(3,refreshes);
    }
    [Fact]
    public void Workflow_copy_retains_independent_manual_path()
    {
        var workflow=new SunEyeVision.Workflow.Workflow("w","test");
        workflow.Connections.Add(new Connection{ManualPathPoints=Path()});
        var copy=workflow.Clone();Assert.Equal(Path(),copy.Connections[0].ManualPathPoints);
        copy.Connections[0].ManualPathPoints![1]=new Point(1,1);Assert.Equal(Path(),workflow.Connections[0].ManualPathPoints);
    }
    [Fact]
    public void Recovered_segment_drag_snaps_and_preserves_endpoints()
    {
        var points=Path();var drag=new RecoveredConnectionDrag(points,1,new Rect(100,100,110,30),new Rect(400,300,110,30));
        drag.Move(new Point(282,210));
        Assert.Equal(points[0],drag.PathPoints[0]);Assert.Equal(points[^1],drag.PathPoints[^1]);
        Assert.NotEqual(points,drag.PathPoints.ToArray());
        Assert.Contains(drag.PathPoints,p=>p.X==280);
        Assert.Equal(Path(),points);
    }
    [Theory]
    [InlineData("LeftPort",20,30,20,15)]
    [InlineData("RightPort",20,30,0,15)]
    [InlineData("TopPort",30,20,15,20)]
    [InlineData("BottomPort",30,20,15,0)]
    public void Port_uses_edge_anchor_and_highlight_preserves_hit_area(string tag,double w,double h,double x,double y)
    {
        OnSta(()=>{var port=new VmExecutionPort{Tag=tag,Width=w,Height=h};port.Measure(new Size(w,h));port.Arrange(new Rect(0,0,w,h));
            Assert.Equal(new Point(x,y),port.Anchor);port.IsHighlighted=true;port.UpdateLayout();
            Assert.Equal(new Point(x,y),port.Anchor);Assert.Equal(w,port.Width);Assert.Equal(h,port.Height);});
    }
    [Fact]
    public void Manual_path_reattaches_to_moved_node_without_mutating_saved_points()
    {
        OnSta(()=>{
            var source=new WorkflowNode{Id="a",Position=new Point(155,115)};var target=new WorkflowNode{Id="b",Position=new Point(455,315)};
            var connection=new Connection{Id="w",SourceNodeId="a",TargetNodeId="b",SourcePort="RightPort",TargetPort="LeftPort",ManualPathPoints=Path()};
            var cache=new ConnectionPathCache(new ObservableCollection<WorkflowNodeBase>{source,target});cache.GetPath(connection);
            source.Position=new Point(155,140);cache.MarkDirty(connection);cache.GetPath(connection);
            Assert.Equal(source.RightPortPosition,connection.PathPoints[0]);Assert.Equal(target.LeftPortPosition,connection.PathPoints[^1]);Assert.Equal(Path(),connection.ManualPathPoints);
        });
    }
    private static void OnSta(Action action)
    {
        Exception? error=null;var thread=new Thread(()=>{try{action();}catch(Exception e){error=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(error!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
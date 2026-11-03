using System.Windows;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Services.PathCalculators;
using SunEyeVision.UI.Services.Rendering;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class CanvasGeometrySessionTests
{
 [Theory] [InlineData(357.3,281.8,355,280)] [InlineData(355,280,355,280)]
 public void Drop_preview_and_creation_share_the_five_unit_grid(double x,double y,double expectedX,double expectedY)
  => Assert.Equal(new Point(expectedX,expectedY),WorkflowDragDropHandler.SnapVmPoint(new Point(x,y)));
 [Fact] public void Placement_guides_and_hot_region_respect_variable_height_and_hysteresis()
 {
  var aligned=VmPlacementCalculator.Calculate(new Rect(100,170,110,30),new[]{new VmPlacementNode("a",new Rect(100,100,110,30))},.5,null);
  Assert.Equal(new[]{100d,210d,155d},aligned.Guides.Select(g=>g.Axis));Assert.All(aligned.Guides,g=>Assert.True(g.Vertical));
  var nodes=new[]{new VmPlacementNode("parent",new Rect(100,100,110,40))};var first=VmPlacementCalculator.Calculate(new Rect(100,170,110,30),nodes,1,null);
  Assert.Equal(new Point(100,175),first.Hot!.TopLeft);Assert.Equal(first.Hot,VmPlacementCalculator.Calculate(new Rect(115,185,110,30),nodes,1,first.Hot).Hot);
  Assert.Null(VmPlacementCalculator.Calculate(new Rect(125,185,110,30),nodes,1,first.Hot).Hot);
 }
 [Fact] public void Segment_drag_preserves_endpoints_and_does_not_mutate_saved_path()
 {
  var original=new[]{new Point(210,115),new Point(260,115),new Point(260,315),new Point(400,315)};var drag=new RecoveredConnectionDrag(original,1,new Rect(100,100,110,30),new Rect(400,300,110,30));
  drag.Move(new Point(282,210));Assert.Equal(original[0],drag.PathPoints[0]);Assert.Equal(original[^1],drag.PathPoints[^1]);Assert.Contains(drag.PathPoints,p=>p.X==280);Assert.Equal(new Point(260,115),original[1]);
 }
 [Theory] [InlineData("LeftPort",20,30,20,15)] [InlineData("RightPort",20,30,0,15)] [InlineData("TopPort",30,20,15,20)] [InlineData("BottomPort",30,20,15,0)]
 public void Port_highlight_preserves_anchor_and_hit_area(string tag,double w,double h,double x,double y)=>UiRuntime.Run(()=>
 {
  var port=new VmExecutionPort{Tag=tag,Width=w,Height=h};port.Measure(new Size(w,h));port.Arrange(new Rect(0,0,w,h));Assert.Equal(new Point(x,y),port.Anchor);port.IsHighlighted=true;port.UpdateLayout();Assert.Equal(new Point(x,y),port.Anchor);Assert.Equal(w,port.Width);Assert.Equal(h,port.Height);
 });
}

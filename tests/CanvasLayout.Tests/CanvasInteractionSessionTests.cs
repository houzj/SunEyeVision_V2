using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class CanvasInteractionSessionTests
{
 [Fact] public void Actual_placement_feedback_respects_occupied_bottom_port_and_clears()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var parent=f.Add();var child=f.Add("BlobAnalysis",350,290);var edge=f.Connect(parent,child,"BottomPort","TopPort");
  var parentRect=f.Projection(parent).NodeRect;
  f.View.UpdateVmPlacementFeedback(new Point(parentRect.X+55,parentRect.Y+85),30,Array.Empty<string>());
  Assert.NotNull(UiRuntime.Field<object>(f.View,"_vmNodeHotRegion"));Assert.Null(f.View.VmAvailableHotPredecessor());
  f.Canvas.Delete(Array.Empty<CanvasNodeProjection>(),new[]{edge});UiRuntime.Pump();Assert.Equal(parent.Id,f.View.VmAvailableHotPredecessor());
  f.View.ClearVmPlacementFeedback();Assert.Null(UiRuntime.Field<object>(f.View,"_vmNodeHotRegion"));
 });
 [Fact] public void Actual_wire_hit_area_does_not_start_blank_drag_and_right_click_preserves_node_selection()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var first=f.Add();var second=f.Add("BlobAnalysis",350,290);var edge=f.Connect(first,second,"RightPort","LeftPort");
  var selected=f.Projection(first);selected.IsSelected=true;f.Host.UpdateLayout();UiRuntime.Pump();
  var hit=UiRuntime.Visuals<System.Windows.Shapes.Path>(f.View).First(p=>ReferenceEquals(p.DataContext,edge)&&p.StrokeThickness==2&&p.Stroke is SolidColorBrush b&&b.Color.A==0);
  hit.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});
  Assert.False(UiRuntime.Field<bool>(f.View,"_isPanning"));Assert.False(UiRuntime.Field<bool>(f.View,"_isBoxSelecting"));
  UiRuntime.Call(f.View,"ConnectionHitArea_MouseRightButtonDown",hit,new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Right){RoutedEvent=UIElement.MouseRightButtonDownEvent});
  Assert.True(selected.IsSelected);Assert.True(edge.IsSelected);Assert.Equal(Key.Delete,Assert.IsType<KeyBinding>(Assert.Single(hit.InputBindings.Cast<InputBinding>())).Key);
 });
 [Fact] public void Blank_drag_can_be_cancelled_and_wire_handle_is_not_blank()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture(); f.Add(); f.Host.UpdateLayout(); UiRuntime.Pump();
  var canvas=(System.Windows.Controls.Canvas)f.View.FindName("WorkflowCanvas");
  var handle=new Rectangle { Width=6, Height=6 }; canvas.Children.Add(handle);
  UiRuntime.Field<List<Rectangle>>(f.View,"_vmWireHandles").Add(handle);
  handle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});
  Assert.False(UiRuntime.Field<bool>(f.View,"_isBoxSelecting"));
  canvas.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});
  Assert.True(UiRuntime.Field<bool>(f.View,"_isBoxSelecting"));
  UiRuntime.Call(f.View,"CancelVmCanvasGesture"); Assert.False(UiRuntime.Field<bool>(f.View,"_isBoxSelecting"));
 });
 [Fact] public void Insertion_hover_uses_actual_rendered_edge_and_restores_stroke()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture(); var a=f.Add(); var b=f.Add("BlobAnalysis",350,290); var edge=f.Connect(a,b,"BottomPort","TopPort");
  f.Host.UpdateLayout(); UiRuntime.Pump();
  var points=edge.PathPoints.Select(p=>new Point(p.X,p.Y)).ToArray();
  var pair=Enumerable.Range(0,points.Length-1).Select(i=>(A:points[i],B:points[i+1])).OrderByDescending(p=>(p.B-p.A).Length).First();
  var pointer=pair.A+(pair.B-pair.A)*.5;
  Assert.Same(edge,UiRuntime.Call(f.View,"FindVmInsertionWire",pointer,Array.Empty<string>()));
  UiRuntime.Call(f.View,"UpdateVmInsertionWire",pointer,Array.Empty<string>());
  var visual=UiRuntime.Field<System.Windows.Shapes.Path>(f.View,"_vmInsertionView"); Assert.Equal(10d,visual.StrokeThickness);
  UiRuntime.Call(f.View,"ClearVmInsertionWire"); Assert.Equal(2d,visual.StrokeThickness);
  Assert.Null(UiRuntime.Call(f.View,"FindVmInsertionWire",pointer,new[]{a.Id}));
 });
}

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
 [Fact] public void Running_tab_rejects_canvas_gesture_until_stopped()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture(); f.Add(); f.Host.UpdateLayout(); UiRuntime.Pump();
  var property=f.View.GetType().GetProperty("CanEditVmCanvas",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
  Assert.True((bool)property.GetValue(f.View)!); f.Tab.IsRunning=true;
  Assert.False((bool)property.GetValue(f.View)!);
  var canvas=(System.Windows.Controls.Canvas)f.View.FindName("WorkflowCanvas");
  canvas.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});
  Assert.False(UiRuntime.Field<bool>(f.View,"_isBoxSelecting"));
  f.Tab.IsRunning=false; Assert.True((bool)property.GetValue(f.View)!);
 });
 [Fact] public void Auto_connect_is_one_undoable_transaction_and_failed_creation_preserves_graph()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture(); var first=f.Add(); var before=f.Session.HistoryEntries.Count;
  var second=f.Canvas.CreateNode("BlobAnalysis",new(300,300),predecessor:f.Projection(first)); UiRuntime.Pump();
  var edge=Assert.Single(f.Canvas.Connections); Assert.Equal(first.Id,edge.SourceNodeId); Assert.Equal(second.Id,edge.TargetNodeId);
  Assert.Equal(before+1,f.Session.HistoryEntries.Count);
  Assert.True(f.Canvas.Undo()); UiRuntime.Pump(); Assert.Single(f.Canvas.Nodes); Assert.Empty(f.Canvas.Connections);
  Assert.True(f.Canvas.Redo()); UiRuntime.Pump(); Assert.Equal(2,f.Canvas.Nodes.Count); Assert.Single(f.Canvas.Connections);
  var count=f.Session.HistoryEntries.Count;
  Assert.ThrowsAny<Exception>(()=>f.Canvas.CreateNode("unadmitted-tool",new(500,300),predecessor:f.Projection(first)));
  Assert.Equal(2,f.Canvas.Nodes.Count); Assert.Single(f.Canvas.Connections); Assert.Equal(count,f.Session.HistoryEntries.Count);
 });
 [Fact] public void Clipboard_repeated_paste_selects_new_nodes_and_session_switch_retires_fragment()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.Projection(node).IsSelected=true;
  Assert.True(f.Main.CopyCanvasSelection());Assert.True(f.Main.CanPasteCanvasSelection);
  Assert.True(f.Main.PasteCanvasSelection());UiRuntime.Pump();var first=Assert.Single(f.Canvas.Nodes.Where(item=>item.IsSelected));
  Assert.Equal(node.PositionX+20,first.PositionX);Assert.Equal(node.PositionY+20,first.PositionY);
  Assert.True(f.Main.PasteCanvasSelection());UiRuntime.Pump();var second=Assert.Single(f.Canvas.Nodes.Where(item=>item.IsSelected));
  Assert.Equal(node.PositionX+40,second.PositionX);Assert.Equal(node.PositionY+40,second.PositionY);
  Assert.True(f.Session.Undo());UiRuntime.Pump();Assert.Equal(2,f.Canvas.Nodes.Count);
  f.Save();var source=f.Session.LastWrittenSnapshot!.TargetPath;var path=System.IO.Path.Combine(f.DirectoryPath,"other.solution");System.IO.File.Copy(source,path);
  var opened=(Task<bool>)UiRuntime.Call(f.Main,"OpenExternalSolutionAsync",path,false)!;UiRuntime.Await(opened);
  Assert.True(opened.Result);Assert.False(f.Main.CanPasteCanvasSelection);
 });
 [Fact] public void Control_drag_copies_once_and_escape_cancels_preview()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var original=f.Add();var projection=f.Projection(original);projection.IsSelected=true;
  var start=new Point(original.PositionX,original.PositionY);UiRuntime.Call(f.View,"ArmVmNodeMove",projection,start);
  var move=UiRuntime.Field<object>(f.View,"_vmNodeMove")!;move.GetType().GetProperty("Copy")!.SetValue(move,true);
  UiRuntime.Call(f.View,"PreviewVmNodeMove",move,new Point(start.X+50,start.Y+40));UiRuntime.Call(f.View,"CancelVmNodeMove");
  Assert.Single(f.Canvas.Nodes);Assert.Equal(start.X,original.PositionX);
  UiRuntime.Call(f.View,"ArmVmNodeMove",projection,start);move=UiRuntime.Field<object>(f.View,"_vmNodeMove")!;move.GetType().GetProperty("Copy")!.SetValue(move,true);
  var token=(string)move.GetType().GetProperty("Token")!.GetValue(move)!;
  var format=(string)typeof(WorkflowCanvasControl).GetField("VmNodeDragFormat",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetRawConstantValue()!;
  var data=new DataObject();data.SetData(format,token);var canvas=(System.Windows.Controls.Canvas)f.View.FindName("WorkflowCanvas");
  var ctor=typeof(DragEventArgs).GetConstructors(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public).Single(item=>item.GetParameters().Length==5);
  var args=(DragEventArgs)ctor.Invoke(new object[]{data,DragDropKeyStates.ControlKey,DragDropEffects.Copy,canvas,new Point(start.X+50,start.Y+40)});
  args.RoutedEvent=DragDrop.PreviewDropEvent;var count=f.Session.HistoryEntries.Count;canvas.RaiseEvent(args);UiRuntime.Pump();
  Assert.Equal(DragDropEffects.Copy,args.Effects);Assert.Equal(2,f.Canvas.Nodes.Count);Assert.Equal(start.X,original.PositionX);Assert.Equal(count+1,f.Session.HistoryEntries.Count);
  Assert.True(f.Session.Undo());Assert.Single(f.Canvas.Nodes);UiRuntime.Call(f.View,"CancelVmNodeMove");
 });
 [Fact] public void Right_click_copy_targets_the_node_under_menu_instead_of_old_selection()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var first=f.Add();var second=f.Add("BlobAnalysis",350,290);f.Projection(first).IsSelected=true;
  f.Host.UpdateLayout();UiRuntime.Pump();
  var hit=UiRuntime.Visuals<System.Windows.Controls.Border>(f.View).Single(border=>border.Name=="HitAreaBorder" && ReferenceEquals(border.Tag,f.Projection(second)));
  var menu=hit.ContextMenu!;menu.PlacementTarget=hit;var copy=menu.Items.OfType<System.Windows.Controls.MenuItem>().Single(item=>Equals(item.Header,"复制"));
  copy.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
  Assert.False(f.Projection(first).IsSelected);Assert.True(f.Projection(second).IsSelected);Assert.True(f.Main.CanPasteCanvasSelection);
  Assert.True(f.Main.PasteCanvasSelection(new Point(600,400)));var pasted=Assert.Single(f.Canvas.Nodes.Where(item=>item.IsSelected));
  Assert.Equal("BlobAnalysis",pasted.ToolType);Assert.Equal(new Point(600,400),pasted.NodeRect.TopLeft);
 });
 [Fact] public void Selection_observer_failure_does_not_report_committed_paste_as_failed()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var source=f.Add();f.Projection(source).IsSelected=true;Assert.True(f.Main.CopyCanvasSelection());
  void Fault(object? sender,System.ComponentModel.PropertyChangedEventArgs e){if(e.PropertyName=="IsSelected")throw new InvalidOperationException("选择视图故障");}
  void Attach(object? sender,EventArgs e){foreach(var node in f.Canvas.Nodes.Where(item=>!ReferenceEquals(item.Definition,source)))node.PropertyChanged+=Fault;}
  f.Canvas.Changed+=Attach;try
  {
   var count=f.Session.HistoryEntries.Count;Assert.True(f.Main.PasteCanvasSelection());Assert.Equal(2,f.Session.GetNodes(f.Root.Id).Count);
   Assert.Equal(count+1,f.Session.HistoryEntries.Count);Assert.Contains("已粘贴",f.Main.StatusText);
  }
  finally{f.Canvas.Changed-=Attach;foreach(var node in f.Canvas.Nodes)node.PropertyChanged-=Fault;}
 });
 [Fact] public void Paste_after_deleting_source_uses_frozen_visual_anchor()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var source=f.Add();var selected=f.Projection(source);selected.IsSelected=true;
  f.Fields(source,("sequence.initial",new SunEyeVision.Plugin.Contracts.ConstantSetting(17L)));Assert.True(f.Main.CopyCanvasSelection());
  f.Canvas.Delete(new[]{selected},Array.Empty<CanvasConnectionProjection>());UiRuntime.Pump();Assert.Empty(f.Canvas.Nodes);
  Assert.True(f.Main.PasteCanvasSelection(new Point(500,300)));var pasted=Assert.Single(f.Canvas.Nodes);
  Assert.Equal(new Point(500,300),pasted.NodeRect.TopLeft);
  Assert.Equal(17L,Assert.IsType<SunEyeVision.Plugin.Contracts.ConstantSetting>(pasted.Definition.Configuration.Fields.Read("sequence.initial")).Value);
 });
}

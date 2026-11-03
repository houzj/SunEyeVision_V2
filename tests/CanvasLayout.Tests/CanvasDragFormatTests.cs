using System.Windows;
using System.Windows.Input;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Canvas UI")]
public sealed class CanvasDragFormatTests
{
 [Theory]
 [InlineData(true)]
 [InlineData(false)]
 public void Foreign_format_or_wrong_session_token_cannot_commit_a_real_canvas_drop(bool obsoleteFormat)=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var original=f.Add();var node=f.Projection(original);node.IsSelected=true;var start=new Point(original.PositionX,original.PositionY);
  UiRuntime.Call(f.View,"ArmCanvasNodeMove",node,start);var move=UiRuntime.Field<object>(f.View,"_canvasNodeMove")!;move.GetType().GetProperty("Copy")!.SetValue(move,true);var token=(string)move.GetType().GetProperty("Token")!.GetValue(move)!;
  var current=(string)typeof(WorkflowCanvasControl).GetField("CanvasNodeDragFormat",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetRawConstantValue()!;
  var data=new DataObject();data.SetData(obsoleteFormat?"SunEyeVision.VmNodeMove.Token":current,obsoleteFormat?token:Guid.NewGuid().ToString("N"));
  var canvas=(System.Windows.Controls.Canvas)f.View.FindName("WorkflowCanvas");var ctor=typeof(DragEventArgs).GetConstructors(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public).Single(item=>item.GetParameters().Length==5);
  var args=(DragEventArgs)ctor.Invoke(new object[]{data,DragDropKeyStates.ControlKey,DragDropEffects.Copy,canvas,new Point(start.X+50,start.Y+40)});args.RoutedEvent=DragDrop.PreviewDropEvent;var history=f.Session.HistoryEntries.Count;
  try{canvas.RaiseEvent(args);UiRuntime.Pump();Assert.Single(f.Canvas.Nodes);Assert.Equal(history,f.Session.HistoryEntries.Count);Assert.Equal(start,new Point(original.PositionX,original.PositionY));}
  finally{UiRuntime.Call(f.View,"CancelCanvasNodeMove");}
 });
}

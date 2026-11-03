using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.UI.Shell.Controls.Shell;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Canvas UI")]
public sealed class RootWorkflowOverviewTests
{
 [Theory]
 [InlineData(-20,-80,0,0)]
 [InlineData(102,108,100,110)]
 [InlineData(9999,9999,4860,4880)]
 public void Overview_drag_uses_original_five_pixel_snap_and_whole_card_page_bounds(double x,double y,double expectedX,double expectedY)
 {var point=RootOverviewGeometry.Position(x,y);Assert.Equal(expectedX,point.X);Assert.Equal(expectedY,point.Y);}
 [Fact]
 public void Copy_latches_at_start_and_requires_exact_control_at_drop()
 {
  Assert.True(RootOverviewGeometry.AcceptDrop(true,DragDropKeyStates.ControlKey|DragDropKeyStates.LeftMouseButton));
  Assert.False(RootOverviewGeometry.AcceptDrop(true,DragDropKeyStates.None));
  Assert.False(RootOverviewGeometry.AcceptDrop(true,DragDropKeyStates.ControlKey|DragDropKeyStates.ShiftKey));
  Assert.False(RootOverviewGeometry.AcceptDrop(false,DragDropKeyStates.ControlKey));
  Assert.True(RootOverviewGeometry.AcceptDrop(false,DragDropKeyStates.None));
 }
 [Fact]
 public void Actual_overview_validates_atomically_renders_source_cards_and_routes_requests_without_model_writes()
 {
  UiRuntime.Run(()=>
  {
    var control=new RootWorkflowOverviewControl();
    var entry=new RootWorkflowOverviewEntry("root","根流程",false,false,false,true,true,true,null,null,null,null);
    control.SetEntries(new[]{entry},"document",7);
    control.Measure(new Size(800,600));control.Arrange(new Rect(0,0,800,600));control.UpdateLayout();
    var surface=(Canvas)control.FindName("Surface");var card=Assert.IsType<Grid>(Assert.Single(surface.Children.Cast<UIElement>()));
    var payloadType=typeof(RootWorkflowOverviewControl).GetNestedType("DragPayload",System.Reflection.BindingFlags.NonPublic)!;
    var payload=Activator.CreateInstance(payloadType,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,new object[]{control,new object(),"document",7L,"root",new Point(),false},null);
    typeof(RootWorkflowOverviewControl).GetField("_payload",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(control,payload);
    control.SetEntries(new[]{entry with{RunCount=1}},"document",7);Assert.Same(card,Assert.Single(surface.Children.Cast<UIElement>()));
    control.CancelGesture();var refreshed=Assert.IsType<Grid>(Assert.Single(surface.Children.Cast<UIElement>()));Assert.NotSame(card,refreshed);card=refreshed;
    Assert.Equal(140,card.Width);Assert.Equal(120,card.Height);Assert.Equal(0,Canvas.GetLeft(card));
    Assert.Contains(Descendants<TextBlock>(card),t=>t.Text=="1 次\n--ms");
    Assert.Throws<ArgumentException>(()=>control.SetEntries(new[]{entry,entry},"bad",8));
    Assert.Same(card,Assert.Single(surface.Children.Cast<UIElement>()));
    Assert.Throws<ArgumentException>(()=>control.SetEntries(new[]{entry with{X=1,Y=null}},"bad",8));
    string? opened=null,configured=null;RootOverviewRunKind? run=null;
    control.OpenRequested+=(_,e)=>opened=e.RootId;control.ConfigureRequested+=(_,e)=>configured=e.RootId;control.RunRequested+=(_,e)=>run=e.RunKind;
    var buttons=Descendants<Button>(card).ToArray();Assert.Equal(4,buttons.Length);
    buttons.Single(b=>(string)b.ToolTip=="展开流程").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.Equal("root",opened);
    buttons.Single(b=>(string)b.ToolTip=="流程配置").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.Equal("root",configured);
    buttons.Single(b=>(string)b.ToolTip=="连续运行").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.Equal(RootOverviewRunKind.ToggleContinuous,run);
    control.SetEntries(new[]{entry with{RunCount=4,ElapsedMilliseconds=12.5,IsReady=true,CanConfigure=false,CanRun=false}},"document",8);
    card=Assert.IsType<Grid>(Assert.Single(surface.Children.Cast<UIElement>()));Assert.False(Descendants<Button>(card).Single(b=>(string)b.ToolTip=="流程配置").IsEnabled);
    Assert.Contains(Descendants<TextBlock>(card),t=>t.Text.StartsWith("4 次"));
    control.SetEntries(Array.Empty<RootWorkflowOverviewEntry>(),"document",9);
    Assert.Empty(surface.Children.Cast<UIElement>());Assert.Equal(Visibility.Visible,((TextBlock)control.FindName("EmptyHint")).Visibility);
    control.CancelGesture();
  });
 }
 static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject
 {
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
  {var child=VisualTreeHelper.GetChild(parent,i);if(child is T match)yield return match;foreach(var nested in Descendants<T>(child))yield return nested;}
 }
}

using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.Workflow;
using Xunit;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class WorkflowSearchTests
{
 private static Flow[] Definitions()
 {
  var root=new Flow("root","流程A");var child=new Flow("child","内部"){IsSubroutine=true};var other=new Flow("other","流程B");var orphan=new Flow("orphan","孤立"){IsSubroutine=true};
  root.Nodes.Add(new("owner","组","组","Subroutine"){SubroutineId=child.Id});child.Nodes.Add(new("same","Camera1","Camera1","ImageLoad"){GlobalIndex=1});child.Nodes.Add(new("two","Camera2","Camera2","ImageLoad"){GlobalIndex=2});other.Nodes.Add(new("same","Camera3","Camera3","ImageLoad"));orphan.Nodes.Add(new("orphan-node","Camera4","Camera4","ImageLoad"));return new[]{root,child,other,orphan};
 }
 [Fact]
 public void Catalog_matches_native_names_and_root_scope_and_preserves_nested_identity()
 {
  var flows=Definitions();var current=WorkflowSearchCatalog.Search(flows,"root","Camera");Assert.Equal(2,current.Count);Assert.Equal("流程A.组.Camera1",current[0].FullPath);Assert.Equal("owner",Assert.Single(current[0].Hops).Owner.Id);Assert.Equal("1 Camera1",current[0].Caption);
  Assert.Equal(3,WorkflowSearchCatalog.Search(flows,null,"Camera").Count);Assert.Empty(WorkflowSearchCatalog.Search(flows,null,"camera"));Assert.Empty(WorkflowSearchCatalog.Search(flows,null,""));
  Assert.True(WorkflowSearchCatalog.IsCurrent(current[0],flows));flows[1].Nodes[0]=new("same","Camera1","Camera1","ImageLoad");Assert.False(WorkflowSearchCatalog.IsCurrent(current[0],flows));
 }
 [Fact]
 public void Catalog_guards_cycles_and_rejects_replaced_or_reowned_Group_paths()
 {
  var flows=Definitions();var entry=WorkflowSearchCatalog.Search(flows,"root","Camera1").Single();flows[1].Nodes.Add(new("loop","循环组","循环组","Subroutine"){SubroutineId="child"});Assert.Single(WorkflowSearchCatalog.Search(flows,"root","循环"));
  flows[0].Nodes[0].SubroutineId="orphan";Assert.False(WorkflowSearchCatalog.IsCurrent(entry,flows));flows[0].Nodes[0].SubroutineId="child";var replaced=flows.ToArray();replaced[1]=new("child","新内部"){IsSubroutine=true};Assert.False(WorkflowSearchCatalog.IsCurrent(entry,replaced));
 }
 [Fact]
 public void Visible_Group_context_is_distinct_from_root_and_return_restores_root_identity()=>Sta(()=>
 {
  var service=new SunEyeVision.UI.Services.WorkflowContextService();var tab=new SunEyeVision.UI.Features.WorkflowUI.WorkflowTabViewModel{Id="root",Name="流程"};
  var root=SunEyeVision.UI.Shell.Windows.MainWindowViewModel.ResolveCanvasContext(service,tab)!;
  tab.NavigateToSubroutine("child","组",null,"owner");var child=SunEyeVision.UI.Shell.Windows.MainWindowViewModel.ResolveCanvasContext(service,tab)!;Assert.Equal("child",child.WorkflowId);Assert.NotSame(root,child);
  Assert.True(tab.NavigateBack());Assert.Same(root,SunEyeVision.UI.Shell.Windows.MainWindowViewModel.ResolveCanvasContext(service,tab));Assert.Null(SunEyeVision.UI.Shell.Windows.MainWindowViewModel.ResolveCanvasContext(service,null));service.Clear();
 });
 [Theory]
 [InlineData("Dark")]
 [InlineData("Light")]
 public void Actual_search_supports_scope_keyboard_commit_clear_and_unload(string theme)=>Sta(()=>
 {
  var flows=Definitions();var control=new WorkflowSearchControl{HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};control.QuerySources=(query,all)=>WorkflowSearchCatalog.Search(flows,all?null:"root",query);WorkflowSearchEntry? target=null;var navigationCount=0;control.Navigate=entry=>{navigationCount++;target=entry;return WorkflowSearchCatalog.IsCurrent(entry,flows);};
  var owner=new Window{Content=control,Width=700,Height=400,Left=-10000,ShowActivated=false};foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/"+theme+"/"+theme+"Theme.xaml"})owner.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
  try
  {
   owner.Show();var expand=(ToggleButton)control.FindName("Expand");var query=(TextBox)control.FindName("Query");var scope=(ComboBox)control.FindName("Scope");var popup=(Popup)control.FindName("ResultsPopup");var results=(ListBox)control.FindName("Results");
   Assert.Equal(Visibility.Collapsed,((FrameworkElement)control.FindName("SearchFields")).Visibility);expand.IsChecked=true;query.Text="Camera";Pump();Assert.True(popup.IsOpen);Assert.Equal(2,results.Items.Count);Assert.Equal(155,query.ActualWidth);Assert.Equal(30,((Button)control.FindName("ClearSearch")).ActualWidth);
   scope.SelectedIndex=0;Pump();Assert.Equal(3,results.Items.Count);
   var source=PresentationSource.FromVisual(owner)!;query.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Down){RoutedEvent=Keyboard.PreviewKeyDownEvent});Assert.Equal(1,results.SelectedIndex);
   var bar=new RenderTargetBitmap((int)Math.Ceiling(control.ActualWidth),40,96,96,PixelFormats.Pbgra32);bar.Render(control);Save(bar,"workflow-search-bar-"+theme+".png");var content=(FrameworkElement)popup.Child;content.UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(content);Save(image,"workflow-search-results-"+theme+".png");
   query.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});Assert.Same(flows[1].Nodes[1],target!.Node);Assert.False(popup.IsOpen);Assert.Equal("Camera2",query.Text);
   query.Text="Camera";Pump();query.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});query.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});Assert.Equal(1,navigationCount);Assert.False(popup.IsOpen);
   query.Text="camera";query.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});Pump();Assert.False(popup.IsOpen);

   query.Text="Camera";Pump();var row=(ListBoxItem)results.ItemContainerGenerator.ContainerFromIndex(0);row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseDownEvent,Source=row});Assert.Equal(2,navigationCount);Assert.Same(flows[1].Nodes[0],target!.Node);Assert.False(popup.IsOpen);
   query.Text="Camera";Pump();query.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));Assert.False(popup.IsOpen);
   query.Text="missing";Pump();Assert.Equal(Visibility.Visible,((FrameworkElement)control.FindName("NoResults")).Visibility);((Button)control.FindName("ClearSearch")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();Assert.False(popup.IsOpen);Assert.Empty(query.Text);
   query.Text="Camera";Pump();control.SetValue(WorkflowSearchControl.ScopeIdentityProperty,"new-container");Assert.False(popup.IsOpen);Assert.False(expand.IsChecked);Assert.Empty(results.Items);
   expand.IsChecked=true;Pump();control.SetValue(WorkflowSearchControl.DefinitionIdentityProperty,new object());Assert.False(popup.IsOpen);Assert.False(expand.IsChecked);
   expand.IsChecked=true;Pump();control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Assert.False(popup.IsOpen);Assert.Empty(results.Items);Assert.False(expand.IsChecked);
  }
  finally{owner.Close();}
 });
 private static void Save(BitmapSource image,string name){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name));encoder.Save(file);}
 private static void Pump(){var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);}
 private static void Sta(Action action){Exception? failure=null;var thread=new Thread(()=>{try{action();}catch(Exception error){failure=error;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(45)));if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();}
}

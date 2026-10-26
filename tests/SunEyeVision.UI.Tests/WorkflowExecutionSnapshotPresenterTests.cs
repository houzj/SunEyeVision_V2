using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class WorkflowExecutionSnapshotPresenterTests
{
 [Theory]
 [InlineData("Dark")]
 [InlineData("Light")]
 public void Actual_owned_detail_replaces_old_window_releases_bindings_and_owner_close_releases_current(string theme)=>Sta(()=>
 {
  var owner=new Window{Width=900,Height=650,Left=-10000,ShowActivated=false};
  foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/"+theme+"/"+theme+"Theme.xaml"})owner.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
  owner.Show();var presenter=new WorkflowExecutionSnapshotPresenter();
  try
  {
   var first=new WorkflowExecutionSnapshot("first","第一流程");Assert.True(presenter.Show(first,owner));var old=presenter.CurrentWindow!;Assert.True(old.IsVisible);Assert.Same(owner,old.Owner);
   var second=new WorkflowExecutionSnapshot("second","第二流程");second.Complete(new ExecutionResult{Status=ExecutionStatus.Success,Success=true,ExecutionTime=TimeSpan.FromMilliseconds(23.45)},id=>id);second.UpdateNodeResult("node","解析1",new(){NodeId="node",Success=true,Status=ExecutionStatus.Success,StartTime=new DateTime(2026,10,27),EndTime=new DateTime(2026,10,27).AddMilliseconds(12),Result=new CommunicationToolResults{ExecutionTimeMs=8.5}});
   Assert.True(presenter.Show(second,owner));var current=presenter.CurrentWindow!;current.Resources.MergedDictionaries.Add(owner.Resources);
   Assert.False(old.IsVisible);Assert.Null(old.DataContext);Assert.NotSame(old,current);Assert.Same(second,current.DataContext);Assert.Single(owner.OwnedWindows.Cast<Window>());
   current.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);current.UpdateLayout();var grid=Visual<DataGrid>(current).Single();Assert.True(grid.IsReadOnly);Assert.Single(grid.Items);Assert.Equal(4,grid.Columns.Count);
   var content=(FrameworkElement)current.Content;var image=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(current);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"workflow-time-details-"+theme+".png")))encoder.Save(file);
   owner.Close();Assert.Null(presenter.CurrentWindow);Assert.False(current.IsVisible);Assert.Null(current.DataContext);Assert.Equal(23.45,second.Elapsed.TotalMilliseconds);
  }
  finally{presenter.Close();owner.Close();}
 });
 [Fact]
 public void Null_request_closes_existing_and_cancelled_close_never_opens_a_second_detail()=>Sta(()=>
 {
  var owner=new Window{Width=300,Height=200,Left=-10000,ShowActivated=false};owner.Show();var presenter=new WorkflowExecutionSnapshotPresenter();
  try
  {
   Assert.True(presenter.Show(new("one","第一流程"),owner));var first=presenter.CurrentWindow!;System.ComponentModel.CancelEventHandler cancel=(_,e)=>e.Cancel=true;first.Closing+=cancel;
   Assert.False(presenter.Show(new("two","第二流程"),owner));Assert.Same(first,presenter.CurrentWindow);Assert.Single(owner.OwnedWindows.Cast<Window>());
   first.Closing-=cancel;Assert.False(presenter.Show(null,owner));Assert.Null(presenter.CurrentWindow);Assert.Empty(owner.OwnedWindows.Cast<Window>());
   Assert.True(presenter.Show(new("three","第三流程"),owner));presenter.CurrentWindow!.Close();Assert.Null(presenter.CurrentWindow);Assert.True(presenter.Close());
  }
  finally{presenter.Close();owner.Close();}
 });
 private static IEnumerable<T> Visual<T>(DependencyObject root) where T:DependencyObject
 {for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T found)yield return found;foreach(var value in Visual<T>(child))yield return value;}}
 private static void Sta(Action action)
 {Exception? failure=null;var thread=new Thread(()=>{try{action();}catch(Exception error){failure=error;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(45)));if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();}
}

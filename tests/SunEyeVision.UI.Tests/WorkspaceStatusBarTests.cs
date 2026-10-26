using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Shell.Controls.Shell;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class WorkspaceStatusBarTests
{
 [Theory]
 [InlineData("Dark")]
 [InlineData("Light")]
 public void Actual_footer_requests_discrete_zoom_and_reset_without_feedback_from_host_updates(string theme)=>Sta(()=>
 {
  var control=new WorkspaceStatusBarControl{Width=900,Height=28,StatusText="等待添加模块"};var window=new Window{Content=control,SizeToContent=SizeToContent.WidthAndHeight,Left=-10000,ShowActivated=false};
  foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/"+theme+"/"+theme+"Theme.xaml"})window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
  try
  {
   window.Show();window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);window.UpdateLayout();
   var slider=(Slider)control.FindName("ZoomSlider");var reset=(Button)control.FindName("ResetZoomButton");var requests=new List<double>();control.ScaleRequested+=value=>{requests.Add(value);control.Scale=value;};
   Assert.Equal(80,slider.ActualWidth);Assert.Equal(.1,slider.Minimum);Assert.Equal(2,slider.Maximum);Assert.True(slider.IsSnapToTickEnabled);
   control.Scale=.8;window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);Assert.Empty(requests);Assert.Equal(.8,slider.Value);
   slider.SetCurrentValue(Slider.ValueProperty,1.4);window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);Assert.Equal(new[]{1.4},requests);Assert.Equal(1.4,control.Scale);
   Assert.Contains(Visual<TextBlock>(reset),t=>t.Text.Contains("140"));
   reset.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);Assert.Equal(1d,control.Scale);Assert.Equal(1d,slider.Value);Assert.Equal(2,requests.Count);
   Assert.Equal("等待添加模块",((TextBlock)control.FindName("StatusTextBlock")).Text);
   var image=new RenderTargetBitmap(900,28,96,96,PixelFormats.Pbgra32);image.Render(control);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"workspace-footer-"+theme+".png"));encoder.Save(file);
  }
  finally{window.Close();}
 });
 [Fact]
 public void Shell_places_status_footer_below_canvas_and_minimap()=>Sta(()=>
 {
  var shell=new ShellWorkspaceControl();var footer=(WorkspaceStatusBarControl)shell.FindName("WorkspaceStatusBar");Assert.Equal(1,Grid.GetRow(footer));Assert.IsType<Grid>(footer.Parent);
 });
 private static IEnumerable<T> Visual<T>(DependencyObject root) where T:DependencyObject
 {for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T found)yield return found;foreach(var value in Visual<T>(child))yield return value;}}
 private static void Sta(Action action)
 {Exception? failure=null;var thread=new Thread(()=>{try{action();}catch(Exception error){failure=error;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(45)));if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();}
}

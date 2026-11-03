using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.UI.Tests;
using NSubstitute;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Vision.Hosting;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport;
using SunEyeVision.UI.Features.Camera;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Controls.Inputs;

internal static class Program
{
 [DllImport("user32.dll", CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
 [DllImport("user32.dll",SetLastError=true)] static extern bool SetThreadDesktop(IntPtr desktop);
 [STAThread] static int Main()
 {
  try {
   using var watchdog=new Timer(_=>{Console.Error.WriteLine("camera watchdog expired");Environment.Exit(3);},null,TimeSpan.FromSeconds(75),Timeout.InfiniteTimeSpan);
   int result=1;Exception? failure=null;
   var thread=new Thread(()=>{try{result=RunHidden();}catch(Exception e){failure=e;}});
   thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
   if(failure is not null)throw new Exception("隐藏桌面探针失败",failure);
   return result;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
 static int RunHidden()
 {
   var desktop=CreateDesktop("SunEyeCamera-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);
   if(desktop==IntPtr.Zero||!SetThreadDesktop(desktop))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"隐藏桌面不可用");
   return RunWpf();
 }
 static int RunWpf()
 {
   var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
   foreach(var path in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
    app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+path,UriKind.Relative)});
   var test=new CameraStreamOwnershipTests();
   Console.WriteLine("camera: borrowed stream stop/close");test.Preview_successful_stop_drains_stream_before_workflow_can_reuse_borrowed_session();
   Console.WriteLine("camera: late open close");test.Closing_during_inflight_open_waits_and_releases_only_the_session_it_opened();
   Console.WriteLine("camera: driver frame cleanup");test.Preview_null_conversion_from_driver_thread_is_marshaled_and_releases_frame();
   Console.WriteLine("camera: real frame presentation");FramePresentation();
   Console.WriteLine("camera: parameter bindings");ParameterBindings();
   Console.WriteLine("camera: pass");return 0;
 }
 public sealed class PanelContext
 {
  public IEnumerable<CameraParameterItemViewModel> SelectedParameters { get; init; } = Array.Empty<CameraParameterItemViewModel>();
 }
 static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject
 {
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
  {
   var child=VisualTreeHelper.GetChild(root,i);
   if(child is T match)yield return match;
   foreach(var nested in Children<T>(child))yield return nested;
  }
 }
 static void ParameterBindings()
 {
  var number=new CameraParameterItemViewModel(new CameraParameterDescriptor { Name="Gain", Kind=CameraParameterKind.Double, Minimum=0d, Maximum=10d, Increment=0.1d },1.2d);
  var boolean=new CameraParameterItemViewModel(new CameraParameterDescriptor { Name="Enabled", Kind=CameraParameterKind.Boolean },false);
  var panel=new CameraParameterPanel { DataContext=new PanelContext { SelectedParameters=new[]{number,boolean} } };
  var window=new Window { Width=700,Height=500,Content=panel,ShowActivated=false };
  try
  {
   window.Show();window.UpdateLayout();
   var numeric=Children<DoubleUpDown>(panel).Single(x=>ReferenceEquals(x.DataContext,number));
   var toggle=Children<ContentToggleSwitch>(panel).Single(x=>ReferenceEquals(x.DataContext,boolean));
   if(numeric.Value!=1.2 || numeric.Minimum!=0 || numeric.Maximum!=10 || numeric.Increment!=0.1 || toggle.IsChecked)
    throw new Exception("initial parameter values or bounds");
   numeric.SetCurrentValue(DoubleUpDown.ValueProperty,2.3d);
   toggle.SetCurrentValue(ContentToggleSwitch.IsCheckedProperty,true);
   if(Math.Abs(number.NumericValue-2.3)>0.001 || !boolean.BooleanValue)throw new Exception("parameter edit did not reach model");
   number.NumericValue=3.4;boolean.BooleanValue=false;
   if(numeric.Value!=3.4 || toggle.IsChecked)throw new Exception("parameter update did not reach original controls");
  }
  finally {window.Close();}
 }
 static void FramePresentation()
 {
  var runtime=new CameraRuntimeManager(Array.Empty<ICameraAdapterFactory>());
  var reference=new CameraDeviceRef("sample");
  var host=new CameraPreviewHost(runtime,reference);
  var image=Substitute.For<IVisionImage>();
  var bridge=Substitute.For<IVisionImageDisplayBridge>();
  byte[] pixels={1,2,3,255,4,5,6,255};
  var bitmap=BitmapSource.Create(2,1,96,96,PixelFormats.Bgra32,null,pixels,8);bitmap.Freeze();
  bridge.ToBitmapSource(image).Returns(bitmap);
  var previous=VisionImageConverterLocator.Current;VisionImageConverterLocator.Current=bridge;
  try {
   var frame=new CameraFrame(reference,1,DateTimeOffset.UtcNow,image);
   Pump(async()=>await host.OnFrameAsync(frame,CancellationToken.None));
   var view=(ImageViewBox)host.FindName("ImageDisplay");
   if(!frame.IsDisposed||view.ImageDataSource is not { Width:2, Height:1 } output||output.Buffer[0]!=1)
    throw new Exception("sample frame was not shown or released");
   Pump(async()=>await host.DisposeAsync());
   if(view.ImageDataSource is not null)throw new Exception("preview pixels retained after close");
   var late=new CameraFrame(reference,2,DateTimeOffset.UtcNow,Substitute.For<IVisionImage>());
   Pump(async()=>await host.OnFrameAsync(late,CancellationToken.None));
   if(!late.IsDisposed||view.ImageDataSource is not null)throw new Exception("late frame published after close");
  } finally {VisionImageConverterLocator.Current=previous;Pump(async()=>await runtime.DisposeAsync());}
 }
 static void Pump(Func<Task> action)
 {
  var frame=new DispatcherFrame();Exception? failure=null;
  _=Run();Dispatcher.PushFrame(frame);
  if(failure is not null)throw new Exception("camera pump failed",failure);
  async Task Run(){try{await action().WaitAsync(TimeSpan.FromSeconds(15));}catch(Exception error){failure=error;}finally{frame.Continue=false;}}
 }
}

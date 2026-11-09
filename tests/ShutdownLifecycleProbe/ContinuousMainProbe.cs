using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Infrastructure.Persistence.ApplicationData;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Shell.Windows;
internal static class ContinuousMainProbe
{
 [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)] private delegate uint NativeStart(IntPtr state);
 [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr CreateThread(IntPtr attributes,UIntPtr size,NativeStart start,IntPtr state,uint flags,out uint id);
 [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
 [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] private static extern bool SetThreadDesktop(IntPtr desktop);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] private static extern bool CloseDesktop(IntPtr desktop);
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] private static void RunActualApp(Action runWindow){Console.WriteLine("continuous native: actual App resources");var app=new ProbeApp{ShutdownMode=ShutdownMode.OnExplicitShutdown};foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});Console.WriteLine("continuous native: services");SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));runWindow();if(!app.Dispatcher.HasShutdownFinished){app.Dispatcher.InvokeShutdown();}}
 [System.Runtime.InteropServices.DllImport("ole32.dll")] private static extern int OleInitialize(IntPtr reserved);
 [System.Runtime.InteropServices.DllImport("ole32.dll")] private static extern void OleUninitialize();
 internal static int Run(Action? runWindow=null)
 {
  using var watchdog=new Timer(_=>{Console.Error.WriteLine("continuous main watchdog expired");Environment.Exit(3);},null,TimeSpan.FromSeconds(80),Timeout.InfiniteTimeSpan);
  Exception? failure=null;IntPtr desktop=IntPtr.Zero;
  NativeStart start=_=>{try{desktop=CreateDesktop("SunEyeVision-Continuous-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);if(desktop==IntPtr.Zero||!SetThreadDesktop(desktop))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));try{RunActualApp(runWindow??RunWindow);}finally{Console.WriteLine("continuous native: dispatcher drained");OleUninitialize();Console.WriteLine("continuous native: OLE released");}}catch(Exception error){Console.Error.WriteLine("native probe primary failure: "+error);failure=error;}return 0;};
  var thread=CreateThread(IntPtr.Zero,UIntPtr.Zero,start,IntPtr.Zero,0,out _);if(thread==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
  if(WaitForSingleObject(thread,75000)!=0)throw new TimeoutException("隐藏连续主窗未退出。");CloseHandle(thread);GC.KeepAlive(start);
  if(desktop!=IntPtr.Zero&&!CloseDesktop(desktop))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
  if(failure is not null)throw failure;return 0;
 }
 private static void Check(bool value,string message){if(!value)throw new Exception(message);}
 private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 private static void Until(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(20);while(!done()){if(DateTime.UtcNow>end)throw new TimeoutException("continuous actual window did not settle");Pump();Thread.Sleep(1);}}
 private static T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(instance)!;
 private static void RunWindow()
 {
  var app=(SunEyeVision.UI.App)System.Windows.Application.Current;
  var plugins=new PluginManager();plugins.LoadPluginsAsync().GetAwaiter().GetResult();Check(plugins.LastAdmissionError is null,plugins.LastAdmissionError??"");using var pin=plugins.PinDesktopHost();
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","_validation","ContinuousMain",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);
  var paths=ApplicationDataPaths.Open(Path.Combine(root,"Data"));ServiceInitializer.ConfigureDataPaths(paths);ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(paths,plugins.RuntimeDirectory!);
  typeof(SunEyeVision.UI.App).GetField("_dataPaths",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,paths);
 
  var session=ServiceInitializer.SolutionManager.CreateNew("实际连续主窗");var flow=session.GetWorkflows().Single();var graph=session.PrepareGraphEdit(flow.Id);var node=session.CreateNode(graph,"ImageLoad",new(150,120));session.CommitGraphEdit(graph,"建立真实图像源");
  var image=Path.Combine(root,"source.pgm");File.WriteAllBytes(image,System.Text.Encoding.ASCII.GetBytes("P5\n2 1\n255\n").Concat(new byte[]{21,45}).ToArray());var draft=session.Editing.Host.BeginEdit(flow.Id,node.Id);draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"local.paths",new ConstantSetting(new[]{image})},{"local.start",new ConstantSetting(0L)},{"local.auto-switch",new ConstantSetting(false)}});
  var settings=session.Editing.Host.BeginRootEdit(flow.Id);settings.WorkspaceSettings=settings.WorkspaceSettings with{RunIntervalMs=600};session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{draft},roots:new[]{settings}));
  session.SaveAs(Path.Combine(root,"main.solution"),Path.Combine(root,"recovery"),root,new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()),new System.Text.Json.JsonSerializerOptions(),scope=>new ComplexVariableValueCodec(scope));
  MainWindow main;try { main=new MainWindow(plugins.DesktopAdmission!){Left=-32000,Top=-32000,Width=1100,Height=700,ShowActivated=false,ShowInTaskbar=false,WindowState=WindowState.Normal}; } catch(Exception error) { Console.Error.WriteLine("continuous main construct: "+error);throw; }app.MainWindow=main;var vm=(MainWindowViewModel)main.DataContext;main.Show();Pump();
  var top=(ShellTopBarControl)main.FindName("TopBarHost");var button=(Button)top.FindName("ContinuousRunToggle");Check(button.IsEnabled&&button.Command!.CanExecute(null),"真实连续工具栏被错误禁用");button.Command.Execute(null);
  var handle=Field<DocumentContinuousRun>(vm,"_formalContinuousRun");var task=Field<Task>(vm,"_formalRunTask");var rounds=new List<ContinuousRoundSummary>();handle.RoundCompleted+=rounds.Add;
  Until(()=>handle.State.Phase==ContinuousPhase.Waiting);vm.PauseCommand.Execute(null);Check(vm.IsRunning&&vm.IsExecutionPaused&&!vm.SaveCurrentSolutionCommand.CanExecute(null),"暂停错误释放Busy/保存门禁");
  Console.WriteLine("continuous main: toolbar real frame and paused reservation");
  var round=handle.State.Round;var wait=Task.Delay(120);Until(()=>wait.IsCompleted);Check(handle.State.Round==round,"暂停期间多执行一轮");vm.PauseCommand.Execute(null);Until(()=>rounds.Count>=2&&handle.State.Phase==ContinuousPhase.Waiting);vm.PauseCommand.Execute(null);
  using(var reader=session.AcquirePublishedResult(flow.Id,node.Id))Check(reader?.Snapshot.IsSuccess==true,"真实主窗没有正式HALCON结果");Check(!session.HasUnsavedChanges,"连续运行污染保存状态");
  Console.WriteLine("continuous main: resume fresh round and real result reader");
  var cancel=true;var sawCancel=false;var sawStop=false;var closed=false;Exception? dialogFailure=null;main.Closed+=(_,_)=>closed=true;
  var dialogs=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
  dialogs.Tick+=(_,_)=>
  {
   foreach(var dialog in app.Windows.OfType<ThemeMessageDialog>().ToArray())
   {
    var text=((TextBlock)dialog.FindName("MessageText")).Text;
    Console.WriteLine("continuous dialog: "+text);
    if(!text.Contains("停止全部执行")){dialogFailure=new Exception("意外对话框："+text);typeof(ThemeMessageDialog).GetMethod("OnOkClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dialog,new object?[]{null,new RoutedEventArgs()});continue;}
    var method=cancel?"OnCancelClick":"OnOkClick";if(cancel)sawCancel=true;else sawStop=true;
    typeof(ThemeMessageDialog).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dialog,new object?[]{null,new RoutedEventArgs()});
   }
  };
  dialogs.Start();main.Close();Until(()=>sawCancel&&!Field<bool>(main,"_isClosingPromptInProgress"));Check(main.IsVisible&&vm.IsRunning&&vm.IsExecutionPaused,"取消关闭破坏实际连续会话");Console.WriteLine("continuous main: cancel close preserves actual paused run");
  cancel=false;main.Close();try{Until(()=>closed);}catch{Console.Error.WriteLine("close pending: "+vm.StatusText+" running="+vm.IsRunning+" task="+task.Status+" prompt="+Field<bool>(main,"_isClosingPromptInProgress"));throw;}dialogs.Stop();Check(sawStop&&task.IsCompleted&&!vm.IsRunning,"主窗提前关闭或执行未排空");if(dialogFailure is not null)throw dialogFailure;
  ServiceInitializer.DisposeCommunicationRuntimeAsync().AsTask().GetAwaiter().GetResult();ServiceInitializer.DisposeCameraRuntimeAsync().AsTask().GetAwaiter().GetResult();ServiceInitializer.SolutionManager.CloseCurrent(true);pin.Dispose();plugins.UnloadPluginsAsync().GetAwaiter().GetResult();
  Console.WriteLine("continuous main: actual Close awaits Stop/drain and releases consumers");
 }
}


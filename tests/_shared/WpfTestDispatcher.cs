using System.Windows;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
namespace SunEyeVision.TestShared;
/// <summary>One test-owned WPF dispatcher. Hidden desktop is an explicit collection opt-in.</summary>
internal static class WpfTestDispatcher
{
 private static bool _hiddenDesktopRequested,_shellResourcesLoaded;
 private static Exception? _dispatchFailure;
 private static IntPtr _ownedDesktop,_nativeThread;
 [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)] private delegate uint NativeThreadStart(IntPtr state);
 private static NativeThreadStart? _nativeStart;
 [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr CreateThread(IntPtr attributes,UIntPtr size,NativeThreadStart start,IntPtr state,uint flags,out uint id);
 [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)] private static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
 [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)] private static extern bool CloseHandle(IntPtr handle);
 [System.Runtime.InteropServices.DllImport("ole32.dll")] private static extern int OleInitialize(IntPtr reserved);
 [System.Runtime.InteropServices.DllImport("ole32.dll")] private static extern void OleUninitialize();
 private static Thread? _ownedThread;
 private static System.Windows.Application? _ownedApplication;
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] private static extern bool SetThreadDesktop(IntPtr desktop);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] private static extern bool CloseDesktop(IntPtr desktop);
 private static readonly Lazy<Dispatcher> Owned=new(()=>
 {
  Dispatcher? dispatcher=null;Exception? failure=null;using var ready=new ManualResetEventSlim();
  if(_hiddenDesktopRequested)
  {
   // A managed STA thread has OLE windows before its delegate starts (ERROR_BUSY).
   // Bind a fresh native thread first; only then initialize its STA/WPF owner.
   _nativeStart=_=>{OwnerThread(ready,value=>dispatcher=value,error=>failure=error);return 0;};
   _nativeThread=CreateThread(IntPtr.Zero,UIntPtr.Zero,_nativeStart,IntPtr.Zero,0,out _);
   if(_nativeThread==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
  }
  else
  {
   var thread=new Thread(()=>OwnerThread(ready,value=>dispatcher=value,error=>failure=error)){IsBackground=true,Name="正式WPF测试宿主"};
   _ownedThread=thread;thread.SetApartmentState(ApartmentState.STA);thread.Start();
  }
  if(!ready.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("正式WPF测试Dispatcher未就绪。");
  if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
  return dispatcher!;
 });
 // Keep the native desktop entry separate: JIT/type initialization of WPF must happen after SetThreadDesktop.
 [MethodImpl(MethodImplOptions.NoInlining)]
 private static void OwnerThread(ManualResetEventSlim ready,Action<Dispatcher> accept,Action<Exception> failed)
 {
  try
  {
   if(_hiddenDesktopRequested)
   {
    _ownedDesktop=CreateDesktop("SunEyeVision-Tests-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);
    if(_ownedDesktop==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"CreateDesktop失败；不回退用户桌面。");
    if(!SetThreadDesktop(_ownedDesktop))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"SetThreadDesktop失败；不回退用户桌面。");
   }
   if(_hiddenDesktopRequested)
   {
    System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
    try{RunWpf(ready,accept,failed);}finally{OleUninitialize();}
   }
   else RunWpf(ready,accept,failed);
  }
  catch(Exception error){_dispatchFailure=error;failed(error);try{ready.Set();}catch(ObjectDisposedException){}}
 }
 [MethodImpl(MethodImplOptions.NoInlining)]
 private static void RunWpf(ManualResetEventSlim ready,Action<Dispatcher> accept,Action<Exception> failed)
 {
  try{var app=new System.Windows.Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};_ownedApplication=app;accept(app.Dispatcher);}
  catch(Exception error){failed(error);ready.Set();return;}
  ready.Set();Dispatcher.Run();
 }
 internal static void Invoke(Action action,bool loadShellResources=true)
 {
  if(_dispatchFailure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_dispatchFailure).Throw();
  var dispatcher=System.Windows.Application.Current?.Dispatcher??Owned.Value;
  void Run()
  {
   if(loadShellResources&&!_shellResourcesLoaded)
   {
    foreach(var path in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
     System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+path,UriKind.Relative)});
    _shellResourcesLoaded=true;
   }
   action();
  }
  if(dispatcher.CheckAccess())Run();else dispatcher.Invoke(Run);
 }
 internal static void RequireHiddenDesktop()
 {
  if(Owned.IsValueCreated||System.Windows.Application.Current is not null)throw new InvalidOperationException("须在WPF owner建立前选择隐藏desktop。");
  _hiddenDesktopRequested=true;
 }
 internal static void ShutdownOwned()
 {
  var app=_ownedApplication;
  if(_dispatchFailure is null&&app is not null&&ReferenceEquals(System.Windows.Application.Current,app)&&!app.Dispatcher.HasShutdownStarted)
  {
   var dispatcher=app.Dispatcher;dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
   dispatcher.Invoke(()=>{app.Shutdown();dispatcher.BeginInvokeShutdown(DispatcherPriority.ApplicationIdle);});
  }
  if(_ownedThread is {} thread&&thread!=Thread.CurrentThread&&!thread.Join(TimeSpan.FromSeconds(5)))throw new TimeoutException("WPF测试owner未正常退出。");
  if(_nativeThread!=IntPtr.Zero)
  {
   if(WaitForSingleObject(_nativeThread,5000)!=0)throw new TimeoutException("隐藏WPF owner未正常退出。");
   CloseHandle(_nativeThread);_nativeThread=IntPtr.Zero;_nativeStart=null;
  }
  if(_ownedDesktop!=IntPtr.Zero){if(!CloseDesktop(_ownedDesktop))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"隐藏测试desktop未释放。");_ownedDesktop=IntPtr.Zero;}
  if(_dispatchFailure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_dispatchFailure).Throw();
 }
}

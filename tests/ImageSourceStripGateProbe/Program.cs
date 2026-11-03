using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
internal static class Program
{
 [STAThread] public static int Main()
 {
  try
  {
   var app=new System.Windows.Application();var session=new ProbeSession{CanEdit=false,CanNavigate=true};
   var strip=new ImageSourceStrip{Session=session,IsRunning=true};
   var window=new Window{Content=strip,Width=600,Height=180,ShowInTaskbar=false,WindowStyle=WindowStyle.ToolWindow};
   window.Show();Pump();
   var list=(ListBox)strip.FindName("lst");var add=(Button)strip.FindName("BtnImageAdd");
   Check(strip.CanNavigate&&!strip.CanEdit&&list.IsEnabled&&!add.IsEnabled,"运行只读时保留键盘列表且禁用配置");
   var shadow=(Grid)strip.FindName("RunningShadow");Check(shadow.Visibility==Visibility.Visible&&shadow.IsHitTestVisible,"运行遮罩仍拦鼠标");
   var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(list),0,Key.Right){RoutedEvent=Keyboard.PreviewKeyDownEvent};
   list.RaiseEvent(key);list.SelectedIndex=1;Pump();
   Check(session.Requests.SequenceEqual(new[]{ImageSourceStripRequestKind.Activate}),"运行键盘只发运行导航意图");
   typeof(ImageSourceStrip).GetMethod("BtnImageClear_Click",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(strip,new object[]{strip,new RoutedEventArgs()});Pump();
   Check(session.Requests.Count==1,"程序事件也不能绕过编辑门禁");
   session.CanNavigate=false;session.Raise();Pump();
   Check(!list.IsEnabled,"两种权限关闭时禁用列表");
   session.CanEdit=true;session.Raise();Pump();Check(list.IsEnabled&&add.IsEnabled,"只有编辑权限仍可配置图像条目");
   key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(list),0,Key.Left){RoutedEvent=Keyboard.PreviewKeyDownEvent};list.RaiseEvent(key);
   Check(key.Handled,"无导航权限的方向键不改变选择");
   window.Close();strip.Session=null;Console.WriteLine("PASS 6: 实际WPF图像条门禁与运行遮罩；非自然OS输入或完整主窗验收");return 0;
  }
  catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
 private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
 private sealed class ProbeSession:IImageSourceStripSession
 {
  public bool CanEdit{get;set;}public bool CanNavigate{get;set;}public bool IsReadOnly=>!CanEdit;
  public IReadOnlyList<ImageSourceStripItem> Items{get;}=new[]{new ImageSourceStripItem("a","A",null,true),new ImageSourceStripItem("b","B",null,true)};
  public long Version=>1;public int DisplayIndex{get;private set;}public int ConfiguredStartIndex=>0;
  public bool IsSelectMode=>false;public bool AutoSwitchEnabled=>false;public bool IsImporting=>false;public string? Status=>null;
  public List<ImageSourceStripRequestKind> Requests{get;}=new();public event PropertyChangedEventHandler? PropertyChanged;
  public void Raise()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
  public Task RequestAsync(ImageSourceStripRequest request,CancellationToken cancellation){Requests.Add(request.Kind);if(request.Kind==ImageSourceStripRequestKind.Activate)DisplayIndex=Items.ToList().FindIndex(item=>item.Id==request.ItemId);return Task.CompletedTask;}
 }
}

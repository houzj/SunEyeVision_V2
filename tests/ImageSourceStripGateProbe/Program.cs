using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
   CheckToolbarLayout(strip,session,add);
   CheckDirectoryMenu(strip,list,session);
   window.Close();strip.Session=null;Console.WriteLine("PASS 8: 实际WPF图像条门禁、运行遮罩、工具条占位块 130/0 与增删图入口条件；非自然OS输入或完整主窗验收");return 0;
  }
  catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
 // 原件工具条右侧组件顺序固定：ComboBox → 占位块(130 或 0) → 展开按钮(36)。
// 占位块按「本模块自己是不是图像源」在 130 与 0 之间切换；不做这一步，自动切换与运行全部会整体偏左 130px。
private static void CheckToolbarLayout(ImageSourceStrip strip,ProbeSession session,Button add)
{
 var slot=(Border)strip.FindName("ImportSlot");
 var toggle=(ToggleButton)strip.FindName("ExpandToggle");
 var mode=(ComboBox)strip.FindName("Mode");
 double Gap()=>toggle.TranslatePoint(new Point(0,0),strip).X-mode.TranslatePoint(new Point(mode.ActualWidth,0),strip).X;
 Check(session.ProvidesImageImport,"前置：当前会话是图像源节点自身");
 Check(Math.Abs(slot.ActualWidth-130)<0.5,"图像源节点保留 130px 占位");
 Check(add.IsVisible,"图像源节点展开时给出增删图入口");
 double ownerGap=Gap();
 session.ProvidesImageImport=false;session.Raise();Pump();
 Check(slot.ActualWidth==0,"下游节点不留占位块");
 Check(Math.Abs(ownerGap-Gap()-130)<0.5,"下游节点右侧控件整体右移 130px");
 Check(!add.IsVisible,"下游节点不给增删图入口");
 strip.IsExpanded=false;Pump();
 Check(!add.IsVisible,"收起后不显示增删图入口");
 Check(slot.ActualWidth==0,"收起不改变占位归属");
 strip.IsExpanded=true;session.ProvidesImageImport=true;session.Raise();Pump();
 Check(slot.ActualWidth==130&&add.IsVisible,"回到图像源节点后占位与入口一并恢复");
 Check(Math.Abs(ownerGap-Gap())<0.5,"回到图像源节点后工具条右侧组件复位");
}
private static void CheckDirectoryMenu(ImageSourceStrip strip,ListBox list,ProbeSession session)
{
 strip.IsRunning=false;list.ScrollIntoView(list.Items[0]);list.UpdateLayout();Pump();
 var container=(ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
 var thumbnail=FindVisual<ImageSourceThumbnail>(container)??throw new InvalidOperationException("未生成原件缩略图");
 var menu=thumbnail.ContextMenu;var action=(MenuItem)menu.Items[0];
 void Open(){menu.PlacementTarget=thumbnail;menu.IsOpen=true;Pump();}
 void Click(){action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent,action));Pump();}
 var requestCount=session.Requests.Count;
 Open();
 Check(session.DisplayIndex==1,"右键菜单不改变执行游标");
 menu.IsOpen=false;Click();
 Check(session.Requests.Count==requestCount+1&&session.Requests[^1]==ImageSourceStripRequestKind.OpenDirectory
   &&session.LastItemId=="b","菜单读取当前图像，右击非当前缩略图时仍定位当前图像");
 Open();session.DisplayIndex=0;session.Raise();Pump();
 menu.IsOpen=false;Click();
 Check(session.Requests.Count==requestCount+2&&session.LastItemId=="a","执行菜单命令时读回最新图像索引");
 Open();session.DisplayIndex=-1;session.Raise();Pump();
 menu.IsOpen=false;Click();
 Check(session.Requests.Count==requestCount+2,"没有当前图像时不发目录请求");
 session.DisplayIndex=1;session.Raise();Pump();
 Open();session.Version++;
 Click();
 Check(session.Requests.Count==requestCount+2,"列表版本变化后旧菜单不发请求");
 menu.IsOpen=false;Pump();Open();
 var replacement=new ProbeSession{CanEdit=true};
 strip.Session=replacement;Click();
 Check(!menu.IsOpen&&replacement.Requests.Count==0&&session.Requests.Count==requestCount+2,"换源关闭旧菜单并拒绝迟到点击");
}
private static T? FindVisual<T>(DependencyObject parent) where T:DependencyObject
{
 if(parent is T match)return match;
 for(var index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++)
  if(FindVisual<T>(VisualTreeHelper.GetChild(parent,index)) is {} child)return child;
 return null;
}
private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
 private sealed class ProbeSession:IImageSourceStripSession
 {
  public bool CanEdit{get;set;}public bool CanNavigate{get;set;}public bool IsReadOnly=>!CanEdit;
  public bool ProvidesImageImport{get;set;}=true;
  public IReadOnlyList<ImageSourceStripItem> Items{get;}=new[]{new ImageSourceStripItem("a","A",null,true),new ImageSourceStripItem("b","B",null,true)};
  public long Version{get;set;}=1;public int DisplayIndex{get;set;}public int ConfiguredStartIndex=>0;
  public bool IsSelectMode=>false;public bool AutoSwitchEnabled=>false;public bool IsImporting=>false;public string? Status=>null;
  public List<ImageSourceStripRequestKind> Requests{get;}=new();public string? LastItemId{get;private set;} public event PropertyChangedEventHandler? PropertyChanged;
  public void Raise()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
  public Task RequestAsync(ImageSourceStripRequest request,CancellationToken cancellation){Requests.Add(request.Kind);LastItemId=request.ItemId;if(request.Kind==ImageSourceStripRequestKind.Activate)DisplayIndex=Items.ToList().FindIndex(item=>item.Id==request.ItemId);return Task.CompletedTask;}
 }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Infrastructure.Persistence.ApplicationData;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.UI;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Shell.Windows;

internal static class RootCollectionMainProbe
{
 private static void Check(bool value,string message){if(!value)throw new Exception(message);}
 private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 private static void Until(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(20);while(!done()){if(DateTime.UtcNow>end)throw new TimeoutException("根集合真实主窗未到达预期状态。");Pump();Thread.Sleep(1);}}
 private static void Await(Task task){Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();Pump();}
 private static object? Call(object owner,string method,params object?[] args)=>owner.GetType().GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Single(item=>item.Name==method&&item.GetParameters().Length==args.Length).Invoke(owner,args);
 private static IEnumerable<T> Visuals<T>(DependencyObject element)where T:DependencyObject{if(element is T match)yield return match;for(var i=0;i<VisualTreeHelper.GetChildrenCount(element);i++)foreach(var child in Visuals<T>(VisualTreeHelper.GetChild(element,i)))yield return child;}
 private static void TabMenu(ShellWorkspaceControl workspace,WorkflowTabViewModel tab,string header)
 {
  var item=(TabItem?)Call(workspace,"FindTabItem",tab)??throw new Exception("实际Tab容器未生成。");item.ApplyTemplate();
  var anchor=Visuals<FrameworkElement>(item).First(element=>element.ContextMenu is not null);var menu=anchor.ContextMenu!;menu.PlacementTarget=anchor;menu.IsOpen=true;Pump();
  Check(ReferenceEquals(menu.DataContext,tab),"实际菜单没有继承原Tab身份。");var command=menu.Items.OfType<MenuItem>().Single(entry=>entry.Header?.ToString()==header);Check(command.IsEnabled,"实际菜单入口被错误禁用："+header);
  command.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));menu.IsOpen=false;Pump();
 }
 private static void Render(FrameworkElement element,string path)
 {
  element.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(element.ActualWidth)),Math.Max(1,(int)Math.Ceiling(element.ActualHeight)),96,96,PixelFormats.Pbgra32);bitmap.Render(element);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);Console.WriteLine("root main visual: "+path);
 }
 private static Task Run(MainWindowViewModel vm){vm.RunWorkflowCommand.Execute(null);return(Task)vm.GetType().GetField("_formalRunTask",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;}
 private static void Save(DocumentSession session,string file,string root)=>session.SaveAs(file,Path.Combine(root,Guid.NewGuid()+".recovery"),root,new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()),new System.Text.Json.JsonSerializerOptions(),scope=>new ComplexVariableValueCodec(scope),File.Exists(file)?SunEyeVision.Persistence.TargetExpectation.Match(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)))):SunEyeVision.Persistence.TargetExpectation.Missing);
 internal static void RunWindow()
 {
  var app=(App)System.Windows.Application.Current;var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","_validation","RootCollectionMain",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);
  var locations=new ApplicationDataLocationService(Path.Combine(root,"locator.json"));var paths=locations.Prepare(Path.Combine(root,"Data"));typeof(App).GetField("_dataPaths",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,paths);typeof(App).GetField("_dataLocations",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,locations);
  var plugins=new PluginManager();plugins.LoadPluginsAsync().GetAwaiter().GetResult();Check(plugins.LastAdmissionError is null,plugins.LastAdmissionError??"");using var pin=plugins.PinDesktopHost();
  ServiceInitializer.ConfigureDataPaths(paths);ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(paths,plugins.RuntimeDirectory!);var theme=new ThemeManager(()=>null,_=>{});theme.Initialize();ServiceLocator.Instance.Register(theme);
  var session=ServiceInitializer.SolutionManager.CreateNew("隐藏根集合生产链");var flow=session.GetWorkflows().Single();var graph=session.PrepareGraphEdit(flow.Id);var source=session.CreateNode(graph,"ImageLoad",new(150,120));var blob=session.CreateNode(graph,"BlobAnalysis",new(350,210));graph.AddConnection(new("source-blob",source.Id,blob.Id,"BottomPort","TopPort"));session.CommitGraphEdit(graph,"建立真实源和Blob");
  var image=Path.Combine(root,"source.pgm");File.WriteAllBytes(image,System.Text.Encoding.ASCII.GetBytes("P5\n2 2\n255\n").Concat(new byte[]{35,35,35,35}).ToArray());var fields=session.Editing.Host.BeginEdit(flow.Id,source.Id);fields.ApplyFields(new Dictionary<string,ParameterSetting>{{"local.paths",new ConstantSetting(new[]{image})},{"local.start",new ConstantSetting(0L)}});session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{fields}));var file=Path.Combine(root,"roots.solution");Save(session,file,root);
  var main=new MainWindow(plugins.DesktopAdmission!){WindowState=WindowState.Normal,Left=-32000,Top=-32000,Width=1100,Height=700,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=main;var vm=(MainWindowViewModel)main.DataContext;main.Show();Pump();var workspace=(ShellWorkspaceControl)main.FindName("WorkspaceHost");
  var original=vm.WorkflowTabViewModel.SelectedTab!;var history=session.HistoryEntries.Count;var add=Visuals<Button>(workspace).First(button=>button.IsVisible&&button.ToolTip?.ToString()=="添加工作流");Check(add.IsEnabled,"实际加号未启用");add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();Check(session.GetWorkflows().Count==2&&session.HistoryEntries.Count==history+1,"加号没有唯一集合历史");Console.WriteLine("root main: actual add button publishes one root/history");
  TabMenu(workspace,original,"复制流程");var copy=vm.WorkflowTabViewModel.SelectedTab!;var copyRoot=session.GetWorkflows().Single(item=>item.Id==copy.Id);Check(copyRoot.Nodes.Count==2&&copyRoot.Connections.Count==1,"右键复制没有真实图");
  TabMenu(workspace,copy,"重命名");var input=Visuals<TextBox>(workspace).Single(box=>box.Name=="TabNameEditor"&&ReferenceEquals(box.DataContext,copy));input.Text="隐藏复制根";var keyboard=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(input),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent};input.RaiseEvent(keyboard);Pump();Check(copyRoot.Name=="隐藏复制根"&&copy.Name==copyRoot.Name,"实际行内改名没有正式提交");Console.WriteLine("root main: actual copy menu and inline rename use formal root identities");
  Await(Run(vm));var copiedBlob=copyRoot.Nodes.Single(node=>node.ToolType=="BlobAnalysis");using(var result=session.AcquirePublishedResult(copyRoot.Id,copiedBlob.Id))Check(result?.Snapshot.IsSuccess==true,"复制根未执行正式HALCON");var cap=session.AdmitPresentation(copyRoot.Id,copiedBlob.Id);vm.SelectedNode=copiedBlob;Pump();Render(main,Path.Combine(root,"root-main-dark.png"));theme.ApplyTheme(AppTheme.Light);Pump();Render(main,Path.Combine(root,"root-main-light.png"));theme.ApplyTheme(AppTheme.Dark);Pump();
  var accept=false;var answered=false;var dialogError="";var dialogs=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};dialogs.Tick+=(_,_)=>{foreach(var dialog in app.Windows.OfType<ThemeMessageDialog>().ToArray()){var text=((TextBlock)dialog.FindName("MessageText")).Text;if(!text.Contains("确定要删除工作流")){dialogError=text;typeof(ThemeMessageDialog).GetMethod("OnOkClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dialog,new object?[]{null,new RoutedEventArgs()});continue;}answered=true;typeof(ThemeMessageDialog).GetMethod(accept?"OnYesClick":"OnNoClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dialog,new object?[]{null,new RoutedEventArgs()});}};dialogs.Start();
  history=session.HistoryEntries.Count;TabMenu(workspace,copy,"删除");Check(answered&&session.HistoryEntries.Count==history&&vm.WorkflowTabViewModel.Tabs.Contains(copy),"取消删除产生副作用");accept=true;answered=false;TabMenu(workspace,copy,"删除");dialogs.Stop();Check(answered&&dialogError.Length==0&&!vm.WorkflowTabViewModel.Tabs.Contains(copy)&&!cap.IsCurrent,"删除没有退出实际Tab/准入："+dialogError);Check(session.LastObserverError is null,session.LastObserverError?.ToString()??"");
  vm.UndoCommand.Execute(null);Pump();var restored=vm.WorkflowTabViewModel.Tabs.Single(tab=>tab.Id==copyRoot.Id);Check(!ReferenceEquals(restored,copy)&&session.GetWorkflows().Any(item=>ReferenceEquals(item,copyRoot))&&!cap.IsCurrent,"Undo复活旧Tab或cap");Console.WriteLine("root main: cancel/delete/undo preserves history and retires old admission");
  Save(session,file,root);var reopenFile=Path.Combine(root,"reopened-roots.solution");File.Copy(file,reopenFile);var opened=(Task<bool>)Call(vm,"OpenExternalSolutionAsync",reopenFile,false)!;Await(opened);Check(opened.Result,"根集合保存重开失败："+vm.StatusText);var reopened=ServiceInitializer.SolutionManager.CurrentSession!;Check(!ReferenceEquals(session,reopened)&&reopened.GetWorkflows().Count==3,"重开未换实际Session");vm.SetSelectedTab(vm.WorkflowTabViewModel.Tabs.Single(tab=>tab.Id==copyRoot.Id));Await(Run(vm));using(var result=reopened.AcquirePublishedResult(copyRoot.Id,copiedBlob.Id))Check(result?.Snapshot.IsSuccess==true,"重开后根副本不可运行");Console.WriteLine("root main: save/reopen and real copied HALCON run");
  var closed=false;main.Closed+=(_,_)=>closed=true;main.Close();Until(()=>closed);ServiceInitializer.DisposeCommunicationRuntimeAsync().AsTask().GetAwaiter().GetResult();ServiceInitializer.DisposeCameraRuntimeAsync().AsTask().GetAwaiter().GetResult();ServiceInitializer.SolutionManager.CloseCurrent(true);pin.Dispose();plugins.UnloadPluginsAsync().GetAwaiter().GetResult();Console.WriteLine("root main: real window/consumers/session/plugins drained");
 }
}


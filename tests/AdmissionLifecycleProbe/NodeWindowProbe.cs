using System.IO;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Infrastructure.Persistence.ApplicationData;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Shell.Windows;

internal static class NodeWindowProbe
{
 internal static int Run()
 {
  SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
  try { SunEyeVision.TestShared.WpfTestDispatcher.Invoke(RunWindows); return 0; }
  finally { SunEyeVision.TestShared.WpfTestDispatcher.ShutdownOwned(); }
 }
 private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 private static void Until(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(15);while(!done()){if(DateTime.UtcNow>end)throw new TimeoutException("参数窗关闭未排空。");Pump();Thread.Sleep(1);}}
 private static void RunWindows()
 {
  var app=System.Windows.Application.Current;
  foreach(var resource in new[]{"Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml"})
   app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
  var manager=new PluginManager();manager.LoadPluginsAsync().GetAwaiter().GetResult();SessionLifecycleProbe.Check(manager.LastAdmissionError is null,manager.LastAdmissionError??"");
  using var pin=manager.PinDesktopHost();
  var root=SessionLifecycleProbe.CaseDirectory("node-window");
  var paths=new ApplicationDataLocationService(Path.Combine(root,"locator.json")).Prepare(Path.Combine(root,"Data"));
  ServiceInitializer.ConfigureDataPaths(paths);ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(paths,manager.RuntimeDirectory!);
  var session=ServiceInitializer.SolutionManager.CreateNew("真实参数窗");
  var viewModel=new MainWindowViewModel(manager.DesktopAdmission!);
  var shell=new Window{DataContext=viewModel,Width=620,Height=400,Left=-32000,Top=-32000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false};app.MainWindow=shell;shell.Show();
  try
  {
   var flow=session.GetWorkflows().Single();
   foreach(var id in new[]{"UnknownTool","Subroutine","Condition"})
   {
    var invalid=new SunEyeVision.Domain.Workflows.NodeDefinition(Guid.NewGuid().ToString("N"),id,id,
     new SunEyeVision.Domain.Workflows.ToolConfiguration(id,new SunEyeVision.Plugin.Contracts.FieldSettings(Array.Empty<SunEyeVision.Plugin.Contracts.Port>(),new Dictionary<string,SunEyeVision.Plugin.Contracts.ParameterSetting>())));
    viewModel.OpenDebugWindowCommand.Execute(invalid);Pump();
    SessionLifecycleProbe.Check(viewModel.StatusText.StartsWith("节点打开失败：",StringComparison.Ordinal)&&!app.Windows.OfType<RuntimeToolEditorWindow>().Any()&&session.Editing.RegisteredScopeCount==0,"未准入/暂停节点打开或登记参数窗："+id);
   }
   Console.WriteLine("probe: actual main command rejected unadmitted and paused nodes without constructing windows");
   foreach(var id in new[]{"ImageLoad","BlobAnalysis"})
   {
    var graph=session.PrepareGraphEdit(flow.Id);var node=session.CreateNode(graph,id,new(150,120));session.CommitGraphEdit(graph,"建立真实参数节点");
    viewModel.SelectedNode=node;viewModel.OpenDebugWindowCommand.Execute(node);Pump();
    var window=app.Windows.OfType<RuntimeToolEditorWindow>().SingleOrDefault(w=>w.IsVisible)??throw new Exception("正式命令未显示实际参数窗："+id);
    SessionLifecycleProbe.Check(window.IsLoaded&&SunEyeVision.Plugin.Editing.Wpf.ToolEditorCommit.GetDraftContext(window.Editor) is ProductionEditorContext{IsValid:true},"实际参数草稿未接入："+id);
    SessionLifecycleProbe.Check(session.Editing.RegisteredScopeCount>0,"实际窗口未登记编辑participant");
    window.Close();Until(()=>!window.IsVisible&&session.Editing.RegisteredScopeCount==0);
    Console.WriteLine("隐藏桌面真实窗口通过: "+id);
   }
   Console.WriteLine("probe: hidden production parameter windows closed and scopes released");
  }
  finally
  {
   viewModel.ReleaseDocumentHost();shell.Close();
   ServiceInitializer.DisposeCommunicationRuntimeAsync().AsTask().GetAwaiter().GetResult();ServiceInitializer.DisposeCameraRuntimeAsync().AsTask().GetAwaiter().GetResult();
   ServiceInitializer.SolutionManager.CloseCurrent(true);pin.Dispose();manager.UnloadPluginsAsync().GetAwaiter().GetResult();Pump();
  }
 }
}

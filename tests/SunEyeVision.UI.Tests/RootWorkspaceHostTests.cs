using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Workflow;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using Xunit;
namespace SunEyeVision.UI.Tests;
/// <summary>单独测试进程运行，真实Application/宿主激活与方案文件重开；不冒称桌面鼠标操作。</summary>
[Collection("VM canvas UI")]
public sealed class RootWorkspaceHostTests
{
 [Fact]
 public void Actual_host_copies_root_and_manages_empty_roots_with_undo_redo_and_file_reopen()
 {
  Exception? failure=null;var thread=new Thread(()=>
  {
   Window? owner=null;
   try
   {
    if(Application.Current==null)_=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
    foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})Application.Current.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
    ServiceInitializer.InitializeServices();var viewModel=new MainWindowViewModel();
    ToolRegistry.RegisterTool(typeof(RootWorkspaceSettingsTests.RootPolicyProbe));
    SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginLoader.MarkAsLoaded();
    var shell=new SunEyeVision.UI.Shell.Controls.Shell.ShellWorkspaceControl{DataContext=viewModel.WorkflowTabViewModel};shell.Initialize(viewModel);
    owner=new Window{Content=shell,Width=1100,Height=800,Left=-10000,ShowActivated=false};
    owner.Show();Pump();
    var manager=ServiceInitializer.SolutionManager;var document=manager.CurrentSolution!;
    var source=Assert.Single(document.Workflows.Where(w=>!w.IsSubroutine));var sourceTab=Assert.Single(viewModel.WorkflowTabViewModel.Tabs);
    source.WorkspaceSettings=new(){RunIntervalMs=41};
    var first=new SunEyeVision.UI.Shared.WorkflowNode("first","7 自定义入口","自定义入口","RootPolicyProbe"){GlobalIndex=7,LocalIndex=3,Parameters=new RootWorkspaceSettingsTests.ProbeParameters(),Position=new Point(100,100)};
    var second=new SunEyeVision.UI.Shared.WorkflowNode("second","8 终点","终点","RootPolicyProbe"){GlobalIndex=8,LocalIndex=5,Parameters=new RootWorkspaceSettingsTests.ProbeParameters(),Position=new Point(200,220)};
    source.Nodes.Add(first);source.Nodes.Add(second);source.Connections.Add(new(){Id="edge",SourceNodeId=first.Id,TargetNodeId=second.Id,ManualPathPoints=new[]{new Point(100,150),new Point(200,150)}});
    Assert.True(viewModel.CopyRootWorkspace(sourceTab),viewModel.StatusText);
    var copied=document.Workflows.Single(w=>!w.IsSubroutine&&w.Id!=source.Id);var copyTab=viewModel.WorkflowTabViewModel.SelectedTab!;
    Assert.Equal(2,copied.Nodes.Count);Assert.Equal("自定义入口",copied.Nodes[0].DispName);Assert.Equal(3,copied.Nodes[0].LocalIndex);Assert.Equal(41,copied.WorkspaceSettings.RunIntervalMs);
    Assert.NotEqual(first.Id,copied.Nodes[0].Id);Assert.Equal(copied.Nodes[0].Id,Assert.Single(copied.Connections).SourceNodeId);Assert.NotSame(first.Parameters,copied.Nodes[0].Parameters);
    var tabs=(TabControl)shell.FindName("WorkflowTabControl");Pump();
    var active=(TabItem)tabs.ItemContainerGenerator.ContainerFromItem(copyTab);active.ApplyTemplate();
    var inactive=(TabItem)tabs.ItemContainerGenerator.ContainerFromItem(sourceTab);inactive.ApplyTemplate();
    Assert.Equal(40,active.ActualHeight);Assert.Equal(new CornerRadius(0),((Border)active.Template.FindName("TabBorder",active)).CornerRadius);
    Assert.Equal(Visibility.Visible,((FrameworkElement)active.Template.FindName("RootTabTools",active)).Visibility);
    Assert.Equal(Visibility.Collapsed,((FrameworkElement)inactive.Template.FindName("RootTabTools",inactive)).Visibility);
    Assert.False(((Button)active.Template.FindName("RootVariablesBtn",active)).IsEnabled);
    first.Parameters.BindingValidationPending["InputImage"]="待确认";sourceTab.RefreshRootReadiness();Assert.False(sourceTab.IsRootConfigurationReady);Assert.True(copyTab.IsRootConfigurationReady);
    viewModel.SetSelectedTab(sourceTab);Pump();
    Assert.Equal(Visibility.Visible,((FrameworkElement)inactive.Template.FindName("RootReadinessIndicator",inactive)).Visibility);
    first.LastResult=new RootWorkspaceSettingsTests.ProbeResults{Status=SunEyeVision.Plugin.SDK.Execution.Results.ExecutionStatus.Failed};sourceTab.RefreshRootReadiness();Assert.False(sourceTab.IsRootConfigurationReady); // 运行结果不能把坏配置变成就绪。
    SaveHeader(tabs,"Errors-Dark");
    first.Parameters.BindingValidationPending.Clear();sourceTab.RefreshRootReadiness();Assert.True(sourceTab.IsRootConfigurationReady);Pump();
    Assert.Equal(Visibility.Collapsed,((FrameworkElement)inactive.Template.FindName("RootReadinessIndicator",inactive)).Visibility);
    viewModel.SetSelectedTab(copyTab);Pump();
    var copyId=copied.Id;Assert.True(viewModel.CanUndoRootWorkspace);viewModel.UndoRootWorkspace();Assert.Single(document.Workflows);viewModel.RedoRootWorkspace();Assert.Same(copied,document.Workflows.Single(w=>w.Id==copyId));
    Assert.True(viewModel.DeleteRootWorkspace(copyTab),viewModel.StatusText);Assert.Single(document.Workflows);
    Assert.True(viewModel.DeleteRootWorkspace(sourceTab),viewModel.StatusText);Assert.Empty(document.Workflows);Assert.Null(viewModel.WorkflowTabViewModel.SelectedTab);Pump();
    Assert.Equal(Visibility.Visible,((FrameworkElement)shell.FindName("RootEmptyPanel")).Visibility);Assert.Empty(((System.Windows.Controls.Canvas)shell.FindName("MiniMapCanvas")).Children);Assert.True(shell.RootWorkspaceEditingAllowed);Assert.True(shell.RootUndoAllowed);
    viewModel.UndoRootWorkspace();Assert.Single(document.Workflows);viewModel.UndoRootWorkspace();Assert.Equal(2,document.Workflows.Count);
    Pump();Assert.Equal(Visibility.Collapsed,((FrameworkElement)shell.FindName("RootEmptyPanel")).Visibility);
    var directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"root-host");Directory.CreateDirectory(directory);var path=Path.Combine(directory,"根流程验收.json");
    var saved=manager.SaveCurrentDocument(path);Assert.True(saved.FileWritten,saved.Error);var reopened=new SolutionRepository().Load(path);
    Assert.Equal(2,reopened.Workflows.Count);var loadedCopy=reopened.Workflows.Single(w=>w.Id==copyId);Assert.Equal(41,loadedCopy.WorkspaceSettings.RunIntervalMs);Assert.Equal(copied.Nodes.Select(n=>n.Id),loadedCopy.Nodes.Select(n=>n.Id));
    var engine=new WorkflowEngine(VisionLogger.Instance);foreach(var flow in reopened.Workflows)engine.RegisterWorkflow(flow);
    var runner=new WorkflowExecutionEngine(engine,new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginManager(VisionLogger.Instance),VisionLogger.Instance);
    using var run=runner.ExecuteWorkflowAsync(copyId,null).GetAwaiter().GetResult();Assert.True(run.Success,string.Join(";",run.Errors));Assert.Equal(2,run.NodeResults.Count);
    for(int i=0;i<8;i++)Assert.True(viewModel.AddRootWorkspace());
    owner.Width=540;Pump();owner.UpdateLayout();Pump();
    var viewer=(ScrollViewer)tabs.Template.FindName("TabPanelScrollViewer",tabs);
    var fixedAdd=(Border)tabs.Template.FindName("FixedAddButtonBorder",tabs);
    Assert.Equal(Visibility.Visible,fixedAdd.Visibility);
    var resizedSelected=(TabItem)tabs.ItemContainerGenerator.ContainerFromItem(viewModel.WorkflowTabViewModel.SelectedTab);
    var resizedLeft=resizedSelected.TransformToAncestor(viewer).Transform(new Point()).X;
    Assert.True(resizedLeft+resizedSelected.ActualWidth<=viewer.ViewportWidth+1,"缩窄窗口后活动Tab未完整可见");
    var last=viewModel.WorkflowTabViewModel.Tabs.Last();viewModel.SetSelectedTab(sourceTab);Pump();Pump();Assert.Equal(0,viewer.HorizontalOffset);
    viewModel.SetSelectedTab(last);Pump();Pump();owner.UpdateLayout();
    active=(TabItem)tabs.ItemContainerGenerator.ContainerFromItem(last);var left=active.TransformToAncestor(viewer).Transform(new Point()).X;
    Assert.InRange(left,-1,viewer.ViewportWidth);Assert.True(left+active.ActualWidth<=viewer.ViewportWidth+1);
    SaveHeader(tabs,"Dark");
    Application.Current.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/Themes/Light/LightTheme.xaml",UriKind.Relative)});Pump();SaveHeader(tabs,"Light");
    owner.Width=2000;Pump();Pump();Assert.Equal(Visibility.Collapsed,fixedAdd.Visibility);

   }
   catch(Exception error){failure=error;}
   finally{owner?.Close();}
  }){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(40)),"真实根工作区宿主验证超时");
  if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
 }
 private static void SaveHeader(TabControl tabs,string theme)
 {
  var bar=(FrameworkElement)tabs.Template.FindName("TabHeaderBar",tabs);bar.UpdateLayout();
  var image=new RenderTargetBitmap((int)Math.Ceiling(bar.ActualWidth),(int)Math.Ceiling(bar.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(bar);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"root-tabs-"+theme+".png"));encoder.Save(file);
 }
 private static void Pump()
 {
  var frame=new System.Windows.Threading.DispatcherFrame();System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(frame);
 }
}

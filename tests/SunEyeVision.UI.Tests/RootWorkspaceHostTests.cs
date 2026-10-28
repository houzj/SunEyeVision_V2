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
    SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
    if(Application.Current==null)_=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
    foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})Application.Current.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
    ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"host-solutions"));var viewModel=new MainWindowViewModel();
    ToolRegistry.RegisterTool(typeof(RootConfigurationTests.Probe));
    SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginLoader.MarkAsLoaded();
    var shell=new SunEyeVision.UI.Shell.Controls.Shell.ShellWorkspaceControl{DataContext=viewModel.WorkflowTabViewModel};shell.Initialize(viewModel);
    var topBar=new SunEyeVision.UI.Shell.Controls.Shell.ShellTopBarControl{DataContext=viewModel.TopBar};
    var workspace=new DockPanel();DockPanel.SetDock(topBar,Dock.Top);workspace.Children.Add(topBar);workspace.Children.Add(shell);
    owner=new Window{Content=workspace,Width=1100,Height=800,Left=-10000,ShowActivated=false};
    owner.Show();Pump();
    Assert.Equal(new Rect(0,0,6,6),Assert.IsType<DrawingBrush>(Application.Current.FindResource(SunEyeVision.UI.Themes.Base.ThemeKeys.WorkflowCanvasGridBrush)).Viewport);
    VerifyWorkflowButtonThemes(shell,"Dark");VerifyMiniMap(shell,owner,"Dark");SaveConfiguration(owner,"Canvas-Dark");
    var canvasLight=new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/Themes/Light/LightTheme.xaml",UriKind.Relative)};
    Application.Current.Resources.MergedDictionaries.Add(canvasLight);Pump();VerifyWorkflowButtonThemes(shell,"Light");VerifyMiniMap(shell,owner,"Light");SaveConfiguration(owner,"Canvas-Light");
    Application.Current.Resources.MergedDictionaries.Remove(canvasLight);Pump();
    var manager=ServiceInitializer.SolutionManager;var document=manager.CurrentSolution!;
    var source=Assert.Single(document.Workflows.Where(w=>!w.IsSubroutine));var sourceTab=Assert.Single(viewModel.WorkflowTabViewModel.Tabs);
    VerifySourceEditing(viewModel,source,sourceTab);
    source.WorkspaceSettings=new(){RunIntervalMs=41};
    var first=new SunEyeVision.UI.Shared.WorkflowNode("first","7 自定义入口","自定义入口","RootConfigurationProbe"){GlobalIndex=7,LocalIndex=3,Parameters=new RootConfigurationTests.Parameters(),Position=new Point(100,100)};
    var second=new SunEyeVision.UI.Shared.WorkflowNode("second","8 终点","终点","RootConfigurationProbe"){GlobalIndex=8,LocalIndex=5,Parameters=new RootConfigurationTests.Parameters(),Position=new Point(200,220)};
    source.Nodes.Add(first);source.Nodes.Add(second);source.Connections.Add(new(){Id="edge",SourceNodeId=first.Id,TargetNodeId=second.Id,ManualPathPoints=new[]{new Point(100,150),new Point(200,150)}});
    // Actual canvas context menu shares the toolbox catalog; unavailable and failed loads stay disabled.
    var menuCase=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"context-menu");
    Directory.CreateDirectory(Path.Combine(menuCase,"plugins","probe"));
    File.WriteAllText(Path.Combine(menuCase,"ToolGroups.xml"),"<ToolGroups SchemaVersion='1'><Group Name='通信' Order='14' Icon='CommunicationImageEnable.png' HoverIcon='CommunicationImageHover.png'/><Tool Id='RootConfigurationProbe' Category='通信' Directory='probe'/><Tool Id='FailedContextTool' Category='通信' Directory='probe'/></ToolGroups>");
    File.WriteAllText(Path.Combine(menuCase,"plugins","probe","ToolItemInfo.xml"),"<ToolItems SchemaVersion='1' Assembly='missing.dll'><Tool Id='RootConfigurationProbe' DisplayName='右键探针' Category='通信' Order='10' Icon='generic' EntryType='Probe'/><Tool Id='FailedContextTool' DisplayName='加载失败探针' Category='通信' Order='11' Icon='generic' EntryType='Missing'/></ToolItems>");
    ToolRegistry.ConfigureDirectory(new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.ToolDirectoryCatalog(Path.Combine(menuCase,"plugins"),Path.Combine(menuCase,"ToolGroups.xml"),Array.Empty<SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DirectoryTool>()),id=>{if(id!="RootConfigurationProbe")throw new InvalidOperationException("故障注入：组件加载失败");});
    var canvas=shell.GetCurrentWorkflowCanvas()!;
    canvas.GetType().GetField("_viewModel",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(canvas,viewModel);
    canvas.GetType().GetField("_vmPastePoint",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(canvas,new Point(347,283));
    var surface=(System.Windows.Controls.Canvas)canvas.FindName("WorkflowCanvas");var toolMenu=surface.ContextMenu;
    toolMenu.PlacementTarget=surface;toolMenu.IsOpen=true;Pump();
    var category=Assert.Single(toolMenu.Items.OfType<MenuItem>().Where(item=>item.Header as string=="通信"));
    category.IsSubmenuOpen=true;Pump();
    SaveMenuVisual(toolMenu,"context-root");
    var categoryPopup=(System.Windows.Controls.Primitives.Popup)category.Template.FindName("PART_Popup",category);
    Assert.NotNull(categoryPopup);SaveMenuVisual((FrameworkElement)categoryPopup.Child,"context-tools");
    var leaves=category.Items.OfType<MenuItem>().ToArray();
    Assert.Contains(leaves,item=>item.Tag is SunEyeVision.UI.Shell.Controls.Toolbox.ToolItem {ToolId:"vm:communication.send"}||item.Tag is SunEyeVision.UI.Shell.Controls.Toolbox.ToolItem {IsAvailable:false});
    var loadedLeaf=leaves.Single(item=>item.Tag is SunEyeVision.UI.Shell.Controls.Toolbox.ToolItem {ToolId:"RootConfigurationProbe"});
    Assert.True(loadedLeaf.IsEnabled);Assert.NotNull(loadedLeaf.Icon);Assert.Equal(1,loadedLeaf.Opacity);
    Assert.All(leaves.Where(item=>item.Tag is SunEyeVision.UI.Shell.Controls.Toolbox.ToolItem {IsAvailable:false}),item=>{Assert.False(item.IsEnabled);Assert.Equal(.45,item.Opacity);});
    Assert.False(leaves.Single(item=>item.Tag is SunEyeVision.UI.Shell.Controls.Toolbox.ToolItem {ToolId:"FailedContextTool"}).IsEnabled);
    var beforeContextHistory=manager.Session!.History.Position;
    loadedLeaf.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Pump();
    var addedByContext=source.Nodes.Single(node=>node.Id!=first.Id&&node.Id!=second.Id);
    Assert.Equal(new Point(345,285),addedByContext.Position);Assert.Equal(beforeContextHistory+1,manager.Session.History.Position);
    viewModel.UndoCommand.Execute(null);Assert.DoesNotContain(addedByContext,source.Nodes);
    viewModel.RedoCommand.Execute(null);Assert.Same(addedByContext,source.Nodes.Single(node=>node.Id==addedByContext.Id));
    viewModel.UndoCommand.Execute(null);
    toolMenu.IsOpen=false;Pump();
    Assert.True(viewModel.AddRootWorkspace());var staleTarget=viewModel.WorkflowTabViewModel.SelectedTab!;
    loadedLeaf.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert.Empty(staleTarget.WorkflowNodes);Assert.Equal(2,source.Nodes.Count);
    viewModel.UndoRootWorkspace();Assert.Same(sourceTab,viewModel.WorkflowTabViewModel.SelectedTab);Pump();
    var undoEdit=(Button)topBar.FindName("UndoEditButton");var redoEdit=(Button)topBar.FindName("RedoEditButton");
    Assert.Same(viewModel.UndoCommand,undoEdit.Command);Assert.Same(viewModel.RedoCommand,redoEdit.Command);
    sourceTab.CommandManager.Execute(new SunEyeVision.UI.Features.WorkflowUI.Commands.MoveNodeCommand(first,new Point(100,100),new Point(160,170)));Pump();
    Assert.True(undoEdit.Command.CanExecute(null));undoEdit.Command.Execute(null);Assert.Equal(new Point(100,100),first.Position);
    Assert.True(redoEdit.Command.CanExecute(null));redoEdit.Command.Execute(null);Assert.Equal(new Point(160,170),first.Position);
    undoEdit.Command.Execute(null);Pump();
    viewModel.OpenRootConfiguration(sourceTab,owner);Pump();
    Assert.False(undoEdit.Command.CanExecute(null));Assert.False(redoEdit.Command.CanExecute(null));
    var configuration=Application.Current.Windows.OfType<RootWorkflowConfigurationWindow>().Single();
    viewModel.OpenRootConfiguration(sourceTab,owner);Assert.Same(configuration,Application.Current.Windows.OfType<RootWorkflowConfigurationWindow>().Single());
    var candidate=configuration.Context.Candidates().Single(c=>c.Node.Id==first.Id&&c.FieldId=="Value");
    var proxy=configuration.Context.Add(candidate);configuration.Context.Write(proxy,new SunEyeVision.Plugin.Contracts.ConstantSetting(11L));
    Assert.Equal(3,((RootConfigurationTests.Parameters)first.Parameters).Value);Assert.Empty(source.RootConfiguration.RunParameters);
    Assert.True(configuration.Context.TryApply(out var applyError),applyError);Assert.Equal(11,((RootConfigurationTests.Parameters)first.Parameters).Value);Assert.Single(source.RootConfiguration.RunParameters);
    configuration.Context.Undo();Assert.Equal(3,((RootConfigurationTests.Parameters)first.Parameters).Value);Assert.Empty(source.RootConfiguration.RunParameters);
    configuration.Context.Redo();Assert.Equal(11,((RootConfigurationTests.Parameters)first.Parameters).Value);Assert.Single(source.RootConfiguration.RunParameters);
    ((Button)configuration.FindName("RunOnce")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    var executionWait=System.Diagnostics.Stopwatch.StartNew();
    while(viewModel.CaptureRootOverview().Entries.Single(e=>e.RootId==source.Id).RunCount is null&&executionWait.Elapsed<TimeSpan.FromSeconds(15)){Pump();Thread.Sleep(1);}
    Pump();Assert.Equal(1,viewModel.CaptureRootOverview().Entries.Single(e=>e.RootId==source.Id).RunCount);
    configuration.RefreshPublishedResults();Assert.Equal(2,((DataGrid)configuration.FindName("Results")).Items.Count);
    viewModel.StatusBar.ShowExecutionSnapshotCommand.Execute(null);Pump();
    var timing=Application.Current.Windows.OfType<WorkflowExecutionSnapshotWindow>().Single();SaveConfiguration(timing,"Timing-Dark");
    var timingLight=new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/Themes/Light/LightTheme.xaml",UriKind.Relative)};Application.Current.Resources.MergedDictionaries.Add(timingLight);Pump();SaveConfiguration(timing,"Timing-Light");Application.Current.Resources.MergedDictionaries.Remove(timingLight);timing.Close();
    var consumerProxy=configuration.Context.Add(configuration.Context.Candidates().Single(c=>c.Node.Id==second.Id&&c.FieldId=="Value"));
    var declaredOutput=configuration.Context.Query.FormalOutputContractReader!(first.Id).Single(o=>o.Name=="Echo");var sourcePort=SunEyeVision.Plugin.SDK.Execution.Parameters.BindingClrAdapter.DescribeOutput(declaredOutput);
    configuration.Context.Write(consumerProxy,new SunEyeVision.Plugin.Contracts.ReferenceSetting(new SunEyeVision.Plugin.Contracts.OutputReference(first.Id,sourcePort.Id,sourcePort.Type,scopeId:source.Id,storage:sourcePort.Storage)));
    Assert.True(configuration.Context.TryApply(out var referenceError),referenceError);Assert.Equal(2,source.RootConfiguration.RunParameters.Length);
    configuration.Context.Write(source.RootConfiguration.RunParameters[0],new SunEyeVision.Plugin.Contracts.ConstantSetting(17L));
    SaveConfiguration(configuration,"Dark");
    var light=new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/Themes/Light/LightTheme.xaml",UriKind.Relative)};
    Application.Current.Resources.MergedDictionaries.Add(light);Pump();SaveConfiguration(configuration,"Light");Application.Current.Resources.MergedDictionaries.Remove(light);
    configuration.Close();Pump();Assert.Equal(11,((RootConfigurationTests.Parameters)first.Parameters).Value);
    // Main toolbar must traverse one chronology across root management, canvas and accepted parameters.
    var sharedHistory=manager.Session!.History;
    var initialPosition=sharedHistory.Position;
    sourceTab.CommandManager.Execute(new SunEyeVision.UI.Features.WorkflowUI.Commands.MoveNodeCommand(first,new Point(100,100),new Point(180,190)));
    var getCanvasViewer=shell.GetType().GetMethod("GetCurrentScrollViewer",BindingFlags.Instance|BindingFlags.NonPublic)!;
    var footer=(SunEyeVision.UI.Shell.Controls.Shell.WorkspaceStatusBarControl)shell.FindName("WorkspaceStatusBar");
    ((Slider)footer.FindName("ZoomSlider")).Value=1.3;Pump();Pump();Assert.Equal(1.3,sourceTab.CurrentScale,6);
    var sourceViewer=(ScrollViewer)getCanvasViewer.Invoke(shell,null)!;
    sourceViewer.ScrollToHorizontalOffset(260);sourceViewer.ScrollToVerticalOffset(390);Pump();Pump();
    Assert.True(viewModel.AddRootWorkspace(),viewModel.StatusText);
    var otherTab=viewModel.WorkflowTabViewModel.SelectedTab!;
    var other=document.Workflows.Single(w=>w.Id==otherTab.RootWorkflowId);
    Pump();Pump();((Slider)footer.FindName("ZoomSlider")).Value=.7;Pump();Pump();Assert.Equal(.7,otherTab.CurrentScale,6);
    var otherViewer=(ScrollViewer)getCanvasViewer.Invoke(shell,null)!;
    otherViewer.ScrollToHorizontalOffset(70);otherViewer.ScrollToVerticalOffset(140);Pump();Pump();
    var navigationPosition=sharedHistory.Position;
    viewModel.SetSelectedTab(sourceTab);Pump();Pump();
    Assert.Equal(1.3,footer.Scale,6);sourceViewer=(ScrollViewer)getCanvasViewer.Invoke(shell,null)!;
    Assert.InRange(sourceViewer.HorizontalOffset,259,261);Assert.InRange(sourceViewer.VerticalOffset,389,391);
    viewModel.SetSelectedTab(otherTab);Pump();Pump();
    Assert.Equal(.7,footer.Scale,6);otherViewer=(ScrollViewer)getCanvasViewer.Invoke(shell,null)!;
    Assert.InRange(otherViewer.HorizontalOffset,69,71);Assert.InRange(otherViewer.VerticalOffset,139,141);
    Assert.Equal(navigationPosition,sharedHistory.Position);
    var third=new SunEyeVision.UI.Shared.WorkflowNode("third","9 测试","测试","RootConfigurationProbe"){GlobalIndex=9,LocalIndex=1,Parameters=new RootConfigurationTests.Parameters(),Position=new Point(220,230)};
    viewModel.AddNodeToWorkflow(third);
    Assert.Contains(third,other.Nodes);
    otherTab.CommandManager.Execute(new SunEyeVision.UI.Features.WorkflowUI.Commands.MoveNodeCommand(third,new Point(220,230),new Point(310,330)));
    Assert.Equal(initialPosition+4,sharedHistory.Position);
    viewModel.SetSelectedTab(sourceTab);Pump();
    undoEdit.Command.Execute(null);Assert.Equal(new Point(220,230),third.Position);Assert.Same(otherTab,viewModel.WorkflowTabViewModel.SelectedTab);
    undoEdit.Command.Execute(null);Assert.Empty(other.Nodes);
    undoEdit.Command.Execute(null);Assert.DoesNotContain(other,document.Workflows);
    undoEdit.Command.Execute(null);Assert.Equal(new Point(100,100),first.Position);
    Assert.Equal(initialPosition,sharedHistory.Position);
    for(var replay=0;replay<4;replay++){Assert.True(redoEdit.Command.CanExecute(null));redoEdit.Command.Execute(null);}
    Assert.Contains(third,other.Nodes);Assert.Equal(new Point(310,330),third.Position);
    for(var replay=0;replay<4;replay++){Assert.True(undoEdit.Command.CanExecute(null));undoEdit.Command.Execute(null);}
    // A new edit drops redo entries from every root, and history locations carry session identity.
    sourceTab.CommandManager.Execute(new SunEyeVision.UI.Features.WorkflowUI.Commands.MoveNodeCommand(first,new Point(100,100),new Point(110,120)));
    Assert.False(redoEdit.Command.CanExecute(null));
    var currentLocation=viewModel.EditHistoryLocations.Single(l=>l.IsCurrent);
    Assert.Equal(sharedHistory.SessionId,currentLocation.SessionId);
    var historyButton=(Button)topBar.FindName("EditHistoryButton");
    historyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
    Assert.Equal(viewModel.EditHistoryLocations.Count,historyButton.ContextMenu.Items.Count);
    Assert.All(historyButton.ContextMenu.Items.OfType<MenuItem>(),item=>Assert.Same(viewModel.NavigateEditHistoryCommand,item.Command));
    historyButton.ContextMenu.IsOpen=false;
    var closeWait=System.Diagnostics.Stopwatch.StartNew();while(historyButton.ContextMenu!=null&&closeWait.Elapsed<TimeSpan.FromSeconds(1)){Pump();Thread.Sleep(1);}
    Assert.Null(historyButton.ContextMenu);
    Assert.False(viewModel.NavigateEditHistoryCommand.CanExecute(currentLocation with{SessionId=Guid.NewGuid()}));
    viewModel.NavigateEditHistoryCommand.Execute(viewModel.EditHistoryLocations[^2]);Assert.Equal(new Point(100,100),first.Position);
    // This undo is the accepted reference configuration, not an unrelated canvas stack.
    undoEdit.Command.Execute(null);Assert.Single(source.RootConfiguration.RunParameters);
    redoEdit.Command.Execute(null);Assert.Equal(2,source.RootConfiguration.RunParameters.Length);
    Assert.Same(first,source.Nodes[0]);Assert.Equal(11,((RootConfigurationTests.Parameters)first.Parameters).Value);
    viewModel.SetSelectedTab(sourceTab);Pump();
    var overviewFrame=viewModel.CaptureRootOverview();
    Assert.False(viewModel.CommitOverviewPosition(new(source.Id,100,110,"stale-document",overviewFrame.Revision),false));
    Assert.True(viewModel.CommitOverviewPosition(new(source.Id,102,108,overviewFrame.Identity,overviewFrame.Revision),false));
    Assert.Equal(100,source.WorkspaceSettings.OverviewX);Assert.Equal(110,source.WorkspaceSettings.OverviewY);
    viewModel.UndoRootWorkspace();Assert.Null(source.WorkspaceSettings.OverviewX);viewModel.RedoRootWorkspace();Assert.Equal(100,source.WorkspaceSettings.OverviewX);viewModel.UndoRootWorkspace();
    overviewFrame=viewModel.CaptureRootOverview();Assert.True(viewModel.CommitOverviewPosition(new(source.Id,300,450,overviewFrame.Identity,overviewFrame.Revision),true));
    var overviewCopy=document.Workflows.Single(w=>w.Id!=source.Id);Assert.Equal(300,overviewCopy.WorkspaceSettings.OverviewX);Assert.Equal(450,overviewCopy.WorkspaceSettings.OverviewY);
    viewModel.UndoRootWorkspace();Assert.Single(document.Workflows);Assert.Same(sourceTab,viewModel.WorkflowTabViewModel.SelectedTab);
    shell.SetOverviewVisible(true);Pump();SaveConfiguration(owner,"Overview-Dark");
    var overviewLight=new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/Themes/Light/LightTheme.xaml",UriKind.Relative)};Application.Current.Resources.MergedDictionaries.Add(overviewLight);Pump();SaveConfiguration(owner,"Overview-Light");Application.Current.Resources.MergedDictionaries.Remove(overviewLight);shell.SetOverviewVisible(false);
    Assert.True(viewModel.CopyRootWorkspace(sourceTab),viewModel.StatusText);
    var copied=document.Workflows.Single(w=>!w.IsSubroutine&&w.Id!=source.Id);var copyTab=viewModel.WorkflowTabViewModel.SelectedTab!;
    Assert.Equal(copied.Nodes[0].Id,copied.RootConfiguration.RunParameters[0].NodeId);Assert.NotEqual(proxy.Id,copied.RootConfiguration.RunParameters[0].Id);
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
    Assert.Equal(Visibility.Visible,((FrameworkElement)shell.FindName("RootEmptyPanel")).Visibility);Assert.Empty(((System.Windows.Controls.Canvas)shell.FindName("MiniMapCanvas")).Children);Assert.True(shell.RootWorkspaceEditingAllowed);Assert.True(undoEdit.Command.CanExecute(null));
    viewModel.UndoRootWorkspace();Assert.Single(document.Workflows);viewModel.UndoRootWorkspace();Assert.Equal(2,document.Workflows.Count);
    Pump();Assert.Equal(Visibility.Collapsed,((FrameworkElement)shell.FindName("RootEmptyPanel")).Visibility);
    var directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"root-host");Directory.CreateDirectory(directory);var path=Path.Combine(directory,"根流程验收.json");
    var saved=manager.SaveCurrentDocument(path);Assert.True(saved.FileWritten,saved.Error);var reopened=new SolutionRepository().Load(path);
    Assert.Equal(2,reopened.Workflows.Count);var loadedCopy=reopened.Workflows.Single(w=>w.Id==copyId);Assert.Equal(41,loadedCopy.WorkspaceSettings.RunIntervalMs);Assert.Equal(copied.Nodes.Select(n=>n.Id),loadedCopy.Nodes.Select(n=>n.Id));
    Assert.Equal(11,((RootConfigurationTests.Parameters)loadedCopy.Nodes[0].Parameters).Value);Assert.Equal(2,loadedCopy.RootConfiguration.RunParameters.Length);
    var engine=new WorkflowEngine(VisionLogger.Instance);foreach(var flow in reopened.Workflows)engine.RegisterWorkflow(flow);
    var runner=new WorkflowExecutionEngine(engine,new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginManager(VisionLogger.Instance),VisionLogger.Instance);
    var runTask=runner.ExecuteWorkflowAsync(copyId,null);var runWait=System.Diagnostics.Stopwatch.StartNew();while(!runTask.IsCompleted&&runWait.Elapsed<TimeSpan.FromSeconds(15)){Pump();Thread.Sleep(1);}Assert.True(runTask.IsCompleted,"正式引擎任务未完成");using var run=runTask.GetAwaiter().GetResult();Assert.True(run.Success,string.Join(";",run.Errors));Assert.Equal(2,run.NodeResults.Count);Assert.All(run.NodeResults.Values,result=>Assert.Equal(11,Assert.IsType<RootConfigurationTests.Results>(result.Result).Echo));
    for(int i=0;i<8;i++)Assert.True(viewModel.AddRootWorkspace());
    owner.Width=540;Pump();owner.UpdateLayout();Pump();
    var viewer=(ScrollViewer)tabs.Template.FindName("TabPanelScrollViewer",tabs);
    var fixedAdd=(Border)tabs.Template.FindName("FixedAddButtonBorder",tabs);
    WaitForLayout(()=>Math.Abs(owner.ActualWidth-owner.Width)<1&&viewer.ViewportWidth>0&&
      ((FrameworkElement)tabs.Template.FindName("HeaderPanel",tabs)).IsArrangeValid);
    Console.WriteLine($"Tab overflow diagnostic: owner={owner.Width}/{owner.ActualWidth}; shell={shell.ActualWidth}; tabs={tabs.ActualWidth}; viewer={viewer.ActualWidth}/{viewer.ViewportWidth}/{viewer.ExtentWidth}; headers={((FrameworkElement)tabs.Template.FindName("HeaderPanel",tabs)).ActualWidth}; overview={shell.IsRootOverviewVisible}; fixed={fixedAdd.Visibility}");
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
 private static void VerifySourceEditing(MainWindowViewModel main,SunEyeVision.Workflow.Workflow root,SunEyeVision.UI.Features.WorkflowUI.WorkflowTabViewModel tab)
 {
  var manager=ServiceInitializer.SolutionManager;var history=manager.Session!.History;var opening=history.Position;
  var node=new SunEyeVision.UI.Shared.WorkflowNode("image-source-admission","图像来源","图像来源","ImageLoad")
   {RuntimeConfiguration=ImageSourceTestRuntime.Configuration()};
  tab.CommandManager.Execute(new SunEyeVision.UI.Features.WorkflowUI.Commands.AddNodeCommand(root.Nodes,node));
  var source=node.EnsureInputSource();
  typeof(MainWindowViewModel).GetMethod("BindLocalImageSourceEditor",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,new object[]{node,source});
  var before=history.Position;
  source.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"missing-a.png")},
   new SunEyeVision.UI.Shared.ImageInfo{FilePath=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"missing-b.png")}},selectFirstAdded:true);
  Assert.Equal(before+1,history.Position);Assert.Equal(2,ImageSourceTestRuntime.Read(node).FilePaths.Count);Assert.Equal(0,ImageSourceTestRuntime.Read(node).CurrentIndex);
  history.Undo();Assert.Empty(ImageSourceTestRuntime.Read(node).FilePaths);Assert.Empty(source.Images);
  history.Redo();Assert.Equal(2,source.Count);Assert.Equal(0,source.CurrentIndex);
  source.Images[1].IsForRun=true;Assert.Single(ImageSourceTestRuntime.Read(node).ForRunPaths);
  history.Undo();Assert.Empty(ImageSourceTestRuntime.Read(node).ForRunPaths);Assert.False(source.Images[1].IsForRun);
  history.Redo();Assert.True(source.Images[1].IsForRun);
  // The actual Runtime source is formal; prepare only its private display
  // selection via the existing setter, without opening a production editing bypass.
  void SetActiveSource(SunEyeVision.UI.Shared.ImageInputSource? value)
  {
   typeof(MainWindowViewModel).GetProperty(nameof(MainWindowViewModel.ActiveInputSource))!
    .GetSetMethod(nonPublic:true)!.Invoke(main,new object?[]{value});
   Assert.Same(value,main.ActiveInputSource);
  }
  var previousSource=main.ActiveInputSource;SetActiveSource(source);
  try
  {
   Assert.True(main.TrySelectPreviewImage(tab,source,source.Images[0],0,source.EditVersion));
   var position=history.Position;
   Assert.True(main.TrySelectPreviewImage(tab,source,source.Images[0],0,source.EditVersion));
   Assert.Equal(position,history.Position); // Explicit same-value selection resets execution, without another history entry.
   EventHandler switchSource=(_,_)=>SetActiveSource(previousSource);
   source.ConfigurationEdited+=switchSource;
   try{Assert.False(main.TrySelectPreviewImage(tab,source,source.Images[0],0,source.EditVersion));}
   finally{source.ConfigurationEdited-=switchSource;}
  }
  finally{SetActiveSource(previousSource);}
  VerifySourceBaseAfterSave(main,node,root.Id,source);
  VerifySourceSettings(main,node,history);
  while(history.Position>opening)history.Undo();
  Assert.DoesNotContain(node,root.Nodes);
  Assert.Throws<InvalidOperationException>(()=>source.AddImage(new SunEyeVision.UI.Shared.ImageInfo{FilePath="stale.png"}));
  Assert.Empty(source.Images);
 }
 private static void VerifySourceBaseAfterSave(MainWindowViewModel main,SunEyeVision.UI.Shared.WorkflowNode node,string rootId,
  SunEyeVision.UI.Shared.ImageInputSource source)
 {
  var manager=ServiceInitializer.SolutionManager;var session=manager.Session!;
  var data=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"source-save-base");
  var a=Path.Combine(data,"a");var b=Path.Combine(data,"b");Directory.CreateDirectory(a);Directory.CreateDirectory(b);
  void WritePixel(string directory,byte pixel)
  {
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new[]{pixel},1)));
   using var file=File.Create(Path.Combine(directory,"pixel.png"));encoder.Save(file);
  }
  WritePixel(a,30);WritePixel(b,220);
  session.Document.ResourceBaseDirectory=a;
  var edit=session.Editing.Host.BeginEdit(rootId,node.Id);
  ImageSourceTestRuntime.Edit(edit.Node,policy=>{policy.FilePaths=new(){"pixel.png"};policy.ForRunPaths.Clear();policy.CurrentIndex=0;});
  session.Editing.Host.Commit(edit);
  typeof(MainWindowViewModel).GetMethod("RefreshAcceptedImageSourceViews",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null);
  var old=Assert.Single(source.Images);var version=source.EditVersion;
  Assert.Equal(Path.Combine(a,"pixel.png"),old.FilePath);
  var frozen=SunEyeVision.UI.Services.Workflow.WorkflowExecutionManager.CaptureLocalImagePlan(node.Id,ImageSourceTestRuntime.Read(node),
   SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(session.Document));
  var position=session.History.Position;
  var saved=manager.SaveCurrentDocument(Path.Combine(b,"source-base.json"));Assert.True(saved.FileWritten,saved.Error);
  Assert.Equal(position,session.History.Position);
  var current=Assert.Single(source.Images);Assert.NotSame(old,current);Assert.Equal(Path.Combine(b,"pixel.png"),current.FilePath);
  Assert.Equal(new[]{"pixel.png"},ImageSourceTestRuntime.Read(node).FilePaths);
  Assert.False(source.TryPresentRuntimeCursor(version,old.Id,"pixel.png",old.FilePath));
  Assert.Equal(Path.Combine(a,"pixel.png"),Assert.Single(frozen.Entries).Address);
  var latest=SunEyeVision.UI.Services.Workflow.WorkflowExecutionManager.CaptureLocalImagePlan(node.Id,ImageSourceTestRuntime.Read(node),
   SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(session.Document));
  Assert.Equal(current.FilePath,Assert.Single(latest.Entries).Address);
 }
 private static void VerifySourceSettings(MainWindowViewModel main,SunEyeVision.UI.Shared.WorkflowNode node,DocumentHistoryCoordinator history)
 {
  var field=typeof(MainWindowViewModel).GetField("_openDebugWindow",BindingFlags.Instance|BindingFlags.NonPublic)!;
  Assert.Null(field.GetValue(main));
  typeof(MainWindowViewModel).GetMethod("ExecuteOpenDebugWindow",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,new object[]{node});
  Window? window=null;SunEyeVision.UI.Services.Editing.RuntimeNodeConfigurationControl? page=null;
  var wait=System.Diagnostics.Stopwatch.StartNew();
  while(wait.Elapsed<TimeSpan.FromSeconds(8))
  {
   Pump();window=field.GetValue(main) as Window;
   if(window is {IsLoaded:true})page=SourceDescendants(window).OfType<SunEyeVision.UI.Services.Editing.RuntimeNodeConfigurationControl>().SingleOrDefault();
   if(page is not null)break;
  }
  Assert.NotNull(window);Assert.NotNull(page);
  try
  {
   var content=Assert.IsType<ContentControl>(window!.FindName("ContentHost"));
   Assert.Same(page,content.Content);content.Content=null;Pump();content.Content=page;Pump();
   var picker=SourceDescendants(page!).OfType<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor>().Single(editor=>editor.FieldId=="source.kind");
   var before=history.Position;
   Assert.True(picker.TrySetConstant("camera"));Pump();
   Assert.Equal(before,history.Position); // Tool editor is a private draft, never an immediate document writer.
   Assert.Equal(SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Local,ImageSourceTestRuntime.Read(node).SourceKind);
   Assert.True(page!.TryCommitEditor(out var error),error);Pump();
   Assert.Equal(before+1,history.Position);
   Assert.Equal(SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Camera,ImageSourceTestRuntime.Read(node).SourceKind);
   Assert.Single(ImageSourceTestRuntime.Read(node).FilePaths); // Save-base phase reduced the list to pixel.png.
   history.Undo();Pump();Assert.Equal(SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Local,ImageSourceTestRuntime.Read(node).SourceKind);
   history.Redo();Pump();Assert.Equal(SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Camera,ImageSourceTestRuntime.Read(node).SourceKind);
   // A different live window must not authorize this draft's document commit.
   var unrelated=new Window{Width=100,Height=100,Left=-10000,ShowActivated=false};
   var committed=history.Position;
   try
   {
    unrelated.Show();field.SetValue(main,unrelated);
    Assert.True(picker.TrySetConstant("local"));Pump();
    Assert.False(page.TryCommitEditor(out _));Assert.Equal(committed,history.Position);
    Assert.Equal(SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Camera,ImageSourceTestRuntime.Read(node).SourceKind);
   }
   finally{field.SetValue(main,window);unrelated.Close();}
   Assert.True(picker.TrySetConstant("camera"));Pump();

  }
  finally{window!.Close();Pump();}
  Assert.False(main.IsDebugWindowOpen);
  // A retired actual editor cannot authorize a new formal edit after its window closes.
  var position=history.Position;
  try{Assert.False(page!.TryCommitEditor(out _));}catch(ObjectDisposedException){}
  Assert.Equal(position,history.Position);
 }
 private static IEnumerable<DependencyObject> SourceDescendants(DependencyObject root)
 {
  yield return root;
  for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
   foreach(var child in SourceDescendants(VisualTreeHelper.GetChild(root,i)))yield return child;
 }
 private static void VerifyWorkflowButtonThemes(SunEyeVision.UI.Shell.Controls.Shell.ShellWorkspaceControl shell,string theme)
 {
  var hoverKey=(DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(null)!;
  var focusKey=(DependencyPropertyKey)typeof(UIElement).GetField("IsKeyboardFocusedPropertyKey",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(null)!;
  var pressedKey=(DependencyPropertyKey)typeof(System.Windows.Controls.Primitives.ButtonBase).GetField("IsPressedPropertyKey",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(null)!;
  var panel=new StackPanel{Orientation=Orientation.Horizontal,Background=(Brush)Application.Current.FindResource(SunEyeVision.UI.Themes.Base.ThemeKeys.TabStripBackgroundBrush)};
  var window=new Window{Content=panel,Width=300,Height=90,Left=-10000,ShowActivated=false};
  try
  {
   foreach(var template in new[]{"WorkflowSingleTemplate","WorkflowContinuousTemplate","WorkflowStopTemplate","WorkflowAddTemplate","RootConfigurationTemplate","RootVariablesTemplate"})
    panel.Children.Add(new Button{Style=(Style)shell.FindResource("WorkflowImageButton"),Template=(ControlTemplate)shell.FindResource(template),IsEnabled=true});
   panel.Children.Add(new Button{Style=(Style)shell.FindResource("WorkflowImageButton"),Content="切换",IsEnabled=true});
   window.Show();Pump();
   void Verify(Brush actual,string key)=>Assert.Equal(((SolidColorBrush)Application.Current.FindResource(key)).Color,Assert.IsType<SolidColorBrush>(actual).Color);
   foreach(Button button in panel.Children)
   {
    button.ApplyTemplate();var border=Assert.IsType<Border>(System.Windows.Media.VisualTreeHelper.GetChild(button,0));
    Assert.Equal(Colors.Transparent,Assert.IsType<SolidColorBrush>(border.Background).Color);
    button.SetValue(hoverKey,true);Pump();Verify(border.Background,SunEyeVision.UI.Themes.Base.ThemeKeys.ButtonHoverBackgroundBrush);
    button.SetValue(hoverKey,false);button.SetValue(focusKey,true);Pump();Verify(border.Background,SunEyeVision.UI.Themes.Base.ThemeKeys.ButtonHoverBackgroundBrush);
    button.SetValue(pressedKey,true);Pump();Verify(border.Background,SunEyeVision.UI.Themes.Base.ThemeKeys.ButtonPressedBackgroundBrush);
    button.IsEnabled=false;Pump();Assert.Equal(Colors.Transparent,Assert.IsType<SolidColorBrush>(border.Background).Color);
    button.SetValue(pressedKey,false);button.SetValue(focusKey,false);button.SetValue(hoverKey,false);button.IsEnabled=true;
   }
   foreach(Button button in panel.Children)button.SetValue(hoverKey,true);
   Pump();SaveMenuVisual(panel,"workflow-buttons-"+theme+"-hover");
  }
  finally{window.Close();}
 }
 private static void VerifyMiniMap(SunEyeVision.UI.Shell.Controls.Shell.ShellWorkspaceControl shell,Window owner,string theme)
 {
  var host=(FrameworkElement)shell.FindName("MiniMapHost");var map=(Border)shell.FindName("MiniMapBorder");
  var mapCanvas=(System.Windows.Controls.Canvas)shell.FindName("MiniMapCanvas");
  var retract=(Button)shell.FindName("MiniMapMinimizeButton");var expand=(Button)shell.FindName("MiniMapRestoreButton");
  Assert.Equal(187,mapCanvas.ActualWidth);Assert.Equal(150,mapCanvas.ActualHeight);Assert.Equal(new Thickness(0),map.BorderThickness);
  var sample=Assert.IsType<VisualBrush>(Assert.IsType<System.Windows.Shapes.Rectangle>(mapCanvas.Children[0]).Fill);
  var drawing=new DrawingVisual();using(var context=drawing.RenderOpen())context.DrawRectangle(sample,null,new Rect(0,0,187,150));
  var bitmap=new RenderTargetBitmap(187,150,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);var pixels=new byte[187*150*4];bitmap.CopyPixels(pixels,187*4,0);
  Assert.True(Enumerable.Range(0,187*150).Count(index=>pixels[index*4+3]<255)>187*150/2,"小地图不得采样独立的实色底层");
  var hoverKey=(DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(null)!;
  var pressedKey=(DependencyPropertyKey)typeof(System.Windows.Controls.Primitives.ButtonBase).GetField("IsPressedPropertyKey",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(null)!;
  void CheckButton(Button button)
  {
   button.ApplyTemplate();button.UpdateLayout();Assert.Equal(32,button.ActualWidth);Assert.Equal(23,button.ActualHeight);
   var image=Assert.IsType<Image>(button.Content);var source=Assert.IsAssignableFrom<BitmapSource>(image.Source);Assert.Equal(32,source.PixelWidth);Assert.Equal(23,source.PixelHeight);
   var bounds=image.TransformToAncestor(button).TransformBounds(new Rect(image.RenderSize));Assert.InRange(bounds.Left,-.01,.01);Assert.InRange(bounds.Top,-.01,.01);Assert.InRange(bounds.Right,31.99,32.01);Assert.InRange(bounds.Bottom,22.99,23.01);
   var inOwner=button.TransformToAncestor(owner).TransformBounds(new Rect(button.RenderSize));Assert.True(inOwner.Right<owner.ActualWidth&&inOwner.Bottom<owner.ActualHeight);
  }
  CheckButton(retract);SaveMenuVisual(host,"minimap-"+theme+"-normal");
  retract.SetValue(hoverKey,true);Pump();var chrome=(Border)retract.Template.FindName("Chrome",retract);
  Assert.Equal(((SolidColorBrush)Application.Current.FindResource(SunEyeVision.UI.Themes.Base.ThemeKeys.ButtonHoverBackgroundBrush)).Color,Assert.IsType<SolidColorBrush>(chrome.Background).Color);
  SaveMenuVisual(host,"minimap-"+theme+"-hover");
  retract.SetValue(pressedKey,true);Pump();Assert.Equal(((SolidColorBrush)Application.Current.FindResource(SunEyeVision.UI.Themes.Base.ThemeKeys.ButtonPressedBackgroundBrush)).Color,Assert.IsType<SolidColorBrush>(chrome.Background).Color);SaveMenuVisual(host,"minimap-"+theme+"-pressed");
  retract.IsEnabled=false;Pump();Assert.Equal(Colors.Transparent,Assert.IsType<SolidColorBrush>(chrome.Background).Color);retract.IsEnabled=true;Pump();
  retract.SetValue(pressedKey,false);retract.SetValue(hoverKey,false);retract.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
  Assert.Equal(Visibility.Collapsed,map.Visibility);Assert.Equal(Visibility.Visible,expand.Visibility);CheckButton(expand);
  expand.SetValue(hoverKey,true);Pump();SaveMenuVisual(host,"minimap-"+theme+"-collapsed-hover");expand.SetValue(hoverKey,false);
  expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();Assert.Equal(Visibility.Visible,map.Visibility);Assert.Equal(Visibility.Collapsed,expand.Visibility);
 }
 private static void SaveMenuVisual(FrameworkElement visual,string name)
 {
  visual.UpdateLayout();BitmapSource bitmap;
  var owner=Window.GetWindow(visual);
  if(owner!=null&&owner.IsAncestorOf(visual))
  {
   // 右下角覆盖层带父级布局偏移：先渲染真实窗口，再以窗口坐标裁取，避免空图。
   var whole=new RenderTargetBitmap((int)Math.Ceiling(owner.ActualWidth),(int)Math.Ceiling(owner.ActualHeight),96,96,PixelFormats.Pbgra32);whole.Render(owner);
   var bounds=visual.TransformToAncestor(owner).TransformBounds(new Rect(visual.RenderSize));
   var left=(int)Math.Floor(bounds.Left);var top=(int)Math.Floor(bounds.Top);
   bitmap=new CroppedBitmap(whole,new Int32Rect(left,top,(int)Math.Ceiling(bounds.Right)-left,(int)Math.Ceiling(bounds.Bottom)-top));
  }
  else
  {
   var direct=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);direct.Render(visual);bitmap=direct;
  }
  var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);Assert.Contains(Enumerable.Range(0,bitmap.PixelWidth*bitmap.PixelHeight),index=>pixels[index*4+3]>0);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name+".png"));encoder.Save(output);
 }
 private static void SaveConfiguration(Window window,string theme)
 {
  Pump();window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
  using var stream=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"root-configuration-"+theme+".png"));encoder.Save(stream);
 }
 private static void SaveHeader(TabControl tabs,string theme)
 {
  var bar=(FrameworkElement)tabs.Template.FindName("TabHeaderBar",tabs);bar.UpdateLayout();
  var image=new RenderTargetBitmap((int)Math.Ceiling(bar.ActualWidth),(int)Math.Ceiling(bar.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(bar);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"root-tabs-"+theme+".png"));encoder.Save(file);
 }
 private static void WaitForLayout(Func<bool> ready)
 {
  var frame=new System.Windows.Threading.DispatcherFrame();var timeout=System.Diagnostics.Stopwatch.StartNew();
  var timer=new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background)
   {Interval=TimeSpan.FromMilliseconds(15)};
  timer.Tick+=(_,_)=>{if(ready()||timeout.Elapsed>TimeSpan.FromSeconds(3)){timer.Stop();frame.Continue=false;}};
  timer.Start();try{System.Windows.Threading.Dispatcher.PushFrame(frame);}finally{timer.Stop();}
  Assert.True(ready(),"原生窗口缩放后的布局未在时限内完成");
 }
 private static void Pump()
 {
  var frame=new System.Windows.Threading.DispatcherFrame();System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(frame);
 }
}

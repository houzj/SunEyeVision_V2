using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Infrastructure.Persistence.ApplicationData;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Shell.Windows;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("正式WPF owner")]
public sealed class FormalCanvasCompositionTests
{
 [Fact] public void Actual_session_canvas_composition_uses_same_node_template_identity_and_graph_history()=>SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
 {
  var root=typeof(FormalCanvasCompositionTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item=>item.Key=="HostDebugRoot").Value!;
  AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",root);var plugins=new PluginManager();plugins.LoadPluginsAsync().GetAwaiter().GetResult();Assert.Null(plugins.LastAdmissionError);
  var pin=plugins.PinDesktopHost();var directory=Path.GetFullPath(Path.Combine(root,"..","..","_validation","ShellLayout",Guid.NewGuid().ToString("N")));
  var paths=ApplicationDataPaths.Open(Path.Combine(directory,"Data"));ServiceInitializer.ConfigureDataPaths(paths);ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(paths,plugins.DesktopAdmission!.Runtime);var theme=new ThemeManager(()=>null,_=>{});theme.Initialize();ServiceLocator.Instance.Register(theme);
  var session=ServiceInitializer.SolutionManager.CreateNew("实际布局与画布");var main=new MainWindowViewModel(plugins.DesktopAdmission);var workspace=new ShellWorkspaceControl{DataContext=main.Workspace};workspace.Initialize(main);
  var window=new Window{Content=workspace,DataContext=main,Width=1000,Height=600,Left=-32000,Top=-32000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};
  try
  {
   window.Show();Pump();window.UpdateLayout();var canvas=main.WorkflowTabViewModel.SelectedTab!.DocumentCanvas!;Assert.Empty(canvas.Nodes);
   var first=canvas.CreateNode("ImageLoad",new(150,120));var second=canvas.CreateNode("BlobAnalysis",new(350,240));Pump();window.UpdateLayout();
   var view=Assert.IsType<WorkflowCanvasControl>(workspace.GetCurrentWorkflowCanvas());Assert.Same(main.WorkflowTabViewModel.SelectedTab,view.DataContext);var items=Assert.IsType<ItemsControl>(view.FindName("CanvasNodeItems"));Assert.Same(canvas.Nodes,items.ItemsSource);Assert.Same(first,canvas.Nodes[0].Definition);Assert.Same(second,canvas.Nodes[1].Definition);
   canvas.Connect(new(Guid.NewGuid().ToString("N"),first.Id,second.Id,"BottomPort","TopPort"));Assert.Single(canvas.Connections);session.Undo();Assert.Empty(canvas.Connections);session.Redo();Assert.Single(canvas.Connections);
   var original=first.Position;canvas.MoveNodes(new Dictionary<CanvasNodeProjection,SunEyeVision.Domain.Workflows.CanvasPoint>{{canvas.Nodes[0],new(200,175)}});Assert.Equal(200,first.Position.X);session.Undo();Assert.Equal(original,first.Position);Assert.Same(first,session.FindNode(session.GetWorkflows().Single().Id,first.Id));
   main.SelectedNode=first;Assert.Same(first,main.SelectedNode);Assert.True(main.CanEditWorkflowCanvas);
  }
  finally
  {
   main.ReleaseDocumentHost();window.Content=null;window.Close();ServiceInitializer.DisposeCommunicationRuntimeAsync().AsTask().GetAwaiter().GetResult();ServiceInitializer.DisposeCameraRuntimeAsync().AsTask().GetAwaiter().GetResult();ServiceInitializer.SolutionManager.CloseCurrent(true);pin.Dispose();plugins.UnloadPluginsAsync().GetAwaiter().GetResult();Pump();
  }
 });
 private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
}

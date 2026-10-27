using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Shell.Windows;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("VM canvas UI")]
public sealed class WorkspaceTimingPresentationTests
{
 [Fact]
 public void Timing_view_uses_source_dimensions_shared_snapshot_and_releases_only_view_bindings()
 {
  OnSta(() =>
  {
   var snapshot=new WorkflowExecutionSnapshot("root-1","流程1");
   snapshot.Nodes.Add(new WorkflowNodeExecutionSnapshot("node-1","图像源1","尚未运行"));
   var window=new WorkflowExecutionSnapshotWindow(snapshot);
   Assert.Equal(370,window.Width);Assert.Equal(420,window.Height);
   Assert.Equal(370,window.MinWidth);Assert.Equal(260,window.MinHeight);
   Assert.Equal("耗时查看",window.Title);
   Assert.Same(snapshot,window.DataContext);
   Pump();
   var list=Assert.IsType<ListView>(window.FindName("TimingList"));
   Assert.Same(snapshot.Nodes,list.ItemsSource);
   Assert.Equal(1,list.Items.Count);
   window.Close();
   Assert.Null(window.DataContext);Assert.Null(list.ItemsSource);
   Assert.Single(snapshot.Nodes); // 关闭视图不得清空执行管理器拥有的快照。
  });
 }

 [Fact]
 public void Footer_defaults_to_unavailable_real_node_timings_and_routes_zoom_to_host()
 {
  OnSta(() =>
  {
   var footer=new WorkspaceStatusBarControl();
   Assert.Equal("—",footer.ModuleTimeText);Assert.Equal("—",footer.AlgorithmTimeText);
   Assert.Equal(WorkspaceConfigurationState.Waiting,footer.ConfigurationState);
   double? requested=null;footer.ScaleRequested+=value=>requested=value;
   footer.Scale=1.5;
   var reset=Assert.IsType<Button>(footer.FindName("ResetZoomButton"));
   reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Assert.Equal(1d,requested);Assert.Equal(1.5,footer.Scale); // 呈现层不直接变更宿主视口。
   footer.ModuleTimeText="12.34";footer.AlgorithmTimeText="5.67";Pump();
   Assert.Equal("12.34",Assert.IsType<TextBlock>(footer.FindName("ModuleTimeValue")).Text);
   Assert.Equal("5.67",Assert.IsType<TextBlock>(footer.FindName("AlgorithmTimeValue")).Text);
  });
 }

 private static void Pump()
 {
  var frame=new System.Windows.Threading.DispatcherFrame();System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>frame.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(frame);
 }
 [Fact]
 public void Missing_result_timing_is_distinct_from_measured_zero_and_new_run_clears_flags()
 {
  var snapshot=new WorkflowExecutionSnapshot("root","流程");Assert.Equal("—",snapshot.FlowElapsedText);
  snapshot.UpdateNodeStatus("node","节点","运行中");var node=Assert.Single(snapshot.Nodes);Assert.False(node.HasModuleTiming);Assert.False(node.HasAlgorithmTiming);
  snapshot.UpdateNodeResult("node","节点",new SunEyeVision.Plugin.SDK.Execution.Results.NodeExecutionResult{NodeId="node",Status=SunEyeVision.Plugin.SDK.Execution.Results.ExecutionStatus.Cancelled});
  Assert.False(node.HasModuleTiming);Assert.False(node.HasAlgorithmTiming);
  snapshot.UpdateNodeResult("node","节点",new SunEyeVision.Plugin.SDK.Execution.Results.NodeExecutionResult{NodeId="node",StartTime=new DateTime(2026,10,27),EndTime=new DateTime(2026,10,27),Status=SunEyeVision.Plugin.SDK.Execution.Results.ExecutionStatus.Success,Result=new RootConfigurationTests.Results{ExecutionTimeMs=0}});
  Assert.True(node.HasModuleTiming);Assert.True(node.HasAlgorithmTiming);Assert.Equal(0,node.AlgorithmElapsedMs);
  snapshot.Begin("流程");Assert.False(node.HasModuleTiming);Assert.False(node.HasAlgorithmTiming);
 }
 private static void OnSta(Action action)
 {
  Exception? failure=null;
  var thread=new Thread(()=>{try{action();}catch(Exception ex){failure=ex;}});
  thread.SetApartmentState(ApartmentState.STA);thread.Start();
  Assert.True(thread.Join(TimeSpan.FromSeconds(20)),"WPF计时呈现检查超时");
  if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
 }
}

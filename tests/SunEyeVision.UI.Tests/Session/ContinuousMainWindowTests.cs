using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
using SunEyeVision.UI.Services.Workflow;
using System.Windows.Controls;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class ContinuousMainWindowTests
{
 [Fact] public void Real_blob_business_ng_stops_main_window_without_replaying_history()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var source=f.Add();var blob=f.Add("BlobAnalysis",350,210);f.Connect(source,blob);f.ConfigureImage(source,f.Image());f.Fields(source,("local.auto-switch",new ConstantSetting(false)));
  f.Fields(blob,("field:99a8e45eec7f406b93ab7226c73744ce",new ConstantSetting(true)),("field:4dd2475077314fc68baf68900d8d2b93",new ConstantSetting(2L)));
  var root=f.Session.Editing.Host.BeginRootEdit(f.Root.Id);root.WorkspaceSettings=root.WorkspaceSettings with{StopOnNg=true,RunIntervalMs=0};f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,roots:new[]{root}));f.Save();var history=f.Session.HistoryEntries.Count;
  f.Main.ToggleContinuousAllCommand.Execute(null);var task=Running(f);var run=UiRuntime.Field<DocumentContinuousRun>(f.Main,"_formalContinuousRun")!;var rounds=new List<ContinuousRoundSummary>();run.RoundCompleted+=rounds.Add;UiRuntime.Await(task);Until(()=>!f.Main.IsRunning);
  Assert.True(Assert.Single(Assert.Single(rounds).Workflows).HasBusinessNg);Assert.Contains("NG",f.Main.StatusText);Assert.Equal(history,f.Session.HistoryEntries.Count);Assert.False(f.Session.HasUnsavedChanges);using var result=f.Session.AcquirePublishedResult(f.Root.Id,blob.Id);Assert.True(result?.Snapshot.IsSuccess);
 });
 [Fact] public void All_roots_filters_only_empty_roots_and_rejects_unready_nonempty_batch()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var source=f.Add();f.ConfigureImage(source,f.Image());f.Fields(source,("local.auto-switch",new ConstantSetting(false)));Interval(f,500);f.Save();
  var file=System.IO.Path.Combine(f.DirectoryPath,"document.solution");var document=System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(file))!;
  var flows=document["Workflows"]!.AsArray();var empty=flows[0]!.DeepClone();empty["Id"]="empty-root";empty["Name"]="空根";empty["Nodes"]=new System.Text.Json.Nodes.JsonArray();empty["Connections"]=new System.Text.Json.Nodes.JsonArray();flows.Add(empty);
  var mixed=System.IO.Path.Combine(f.DirectoryPath,"mixed.solution");System.IO.File.WriteAllText(mixed,document.ToJsonString());
  var firstOpen=(Task<bool>)UiRuntime.Call(f.Main,"OpenExternalSolutionAsync",mixed,false)!;UiRuntime.Await(firstOpen);Assert.True(firstOpen.Result);
  f.Main.ToggleContinuousAllCommand.Execute(null);var task=Running(f);var run=UiRuntime.Field<DocumentContinuousRun>(f.Main,"_formalContinuousRun")!;var summaries=new List<ContinuousRoundSummary>();run.RoundCompleted+=summaries.Add;Until(()=>run.State.Phase==ContinuousPhase.Waiting);
  Assert.Single(summaries.Single().Workflows);Assert.Equal(f.Root.Id,summaries.Single().Workflows.Single().WorkflowId);Assert.False(f.Main.WorkflowTabViewModel.Tabs.Single(tab=>tab.RootWorkflowId=="empty-root").IsRunning);
  f.Main.StopWorkflowCommand.Execute(null);UiRuntime.Await(task);Until(()=>!f.Main.IsRunning);
  var invalid=flows[0]!.DeepClone();invalid["Id"]="unready-root";invalid["Name"]="未就绪非空根";var node=invalid["Nodes"]![0]!;node["Id"]="unready-node";
  // Break an automatically owned image binding through genuine graph edits,
  // after reopening the same V4 document with both actual nonempty roots.
  flows.Add(invalid);var bad=System.IO.Path.Combine(f.DirectoryPath,"bad.solution");System.IO.File.WriteAllText(bad,document.ToJsonString());
  var opened=(Task<bool>)UiRuntime.Call(f.Main,"OpenExternalSolutionAsync",bad,false)!;UiRuntime.Await(opened);Assert.True(opened.Result);
  var session=SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.CurrentSession!;var graph=session.PrepareGraphEdit("unready-root");var blob=session.CreateNode(graph,"BlobAnalysis",new(350,210));var edge=new SunEyeVision.Domain.Workflows.ConnectionDefinition("broken-input","unready-node",blob.Id,"BottomPort","TopPort");graph.AddConnection(edge);session.CommitGraphEdit(graph,"建立实际自动图像绑定");var disconnect=session.PrepareGraphEdit("unready-root");disconnect.RemoveConnection(edge);session.CommitGraphEdit(disconnect,"删除来源后保留明确待重绑状态");Assert.NotEmpty(session.CheckWorkflowReadiness("unready-root"));UiRuntime.Pump();
  f.Main.ToggleContinuousAllCommand.Execute(null);Assert.False(f.Main.IsRunning);Assert.All(f.Main.WorkflowTabViewModel.Tabs,tab=>Assert.False(tab.IsRunning));
 });
 private static void Until(Func<bool> condition){var end=DateTime.UtcNow.AddSeconds(20);while(!condition()){if(DateTime.UtcNow>end)throw new TimeoutException("真实连续主窗未达到预期状态。");UiRuntime.Pump();Thread.Sleep(1);}}
 private static void Interval(DocumentUiFixture f,int milliseconds){var draft=f.Session.Editing.Host.BeginRootEdit(f.Root.Id);draft.WorkspaceSettings=draft.WorkspaceSettings with{RunIntervalMs=milliseconds};f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,roots:new[]{draft}));UiRuntime.Pump();}
 private static Task Running(DocumentUiFixture f)=>UiRuntime.Field<Task>(f.Main,"_formalRunTask")??throw new Exception(f.Main.StatusText);
 [Fact] public void Actual_toolbar_continuous_runs_real_frames_and_presents_frozen_timing_without_dirty_history()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var source=f.Add();var blob=f.Add("BlobAnalysis",350,210);f.Connect(source,blob);
  var port=f.Session.GetNodeSchema(f.Root.Id,source.Id)!.Outputs.Single(port=>port.Id=="output.image");f.Fields(blob,("input.image",new ReferenceSetting(new OutputReference(source.Id,port.Id,port.Type,new[]{"data"},scopeId:f.Root.Id))));
  f.ConfigureImage(source,f.Image(10),f.Image(30),f.Image(60),f.Image(90));f.Fields(source,("local.auto-switch",new ConstantSetting(true)),("local.stop-at-last",new ConstantSetting(true)));Interval(f,5);f.Main.SelectedNode=blob;f.Save();var history=f.Session.HistoryEntries.Count;
  Assert.True(f.Main.ToggleContinuousAllCommand.CanExecute(null));f.Main.ToggleContinuousAllCommand.Execute(null);var run=UiRuntime.Field<DocumentContinuousRun>(f.Main,"_formalContinuousRun")!;var summaries=new List<ContinuousRoundSummary>();run.RoundCompleted+=summaries.Add;var task=Running(f);
  Assert.True(f.Main.IsRunning);Assert.True(f.Main.IsContinuousRunning);Assert.False(f.Main.SaveCurrentSolutionCommand.CanExecute(null));UiRuntime.Await(task);Until(()=>!f.Main.IsRunning);
  Assert.NotEmpty(summaries);var final=summaries.Last().Workflows.Single();var snapshot=Assert.IsType<WorkflowExecutionSnapshot>(UiRuntime.Call(f.Main,"GetFormalExecutionSnapshot",f.Root.Id));
  Assert.Equal(final.ExecutionTime,snapshot.Elapsed);foreach(var item in final.Nodes){var actual=snapshot.Nodes.Single(node=>node.NodeId==item.NodeId);Assert.Equal(item.Duration.HasValue,actual.HasModuleTiming);if(item.Duration.HasValue)Assert.Equal(item.Duration.Value,actual.ModuleElapsed);if(item.AlgorithmExecutionTimeMs.HasValue)Assert.Equal(item.AlgorithmExecutionTimeMs.Value,actual.AlgorithmElapsedMs);}
  using var result=f.Session.AcquirePublishedResult(f.Root.Id,blob.Id);Assert.True(result?.Snapshot.IsSuccess);Assert.Equal(history,f.Session.HistoryEntries.Count);Assert.False(f.Session.HasUnsavedChanges);Assert.True(f.Tab.IsRootConfigurationReady);Assert.False(f.Tab.IsRunning);
 });
 [Fact] public void A_thumbnail_queued_at_the_last_frame_stops_after_that_continuous_round()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();
  var source=f.Add();
  var blob=f.Add("BlobAnalysis",350,210);
  var paths=new[]{f.Image(150,6),f.Image(160,6),f.Image(170,6),f.Image(180,6)};
  f.ConfigureImage(source,paths);
  f.Connect(source,blob);
  f.Fields(source,("local.auto-switch",new ConstantSetting(true)),("local.stop-at-last",new ConstantSetting(true)));
  Interval(f,1000);
  f.Main.SelectedNode=source;
  UiRuntime.Pump();
  var strip=f.Main.ActiveImageSourceBinding!.Strip;
  f.Main.ToggleContinuousAllCommand.Execute(null);
  var task=Running(f);
  var run=UiRuntime.Field<DocumentContinuousRun>(f.Main,"_formalContinuousRun")!;
  Until(()=>run.State.Phase==ContinuousPhase.Waiting&&run.State.Round>0);
  var firstRound=run.State.Round;
  UiRuntime.Await(strip.RequestAsync(new(ImageSourceStripRequestKind.Activate,strip.Version,strip.Items[^1].Id),CancellationToken.None));
  UiRuntime.Await(task);
  Assert.Equal(firstRound+1,run.State.Round);
  using var image=f.Session.AcquirePublishedResult(f.Root.Id,source.Id);
  Assert.Equal(paths[^1],image!.Values!.Get<string>("output.file"));
  using var result=f.Session.AcquirePublishedResult(f.Root.Id,blob.Id);
  Assert.True(result?.Snapshot.IsSuccess);
  Assert.Equal(0L,source.Configuration.Fields.Get<long>("local.start"));
 });
 [Fact] public void Waiting_pause_stop_keeps_root_busy_and_rejects_late_round_status()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var source=f.Add();f.ConfigureImage(source,f.Image());f.Fields(source,("local.auto-switch",new ConstantSetting(false)));Interval(f,800);f.Save();
  f.Main.ToggleContinuousAllCommand.Execute(null);var task=Running(f);var run=UiRuntime.Field<DocumentContinuousRun>(f.Main,"_formalContinuousRun")!;Until(()=>run.State.Phase==ContinuousPhase.Waiting);f.Main.PauseCommand.Execute(null);
  Assert.True(f.Main.IsExecutionPaused);Assert.True(f.Main.IsRunning);Assert.False(f.Main.CanEditWorkflowCanvas);Assert.False(f.Main.SaveCurrentSolutionCommand.CanExecute(null));var round=run.State.Round;UiRuntime.Await(Task.Delay(180));Assert.Equal(round,run.State.Round);
  var snapshot=(WorkflowExecutionSnapshot)UiRuntime.Call(f.Main,"GetFormalExecutionSnapshot",f.Root.Id)!;var prior=snapshot.Nodes.Single().Status;
  var callback=(Action<ContinuousNodeStatus>)typeof(DocumentContinuousRun).GetField("NodeStatusChanged",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(run)!;
  callback(new(Guid.NewGuid(),round,f.Root.Id,source.Id,SunEyeVision.Plugin.Runtime.ExecutionStatus.Failed));callback(new(run.State.RootRunId,0,f.Root.Id,source.Id,SunEyeVision.Plugin.Runtime.ExecutionStatus.Failed));Assert.Equal(prior,snapshot.Nodes.Single().Status);
  f.Main.StopWorkflowCommand.Execute(null);Assert.True(f.Main.IsRunning);UiRuntime.Await(task);Until(()=>!f.Main.IsRunning);Assert.False(f.Session.HasUnsavedChanges);Assert.True(f.Tab.IsRootConfigurationReady);
 });
 [Fact] public void All_empty_is_rejected_and_disabled_node_root_is_not_silently_filtered()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();Assert.False(f.Main.ToggleContinuousAllCommand.CanExecute(null));f.Main.ToggleContinuousAllCommand.Execute(null);Assert.False(f.Main.IsRunning);Assert.Contains("非空",f.Main.StatusText);
  var node=f.Add();var graph=f.Session.PrepareGraphEdit(f.Root.Id);graph.SetNodeEnabled(node,false);f.Session.CommitGraphEdit(graph,"禁用真实节点");UiRuntime.Pump();
  f.Main.ToggleContinuousAllCommand.Execute(null);var task=Running(f);Assert.True(f.Main.IsContinuousRunning);f.Main.StopWorkflowCommand.Execute(null);UiRuntime.Await(task);Until(()=>!f.Main.IsRunning);Assert.Null(f.Session.AcquirePublishedResult(f.Root.Id,node.Id));
 });
}

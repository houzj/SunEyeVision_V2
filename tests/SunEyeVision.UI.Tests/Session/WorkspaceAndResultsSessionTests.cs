using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Canvas UI")]
public sealed class WorkspaceAndResultsSessionTests
{
 [Fact] public void Root_run_settings_undo_preserves_independent_navigation_and_save_dirty_identity()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();f.Save();Assert.False(f.Session.HasUnsavedChanges);
  var history=f.Session.HistoryEntries.Count;var revision=f.Session.ChangeRevision;
  f.Session.SetOverviewPosition(f.Root,new(105,210));Assert.Equal(history,f.Session.HistoryEntries.Count);Assert.Equal(revision,f.Session.ChangeRevision);Assert.True(f.Session.HasUnsavedChanges);
  var draft=f.Session.Editing.Host.BeginRootEdit(f.Root.Id);var interval=draft.WorkspaceSettings.RunIntervalMs;
  draft.WorkspaceSettings=draft.WorkspaceSettings with{RunIntervalMs=interval+17,StopOnNg=true};
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,roots:new[]{draft}));
  f.Session.SetOverviewPosition(f.Root,new(205,310));f.Session.Undo();Assert.Equal(interval,f.Root.WorkspaceSettings.RunIntervalMs);Assert.Equal(205,f.Root.WorkspaceSettings.OverviewX);
  f.Session.Redo();Assert.Equal(interval+17,f.Root.WorkspaceSettings.RunIntervalMs);Assert.Equal(205,f.Root.WorkspaceSettings.OverviewX);
  f.Save();Assert.False(f.Session.HasUnsavedChanges);
 });
 [Fact] public void Actual_halcon_run_publishes_leased_results_timing_and_display_navigation_without_parameter_history()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var image=f.Add();f.ConfigureImage(image,f.Image());f.Main.SelectedNode=image;f.Run();
  using var reader=f.Session.AcquirePublishedResult(f.Root.Id,image.Id);Assert.NotNull(reader);Assert.True(reader.Snapshot.IsSuccess,f.Main.StatusText);
  Assert.False(f.Main.IsRunning);Assert.Equal(1,((SunEyeVision.UI.Services.Workflow.WorkflowExecutionSnapshot)UiRuntime.Call(f.Main,"GetFormalExecutionSnapshot",f.Root.Id)!).CompletedRunCount);Assert.Equal("成功",Assert.Single(((SunEyeVision.UI.Services.Workflow.WorkflowExecutionSnapshot)UiRuntime.Call(f.Main,"GetFormalExecutionSnapshot",f.Root.Id)!).Nodes).Status);
  var output=reader.Snapshot.Schema.Outputs.Single(item=>item.Id=="output.image");var reference=new OutputReference(image.Id,output.Id,output.Type,new[]{"data"},scopeId:f.Root.Id,storage:output.Storage);
  var before=f.Session.HistoryEntries.Count;var revision=f.Session.ChangeRevision;
  f.Session.SetNodeDisplayReference(f.Root.Id,image,ParameterSettingCanonical.Serialize(reference));UiRuntime.Pump();
  Assert.Equal(before,f.Session.HistoryEntries.Count);Assert.Equal(revision,f.Session.ChangeRevision);Assert.NotNull(f.Main.CurrentResultPublication);
  Assert.Equal(SunEyeVision.Plugin.Wpf.Presenters.Results.ResultViewState.Success,f.Main.CurrentResultPublication!.State);
  f.Canvas.Delete(new[]{f.Projection(image)},Array.Empty<SunEyeVision.UI.Features.WorkflowUI.Canvas.CanvasConnectionProjection>());Assert.Null(f.Main.SelectedNode);
  Assert.True(reader.Snapshot.IsSuccess);Assert.Null(f.Session.AcquirePublishedResult(f.Root.Id,image.Id));
 });
}

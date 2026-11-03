using System.Windows;
using SunEyeVision.Application.Editing;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.UI.Services.Editing;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class EditorWindowSessionTests
{
 [Fact] public void Actual_parameter_window_commits_private_fields_rebases_and_closes_its_participant()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.Main.SelectedNode=node;f.Main.OpenDebugWindowCommand.Execute(node);UiRuntime.Pump();
  var window=System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();
  var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  Assert.Same(node,context.FormalNode);Assert.True(context.IsValid);
  var settings=UiRuntime.Visuals<DeclaredSettingsView>(window.Editor).Single();
  Assert.True(settings.TrySetFields(new Dictionary<string,ParameterSetting>{{"sequence.initial",new ConstantSetting(9L)}}));
  Assert.NotEqual(9L,(node.Configuration.Fields.Read("sequence.initial") as ConstantSetting)?.Value);
  var fields=(SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView)settings.Content;
  var field=fields.Fields.Single(item=>item.FieldId=="sequence.initial");
  var text=Assert.IsType<System.Windows.Controls.TextBox>(field.InputElement);var body=window.Editor.Content;
  EventHandler<SunEyeVision.Application.DocumentChangeSet>? delayed=null;
  delayed=(_,_)=>{f.Session.Changed-=delayed;text.Text="未完成数值";};f.Session.Changed+=delayed;
  Assert.True(context.TryCommitEditor(out var error),error);Assert.Equal(9L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("sequence.initial")).Value);
  Assert.Same(body,window.Editor.Content);Assert.Equal("未完成数值",text.Text);Assert.True(window.Editor.HasPendingEdits);
  text.Text="12";Assert.True(context.TryCommitEditor(out error),error);Assert.Equal(12L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("sequence.initial")).Value);
  window.Close();UiRuntime.Pump();Assert.False(context.IsActive);Assert.False(context.TryCommitEditor(out _));
 });
 [Fact] public void Actual_tool_editor_is_private_and_deleted_editor_cannot_publish()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add("BlobAnalysis");f.Main.SelectedNode=node;f.Main.OpenDebugWindowCommand.Execute(node);UiRuntime.Pump();
  var window=System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  Assert.True(context.IsValid);Assert.True(window.ExecuteOnce.CanExecute(null));Assert.False(window.StartContinuous.CanExecute(null));
  bool? dirtyDuringNotification=null;Exception? notificationFailure=null;
  EventHandler<SunEyeVision.Application.DocumentChangeSet> observe=(_,_)=>
  {try{dirtyDuringNotification=f.Session.HasUnsavedChanges;}catch(Exception error){notificationFailure=error;}};
  f.Session.Changed+=observe;
  f.Canvas.Delete(new[]{f.Projection(node)},Array.Empty<SunEyeVision.UI.Features.WorkflowUI.Canvas.CanvasConnectionProjection>());UiRuntime.Pump();
  f.Session.Changed-=observe;Assert.Null(notificationFailure);Assert.True(dirtyDuringNotification);Assert.True(f.Session.HasUnsavedChanges);
  Assert.False(context.IsValid);Assert.False(context.TryCommitEditor(out _));window.Close();
 });
}

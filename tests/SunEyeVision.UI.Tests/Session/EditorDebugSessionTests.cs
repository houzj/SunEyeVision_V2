using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class EditorDebugSessionTests
{
 private static RuntimeToolEditorWindow Open(DocumentUiFixture f,NodeDefinition node)
 {
  f.Main.SelectedNode=node;f.Main.OpenDebugWindowCommand.Execute(node);UiRuntime.Pump();
  var window=System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();UiRuntime.AwaitEditorReady(window);return window;
 }
 private static void Run(RuntimeToolEditorWindow window)
 {
  Assert.True(window.ExecuteOnce.CanExecute(null));UiRuntime.Await((Task)UiRuntime.Call(window,"RunOnceAsync")!);
  Assert.True(window.Editor.CurrentDebugSnapshot?.IsSuccess,window.Editor.CurrentDebugSnapshot?.ErrorMessage);
 }
 private static string FileOf(DeclaredOutputSnapshot snapshot)
 {using var reader=snapshot.AcquireReader();return reader.Values!.Get<string>("output.file");}
 private static IReadOnlyList<Button> VisibleFooterButtons(RuntimeToolEditorWindow window)
 {
  UiRuntime.Pump();
  var frame = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
  var footer = Assert.IsType<ItemsControl>(frame.FindName("BottomCommands"));
  return UiRuntime.Visuals<Button>(footer).Where(button => button.IsVisible).ToArray();
 }
 private static IEnumerable<string?> FooterLabels(params string[] keys) => keys.Select(AppLocalizationService.GetLang);
 [Fact] public void Title_bar_close_discards_the_blob_draft_after_confirm_rejects_invalid_input()=>UiRuntime.Run(()=>
 {
  const string threshold = "field:43bda03261df55b88f963bd18f2ba4d9";
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image());
  fixture.Connect(source, blob);
  var window = Open(fixture, blob);
  var topic = Assert.IsType<Uri>(window.HelpTopic);
  Assert.Equal("#tool-blob", topic.Fragment);
  Assert.Contains("id=\"tool-blob\"", System.IO.File.ReadAllText(topic.LocalPath));
  var help = UiRuntime.Visuals<Button>(window).Single(button => ReferenceEquals(button.Command, window.HelpCommand));
  Assert.True(help.IsVisible);
  Assert.True(help.IsEnabled);
  var context = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  var settings = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
  settings.SelectedPageIndex = 1;
  UiRuntime.Pump();
  var accepted = blob.Configuration.Fields.Get<long>(threshold);
  Assert.True(settings.TrySetFields(new Dictionary<string,ParameterSetting> { [threshold] = new ConstantSetting(43L) }));
  var row = UiRuntime.Visuals<DeclaredFieldRow>(settings).Single(item => item.FieldId == threshold);
  Assert.IsAssignableFrom<TextBox>(row.InputElement).Text = "未完成";
  Assert.False(window.TryConfirm(out var error));
  Assert.NotEmpty(error);
  Assert.True(window.IsVisible);
  Assert.Equal(accepted, blob.Configuration.Fields.Get<long>(threshold));

  var titleClose = Assert.IsType<Button>(window.Template.FindName("CloseWinButton", window));
  titleClose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  UiRuntime.Await(window.CloseAndDrainAsync());
  Assert.False(context.IsActive);
  Assert.False(window.IsCurrent);
  Assert.Equal(accepted, blob.Configuration.Fields.Get<long>(threshold));
 });
 [Fact] public void Image_source_result_group_expansion_survives_rerun_and_resets_on_node_switch()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var other = fixture.Add(x: 350, y: 240);
  fixture.ConfigureImage(source, fixture.Image(180, 8));
  fixture.ConfigureImage(other, fixture.Image(120, 4));
  fixture.Main.SelectedNode = source;
  fixture.Run();
  UiRuntime.Pump();

  var first = fixture.Main.FlatResultRows.Single(row => row.DisplayName == "图像" && row.Level == 1);
  Assert.True(first.IsExpanded);
  first.IsExpanded = false;
  var publication = fixture.Main.CurrentResultPublication!.Stamp;
  fixture.Run();
  UiRuntime.Pump();
  Assert.NotEqual(publication, fixture.Main.CurrentResultPublication!.Stamp);
  var current = fixture.Main.FlatResultRows.Single(row => row.DisplayName == "图像" && row.Level == 1);
  Assert.NotSame(first, current);
  Assert.False(current.IsExpanded);
  first.IsExpanded = true;
  Assert.False(current.IsExpanded);
  Assert.DoesNotContain(fixture.Main.FlatResultRows, row => row.DisplayName == "图像宽度");

  fixture.Main.SelectedNode = other;
  UiRuntime.Pump();
  Assert.True(fixture.Main.FlatResultRows.Single(row => row.DisplayName == "图像" && row.Level == 1).IsExpanded);
  Assert.Equal("4", fixture.Main.FlatResultRows.Single(row => row.DisplayName == "图像宽度").DisplayValue);
 });
 [Fact] public void History_keeps_formal_and_private_tool_records_in_their_own_scope()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image(180, 8));
  fixture.Connect(source, blob);
  fixture.Run();
  fixture.Run();
  fixture.Main.SelectedNode = source;
  Assert.Equal(new long[] { 2, 1 }, fixture.Main.ResultHistory.Select(row => row.RowIndex));
  Assert.All(fixture.Main.ResultHistory, row => Assert.StartsWith("Image:Width:", row.Content));

  fixture.Main.SelectedNode = blob;
  var formal = fixture.Main.ResultHistory.ToArray();
  Assert.Equal(2, formal[0].RowIndex);
  Assert.StartsWith("Module Status:", formal[0].Content);
  var window = Open(fixture, blob);
  Assert.Empty(fixture.Main.ResultHistory);
  Run(window);
  Assert.Equal(1, fixture.Main.ResultHistory[0].RowIndex);
  Assert.True(fixture.Main.ClearResultHistoryCommand.CanExecute(null));
  fixture.Main.ClearResultHistoryCommand.Execute(null);
  Assert.Empty(fixture.Main.ResultHistory);
  UiRuntime.Await(window.CloseAndDrainAsync());
  Assert.Equal(formal, fixture.Main.ResultHistory.ToArray());
  fixture.Main.ClearResultHistoryCommand.Execute(null);
  Assert.Empty(fixture.Main.ResultHistory);
  fixture.Main.SelectedNode = source;
  Assert.Equal(2, fixture.Main.ResultHistory.Count);
 });

 [Fact] public void Hidden_current_result_table_reads_latest_publication_when_selected_again()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image(180, 8));
  fixture.Connect(source, blob);
  fixture.Main.SelectedNode = blob;
  fixture.Run();
  UiRuntime.Pump();
  var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  var currentTab = Assert.IsType<TabItem>(shell.FindName("CurrentResultTab"));
  var historyTab = Assert.IsType<TabItem>(shell.FindName("HistoryTab"));
  var currentTable = Assert.Single(UiRuntime.Visuals<DataGrid>(fixture.Main.SelectedNodeDataAnalysisControl!));
  Assert.True(currentTab.IsSelected);
  var previousRow = Assert.Single(currentTable.Items.Cast<object>());
  historyTab.IsSelected = true;
  UiRuntime.Pump();
  fixture.Run();
  fixture.Run();
  UiRuntime.Pump();
  Assert.True(historyTab.IsSelected);
  Assert.Empty(currentTable.Items);
  var historyTable = Assert.IsType<DataGrid>(shell.FindName("HistoryGrid"));
  Assert.Equal(3, historyTable.Items.Count);
  currentTab.IsSelected = true;
  UiRuntime.Pump();
  Assert.NotSame(previousRow, Assert.Single(currentTable.Items.Cast<object>()));
  fixture.Main.SelectedNode = source;
  UiRuntime.Pump();
  Assert.True(historyTab.IsSelected);
  Assert.Null(shell.CurrentResultContent);
  var infoTabs = Assert.IsType<TabControl>(shell.FindName("InfoTab"));
  infoTabs.SelectedIndex = 2;
  fixture.Main.SelectedNode = blob;
  UiRuntime.Pump();
  Assert.True(currentTab.IsSelected);
  var resumedTable = Assert.Single(UiRuntime.Visuals<DataGrid>(fixture.Main.SelectedNodeDataAnalysisControl!));
  Assert.Single(resumedTable.Items.Cast<object>());
 });

 [Fact] public void Blob_centroid_point_subscription_survives_capture_and_reopen()=>UiRuntime.Run(()=>
 {
  const string correctionEnabled = "field:c95f3a26edf55097863a7ec47d17cce5";
  const string runPoint = "input.pose.run.point";
  using var fixture = new DocumentUiFixture();
  var image = fixture.Add();
  var producer = fixture.Add("BlobAnalysis", 230, 160);
  var consumer = fixture.Add("BlobAnalysis", 350, 260);
  fixture.ConfigureImage(image, fixture.Image(180, 8));
  fixture.Connect(image, producer);
  fixture.Connect(producer, consumer);
  var imagePort = fixture.Session.GetNodeSchema(fixture.Root.Id, image.Id)!.Outputs.Single(port => port.Id == "output.image");
  var origin = new RecordValue(FixtureDeclarations.Point, new Dictionary<string, object?> { ["x"] = 0d, ["y"] = 0d });
  var pose = new RecordValue(FixtureDeclarations.Pose, new Dictionary<string, object?>
   { ["point"] = origin, ["angle"] = 0d, ["scale-x"] = 1d, ["scale-y"] = 1d });
  fixture.Fields(consumer, ("input.image", new ReferenceSetting(
   new OutputReference(image.Id, imagePort.Id, imagePort.Type, new[] { "data" }, scopeId: fixture.Root.Id))),
   (correctionEnabled, new ConstantSetting(true)), ("input.pose", new ConstantSetting(
    new RecordValue(FixtureDeclarations.Info, new Dictionary<string, object?> { ["reference"] = pose, ["run"] = pose }))));
  fixture.Run();

  var schema = fixture.Session.GetNodeSchema(fixture.Root.Id, producer.Id)!;
  var centroidPort = schema.Outputs.Single(port => port.Type == TypeRef.SequenceOf(FixtureDeclarations.Point));
  Point2 expected;
  using (var result = fixture.Session.AcquirePublishedResult(fixture.Root.Id, producer.Id))
  {
   var centroid = Assert.IsType<RecordValue>(Assert.Single(result!.Values!.Get<SequenceValue>(centroidPort.Id)));
   expected = new(centroid.Get<double>("x"), centroid.Get<double>("y"));
   Assert.NotEqual(default, expected);
  }

  var window = Open(fixture, consumer);
  var assist = Assert.Single(UiRuntime.Visuals<NativeAssistUiDraftControls>(window.Editor));
  Assert.True(assist.TrySetMode(1));
  UiRuntime.Pump();
  var row = UiRuntime.Visuals<DeclaredFieldRow>(assist).Single(item => item.FieldId == runPoint);
  var editor = Assert.IsType<FieldRowModel>(row.Content).ReferenceDataA!;
  object? Read(object owner, string property) => owner.GetType().GetProperty(property)!.GetValue(owner);
  IEnumerable<object> Nodes(object owner, string property) =>
   Assert.IsAssignableFrom<System.Collections.IEnumerable>(Read(owner, property)).Cast<object>();
  IEnumerable<object> Descendants(object node) => new[] { node }.Concat(Nodes(node, "EnumEntrysA").SelectMany(Descendants));
  Assert.IsAssignableFrom<System.Windows.Input.ICommand>(Read(editor, "DropDownCommand")).Execute(null);
  var candidates = Nodes(editor, "SourceNodes").SelectMany(Descendants).ToArray();
  var rectangle = schema.OutputDisplay.Single(output => output.DisplayName == "Blob矩形框");
  Assert.Contains(candidates, node => Read(node, "Candidate") is DeclaredReferenceCandidate item
   && item.NodeId == producer.Id && item.Output.Id == rectangle.FieldId && item.MemberPath.SequenceEqual(new[] { "origin" }));
  var candidate = candidates.Single(node =>
   Read(node, "Candidate") is DeclaredReferenceCandidate item && item.NodeId == producer.Id &&
   item.Output.Id == centroidPort.Id && item.MemberPath.Count == 0);
  Assert.IsAssignableFrom<System.Windows.Input.ICommand>(Read(editor, "ClickCommand")).Execute(candidate);
  var text = Assert.IsType<string>(Read(editor, "CurValueA"));
  Assert.EndsWith("[]", text);
  editor.GetType().GetProperty("CurValueA")!.SetValue(editor, text[..^2] + "[0]");
  Run(window);
  void CheckPoint(DeclaredOutputSnapshot snapshot)
  {
   var output = Assert.IsAssignableFrom<IToolResultGeometryOutput>(snapshot.Output);
   var shape = Assert.Single(output.ResultGeometry.Groups.Single(group => group.Role == ResultGeometryRole.RunPoint).Shapes);
   Assert.Equal(expected, shape.Get<PointGeometry>("center").Point);
  }
  CheckPoint(window.Editor.CurrentDebugSnapshot!);
  Assert.True(window.TryConfirm(out var error), error);
  UiRuntime.Await(window.CloseAndDrainAsync());

  fixture.Save();
  var reopened = ServiceInitializer.SolutionManager.Open(System.IO.Path.Combine(fixture.DirectoryPath, "document.solution"),
   _ => new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string, string>>()),
   _ => null, new System.Text.Json.JsonSerializerOptions());
  UiRuntime.Pump();
  var savedNode = reopened.FindNode(fixture.Root.Id, consumer.Id)!;
  foreach (var axis in new[] { "x", "y" })
  {
   var binding = Assert.IsType<ReferenceSetting>(savedNode.Configuration.Fields.Read(runPoint + "." + axis)).Reference;
   Assert.Equal(producer.Id, binding.NodeId);
   Assert.Equal(centroidPort.Id, binding.PortId);
   Assert.Equal(new[] { axis }, binding.MemberFieldPath);
   Assert.Equal(0, Assert.IsType<BindingSelection.AtConstant>(binding.Selection).Index);
  }
  fixture.Run();
  using var restored = reopened.AcquirePublishedResult(fixture.Root.Id, consumer.Id);
  CheckPoint(restored!.Snapshot);
 });
 [Fact] public void Reset_from_the_run_page_restores_algorithm_settings_across_pages_in_the_same_draft()=>UiRuntime.Run(()=>
 {
  const string threshold = "field:43bda03261df55b88f963bd18f2ba4d9";
  const string zeroIsOk = "field:2e46ec3997424050a6aecbdfd92b4f28";
  const string countEnabled = "field:99a8e45eec7f406b93ab7226c73744ce";
  const string countLower = "field:4dd2475077314fc68baf68900d8d2b93";
  const string graphicColor = "display.blob.ok";
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image());
  fixture.Connect(source, blob);
  using(var variables=fixture.Session.Editing.Host.BeginVariableEdit())
  {
   variables.Variables.Add(new GlobalVariable("重置测试下限", 2L, "Integer"));
   fixture.Session.CommitEdit(DocumentEditProposal.Prepare(fixture.Session.Editing.Host, variables:variables));
  }
  var variable = fixture.Session.Document.GlobalVariables.Single(item => item.Name == "重置测试下限");
  var customColor = PresentationDeclarations.Color(25, 50, 75);
  fixture.Fields(blob, (threshold, new ConstantSetting(44L)), (zeroIsOk, new ConstantSetting(true)),
   (countEnabled, new ConstantSetting(true)),
   (countLower, new VariableSetting(new VariableReference(fixture.Session.Document.Id, variable.Id, TypeRef.ScalarOf(ScalarKind.Int64)))),
   (graphicColor, new ConstantSetting(customColor)));
  var window = Open(fixture, blob);
  var context = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  var settings = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
  settings.SelectedPageIndex = 1;
  UiRuntime.Pump();
  var draft = context.PreparedDraft;
  var imageSource = draft.Configuration.Fields.Read("input.image");
  window.Editor.ResetRunParameters();
  Assert.Same(draft, context.PreparedDraft);
  var accepted = settings.CaptureAcceptedFields();
  Assert.Equal(100L, accepted.Get<long>(threshold));
  Assert.False(accepted.Get<bool>(zeroIsOk));
  Assert.False(accepted.Get<bool>(countEnabled));
  Assert.Equal(1L, accepted.Get<long>(countLower));
  Assert.Equal(25L, accepted.Get<RecordValue>(graphicColor).Get<long>("r"));
  Assert.Equal(imageSource, draft.Configuration.Fields.Read("input.image"));
  Assert.Equal(44L, blob.Configuration.Fields.Get<long>(threshold));
  Assert.IsType<VariableSetting>(blob.Configuration.Fields.Read(countLower));
  Assert.True(window.Confirm.CanExecute(null));
  window.Confirm.Execute(null);
  UiRuntime.Await(window.CloseAndDrainAsync());
  Assert.Equal(100L, blob.Configuration.Fields.Get<long>(threshold));
  Assert.False(blob.Configuration.Fields.Get<bool>(zeroIsOk));
  Assert.Equal(1L, blob.Configuration.Fields.Get<long>(countLower));
 });
 [Fact] public void Blob_page_switch_retires_the_previous_reference_request_and_reuses_the_parameter_row()=>UiRuntime.Run(()=>
 {
  const string threshold = "field:43bda03261df55b88f963bd18f2ba4d9";
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.Connect(source, blob);
  var window = Open(fixture, blob);
  var settings = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
  var owner = Assert.IsType<ReferenceWindowOwner>(UiRuntime.Field<ReferenceWindowOwner>(window.Editor, "_referenceOwner"));
  settings.SelectedPageIndex = 1;
  UiRuntime.Pump();
  DeclaredFieldRow ThresholdRow() => Assert.Single(UiRuntime.Visuals<DeclaredFieldRow>(settings), row => row.FieldId == threshold);
  var thresholdRow = ThresholdRow();
  var model = Assert.IsType<FieldRowModel>(thresholdRow.Content);
  model.TypeChangeCommand.Execute(null);
  UiRuntime.Pump();
  Assert.True(model.IsSubA);
  var session = Assert.IsAssignableFrom<object>(model.ReferenceDataA);
  var open = Assert.IsAssignableFrom<System.Windows.Input.ICommand>(session.GetType().GetProperty("PopSubscribeWindowCommand")!.GetValue(session));
  open.Execute(null);
  var previous = Assert.IsType<ReferenceExpansionRequest>(owner.CurrentRequest);
  Assert.True(owner.IsExpanded);
  settings.SelectedPageIndex = 2;
  Assert.Null(owner.CurrentRequest);
  Assert.False(previous.IsValid);
  Assert.True(owner.IsExpanded);
  settings.SelectedPageIndex = 1;
  UiRuntime.Pump();
  Assert.Same(thresholdRow, ThresholdRow());
  Assert.False(previous.RefreshCommand.CanExecute(null));
  open.Execute(null);
  Assert.IsType<ReferenceExpansionRequest>(owner.CurrentRequest);
  Assert.NotSame(previous, owner.CurrentRequest);
  UiRuntime.Await(window.CloseAndDrainAsync());
  Assert.Null(owner.CurrentRequest);
 });
 [Fact] public void Image_source_file_name_uses_the_same_frame_and_persists_its_display_switch()=>UiRuntime.Run(()=>
 {
  const string showName = "display.image-name";
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var path = fixture.Image();
  fixture.ConfigureImage(source, path);
  var window = Open(fixture, source);
  Run(window);
  UiRuntime.Pump();
  var snapshot = window.Editor.CurrentDebugSnapshot;
  var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  var viewport = Assert.IsType<ImageViewBox>(shell.OutputViewport);
  Assert.Equal(System.IO.Path.GetFileName(path), viewport.ImageName);
  var settings = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
  Assert.True(settings.TrySetFields(new Dictionary<string, ParameterSetting> { [showName] = new ConstantSetting(false) }));
  UiRuntime.Pump();
  Assert.Null(viewport.ImageName);
  Assert.Same(snapshot, window.Editor.CurrentDebugSnapshot);
  Assert.True(window.Confirm.CanExecute(null));
  window.Confirm.Execute(null);
  UiRuntime.Await(window.CloseAndDrainAsync());
  fixture.Save();
  var reopened = ServiceInitializer.SolutionManager.Open(
   System.IO.Path.Combine(fixture.DirectoryPath, "document.solution"),
   _ => new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string, string>>()),
   _ => null, new System.Text.Json.JsonSerializerOptions());
  UiRuntime.Pump();
  var reopenedSource = Assert.IsType<NodeDefinition>(reopened.FindNode(fixture.Root.Id, source.Id));
  Assert.Equal(false, Assert.IsType<ConstantSetting>(reopenedSource.Configuration.Fields.Read(showName)).Value);
  fixture.Main.SelectedNode = reopenedSource;
  fixture.Run();
  UiRuntime.Pump();
  Assert.Null(viewport.ImageName);
 });
 [Fact] public void Switching_nodes_from_assembly_ready_does_not_show_the_retired_window()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image());
  fixture.Connect(source, blob);
  fixture.Main.OpenDebugWindowCommand.Execute(source);
  var first = System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();
  Assert.False(first.IsVisible);
  var shown = false;
  var switched = false;
  first.IsVisibleChanged += (_, args) => shown |= args.NewValue is true;
  first.Changed += (_, _) =>
  {
   if (switched || !first.AssemblyReadyTask.IsCompletedSuccessfully) return;
   switched = true;
   fixture.Main.OpenDebugWindowCommand.Execute(blob);
  };
  UiRuntime.Pump();
  Assert.True(switched);
  Assert.False(shown);
  Assert.False(first.IsCurrent);
  var current = System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();
  UiRuntime.AwaitEditorReady(current);
  Assert.True(current.IsVisible);
  Assert.Same(blob, Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(current.Editor)).FormalNode);
  UiRuntime.Await(current.CloseAndDrainAsync());
 });
 [Fact] public void Opening_blob_from_node_selection_notification_retires_the_outer_image_source_request()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image());
  fixture.Connect(source, blob);
  fixture.Main.SelectedNode = null;
  var switched = false;
  fixture.Main.PropertyChanged += (_, args) =>
  {
   if (switched || args.PropertyName != nameof(fixture.Main.SelectedNode) || !ReferenceEquals(fixture.Main.SelectedNode, source)) return;
   switched = true;
   fixture.Main.OpenDebugWindowCommand.Execute(blob);
  };

  fixture.Main.OpenDebugWindowCommand.Execute(source);
  var window = Assert.Single(System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>());
  UiRuntime.AwaitEditorReady(window);
  Assert.True(switched);
  Assert.True(window.IsVisible);
  Assert.Same(blob, Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor)).FormalNode);
  var close = window.CloseAndDrainAsync();
  UiRuntime.Await(close);
  Assert.True(close.GetAwaiter().GetResult());
 });
 [Fact] public void Actual_window_private_cursor_result_and_rebased_draft_stay_separate_from_formal_run()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();var first=f.Image(22);var second=f.Image(44);f.ConfigureImage(node,first,second);f.Fields(node,("local.auto-switch",new ConstantSetting(true)));
  var configuration=node.Configuration;var window=Open(f,node);var scope=window.Editor.DebugScopeId;Assert.NotNull(scope);
  var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  var settings=UiRuntime.Visuals<DeclaredSettingsView>(window.Editor).Single();Assert.True(settings.TrySetFields(new Dictionary<string,ParameterSetting>{{"sequence.initial",new ConstantSetting(9L)}}));
  Run(window);var one=window.Editor.CurrentDebugSnapshot!;Assert.Equal(second,FileOf(one));Assert.Null(f.Session.AcquirePublishedResult(f.Root.Id,node.Id));Assert.Same(configuration,node.Configuration);
  Assert.Equal(one.Identity,f.Main.CurrentResultPublication!.Stamp.Invocation);Assert.NotEqual(f.Session.ResultOwnerId,one.Identity.DocumentSessionId);
  Run(window);Assert.Equal(first,FileOf(window.Editor.CurrentDebugSnapshot!));Assert.Equal(scope,window.Editor.DebugScopeId);Assert.Equal(0L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("local.start")).Value);
  Assert.Equal(9L,context.ReadCurrentParameter("sequence.initial")?.Value);Assert.True(context.TryCommitEditor(out var error),error);Assert.Equal(scope,window.Editor.DebugScopeId);
  Run(window);Assert.Equal(scope,window.Editor.DebugScopeId);Assert.True(window.CloseAndDrainAsync().GetAwaiter().GetResult());UiRuntime.Pump();Assert.Null(f.Main.CurrentResultPublication?.Stamp.Invocation);
  f.Run();using var formal=f.Session.AcquirePublishedResult(f.Root.Id,node.Id);Assert.NotNull(formal);Assert.Equal(second,FileOf(formal.Snapshot));
 });
 [Fact] public void Confirmed_blob_roi_threshold_and_display_reopen_on_the_same_image_source_connection()=>UiRuntime.Run(()=>
 {
  const string threshold = "field:43bda03261df55b88f963bd18f2ba4d9";
  const string areaEnabled = "field:e9845ab7a46e5ee9b42310bef5a11c19";
  const string binary = "field:8ca10000000040008000000000000006";
  const string displayText = "display.text.content";
  const string caption = "保存后的 Blob";
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  var path = fixture.Image(51, 24);
  fixture.ConfigureImage(source, path);
  var connectionId = fixture.Connect(source, blob).Id;
  fixture.Run();

  var window = Open(fixture, blob);
  var settings = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
  var roiEditor = Assert.Single(UiRuntime.Visuals<NativeRoiEditor>(window.Editor), item => !item.PolygonOnly);
  Assert.True(roiEditor.PrepareInteraction?.Invoke());
  var roi = Assert.IsType<NativeRoiSession>(UiRuntime.Field<NativeRoiSession>(roiEditor, "_session"));
  var draw = Assert.IsType<RoiDrawRequest>(roi.BeginDraw("rectangle"));
  Assert.True(roi.CompleteDraw(draw, RoiGeometryEditing.Create("rectangle", new Point2(3, 3), new Point2(15, 15))));
  var drawnId = Assert.Single(roi.Items).Id;
  settings.SelectedPageIndex = 1;
  UiRuntime.Pump();
  Assert.True(settings.TrySetFields(new Dictionary<string, ParameterSetting>
  {
   [threshold] = new ConstantSetting(40L),
   [areaEnabled] = new ConstantSetting(false)
  }));
  settings.SelectedPageIndex = 2;
  UiRuntime.Pump();
  Assert.True(settings.TrySetFields(new Dictionary<string, ParameterSetting>
  {
   [displayText] = new ConstantSetting(caption)
  }));
  Run(window);
  var debugSnapshot = Assert.IsType<DeclaredOutputSnapshot>(window.Editor.CurrentDebugSnapshot);
  byte[] expectedPixels;
  using (var debugReader = debugSnapshot.AcquireReader())
   expectedPixels = debugReader.Values!.Get<RecordValue>(binary).Get<IReadOnlyImage>("data").CopyPixels();
  Assert.Contains((byte)255, expectedPixels);
  Assert.Equal(0, expectedPixels[20 * 24 + 20]);
  var context = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  var originalView = window.Editor.Content;
  var reusedAtClose = false;
  window.Closing += (_, _) => reusedAtClose = ReferenceEquals(originalView, window.Editor.Content);
  Assert.True(window.Confirm.CanExecute(null));
  window.Confirm.Execute(null);
  UiRuntime.Await(window.CloseAndDrainAsync());
  Assert.True(reusedAtClose);
  Assert.False(context.IsActive);
  Assert.Equal(40L, Assert.IsType<ConstantSetting>(blob.Configuration.Fields.Read(threshold)).Value);

  fixture.Save();
  var reopened = ServiceInitializer.SolutionManager.Open(
   System.IO.Path.Combine(fixture.DirectoryPath, "document.solution"),
   _ => new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string, string>>()),
   _ => null, new System.Text.Json.JsonSerializerOptions());
  UiRuntime.Pump();
  var reopenedBlob = Assert.IsType<NodeDefinition>(reopened.FindNode(fixture.Root.Id, blob.Id));
  Assert.NotSame(blob, reopenedBlob);
  Assert.Equal(40L, Assert.IsType<ConstantSetting>(reopenedBlob.Configuration.Fields.Read(threshold)).Value);
  var imageReference = Assert.IsType<ReferenceSetting>(reopenedBlob.Configuration.Fields.Read("input.image"));
  Assert.Equal(source.Id, imageReference.Reference.NodeId);
  Assert.Equal("output.image", imageReference.Reference.PortId);
  Assert.Equal(new[] { "data" }, imageReference.Reference.MemberFieldPath);
  Assert.Equal("connection:" + connectionId, imageReference.OriginId);
  Assert.Null(reopened.AcquirePublishedResult(fixture.Root.Id, blob.Id));
  fixture.Run();
  using (var publishedSource = reopened.AcquirePublishedResult(fixture.Root.Id, source.Id))
   Assert.Equal(path, FileOf(Assert.IsType<DeclaredOutputSnapshot>(publishedSource?.Snapshot)));
  var reopenedWindow = Open(fixture, reopenedBlob);
  var reopenedView = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(reopenedWindow.Editor));
  var reopenedRoiEditor = Assert.Single(UiRuntime.Visuals<NativeRoiEditor>(reopenedWindow.Editor), item => !item.PolygonOnly);
  Assert.True(reopenedRoiEditor.PrepareInteraction?.Invoke());
  var reopenedRoi = Assert.IsType<NativeRoiSession>(UiRuntime.Field<NativeRoiSession>(reopenedRoiEditor, "_session"));
  Assert.Equal(drawnId, Assert.Single(reopenedRoi.Items).Id);
  Run(reopenedWindow);
  UiRuntime.Pump();
  var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  var viewport = Assert.IsType<ImageViewBox>(shell.OutputViewport);
  Assert.Equal(caption, Assert.Single(viewport.ShapeList.OfType<IMultiText>().SelectMany(item => item.Texts)).Content);
  reopenedView.SelectedPageIndex = 1;
  UiRuntime.Pump();
  Assert.True(reopenedView.TrySetFields(new Dictionary<string, ParameterSetting> { [threshold] = new ConstantSetting(60L) }));
  UiRuntime.Await(reopenedWindow.CloseAndDrainAsync());
  Assert.Equal(40L, Assert.IsType<ConstantSetting>(reopenedBlob.Configuration.Fields.Read(threshold)).Value);
  fixture.Run();
  using var result = reopened.AcquirePublishedResult(fixture.Root.Id, blob.Id);
  Assert.NotNull(result);
  using var reader = result.Snapshot.AcquireReader();
  Assert.Equal(1L, reader.Values!.Get<long>("field:8ca10000000040008000000000000011"));
  var image = reader.Values.Get<RecordValue>(binary).Get<IReadOnlyImage>("data");
  Assert.Equal(expectedPixels, image.CopyPixels());
  Assert.NotEqual(debugSnapshot.Identity.DocumentSessionId, result.Snapshot.Identity.DocumentSessionId);
 });
 [Fact] public void Actual_blob_window_previews_real_published_input_then_runs_and_retires_old_preview()=>UiRuntime.Run(()=>
 {
  const int side = 6;
  const string lowThreshold = "field:43bda03261df55b88f963bd18f2ba4d9";
  using var f=new DocumentUiFixture();var source=f.Add();var blob=f.Add("BlobAnalysis",350,240);var imagePath=f.Image(side:side);f.ConfigureImage(source,imagePath);f.Connect(source,blob);f.Run();
  Assert.Equal(CanvasNodeRunState.Passed,f.Projection(source).RunState);
  Assert.Equal(CanvasNodeRunState.Rejected,f.Projection(blob).RunState);
  var window=Open(f,blob);var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));var scope=window.Editor.DebugScopeId;
  var settings=UiRuntime.Visuals<DeclaredSettingsView>(window.Editor).Single();
  settings.SelectedPageIndex=1;UiRuntime.Pump();
  var thresholdRow=UiRuntime.Visuals<DeclaredFieldRow>(settings).Single(row=>row.FieldId==lowThreshold);
  var thresholdText=Assert.IsAssignableFrom<TextBox>(thresholdRow.InputElement);
  var accepted=context.PreparedDraft.Configuration.Fields.Get<long>(lowThreshold);
  var revision=context.PreparedDraft.ExecutionRevision;
  thresholdText.Text="42";
  Assert.True(thresholdRow.HasPendingEdits);
  using var inputs=window.Editor.CaptureDebugInputs(new[]{"input.image"});Assert.True(inputs.IsCurrent);
  Assert.Equal(Enumerable.Repeat((byte)51,side*side),inputs.Inputs.Get<IReadOnlyImage>("input.image").CopyPixels());
  Assert.Equal(accepted,context.PreparedDraft.Configuration.Fields.Get<long>(lowThreshold));
  Assert.Equal(revision,context.PreparedDraft.ExecutionRevision);
  Assert.True(thresholdRow.HasPendingEdits);
  Run(window);Assert.False(inputs.IsCurrent);Assert.Equal(scope,window.Editor.DebugScopeId);Assert.True(window.Editor.CurrentDebugSnapshot!.IsSuccess);
  Assert.Equal(42L,context.PreparedDraft.Configuration.Fields.Get<long>(lowThreshold));
  void VerifyThreshold(long threshold,byte pixel,long count)
  {
   Assert.True(settings.TrySetFields(new Dictionary<string,ParameterSetting>
   {
    [lowThreshold]=new ConstantSetting(threshold),
    ["field:e9845ab7a46e5ee9b42310bef5a11c19"]=new ConstantSetting(false)
   }));
   Run(window);
   var snapshot=window.Editor.CurrentDebugSnapshot!;
   Assert.Equal(snapshot.Identity,f.Main.CurrentResultPublication!.Stamp.Invocation);
   using var reader=snapshot.AcquireReader();
   foreach(var imageId in new[]{"field:8ca10000000040008000000000000005","field:8ca10000000040008000000000000006"})
   {
    var image=reader.Values!.Get<RecordValue>(imageId).Get<IReadOnlyImage>("data");
    var expected=Enumerable.Range(0,side*side).Select(index =>
     index / side == 0 || index % side == 0 ? (byte)0 : pixel);
    Assert.Equal(expected,image.CopyPixels());
   }
   Assert.Equal(count,reader.Values!.Get<long>("field:8ca10000000040008000000000000011"));
   Assert.Equal(count>0, snapshot.IsPass);
   Assert.Equal(count>0?1L:0L,reader.Values.Get<long>("field:8ca10000000040008000000000000004"));
   Assert.Equal(Enumerable.Repeat(1L,(int)count),
    reader.Values.Get<SequenceValue>("field:8ca10000000040008000000000000109").Cast<long>());
  }
  VerifyThreshold(40,255,1);
  VerifyThreshold(60,0,0);
  settings.SelectedPageIndex=0;UiRuntime.Pump();
  using var next=window.Editor.CaptureDebugInputs(new[]{"input.image"});Assert.True(next.IsCurrent);
  f.Fields(source,("sequence.initial",new ConstantSetting(99L)));Assert.False(next.IsCurrent);
  Assert.Equal(CanvasNodeRunState.None,f.Projection(source).RunState);
  Assert.Same(blob,context.FormalNode);
  UiRuntime.Await(window.CloseAndDrainAsync());UiRuntime.Pump();Assert.False(context.IsActive);
  var sourceWindow=Open(f,source);
  var sourceContext=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(sourceWindow.Editor));
  Assert.Same(source,sourceContext.FormalNode);Assert.NotEqual(scope,sourceWindow.Editor.DebugScopeId);
  Run(sourceWindow);Assert.Equal(imagePath,FileOf(sourceWindow.Editor.CurrentDebugSnapshot!));
  Assert.False(next.IsCurrent);Assert.False(context.TryCommitEditor(out _));
  UiRuntime.Await(sourceWindow.CloseAndDrainAsync());
 });
 [Fact] public void Actual_blob_image_mask_selector_retires_results_and_feeds_the_next_invocation()=>UiRuntime.Run(()=>
 {
  const string maskInput = "input.roi.mask-image";
  const string maskOutput = "field:8ca10000000040008000000000000103";
  const string binaryOutput = "field:8ca10000000040008000000000000006";
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image(180, 6));
  fixture.Connect(source, blob);
  fixture.Run();
  var window = Open(fixture, blob);
  DeclaredFieldRow Row(string id) => UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Single(row => row.FieldId == id);
  Assert.True(Row("input.roi.area-type").TrySetConstant(1L));
  window.UpdateLayout();
  UiRuntime.Pump();
  var maskRow = Row(maskInput);
  var model = Assert.IsType<FieldRowModel>(maskRow.Content);
  Assert.Equal(DeclaredFieldTemplateRegistry.ImageSource, model.TemplateKey);
  var selector = Assert.IsType<ComboBox>(maskRow.InputElement);
  Assert.Contains(UiRuntime.Visuals<TextBlock>(maskRow), label => label.IsVisible && label.Text == model.DisplayNameA);
  var candidate = selector.Items.OfType<DeclaredFieldRow.ImageSourceChoice>()
      .Single(item => item.Setting is ReferenceSetting binding && binding.Reference.NodeId == source.Id);
  selector.SelectedItem = candidate;
  Assert.IsType<ReferenceSetting>(maskRow.Setting);
  var outputRow = Row("input.roi.output-mask");
  Assert.True(outputRow.TrySetConstant(true));
  Run(window);
  using (var reader = window.Editor.CurrentDebugSnapshot!.AcquireReader())
  {
   foreach (var id in new[] { maskOutput, binaryOutput })
    Assert.Equal(Enumerable.Repeat((byte)255, 36), reader.Values!.Get<RecordValue>(id).Get<IReadOnlyImage>("data").CopyPixels());
  }
  Assert.True(outputRow.TrySetConstant(false));
  Assert.Null(window.Editor.CurrentDebugSnapshot);
  Run(window);
  selector.SelectedItem = selector.Items.OfType<DeclaredFieldRow.ImageSourceChoice>().Single(item => item.Setting is UnsetSetting);
  Assert.IsType<UnsetSetting>(maskRow.Setting);
  Assert.Null(window.Editor.CurrentDebugSnapshot);
  UiRuntime.Await(window.CloseAndDrainAsync());
 });
 [Fact] public void Blob_preserved_window_projects_roi_modes_from_the_rebased_draft()=>UiRuntime.Run(()=>
 {
  const string inheritance="input.roi.inheritance-mode";
  const string maskMode="input.roi.mask.inherit";
  using var fixture=new DocumentUiFixture();
  var source=fixture.Add();
  var blob=fixture.Add("BlobAnalysis",350,240);
  fixture.ConfigureImage(source,fixture.Image(200,6));
  fixture.Connect(source,blob);
  var window=Open(fixture,blob);
  var body=window.Editor.Content;
  var roi=Assert.Single(UiRuntime.Visuals<NativeRoiUiDraftControls>(window.Editor));
  DeclaredFieldRow Row(string id)=>UiRuntime.Visuals<DeclaredFieldRow>(roi).Single(row=>row.FieldId==id);
  Assert.True(roi.IsLocalDrawing("inspection"));
  Assert.True(Row("input.roi.output-mask").TrySetConstant(true));
  var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  var imageInput=UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Single(row=>row.FieldId=="input.image");
  var picker=Assert.IsType<ComboBox>(imageInput.InputElement);
  var initial=Assert.IsType<ReferenceSetting>(picker.SelectedValue);
  Assert.Equal("output.image",initial.Reference.PortId);
  var gray=fixture.Session.GetNodeSchema(fixture.Root.Id,source.Id)!.Outputs.Single(port=>port.Id=="output.gray-image");
  EventHandler<SunEyeVision.Application.DocumentChangeSet>? delayed=null;
  delayed=(_,_)=>{fixture.Session.Changed-=delayed;context.PreparedDraft.ApplyFields(new Dictionary<string,ParameterSetting>
  {
   [inheritance]=new ConstantSetting("RectangleParameters"),
   [maskMode]=new ConstantSetting(true),
   ["input.image"]=new ReferenceSetting(new OutputReference(source.Id,gray.Id,gray.Type,new[]{"data"},scopeId:fixture.Root.Id))
  });};
  fixture.Session.Changed+=delayed;
  try { Assert.True(context.TryCommitEditor(out var error),error); }
  finally { fixture.Session.Changed-=delayed; }
  UiRuntime.Pump();
  Assert.Same(body,window.Editor.Content);
  Assert.False(roi.IsLocalDrawing("inspection"));
  Assert.True(roi.IsInheritedMask);
  Assert.Equal(1L,Assert.IsType<ConstantSetting>(Row("roi-inheritance").Setting).Value);
  Assert.True(Row("input.roi.rectangle.center-x").IsVisible);
  Assert.Equal(gray.Id,Assert.IsType<ReferenceSetting>(picker.SelectedValue).Reference.PortId);
  Assert.Equal(initial.Reference.PortId,Assert.IsType<ReferenceSetting>(blob.Configuration.Fields.Read("input.image")).Reference.PortId);
  Assert.Equal("RectangleParameters",context.PreparedDraft.Configuration.Fields.Get<string>(inheritance));
  Assert.IsType<UnsetSetting>(blob.Configuration.Fields.Read(inheritance));
  UiRuntime.Await(window.CloseAndDrainAsync());
 });
 [Fact] public void Blob_workspace_refresh_does_not_drop_an_input_edit_received_during_notification()=>UiRuntime.Run(()=>
 {
  const string maskInput="input.roi.mask-image";
  using var fixture=new DocumentUiFixture();
  var source=fixture.Add();
  var blob=fixture.Add("BlobAnalysis",350,240);
  fixture.ConfigureImage(source,fixture.Image(180,6));
  fixture.Fields(source,("output.format",new ConstantSetting("rgb24")),("output.mono8",new ConstantSetting(true)));
  fixture.Connect(source,blob);
  fixture.Run();
  var window=Open(fixture,blob);
  try
  {
   var area=UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Single(row=>row.FieldId=="input.roi.area-type");
   Assert.True(area.TrySetConstant(1L));
   UiRuntime.Pump();
   var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
   var shell=Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
   var ports=fixture.Session.GetNodeSchema(fixture.Root.Id,source.Id)!.Outputs;
   string[] Inputs()=>shell.InputItems!.Cast<ResultWorkspaceImageSource>().Select(item=>item.Reference.PortId).ToArray();
   void SetMask(string portId)
   {
    var port=ports.Single(port=>port.Id==portId);
    var path=port.Type.Fields.Where(field=>field.Type==TypeRef.ResourceOf(ResourceKind.Image)).Select(field=>field.Id).ToArray();
    Assert.Single(path);
    context.PreparedDraft.ApplyFields(new Dictionary<string,ParameterSetting>
    {
     [maskInput]=new ReferenceSetting(new OutputReference(source.Id,port.Id,port.Type,path,scopeId:fixture.Root.Id))
    });
    UiRuntime.Call(fixture.Main,"RefreshEditorDebugResult");
   }
   bool replaced=false;
   void ReplaceMask()
   {
    if(replaced)return;
    replaced=true;
    Assert.Contains("output.gray-image",Inputs());
    SetMask("output.image");
   }
   fixture.Main.ResultWorkspaceChanged+=ReplaceMask;
   try { SetMask("output.gray-image"); }
   finally { fixture.Main.ResultWorkspaceChanged-=ReplaceMask; }
   Assert.True(replaced);
   Assert.Equal(new[]{"output.image"},Inputs());
   Assert.IsType<UnsetSetting>(blob.Configuration.Fields.Read(maskInput));
  }
  finally { UiRuntime.Await(window.CloseAndDrainAsync()); }
 });
 [Fact] public void Blob_primary_image_edit_selects_the_new_roi_frame_without_replacing_the_saved_output_choice()=>UiRuntime.Run(()=>
 {
  const string binary = "field:8ca10000000040008000000000000006";
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image(180, 12));
  fixture.Fields(source, ("output.format", new ConstantSetting("rgb24")), ("output.mono8", new ConstantSetting(true)));
  fixture.Connect(source, blob);
  fixture.Main.SelectedNode = blob;
  fixture.Run();
  var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  ResultWorkspaceImageSource Output() => Assert.IsType<ResultWorkspaceImageSource>(shell.SelectedOutputItem);
  var binaryChoice = shell.OutputItems!.Cast<ResultWorkspaceImageSource>().Single(item => item.Reference.PortId == binary);
  shell.SetCurrentValue(ResultWorkspaceShell.SelectedOutputItemProperty, binaryChoice);
  var saved = blob.SelectedDisplayReference;
  Assert.Equal(binaryChoice.Key, saved);

  var window = Open(fixture, blob);
  Assert.Equal(binary, Output().Reference.PortId);
  Assert.False(Output().HasImageValue);
  var imageRow = UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Single(row => row.FieldId == "input.image");
  var picker = Assert.IsType<ComboBox>(imageRow.InputElement);
  picker.SelectedItem = picker.Items.OfType<DeclaredFieldRow.ImageSourceChoice>().Single(item =>
   item.Setting is ReferenceSetting setting && setting.Reference.PortId == "output.gray-image");
  UiRuntime.Pump();
  Assert.Equal("output.gray-image", Output().Reference.PortId);
  Assert.True(Output().HasImageValue);
  var roi = Assert.Single(UiRuntime.Visuals<NativeRoiEditor>(window.Editor).Where(item => !item.PolygonOnly));
  Assert.True(roi.PrepareInteraction?.Invoke());
  Assert.Equal(saved, blob.SelectedDisplayReference);

  shell.SetCurrentValue(ResultWorkspaceShell.IsDoubleImageProperty, true);
  UiRuntime.Pump();
  Assert.Equal("output.gray-image", Assert.IsType<ResultWorkspaceImageSource>(shell.SelectedInputItem).Reference.PortId);
  var context = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  var grayReference = Assert.IsType<ReferenceSetting>(context.PreparedDraft.Configuration.Fields.Read("input.image"));
  var area = UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Single(row => row.FieldId == "input.roi.area-type");
  Assert.True(area.TrySetConstant(1L));
  context.PreparedDraft.ApplyFields(new Dictionary<string, ParameterSetting> { ["input.roi.mask-image"] = grayReference });
  UiRuntime.Call(fixture.Main, "RefreshEditorDebugResult");
  picker.SelectedItem = picker.Items.OfType<DeclaredFieldRow.ImageSourceChoice>().Single(item =>
   item.Setting is ReferenceSetting setting && setting.Reference.PortId == "output.image");
  UiRuntime.Pump();
  Assert.Contains(shell.InputItems!.Cast<ResultWorkspaceImageSource>(), item => item.Reference.PortId == "output.gray-image");
  Assert.Equal("output.image", Assert.IsType<ResultWorkspaceImageSource>(shell.SelectedInputItem).Reference.PortId);
  Assert.Equal("output.gray-image", Output().Reference.PortId);

  UiRuntime.Await(window.CloseAndDrainAsync());
  Assert.Equal(binary, Output().Reference.PortId);
  Assert.Equal(saved, blob.SelectedDisplayReference);
 });

 [Fact] public void Actual_blob_roi_keeps_its_source_frame_during_drawing_and_rejects_a_retired_viewport()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image(200, 24));
  fixture.Connect(source, blob);
  fixture.Run();
  var window = Open(fixture, blob);
  var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  shell.SetCurrentValue(ResultWorkspaceShell.IsDoubleImageProperty, true);
  UiRuntime.Pump();
  var editor = Assert.Single(UiRuntime.Visuals<NativeRoiEditor>(window.Editor).Where(item => !item.PolygonOnly));
  Assert.True(editor.PrepareInteraction?.Invoke());
  var session = UiRuntime.Field<NativeRoiSession>(editor, "_session");
  Assert.NotNull(session);
  var rectangle = RoiGeometryEditing.Create("rectangle", new Point2(3, 3), new Point2(15, 15));
  var request = session.BeginDraw("rectangle");
  Assert.NotNull(request);
  Assert.True(window.Editor.HasPendingEdits);
  Assert.True(session.IsCurrent);
  var currentInput = Assert.IsType<ResultWorkspaceImageSource>(shell.SelectedInputItem);
  var obsoleteInput = currentInput.CreateInputItem();
  var frameNotices = 0;
  void CountFrameNotice() => frameNotices++;
  fixture.Main.RoiFrameAvailabilityChanged += CountFrameNotice;
  try
  {
   shell.SetCurrentValue(ResultWorkspaceShell.SelectedInputItemProperty, obsoleteInput);
   UiRuntime.Pump();
   Assert.Same(currentInput, shell.SelectedInputItem);
   Assert.Equal(0, frameNotices);
   Assert.Same(request, session.ActiveDraw);
   Assert.True(session.IsCurrent);
  }
  finally { fixture.Main.RoiFrameAvailabilityChanged -= CountFrameNotice; }
  Assert.True(session.CompleteDraw(request, rectangle));
  Assert.Single(session.Items);
  var second = session.BeginDraw("rectangle");
  Assert.NotNull(second);
  Assert.False(window.Editor.TryCapture(out _));
  shell.SetCurrentValue(ResultWorkspaceShell.IsDoubleImageProperty, false);
  UiRuntime.Pump();
  Assert.False(session.CompleteDraw(second, rectangle));
  shell.SetCurrentValue(ResultWorkspaceShell.IsDoubleImageProperty, true);
  UiRuntime.Pump();
  Assert.True(editor.PrepareInteraction?.Invoke());
  var resumed = UiRuntime.Field<NativeRoiSession>(editor, "_session");
  Assert.NotNull(resumed);
  Assert.NotSame(session, resumed);
  Assert.Single(resumed.Items);
  Assert.True(window.Editor.TryCapture(out var error), error);
  UiRuntime.Await(window.CloseAndDrainAsync());
 });
 [Fact] public void Blob_mask_drawing_display_and_inheritance_survive_save_and_reopen()=>UiRuntime.Run(()=>
 {
  const string maskMode = "input.roi.mask.inherit";
  const string maskSource = "input.roi.mask.polygon-strings";
  const string maskOutput = "field:8ca10000000040008000000000000124";
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  var consumer = fixture.Add("BlobAnalysis", 550, 360);
  fixture.ConfigureImage(source, fixture.Image(200, 24));
  fixture.Connect(source, blob);
  fixture.Connect(blob, consumer);
  var imagePort = fixture.Session.GetNodeSchema(fixture.Root.Id, source.Id)!.Outputs.Single(port => port.Id == "output.image");
  fixture.Fields(consumer, ("input.image", new ReferenceSetting(new OutputReference(source.Id,
   imagePort.Id, imagePort.Type, new[] { "data" }, scopeId: fixture.Root.Id))));
  fixture.Run();
  var window = Open(fixture, blob);
  var editor = Assert.Single(UiRuntime.Visuals<NativeRoiEditor>(window.Editor), item => item.PolygonOnly);
  Assert.True(editor.PrepareInteraction?.Invoke());
  var session = Assert.IsType<NativeRoiSession>(UiRuntime.Field<NativeRoiSession>(editor, "_session"));
  var request = session.BeginDraw("polygon");
  Assert.NotNull(request);
  var vertices = RoiDeclarations.Shape.Fields.Single(port => port.Id == "vertices").Type;
  var shape = new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?>
  {
   ["kind"] = "polygon", ["closed"] = true,
   ["vertices"] = new SequenceValue(vertices, new[] { new Point2(4, 4), new Point2(8, 4), new Point2(8, 8), new Point2(4, 8) }
    .Select(point => new PointGeometry(RoiDeclarations.ImageFrame, point)))
  });
  Assert.True(session.CompleteDraw(request, shape));
  Run(window);
  var snapshot = Assert.IsType<DeclaredOutputSnapshot>(window.Editor.CurrentDebugSnapshot);
  Assert.True(snapshot.IsSuccess);
  var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  var viewport = Assert.IsType<ImageViewBox>(shell.OutputViewport);
  IPolygon Mask() => Assert.Single(viewport.ShapeList.OfType<Tuple<string, object>>()
   .SelectMany(group => ((IEnumerable<object>)group.Item2).OfType<IPolygon>()));
  var orange = Color.FromRgb(255, 84, 0).ToString();
  Assert.Equal(orange, Mask().Fill);
  Assert.Equal(.5d, Mask().Opacity);
  var view = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
  view.SelectedPageIndex = 2;
  UiRuntime.Pump();
  var magenta = PresentationDeclarations.Color(255, 0, 255);
  Assert.True(view.TrySetFields(new Dictionary<string, ParameterSetting>
  {
   ["display.mask.ok"] = new ConstantSetting(magenta), ["display.mask.ng"] = new ConstantSetting(magenta),
   ["display.mask.opacity"] = new ConstantSetting(.75d)
  }));
  UiRuntime.Pump();
  Assert.Same(snapshot, window.Editor.CurrentDebugSnapshot);
  Assert.Equal(orange, Mask().Fill);
  Assert.Equal(Colors.Magenta.ToString(), Mask().Stroke);
  Assert.Equal(.75d, Mask().Opacity);
  var producerContext = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  Assert.True(producerContext.TryCommitEditor(out var producerError), producerError);
  UiRuntime.Await(window.CloseAndDrainAsync());
  Assert.False(session.IsCurrent);
  Assert.Empty(viewport.ShapeList?.OfType<Tuple<string, object>>()
   .SelectMany(group => ((IEnumerable<object>)group.Item2).OfType<IPolygon>()) ?? Enumerable.Empty<IPolygon>());

  fixture.Run();
  var inheriting = Open(fixture, consumer);
  try
  {
   shell.SetCurrentValue(ResultWorkspaceShell.IsDoubleImageProperty, true);
   var roi = Assert.Single(UiRuntime.Visuals<NativeRoiUiDraftControls>(inheriting.Editor));
   var creation = roi.Children.OfType<DeclaredFieldRow>().Single(row => row.FieldId == "roi-mask-creation");
   Assert.True(creation.TrySetConstant(1L));
   inheriting.UpdateLayout();
   UiRuntime.Pump();
   var sourceRow = roi.Children.OfType<DeclaredFieldRow>().Single(row => row.FieldId == maskSource);
   Assert.True(sourceRow.IsVisible);
   var referenceEditor = Assert.IsType<FieldRowModel>(sourceRow.Content).ReferenceDataA!;
   object? Read(object owner, string property) => owner.GetType().GetProperty(property)!.GetValue(owner);
   void Execute(string property, object? argument = null) =>
    Assert.IsAssignableFrom<System.Windows.Input.ICommand>(Read(referenceEditor, property)).Execute(argument);
   IEnumerable<object> Nodes(object owner, string property) =>
    Assert.IsAssignableFrom<System.Collections.IEnumerable>(Read(owner, property)).Cast<object>();
   IEnumerable<object> Descendants(object node) => new[] { node }.Concat(Nodes(node, "EnumEntrysA").SelectMany(Descendants));
   void SelectSource()
   {
    Execute("DropDownCommand");
    var candidate = Nodes(referenceEditor, "SourceNodes").SelectMany(Descendants).Single(node =>
     Read(node, "Candidate") is DeclaredReferenceCandidate item && item.NodeId == blob.Id && item.Output.Id == maskOutput);
    Execute("ClickCommand", candidate);
    UiRuntime.Pump();
   }
   SelectSource();
   Assert.Equal(blob.Id, Assert.IsType<ReferenceSetting>(sourceRow.Setting).Reference.NodeId);
   Assert.IsType<UnsetSetting>(consumer.Configuration.Fields.Read(maskSource));
   var inputViewport = Assert.IsType<ImageViewBox>(shell.InputViewport);
   Assert.Single(inputViewport.ShapeList.OfType<Tuple<string, object>>()
    .SelectMany(group => ((IEnumerable<object>)group.Item2).OfType<IPolygon>()));
   Assert.True(creation.TrySetConstant(0L));
   UiRuntime.Pump();
   Assert.False(sourceRow.IsVisible);
   Assert.IsType<UnsetSetting>(sourceRow.Setting);
   Assert.Empty(inputViewport.ShapeList.OfType<Tuple<string, object>>()
    .SelectMany(group => ((IEnumerable<object>)group.Item2).OfType<IPolygon>()));
   Assert.True(creation.TrySetConstant(1L));
   UiRuntime.Pump();
   Assert.False(inheriting.Editor.TryCapture(out _));
   SelectSource();
   Run(inheriting);
   var debug = Assert.IsType<DeclaredOutputSnapshot>(inheriting.Editor.CurrentDebugSnapshot);
   Assert.True(debug.IsSuccess, debug.ErrorMessage);
   var context = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(inheriting.Editor));
   Assert.True(context.TryCommitEditor(out var error), error);
  }
  finally { UiRuntime.Await(inheriting.CloseAndDrainAsync()); }

  fixture.Save();
  var reopened = ServiceInitializer.SolutionManager.Open(
   System.IO.Path.Combine(fixture.DirectoryPath, "document.solution"),
   _ => new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string, string>>()),
   _ => null, new System.Text.Json.JsonSerializerOptions());
  var restored = reopened.FindNode(fixture.Root.Id, consumer.Id)!;
  Assert.True(restored.Configuration.Fields.Get<bool>(maskMode));
  var restoredSource = Assert.IsType<ReferenceSetting>(restored.Configuration.Fields.Read(maskSource)).Reference;
  Assert.Equal(blob.Id, restoredSource.NodeId);
  Assert.Equal(maskOutput, restoredSource.PortId);
  var restoredWindow = Open(fixture, restored);
  try
  {
   var roi = Assert.Single(UiRuntime.Visuals<NativeRoiUiDraftControls>(restoredWindow.Editor));
   Assert.True(roi.IsInheritedMask);
   var row = roi.Children.OfType<DeclaredFieldRow>().Single(item => item.FieldId == maskSource);
   Assert.Equal(blob.Id, Assert.IsType<ReferenceSetting>(row.Setting).Reference.NodeId);
   Assert.True(roi.Children.OfType<DeclaredFieldRow>().Single(item => item.FieldId == "roi-mask-creation").TrySetConstant(0L));
  }
  finally { UiRuntime.Await(restoredWindow.CloseAndDrainAsync()); }
  Assert.True(restored.Configuration.Fields.Get<bool>(maskMode));
  fixture.Run();
  using var result = reopened.AcquirePublishedResult(fixture.Root.Id, consumer.Id);
  Assert.True(result?.Snapshot.IsSuccess, result?.Snapshot.ErrorMessage);
  var geometry = Assert.IsAssignableFrom<IToolResultGeometryOutput>(result!.Snapshot.Output);
  var inheritedMask = geometry.ResultGeometry.Groups.Single(group => group.Role == ResultGeometryRole.Mask);
  Assert.Equal("polygon", Assert.Single(inheritedMask.Shapes).Get<string>("kind"));
  Assert.Equal("4.000000,4.000000,8.000000,4.000000,8.000000,8.000000,4.000000,8.000000",
   Assert.Single(result.Values!.Get<SequenceValue>(maskOutput).Cast<string>()));
  var binary = result.Values!.Get<RecordValue>("field:8ca10000000040008000000000000006").Get<IReadOnlyImage>("data");
  var pixels = binary.CopyPixels();
  Assert.Equal(0, pixels[6 * binary.Layout.RowStrideBytes + 6]);
  Assert.Equal(255, pixels[12 * binary.Layout.RowStrideBytes + 12]);
 });

 [Fact] public void Result_row_recolors_its_native_rectangle_until_the_publication_is_replaced()=>UiRuntime.Run(()=>
 {
  const string binaryImage = "field:8ca10000000040008000000000000006";
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image(200, side: 6));
  Assert.Equal("mono8",source.Configuration.Fields.Get<string>("output.format"));
  var sourceWindow = Open(fixture,source);
  var sourceRows = UiRuntime.Visuals<DeclaredFieldRow>(sourceWindow.Editor).ToDictionary(row=>row.FieldId);
  var outputMono8 = sourceRows["output.mono8"];
  Assert.Equal(Visibility.Collapsed,outputMono8.Visibility);
  Assert.True(sourceRows["output.format"].TrySetConstant("rgb24"));
  UiRuntime.Pump();
  Assert.Equal(Visibility.Visible,outputMono8.Visibility);
  Assert.True(outputMono8.TrySetConstant(true));
  Assert.True(sourceWindow.TryConfirm(out var commitError),commitError);
  UiRuntime.Await(sourceWindow.CloseAndDrainAsync());
  Assert.True(source.Configuration.Fields.Get<bool>("output.mono8"));
  fixture.Connect(source, blob);
  fixture.Fields(blob,
   ("field:8592aa1bb7394e81b462a917de7d3d76", new ConstantSetting("NoneSequentialContour")),
   ("display.blob.ok", new ConstantSetting(PresentationDeclarations.Color(255, 0, 255))),
   ("display.blob.ng", new ConstantSetting(PresentationDeclarations.Color(0, 0, 255))),
   ("display.blob.width", new ConstantSetting(3d)),
   ("display.blob.opacity", new ConstantSetting(.5d)));
  fixture.Main.SelectedNode = blob;
  fixture.Run();
  UiRuntime.Pump();

  using var first = fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id);
  Assert.NotNull(first);
  Assert.True(fixture.Session.IsPublishedResultCurrent(fixture.Root.Id, blob.Id, first.Snapshot));
  Assert.False(fixture.Session.IsPublishedResultCurrent(fixture.Root.Id, source.Id, first.Snapshot));

  var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  var choices = shell.OutputItems!.Cast<ResultWorkspaceImageSource>().ToArray();
  var mask = Assert.Single(choices, item => item.IsAuxiliaryImage);
  Assert.Same(mask, choices[^1]);
  var ordinaryNodeIds = choices.Where(item => !item.IsAuxiliaryImage).Select(item => item.Reference.NodeId).Distinct();
  Assert.Equal(new[] { source, blob }.OrderBy(node => node.Name, StringComparer.CurrentCulture).Select(node => node.Id), ordinaryNodeIds);
  var defaultImage = Assert.IsType<ResultWorkspaceImageSource>(shell.SelectedOutputItem);
  Assert.Equal(blob.Id, defaultImage.Reference.NodeId);
  Assert.Equal("field:8ca10000000040008000000000000005", defaultImage.Reference.PortId);
  var viewport = Assert.IsType<ImageViewBox>(shell.OutputViewport);
  DataGrid Table() => Assert.Single(UiRuntime.Visuals<DataGrid>(fixture.Main.SelectedNodeDataAnalysisControl!));
  IEnumerable<IRectangle> Rectangles() => viewport.ShapeList.OfType<Tuple<string, object>>()
   .SelectMany(group => ((IEnumerable<object>)group.Item2).OfType<IRectangle>());
  IRectangle Rectangle() => Rectangles().First();
  IRectangle Detection() => Rectangles().Last();
  void VerifyContourPalette()
  {
   var contour = Assert.Single(Assert.Single(viewport.ShapeList.OfType<IMultiPoint>()).OutlinePoints);
   Assert.Equal(Color.FromRgb(102, 255, 0).ToString(), contour.Stroke);
   Assert.Equal(Colors.Red.ToString(), contour.NgColor);
  }

  Assert.Equal(Colors.Magenta.ToString(), Rectangle().Stroke);
  Assert.Equal(3d, Rectangle().StrokeThickness);
  Assert.Equal(.5d, Rectangle().Opacity);
  var bounds = first.Values!.Read("field:8ca10000000040008000000000000100").GetElement(0).GetField("oriented-bounds");
  var center = bounds.GetField("center");
  Assert.Equal(new Point(center.GetField("x").GetScalar<double>(), center.GetField("y").GetScalar<double>()), Rectangle().CenterPoint);
  Assert.Equal(bounds.GetField("width").GetScalar<double>(), Rectangle().Width);
  Assert.Equal(bounds.GetField("height").GetScalar<double>(), Rectangle().Height);
  Assert.Equal(bounds.GetField("angle-degrees").GetScalar<double>(), Rectangle().Angle, 10);
  VerifyContourPalette();
  Assert.Equal(Colors.Blue.ToString(), Detection().Stroke);
  Assert.Equal(new Point(3, 3), Detection().CenterPoint);
  Assert.Equal((6d, 6d), (Detection().Width, Detection().Height));
  var table=Table();
  Assert.Equal(FooterLabels("Number","Area","Perimeter","CentroidX","CentroidY","Angle","LongAxis","ShortAxis","Circularity","Rectangularity"),
   table.Columns.Select(column=>column.Header as string));
  Assert.All(table.Columns.OfType<DataGridTextColumn>(),column=>Assert.Null(((System.Windows.Data.Binding)column.Binding).StringFormat));
  var firstRow=Assert.Single(table.Items.Cast<object>());
  var cell=firstRow.GetType().GetProperty("Item")!;
  Assert.Equal(0L,cell.GetValue(firstRow,new object[]{"label"}));
  var publishedArea=first.Values!.Get<double[]>("field:8ca10000000040008000000000000019")[0];
  Assert.Equal(((float)publishedArea).ToString(System.Globalization.CultureInfo.CurrentCulture),cell.GetValue(firstRow,new object[]{"area"}));
  Table().SelectedIndex = 0;
  UiRuntime.Pump();
  Assert.Equal(Colors.Yellow.ToString(), Rectangle().Stroke);
  Assert.Equal(Colors.Blue.ToString(), Detection().Stroke);
  VerifyContourPalette();

  var gray = choices.Single(item => item.Reference.NodeId == source.Id && item.Reference.PortId == "output.gray-image");
  shell.SetCurrentValue(ResultWorkspaceShell.SelectedOutputItemProperty, gray);
  UiRuntime.Pump();
  Assert.Same(gray, shell.SelectedOutputItem);
  // 原件 ModuleResultView.xaml:142 的 ShapeList 恒绑模块结果；imageListCombobox_SelectionChanged
  // （ModuleResultView.xaml.cs:2152-2153）只在 SelectedValue==null 时清空。换显示图像仅换背景，
  // 矩形与文字必须保留；仅跨坐标域的轮廓点不重采样。
  Assert.Equal(Colors.Yellow.ToString(), Rectangle().Stroke);
  Assert.NotEmpty(viewport.ShapeList.OfType<Tuple<string, object>>());
  Assert.NotEmpty(viewport.ShapeList.OfType<IMultiText>());
  Assert.Empty(viewport.ShapeList.OfType<IMultiPoint>());
  Assert.True(fixture.Session.IsPublishedResultCurrent(fixture.Root.Id, blob.Id, first.Snapshot));

  var binary = shell.OutputItems!.Cast<ResultWorkspaceImageSource>().Single(
   item => item.Reference.NodeId == blob.Id && item.Reference.PortId == binaryImage);
  shell.SetCurrentValue(ResultWorkspaceShell.SelectedOutputItemProperty, binary);
  UiRuntime.Pump();
  Assert.Equal(Colors.Yellow.ToString(), Rectangle().Stroke);

  fixture.Run();
  UiRuntime.Pump();
  Assert.False(fixture.Session.IsPublishedResultCurrent(fixture.Root.Id, blob.Id, first.Snapshot));
  Assert.NotNull(first.Values);
  using var replacement = fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id);
  Assert.NotNull(replacement);
  Assert.True(fixture.Session.IsPublishedResultCurrent(fixture.Root.Id, blob.Id, replacement.Snapshot));
  Assert.Equal(-1, Table().SelectedIndex);
  Assert.Equal(Colors.Magenta.ToString(), Rectangle().Stroke);
  VerifyContourPalette();
  fixture.Fields(source,("output.mono8",new ConstantSetting(false)));
  fixture.Run();
  using var sourceResult=fixture.Session.AcquirePublishedResult(fixture.Root.Id,source.Id);
  Assert.NotNull(sourceResult);
  Assert.True(sourceResult.Values!.Read("output.gray-image").IsNull);
  Assert.False(sourceResult.Values.Read("output.image").IsNull);
 });
 [Fact] public void Blob_text_uses_content_count_and_keeps_fixed_text_when_no_blobs_are_found()=>UiRuntime.Run(()=>
 {
  const string threshold = "field:43bda03261df55b88f963bd18f2ba4d9";
  const string areaEnabled = "field:e9845ab7a46e5ee9b42310bef5a11c19";
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  var image = fixture.Image(side: 8);
  var pixels = new byte[64];
  pixels[2 * 8 + 2] = pixels[2 * 8 + 3] = 200;
  pixels[3 * 8 + 2] = pixels[4 * 8 + 2] = 200;
  pixels[5 * 8 + 5] = pixels[5 * 8 + 6] = 200;
  System.IO.File.WriteAllBytes(image, System.Text.Encoding.ASCII.GetBytes("P5\n8 8\n255\n").Concat(pixels).ToArray());
  fixture.ConfigureImage(source, image);
  fixture.Connect(source, blob);
  fixture.Main.SelectedNode = blob;
  var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  var viewport = Assert.IsType<ImageViewBox>(shell.OutputViewport);
  IText[] Texts() => viewport.ShapeList.OfType<IMultiText>().SelectMany(item => item.Texts).ToArray();

  fixture.Fields(blob, (threshold, new ConstantSetting(100L)), (areaEnabled, new ConstantSetting(false)),
   ("display.text.content", new ConstantSetting("Blob面积:{面积}")));
  fixture.Run();
  UiRuntime.Pump();
  Assert.Equal(2, Texts().Length);

  var circularity = fixture.Session.GetNodeSchema(fixture.Root.Id, blob.Id)!.OutputDisplay
   .Single(output => output.DisplayName == "圆度").FieldId;
  var path = new ResultFieldPath(circularity);
  fixture.Fields(blob, ("display.text.content", new DisplayExpressionSetting("圆度:{圆度}/{精度}",
   new[] { new DisplayExpressionSlot("圆度", path), new DisplayExpressionSlot("精度", path, "G9") })));
  fixture.Run();
  UiRuntime.Pump();
  using (var result = fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id))
  {
   var values = result!.Values!.Get<double[]>(circularity);
   var culture = System.Globalization.CultureInfo.CurrentCulture;
   Assert.Contains(values, value => value.ToString(culture) != ((float)value).ToString(culture));
   Assert.Equal(values.Select(value => $"圆度:{((float)value).ToString(culture)}/{((float)value).ToString("G9", culture)}"),
    Texts().Select(item => item.Content));
  }

  fixture.Fields(blob, ("display.text.content", new ConstantSetting("检测结果")),
   ("display.text.x", new ConstantSetting(1d)), ("display.text.y", new ConstantSetting(2d)));
  fixture.Run();
  UiRuntime.Pump();
  var text = Assert.Single(Texts());
  Assert.Equal("检测结果", text.Content);
  Assert.Equal(new Point(1, 2), text.Position);

  fixture.Fields(blob, (threshold, new ConstantSetting(255L)));
  fixture.Run();
  UiRuntime.Pump();
  text = Assert.Single(Texts());
  Assert.Equal("检测结果", text.Content);
  Assert.Equal(Colors.Red.ToString(), text.Stroke);
 });
 [Fact] public void Blob_display_edits_repaint_the_current_debug_result_until_execution_fields_change()=>UiRuntime.Run(()=>
 {
  const string threshold = "field:43bda03261df55b88f963bd18f2ba4d9";
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image(200, 6));
  fixture.Connect(source, blob);
  fixture.Run();
  using var formal = fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id);
  Assert.NotNull(formal);
  var window = Open(fixture, blob);
  Assert.Equal(formal.Snapshot.Identity, fixture.Main.CurrentResultPublication?.Stamp.Invocation);
  Run(window);
  var snapshot = window.Editor.CurrentDebugSnapshot!;
  var scope = window.Editor.DebugScopeId;
  var context = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  var executionRevision = context.PreparedDraft.ExecutionRevision;
  var view = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
  view.SelectedPageIndex = 2;
  UiRuntime.Pump();
  Assert.True(view.TrySetFields(new Dictionary<string, ParameterSetting>
  {
   ["display.text.content"] = new ConstantSetting("私有结果"),
   ["display.text.x"] = new ConstantSetting(1d),
   ["display.text.y"] = new ConstantSetting(2d),
   ["display.text.ok"] = new ConstantSetting(PresentationDeclarations.Color(255, 0, 255))
  }));
  UiRuntime.Pump();
  Assert.Equal(executionRevision, context.PreparedDraft.ExecutionRevision);
  Assert.Same(snapshot, window.Editor.CurrentDebugSnapshot);
  Assert.Equal(snapshot.Identity, fixture.Main.CurrentResultPublication!.Stamp.Invocation);
  var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  var viewport = Assert.IsType<ImageViewBox>(shell.OutputViewport);
  var text = Assert.Single(viewport.ShapeList.OfType<IMultiText>().SelectMany(item => item.Texts));
  Assert.Equal("私有结果", text.Content);
  Assert.Equal(new Point(1, 2), text.Position);
  Assert.Equal(Colors.Magenta.ToString(), text.Stroke);

  var visibility = new[] { "blob", "inner-rectangle", "inspection", "outline", "text", "mask", "annulus" }
   .ToDictionary(group => $"display.{group}.visible", _ => (ParameterSetting)new ConstantSetting(false));
  Assert.True(view.TrySetFields(visibility));
  UiRuntime.Pump();
  Assert.Empty(viewport.ShapeList.OfType<Tuple<string, object>>());
  Assert.Empty(viewport.ShapeList.OfType<IMultiText>());
  Assert.Empty(viewport.ShapeList.OfType<IMultiPoint>());
  Assert.Same(snapshot, window.Editor.CurrentDebugSnapshot);
  Assert.True(view.TrySetFields(new Dictionary<string,ParameterSetting> { ["display.text.visible"] = new ConstantSetting(true) }));
  UiRuntime.Pump();
  Assert.Empty(viewport.ShapeList.OfType<Tuple<string, object>>());
  Assert.Equal("私有结果", Assert.Single(viewport.ShapeList.OfType<IMultiText>().SelectMany(item => item.Texts)).Content);
  Assert.Same(snapshot, window.Editor.CurrentDebugSnapshot);
  Assert.Equal(executionRevision, context.PreparedDraft.ExecutionRevision);

  view.SelectedPageIndex = 1;
  UiRuntime.Pump();
  Assert.True(view.TrySetFields(new Dictionary<string, ParameterSetting> { [threshold] = new ConstantSetting(250L) }));
  Assert.True(context.PreparedDraft.ExecutionRevision > executionRevision);
  Assert.Null(window.Editor.CurrentDebugSnapshot);
  UiRuntime.Pump();
  Assert.Null(fixture.Main.CurrentResultPublication?.Stamp.Invocation);
  var inputImage = Assert.IsType<ResultWorkspaceImageSource>(shell.SelectedOutputItem);
  Assert.True(inputImage.IsUpstream);
  Assert.Equal(source.Id, inputImage.Reference.NodeId);
  Assert.Empty(viewport.ShapeList?.OfType<IMultiText>() ?? Enumerable.Empty<IMultiText>());
  Assert.Equal(scope, window.Editor.DebugScopeId);
  UiRuntime.Await(window.CloseAndDrainAsync());
  Assert.Equal(formal.Snapshot.Identity, fixture.Main.CurrentResultPublication?.Stamp.Invocation);
  Assert.Equal(blob.Id, Assert.IsType<ResultWorkspaceImageSource>(shell.SelectedOutputItem).Reference.NodeId);
 });
 [Theory]
 [InlineData("input.roi.output-mask","field:8ca10000000040008000000000000103",true)]
 [InlineData("field:2fff619e51a359a6a9af286c5e8fef65","field:8ca10000000040008000000000000005",false)]
 [InlineData("field:73f2c01e7b385e0ab2d35f739a88e718","field:8ca10000000040008000000000000006",false)]
 public void Actual_result_workspace_clears_and_restores_the_selected_output_image(string enabledField,string imagePort,bool isMask)=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image(200, side: 6));
  fixture.Connect(source, blob);
  fixture.Fields(blob, (enabledField, new ConstantSetting(true)));
  fixture.Main.SelectedNode = blob;
  fixture.Run();
  UiRuntime.Pump();

  var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  var viewport = Assert.IsType<ImageViewBox>(shell.OutputViewport);
  ResultWorkspaceImageSource ImageChoice() => shell.OutputItems!.Cast<ResultWorkspaceImageSource>().Single(
   item => item.Reference.NodeId == blob.Id && item.Reference.PortId == imagePort);
  var enabled = ImageChoice();
  Assert.True(enabled.HasImageValue);
  shell.SetCurrentValue(ResultWorkspaceShell.SelectedOutputItemProperty, enabled);
  UiRuntime.Pump();
  var rendered = Assert.IsAssignableFrom<SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface.IImageData>(viewport.ImageDataSource);
  Assert.Equal((6, 6), (rendered.Width, rendered.Height));
  using (var reader = fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id))
  {
   var image = reader!.Values!.Get<RecordValue>(imagePort).Get<IReadOnlyImage>("data");
   var expected = Enumerable.Range(0, 36).Select(index => isMask || index / 6 != 0 && index % 6 != 0 ? (byte)255 : (byte)0);
   Assert.Equal(expected, image.CopyPixels());
  }

  fixture.Fields(blob, (enabledField, new ConstantSetting(false)));
  fixture.Run();
  UiRuntime.Pump();
  Assert.False(enabled.Current());
  Assert.Same(ImageChoice(), shell.SelectedOutputItem);
  Assert.False(ImageChoice().HasImageValue);
  Assert.Null(viewport.ImageDataSource);
  Assert.Empty(viewport.ShapeList.OfType<IMultiText>());

  fixture.Fields(blob, (enabledField, new ConstantSetting(true)));
  fixture.Run();
  UiRuntime.Pump();
  var restored = ImageChoice();
  Assert.Same(restored, shell.SelectedOutputItem);
  Assert.True(restored.HasImageValue);
  Assert.NotEqual(enabled.Publication!.Stamp.Invocation, restored.Publication!.Stamp.Invocation);
  Assert.NotNull(viewport.ImageDataSource);
 });
 [Fact] public void Blob_output_overlay_is_visible_only_on_its_background_and_retires_on_a_new_choice_plan()=>UiRuntime.Run(()=>
 {
  using var fixture=new DocumentUiFixture();
  var source=fixture.Add();
  var blob=fixture.Add("BlobAnalysis",350,240);
  fixture.ConfigureImage(source,fixture.Image(200,6));
  fixture.Connect(source,blob);
  fixture.Main.SelectedNode=blob;
  fixture.Run();
  var shell=Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  var viewport=Assert.IsType<ImageViewBox>(shell.OutputViewport);
  ResultWorkspaceImageSource Choice(string port)=>shell.OutputItems!.Cast<ResultWorkspaceImageSource>().Single(item=>item.Reference.PortId==port);
  System.Windows.Controls.Image[] Layers()=>viewport.ImageLayers.Children.OfType<System.Windows.Controls.Image>()
    .Where(image=>Panel.GetZIndex(image) is >=100 and <=102).ToArray();
  var background=Choice("field:8ca10000000040008000000000000005");
  var binary=Choice("field:8ca10000000040008000000000000006");
  Assert.Same(background,shell.SelectedOutputItem);
  var export=Assert.IsType<Func<bool,System.Windows.Media.Imaging.BitmapSource>>(typeof(ImageViewBox)
    .GetProperty("NativePrepareExport",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(viewport));
  byte[] Export(bool rendered)
  {
   var image=export(rendered);
   var stride=(image.PixelWidth*image.Format.BitsPerPixel+7)/8;
   var buffer=new byte[stride*image.PixelHeight];
   image.CopyPixels(buffer,stride,0);
   return buffer;
  }
  var raw=Export(false);
  var rendered=Export(true);
  var toggle=shell.SelectOverlayCommand!;
  Assert.True(toggle.CanExecute(binary));
  toggle.Execute(binary);
  UiRuntime.Pump();
  background=Choice(background.Reference.PortId);
  binary=Choice(binary.Reference.PortId);
  Assert.True(background.IsBackgroundImage);
  Assert.True(binary.IsCheck);
  Assert.Equal(raw,Export(false));
  Assert.Equal(rendered,Export(true));
  Assert.True(shell.SelectBackgroundCommand!.CanExecute(binary));
  var layer=Assert.Single(Layers());
  Assert.False(layer.IsHitTestVisible);
  var bitmap=Assert.IsAssignableFrom<System.Windows.Media.Imaging.BitmapSource>(layer.Source);
  var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];
  bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);
  Assert.All(Enumerable.Range(0,bitmap.PixelWidth*bitmap.PixelHeight),index=>Assert.Equal((byte)191,bytes[index*4+3]));
  viewport._imageViewWidth=3;
  viewport._imageViewHeight=2;
  viewport.OnFreshExtendContent?.Invoke(viewport,null!);
  UiRuntime.Pump();
  var sampled=Assert.IsAssignableFrom<System.Windows.Media.Imaging.BitmapSource>(Assert.Single(Layers()).Source);
  Assert.Equal((3,2),(sampled.PixelWidth,sampled.PixelHeight));
  var sampledBytes=new byte[24];
  sampled.CopyPixels(sampledBytes,12,0);
  Assert.Equal(new byte[]{0,0,0,0,255,255},Enumerable.Range(0,6).Select(index=>sampledBytes[index*4]));
  Assert.All(Enumerable.Range(0,6),index=>Assert.Equal((byte)191,sampledBytes[index*4+3]));
  shell.SetCurrentValue(ResultWorkspaceShell.SelectedOutputItemProperty,binary);
  UiRuntime.Pump();
  Assert.Empty(Layers());
  shell.SetCurrentValue(ResultWorkspaceShell.SelectedOutputItemProperty,Choice(background.Reference.PortId));
  UiRuntime.Pump();
  Assert.Single(Layers());
  fixture.Run();
  Assert.False(toggle.CanExecute(binary));
  Assert.True(Choice(binary.Reference.PortId).IsCheck);
  Assert.Single(Layers());
  fixture.Save();
  var reopened=ServiceInitializer.SolutionManager.Open(System.IO.Path.Combine(fixture.DirectoryPath,"document.solution"),
   _=>new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()),
   _=>null,new System.Text.Json.JsonSerializerOptions());
  UiRuntime.Pump();
  fixture.Main.SelectedNode=reopened.FindNode(fixture.Root.Id,blob.Id);
  fixture.Run();
  Assert.True(Choice(background.Reference.PortId).IsBackgroundImage);
  Assert.True(Choice(binary.Reference.PortId).IsCheck);
  Assert.Single(Layers());
  fixture.Main.SelectedNode=reopened.FindNode(fixture.Root.Id,source.Id);
  UiRuntime.Pump();
  Assert.Empty(Layers());
 });
 [Fact] public void Overlay_commands_restore_rejected_checks_and_keep_background_layer_order()=>UiRuntime.Run(()=>
 {
  const string blobImage="field:8ca10000000040008000000000000005",binaryImage="field:8ca10000000040008000000000000006";
  using var fixture=new DocumentUiFixture();
  var source=fixture.Add();
  var blob=fixture.Add("BlobAnalysis",350,240);
  fixture.ConfigureImage(source,fixture.Image(200,24));
  fixture.Connect(source,blob);
  fixture.Main.SelectedNode=blob;
  fixture.Run();
  var shell=Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  var viewport=Assert.IsType<ImageViewBox>(shell.OutputViewport);
  var toggle=shell.SelectOverlayCommand!;
  var swap=shell.SelectBackgroundCommand!;
  var owner=Assert.IsAssignableFrom<Window>(Window.GetWindow(shell));
  ResultWorkspaceImageSource Choice(string port)=>shell.OutputItems!.Cast<ResultWorkspaceImageSource>().Single(item=>item.Reference.PortId==port);
  void Toggle(string port)
  {
   var choice=Choice(port);
   Assert.True(toggle.CanExecute(choice));
   toggle.Execute(choice);
   UiRuntime.Pump();
  }
  void Answer(string button,Action execute,string? expectedMessage=null)
  {
   string? message=null;
   owner.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>
   {
    var dialog=owner.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.NativeMessageBox>()
     .SingleOrDefault(item=>item.IsVisible);
    if(dialog is null)return;
    if(dialog.FindName(button) is not Button response){dialog.Close();return;}
    message=dialog.Message;
    response.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   }));
   execute();
   Assert.NotNull(message);
   if(expectedMessage is not null)Assert.Equal(expectedMessage,message);
   UiRuntime.Pump();
  }
  void Swap(string button)
  {
   var choice=Choice("output.image");
   Assert.True(swap.CanExecute(choice));
   Answer(button,()=>swap.Execute(choice));
  }
  Assert.Same(Choice(blobImage),shell.SelectedOutputItem);
  var empty=Choice("field:8ca10000000040008000000000000103");
  Assert.False(empty.HasImageValue);
  Assert.True(toggle.CanExecute(empty));
  var restored=0;
  empty.PropertyChanged+=(_,args)=>{if(args.PropertyName==nameof(ResultWorkspaceImageSource.IsCheck))restored++;};
  Answer("OkBtn",()=>toggle.Execute(empty),AppLocalizationService.GetLang("Lang_CheckImageEmptyTip","图像数据为空，不支持叠加显示。"));
  Assert.Equal(1,restored);
  Assert.False(empty.IsCheck);
  Assert.Null(blob.DisplayOverlays);
  Assert.Same(Choice(blobImage),shell.SelectedOutputItem);
  Toggle(binaryImage);
  Toggle("output.image");
  Toggle(binaryImage);
  Assert.Equal(new string?[] { null,Choice("output.image").Key,null },blob.DisplayOverlays!.Slots);
  Swap("NoBtn");
  Assert.Same(Choice(blobImage),shell.SelectedOutputItem);
  Assert.Equal(new string?[] { null,Choice("output.image").Key,null },blob.DisplayOverlays!.Slots);
  Swap("YesBtn");
  Assert.Same(Choice("output.image"),shell.SelectedOutputItem);
  Assert.Equal(new string?[] { Choice(blobImage).Key,null,null },blob.DisplayOverlays!.Slots);
  Assert.Single(viewport.ImageLayers.Children.OfType<System.Windows.Controls.Image>(),image=>Panel.GetZIndex(image)==100);

  shell.SetCurrentValue(ResultWorkspaceShell.SelectedOutputItemProperty,Choice(binaryImage));
  UiRuntime.Pump();
  Toggle("output.image");
  Assert.Null(blob.DisplayOverlays);
  Assert.Same(Choice(binaryImage),shell.SelectedOutputItem);
  Assert.True(Choice(binaryImage).IsCheck);
  Assert.DoesNotContain(viewport.ImageLayers.Children.OfType<System.Windows.Controls.Image>(),image=>Panel.GetZIndex(image) is >=100 and <=102);
 });
 [Fact] public void Optional_gray_image_retires_the_previous_frame_and_blocks_its_bound_blob_until_restored()=>UiRuntime.Run(()=>
 {
  const string grayPort="output.gray-image";
  const string mono8Field="output.mono8";
  using var fixture=new DocumentUiFixture();
  var source=fixture.Add();
  var blob=fixture.Add("BlobAnalysis",350,240);
  fixture.ConfigureImage(source,fixture.Image(200,6));
  fixture.Fields(source,("output.format",new ConstantSetting("rgb24")),(mono8Field,new ConstantSetting(true)));
  fixture.Connect(source,blob);
  var port=fixture.Session.GetNodeSchema(fixture.Root.Id,source.Id)!.Outputs.Single(port=>port.Id==grayPort);
  fixture.Fields(blob,("input.image",new ReferenceSetting(
   new OutputReference(source.Id,grayPort,port.Type,new[]{"data"},scopeId:fixture.Root.Id))));
  fixture.Main.SelectedNode=blob;
  fixture.Run();
  using var previousBlob=fixture.Session.AcquirePublishedResult(fixture.Root.Id,blob.Id);
  Assert.True(previousBlob?.Snapshot.IsSuccess,previousBlob?.Snapshot.ErrorMessage);
  var shell=Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
  var viewport=Assert.IsType<ImageViewBox>(shell.OutputViewport);
  ResultWorkspaceImageSource GrayChoice()=>shell.OutputItems!.Cast<ResultWorkspaceImageSource>().Single(
   item=>item.Reference.NodeId==source.Id&&item.Reference.PortId==grayPort);
  var previous=GrayChoice();
  shell.SetCurrentValue(ResultWorkspaceShell.SelectedOutputItemProperty,previous);
  UiRuntime.Pump();
  Assert.True(previous.HasImageValue);
  Assert.NotNull(viewport.ImageDataSource);

  fixture.Fields(source,(mono8Field,new ConstantSetting(false)));
  fixture.Run();
  Assert.False(previous.Current());
  Assert.Same(GrayChoice(),shell.SelectedOutputItem);
  Assert.False(GrayChoice().HasImageValue);
  Assert.Null(viewport.ImageDataSource);
  using(var current=fixture.Session.AcquirePublishedResult(fixture.Root.Id,source.Id))
  {
   Assert.True(current?.Snapshot.IsSuccess,current?.Snapshot.ErrorMessage);
   Assert.True(current!.Values!.Read(grayPort).IsNull);
   Assert.False(current.Values.Read("output.image").IsNull);
  }
  using(var failed=fixture.Session.AcquirePublishedResult(fixture.Root.Id,blob.Id))
  {
   Assert.NotNull(failed);
   Assert.False(failed.Snapshot.IsSuccess);
   Assert.Contains(grayPort,failed.Snapshot.ErrorMessage);
   Assert.False(fixture.Session.IsPublishedResultCurrent(fixture.Root.Id,blob.Id,previousBlob!.Snapshot));
  }

  fixture.Fields(source,(mono8Field,new ConstantSetting(true)));
  fixture.Run();
  var restored=GrayChoice();
  Assert.Same(restored,shell.SelectedOutputItem);
  Assert.True(restored.HasImageValue);
  Assert.NotEqual(previous.Publication!.Stamp.Invocation,restored.Publication!.Stamp.Invocation);
  Assert.NotNull(viewport.ImageDataSource);
  using var recovered=fixture.Session.AcquirePublishedResult(fixture.Root.Id,blob.Id);
  Assert.True(recovered?.Snapshot.IsSuccess,recovered?.Snapshot.ErrorMessage);
 });
 [Fact] public void Closing_active_actual_window_waits_for_real_host_prepare_before_disposing_scope()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);
  var run=editor.Window.Editor.RunDebugAsync();UiRuntime.Await(gate.Entered.Task);var close=editor.Window.CloseAndDrainAsync();
  Assert.False(close.IsCompleted);Assert.True(editor.Window.IsCurrent);Assert.True(editor.Window.Editor.IsDebugBusy);
  gate.Release.TrySetResult();UiRuntime.Await(close);try{UiRuntime.Await(run);}catch(OperationCanceledException){}catch(InvalidOperationException){}
  Assert.True(close.Result);Assert.False(editor.Window.IsCurrent);Assert.False(editor.Context.IsValid);Assert.Null(f.Session.AcquirePublishedResult(f.Root.Id,node.Id));
 });
 [Fact] public void Image_source_stop_at_last_follows_local_auto_switch_without_clearing_the_draft()=>UiRuntime.Run(()=>
 {
  const string autoSwitch="local.auto-switch",stopAtLast="local.stop-at-last",sourceKind="source.kind";
  using var fixture=new DocumentUiFixture();
  var node=fixture.Add();
  fixture.ConfigureImage(node,fixture.Image());
  using var factory=new ImageSourceHostInputFactory();
  using var editor=new PrivateWindow(fixture,node,factory);
  var window=editor.Window;
  var settings=Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
  bool StopRowVisible()=>UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Any(row=>row.FieldId==stopAtLast&&row.IsVisible);
  void Edit(string field,object value)
  {
   Assert.True(settings.TrySetFields(new Dictionary<string,ParameterSetting> { [field]=new ConstantSetting(value) }));
   window.UpdateLayout();
   UiRuntime.Pump();
  }
  Edit(autoSwitch,true);
  Edit(stopAtLast,true);
  Assert.True(StopRowVisible());
  Edit(autoSwitch,false);
  Assert.False(StopRowVisible());
  Assert.Equal(true,editor.Context.ReadCurrentParameter(stopAtLast)?.Value);
  Edit(autoSwitch,true);
  Assert.True(StopRowVisible());
  Edit(sourceKind,"camera");
  Assert.False(StopRowVisible());
  Edit(sourceKind,"local");
  Assert.True(StopRowVisible());
  Assert.Equal(true,editor.Context.ReadCurrentParameter(stopAtLast)?.Value);
  Assert.False(node.Configuration.Fields.Get<bool>(stopAtLast));
 });
 [Fact] public void New_raw_text_during_real_debug_is_preserved_and_late_private_values_are_rejected()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);
  var settings=UiRuntime.Visuals<DeclaredSettingsView>(editor.Window.Editor).Single();var fields=(SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView)settings.Content;
  var text=Assert.IsType<TextBox>(fields.Fields.Single(item=>item.FieldId=="sequence.initial").InputElement);var body=editor.Window.Editor.Content;
  var run=editor.Window.Editor.RunDebugAsync();UiRuntime.Await(gate.Entered.Task);text.Text="尚未输入完";gate.Release.TrySetResult();
  Assert.ThrowsAny<Exception>(()=>UiRuntime.Await(run));Assert.Equal("尚未输入完",text.Text);Assert.Same(body,editor.Window.Editor.Content);Assert.Null(editor.Window.Editor.CurrentDebugSnapshot);
  text.Text="12";Assert.True(editor.Context.TryCommitEditor(out var error),error);Assert.Equal(12L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("sequence.initial")).Value);
 });
 [Fact] public void Deleted_actual_node_cancels_debug_and_undo_cannot_accept_the_retired_window_result()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);
  var run=editor.Window.Editor.RunDebugAsync();UiRuntime.Await(gate.Entered.Task);f.Canvas.Delete(new[]{f.Projection(node)},Array.Empty<SunEyeVision.UI.Features.WorkflowUI.Canvas.CanvasConnectionProjection>());
  Assert.False(editor.Context.IsValid);gate.Release.TrySetResult();Assert.ThrowsAny<Exception>(()=>UiRuntime.Await(run));
  UiRuntime.Await(editor.Window.CloseAndDrainAsync());f.Session.Undo();Assert.Same(node,f.Session.FindNode(f.Root.Id,node.Id));Assert.False(editor.Context.IsValid);Assert.Null(f.Session.AcquirePublishedResult(f.Root.Id,node.Id));
 });
 [Fact] public void Continuous_stop_is_unavailable_during_one_shot_execution()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);var scope=editor.Window.Editor.DebugScopeId;
  var run=editor.Window.Editor.RunDebugAsync();UiRuntime.Await(gate.Entered.Task);
  var buttons=VisibleFooterButtons(editor.Window);
  Assert.Equal(FooterLabels("Lang_MutiRun", "Execute", "Confirm"), buttons.Select(button => button.Content as string));
  Assert.All(buttons, button => Assert.False(button.IsEnabled));
  Assert.False(editor.Window.Stop.CanExecute(null));editor.Window.Stop.Execute(null);gate.Release.TrySetResult();
  UiRuntime.Await(run);
  Assert.True(editor.Window.Editor.CurrentDebugSnapshot?.IsSuccess);
  Assert.True(editor.Context.IsValid);Assert.Equal(scope,editor.Window.Editor.DebugScopeId);Run(editor.Window);Assert.Equal(scope,editor.Window.Editor.DebugScopeId);
 });
 [Fact] public void Failed_execute_uses_an_owned_native_dialog_without_filling_the_external_slot()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var node = fixture.Add();
  fixture.ConfigureImage(node, System.IO.Path.Combine(fixture.DirectoryPath, "missing-image.pgm"));
  var window = Open(fixture, node);
  var frame = Assert.IsType<NativeToolEditorFrame>(window.Content);
  var external = Assert.IsType<Border>(frame.FindName("ExternalSlot"));
  Assert.Equal(Visibility.Collapsed, external.Visibility);
  var dialogs = new List<NativeMessageBox>();
  var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
  timer.Tick += (_, _) =>
  {
   if (window.OwnedWindows.OfType<NativeMessageBox>().FirstOrDefault(item => item.IsVisible) is not { } dialog) return;
   dialogs.Add(dialog);
   dialog.Close();
  };
  timer.Start();
  try { UiRuntime.Await(Assert.IsAssignableFrom<Task>(UiRuntime.Call(window, "RunOnceAsync"))); }
  finally { timer.Stop(); }
  Assert.Single(dialogs);
  Assert.True(window.IsVisible);
  Assert.Null(frame.ExternalContent);
  Assert.Equal(Visibility.Collapsed, external.Visibility);
  Assert.True(window.ExecuteOnce.CanExecute(null));
  UiRuntime.Await(window.CloseAndDrainAsync());
 });

 [Fact] public void Retired_execution_does_not_publish_an_error_over_the_edited_window()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  fixture.ConfigureImage(source, fixture.Image());
  using var gate = new GatedFactory();
  using var editor = new PrivateWindow(fixture, source, gate);
  var window = editor.Window;
  var completions = new List<RuntimeToolExecutionEventArgs>();
  window.ToolExecutionCompleted += (_, result) => completions.Add(result);
  var run = Assert.IsAssignableFrom<Task>(UiRuntime.Call(window, "RunOnceAsync"));
  UiRuntime.Await(gate.Entered.Task);
  var settings = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
  Assert.True(settings.TrySetFields(new Dictionary<string, ParameterSetting> { ["sequence.initial"] = new ConstantSetting(12L) }));
  gate.Release.TrySetResult();
  UiRuntime.Await(run);
  Assert.Empty(completions);
  Assert.Null(window.Editor.CurrentDebugSnapshot);
  Assert.Equal(12L, editor.Context.ReadCurrentParameter("sequence.initial")?.Value);
  Run(window);
  Assert.NotNull(Assert.Single(completions).Result);
 });
 [Fact] public void Receipt_rebase_during_running_rejects_old_invocation_and_preserves_scope_for_new_actual_draft()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);var scope=editor.Window.Editor.DebugScopeId;
  var previous=editor.Context.PreparedDraft;var run=editor.Window.Editor.RunDebugAsync();UiRuntime.Await(gate.Entered.Task);
  Assert.True(editor.Context.TryCommitEditor(out var error),error);Assert.NotSame(previous,editor.Context.PreparedDraft);Assert.Equal(scope,editor.Window.Editor.DebugScopeId);
  gate.Release.TrySetResult();var failure=Assert.ThrowsAny<Exception>(()=>UiRuntime.Await(run));Assert.True(failure is OperationCanceledException or InvalidOperationException,failure.ToString());Assert.Null(editor.Window.Editor.CurrentDebugSnapshot);
  Run(editor.Window);Assert.Equal(scope,editor.Window.Editor.DebugScopeId);
 });
 [Fact] public void Closing_from_first_capture_notification_waits_for_admission_to_unwind()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  fixture.ConfigureImage(source, fixture.Image());
  using var gate = new GatedFactory();
  using var editor = new PrivateWindow(fixture, source, gate);
  Task<bool>? close = null;
  var observed = false;
  var wasBusy = false;
  var waited = false;
  editor.Window.Changed += (_, _) =>
  {
   if (observed) return;
   observed = true;
   wasBusy = editor.Window.Editor.IsDebugBusy;
   close = editor.Window.CloseAndDrainAsync();
   waited = !close.IsCompleted;
  };
  var execution = editor.Window.Editor.RunDebugAsync();
  Assert.ThrowsAny<OperationCanceledException>(() => UiRuntime.Await(execution));
  Assert.True(wasBusy);
  Assert.True(waited);
  Assert.NotNull(close);
  UiRuntime.Await(close!);
  Assert.True(close!.GetAwaiter().GetResult());
  Assert.False(gate.Entered.Task.IsCompleted);
  Assert.False(editor.Window.IsCurrent);
 });
 [Fact] public void Continuous_stop_drains_real_host_prepare_and_allows_same_scope_to_restart()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);
  var scope=editor.Window.Editor.DebugScopeId;
  var results=new List<RuntimeToolExecutionEventArgs>();
  editor.Window.ToolExecutionCompleted+=(_,result)=>results.Add(result);
  Assert.True(editor.Window.StartContinuous.CanExecute(null));
  var idleButtons=VisibleFooterButtons(editor.Window);
  var run=editor.Window.Editor.StartContinuousDebugAsync();UiRuntime.Await(gate.Entered.Task);
  Assert.True(editor.Window.State.IsContinuous);Assert.False(editor.Window.ExecuteOnce.CanExecute(null));Assert.False(editor.Window.Confirm.CanExecute(null));
  var runningButtons=VisibleFooterButtons(editor.Window);
  Assert.Equal(FooterLabels("Lang_StopRun", "Confirm"), runningButtons.Select(button => button.Content as string));
  Assert.Same(idleButtons[^1],runningButtons[^1]);
  Assert.True(runningButtons[0].IsEnabled);
  editor.Window.Stop.Execute(null);Assert.False(run.IsCompleted);Assert.True(editor.Window.Editor.IsDebugBusy);
  Assert.False(editor.Window.State.IsContinuous);
  Assert.False(editor.Window.Stop.CanExecute(null));
  var drainingButtons=VisibleFooterButtons(editor.Window);
  Assert.Equal(FooterLabels("Lang_MutiRun", "Execute", "Confirm"), drainingButtons.Select(button => button.Content as string));
  Assert.Equal(idleButtons,drainingButtons);
  Assert.All(drainingButtons, button => Assert.False(button.IsEnabled));
  gate.Release.TrySetResult();UiRuntime.Await(run);
  Assert.True(Assert.Single(results).Result?.IsSuccess);
  Assert.Single(editor.Window.Editor.DebugHistory!.Read());
  Assert.Equal(1,gate.PrepareCount);
  Assert.False(editor.Window.Editor.IsDebugBusy);Assert.False(editor.Window.State.IsContinuous);Assert.Equal(scope,editor.Window.Editor.DebugScopeId);
  var restartedButtons=VisibleFooterButtons(editor.Window);
  Assert.Equal(idleButtons,restartedButtons);
  Assert.All(restartedButtons, button => Assert.True(button.IsEnabled));
  Assert.True(editor.Window.StartContinuous.CanExecute(null));Run(editor.Window);
 });
 [Fact] public void Continuous_stop_during_owner_notification_prevents_dispatch_of_the_first_round()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());
  using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);
  var stopped=false;
  editor.Window.Changed+=(_,_) =>
  {
   if(stopped||!editor.Window.State.IsContinuous)return;
   stopped=true;
   editor.Window.Stop.Execute(null);
  };
  gate.Release.TrySetResult();
  UiRuntime.Await(editor.Window.Editor.StartContinuousDebugAsync());
  Assert.True(stopped);
  Assert.Equal(0,gate.PrepareCount);
  Assert.Null(editor.Window.Editor.CurrentDebugSnapshot);
  Assert.True(editor.Window.ExecuteOnce.CanExecute(null));
 });
 [Fact] public void Continuous_close_waits_for_inflight_capture_and_rejects_late_publication()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);
  var history=Assert.IsType<ResultHistoryBuffer>(editor.Window.Editor.DebugHistory);
  var publications=0;editor.Window.ToolExecutionCompleted+=(_,_)=>publications++;
  var run=editor.Window.Editor.StartContinuousDebugAsync();UiRuntime.Await(gate.Entered.Task);
  Task<bool>? nestedClose=null;
  editor.Window.Changed+=(_,_)=>nestedClose??=editor.Window.CloseAndDrainAsync();
  var close=editor.Window.CloseAndDrainAsync();Assert.False(close.IsCompleted);
  Assert.Same(close,nestedClose);
  Assert.Same(close,editor.Window.CloseAndDrainAsync());
  gate.Release.TrySetResult();UiRuntime.Await(close);UiRuntime.Await(run);
  Assert.True(close.Result);Assert.False(editor.Context.IsValid);Assert.Equal(0,publications);
  Assert.Empty(history.Read());
  Assert.Empty(f.Session.ReadResultHistory(f.Root.Id,node));
  Assert.Null(f.Session.AcquirePublishedResult(f.Root.Id,node.Id));
 });
 [Fact] public void Closing_from_result_state_notification_drains_without_forwarding_the_retired_result()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var node = fixture.Add();
  fixture.ConfigureImage(node, fixture.Image());
  using var gate = new GatedFactory();
  using var editor = new PrivateWindow(fixture, node, gate);
  var history = Assert.IsType<ResultHistoryBuffer>(editor.Window.Editor.DebugHistory);
  Task<bool>? close = null;
  var requestedClose = false;
  var completions = 0;
  editor.Window.ToolExecutionCompleted += (_, _) => completions++;
  editor.Window.Changed += (_, _) =>
  {
   if (requestedClose || editor.Window.Editor.CurrentDebugSnapshot is not { IsSuccess: true }) return;
   requestedClose = true;
   close = editor.Window.CloseAndDrainAsync();
  };
  var run = editor.Window.Editor.RunDebugAsync();
  UiRuntime.Await(gate.Entered.Task);
  gate.Release.TrySetResult();
  UiRuntime.Await(run);
  Assert.NotNull(close);
  UiRuntime.Await(close);
  Assert.True(close.Result);
  Assert.False(editor.Window.IsCurrent);
  Assert.Equal(0, completions);
  Assert.Empty(history.Read());
  Assert.Null(fixture.Session.AcquirePublishedResult(fixture.Root.Id, node.Id));
 });
 [Fact] public void Continuous_admission_rechecks_retirement_after_host_callback()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();using var scope=f.Session.OpenDebugSession(f.Root.Id,node);
  var captures=0;
  var run=scope.StartContinuousAsync(()=>{captures++;return Task.FromResult(true);},()=>true,()=>{scope.StopContinuous();return true;});
  UiRuntime.Await(run);Assert.Equal(0,captures);Assert.False(scope.IsContinuous);
 });
 private sealed class PrivateWindow:IDisposable
 {
  internal readonly RuntimeToolEditorWindow Window;internal readonly ProductionEditorContext Context;
  internal PrivateWindow(DocumentUiFixture f,NodeDefinition node,IHostInputFactory factory)
  {
   var control=new RuntimeNodeConfigurationControl();Window=new(control){Owner=f.Host,ShowInTaskbar=false};
   Context=new(f.Session,f.Root.Id,node,()=>ReferenceEquals(SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.CurrentSession,f.Session),control.BindDraft);
   ToolEditorCommit.AttachDraftContext(control,Context);Context.PrepareView=()=>control.TryCapture(out var error)?null:error;Context.HasPendingViewEdits=()=>control.HasPendingEdits;Context.PreserveViewAfterRebase=control.AdoptRebasedDraft;
   control.ConfigureDebug(Context,()=>factory,()=>true,_=>{},_=>null,()=>{});Window.SetRunAvailability(()=>true);
   Context.Deactivated+=control.Dispose;Context.Deactivated+=()=>{if(Window.IsLoaded)Window.Close();};Window.Closed+=(_,_)=>Context.Discard();
   control.BindDraft(Context.PreparedDraft);Window.Show();Window.SetAssemblyReady();UiRuntime.Pump();
  }
  public void Dispose(){UiRuntime.Await(Window.CloseAndDrainAsync());Context.Discard();}
 }
 private sealed class GatedFactory:IHostInputFactory,IDisposable
 {
  private readonly ImageSourceHostInputFactory _actual=new();internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
  private int _prepareCount;
  internal int PrepareCount=>Volatile.Read(ref _prepareCount);
  public IHostInputPreparer? Create(HostInputFactoryContext context)=>new Gate(_actual.Create(context)!,this);
  public void Dispose()=>_actual.Dispose();
  private sealed class Gate(IHostInputPreparer actual,GatedFactory owner):IHostInputPreparer
  {
   public string CapabilityId=>actual.CapabilityId;public int CapabilityVersion=>actual.CapabilityVersion;public IReadOnlyList<string> Services=>actual.Services;
   public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)
   {Interlocked.Increment(ref owner._prepareCount);owner.Entered.TrySetResult();await owner.Release.Task;context.Cancellation.ThrowIfCancellationRequested();return await actual.PrepareAsync(context);}
   public void Dispose()=>actual.Dispose();
  }
 }
}

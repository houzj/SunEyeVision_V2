using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Commands;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Tool.SendData;
using SunEyeVision.Tool.SendData.Views;
using SunEyeVision.Workflow;
using Xunit;
using Xunit.Abstractions;

namespace SunEyeVision.UI.Tests;

[CollectionDefinition("Editor commit UI", DisableParallelization = true)]
public sealed class EditorCommitUiCollection { }

[Collection("Editor commit UI")]
public sealed class ToolEditorCommitTests(ITestOutputHelper output)
{
    private sealed class Sender : ICommunicationSendService
    {
        public CommunicationSendRequest? Request;
        public IReadOnlyList<CommunicationSendTarget> GetTargets() => new[] { new CommunicationSendTarget("Device", "point", "device", "test2", "OpcUa", "Double", DeviceName: "OPC UA") };
        public Task<PreparedCommunicationSend> PrepareAsync(CommunicationSendRequest request, CancellationToken token = default)
        { Request = request; return Task.FromResult(new PreparedCommunicationSend("test2", request.Inputs[0].Value, _ => Task.FromResult(1))); }
        public void ConfigureVariablePublications(string id, IReadOnlyList<SendVariablePublication> mappings) { }
    }
    [Fact]
    public void Real_send_grid_selections_are_submitted_before_command_snapshot_without_closing()
    {
        Sta(() =>
        {
            var p = new SendDataParameters { DeviceId = "device" };
            var flow = new SunEyeVision.Workflow.Workflow("flow", "flow"); var service = new Sender();
            var editor = new SendDataEditor(); editor.SetParameters(p);
            editor.SetDataProvider(new DataSourceQueryService(flow, flow) { CommunicationSendService = service });
            editor.SetCurrentNode(new WorkflowNodeBase("node", "发送", "发送", "SendData") { Parameters = p });
            var window = new Window { Content = editor, Width = 540, Height = 620, ShowActivated = false, Left = -10000, Top = -10000 };
            try
            {
                window.Show(); editor.UpdateLayout();
                var grid = (DataGrid)editor.FindName("RowsGrid"); grid.SelectedIndex = 0;
                grid.CurrentCell = new(grid.Items[0], grid.Columns[1]); grid.BeginEdit(); editor.UpdateLayout();
                var target = Children<ComboBox>(grid).Single(c => c.SelectedValuePath == "Id"); target.SelectedValue = "point";
                var type = Children<ComboBox>(grid).Single(c => c.Items.Contains("Double")); type.SelectedItem = "Double";
                p.Rows[0].InputText = "1111";
                output.WriteLine($"Before execute: Target={p.Rows[0].TargetId}, Type={p.Rows[0].DataType}");
                Assert.Equal("", p.Rows[0].TargetId); Assert.Equal("String", p.Rows[0].DataType);
                ToolResults? result = null; editor.ToolExecutionCompleted += (_, e) => result = e.Results as ToolResults;
                ToolCommands.Execute.Execute(null, editor);
                var until = DateTime.UtcNow.AddSeconds(10);
                while (result == null && DateTime.UtcNow < until) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Assert.NotNull(result); Assert.True(result.IsSuccess, result.ErrorMessage);
                Assert.Equal("point", service.Request!.Inputs[0].TargetId); Assert.Equal("Double", service.Request.Inputs[0].DataType);
                Assert.Equal(1111d, service.Request.Inputs[0].Value);
                var frozen = (SendDataParameters)ToolParameterSnapshot.Create(p); p.Rows[0].DataType = "String";
                Assert.Equal("Double", frozen.Rows[0].DataType); Assert.True(window.IsVisible);
            }
            finally { window.Close(); }
        });
    }
    private sealed class Value { public int Number { get; set; } = 1; }
    private sealed class Editor : ToolDebugControlBase { protected override object ExecuteTool() => new GenericToolResults(); }
    [Fact]
    public void Document_prepare_leaves_clean_legacy_open_but_rejects_pending_binding_before_source_write() => Sta(() =>
    {
        var parameters = new SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters { Separator = "," };
        var node = new WorkflowNodeBase("legacy", "legacy", "legacy", "ProtocolParse") { Parameters = parameters };
        var text = new TextBox();
        text.SetBinding(TextBox.TextProperty, new Binding(nameof(parameters.Separator))
        { Source = parameters, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit });
        var editor = new Editor { Content = text }; editor.SetCurrentNode(node); editor.SetParameters(parameters);
        var window = new Window { Content = editor, ShowActivated = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show(); editor.UpdateLayout();
            Assert.True(ToolEditorCommit.TryPrepareOpenDraftEditors(n => ReferenceEquals(n, node), out var clean, out var error), error);
            Assert.Empty(clean); Assert.Equal(",", parameters.Separator);
            text.Text = ";";
            Assert.False(ToolEditorCommit.TryPrepareOpenDraftEditors(n => ReferenceEquals(n, node), out _, out error));
            Assert.Contains("尚未接入", error); Assert.Equal(",", parameters.Separator); Assert.Equal(";", text.Text);
        }
        finally { editor.SuppressClosePersistence = true; window.Close(); }
    });
    [Fact]
    public void Document_prepare_detects_legacy_live_edit_even_after_lost_focus_clears_binding_dirty() => Sta(() =>
    {
        var parameters = new SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters { Separator = "," };
        var node = new WorkflowNodeBase("legacy", "legacy", "legacy", "ProtocolParse") { Parameters = parameters };
        var editor = new Editor(); editor.SetCurrentNode(node); editor.SetParameters(parameters);
        var window = new Window { Content = editor, ShowActivated = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show(); editor.UpdateLayout(); parameters.Separator = ";";
            Assert.False(ToolEditorCommit.TryPrepareOpenDraftEditors(n => ReferenceEquals(n, node), out _, out var error));
            Assert.Contains("尚未接入", error); Assert.Equal(";", parameters.Separator);
        }
        finally { editor.SuppressClosePersistence = true; window.Close(); }
    });
    [Fact]
    public void Confirming_point_coordinate_constants_clears_orphan_rebinding_markers_in_real_editor()
    {
        Sta(() =>
        {
            var p = new SunEyeVision.Tool.PointPointMeasure.PointPointMeasureParameters {
                P1InputMode = SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models.PointInputMode.ByCoordinate,
                P2InputMode = SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models.PointInputMode.ByCoordinate };
            foreach (var key in new[] { "P1X", "P1Y", "P2X", "P2Y" }) p.RebindingRequired[key] = "旧绑定已失效";
            var node = new WorkflowNodeBase("point", "point", "point", "PointPointMeasure") { Parameters = p };
            var flow = new SunEyeVision.Workflow.Workflow("f", "f"); flow.Nodes.Add(node);
            var solution = new Solution(); solution.Workflows.Add(flow);
            var service = new OutputBindingChangeService(); service.Reset(solution);
            var query = new DataSourceQueryService(flow, flow) { CommitOutputConfiguration = id => service.Commit("f", id) };
            var editor = new SunEyeVision.Tool.PointPointMeasure.Views.PointPointMeasureToolDebugControl();
            editor.SetParameters(p); editor.SetCurrentNode(node); editor.SetDataProvider(query);
            var window = new Window { Content = editor, ShowActivated = false, Left = -10000 };
            try
            {
                window.Show(); editor.UpdateLayout();
                output.WriteLine("coordinate controls: " + string.Join(",", Children<ConfigSetting>(editor).Select(c => c.ParameterName + ":" + c.BindingType)));
                Assert.True(ToolEditorCommit.TryCommit(editor, out var error), error);
                output.WriteLine("submitted settings: " + string.Join(",", node.ParamSettings.Settings.Select(s => s.ParameterName + ":" + s.BindingType)));
                Assert.Empty(p.RebindingRequired);
                Assert.All(node.ParamSettings.Settings.Where(s => new[] { "P1X", "P1Y", "P2X", "P2Y" }.Contains(s.ParameterName)), s => Assert.Equal(BindingType.Constant, s.BindingType));
                Assert.Equal(4, node.ParamSettings.Settings.Count(s => new[] { "P1X", "P1Y", "P2X", "P2Y" }.Contains(s.ParameterName)));
                OutputBindingChangeService.EnsureExecutable(flow);
                var request = new SunEyeVision.Plugin.SDK.Core.ToolExecutionRequest(ToolParameterSnapshot.Create(p)) {
                    ParameterBindings = new ParameterBindingSet(node.ParamSettings.Settings.Select(ParameterBindingDefinition.FromSetting)) };
                Assert.True(new SunEyeVision.Plugin.SDK.Core.ToolExecutor(new ParameterResolver()).ResolveRuntimeParameters(request).IsValid);
            }
            finally { window.Close(); }
        });
    }
    [Fact]
    public void Image_selector_writes_model_and_refresh_preserves_reference_and_two_way_binding()
    {
        Sta(() =>
        {
            var p = new MeasureGuideParameters();
            var source = new AvailableDataSource { SourceNodeId = "source", PropertyName = "OutputImage",
                PropertyType = typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage), VariableType = "Image" };
            var selector = new ImageSourceSelector { AvailableDataSources = new() { source } };
            selector.SetBinding(ImageSourceSelector.SelectedDataSourceProperty, new Binding(nameof(p.ImageSource)) {
                Source = p, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            selector.SelectedDataSource = source.WithConnectionOwnership(null);
            Assert.True(BindingOperations.IsDataBound(selector, ImageSourceSelector.SelectedDataSourceProperty));
            Assert.NotNull(p.ImageSourceRef); Assert.Equal("source", p.ImageSourceRef.SourceNodeId);
            var resources = new ResourceDictionary { Source = new Uri("/SunEyeVision.Plugin.SDK;component/Themes/Generic.xaml", UriKind.Relative) };
            selector.Style = (Style)resources[typeof(ImageSourceSelector)];
            var window = new Window { Content = selector, ShowActivated = false, Left = -10000 };
            try
            {
                window.Show(); selector.ApplyTemplate(); selector.UpdateLayout();
                selector.AvailableDataSources = new() { new AvailableDataSource { SourceNodeId = "source",
                    PropertyName = "OutputImage", PropertyType = source.PropertyType, VariableType = "Image" } };
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.True(BindingOperations.IsDataBound(selector, ImageSourceSelector.SelectedDataSourceProperty));
                Assert.NotNull(p.ImageSource); Assert.NotNull(p.ImageSourceRef);
                var frozen = (MeasureGuideParameters)ToolParameterSnapshot.Create(p);
                Assert.Equal("source", frozen.ImageSourceRef!.SourceNodeId);
                Assert.Equal("OutputImage", frozen.ImageSourceRef.PropertyName);
                var other = new AvailableDataSource { SourceNodeId = "other", PropertyName = "OutputImage",
                    PropertyType = source.PropertyType, VariableType = "Image" };
                selector.AvailableDataSources.Add(other);
                var popup = (System.Windows.Controls.Primitives.Popup)selector.Template.FindName("PART_Popup", selector);
                var list = (ListBox)selector.Template.FindName("PART_ImageSourceList", selector);
                popup.IsOpen = true; list.SelectedItem = other;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Equal("other", p.ImageSourceRef!.SourceNodeId);
                Assert.True(BindingOperations.IsDataBound(selector, ImageSourceSelector.SelectedDataSourceProperty));
            }
            finally { window.Close(); }
        });
    }
    [Fact]
    public void Image_selector_refresh_never_selects_first_or_replaces_missing_source_and_preserves_ownership()
    {
        Sta(() =>
        {
            var source = new AvailableDataSource { SourceNodeId = "source", PropertyName = "image", PropertyType = typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage) };
            var other = new AvailableDataSource { SourceNodeId = "other", PropertyName = "image", PropertyType = source.PropertyType };
            var selector = new ImageSourceSelector { AvailableDataSources = new() { source, other } };
            Assert.Null(selector.SelectedDataSource);
            selector.SelectedDataSource = source.WithConnectionOwnership("edge");
            selector.AvailableDataSources = new() { source, other }; Assert.Equal("edge", selector.SelectedDataSource.AutomaticConnectionId);
            selector.SelectedDataSource = source.WithConnectionOwnership(null); Assert.Null(selector.SelectedDataSource.AutomaticConnectionId);
            selector.AvailableDataSources = new() { other }; Assert.Equal("source", selector.SelectedDataSource.SourceNodeId);
            selector.SelectedDataSource = null!; selector.AvailableDataSources = new() { other }; Assert.Null(selector.SelectedDataSource);
        });
    }
    [Fact]
    public void Cleared_named_binding_is_not_reinserted_by_live_editor_persistence()
    {
        Sta(() =>
        {
            var control = new ConfigSetting { ParameterName = "InputText" };
            var editor = new Editor { Content = control }; var node = new WorkflowNodeBase("consumer", "consumer", "consumer", "Test");
            node.ParamSettings.SetSetting(ParamSetting.CreateBinding("InputText", "source", "field"));
            editor.SetCurrentNode(node);
            var window = new Window { Content = editor, ShowActivated = false, Left = -10000 };
            try
            {
                window.Show(); editor.UpdateLayout(); editor.RefreshCommittedBindings(); Assert.Equal("source.field", control.BindingSource);
                node.ParamSettings.RemoveSetting("InputText"); node.Parameters.RebindingRequired["InputText"] = "上游输出变化，需重新绑定";
                ToolEditorCommit.RefreshCommittedBindings(); editor.PersistParameterBindings();
                Assert.Empty(control.BindingSource); Assert.Empty(control.BindingDisplayText); Assert.Empty(node.ParamSettings.GetDynamicSettings());
                Assert.Single(node.Parameters.RebindingRequired);
            }
            finally { window.Close(); }
        });
    }
    [Fact]
    public void Open_editor_scope_is_respected_and_invalid_editor_prevents_other_pending_publications()
    {
        Sta(() =>
        {
            var a = new Value(); var b = new Value(); var ta = new TextBox(); var tb = new TextBox();
            ta.SetBinding(TextBox.TextProperty, new Binding(nameof(Value.Number)) { Source = a, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit });
            tb.SetBinding(TextBox.TextProperty, new Binding(nameof(Value.Number)) { Source = b, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit });
            var ea = new Editor { Content = ta }; var eb = new Editor { Content = tb };
            ea.SetCurrentNode(new WorkflowNodeBase("a", "a", "a", "SendData")); eb.SetCurrentNode(new WorkflowNodeBase("b", "b", "b", "SendData"));
            var wa = new Window { Content = ea, ShowActivated = false, Left = -10000 }; var wb = new Window { Content = eb, ShowActivated = false, Left = -10000 };
            try
            {
                wa.Show(); wb.Show(); ta.Text = "7"; tb.Text = "8";
                Assert.True(ToolEditorCommit.TryCommitOpenEditors(id => id == "a", out _)); Assert.Equal(7, a.Number); Assert.Equal(1, b.Number);
                ta.Text = "9"; tb.Text = "invalid";
                Assert.False(ToolEditorCommit.TryCommitOpenEditors(null, out _)); Assert.Equal(7, a.Number); Assert.Equal(1, b.Number);
                Assert.Equal("invalid", tb.Text);
                tb.Text = "10"; Assert.True(ToolEditorCommit.TryCommitOpenEditors(null, out _)); Assert.Equal(9, a.Number); Assert.Equal(10, b.Number);
            }
            finally { wa.Close(); wb.Close(); }
            ta.Text = "11"; Assert.True(ToolEditorCommit.TryCommitOpenEditors(null, out _)); Assert.Equal(9, a.Number);
        });
    }
    [Fact]
    public void Pending_text_is_committed_via_binding_but_invalid_text_is_retained()
    {
        Sta(() =>
        {
            var value = new Value(); var text = new TextBox();
            text.SetBinding(TextBox.TextProperty, new Binding(nameof(Value.Number)) { Source = value, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit });
            text.Text = "7"; Assert.Equal(1, value.Number);
            Assert.True(ToolEditorCommit.TryCommit(text, out _)); Assert.Equal(7, value.Number);
            text.Text = "invalid"; Assert.False(ToolEditorCommit.TryCommit(text, out _));
            Assert.Equal("invalid", text.Text); Assert.Equal(7, value.Number);
            text.Text = "8"; Assert.True(ToolEditorCommit.TryCommit(text, out _)); Assert.Equal(8, value.Number);
        });
    }
    private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T t) yield return t; foreach (var item in Children<T>(child)) yield return item; }
    }
    [Fact]
    public void Pending_binding_label_never_replaces_real_reference_or_exposes_guid()
    {
        Sta(() =>
        {
            var expression = Guid.NewGuid() + ".Field_unknown";
            var binding = new ParamBinding { SyncEditingToSource = true, BindingSource = expression };
            binding.SetPendingValidation("上游契约暂不可用");
            Assert.Equal(expression, binding.BindingSource); Assert.Equal("已绑定（待校验）", binding.BindingDisplayText);
            binding.RefreshBindingDisplay(); Assert.Equal(expression, binding.BindingSource); Assert.DoesNotContain("Field_unknown", binding.BindingDisplayText);
            binding.SetPendingValidation(null); Assert.Equal(expression, binding.BindingSource);
        });
    }
    private static void Sta(Action action)
    {
        Exception? failure = null; var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

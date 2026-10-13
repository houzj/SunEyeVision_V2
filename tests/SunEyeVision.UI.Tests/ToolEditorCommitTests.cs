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
    private static void Sta(Action action)
    {
        Exception? failure = null; var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

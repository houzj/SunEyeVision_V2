using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class BindingUiRegressionTests
{
    [Fact]
    public void Position_source_null_clear_and_existing_indexed_path_pass_actual_wpf_commit()
    {
        Sta(() =>
        {
            var model = new MeasureGuideParameters(); var control = new ParamBinding();
            control.SetBinding(ParamBinding.BindingSourceProperty, new Binding(nameof(model.PositionCorrectionBindingSource))
            { Source = model, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit });
            control.SetCurrentValue(ParamBinding.BindingSourceProperty, "");
            Assert.True(ToolEditorCommit.TryCommit(control, out _)); Assert.Null(model.PositionCorrectionBindingSource);
            model.PositionCorrectionBindingSource = new AvailableDataSource { SourceNodeId = "align", PropertyName = "Matrix[0]" };
            Assert.Equal("align.Matrix[0]", control.BindingSource);
            Assert.True(ToolEditorCommit.TryCommit(control, out _)); Assert.Equal("Matrix[0]", model.PositionCorrectionSource!.PropertyName);
            control.BindingSource = "other.Matrix[1]";
            Assert.True(BindingOperations.IsDataBound(control, ParamBinding.BindingSourceProperty));
            Assert.True(ToolEditorCommit.TryCommit(control, out _)); Assert.Equal("other", model.PositionCorrectionBindingSource!.SourceNodeId);
            Assert.Equal("Matrix[1]", model.PositionCorrectionSource!.PropertyName);
            control.BindingSource = "";
            Assert.True(ToolEditorCommit.TryCommit(control, out _)); Assert.Null(model.PositionCorrectionSource);
            Assert.False(System.Windows.Controls.Validation.GetHasError(control));
        });
    }

    [Fact]
    public void Source_converter_accepts_empty_and_null_but_still_rejects_invalid_nonempty_paths()
    {
        var converter = new AvailableDataSourceConverter();
        Assert.Null(converter.ConvertFromString("")); Assert.Null(converter.ConvertFromString("  "));
        Assert.Equal("", converter.ConvertToString(null));
        Assert.Throws<NotSupportedException>(() => converter.ConvertFromString("not_a_path"));
    }

    private sealed class NumberModel { public int Number { get; set; } = 7; }
    private sealed class Editor : ToolDebugControlBase { protected override object ExecuteTool() => new object(); }

    [Fact]
    public void Invalid_input_blocks_commit_but_close_allows_explicit_discard_or_continue_editing()
    {
        Sta(() =>
        {
            var model = new NumberModel(); var text = new TextBox();
            text.SetBinding(TextBox.TextProperty, new Binding(nameof(model.Number))
            { Source = model, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit });
            var editor = new Editor { Content = text }; var window = new Window { Content = editor, ShowActivated = false, Left = -10000, Top = -10000 };
            window.Show();
            try
            {
                text.Text = "invalid"; Assert.False(ToolEditorCommit.TryCommit(editor, out _));
                Assert.False(ToolEditorClose.TryClose(editor, window, _ => false)); Assert.Equal("invalid", text.Text);
                Assert.True(ToolEditorClose.TryClose(editor, window, prompt =>
                { Assert.Contains("已实时写入", prompt); return true; }));
                Assert.Equal(7, model.Number); Assert.Equal("7", text.Text);
                Assert.True((bool)typeof(ToolDebugControlBase).GetProperty("SuppressClosePersistence", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(editor)!);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Valid_close_commits_and_does_not_prompt()
    {
        Sta(() =>
        {
            var text = new TextBox(); var model = new NumberModel();
            text.SetBinding(TextBox.TextProperty, new Binding(nameof(model.Number)) { Source = model, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit });
            text.Text = "8";
            Assert.True(ToolEditorClose.TryClose(text, new Window(), _ => throw new Exception("Unexpected prompt")));
            Assert.Equal(8, model.Number);
        });
    }

    [Fact]
    public void Ordinary_candidates_remain_upstream_and_opt_in_candidates_are_filtered_and_sectioned()
    {
        var flow = new SunEyeVision.Workflow.Workflow("f", "f");
        foreach (var id in new[] { "a", "b", "numeric", "target", "child" })
            flow.Nodes.Add(new(id, id, id, "Probe") { Parameters = new BindingAdaptationB2Tests.SourceParameters { TypeName = id == "numeric" ? "Double" : "String" } });
        flow.Connections.Add(new() { SourceNodeId = "a", TargetNodeId = "target" });
        flow.Connections.Add(new() { SourceNodeId = "target", TargetNodeId = "child" });
        var service = new OutputBindingChangeService(); service.Reset(new Solution { Workflows = new() { flow } });
        var query = new DataSourceQueryService(flow, flow) { WorkflowId = "f", BindingCandidateNodeIds = id => service.GetCandidateNodeIds("f", id),
            FormalOutputContractReader = id => service.ReadContract(flow.Nodes.Single(n => n.Id == id)) };
        Assert.Equal("a", Assert.Single(query.GetAvailableDataSources("target", typeof(string))).SourceNodeId);
        var expanded = query.GetAvailableDataSources("target", typeof(string), true);
        Assert.Equal(new[] { "a", "b" }, expanded.Select(s => s.SourceNodeId));
        var roots = ParamBindingBase.BuildTreeStructure(expanded, typeof(string));
        Assert.Equal(new[] { "上游节点", "其他可绑定节点" }, roots.Select(n => n.Text));
        Assert.Equal("b", Assert.Single(roots[1].Children).Text);
        Sta(() =>
        {
            var control = new ParamBinding { TreeNodes = new(roots) };
            var label = control.GenerateBindingDisplayText(expanded[1].GetBindingPath());
            Assert.StartsWith("b.", label); Assert.DoesNotContain("其他可绑定节点", label);
        });
        Assert.DoesNotContain(expanded, s => s.SourceNodeId is "numeric" or "target" or "child");
    }

    [Fact]
    public void Narrowing_candidates_keeps_existing_legal_unconnected_bindings_visible_without_adding_all_nodes()
    {
        var flow = new SunEyeVision.Workflow.Workflow("f", "f");
        flow.Nodes.Add(new("bound", "bound", "bound", "Probe") { Parameters = new BindingAdaptationB2Tests.SourceParameters() });
        flow.Nodes.Add(new("unused", "unused", "unused", "Probe") { Parameters = new BindingAdaptationB2Tests.SourceParameters() });
        flow.Nodes.Add(new("target", "target", "target", "Probe") { Parameters = new SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters { InputText = "bound.value" } });
        var service = new OutputBindingChangeService(); service.Reset(new Solution { Workflows = new() { flow } });
        var query = new DataSourceQueryService(flow, flow) { WorkflowId = "f", BindingCandidateNodeIds = id => service.GetCandidateNodeIds("f", id),
            ExistingBindingSourceNodeIds = id => service.GetExistingBindingSourceNodeIds("f", id),
            FormalOutputContractReader = id => service.ReadContract(flow.Nodes.Single(n => n.Id == id)) };
        var source = Assert.Single(query.GetAvailableDataSources("target", typeof(string)));
        Assert.Equal("bound", source.SourceNodeId); Assert.Equal("当前绑定来源（无连线）", source.CandidateSection);
        Assert.Equal("bound.value", ((SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters)flow.Nodes[2].Parameters).InputText);
    }

    private static void Sta(Action action)
    {
        Exception? failure = null; var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

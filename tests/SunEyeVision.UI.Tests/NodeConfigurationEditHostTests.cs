using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class NodeConfigurationEditHostTests
{
    private static Solution Model()
    {
        var flow = new SunEyeVision.Workflow.Workflow("f", "f");
        flow.Nodes.Add(new("source", "source", "source", "Probe") { Parameters = new BindingAdaptationB2Tests.SourceParameters() });
        flow.Nodes.Add(new("parse", "parse", "parse", "ProtocolParse") { Parameters = new ProtocolParseParameters { InputText = "source.value" } });
        return new() { Workflows = new() { flow } };
    }

    [Fact]
    public void Host_commits_source_and_detachment_preserving_live_node_identity_and_runtime_values()
    {
        var model = Model(); var source = model.Workflows[0].Nodes[0]; var parse = model.Workflows[0].Nodes[1];
        var variable = new GlobalVariable("v", "initial", "String") { ProducerBinding = new() { WorkflowId = "f", NodeId = "source", ResultName = "value" } };
        model.GlobalVariables.Add(variable); var host = new NodeConfigurationEditHost(model); var edit = host.BeginEdit("f", "source");
        ((BindingAdaptationB2Tests.SourceParameters)edit.Node.Parameters).TypeName = "Double"; variable.Value = "runtime";
        Assert.Equal("String", ((BindingAdaptationB2Tests.SourceParameters)source.Parameters).TypeName);
        host.Commit(edit);
        Assert.Same(source, model.Workflows[0].Nodes[0]); Assert.Same(parse, model.Workflows[0].Nodes[1]);
        Assert.Empty(((ProtocolParseParameters)parse.Parameters).InputText); Assert.Equal("runtime", variable.Value);
        Assert.Null(variable.ProducerBinding); Assert.NotEmpty(variable.RebindingReason);
        host.Undo(); Assert.Equal("source.value", ((ProtocolParseParameters)parse.Parameters).InputText);
        Assert.NotNull(variable.ProducerBinding); Assert.Equal("runtime", variable.Value);
        host.Redo(); Assert.Empty(((ProtocolParseParameters)parse.Parameters).InputText);
    }

    [Fact]
    public void Changed_node_cannot_be_overwritten_by_an_old_editor_but_its_draft_is_preserved()
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var a = host.BeginEdit("f", "source"); var b = host.BeginEdit("f", "source");
        ((BindingAdaptationB2Tests.SourceParameters)a.Node.Parameters).Name = "a"; host.Commit(a);
        ((BindingAdaptationB2Tests.SourceParameters)b.Node.Parameters).Name = "b";
        Assert.Throws<InvalidOperationException>(() => host.Commit(b));
        Assert.Equal("b", ((BindingAdaptationB2Tests.SourceParameters)b.Node.Parameters).Name);
        Assert.Equal("a", ((BindingAdaptationB2Tests.SourceParameters)model.Workflows[0].Nodes[0].Parameters).Name);
    }

    [Fact]
    public void External_design_changes_are_rejected_instead_of_silently_overwritten()
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var edit = host.BeginEdit("f", "source");
        ((ProtocolParseParameters)model.Workflows[0].Nodes[1].Parameters).InputText = "manual";
        Assert.Throws<InvalidOperationException>(() => host.Commit(edit));
        Assert.Equal("manual", ((ProtocolParseParameters)model.Workflows[0].Nodes[1].Parameters).InputText);
        Assert.False(host.CanUndo);
    }

    [Fact]
    public void Unchanged_editor_commit_is_not_an_undo_step_and_consumed_drafts_cannot_replay()
    {
        var host = new NodeConfigurationEditHost(Model()); var edit = host.BeginEdit("f", "source"); var before = host.Capture();
        Assert.Same(before, host.Commit(edit).Snapshot); Assert.False(host.CanUndo);
        Assert.Throws<InvalidOperationException>(() => host.Commit(edit));
    }

    [Fact]
    public void Self_binding_failure_keeps_formal_model_history_and_user_draft_intact()
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var before = host.Capture(); var edit = host.BeginEdit("f", "source");
        edit.Node.ParamSettings.SetSetting(SunEyeVision.Plugin.SDK.Execution.Parameters.ParamSetting.CreateBinding("Name", "source", "value"));
        Assert.Throws<InvalidOperationException>(() => host.Commit(edit));
        Assert.Same(before, host.Capture()); Assert.False(host.CanUndo);
        Assert.Empty(model.Workflows[0].Nodes[0].ParamSettings.Settings); Assert.Single(edit.Node.ParamSettings.Settings);
        Assert.Equal("source.value", ((ProtocolParseParameters)model.Workflows[0].Nodes[1].Parameters).InputText);
    }

    [Fact]
    public void Unrelated_open_editor_can_commit_after_another_node_changes_without_overwriting_it()
    {
        var model = Model(); model.Workflows[0].Nodes.Add(new("other", "other", "other", "Probe")
        { Parameters = new BindingAdaptationB2Tests.SourceParameters() });
        var host = new NodeConfigurationEditHost(model); var a = host.BeginEdit("f", "source"); var b = host.BeginEdit("f", "other");
        ((BindingAdaptationB2Tests.SourceParameters)a.Node.Parameters).Name = "a"; host.Commit(a);
        ((BindingAdaptationB2Tests.SourceParameters)b.Node.Parameters).Name = "b"; host.Commit(b);
        Assert.Equal("a", ((BindingAdaptationB2Tests.SourceParameters)model.Workflows[0].Nodes[0].Parameters).Name);
        Assert.Equal("b", ((BindingAdaptationB2Tests.SourceParameters)model.Workflows[0].Nodes[2].Parameters).Name);
    }

    [Fact]
    public void Throwing_binding_notifications_restore_all_fields_and_allow_the_same_draft_to_retry()
    {
        var model = Model(); var variable = new GlobalVariable("v", "runtime", "String")
        { ProducerBinding = new() { WorkflowId = "f", NodeId = "source", ResultName = "value" } };
        model.GlobalVariables.Add(variable); var host = new NodeConfigurationEditHost(model); var before = host.Capture();
        var producer = variable.ProducerBinding; var consumers = variable.ConsumerBindings;
        var edit = host.BeginEdit("f", "source"); ((BindingAdaptationB2Tests.SourceParameters)edit.Node.Parameters).TypeName = "Double";
        System.ComponentModel.PropertyChangedEventHandler handler = (_, e) =>
        { if (e.PropertyName is nameof(GlobalVariable.ProducerBinding) or nameof(GlobalVariable.ConsumerBindings)) throw new InvalidOperationException("observer failure"); };
        variable.PropertyChanged += handler;
        Assert.Throws<AggregateException>(() => host.Commit(edit));
        Assert.Same(before, host.Capture()); Assert.False(host.CanUndo);
        Assert.Same(producer, variable.ProducerBinding); Assert.Same(consumers, variable.ConsumerBindings);
        Assert.Empty(variable.RebindingReason); Assert.Equal("runtime", variable.Value);
        Assert.Equal("String", ((BindingAdaptationB2Tests.SourceParameters)model.Workflows[0].Nodes[0].Parameters).TypeName);
        Assert.Equal("source.value", ((ProtocolParseParameters)model.Workflows[0].Nodes[1].Parameters).InputText);
        variable.PropertyChanged -= handler;
        host.Commit(edit); Assert.Null(variable.ProducerBinding); Assert.True(host.CanUndo);
    }
}

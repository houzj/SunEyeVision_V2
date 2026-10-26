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
    public void Preview_preferences_remain_independent_of_parameter_commit_undo_and_redo()
    {
        var model = Model(); var source = model.Workflows[0].Nodes[0]; var parse = model.Workflows[0].Nodes[1];
        var host = new NodeConfigurationEditHost(model); var edit = host.BeginEdit("f", "source");
        var sourcePreview = new SunEyeVision.Plugin.SDK.Execution.Parameters.ImageSourceReference("source", "OutputImage", "source", "图像");
        var parsePreview = new SunEyeVision.Plugin.SDK.Execution.Parameters.ImageSourceReference("source", "OutputImage", "source", "图像");
        source.SelectedDisplaySource = sourcePreview; parse.SelectedDisplaySource = parsePreview;
        ((BindingAdaptationB2Tests.SourceParameters)edit.Node.Parameters).Name = "edited";
        host.Validate(edit); host.Commit(edit);
        Assert.Equal("edited", ((BindingAdaptationB2Tests.SourceParameters)source.Parameters).Name);
        Assert.Same(sourcePreview, source.SelectedDisplaySource); Assert.Same(parsePreview, parse.SelectedDisplaySource);
        // Renaming the source output legitimately updates its stable-ID downstream binding.
        Assert.Equal("source.edited", ((ProtocolParseParameters)parse.Parameters).InputText);
        host.Undo(); Assert.Same(sourcePreview, source.SelectedDisplaySource); Assert.Same(parsePreview, parse.SelectedDisplaySource);
        host.Redo(); Assert.Same(sourcePreview, source.SelectedDisplaySource); Assert.Same(parsePreview, parse.SelectedDisplaySource);
        Assert.Equal("edited", ((BindingAdaptationB2Tests.SourceParameters)source.Parameters).Name);
    }

    [Theory]
    [InlineData("binding")]
    [InlineData("global-binding")]
    public void Independent_preview_does_not_adopt_real_external_configuration_changes(string change)
    {
        var model = Model(); model.GlobalVariables.Add(new GlobalVariable("v", "initial", "String"));
        var host = new NodeConfigurationEditHost(model); var before = host.Capture(); var edit = host.BeginEdit("f", "source");
        model.Workflows[0].Nodes[0].SelectedDisplaySource = new("source", "OutputImage", "source", "图像");
        switch (change)
        {
            case "binding": model.Workflows[0].Nodes[1].ParamSettings.SetSetting(SunEyeVision.Plugin.SDK.Execution.Parameters.ParamSetting.CreateBinding("InputText", "source", "value")); break;
            default: model.GlobalVariables[0].ConsumerBindings.Add(new() { WorkflowId = "f", NodeId = "parse", ParameterName = "InputText" }); break;
        }
        Assert.Throws<InvalidOperationException>(() => host.Validate(edit));
        Assert.Throws<InvalidOperationException>(() => host.Commit(edit));
        Assert.Same(before, host.Capture()); Assert.False(host.CanUndo);
    }

    [Theory]
    [InlineData("topology")]
    [InlineData("image-input")]
    [InlineData("enabled")]
    public void Explicit_canvas_structure_is_synchronized_but_changed_node_rejects_its_old_parameter_draft(string change)
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model);
        var before = host.Capture(); var draft = host.BeginEdit("f", "source");
        ((BindingAdaptationB2Tests.SourceParameters)draft.Node.Parameters).Name = "candidate";
        var preview = new SunEyeVision.Plugin.SDK.Execution.Parameters.ImageSourceReference("source", "OutputImage", "source", "图像");
        model.Workflows[0].Nodes[0].SelectedDisplaySource = preview;
        if (change == "topology") model.Workflows[0].Connections.Add(new("edge", "source", "parse"));
        if (change == "image-input") model.Workflows[0].Nodes[0].ImageSourceConfig = new() { NodeId = "source" };
        if (change == "enabled") model.Workflows[0].Nodes[0].IsEnabled = false;
        host.SynchronizeStructure();
        Assert.True(host.Capture().Version > before.Version);
        if (change == "topology")
        {
            host.Validate(draft); host.Commit(draft);
            Assert.Single(model.Workflows[0].Connections);
            Assert.Equal("candidate", ((BindingAdaptationB2Tests.SourceParameters)model.Workflows[0].Nodes[0].Parameters).Name);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => host.Validate(draft));
            Assert.Throws<InvalidOperationException>(() => host.Commit(draft));
            Assert.Equal("value", ((BindingAdaptationB2Tests.SourceParameters)model.Workflows[0].Nodes[0].Parameters).Name);
            Assert.Equal("candidate", ((BindingAdaptationB2Tests.SourceParameters)draft.Node.Parameters).Name);
            if (change == "enabled") Assert.False(model.Workflows[0].Nodes[0].IsEnabled);
            else Assert.Equal("source", model.Workflows[0].Nodes[0].ImageSourceConfig!.NodeId);
        }
        Assert.Same(preview, model.Workflows[0].Nodes[0].SelectedDisplaySource);
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
    public void Throwing_binding_notifications_report_refresh_error_after_final_publication_without_replay()
    {
        var model = Model(); var variable = new GlobalVariable("v", "runtime", "String")
        { ProducerBinding = new() { WorkflowId = "f", NodeId = "source", ResultName = "value" } };
        model.GlobalVariables.Add(variable); var host = new NodeConfigurationEditHost(model); var before = host.Capture();
        var producer = variable.ProducerBinding; var consumers = variable.ConsumerBindings;
        var edit = host.BeginEdit("f", "source"); ((BindingAdaptationB2Tests.SourceParameters)edit.Node.Parameters).TypeName = "Double";
        System.ComponentModel.PropertyChangedEventHandler handler = (_, e) =>
        { if (e.PropertyName is nameof(GlobalVariable.ProducerBinding) or nameof(GlobalVariable.ConsumerBindings)) throw new InvalidOperationException("observer failure"); };
        variable.PropertyChanged += handler;
        host.Commit(edit);
        Assert.Equal(before.Version + 1, host.Capture().Version); Assert.True(host.CanUndo);
        Assert.NotNull(host.LastRefreshError);
        Assert.Null(variable.ProducerBinding); Assert.NotEmpty(variable.RebindingReason); Assert.Equal("runtime", variable.Value);
        Assert.Equal("Double", ((BindingAdaptationB2Tests.SourceParameters)model.Workflows[0].Nodes[0].Parameters).TypeName);
        Assert.Empty(((ProtocolParseParameters)model.Workflows[0].Nodes[1].Parameters).InputText);
        variable.PropertyChanged -= handler;
        Assert.Throws<InvalidOperationException>(() => host.Commit(edit));
        Assert.Equal(before.Version + 1, host.Capture().Version);
    }
}

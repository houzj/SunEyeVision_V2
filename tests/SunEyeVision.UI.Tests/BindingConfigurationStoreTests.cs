using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class BindingConfigurationStoreTests
{
    private static Solution Model()
    {
        var flow = new SunEyeVision.Workflow.Workflow("f", "流程");
        flow.Nodes.Add(new("source", "来源", "来源", "B3Probe") { Parameters = new BindingAdaptationB2Tests.SourceParameters() });
        flow.Nodes.Add(new("parse", "解析", "解析", "ProtocolParse") { Parameters = new ProtocolParseParameters { InputText = "source.value" } });
        return new Solution { Workflows = new() { flow } };
    }
    private static BindingAdaptationB2Tests.SourceParameters Source(Solution model) => (BindingAdaptationB2Tests.SourceParameters)model.Workflows[0].Nodes[0].Parameters;
    private static ProtocolParseParameters Input(Solution model) => (ProtocolParseParameters)model.Workflows[0].Nodes[1].Parameters;

    [Fact]
    public void Draft_edits_never_modify_original_or_published_configuration()
    {
        var original = Model(); var store = new BindingConfigurationStore(original); var draft = store.BeginEdit();
        Source(draft.Configuration).TypeName = "Double";
        Assert.Equal("String", Source(original).TypeName);
        Assert.Equal("String", Source(store.Capture().CopyConfiguration()).TypeName);
        Assert.Equal("source.value", Input(store.Capture().CopyConfiguration()).InputText);
    }

    [Fact]
    public void Source_change_and_consumer_detachment_publish_together_and_undo_together()
    {
        var store = new BindingConfigurationStore(Model()); var before = store.Capture(); var draft = store.BeginEdit();
        Source(draft.Configuration).TypeName = "Double";
        var publication = store.Publish(draft); var model = publication.Snapshot.CopyConfiguration();
        Assert.Equal("Double", Source(model).TypeName); Assert.Empty(Input(model).InputText); Assert.Single(Input(model).RebindingRequired);
        Assert.Empty(publication.Snapshot.Dependencies); Assert.NotEmpty(publication.Changes);
        var restored = store.Undo(publication.Snapshot.Version); model = restored.CopyConfiguration();
        Assert.Equal("String", Source(model).TypeName); Assert.Equal("source.value", Input(model).InputText);
        Assert.Empty(Input(model).RebindingRequired); Assert.Single(restored.Dependencies);
        Assert.True(restored.Version > publication.Snapshot.Version); Assert.False(store.IsCurrent(before.Version));
        var replay = store.Redo(restored.Version).CopyConfiguration();
        Assert.Empty(Input(replay).InputText); Assert.Single(Input(replay).RebindingRequired);
        Assert.Equal("source.value", Input(before.CopyConfiguration()).InputText);
    }

    [Fact]
    public void Cyclic_candidate_is_rejected_without_publishing_or_erasing_the_draft()
    {
        var store = new BindingConfigurationStore(Model()); var before = store.Capture(); var draft = store.BeginEdit();
        draft.Configuration.Workflows[0].Connections.Add(new() { Id = "reverse", SourceNodeId = "parse", TargetNodeId = "source" });
        Assert.Throws<InvalidOperationException>(() => store.Publish(draft));
        Assert.Same(before, store.Capture()); Assert.False(store.CanUndo); Assert.False(store.CanRedo);
        Assert.Single(draft.Configuration.Workflows[0].Connections); Assert.Equal("source.value", Input(draft.Configuration).InputText);
    }

    [Fact]
    public void Stale_and_foreign_drafts_cannot_overwrite_a_newer_generation()
    {
        var store = new BindingConfigurationStore(Model()); var stale = store.BeginEdit(); var fresh = store.BeginEdit();
        Source(fresh.Configuration).Name = "renamed"; store.Publish(fresh);
        Assert.Throws<InvalidOperationException>(() => store.Publish(stale));
        Assert.Throws<InvalidOperationException>(() => store.Publish(new BindingConfigurationStore(Model()).BeginEdit()));
        Assert.Equal("source.renamed", Input(store.Capture().CopyConfiguration()).InputText);
    }

    [Fact]
    public void Snapshot_and_successful_draft_remain_isolated_after_publication()
    {
        var store = new BindingConfigurationStore(Model()); var draft = store.BeginEdit(); var next = store.Publish(draft).Snapshot;
        Source(draft.Configuration).TypeName = "Double"; var external = next.CopyConfiguration(); Input(external).InputText = "changed";
        Assert.Equal("String", Source(next.CopyConfiguration()).TypeName); Assert.Equal("source.value", Input(next.CopyConfiguration()).InputText);
    }

    [Fact]
    public void Deleted_node_and_its_global_producer_consumer_state_restore_as_one_payload()
    {
        var model = Model(); var variable = new GlobalVariable("v", "initial", "String")
        { ProducerBinding = new() { WorkflowId = "f", NodeId = "source", ResultName = "value" } };
        model.GlobalVariables.Add(variable); var store = new BindingConfigurationStore(model); var draft = store.BeginEdit();
        draft.Configuration.Workflows[0].Nodes.RemoveAt(0); var next = store.Publish(draft).Snapshot;
        var changed = next.CopyConfiguration(); Assert.Null(changed.GlobalVariables[0].ProducerBinding);
        Assert.NotEmpty(changed.GlobalVariables[0].RebindingReason);
        var restored = store.Undo(next.Version).CopyConfiguration();
        Assert.Equal(2, restored.Workflows[0].Nodes.Count); Assert.Equal(variable.Id, restored.GlobalVariables[0].Id);
        Assert.NotNull(restored.GlobalVariables[0].ProducerBinding); Assert.Empty(restored.GlobalVariables[0].RebindingReason);
        Assert.Equal("source.value", Input(restored).InputText);
    }

    [Fact]
    public void New_edit_after_undo_discards_redo_and_old_restore_requests_are_rejected()
    {
        var store = new BindingConfigurationStore(Model()); var edit = store.BeginEdit(); Source(edit.Configuration).Name = "first";
        var next = store.Publish(edit).Snapshot;
        var restored = store.Undo(next.Version); Assert.True(store.CanRedo);
        Assert.Throws<InvalidOperationException>(() => store.Redo(next.Version));
        var newer = store.BeginEdit(); Source(newer.Configuration).Name = "second";
        store.Publish(newer); Assert.False(store.CanRedo); Assert.True(store.CanUndo);
    }

    [Fact]
    public void Global_initial_values_are_deep_copied_and_runtime_results_are_excluded()
    {
        var model = Model(); model.GlobalVariables.Add(new GlobalVariable("array", new[] { 1, 2 }, "IntegerArray"));
        model.Workflows[0].Nodes[0].LastResult = new object(); model.Workflows[0].Nodes[0].OutputCache = new object();
        var store = new BindingConfigurationStore(model); var draft = store.BeginEdit();
        ((int[])draft.Configuration.GlobalVariables[0].Value!)[0] = 99;
        Assert.Equal(1, ((int[])store.Capture().CopyConfiguration().GlobalVariables[0].Value!)[0]);
        Assert.Null(draft.Configuration.Workflows[0].Nodes[0].LastResult); Assert.Null(draft.Configuration.Workflows[0].Nodes[0].OutputCache);
    }

    [Fact]
    public void Parameter_only_change_invalidates_consumers_even_when_output_contract_is_unchanged()
    {
        var store = new BindingConfigurationStore(Model()); var draft = store.BeginEdit();
        draft.Configuration.Workflows[0].Nodes[0].IsEnabled = false;
        var publication = store.Publish(draft);
        Assert.Contains(new ScopedNodeIdentity("f", "source"), publication.InvalidatedNodes);
        Assert.Contains(new ScopedNodeIdentity("f", "parse"), publication.InvalidatedNodes);
        Assert.Equal("source.value", Input(publication.Snapshot.CopyConfiguration()).InputText);
    }

    [Fact]
    public void Global_initial_value_changes_invalidate_subscribers_without_a_producer()
    {
        var model = Model(); model.GlobalVariables.Add(new GlobalVariable("v", "old", "String"));
        Input(model).InputText = "__GlobalVars__.v";
        var store = new BindingConfigurationStore(model); var draft = store.BeginEdit();
        draft.Configuration.GlobalVariables[0].Value = "new";
        var publication = store.Publish(draft);
        Assert.Contains(new ScopedNodeIdentity("f", "parse"), publication.InvalidatedNodes);
        Assert.Equal("__GlobalVars__.v", Input(publication.Snapshot.CopyConfiguration()).InputText);
    }

    [Fact]
    public void Connection_removal_invalidates_results_without_clearing_valid_explicit_bindings()
    {
        var model = Model(); model.Workflows[0].Connections.Add(new() { Id = "edge", SourceNodeId = "source", TargetNodeId = "parse" });
        var store = new BindingConfigurationStore(model); var draft = store.BeginEdit(); draft.Configuration.Workflows[0].Connections.Clear();
        var publication = store.Publish(draft);
        Assert.Contains(new ScopedNodeIdentity("f", "parse"), publication.InvalidatedNodes);
        Assert.Equal("source.value", Input(publication.Snapshot.CopyConfiguration()).InputText);
        Assert.Single(publication.Snapshot.Dependencies);
    }

    [Fact]
    public void Connection_owned_image_reference_is_removed_from_a_serialized_draft_and_restored_by_undo()
    {
        var model = Model(); Source(model).TypeName = "Image";
        model.Workflows[0].Nodes[1].Parameters = new MeasureGuideParameters { ImageSource = new AvailableDataSource
        { SourceNodeId = "source", PropertyName = "value", VariableType = "Image", AutomaticConnectionId = "edge" } };
        model.Workflows[0].Connections.Add(new() { Id = "edge", SourceNodeId = "source", TargetNodeId = "parse" });
        var store = new BindingConfigurationStore(model); var draft = store.BeginEdit(); draft.Configuration.Workflows[0].Connections.Clear();
        var next = store.Publish(draft).Snapshot; var parameters = (MeasureGuideParameters)next.CopyConfiguration().Workflows[0].Nodes[1].Parameters;
        Assert.Null(parameters.ImageSourceRef); Assert.Contains("ImageSource", parameters.RebindingRequired.Keys);
        var restored = store.Undo(next.Version).CopyConfiguration();
        Assert.Single(restored.Workflows[0].Connections);
        Assert.Equal("edge", ((MeasureGuideParameters)restored.Workflows[0].Nodes[1].Parameters).ImageSourceRef!.AutomaticConnectionId);
    }

    [Fact]
    public async Task Concurrent_publications_accept_exactly_one_draft_at_the_same_base_version()
    {
        var store = new BindingConfigurationStore(Model()); var a = store.BeginEdit(); var b = store.BeginEdit();
        Source(a.Configuration).Name = "a"; Source(b.Configuration).Name = "b";
        var attempts = await Task.WhenAll(Task.Run(() => Attempt(a)), Task.Run(() => Attempt(b)));
        Assert.Equal(1, attempts.Count(x => x)); Assert.Equal(2, store.Capture().Version);
        bool Attempt(BindingConfigurationDraft draft)
        { try { store.Publish(draft); return true; } catch (InvalidOperationException) { return false; } }
    }

    [Fact]
    public void Unchanged_commit_does_not_create_history_or_invalidate_other_open_drafts()
    {
        var store = new BindingConfigurationStore(Model()); var before = store.Capture(); var other = store.BeginEdit();
        Assert.Same(before, store.Publish(store.BeginEdit()).Snapshot); Assert.False(store.CanUndo);
        Source(other.Configuration).Name = "changed"; Assert.Equal(2, store.Publish(other).Snapshot.Version);
    }
}

using System.Reflection;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class GlobalVariableConfigurationEditHostTests
{
    private static Solution Model()
    {
        var flow = new SunEyeVision.Workflow.Workflow("f", "f");
        flow.Nodes.Add(new("parse", "parse", "parse", "ProtocolParse")
        { Parameters = new ProtocolParseParameters { InputText = "hello" } });
        flow.Nodes.Add(new("other", "other", "other", "ProtocolParse")
        { Parameters = new ProtocolParseParameters { InputText = "world" } });
        return new Solution
        {
            Workflows = new() { flow },
            GlobalVariables = new()
            {
                new("Count", new long[] { 0 }, "Integer") { Id = "count" },
                new("Ratio", new double[] { 1.5 }, "Double") { Id = "ratio" },
                new("Label", new[] { "before" }, "String") { Id = "label" }
            }
        };
    }
    private static GlobalVariable Variable(Solution model, string id) => model.GlobalVariables.Single(v => v.Id == id);
    private static ProtocolParseParameters Parameters(Solution model, string id = "parse") =>
        (ProtocolParseParameters)model.Workflows[0].Nodes.Single(n => n.Id == id).Parameters;
    private static ProtocolParseParameters Parameters(NodeConfigurationEditDraft edit) => (ProtocolParseParameters)edit.Node.Parameters;
    private static void FailAfter(NodeConfigurationEditHost host, int count) => typeof(NodeConfigurationEditHost)
        .GetField("_acceptanceFailureAfterRawMutation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, count);

    [Fact]
    public void Node_and_all_variable_edits_publish_once_without_replacing_surviving_objects()
    {
        var model = Model(); var collection = model.GlobalVariables; var node = model.Workflows[0].Nodes[0];
        var count = Variable(model, "count"); var host = new NodeConfigurationEditHost(model);
        var before = host.Capture(); var edit = host.BeginEdit("f", "parse"); var variables = host.BeginVariableEdit();
        Parameters(edit).Separator = ";"; Variable(variables.Configuration, "count").Name = "CountSaved";
        variables.SetValue("count", new long[] { 19 }); variables.SetValue("ratio", new[] { 3.25 });
        variables.SetValue("label", new[] { "after-save" });
        host.ValidateBatch(new[] { edit }, variables);
        Assert.Same(before, host.Capture()); Assert.Equal("Count", count.Name); Assert.Equal(",", Parameters(model).Separator);
        var publication = host.CommitBatch(new[] { edit }, variables);
        Assert.Equal(before.Version + 1, publication.Snapshot.Version);
        Assert.Same(collection, model.GlobalVariables); Assert.Same(node, model.Workflows[0].Nodes[0]); Assert.Same(count, Variable(model, "count"));
        Assert.Equal(";", Parameters(model).Separator); Assert.Equal("CountSaved", count.Name);
        Assert.Equal(new long[] { 19 }, Assert.IsType<long[]>(count.Value));
        Assert.Equal(new[] { 3.25 }, Assert.IsType<double[]>(Variable(model, "ratio").Value));
        Assert.Equal(new[] { "after-save" }, Assert.IsType<string[]>(Variable(model, "label").Value));
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(new[] { edit }, variables));
    }

    [Fact]
    public void Opening_values_are_live_private_copies_and_untouched_values_remain_latest()
    {
        var model = Model(); var count = Variable(model, "count"); var host = new NodeConfigurationEditHost(model);
        count.Value = new long[] { 10 };
        var variables = host.BeginVariableEdit(); var edit = host.BeginEdit("f", "parse");
        Assert.Equal(new long[] { 10 }, Assert.IsType<long[]>(Variable(variables.Configuration, "count").Value));
        Assert.NotSame(count.Value, Variable(variables.Configuration, "count").Value);
        count.Value = new long[] { 11 }; var revision = count.ValueRevision;
        Variable(variables.Configuration, "count").Name = "CountSaved"; Parameters(edit).Separator = ";";
        host.CommitBatch(new[] { edit }, variables);
        Assert.Equal(new long[] { 11 }, Assert.IsType<long[]>(count.Value)); Assert.Equal(revision, count.ValueRevision);
        Assert.Equal(";", Parameters(model).Separator);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_same_value_intent_rejects_runtime_revision_changes_including_ABA(bool aba)
    {
        var model = Model(); var count = Variable(model, "count"); var host = new NodeConfigurationEditHost(model);
        count.Value = new long[] { 10 }; var variables = host.BeginVariableEdit(); var edit = host.BeginEdit("f", "parse");
        variables.SetValue("count", new long[] { 10 }); Variable(variables.Configuration, "count").Name = "CountSaved";
        Parameters(edit).Separator = ";"; var before = host.Capture();
        count.Value = new long[] { 11 }; if (aba) count.Value = new long[] { 10 };
        var latest = count.Value; var revision = count.ValueRevision;
        Assert.Throws<InvalidOperationException>(() => host.ValidateBatch(new[] { edit }, variables));
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(new[] { edit }, variables));
        Assert.Same(before, host.Capture()); Assert.False(host.CanUndo); Assert.Same(latest, count.Value);
        Assert.Equal(revision, count.ValueRevision); Assert.Equal("Count", count.Name); Assert.Equal(",", Parameters(model).Separator);
        Assert.Equal("CountSaved", Variable(variables.Configuration, "count").Name); Assert.True(variables.HasValueEdits);
    }

    [Fact]
    public void Explicit_store_equal_value_still_executes_acceptance_and_consumes_exactly_one_generation()
    {
        var model = Model(); var count = Variable(model, "count"); var host = new NodeConfigurationEditHost(model);
        count.Value = new long[] { 10 }; var variables = host.BeginVariableEdit(); var before = host.Capture(); var revision = count.ValueRevision;
        variables.SetValue("count", new long[] { 0 });
        var publication = host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), variables);
        Assert.Equal(before.Version + 1, publication.Snapshot.Version); Assert.Equal(revision + 1, count.ValueRevision);
        Assert.Equal(new long[] { 0 }, Assert.IsType<long[]>(count.Value)); Assert.True(host.CanUndo);
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), variables));
        count.Value = new long[] { 33 }; var latestRevision = count.ValueRevision;
        host.Undo(); host.Redo();
        Assert.Equal(new long[] { 33 }, Assert.IsType<long[]>(count.Value)); Assert.Equal(latestRevision, count.ValueRevision);
    }

    [Fact]
    public void Store_equal_explicit_value_command_still_rejects_a_revision_conflict()
    {
        var model = Model(); var count = Variable(model, "count"); var host = new NodeConfigurationEditHost(model);
        count.Value = new long[] { 10 }; var variables = host.BeginVariableEdit(); var before = host.Capture();
        variables.SetValue("count", new long[] { 0 }); count.Value = new long[] { 11 };
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), variables));
        Assert.Same(before, host.Capture()); Assert.Equal(new long[] { 11 }, Assert.IsType<long[]>(count.Value)); Assert.False(host.CanUndo);
    }

    [Fact]
    public void Pure_unedited_save_is_a_no_op_and_never_replays_opening_or_history_values()
    {
        var model = Model(); var count = Variable(model, "count"); var host = new NodeConfigurationEditHost(model);
        count.Value = new long[] { 10 }; var variables = host.BeginVariableEdit(); count.Value = new long[] { 11 };
        var before = host.Capture(); var revision = count.ValueRevision;
        Assert.Same(before, host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), variables).Snapshot);
        Assert.False(host.CanUndo); Assert.Equal(revision, count.ValueRevision); Assert.Equal(new long[] { 11 }, Assert.IsType<long[]>(count.Value));
    }
    [Fact]
    public void Conflict_in_second_variable_writes_neither_first_value_nor_any_configuration()
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var before = host.Capture();
        var variables = host.BeginVariableEdit(); var edit = host.BeginEdit("f", "parse");
        var count = Variable(model, "count"); var ratio = Variable(model, "ratio"); var firstRevision = count.ValueRevision;
        variables.SetValue("count", new long[] { 19 }); variables.SetValue("ratio", new[] { 3.25 });
        Variable(variables.Configuration, "count").Name = "CountSaved"; Parameters(edit).Separator = ";";
        ratio.Value = new[] { 7.5 };
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(new[] { edit }, variables));
        Assert.Same(before, host.Capture()); Assert.Equal(firstRevision, count.ValueRevision);
        Assert.Equal(new long[] { 0 }, Assert.IsType<long[]>(count.Value)); Assert.Equal("Count", count.Name);
        Assert.Equal(new[] { 7.5 }, Assert.IsType<double[]>(ratio.Value)); Assert.Equal(",", Parameters(model).Separator);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(7)]
    public void Raw_acceptance_failure_restores_every_checkpoint_and_the_same_drafts_can_retry(int failureAfter)
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var before = host.Capture();
        var collection = model.GlobalVariables; var order = collection.ToArray(); var values = order.Select(v => v.Value).ToArray();
        var revisions = order.Select(v => v.ValueRevision).ToArray(); var consumers = order.Select(v => v.ConsumerBindings).ToArray();
        var node = model.Workflows[0].Nodes[0]; var parameters = node.Parameters; var settings = node.ParamSettings;
        var variables = host.BeginVariableEdit(); var edit = host.BeginEdit("f", "parse"); Parameters(edit).Separator = ";";
        Variable(variables.Configuration, "count").Name = "CountSaved"; variables.SetValue("count", new long[] { 19 });
        variables.SetValue("ratio", new[] { 3.25 }); Variable(variables.Configuration, "label").Name = "LabelSaved";
        variables.SetValue("label", new[] { "after-save" });
        variables.Configuration.GlobalVariables.Add(new("Ready", new[] { true }, "Boolean") { Id = "ready" });
        FailAfter(host, failureAfter);
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(new[] { edit }, variables));
        Assert.Same(before, host.Capture()); Assert.False(host.CanUndo); Assert.Same(collection, model.GlobalVariables);
        Assert.Equal(order, collection); Assert.Same(parameters, node.Parameters); Assert.Same(settings, node.ParamSettings);
        Assert.Equal("Count", order[0].Name); Assert.Equal("Label", order[2].Name);
        for (var index = 0; index < order.Length; index++)
        {
            Assert.Same(values[index], order[index].Value); Assert.Equal(revisions[index], order[index].ValueRevision);
            Assert.Same(consumers[index], order[index].ConsumerBindings);
        }
        Assert.False(Monitor.IsEntered(GlobalVariable.ValueGate));
        FailAfter(host, -1); host.CommitBatch(new[] { edit }, variables);
        Assert.Equal(before.Version + 1, host.Capture().Version); Assert.Equal(";", Parameters(model).Separator);
        Assert.Equal("CountSaved", Variable(model, "count").Name); Assert.NotNull(Variable(model, "ready"));
    }

    [Fact]
    public void Added_removed_and_reordered_definitions_preserve_collection_and_history_live_values()
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var collection = model.GlobalVariables;
        var count = Variable(model, "count"); var ratio = Variable(model, "ratio"); var variables = host.BeginVariableEdit();
        var edited = Variable(variables.Configuration, "count"); edited.Name = "CountSaved"; edited.Group = "station"; edited.Index = 8; edited.Description = "description";
        variables.Configuration.GlobalVariables.Remove(Variable(variables.Configuration, "ratio"));
        variables.Configuration.GlobalVariables.Reverse();
        variables.Configuration.GlobalVariables.Add(new("Ready", new[] { true }, "Boolean") { Id = "ready" });
        variables.SetValue("ready", new[] { false });
        host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), variables);
        var ready = Variable(model, "ready"); Assert.False(Assert.IsType<bool[]>(ready.Value)[0]);
        Assert.Equal(new[] { "label", "count", "ready" }, collection.Select(v => v.Id));
        Assert.Equal(("CountSaved", "station", 8, "description"), (count.Name, count.Group, count.Index, count.Description));
        count.Value = new long[] { 123 }; ready.Value = new[] { true }; ratio.Value = new[] { 9.5 };
        host.Undo(); Assert.Same(collection, model.GlobalVariables); Assert.Same(ratio, Variable(model, "ratio"));
        Assert.Equal(new[] { 9.5 }, Assert.IsType<double[]>(ratio.Value)); Assert.Equal(new long[] { 123 }, Assert.IsType<long[]>(count.Value));
        Assert.Equal("Count", count.Name); host.Redo(); Assert.Same(ready, Variable(model, "ready"));
        Assert.True(Assert.IsType<bool[]>(ready.Value)[0]); Assert.Equal(new long[] { 123 }, Assert.IsType<long[]>(count.Value));
    }

    [Fact]
    public void Stable_variable_rename_and_pending_node_configuration_are_composed_before_binding_validation()
    {
        var model = Model(); Parameters(model).InputText = "__GlobalVars__.Label"; var host = new NodeConfigurationEditHost(model);
        var edit = host.BeginEdit("f", "parse"); Parameters(edit).Separator = ";"; var variables = host.BeginVariableEdit();
        Variable(variables.Configuration, "label").Name = "LabelSaved"; variables.SetValue("label", new[] { "after-save" });
        host.ValidateBatch(new[] { edit }, variables); host.CommitBatch(new[] { edit }, variables);
        Assert.Equal(";", Parameters(model).Separator); Assert.Equal("__GlobalVars__.LabelSaved", Parameters(model).InputText);
        Assert.Equal("label", Parameters(model).BindingSourceIdentities["InputText"].VariableId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Incompatible_type_or_delete_does_not_silently_detach_an_existing_consumer(bool delete)
    {
        var model = Model(); Parameters(model).InputText = "__GlobalVars__.Label"; var host = new NodeConfigurationEditHost(model);
        var before = host.Capture(); var variables = host.BeginVariableEdit();
        if (delete) variables.Configuration.GlobalVariables.Remove(Variable(variables.Configuration, "label"));
        else { Variable(variables.Configuration, "label").Type = "Double"; variables.SetValue("label", new[] { 3.25 }); }
        Assert.Throws<InvalidOperationException>(() => host.ValidateBatch(Array.Empty<NodeConfigurationEditDraft>(), variables));
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), variables));
        Assert.Same(before, host.Capture()); Assert.Equal("__GlobalVars__.Label", Parameters(model).InputText);
        Assert.Equal("String", Variable(model, "label").Type);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Conflicting_target_parameter_or_setting_delta_is_rejected_without_losing_node_edits(bool settings)
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var before = host.Capture();
        var variables = host.BeginVariableEdit(); var edit = host.BeginEdit("f", "parse"); Parameters(edit).Separator = ";";
        if (settings) variables.Configuration.Workflows[0].Nodes[0].ParamSettings.SetSetting(ParamSetting.CreateConstant("Separator", "|"));
        else Parameters(variables.Configuration).InputText = "changed";
        Assert.Throws<InvalidOperationException>(() => host.ValidateBatch(new[] { edit }, variables));
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(new[] { edit }, variables));
        Assert.Same(before, host.Capture()); Assert.Equal(";", Parameters(edit).Separator); Assert.Equal(",", Parameters(model).Separator);
    }

    [Fact]
    public void Identical_node_and_variable_parameter_deltas_coalesce_instead_of_conflicting()
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var variables = host.BeginVariableEdit(); var edit = host.BeginEdit("f", "parse");
        Parameters(edit).Separator = ";"; Parameters(variables.Configuration).Separator = ";";
        host.ValidateBatch(new[] { edit }, variables); host.CommitBatch(new[] { edit }, variables);
        Assert.Equal(";", Parameters(model).Separator);
    }
    [Fact]
    public void Unchanged_node_draft_allows_variable_target_binding_delta_and_disjoint_pending_node()
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var variables = host.BeginVariableEdit();
        var unchanged = host.BeginEdit("f", "parse"); var other = host.BeginEdit("f", "other"); Parameters(other).Separator = ";";
        Parameters(variables.Configuration).InputText = "__GlobalVars__.Label";
        variables.Configuration.Workflows[0].Nodes[0].ParamSettings.SetSetting(ParamSetting.CreateBinding("InputText", "__GlobalVars__", "Label"));
        host.CommitBatch(new[] { unchanged, other }, variables);
        Assert.Equal("__GlobalVars__.Label", Parameters(model).InputText); Assert.NotNull(model.Workflows[0].Nodes[0].ParamSettings.GetSetting("InputText"));
        Assert.Equal(";", Parameters(model, "other").Separator);
    }

    [Theory]
    [InlineData("empty-name")]
    [InlineData("duplicate-name")]
    [InlineData("empty-id")]
    [InlineData("duplicate-id")]
    [InlineData("bad-type")]
    [InlineData("bad-new-value")]
    [InlineData("topology")]
    public void Invalid_variable_or_unsupported_structure_rejects_the_entire_private_batch(string invalid)
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var before = host.Capture();
        var variables = host.BeginVariableEdit(); var edit = host.BeginEdit("f", "parse"); Parameters(edit).Separator = ";";
        switch (invalid)
        {
            case "empty-name": Variable(variables.Configuration, "count").Name = " "; break;
            case "duplicate-name": Variable(variables.Configuration, "count").Name = "rAtIo"; break;
            case "empty-id": Variable(variables.Configuration, "count").Id = ""; break;
            case "duplicate-id": Variable(variables.Configuration, "count").Id = "ratio"; break;
            case "bad-type": Variable(variables.Configuration, "count").Type = "unknown"; break;
            case "bad-new-value": variables.Configuration.GlobalVariables.Add(new("Bad", "not-integer", "Integer") { Id = "bad" }); break;
            default: variables.Configuration.Workflows[0].Nodes.RemoveAt(0); break;
        }
        Assert.Throws<InvalidOperationException>(() => host.ValidateBatch(new[] { edit }, variables));
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(new[] { edit }, variables));
        Assert.Same(before, host.Capture()); Assert.False(host.CanUndo); Assert.Equal(",", Parameters(model).Separator);
        Assert.Equal("Count", Variable(model, "count").Name); Assert.Equal(3, model.GlobalVariables.Count);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("type")]
    [InlineData("group")]
    [InlineData("index")]
    [InlineData("description")]
    [InlineData("instance")]
    public void External_real_configuration_changes_still_fail_the_baseline_guard(string field)
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var variables = host.BeginVariableEdit(); var count = Variable(model, "count");
        switch (field)
        {
            case "name": count.Name = "external"; break;
            case "type": count.Type = "Double"; break;
            case "group": count.Group = "external"; break;
            case "index": count.Index = 99; break;
            case "description": count.Description = "external"; break;
            default: model.GlobalVariables[0] = new("Count", new long[] { 0 }, "Integer") { Id = "count" }; break;
        }
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), variables));
    }

    [Fact]
    public void Stale_or_other_host_variable_draft_is_rejected_and_duplicate_node_participants_do_not_publish()
    {
        var model = Model(); var host = new NodeConfigurationEditHost(model); var stale = host.BeginVariableEdit();
        var edit = host.BeginEdit("f", "parse"); Parameters(edit).Separator = ";"; host.Commit(edit);
        stale.SetValue("count", new long[] { 19 });
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), stale));
        var foreign = new NodeConfigurationEditHost(Model()).BeginVariableEdit();
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), foreign));
        var duplicate = host.BeginEdit("f", "parse"); var before = host.Capture();
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(new[] { duplicate, duplicate }, null)); Assert.Same(before, host.Capture());
    }

    [Fact]
    public void Notifications_run_after_publication_outside_locks_and_failure_does_not_unpublish_or_allow_replay()
    {
        var model = Model(); var count = Variable(model, "count"); var host = new NodeConfigurationEditHost(model); var before = host.Capture();
        var variables = host.BeginVariableEdit(); Variable(variables.Configuration, "count").Name = "CountSaved"; variables.SetValue("count", new long[] { 19 });
        var observed = false;
        count.PropertyChanged += (_, _) =>
        {
            Assert.False(Monitor.IsEntered(GlobalVariable.ValueGate));
            Assert.False(Monitor.IsEntered(typeof(NodeConfigurationEditHost).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!));
            Assert.Equal(before.Version + 1, host.Capture().Version); observed = true;
            throw new InvalidOperationException("post-commit observer failure");
        };
        var publication = host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), variables);
        Assert.True(observed); Assert.Same(publication.Snapshot, host.Capture()); Assert.NotNull(host.LastRefreshError);
        Assert.Equal("CountSaved", count.Name); Assert.Equal(new long[] { 19 }, Assert.IsType<long[]>(count.Value));
        Assert.Throws<InvalidOperationException>(() => host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), variables));
    }

    [Fact]
    public void Explicit_value_storage_and_definition_capture_do_not_share_editable_arrays_or_bindings()
    {
        var model = Model(); var count = Variable(model, "count"); var host = new NodeConfigurationEditHost(model); var variables = host.BeginVariableEdit();
        var input = new long[] { 19 }; variables.SetValue("count", input); input[0] = 99;
        Assert.Equal(new long[] { 19 }, Assert.IsType<long[]>(Variable(variables.Configuration, "count").Value));
        host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), variables);
        var captured = GlobalVariable.CaptureDefinitions(model.GlobalVariables); var copy = captured.Single(v => v.Id == count.Id);
        Assert.NotSame(count, copy); Assert.NotSame(count.Value, copy.Value); Assert.NotSame(count.ConsumerBindings, copy.ConsumerBindings);
        Assert.Equal(count.ValueRevision, copy.ValueRevision); copy.Name = "private"; Assert.IsType<long[]>(copy.Value)[0] = 111;
        Assert.Equal("Count", count.Name); Assert.Equal(new long[] { 19 }, Assert.IsType<long[]>(count.Value));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(count)); Assert.False(json.RootElement.TryGetProperty("ValueRevision", out _));
    }

    [Fact]
    public void Definition_capture_copies_binding_metadata_without_notifying_or_sharing_mutable_fields()
    {
        var variable = new GlobalVariable("name", new long[] { 1 }, "Integer", "station")
        {
            Id = "id", Index = 8, Description = "description", RebindingReason = "reason", BindingValidationPending = "pending",
            ProducerBinding = new() { WorkflowId = "f", NodeId = "source", SourceFieldId = Guid.NewGuid(), ResultName = "value" },
            ConsumerBindings = new() { new() { WorkflowId = "f", NodeId = "parse", InputKey = "InputText", ParameterName = "InputText" } }
        };
        var notifications = 0; variable.PropertyChanged += (_, _) => notifications++;
        var copy = Assert.Single(GlobalVariable.CaptureDefinitions(new[] { variable }));
        Assert.Equal(0, notifications); Assert.Equal(variable.Id, copy.Id); Assert.Equal(variable.ValueRevision, copy.ValueRevision);
        Assert.Equal((variable.Name, variable.Type, variable.Group, variable.Index, variable.Description), (copy.Name, copy.Type, copy.Group, copy.Index, copy.Description));
        Assert.NotSame(variable.ProducerBinding, copy.ProducerBinding); Assert.NotSame(variable.ConsumerBindings[0], copy.ConsumerBindings[0]);
        Assert.Equal(variable.ProducerBinding!.SourceFieldId, copy.ProducerBinding!.SourceFieldId);
        copy.ProducerBinding.NodeId = "private"; copy.ConsumerBindings[0].ParameterName = "private";
        Assert.Equal("source", variable.ProducerBinding.NodeId); Assert.Equal("InputText", variable.ConsumerBindings[0].ParameterName);
    }
    [Fact]
    public void Concurrent_definition_capture_never_pairs_a_new_name_with_an_old_value()
    {
        var model = Model(); Variable(model, "count").Name = "n0"; var host = new NodeConfigurationEditHost(model);
        using var start = new ManualResetEventSlim();
        var writer = Task.Run(() =>
        {
            start.Wait();
            for (var value = 1; value <= 20; value++)
            {
                var draft = host.BeginVariableEdit(); Variable(draft.Configuration, "count").Name = "n" + value;
                draft.SetValue("count", new long[] { value }); host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), draft);
            }
        });
        start.Set();
        do
        {
            var definition = GlobalVariable.CaptureDefinitions(model.GlobalVariables).Single(v => v.Id == "count");
            Assert.Equal("n" + Assert.IsType<long[]>(definition.Value)[0], definition.Name);
        } while (!writer.IsCompleted);
        writer.GetAwaiter().GetResult();
    }
}

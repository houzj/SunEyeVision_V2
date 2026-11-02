using SunEyeVision.Workflow;
using SunEyeVision.Plugin.Contracts;
using System.Reflection;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class GraphCopyTests
{
    [Fact]
    public void Graph_history_restoration_does_not_rewind_live_field_values()
    {
        var node = new WorkflowNodeBase("history", "history", "history", "BlobAnalysis")
            { RuntimeConfiguration = RuntimeToolConfiguration.Create(RootCanvasFixture.Directory, "BlobAnalysis") };
        var port = node.RuntimeConfiguration!.SettingsSchema.Inputs.First(p => (p.EditModes & FieldEditModes.Reference) != 0
            && node.RuntimeConfiguration.Fields.Read(p.Id) is ConstantSetting { Value: long });
        node.RuntimeConfiguration = node.RuntimeConfiguration.WithFieldSettings(new Dictionary<string,ParameterSetting>
            { [port.Id] = new VariableSetting(new VariableReference("solution", "variable", port.Type, storage: port.Storage)) });
        node.RuntimeConfiguration = Accept(node.RuntimeConfiguration, port.Id, 7L);
        var snapshot = GraphBindingUndoSnapshot.Capture(new[] { node }, null);
        node.RuntimeConfiguration = Accept(node.RuntimeConfiguration, port.Id, 9L);
        snapshot.Restore();
        Assert.Equal(9L, node.RuntimeConfiguration!.ReadCurrentParameter(port.Id)!.Value);
    }

    [Fact]
    public void Configuration_store_undo_redo_restores_constant_edits()
    {
        var manager = new SolutionManager(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!, Guid.NewGuid().ToString("N")), RootCanvasFixture.Directory);
        var document = manager.CreateUntitledSolution();
        var node = RootCanvasFixture.Node("history");
        RootCanvasFixture.SetValue(node, 7);
        document.Workflows[0].Nodes.Add(node);
        var store = new BindingConfigurationStore(document);
        var draft = store.BeginEdit();
        RootCanvasFixture.SetValue(draft.Configuration.Workflows[0].Nodes[0], 9);
        var changed = store.Publish(draft).Snapshot;
        Assert.Equal(9, RootCanvasFixture.Value(changed.CopyConfiguration().Workflows[0].Nodes[0]));
        var undone = store.Undo(changed.Version);
        Assert.Equal(7, RootCanvasFixture.Value(undone.CopyConfiguration().Workflows[0].Nodes[0]));
        var redone = store.Redo(undone.Version);
        Assert.Equal(9, RootCanvasFixture.Value(redone.CopyConfiguration().Workflows[0].Nodes[0]));
    }

    private static RuntimeToolConfiguration Accept(RuntimeToolConfiguration configuration, string field, long value)
        => (RuntimeToolConfiguration)typeof(RuntimeToolConfiguration).GetMethod("WithAcceptedParameterValue", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(configuration, new object[] { field, value })!;

    [Fact]
    public void Removing_edge_invalidates_only_its_automatic_image_binding()
    {
        var source = RootCanvasFixture.Node("source");
        var target = new WorkflowNodeBase("target", "target", "target", "BlobAnalysis")
            { RuntimeConfiguration = RuntimeToolConfiguration.Create(RootCanvasFixture.Directory, "BlobAnalysis") };
        var output = source.RuntimeConfiguration!.Schema.Outputs.Single(p => p.Id == "output.image");
        var input = target.RuntimeConfiguration!.Schema.WorkflowImageInputId!;
        var reference = new OutputReference(source.Id, output.Id, output.Type, new[] { "data" }, scopeId: "flow");
        target.RuntimeConfiguration = target.RuntimeConfiguration.WithFieldSettings(new Dictionary<string,ParameterSetting>
            { [input] = new ReferenceSetting(reference, OriginId: "connection:edge") });
        Assert.False(ConnectionBindingPolicy.RemoveAutomaticImageBinding(target, "other"));
        Assert.True(ConnectionBindingPolicy.RemoveAutomaticImageBinding(target, "edge"));
        var invalid = Assert.IsType<ReferenceSetting>(target.RuntimeConfiguration.Fields.Read(input));
        Assert.Null(invalid.OriginId);
        Assert.False(string.IsNullOrWhiteSpace(invalid.RebindingReason));
        Assert.False(ConnectionBindingPolicy.RemoveAutomaticImageBinding(target, "edge"));
    }

    [Fact]
    public void Copy_remaps_automatic_connection_identity_and_its_reference()
    {
        var flow = new SunEyeVision.Workflow.Workflow("flow", "flow");
        var source = RootCanvasFixture.Node("source");
        var target = new WorkflowNodeBase("target", "target", "target", "BlobAnalysis")
            { RuntimeConfiguration = RuntimeToolConfiguration.Create(RootCanvasFixture.Directory, "BlobAnalysis") };
        var output = source.RuntimeConfiguration!.Schema.Outputs.Single(p => p.Id == "output.image");
        var input = target.RuntimeConfiguration!.Schema.WorkflowImageInputId!;
        target.RuntimeConfiguration = target.RuntimeConfiguration.WithFieldSettings(new Dictionary<string,ParameterSetting>
        {
            [input] = new ReferenceSetting(new OutputReference(source.Id, output.Id, output.Type, new[] { "data" }, scopeId: flow.Id), OriginId: "connection:edge")
        });
        flow.Nodes.Add(source); flow.Nodes.Add(target);
        flow.Connections.Add(new WorkflowConnection { Id = "edge", SourceNodeId = source.Id, TargetNodeId = target.Id });
        var copy = WorkflowFragmentCopy.Prepare(flow, new[] { source.Id, target.Id }, flow.Id, _ => null,
            (node, _) => new WorkflowNodeBase(node.Id + "-copy", node.Name, node.DispName, node.ToolType)
                { RuntimeConfiguration = RuntimeToolConfiguration.Create(RootCanvasFixture.Directory, node.ToolType) });
        var edge = Assert.Single(copy.Connections);
        var copiedTarget = copy.Nodes.Single(node => node.Id == "target-copy");
        var binding = Assert.IsType<ReferenceSetting>(copiedTarget.RuntimeConfiguration!.Fields.Read(input));
        Assert.Equal("source-copy", edge.SourceNodeId);
        Assert.Equal("target-copy", edge.TargetNodeId);
        Assert.Equal("source-copy", binding.Reference.NodeId);
        Assert.Equal("connection:" + edge.Id, binding.OriginId);
    }

    [Fact]
    public void Copy_preserves_admitted_configuration_and_does_not_mutate_source()
    {
        var flow = new SunEyeVision.Workflow.Workflow("flow", "flow");
        var original = RootCanvasFixture.Node("original");
        RootCanvasFixture.SetValue(original, 7);
        flow.Nodes.Add(original);
        var copy = WorkflowFragmentCopy.Prepare(flow, new[] { original.Id }, flow.Id,
            _ => null, (source, _) => RootCanvasFixture.Node("copy"));
        var cloned = Assert.Single(copy.Nodes);
        Assert.Equal(7, RootCanvasFixture.Value(cloned));
        RootCanvasFixture.SetValue(cloned, 9);
        Assert.Equal(7, RootCanvasFixture.Value(original));
        Assert.Single(flow.Nodes);
    }
}

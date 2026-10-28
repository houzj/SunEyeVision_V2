using System.Reflection;
using System.Text.Json;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ImageSourceConfigurationHistoryTests
{
    private static (Solution Model, WorkflowNodeBase Node, NodeConfigurationEditHost Host) Create()
    {
        var root = new SunEyeVision.Workflow.Workflow("root", "流程1");
        var node = ImageSourceTestRuntime.Node();
        ImageSourceTestRuntime.Edit(node, config=>{config.FilePaths.Add("assets/missing.bmp");config.CurrentIndex=0;});
        root.Nodes.Add(node);
        var model = new Solution { Workflows = new() { root } };
        return (model, node, new NodeConfigurationEditHost(model));
    }

    [Fact]
    public void Source_configuration_is_copied_accepted_and_replayed_with_the_same_formal_node()
    {
        var (model, node, host) = Create();
        var original = node.RuntimeConfiguration;
        var edit = host.BeginEdit("root", "source");
        ImageSourceTestRuntime.Edit(edit.Node, config=>{config.FilePaths.Add("assets/b.bmp");config.CurrentIndex = 1;config.ForRunPaths.Add("assets/b.bmp");config.RunMode = 1;});
        host.Commit(edit);
        Assert.Same(node, model.Workflows[0].Nodes[0]);
        Assert.NotSame(edit.Node.RuntimeConfiguration, node.RuntimeConfiguration);
        Assert.Single(original!.ReadFieldValues().Get<SunEyeVision.Plugin.Contracts.SequenceValue>("local.paths"));
        Assert.Equal(1, ImageSourceTestRuntime.Read(node)!.CurrentIndex);
        var accepted = ImageSourceTestRuntime.Read(host.Capture().CopyConfiguration().Workflows[0].Nodes[0]);
        Assert.Equal(new[] { "assets/missing.bmp", "assets/b.bmp" }, accepted.FilePaths);
        // Mutating a consumed candidate cannot mutate the accepted publication or design.
        var consumed=ImageSourceTestRuntime.Read(edit.Node);consumed.FilePaths.Clear();
        Assert.Equal(2, ImageSourceTestRuntime.Read(node).FilePaths.Count);
        host.Undo();
        Assert.Single(ImageSourceTestRuntime.Read(node)!.FilePaths);
        Assert.Equal(0, ImageSourceTestRuntime.Read(node).CurrentIndex);
        host.Redo();
        Assert.Equal(2, ImageSourceTestRuntime.Read(node)!.FilePaths.Count);
        Assert.Equal(new[] { "assets/b.bmp" }, ImageSourceTestRuntime.Read(node).ForRunPaths);
        var json = JsonSerializer.Serialize(model, WorkflowSerializationOptions.Default);
        var reopened = JsonSerializer.Deserialize<Solution>(json, WorkflowSerializationOptions.Default)!;
        Assert.Equal(1, ImageSourceTestRuntime.Read(reopened.Workflows[0].Nodes[0]).CurrentIndex);
    }

    [Fact]
    public void Formal_document_history_replays_source_configuration_on_the_same_node()
    {
        var (model,node,_)=Create();
        var session=(SolutionSession)Activator.CreateInstance(typeof(SolutionSession),BindingFlags.Instance|BindingFlags.NonPublic,
            null,new object[]{model},null)!;
        var host=session.Editing.Host;
        var edit=host.BeginEdit("root",node.Id);
        ImageSourceTestRuntime.Edit(edit.Node, config=>{config.AutoSwitchEnabled=false;config.FilePaths.Add("assets/b.bmp");});
        host.Commit(edit);
        Assert.Single(session.History.Entries);
        Assert.NotNull(host.LastHistoryReceipt);
        session.History.Undo();
        Assert.Same(node,model.Workflows[0].Nodes.Single());
        Assert.Single(ImageSourceTestRuntime.Read(node)!.FilePaths);
        Assert.True(ImageSourceTestRuntime.Read(node).AutoSwitchEnabled);
        session.History.Redo();
        Assert.Same(node,model.Workflows[0].Nodes.Single());
        Assert.Equal(2,ImageSourceTestRuntime.Read(node)!.FilePaths.Count);
        Assert.False(ImageSourceTestRuntime.Read(node).AutoSwitchEnabled);
    }

    [Fact]
    public void Unrelated_structure_sync_preserves_source_history_and_the_added_node()
    {
        var (model,node,host)=Create();
        var edit=host.BeginEdit("root",node.Id);
        ImageSourceTestRuntime.Edit(edit.Node, config=>{config.AutoSwitchEnabled=false;config.FilePaths.Add("assets/b.bmp");});
        host.Commit(edit);
        var added=new WorkflowNodeBase("other","其他节点","其他节点","Probe")
        {Parameters=new BindingAdaptationB2Tests.SourceParameters()};
        model.Workflows[0].Nodes.Add(added);
        host.SynchronizeStructure();
        host.Undo();
        Assert.Same(added,model.Workflows[0].Nodes.Single(item=>item.Id=="other"));
        Assert.Single(ImageSourceTestRuntime.Read(node)!.FilePaths);
        Assert.True(ImageSourceTestRuntime.Read(node).AutoSwitchEnabled);
        host.Redo();
        Assert.Same(added,model.Workflows[0].Nodes.Single(item=>item.Id=="other"));
        Assert.Equal(2,ImageSourceTestRuntime.Read(node)!.FilePaths.Count);
        Assert.False(ImageSourceTestRuntime.Read(node).AutoSwitchEnabled);
    }

    [Fact]
    public void Source_edit_stales_an_older_parameter_editor_instead_of_overwriting_it()
    {
        var (_, node, host) = Create();
        var old = host.BeginEdit("root", "source");
        var sourceEdit = host.BeginEdit("root", "source");
        ImageSourceTestRuntime.Edit(sourceEdit.Node, config=>{config.AutoSwitchEnabled = false;});
        host.Commit(sourceEdit);
        old.Node.RuntimeConfiguration=old.Node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{"sequence.initial",99L}});
        Assert.Throws<InvalidOperationException>(() => host.Commit(old));
        Assert.Equal(99L,old.Node.RuntimeConfiguration!.ReadFieldValues().Get<long>("sequence.initial"));
        Assert.False(ImageSourceTestRuntime.Read(node)!.AutoSwitchEnabled);
    }

    [Fact]
    public void Failed_acceptance_restores_the_original_source_reference_and_keeps_the_candidate()
    {
        var (_, node, host) = Create();
        var before = node.RuntimeConfiguration;
        var publication = host.Capture();
        var edit = host.BeginEdit("root", "source");
        ImageSourceTestRuntime.Edit(edit.Node, config=>{config.AutoSwitchEnabled = false;});
        var injection = typeof(NodeConfigurationEditHost).GetField("_acceptanceFailureAfterRawMutation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        injection.SetValue(host, 1);
        Assert.Throws<InvalidOperationException>(() => host.Commit(edit));
        Assert.Same(before, node.RuntimeConfiguration);
        Assert.Same(publication, host.Capture());
        Assert.False(host.CanUndo);
        Assert.False(ImageSourceTestRuntime.Read(edit.Node).AutoSwitchEnabled);
        injection.SetValue(host, -1);
        host.Commit(edit);
        Assert.False(ImageSourceTestRuntime.Read(node)!.AutoSwitchEnabled);
    }

    [Fact]
    public void Actual_image_source_can_edit_an_empty_list_without_fabricating_an_execution_path()
    {
        var (_, node, host) = Create();
        var root = new SunEyeVision.Workflow.Workflow("actual", "流程1");root.Nodes.Add(node);
        host = new NodeConfigurationEditHost(new Solution { Workflows = new() { root } });
        var edit = host.BeginEdit("actual",node.Id);
        ImageSourceTestRuntime.Edit(edit.Node,config=>{config.FilePaths.Clear();config.ForRunPaths.Clear();config.CurrentIndex=-1;});
        host.Commit(edit);
        Assert.Empty(ImageSourceTestRuntime.Read(node).FilePaths);
        Assert.Empty(node.RuntimeConfiguration!.Validate()); // Structurally editable; readiness is separate.
        Assert.NotEmpty(SunEyeVision.UI.Services.Workflow.RootWorkflowReadiness.EvaluateImageSource(node.Id,ImageSourceTestRuntime.Read(node)));
        host.Undo();Assert.Single(ImageSourceTestRuntime.Read(node).FilePaths);
        host.Redo();Assert.Empty(ImageSourceTestRuntime.Read(node).FilePaths);
        host.Commit(host.BeginEdit("actual",node.Id));
        Assert.Empty(ImageSourceTestRuntime.Read(node).FilePaths);
    }

    [Fact]
    public void Source_edit_does_not_waive_validation_of_changed_actual_tool_parameters()
    {
        var (_,node,host)=Create();var before=node.RuntimeConfiguration;
        var edit=host.BeginEdit("root",node.Id);
        Assert.Throws<ArgumentException>(()=>edit.Node.RuntimeConfiguration=edit.Node.RuntimeConfiguration!.WithFieldValues(
            new Dictionary<string,object?>{{"local.auto-switch",false},{"camera.timeout-ms",0L}}));
        Assert.Same(before,node.RuntimeConfiguration);Assert.True(ImageSourceTestRuntime.Read(node).AutoSwitchEnabled);
        Assert.False(host.CanUndo);
    }

    [Fact]
    public void Copied_node_has_independent_runtime_source_settings_and_list_edits()
    {
        var (_,node,_)=Create();var copy=node.Clone();
        Assert.NotSame(node.RuntimeConfiguration,copy.RuntimeConfiguration);
        ImageSourceTestRuntime.Edit(copy,config=>{config.FilePaths.Add("copy.png");config.AutoSwitchEnabled=false;});
        Assert.Single(ImageSourceTestRuntime.Read(node).FilePaths);Assert.True(ImageSourceTestRuntime.Read(node).AutoSwitchEnabled);
        Assert.Equal(2,ImageSourceTestRuntime.Read(copy).FilePaths.Count);Assert.False(ImageSourceTestRuntime.Read(copy).AutoSwitchEnabled);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("selection")]
    [InlineData("mode")]
    [InlineData("timeout")]
    [InlineData("sequence")]
    [InlineData("format")]
    [InlineData("sourceKind")]
    public void Invalid_source_candidates_do_not_publish(string failure)
    {
        var (_, node, host) = Create();
        var before = node.RuntimeConfiguration;
        var edit = host.BeginEdit("root", "source");
        Assert.ThrowsAny<Exception>(()=>ImageSourceTestRuntime.Edit(edit.Node, config=>
        {
            switch(failure)
            {
                case "identity":config.NodeId="other";break;
                case "selection":config.CurrentIndex=9;break;
                case "mode":config.RunMode=7;break;
                case "timeout":config.CameraTimeoutMs=0;break;
                case "sequence":config.SNInitialValue=-1;break;
                case "format":config.OutputFormat=(SunEyeVision.Plugin.SDK.Vision.Contracts.ImageOutputFormat)999;break;
                default:config.SourceKind=(SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind)999;break;
            }
        }));
        Assert.Same(before,node.RuntimeConfiguration);
        Assert.False(host.CanUndo);
    }
}

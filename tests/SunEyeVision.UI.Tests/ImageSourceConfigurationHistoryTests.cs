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
        var node = new WorkflowNodeBase("source", "图像源1", "图像源1", "Probe")
        {
            Parameters = new BindingAdaptationB2Tests.SourceParameters(),
            ImageSourceConfig = new() { NodeId = "source", FilePaths = new() { "assets/missing.bmp" }, CurrentIndex = 0 }
        };
        root.Nodes.Add(node);
        var model = new Solution { Workflows = new() { root } };
        return (model, node, new NodeConfigurationEditHost(model));
    }

    [Fact]
    public void Source_configuration_is_copied_accepted_and_replayed_with_the_same_formal_node()
    {
        var (model, node, host) = Create();
        var original = node.ImageSourceConfig;
        var edit = host.BeginEdit("root", "source");
        edit.Node.ImageSourceConfig!.FilePaths.Add("assets/b.bmp");
        edit.Node.ImageSourceConfig.CurrentIndex = 1;
        edit.Node.ImageSourceConfig.ForRunPaths.Add("assets/b.bmp");
        edit.Node.ImageSourceConfig.RunMode = 1;
        host.Commit(edit);
        Assert.Same(node, model.Workflows[0].Nodes[0]);
        Assert.NotSame(edit.Node.ImageSourceConfig, node.ImageSourceConfig);
        Assert.Single(original!.FilePaths);
        Assert.Equal(1, node.ImageSourceConfig!.CurrentIndex);
        var accepted = host.Capture().CopyConfiguration().Workflows[0].Nodes[0].ImageSourceConfig!;
        Assert.Equal(new[] { "assets/missing.bmp", "assets/b.bmp" }, accepted.FilePaths);
        // Mutating a consumed candidate cannot mutate the accepted publication or design.
        edit.Node.ImageSourceConfig.FilePaths.Clear();
        Assert.Equal(2, node.ImageSourceConfig.FilePaths.Count);
        host.Undo();
        Assert.Single(node.ImageSourceConfig!.FilePaths);
        Assert.Equal(0, node.ImageSourceConfig.CurrentIndex);
        host.Redo();
        Assert.Equal(2, node.ImageSourceConfig!.FilePaths.Count);
        Assert.Equal(new[] { "assets/b.bmp" }, node.ImageSourceConfig.ForRunPaths);
        var json = JsonSerializer.Serialize(model, WorkflowSerializationOptions.Default);
        var reopened = JsonSerializer.Deserialize<Solution>(json, WorkflowSerializationOptions.Default)!;
        Assert.Equal(1, reopened.Workflows[0].Nodes[0].ImageSourceConfig!.CurrentIndex);
    }

    [Fact]
    public void Source_edit_stales_an_older_parameter_editor_instead_of_overwriting_it()
    {
        var (_, node, host) = Create();
        var old = host.BeginEdit("root", "source");
        var sourceEdit = host.BeginEdit("root", "source");
        sourceEdit.Node.ImageSourceConfig!.AutoSwitchEnabled = false;
        host.Commit(sourceEdit);
        ((BindingAdaptationB2Tests.SourceParameters)old.Node.Parameters).Name = "pending";
        Assert.Throws<InvalidOperationException>(() => host.Commit(old));
        Assert.Equal("pending", ((BindingAdaptationB2Tests.SourceParameters)old.Node.Parameters).Name);
        Assert.False(node.ImageSourceConfig!.AutoSwitchEnabled);
    }

    [Fact]
    public void Failed_acceptance_restores_the_original_source_reference_and_keeps_the_candidate()
    {
        var (_, node, host) = Create();
        var before = node.ImageSourceConfig;
        var publication = host.Capture();
        var edit = host.BeginEdit("root", "source");
        edit.Node.ImageSourceConfig!.AutoSwitchEnabled = false;
        var injection = typeof(NodeConfigurationEditHost).GetField("_acceptanceFailureAfterRawMutation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        injection.SetValue(host, 1);
        Assert.Throws<InvalidOperationException>(() => host.Commit(edit));
        Assert.Same(before, node.ImageSourceConfig);
        Assert.Same(publication, host.Capture());
        Assert.False(host.CanUndo);
        Assert.False(edit.Node.ImageSourceConfig.AutoSwitchEnabled);
        injection.SetValue(host, -1);
        host.Commit(edit);
        Assert.False(node.ImageSourceConfig!.AutoSwitchEnabled);
    }

    [Fact]
    public void Actual_image_source_can_edit_an_empty_list_without_fabricating_an_execution_path()
    {
        var (_, node, host) = Create();
        SunEyeVision.Core.Services.Serialization.ParameterTypeRegistry.RegisterParameterType(typeof(SunEyeVision.Tool.ImageLoad.ImageLoadParameters), "ImageLoad");
        node.Parameters = new SunEyeVision.Tool.ImageLoad.ImageLoadParameters();
        // Recreate the host after installing the actual tool contract.
        var root = new SunEyeVision.Workflow.Workflow("actual", "流程1");
        root.Nodes.Add(node);
        host = new NodeConfigurationEditHost(new Solution { Workflows = new() { root } });
        Assert.False(node.Parameters.Validate().IsValid);
        var edit = host.BeginEdit("actual", node.Id);
        edit.Node.ImageSourceConfig!.FilePaths.Clear();
        edit.Node.ImageSourceConfig.CurrentIndex = -1;
        host.Commit(edit);
        Assert.Empty(node.ImageSourceConfig!.FilePaths);
        Assert.Empty(((SunEyeVision.Tool.ImageLoad.ImageLoadParameters)node.Parameters).FilePath);
        Assert.False(node.Parameters.Validate().IsValid); // Still not executable.
        host.Undo();
        Assert.Single(node.ImageSourceConfig!.FilePaths);
        host.Redo();
        Assert.Empty(node.ImageSourceConfig!.FilePaths);
    }

    [Fact]
    public void Source_edit_does_not_waive_validation_of_changed_actual_tool_parameters()
    {
        var (_, node, _) = Create();
        SunEyeVision.Core.Services.Serialization.ParameterTypeRegistry.RegisterParameterType(typeof(SunEyeVision.Tool.ImageLoad.ImageLoadParameters), "ImageLoad");
        node.Parameters = new SunEyeVision.Tool.ImageLoad.ImageLoadParameters { FilePath = "before.bmp" };
        var root = new SunEyeVision.Workflow.Workflow("actual", "流程1");
        root.Nodes.Add(node);
        var host = new NodeConfigurationEditHost(new Solution { Workflows = new() { root } });
        var edit = host.BeginEdit("actual", node.Id);
        edit.Node.ImageSourceConfig!.AutoSwitchEnabled = false;
        ((SunEyeVision.Tool.ImageLoad.ImageLoadParameters)edit.Node.Parameters).FilePath = string.Empty;
        Assert.Throws<InvalidOperationException>(() => host.Commit(edit));
        Assert.True(node.ImageSourceConfig!.AutoSwitchEnabled);
        Assert.Equal("before.bmp", ((SunEyeVision.Tool.ImageLoad.ImageLoadParameters)node.Parameters).FilePath);
        Assert.False(host.CanUndo);
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
        var before = node.ImageSourceConfig;
        var edit = host.BeginEdit("root", "source");
        switch (failure)
        {
            case "identity": edit.Node.ImageSourceConfig!.NodeId = "other"; break;
            case "selection": edit.Node.ImageSourceConfig!.CurrentIndex = 9; break;
            case "mode": edit.Node.ImageSourceConfig!.RunMode = 7; break;
            case "timeout": edit.Node.ImageSourceConfig!.CameraTimeoutMs = 0; break;
            case "sequence": edit.Node.ImageSourceConfig!.SNInitialValue = -1; break;
            case "format": edit.Node.ImageSourceConfig!.OutputFormat = (SunEyeVision.Plugin.SDK.Vision.Contracts.ImageOutputFormat)999; break;
            default: edit.Node.ImageSourceConfig!.SourceKind = (SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind)999; break;
        }
        Assert.Throws<InvalidOperationException>(() => host.Commit(edit));
        Assert.Same(before, node.ImageSourceConfig);
        Assert.False(host.CanUndo);
    }
}

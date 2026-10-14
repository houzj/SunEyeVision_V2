using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Tool.ReceiveData;
using SunEyeVision.Tool.SendData;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class BindingAdaptationB1Tests
{
    private static WorkflowNodeBase Node(string id, ToolParameters p) => new(id, id, id, "Test") { Parameters = p };
    private static (Solution Solution, OutputBindingChangeService Service) Setup(params WorkflowNodeBase[] nodes)
    {
        var flow = new SunEyeVision.Workflow.Workflow("flow", "flow"); foreach (var n in nodes) flow.Nodes.Add(n);
        var solution = new Solution { Workflows = new() { flow } }; var service = new OutputBindingChangeService(); service.Reset(solution); return (solution, service);
    }
    [Fact]
    public void Changing_sending_type_checks_its_existing_upstream_and_preserves_other_rows()
    {
        var source = new ReceiveDataParameters { OutputType = "Double" }; var send = new SendDataParameters();
        send.Rows[0].DataType = "Double"; send.Rows[0].InputText = "upstream." + source.OutputDefinition().BindingKey;
        send.Rows.Add(new() { Name = "other", InputText = "literal", DataType = "String" });
        var (_, service) = Setup(Node("upstream", source), Node("send", send));
        send.Rows[0].DataType = "String"; Assert.Single(service.Commit("send"));
        Assert.Empty(send.Rows[0].InputText); Assert.Equal("literal", send.Rows[1].InputText); Assert.Single(send.RebindingRequired);
    }
    [Fact]
    public void Compatible_sending_type_change_preserves_numeric_source()
    {
        var source = new ReceiveDataParameters { OutputType = "Double" }; var send = new SendDataParameters();
        send.Rows[0].DataType = "Double"; send.Rows[0].InputText = "upstream." + source.OutputDefinition().BindingKey;
        var (_, service) = Setup(Node("upstream", source), Node("send", send));
        send.Rows[0].DataType = "Float"; Assert.Empty(service.Commit("send")); Assert.NotEmpty(send.Rows[0].InputText);
    }
    [Fact]
    public void Parser_interpretation_change_requires_confirmation_but_delimiter_change_does_not()
    {
        var source = new ReceiveDataParameters { OutputType = "String" }; var parse = new ProtocolParseParameters { InputText = "upstream." + source.OutputDefinition().BindingKey };
        var (_, service) = Setup(Node("upstream", source), Node("parse", parse));
        parse.Separator = ";"; Assert.Empty(service.Commit("parse"));
        parse.Mode = "Bytes"; Assert.Single(service.Commit("parse")); Assert.Empty(parse.InputText);
        parse.InputText = "upstream." + source.OutputDefinition().BindingKey; service.Commit("parse"); Assert.Empty(parse.RebindingRequired);
    }
    [Fact]
    public void Invalid_nonempty_literal_is_retained_and_cannot_bypass_preflight_then_valid_literal_recovers()
    {
        var send = new SendDataParameters(); send.Rows[0].DataType = "Int32"; send.Rows[0].InputText = "not-a-number";
        send.RebindingRequired["Row:" + send.Rows[0].FieldId] = "需重新配置";
        var (_, service) = Setup(Node("send", send)); service.Commit("send");
        Assert.Equal("not-a-number", send.Rows[0].InputText); Assert.Single(send.RebindingRequired);
        var executor = new ToolExecutor(new ParameterResolver()); Assert.False(executor.ResolveRuntimeParameters(new(send)).IsValid);
        send.Rows[0].InputText = "1111"; Assert.False(executor.ResolveRuntimeParameters(new(send)).IsValid);
        service.Commit("send"); Assert.Empty(send.RebindingRequired); Assert.True(executor.ResolveRuntimeParameters(new(send)).IsValid);
    }
    [Fact]
    public void Nonexistent_guid_reference_is_not_a_literal_and_wrong_type_never_clears_marker()
    {
        var send = new SendDataParameters(); send.Rows[0].DataType = "Double";
        send.Rows[0].InputText = Guid.NewGuid() + ".Field_deleted";
        var (_, service) = Setup(Node("send", send)); service.Commit("send"); Assert.Empty(send.Rows[0].InputText); Assert.Single(send.RebindingRequired);
        send.Rows[0].InputText = "NaN"; service.Commit("send"); Assert.Single(send.RebindingRequired);
        send.Rows[0].InputText = "1.5"; service.Commit("send"); Assert.Empty(send.RebindingRequired);
    }
    [Fact]
    public void Parser_invalid_hex_constant_does_not_recover_until_it_is_actually_parseable()
    {
        var parse = new ProtocolParseParameters { Mode = "Bytes", InputText = "GG" }; parse.RebindingRequired["InputText"] = "需要重新配置";
        var (_, service) = Setup(Node("parse", parse)); service.Commit("parse"); Assert.Equal("GG", parse.InputText); Assert.Single(parse.RebindingRequired);
        parse.InputText = "37"; service.Commit("parse"); Assert.Empty(parse.RebindingRequired);
    }
    private sealed class NumericParameters : ToolParameters { public double Number { get; set; } }
    private sealed class IntegerParameters : ToolParameters { public int Number { get; set; } }
    [Fact]
    public void Ordinary_int_parameter_uses_sdk_integer_vocabulary_not_exact_long_clr_rejection()
    {
        var p = new IntegerParameters(); var source = new ReceiveDataParameters { OutputType = "Integer" }; var node = Node("target", p);
        node.ParamSettings.SetSetting(ParamSetting.CreateBinding("Number", "source", source.OutputDefinition().BindingKey));
        var (_, service) = Setup(Node("source", source), node);
        Assert.Empty(service.Commit("target")); Assert.Single(node.ParamSettings.Settings);
        source.OutputName = "改名"; Assert.Empty(service.Commit("source")); Assert.Single(node.ParamSettings.Settings);
    }
    [Fact]
    public void Parser_new_source_selected_with_new_mode_is_not_mistaken_for_old_binding()
    {
        var first = new ReceiveDataParameters { OutputType = "String" }; var second = new ReceiveDataParameters();
        var parse = new ProtocolParseParameters { InputText = "first." + first.OutputDefinition().BindingKey };
        var (_, service) = Setup(Node("first", first), Node("second", second), Node("parse", parse));
        parse.Mode = "Bytes"; parse.InputText = "second." + second.OutputDefinition().BindingKey;
        Assert.Empty(service.Commit("parse")); Assert.NotEmpty(parse.InputText);
    }
    [Fact]
    public void Ordinary_binding_and_explicit_constant_are_validated_before_marker_recovery()
    {
        var input = new NumericParameters(); input.RebindingRequired["Number"] = "需要重新配置";
        var node = Node("consumer", input); var source = new ReceiveDataParameters { OutputType = "String" };
        node.ParamSettings.SetSetting(ParamSetting.CreateBinding("Number", "upstream", source.OutputDefinition().BindingKey));
        var (_, service) = Setup(Node("upstream", source), node); service.Commit("consumer"); Assert.Empty(node.ParamSettings.Settings); Assert.Single(input.RebindingRequired);
        node.ParamSettings.SetSetting(ParamSetting.CreateConstant("Number", "bad")); service.Commit("consumer"); Assert.Single(input.RebindingRequired);
        node.ParamSettings.SetSetting(ParamSetting.CreateConstant("Number", 1.5d)); service.Commit("consumer"); Assert.Empty(input.RebindingRequired);
    }
    private sealed class ImageParameters : ToolParameters { public AvailableDataSource? ImageSource { get; set; } }
    private sealed class ImageOutputParameters : ToolParameters, IOutputContractProvider
    { public IReadOnlyList<OutputVariable> GetOutputContract() => new[] { new OutputVariable { Name = "image", VariableType = "Image", DisplayName = "图像" } }; }
    [Fact]
    public void Image_ownership_and_pending_state_survive_parameter_snapshot()
    {
        var p = new ImageParameters { ImageSource = new() { SourceNodeId = "source", PropertyName = "image", AutomaticConnectionId = "edge", PropertyType = typeof(IVisionImage) } };
        p.RebindingRequired["ImageSource"] = "需重配";
        var copy = (ImageParameters)ToolParameterSnapshot.Create(p);
        Assert.Equal("edge", copy.ImageSource!.AutomaticConnectionId); Assert.Single(copy.RebindingRequired);
        Assert.NotSame(p.ImageSource, copy.ImageSource);
    }
    [Theory]
    [InlineData(typeof(long))]
    [InlineData(typeof(long[]))]
    [InlineData(typeof(string[]))]
    [InlineData(typeof(IVisionImage[]))]
    [InlineData(typeof(SunEyeVision.Plugin.SDK.Models.Communication.BinaryPayload))]
    public void Source_type_and_array_shape_survive_snapshot_without_runtime_data(Type type)
    {
        var p = new ImageParameters { ImageSource = new() { SourceNodeId = "source", PropertyName = "field", PropertyType = type, CurrentValue = new object() } };
        var copy = (ImageParameters)ToolParameterSnapshot.Create(p);
        Assert.Equal(type, copy.ImageSource!.PropertyType); Assert.Equal(p.ImageSource.GetBindingPath(), copy.ImageSource.GetBindingPath()); Assert.Null(copy.ImageSource.CurrentValue);
    }
    [Fact]
    public void Static_output_metadata_can_validate_reselected_image_source()
    {
        var p = new ImageParameters { ImageSource = new() { SourceNodeId = "source", PropertyName = "image" } }; p.RebindingRequired["ImageSource"] = "需重配";
        var (_, service) = Setup(Node("source", new GenericToolParameters()), Node("target", p));
        service.StaticContractReader = _ => new[] { new OutputVariable { Name = "image", VariableType = "Image" } };
        service.Commit("target"); Assert.Empty(p.RebindingRequired);
    }
    [Fact]
    public void Solution_reset_drops_previous_scope_metadata_reader()
    {
        var (solution, service) = Setup(Node("source", new GenericToolParameters()));
        service.StaticContractReader = _ => new[] { new OutputVariable { Name = "stale", VariableType = "String" } };
        service.Reset(solution); Assert.Null(service.StaticContractReader); Assert.Empty(service.InvalidatedNodeIds);
    }
    [Fact]
    public void Removed_connection_only_clears_owned_default_never_explicit_selection()
    {
        var p = new ImageParameters { ImageSource = new() { SourceNodeId = "source", PropertyName = "image", AutomaticConnectionId = "edge", PropertyType = typeof(IVisionImage) } }; var node = Node("target", p);
        Assert.False(ConnectionBindingPolicy.RemoveAutomaticImageBinding(node, "other")); Assert.NotNull(p.ImageSource);
        Assert.True(ConnectionBindingPolicy.RemoveAutomaticImageBinding(node, "edge")); Assert.Null(p.ImageSource); Assert.Single(p.RebindingRequired);
        p.ImageSource = new() { SourceNodeId = "source", PropertyName = "image", PropertyType = typeof(IVisionImage) };
        Assert.False(ConnectionBindingPolicy.RemoveAutomaticImageBinding(node, "edge")); Assert.NotNull(p.ImageSource);
        var (_, service) = Setup(Node("source", new ImageOutputParameters()), node); service.Commit("target"); Assert.Empty(p.RebindingRequired);
    }
    [Fact]
    public void Explicit_image_binding_still_generates_dependency_without_canvas_edge()
    {
        var p = new ImageParameters { ImageSource = new() { SourceNodeId = "source", PropertyName = "image" } };
        var (solution, _) = Setup(Node("source", new ImageOutputParameters()), Node("target", p));
        var graph = WorkflowExecutionDependencyGraph.Compile(solution.Workflows[0]); Assert.Contains("source", graph.GetPredecessors("target"));
    }
    [Fact]
    public void Parser_and_assembler_bindings_generate_dependencies_without_canvas_edges()
    {
        var id = Guid.NewGuid().ToString(); var parseId = Guid.NewGuid().ToString(); var source = new ReceiveDataParameters();
        var parse = new ProtocolParseParameters { InputText = id + "." + source.OutputDefinition().BindingKey };
        var assembly = new SunEyeVision.Tool.ProtocolAssemble.ProtocolAssembleParameters(); assembly.Parts[0].InputText = parseId + "." + parse.Outputs[0].OutputDefinition().BindingKey;
        var (solution, _) = Setup(Node(id, source), Node(parseId, parse), Node("assembly", assembly));
        var graph = WorkflowExecutionDependencyGraph.Compile(solution.Workflows[0]);
        Assert.Contains(id, graph.GetPredecessors(parseId)); Assert.Contains(parseId, graph.GetPredecessors("assembly"));
    }
    [Fact]
    public void Unavailable_contract_does_not_clear_reference_or_allow_pending_input()
    {
        var p = new SendDataParameters(); p.Rows[0].InputText = "source.unknown"; p.RebindingRequired["Row:" + p.Rows[0].FieldId] = "需重新配置";
        var (_, service) = Setup(Node("source", new GenericToolParameters()), Node("send", p)); service.Commit("send");
        Assert.NotEmpty(p.Rows[0].InputText); Assert.Single(p.RebindingRequired);
    }
    [Fact]
    public void B2_confirmed_shape_filter_on_string_is_incompatible_not_unknown()
    {
        var source = new ReceiveDataParameters { OutputType = "String" }; var target = new NumericParameters(); var node = Node("target", target);
        var path = source.OutputDefinition().BindingKey + "[Type=Circle][].Geometry.Center.X";
        node.ParamSettings.SetSetting(ParamSetting.CreateBinding("Number", "upstream", path));
        var (_, service) = Setup(Node("upstream", source), node);
        service.Commit("target"); Assert.Empty(node.ParamSettings.Settings); Assert.Single(target.RebindingRequired);
    }
}

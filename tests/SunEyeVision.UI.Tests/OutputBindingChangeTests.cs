using System.Text.Json;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Tool.ReceiveData;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Tool.ProtocolAssemble;
using SunEyeVision.Tool.SendData;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class OutputBindingChangeTests
{
    private static WorkflowNodeBase Node(string id, ToolParameters p, string? name = null) => new(id, name ?? id, name ?? id, "Test") { Parameters = p };
    private static (Solution, OutputBindingChangeService) Setup(params WorkflowNodeBase[] nodes)
    {
        var flow = new SunEyeVision.Workflow.Workflow("flow", "流程");
        foreach (var n in nodes) flow.Nodes.Add(n);
        var solution = new Solution { Workflows = new() { flow } };
        var service = new OutputBindingChangeService(); service.Reset(solution); return (solution, service);
    }
    private static string Expression(ReceiveDataParameters p) => "source." + p.OutputDefinition().BindingKey;

    [Fact]
    public void Primary_identity_survives_mode_switch_and_save_reload()
    {
        var p = new ReceiveDataParameters(); var id = p.FieldId;
        p.SourceKind = "Variable"; Assert.Equal(id, p.Variables[0].FieldId);
        p.Variables.Add(new() { Name = "out1" }); p.Variables.Move(0, 1); Assert.Equal(id, p.FieldId);
        var copy = JsonSerializer.Deserialize<ReceiveDataParameters>(JsonSerializer.Serialize(p))!;
        Assert.Equal(id, copy.FieldId); copy.SourceKind = "Device"; Assert.Equal(id, copy.OutputDefinition().FieldId);
    }

    [Fact]
    public void Incompatible_change_clears_all_consumers_across_flows_not_unrelated_same_named_node()
    {
        var source = new ReceiveDataParameters(); var parser = new ProtocolParseParameters { InputText = Expression(source) };
        var sender = new SendDataParameters(); sender.Rows[0].DataType = "Binary"; sender.Rows[0].InputText = Expression(source);
        var unrelated = Node("other", new ProtocolParseParameters { InputText = "constant" }, "同名"); unrelated.LastResult = new object();
        var (solution, service) = Setup(Node("source", source), Node("parse", parser, "同名"), unrelated);
        var flow = new SunEyeVision.Workflow.Workflow("flow2", "另一流程"); flow.Nodes.Add(Node("send", sender)); solution.Workflows.Add(flow); service.Reset(solution);
        source.SourceKind = "Variable"; source.Variables[0].DataType = "String";
        Assert.Equal(2, service.Commit("source").Count);
        Assert.Empty(parser.InputText); Assert.Empty(sender.Rows[0].InputText); Assert.NotNull(unrelated.LastResult);
        Assert.Contains("InputText", parser.RebindingRequired.Keys); Assert.Single(sender.RebindingRequired);
    }

    [Fact]
    public void Rename_and_compatible_numeric_assembly_change_preserve_reference()
    {
        var source = new ReceiveDataParameters { OutputType = "Integer" }; var assembly = new ProtocolAssembleParameters();
        assembly.Parts[0].InputText = Expression(source); var original = assembly.Parts[0].InputText;
        var (_, service) = Setup(Node("source", source), Node("assembly", assembly));
        source.OutputName = "新名字"; Assert.Empty(service.Commit("source"));
        source.OutputType = "Double"; Assert.Empty(service.Commit("source"));
        Assert.Equal(original, assembly.Parts[0].InputText);
    }

    [Fact]
    public void Deletion_and_same_name_recreation_never_reconnect()
    {
        var source = new ReceiveDataParameters(); var p = new ProtocolParseParameters { InputText = Expression(source) };
        var (solution, service) = Setup(Node("source", source), Node("parse", p));
        solution.Workflows[0].Nodes.RemoveAt(0); Assert.Single(service.Commit("source"));
        solution.Workflows[0].Nodes.Add(Node("source", new ReceiveDataParameters()));
        Assert.Empty(service.Commit("source")); Assert.Empty(p.InputText); Assert.Single(p.RebindingRequired);
    }

    [Fact]
    public void Ordinary_binding_is_removed_without_changing_initial_parameter()
    {
        var source = new ReceiveDataParameters(); var consumer = Node("ordinary", new GenericToolParameters());
        consumer.ParamSettings.SetSetting(ParamSetting.CreateBinding("InputText", "source", source.OutputDefinition().BindingKey));
        var (solution, service) = Setup(Node("source", source), consumer);
        solution.Workflows[0].Nodes.RemoveAt(0); Assert.Single(service.Commit("source"));
        Assert.Empty(consumer.ParamSettings.GetDynamicSettings()); Assert.Contains("InputText", consumer.Parameters.RebindingRequired.Keys);
    }

    [Fact]
    public void Removed_producer_clears_global_receive_and_consumer_mapping_and_blocks_initial_fallback()
    {
        var source = new ReceiveDataParameters { OutputType = "String" };
        var receive = new ReceiveDataParameters { SourceKind = "Variable" }; receive.Variables[0].VariableId = "v"; receive.Variables[0].VariablePath = "全局.v";
        var p = new ProtocolParseParameters { InputText = "__GlobalVars__.v" };
        var (solution, service) = Setup(Node("source", source), Node("receive", receive), Node("parse", p));
        var variable = new GlobalVariable { Id = "v", Name = "v", Type = "String", Value = "INITIAL",
            ProducerBinding = new() { WorkflowId = "flow", NodeId = "source", ResultName = source.OutputDefinition().BindingKey },
            ConsumerBindings = new() { new() { WorkflowId = "flow", NodeId = "parse", ParameterName = "InputText" } } };
        solution.GlobalVariables.Add(variable); service.Reset(solution); solution.Workflows[0].Nodes.RemoveAt(0);
        service.Commit("source"); Assert.Null(variable.ProducerBinding); Assert.NotEmpty(variable.RebindingReason);
        Assert.Empty(variable.ConsumerBindings); Assert.Empty(receive.Variables[0].VariableId); Assert.Equal("请选择", receive.Variables[0].VariablePath); Assert.Empty(p.InputText);
        Assert.Throws<InvalidOperationException>(() => SolutionExecutionDependencyGraph.Compile(solution));
        var copy = JsonSerializer.Deserialize<GlobalVariable>(JsonSerializer.Serialize(variable))!; Assert.Equal(variable.RebindingReason, copy.RebindingReason);
        variable.ProducerBinding = new() { NodeId = "new" }; Assert.Empty(variable.RebindingReason);
    }

    [Fact]
    public void Rebinding_state_persists_and_snapshot_is_not_changed_and_preflight_rejects_empty_input()
    {
        var source = new ReceiveDataParameters(); var p = new ProtocolParseParameters { InputText = Expression(source) };
        var frozen = (ProtocolParseParameters)ToolParameterSnapshot.Create(p);
        var (solution, service) = Setup(Node("source", source), Node("parse", p)); solution.Workflows[0].Nodes.RemoveAt(0); service.Commit("source");
        Assert.NotEmpty(frozen.InputText); Assert.Empty(frozen.RebindingRequired);
        var copy = JsonSerializer.Deserialize<ProtocolParseParameters>(JsonSerializer.Serialize(p))!;
        var executor = new ToolExecutor(new ParameterResolver()); var request = new ToolExecutionRequest(copy);
        var invalid = executor.ResolveRuntimeParameters(request); Assert.Throws<InvalidOperationException>(() => ToolExecutor.ApplyRuntimeSnapshot(copy, invalid));
        copy.InputText = "explicit new constant"; Assert.False(executor.ResolveRuntimeParameters(new(copy)).IsValid);
        solution.Workflows[0].Nodes[0].Parameters = copy; service.Commit("parse");
        ToolExecutor.ApplyRuntimeSnapshot(copy, executor.ResolveRuntimeParameters(new(copy)));
    }

    private sealed class FailingParameters : ToolParameters, IBindingInputProvider
    {
        public string Text = "";
        public IReadOnlyList<BindingInputReference> GetBindingInputs() => new[] { new BindingInputReference("test", "test", () => Text,
            value => { if (value == "") throw new InvalidOperationException("test rejection"); Text = value; }, new[] { typeof(string) }) };
    }
    [Fact]
    public void Consumer_transaction_rolls_back_when_write_fails()
    {
        var source = new ReceiveDataParameters(); var p = new ProtocolParseParameters { InputText = Expression(source) }; var fail = new FailingParameters { Text = Expression(source) };
        var (solution, service) = Setup(Node("source", source), Node("parse", p), Node("fail", fail)); solution.Workflows[0].Nodes.RemoveAt(0);
        Assert.Throws<InvalidOperationException>(() => service.Commit("source")); Assert.Equal(Expression(source), p.InputText); Assert.Empty(p.RebindingRequired);
    }

    [Fact]
    public void Dependent_results_are_invalidated_without_clearing_compatible_descendant_binding()
    {
        var source = new ReceiveDataParameters(); var p = new ProtocolParseParameters { InputText = Expression(source) };
        var descendant = new ProtocolAssembleParameters(); descendant.Parts[0].InputText = "parse." + p.Outputs[0].OutputDefinition().BindingKey;
        var third = Node("third", descendant); third.LastResult = new object();
        var (_, service) = Setup(Node("source", source), Node("parse", p), third);
        source.OutputType = "String"; service.Commit("source");
        Assert.Contains("third", service.InvalidatedNodeIds); Assert.Null(third.LastResult); Assert.NotEmpty(descendant.Parts[0].InputText);
    }
}

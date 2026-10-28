using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class GlobalProducerBindingRulesTests
{
    private sealed class Parameters : ToolParameters,IOutputContractProvider
    {
        internal IReadOnlyList<OutputVariable> Outputs=Array.Empty<OutputVariable>();
        internal Action? OnRead;
        public IReadOnlyList<OutputVariable> GetOutputContract(){OnRead?.Invoke();return Outputs;}
    }
    private static OutputVariable Output(string id,TypeRef type)=>new()
        {PortId=id,Name="显示名称不能寻址",DisplayName="结果显示",ValueDeclaration=type,Kind=OutputVariableKind.Data};
    private static (Solution Solution,Parameters Parameters,GlobalVariable Variable,ProducerBinding Producer) Setup(
        TypeRef source,string vocabulary="Integer",IEnumerable<string>? members=null,string portId="output.width")
    {
        var parameters=new Parameters{Outputs=new[]{Output(portId,source)}};
        var flow=new SunEyeVision.Workflow.Workflow("flow","流程");
        flow.Nodes.Add(new WorkflowNodeBase("source","生产节点","生产节点","Test"){Parameters=parameters});
        var producer=new ProducerBinding {ReferenceExpression=BindingReferenceSerializer.Serialize(
            new OutputReference("source",portId,source,members,scopeId:flow.Id)),WorkflowName="流程",NodeName="生产节点",ResultDisplayName="结果显示"};
        var variable=new GlobalVariable{Id="variable",Name="变量",Type=vocabulary,ProducerBinding=producer};
        return(new Solution{Workflows=new(){flow},GlobalVariables=new(){variable}},parameters,variable,producer);
    }
    [Theory]
    [InlineData("Integer",true)] [InlineData("Double",true)] [InlineData("String",true)] [InlineData("Boolean",true)]
    [InlineData("Integer[]",true)] [InlineData("Double[]",true)] [InlineData("String[]",true)]
    [InlineData("Image",false)] [InlineData("ROI",false)] [InlineData("Transform",false)]
    [InlineData("Binary",false)] [InlineData("not-declared",false)]
    public void Target_storage_is_declared_pure_value_only(string vocabulary,bool expected)
    {
        Assert.Equal(expected,GlobalProducerBindingRules.TryDescribeTarget(vocabulary,out var target,out var clr,out var reason));
        if(expected){Assert.NotNull(clr);Assert.Equal("value",target.Id);Assert.Empty(reason);}
        else{Assert.Null(target);Assert.Null(clr);Assert.NotEmpty(reason);}
    }
    [Fact]
    public void Opaque_port_identity_and_display_rename_do_not_rewrite_the_formal_reference()
    {
        var f=Setup(TypeRef.ScalarOf(ScalarKind.Int64),portId:"output.width-pixels");
        var expression=f.Producer.ReferenceExpression;
        var service=new OutputBindingChangeService();service.Reset(f.Solution);
        f.Parameters.Outputs[0].Name="另一显示名称";f.Parameters.Outputs[0].DisplayName="重命名";
        Assert.Empty(service.ReconcileAll());Assert.Same(f.Producer,f.Variable.ProducerBinding);
        Assert.Equal(expression,f.Producer.ReferenceExpression);
        Assert.Equal(BindingCompatibility.Compatible,GlobalProducerBindingRules.Validate(f.Solution,f.Producer,f.Variable).State);
    }
    [Fact]
    public void Record_leaf_and_sequence_All_projection_use_declared_member_ids()
    {
        var record=TypeRef.RecordOf("record",new[]{new Port("width.pixels",TypeRef.ScalarOf(ScalarKind.Int64))});
        var scalar=Setup(record,members:new[]{"width.pixels"});
        Assert.Equal(BindingCompatibility.Compatible,GlobalProducerBindingRules.Validate(scalar.Solution,scalar.Producer,scalar.Variable).State);
        var sequence=Setup(TypeRef.SequenceOf(record),"Integer[]",new[]{"width.pixels"});
        Assert.Equal(BindingCompatibility.Conditional,GlobalProducerBindingRules.Validate(sequence.Solution,sequence.Producer,sequence.Variable).State);
        // Int64 -> physical Int32[] is conditional; it is not silently narrowed at edit time.
        Assert.IsType<BindingSelection.AllItems>(sequence.Producer.SourceReference.Selection);
    }
    [Theory]
    [InlineData("scope")] [InlineData("node")] [InlineData("port")] [InlineData("member")] [InlineData("type")]
    public void Removed_or_changed_identity_is_incompatible_without_same_name_fallback(string change)
    {
        var record=TypeRef.RecordOf("r",new[]{new Port("width",TypeRef.ScalarOf(ScalarKind.Int64))});
        var f=Setup(record,members:new[]{"width"});
        switch(change)
        {
            case "scope":f.Solution.Workflows.Clear();break;
            case "node":f.Solution.Workflows[0].Nodes.Clear();break;
            case "port":f.Parameters.Outputs=new[]{Output("other.port",record)};break;
            case "member":f.Parameters.Outputs=new[]{Output("output.width",TypeRef.RecordOf("r",new[]{new Port("other",TypeRef.ScalarOf(ScalarKind.Int64))}))};break;
            case "type":f.Parameters.Outputs=new[]{Output("output.width",TypeRef.ScalarOf(ScalarKind.Text))};break;
        }
        Assert.Equal(BindingCompatibility.Incompatible,GlobalProducerBindingRules.Validate(f.Solution,f.Producer,f.Variable).State);
    }
    [Fact]
    public void Explicit_index_and_unscoped_sources_are_not_silently_reinterpreted_as_All()
    {
        var f=Setup(TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64)));
        foreach(var reference in new[]{
            new OutputReference("source","output.width",f.Producer.SourceReference.Type,selection:new BindingSelection.AtConstant(0),scopeId:"flow"),
            new OutputReference("source","output.width",f.Producer.SourceReference.Type)})
        {
            f.Producer.ReferenceExpression=BindingReferenceSerializer.Serialize(reference);
            Assert.Equal(BindingCompatibility.Incompatible,GlobalProducerBindingRules.Validate(f.Solution,f.Producer,f.Variable).State);
        }
    }
    [Fact]
    public void Malformed_reference_and_unsupported_target_fail_without_mutating_configuration()
    {
        var f=Setup(TypeRef.ResourceOf(ResourceKind.Image),"Image");
        Assert.Equal(BindingCompatibility.Incompatible,GlobalProducerBindingRules.Validate(f.Solution,f.Producer,f.Variable).State);
        f.Producer.ReferenceExpression="{invalid";
        Assert.Equal(BindingCompatibility.Incompatible,GlobalProducerBindingRules.Validate(f.Solution,f.Producer,f.Variable).State);
        Assert.Same(f.Producer,f.Variable.ProducerBinding);Assert.Empty(f.Variable.RebindingReason);
    }
    [Theory]
    [InlineData("node")] [InlineData("reference")] [InlineData("target")]
    public void Provider_reentry_cannot_accept_a_retired_source_or_changed_target(string change)
    {
        var f=Setup(TypeRef.ScalarOf(ScalarKind.Int64));
        f.Parameters.OnRead=()=>
        {
            switch(change)
            {
                case "node":f.Solution.Workflows[0].Nodes.Clear();break;
                case "reference":f.Producer.ReferenceExpression=BindingReferenceSerializer.Serialize(
                    new OutputReference("another","output.width",TypeRef.ScalarOf(ScalarKind.Int64),scopeId:"flow"));break;
                case "target":f.Variable.Type="String";break;
            }
        };
        Assert.Equal(BindingCompatibility.Incompatible,GlobalProducerBindingRules.Validate(f.Solution,f.Producer,f.Variable).State);
    }
    [Fact]
    public void Nullable_optional_member_and_unknown_count_are_conditional_not_unconditionally_ready()
    {
        var record=TypeRef.RecordOf("r",new[]{new Port("width",TypeRef.ScalarOf(ScalarKind.Int64),Required:false,Nullable:true)});
        var f=Setup(record,members:new[]{"width"});
        Assert.Equal(BindingCompatibility.Conditional,GlobalProducerBindingRules.Validate(f.Solution,f.Producer,f.Variable).State);
        var sequence=Setup(TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64)));
        Assert.Equal(BindingCompatibility.Conditional,GlobalProducerBindingRules.Validate(sequence.Solution,sequence.Producer,sequence.Variable).State);
    }
    [Fact]
    public void Reconciliation_reuses_the_same_captured_host_contract_instead_of_reading_a_second_provider()
    {
        var f=Setup(TypeRef.ScalarOf(ScalarKind.Int64));
        // This source's formal catalog belongs to the host, not to a parameter provider.
        f.Solution.Workflows[0].Nodes[0].Parameters=new GenericToolParameters();
        var service=new OutputBindingChangeService();service.Reset(f.Solution);
        service.StaticContractReader=_=>f.Parameters.Outputs;
        service.AcceptHistoryConfiguration(f.Solution);
        Assert.Empty(service.ReconcileAll());Assert.Same(f.Producer,f.Variable.ProducerBinding);
        Assert.Empty(f.Variable.BindingValidationPending);Assert.Empty(f.Variable.RebindingReason);
    }
    [Fact]
    public void Physical_numeric_storage_change_is_not_a_display_rename()
    {
        var f=Setup(TypeRef.ScalarOf(ScalarKind.Int64));
        f.Parameters.Outputs[0].DeclaredType=typeof(int);
        Assert.Equal(BindingCompatibility.Incompatible,GlobalProducerBindingRules.Validate(f.Solution,f.Producer,f.Variable).State);
    }
    [Fact]
    public void Reconciliation_invalidates_removed_member_and_clears_old_consumers_without_rebinding_same_label()
    {
        var record=TypeRef.RecordOf("r",new[]{new Port("width",TypeRef.ScalarOf(ScalarKind.Int64))});
        var f=Setup(record,members:new[]{"width"});
        f.Variable.ConsumerBindings.Add(new(){WorkflowId="missing",NodeId="consumer",ParameterName="Input"});
        var service=new OutputBindingChangeService();service.Reset(f.Solution);
        f.Parameters.Outputs=new[]{Output("output.width",TypeRef.RecordOf("r",new[]{new Port("height",TypeRef.ScalarOf(ScalarKind.Int64))}))};
        service.ReconcileAll();
        Assert.Null(f.Variable.ProducerBinding);Assert.NotEmpty(f.Variable.RebindingReason);Assert.Empty(f.Variable.ConsumerBindings);
    }
}

using System.Collections.Concurrent;
using NSubstitute;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class BindingWorkflowExecutionTests
{
    [Theory]
    [InlineData(17d,false,false)]
    [InlineData(17d,true,true)]
    [InlineData(1e20,true,false)]
    public async Task Actual_integer_property_requires_explicit_checked_conversion(double value,bool explicitConversion,bool expected)
    {
        using var fixture=new Fixture();var target=fixture.Add("target",false,777);
        var parameters=(ProbeParameters)target.Parameters;parameters.UseInt=true;parameters.IntInput=777;
        fixture.Add("source",true,value);fixture.Add("independent",true,9);
        var reference=new SunEyeVision.Plugin.Contracts.OutputReference("source","field:7784e373d39b4375b2f7d911e42e9181",
            (SunEyeVision.Plugin.Contracts.TypeRef)SunEyeVision.Plugin.Contracts.DataType.Number,scopeId:fixture.Flow.Id);
        target.ParamSettings.SetSetting(new ParamSetting{ParameterName="IntInput",BindingType=BindingType.Binding,
            ReferenceExpression=SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(reference),
            Conversion=explicitConversion?SunEyeVision.Plugin.Contracts.BindingConversion.Float64ToInt64Checked:SunEyeVision.Plugin.Contracts.BindingConversion.Automatic});
        using var result=await fixture.Run();
        Assert.Equal(expected,result.Success);Assert.Contains("independent",fixture.Trace);
        if(expected)Assert.Equal(17,((ProbeResults)result.NodeResults["target"].Result!).Value);
        else Assert.DoesNotContain("target",fixture.Trace);
        Assert.Equal(777,parameters.IntInput);
    }

    [Fact]
    public async Task Typed_dynamic_index_orders_both_sources_without_canvas_edges()
    {
        using var fixture=new Fixture();var target=fixture.Add("target",false,777);
        fixture.Add("array",true,23);fixture.Add("index",true,1);
        var index=new SunEyeVision.Plugin.Contracts.OutputReference("index","name:Index",
            SunEyeVision.Plugin.Contracts.TypeRef.ScalarOf(SunEyeVision.Plugin.Contracts.ScalarKind.Int64),scopeId:fixture.Flow.Id,
            storage:SunEyeVision.Plugin.Contracts.NumericStorage.Int32);
        var reference=new SunEyeVision.Plugin.Contracts.OutputReference("array","name:Values",
            SunEyeVision.Plugin.Contracts.TypeRef.SequenceOf((SunEyeVision.Plugin.Contracts.TypeRef)SunEyeVision.Plugin.Contracts.DataType.Number,
                SunEyeVision.Plugin.Contracts.CountConstraint.Exactly(2)),
            selection:new SunEyeVision.Plugin.Contracts.BindingSelection.AtField(index),scopeId:fixture.Flow.Id);
        target.ParamSettings.SetSetting(new ParamSetting{ParameterName="Input",BindingType=BindingType.Binding,
            ReferenceExpression=SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(reference)});
        using var result=await fixture.Run();
        Assert.True(result.Success,string.Join(";",result.Errors));
        Assert.Equal(24,((ProbeResults)result.NodeResults["target"].Result!).Value);
        Assert.Equal("target",fixture.Trace.Last());Assert.Empty(fixture.Flow.Connections);
        Assert.Equal(777,((ProbeParameters)target.Parameters).Input);
    }

    [Fact]
    public async Task Typed_reference_uses_actual_admitted_source_and_preserves_distinct_request_identity()
    {
        using var fixture=new Fixture();
        var target=fixture.Add("target",false,777);fixture.Add("source",true,17);
        var reference=new SunEyeVision.Plugin.Contracts.OutputReference("source","field:7784e373d39b4375b2f7d911e42e9181",
            (SunEyeVision.Plugin.Contracts.TypeRef)SunEyeVision.Plugin.Contracts.DataType.Number,scopeId:fixture.Flow.Id);
        target.ParamSettings.SetSetting(new ParamSetting{ParameterName="Input",BindingType=BindingType.Binding,
            ReferenceExpression=SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(reference)});
        using var result=await fixture.Run();
        Assert.True(result.Success,string.Join(";",result.Errors));
        Assert.Equal(17,((ProbeResults)result.NodeResults["target"].Result!).Value);
        var sourceIdentity=result.NodeResults["source"].Result!.ExecutionIdentity!;
        var targetIdentity=result.NodeResults["target"].Result!.ExecutionIdentity!;
        Assert.NotEqual(sourceIdentity.RequestId,targetIdentity.RequestId);
        Assert.Equal(sourceIdentity.RootRunId,targetIdentity.RootRunId);
        Assert.NotNull(result.NodeResults["source"].Result!.BindingOutputSchema);
        Assert.Equal(777,((ProbeParameters)target.Parameters).Input);
    }

    [Fact]
    public async Task Invalid_typed_reference_never_falls_back_to_valid_legacy_names()
    {
        using var fixture=new Fixture();var target=fixture.Add("target",false,777);fixture.Add("source",true,17);
        var setting=ParamSetting.CreateBinding("Input","source","Value");setting.ReferenceExpression="{}";
        target.ParamSettings.SetSetting(setting);
        using var result=await fixture.Run();
        Assert.False(result.Success);Assert.DoesNotContain("target",fixture.Trace);
        Assert.Contains("source",fixture.Trace);
    }

    private const string ProbeId="BindingWorkflowExecutionProbe";
    private const string MissingSchemaId="BindingWorkflowMissingSchemaProbe";
    private static readonly ConcurrentDictionary<string,ConcurrentQueue<string>> Traces=new();

    [Fact]
    public async Task No_canvas_edge_orders_actual_source_before_consumer_and_next_run_reads_new_value()
    {
        using var fixture=new Fixture();
        var target=fixture.Add("target",false,777);var source=fixture.Add("source",true,5);
        target.ParamSettings.SetSetting(ParamSetting.CreateBinding("Input","source","Value"));
        using(var result=await fixture.Run())
        {Assert.True(result.Success,string.Join(";",result.Errors));Assert.Equal(5,((ProbeResults)result.NodeResults["target"].Result!).Value);}
        Assert.Equal(new[]{"source","target"},fixture.Trace.ToArray());
        ((ProbeParameters)source.Parameters).Value=9;fixture.Trace.Clear();
        using(var result=await fixture.Run())
        {Assert.True(result.Success,string.Join(";",result.Errors));Assert.Equal(9,((ProbeResults)result.NodeResults["target"].Result!).Value);}
        Assert.Equal(new[]{"source","target"},fixture.Trace.ToArray());
        Assert.Equal(777,((ProbeParameters)target.Parameters).Input);
        Assert.Empty(fixture.Flow.Connections);
    }

    [Fact]
    public async Task Canvas_and_binding_cycle_is_rejected_before_tools_run_without_mutating_design()
    {
        using var fixture=new Fixture();var target=fixture.Add("target",false,777);fixture.Add("source",true,5);
        target.ParamSettings.SetSetting(ParamSetting.CreateBinding("Input","source","Value"));
        fixture.Flow.Connections.Add(new WorkflowConnection("back","target","source"));
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>fixture.Run());
        Assert.Contains("循环",error.Message);Assert.Empty(fixture.Trace);
        Assert.Equal(WorkflowExecutionState.Idle,fixture.Runner.CurrentState);
        Assert.Single(target.ParamSettings.Settings);Assert.Single(fixture.Flow.Connections);
        Assert.Equal(777,((ProbeParameters)target.Parameters).Input);
    }

    [Fact]
    public async Task Failed_source_cannot_supply_a_value_or_reuse_an_earlier_success()
    {
        using var fixture=new Fixture();var target=fixture.Add("target",false,777);var source=fixture.Add("source",true,5);
        target.ParamSettings.SetSetting(ParamSetting.CreateBinding("Input","source","Value"));
        using(var first=await fixture.Run())Assert.True(first.Success,string.Join(";",first.Errors));
        ((ProbeParameters)source.Parameters).Fail=true;fixture.Trace.Clear();
        using var failed=await fixture.Run();Assert.False(failed.Success);
        Assert.DoesNotContain("target",fixture.Trace);Assert.Equal(777,((ProbeParameters)target.Parameters).Input);
    }

    [Fact]
    public async Task Missing_global_producer_blocks_consumer_despite_initial_value_and_leaves_unrelated_node_runnable()
    {
        using var fixture=new Fixture();
        fixture.Flow.AddNode(new WorkflowNodeBase("missing","missing","missing",MissingSchemaId) {Parameters=new MissingSchemaParameters()});
        var target=fixture.Add("target",false,777);fixture.Add("independent",true,99);
        target.ParamSettings.SetSetting(ParamSetting.CreateBinding("Input","__GlobalVars__","v"));
        fixture.Solution.GlobalVariables.Add(new GlobalVariable {Id="v-id",Name="v",Type="Double",Value=999d,
            ProducerBinding=new(){WorkflowId=fixture.Flow.Id,NodeId="missing",ResultName="Value"}});
        var service=new OutputBindingChangeService();service.Reset(fixture.Solution);service.ReconcileAll();
        Assert.NotEmpty(target.Parameters.BindingValidationPending);
        using var result=await fixture.Run(new Dictionary<string,object?>{{"v",999d}});
        Assert.False(result.Success);Assert.DoesNotContain("target",fixture.Trace);Assert.Contains("independent",fixture.Trace);
        Assert.Equal(99,((ProbeResults)result.NodeResults["independent"].Result!).Value);
        Assert.Equal(777,((ProbeParameters)target.Parameters).Input);
    }

    [Fact]
    public async Task Explicit_foreign_scope_with_same_node_id_does_not_resolve_to_local_result()
    {
        using var fixture=new Fixture();fixture.Add("source",true,11);var target=fixture.Add("target",false,777);
        var foreign=fixture.Catalog.CreateWorkflow("foreign-"+Guid.NewGuid().ToString("N"),"foreign");
        foreign.AddNode(new WorkflowNodeBase("source","source","source",ProbeId){Parameters=new ProbeParameters{CaseId=fixture.CaseId,Source=true,Value=88}});
        var binding=ParamSetting.CreateBinding("Input","source","Value");binding.SourceWorkflowId=foreign.Id;target.ParamSettings.SetSetting(binding);
        using var result=await fixture.Run();
        Assert.False(result.Success);Assert.DoesNotContain("target",fixture.Trace);
        Assert.Equal(11,((ProbeResults)result.NodeResults["source"].Result!).Value);
        Assert.Equal(777,((ProbeParameters)target.Parameters).Input);
    }

    private sealed class Fixture:IDisposable
    {
        public Fixture()
        {
            CaseId=Guid.NewGuid().ToString("N");Trace=new();Traces[CaseId]=Trace;ToolRegistry.RegisterTool(typeof(ProbeTool));ToolRegistry.RegisterTool(typeof(MissingSchemaTool));
            var logger=Substitute.For<ILogger>();Catalog=new(logger);Flow=Catalog.CreateWorkflow("binding-"+CaseId,"binding");
            Solution=new();Solution.Workflows.Add(Flow);Runner=new(Catalog,Substitute.For<IPluginManager>(),logger);
        }
        public string CaseId {get;} public ConcurrentQueue<string> Trace {get;} public WorkflowEngine Catalog {get;}
        public SunEyeVision.Workflow.Workflow Flow {get;} public Solution Solution {get;} public WorkflowExecutionEngine Runner {get;}
        public WorkflowNodeBase Add(string id,bool source,double value)
        {var node=new WorkflowNodeBase(id,id,id,ProbeId){Parameters=new ProbeParameters{CaseId=CaseId,Label=id,Source=source,Value=value,Input=value}};Flow.AddNode(node);return node;}
        public Task<ExecutionResult> Run(IDictionary<string,object?>? globals=null)
            =>Runner.ExecuteWorkflowAsync(new WorkflowRunRequest(Flow.Id,globalVariables:globals){ExecutionOwner=Solution.ExecutionOwner}).WaitAsync(TimeSpan.FromSeconds(15));
        public void Dispose(){Traces.TryRemove(CaseId,out _);ToolRegistry.UnregisterTool(ProbeId);ToolRegistry.UnregisterTool(MissingSchemaId);}
    }
    public sealed class ProbeParameters:ToolParameters,IOutputContractProvider
    {
        public string CaseId {get;set;}="";public string Label {get;set;}="";public bool Source {get;set;}public bool Fail {get;set;}
        public double Input {get;set;}public double Value {get;set;}
        public int IntInput {get;set;}public bool UseInt {get;set;}
        public IReadOnlyList<OutputVariable> GetOutputContract()=>new[]{
            new OutputVariable{Name="Value",VariableType="Double",DeclaredType=typeof(double),IsCollection=false,FixedCount=1,FieldId=Guid.Parse("7784E373-D39B-4375-B2F7-D911E42E9181")},
            new OutputVariable{Name="Values",VariableType="Double",DeclaredType=typeof(double[]),IsCollection=true,FixedCount=2},
            new OutputVariable{Name="Index",VariableType="Int32",DeclaredType=typeof(int),IsCollection=false,FixedCount=1}};
    }
    public sealed class ProbeResults:ToolResults
    {public double Value {get;set;}public double[] Values {get;set;}=Array.Empty<double>();public int Index {get;set;}
        public override List<PropertyMapEntry> GetPropertyMap()=>new(){new("Value","Value"),new("Values","Values"),new("Index","Index")};}
    [Tool(id:ProbeId,displayName:"Binding workflow execution probe")]
    public sealed class ProbeTool:IToolPlugin<ProbeParameters,ProbeResults>
    {
        public ProbeResults Run(ProbeParameters parameters)
        {
            Traces[parameters.CaseId].Enqueue(parameters.Label);
            var result=new ProbeResults{Value=parameters.Source?parameters.Value:parameters.UseInt?parameters.IntInput:parameters.Input,Values=new[]{parameters.Value,parameters.Value+1},Index=parameters.Value>=int.MinValue&&parameters.Value<=int.MaxValue?(int)parameters.Value:0};
            if(parameters.Fail)result.SetError("controlled source failure");else result.SetSuccess();return result;
        }
    }
    public sealed class MissingSchemaParameters:ToolParameters,IOutputContractProvider
    {
        public IReadOnlyList<OutputVariable> GetOutputContract()=>new[]{new OutputVariable{Name="Value",DeclaredType=typeof(object),VariableType="",IsCollection=false,FixedCount=1}};
    }
    public sealed class MissingSchemaResults:ToolResults
    {public object Value {get;set;}=123d;public override List<PropertyMapEntry> GetPropertyMap()=>new(){new("Value","Value")};}
    [Tool(id:MissingSchemaId,displayName:"Binding incomplete schema probe")]
    public sealed class MissingSchemaTool:IToolPlugin<MissingSchemaParameters,MissingSchemaResults>
    {public MissingSchemaResults Run(MissingSchemaParameters parameters){var result=new MissingSchemaResults();result.SetSuccess();return result;}}
}

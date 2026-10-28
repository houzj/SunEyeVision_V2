using System.Collections.Concurrent;
using System.Reflection;
using NSubstitute;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class WorkflowGlobalProductionTests
{
    private const string ToolId="WorkflowGlobalProductionProbe";
    private static readonly ConcurrentDictionary<string,State> States=new();
    private sealed class State
    {
        internal ConcurrentQueue<string> Trace=new();
        internal Action? BeforeSourceReturns;
    }
    public sealed class Parameters:ToolParameters,IOutputContractProvider
    {
        public string CaseId{get;set;}="";public string Label{get;set;}="";
        public bool Source{get;set;}public bool Fail{get;set;}public double Value{get;set;}public double Input{get;set;}=777;
        public IReadOnlyList<OutputVariable> GetOutputContract()=>new[]{
            new OutputVariable{PortId="output.value",Name="Value",DeclaredType=typeof(double),VariableType="Double",IsCollection=false,Kind=OutputVariableKind.Data},
            new OutputVariable{PortId="output.values",Name="Values",DeclaredType=typeof(double[]),VariableType="Double",IsCollection=true,FixedCount=2,Kind=OutputVariableKind.Data}};
    }
    public sealed class Results:ToolResults
    {
        public double Value{get;set;}public double[] Values{get;set;}=Array.Empty<double>();
        public override List<PropertyMapEntry> GetPropertyMap()=>new(){new("Value","Value"),new("Values","Values")};
    }
    [Tool(ToolId,"Global production regression probe")]
    public sealed class Probe:IToolPlugin<Parameters,Results>
    {
        public Results Run(Parameters parameters)
        {
            var state=States[parameters.CaseId];state.Trace.Enqueue(parameters.Label);
            var result=new Results{Value=parameters.Source?parameters.Value:parameters.Input,Values=new[]{parameters.Value,parameters.Value+1}};
            if(parameters.Source)state.BeforeSourceReturns?.Invoke();
            if(parameters.Fail)result.SetError("controlled producer failure");else result.SetSuccess();return result;
        }
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly string Id=Guid.NewGuid().ToString("N");internal readonly State State=new();
        internal readonly WorkflowEngine Catalog;internal readonly WorkflowExecutionEngine Runner;internal readonly Solution Solution=new();
        internal readonly SunEyeVision.Workflow.Workflow SourceFlow;internal readonly GlobalVariable Variable;
        internal readonly WorkflowNodeBase Source;
        internal Fixture(bool array=false)
        {
            States[Id]=State;ToolRegistry.RegisterTool(typeof(Probe));
            var logger=Substitute.For<ILogger>();Catalog=new(logger);Runner=new(Catalog,Substitute.For<IPluginManager>(),logger);
            SourceFlow=Catalog.CreateWorkflow("producer-"+Id,"producer");Solution.Workflows.Add(SourceFlow);
            Source=Add(SourceFlow,"source",true,5);
            var port=BindingClrAdapter.DescribeOutput(((Parameters)Source.Parameters).GetOutputContract().Single(o=>o.Name==(array?"Values":"Value")));
            Variable=new GlobalVariable("shared",array?new double[]{999}:999d,array?"Double[]":"Double")
            {Id="variable-"+Id,ProducerBinding=BindingAdaptationB2Tests.ProducerFromPort(SourceFlow.Id,Source.Id,port)};
            Solution.GlobalVariables.Add(Variable);
        }
        internal WorkflowNodeBase Add(SunEyeVision.Workflow.Workflow flow,string id,bool source,double value)
        {
            var node=new WorkflowNodeBase(id,id,id,ToolId){Parameters=new Parameters{CaseId=Id,Label=id,Source=source,Value=value}};
            flow.Nodes.Add(node);return node;
        }
        internal WorkflowGlobalProducerOperation Operation()
        {
            Assert.True(GlobalProducerBindingRules.TryDescribeTarget(Variable.Type,out var target,out var clr,out var reason),reason);
            return new(Variable.Id,Variable.Name,Variable.ProducerBinding!.SourceReference,target,clr);
        }
        internal NodeConfigurationEditHost Host()=>new(Solution);
        internal WorkflowRunRequest Request(SunEyeVision.Workflow.Workflow flow,WorkflowGlobalRunValues values,
            WorkflowRunConfiguration? configuration=null)
        {
            configuration??=WorkflowRunConfiguration.Capture(Solution.Workflows,Host().Capture().Version);
            var receive=new WorkflowGlobalVariableReceiveService(Substitute.For<ICommunicationReceiveService>(),values,
                GlobalVariable.CaptureDefinitions(Solution.GlobalVariables.ToArray()));
            return new WorkflowRunRequest(flow.Id,globalVariables:new Dictionary<string,object?>(values.Capture()),communicationInputs:receive)
            {Configuration=configuration,ExecutionOwner=Solution.ExecutionOwner,GlobalProducers=new[]{Operation()},ProducedGlobals=values};
        }
        internal Task<ExecutionResult> Run(WorkflowRunRequest request,CancellationToken cancellation=default)
            =>Runner.ExecuteWorkflowAsync(request,cancellation).WaitAsync(TimeSpan.FromSeconds(20));
        public void Dispose(){States.TryRemove(Id,out _);ToolRegistry.UnregisterTool(ToolId);}
    }
    [Fact]
    public async Task Same_flow_producer_updates_global_before_dependent_node_is_dispatched()
    {
        using var f=new Fixture();var target=f.Add(f.SourceFlow,"target",false,0);f.SourceFlow.Nodes.Move(1,0);
        target.ParamSettings.SetSetting(ParamSetting.CreateBinding("Input","__GlobalVars__",f.Variable.Name));
        using var values=new WorkflowGlobalRunValues(Array.Empty<KeyValuePair<string,object?>>());
        using var run=await f.Run(f.Request(f.SourceFlow,values));
        Assert.True(run.Success,string.Join(";",run.Errors));Assert.Equal(new[]{"source","target"},f.State.Trace.ToArray());
        Assert.Equal(5,Assert.IsType<Results>(run.NodeResults["target"].Result).Value);
        Assert.Equal(5d,values.Capture()[f.Variable.Name]);Assert.Equal(999d,f.Variable.Value);
        Assert.Equal(0,f.Solution.ExecutionOwner.ActiveRuns);
    }
    [Fact]
    public async Task Subsequent_flow_reads_the_same_batch_value_after_source_scope_and_result_close()
    {
        using var f=new Fixture();var consumer=f.Catalog.CreateWorkflow("consumer-"+f.Id,"consumer");f.Solution.Workflows.Add(consumer);
        var target=f.Add(consumer,"target",false,0);target.ParamSettings.SetSetting(ParamSetting.CreateBinding("Input","__GlobalVars__",f.Variable.Name));
        using var values=new WorkflowGlobalRunValues(Array.Empty<KeyValuePair<string,object?>>());
        var configuration=WorkflowRunConfiguration.Capture(f.Solution.Workflows,f.Host().Capture().Version);
        using(var source=await f.Run(f.Request(f.SourceFlow,values,configuration)))Assert.True(source.Success,string.Join(";",source.Errors));
        using var next=await f.Run(f.Request(consumer,values,configuration));
        Assert.True(next.Success,string.Join(";",next.Errors));Assert.Equal(5,Assert.IsType<Results>(next.NodeResults["target"].Result).Value);
        Assert.Equal(999d,f.Variable.Value);Assert.Empty(values.Diagnostics);
    }
    [Fact]
    public async Task Failed_current_source_removes_previous_batch_value_and_never_uses_design_initial_value()
    {
        using var f=new Fixture();using var values=new WorkflowGlobalRunValues(Array.Empty<KeyValuePair<string,object?>>());
        using(var first=await f.Run(f.Request(f.SourceFlow,values)))Assert.True(first.Success);
        Assert.Equal(5d,values.Capture()[f.Variable.Name]);((Parameters)f.Source.Parameters).Fail=true;
        using var failed=await f.Run(f.Request(f.SourceFlow,values));Assert.False(failed.Success);
        Assert.False(values.Capture().ContainsKey(f.Variable.Name));Assert.Contains(f.Variable.Id,values.Diagnostics.Keys);
        Assert.Equal(999d,f.Variable.Value);
    }
    [Fact]
    public async Task Cancellation_before_source_completion_cannot_publish_a_value()
    {
        using var f=new Fixture();using var cancellation=new CancellationTokenSource();
        f.State.BeforeSourceReturns=()=>cancellation.Cancel();
        using var values=new WorkflowGlobalRunValues(Array.Empty<KeyValuePair<string,object?>>());
        using var run=await f.Run(f.Request(f.SourceFlow,values),cancellation.Token);
        Assert.Equal(ExecutionStatus.Cancelled,run.Status);Assert.Empty(values.Capture());
        Assert.Equal(999d,f.Variable.Value);Assert.Equal(0,f.Solution.ExecutionOwner.ActiveRuns);
    }
    [Fact]
    public async Task Published_array_is_independent_of_result_scope_and_each_captured_dictionary()
    {
        using var f=new Fixture(array:true);using var values=new WorkflowGlobalRunValues(Array.Empty<KeyValuePair<string,object?>>());
        using(var run=await f.Run(f.Request(f.SourceFlow,values)))
        {
            Assert.True(run.Success,string.Join(";",run.Errors));
            Assert.IsType<Results>(run.NodeResults["source"].Result).Values[0]=1234;
        }
        var first=Assert.IsType<double[]>(values.Capture()[f.Variable.Name]);Assert.Equal(new[]{5d,6d},first);first[0]=4321;
        Assert.Equal(new[]{5d,6d},Assert.IsType<double[]>(values.Capture()[f.Variable.Name]));
        values.Dispose();Assert.Throws<ObjectDisposedException>(()=>values.Capture());Assert.Equal(new[]{4321d,6d},first);
    }
    [Fact]
    public async Task Published_configuration_revision_and_settings_remain_the_captured_generation()
    {
        using var f=new Fixture();var host=f.Host();var captured=host.Capture();Assert.True(captured.Version>0);
        var configuration=WorkflowRunConfiguration.Capture(f.Solution.Workflows,captured.Version);
        var edit=host.BeginEdit(f.SourceFlow.Id,f.Source.Id);((Parameters)edit.Node.Parameters).Value=19;host.Commit(edit);
        Assert.True(host.Capture().Version>captured.Version);Assert.Equal(captured.Version,configuration.DocumentRevision);
        using var values=new WorkflowGlobalRunValues(Array.Empty<KeyValuePair<string,object?>>());
        using var run=await f.Run(f.Request(f.SourceFlow,values,configuration));Assert.True(run.Success,string.Join(";",run.Errors));
        Assert.Equal(5d,values.Capture()[f.Variable.Name]);Assert.Equal(19,((Parameters)f.Source.Parameters).Value);
    }
    [Fact]
    public async Task Direct_unversioned_capture_cannot_produce_globals()
    {
        using var f=new Fixture();using var values=new WorkflowGlobalRunValues(Array.Empty<KeyValuePair<string,object?>>());
        var direct=WorkflowRunConfiguration.Capture(f.Solution.Workflows);Assert.Equal(0,direct.DocumentRevision);
        using var run=await f.Run(f.Request(f.SourceFlow,values,direct));
        Assert.False(run.Success);Assert.Empty(values.Capture());
        Assert.Contains(run.Errors,error=>error.Contains("captured published configuration revision",StringComparison.Ordinal));
        Assert.Equal(0,f.Solution.ExecutionOwner.ActiveRuns);
    }
    [Fact]
    public async Task Previous_real_invocation_cannot_replace_the_current_admitted_producer()
    {
        using var f=new Fixture();using var oldValues=new WorkflowGlobalRunValues(Array.Empty<KeyValuePair<string,object?>>());
        using var oldRun=await f.Run(f.Request(f.SourceFlow,oldValues));Assert.True(oldRun.Success);
        var old=Assert.IsType<Results>(oldRun.NodeResults["source"].Result);
        using var reader=oldRun.AcquireDeclaredOutput(oldRun.DeclaredOutputs.Single().Identity);
        ((Parameters)f.Source.Parameters).Value=9;
        using var currentValues=new WorkflowGlobalRunValues(Array.Empty<KeyValuePair<string,object?>>());var injected=false;
        void Inject(WorkflowNodeBase node,NodeExecutionResult completed)
        {
            if(node.Id!="source")return;Assert.Equal(9d,currentValues.Capture()[f.Variable.Name]);
            var property=typeof(AlgorithmNode).GetProperty(nameof(AlgorithmNode.LastToolResult))!;
            var context=typeof(AlgorithmNode).GetProperty("BindingContext",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(node)!;
            var session=(WorkflowRunSession)context.GetType().GetField("_session",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(context)!;
            var current=property.GetValue(node);
            try
            {
                property.SetValue(node,old);
                context.GetType().GetMethod("CompleteGlobalProducers",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(context,new object[]{node.Id,session.RootContext!});
                injected=true;
            }
            finally{property.SetValue(node,current);}
        }
        f.Runner.NodeExecutionFinished+=Inject;
        try
        {
            using var run=await f.Run(f.Request(f.SourceFlow,currentValues));Assert.True(run.Success,string.Join(";",run.Errors));
            Assert.True(injected);Assert.False(currentValues.Capture().ContainsKey(f.Variable.Name));
            Assert.Equal("run-mismatch",currentValues.Diagnostics[f.Variable.Id].Code);
            Assert.Equal(9,Assert.IsType<Results>(run.NodeResults["source"].Result).Value);
            Assert.Equal(5d,reader.Values!.Get<double>("output.value"));
        }
        finally{f.Runner.NodeExecutionFinished-=Inject;}
    }
    [Fact]
    public async Task Sequence_variable_is_not_silently_flattened_by_single_item_communication()
    {
        using var f=new Fixture(array:true);using var values=new WorkflowGlobalRunValues(Array.Empty<KeyValuePair<string,object?>>());
        using var run=await f.Run(f.Request(f.SourceFlow,values));Assert.True(run.Success,string.Join(";",run.Errors));
        var receive=new WorkflowGlobalVariableReceiveService(Substitute.For<ICommunicationReceiveService>(),values,
            GlobalVariable.CaptureDefinitions(f.Solution.GlobalVariables));
        var error=Assert.Throws<NotSupportedException>(()=>receive.ReadVariables(new[]{f.Variable.Id}));
        Assert.Contains("不能隐式展平",error.Message);
        Assert.Equal(new[]{5d,6d},Assert.IsType<double[]>(values.Capture()[f.Variable.Name]));
    }
    [Fact]
    public void Runtime_value_slot_rejects_unknown_and_nested_resource_containers()
    {
        foreach(var unsupported in new object[]{new object(),new object[]{new object()},new double[1,1],new[]{new[]{1d}}})
            Assert.Throws<ArgumentException>(()=>new WorkflowGlobalRunValues(new Dictionary<string,object?>{{"value",unsupported}}));
    }
    [Fact]
    public void Resource_targets_are_rejected_before_any_producer_operation_can_be_admitted()
    {
        using var f=new Fixture();
        Assert.False(GlobalProducerBindingRules.TryDescribeTarget("Image",out _,out _,out var reason));Assert.NotEmpty(reason);
        Assert.Throws<ArgumentException>(()=>new WorkflowGlobalProducerOperation(f.Variable.Id,f.Variable.Name,
            f.Variable.ProducerBinding!.SourceReference,new Port("value",TypeRef.ResourceOf(ResourceKind.Image)),typeof(IReadOnlyImage)));
    }
}

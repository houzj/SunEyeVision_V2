using System.IO;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Tool.ReceiveData;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class GroupWorkflowExecutionTests
{
    [Fact]
    public async Task Real_engine_executes_nested_Groups_with_repeated_node_ids_and_scoped_field_outputs()
    {
        var host=typeof(GroupWorkflowExecutionTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var plugin=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(host,"plugins","SunEyeVision.Tool.Subroutine","SunEyeVision.Tool.Subroutine.dll"));
        ToolRegistry.RegisterTool(plugin.GetType("SunEyeVision.Tool.Subroutine.SubroutineTool",true)!);ToolRegistry.RegisterTool(typeof(ProtocolParseTool));
        var logger=VisionLogger.Instance;var catalog=new WorkflowEngine(logger);var id=Guid.NewGuid().ToString("N");
        var root=catalog.CreateWorkflow("root-"+id,"Root");var middle=catalog.CreateWorkflow("middle-"+id,"Middle");middle.IsSubroutine=true;var leaf=catalog.CreateWorkflow("leaf-"+id,"Leaf");leaf.IsSubroutine=true;
        var parse=new ProtocolParseParameters{InputText="7",Mode="Text"};parse.Outputs[0].DataType="float";var field=parse.Outputs[0].OutputDefinition();
        leaf.AddNode(new("producer","producer","producer","ProtocolParse"){Parameters=parse});
        AvailableDataSource Source(string scope,string node,string name,Guid fieldId)=>new(){SourceWorkflowId=scope,SourceNodeId=node,PropertyName=name,SourceFieldId=fieldId,PropertyType=typeof(double),VariableType="Double"};
        var inner=new SubroutineParameters{SubroutineId=leaf.Id};inner.Outputs.Add(new(){Name="Value",TypeName="Double",Source=Source(leaf.Id,"producer",field.BindingKey,field.FieldId),BindingSource="producer."+field.BindingKey});
        middle.AddNode(new("group","inner","inner","Subroutine"){SubroutineId=leaf.Id,Parameters=inner});
        var outer=new SubroutineParameters{SubroutineId=middle.Id};var source=Source(middle.Id,"group","Value",inner.Outputs[0].FieldId);
        outer.Outputs.Add(new(){Name="Final",TypeName="Double",Source=source,BindingSource="group.Value"});
        outer.DisplayConfigs.Add(new(){DisplayName="数值",DisplayType=SubroutineDisplayType.Text,TextStyle=new(){Name="数值"},BoundDataSource=source,BindingSource="group.Value"});
        root.AddNode(new("group","outer","outer","Subroutine"){SubroutineId=middle.Id,Parameters=outer});
        var solution=new Solution();solution.Workflows.Add(root);solution.Workflows.Add(middle);solution.Workflows.Add(leaf);
        var runner=new WorkflowExecutionEngine(catalog,new PluginManager(logger),logger);
        using var result=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(root.Id){ExecutionOwner=solution.ExecutionOwner}).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.True(result.Success,string.Join(";",result.Errors));
        var output=result.NodeResults["group"].Result!;Assert.True(output.IsSuccess,output.ErrorMessage);Assert.Equal(7d,OutputVariableResolver.Resolve(output,"Final"));
        var text=Assert.IsType<TextDisplayItem>(Assert.Single(output.Display.Items));Assert.Equal("7",Assert.Single(text.Texts).Content);
        Assert.Null(outer.ExecuteSubWorkflowAsync);Assert.Null(inner.ExecuteSubWorkflowAsync);Assert.Equal("7",parse.InputText);Assert.Equal("group.Value",outer.Outputs[0].BindingSource);
        Assert.Equal(WorkflowExecutionState.Idle,runner.CurrentState);
        parse.InputText="not-a-number";
        using var failed=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(root.Id){ExecutionOwner=solution.ExecutionOwner}).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.False(failed.Success);Assert.False(failed.NodeResults["group"].Result!.IsSuccess);Assert.Empty(failed.NodeResults["group"].Result!.Display.Items);
        Assert.Equal(WorkflowExecutionState.Idle,runner.CurrentState);Assert.Null(outer.ExecuteSubWorkflowAsync);Assert.Null(inner.ExecuteSubWorkflowAsync);
        parse.InputText="11";
        using var recovered=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(root.Id){ExecutionOwner=solution.ExecutionOwner}).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.True(recovered.Success,string.Join(";",recovered.Errors));Assert.Equal(11d,OutputVariableResolver.Resolve(recovered.NodeResults["group"].Result!,"Final"));Assert.Equal(WorkflowExecutionState.Idle,runner.CurrentState);


    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Group_internal_receive_waits_for_a_fresh_message_and_parent_cancel_reaches_child(bool cancel)
    {
        var host=typeof(GroupWorkflowExecutionTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var plugin=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(host,"plugins","SunEyeVision.Tool.Subroutine","SunEyeVision.Tool.Subroutine.dll"));
        ToolRegistry.RegisterTool(plugin.GetType("SunEyeVision.Tool.Subroutine.SubroutineTool",true)!);ToolRegistry.RegisterTool(typeof(ReceiveDataTool));
        var service=new CommunicationReceiveService();service.Configure(new[]{new CommunicationReceiveSource("device","","TCP","Binary")});
        var logger=VisionLogger.Instance;var catalog=new WorkflowEngine(logger);var id=Guid.NewGuid().ToString("N");
        var root=catalog.CreateWorkflow("root-"+id,"Root");var child=catalog.CreateWorkflow("child-"+id,"Child");child.IsSubroutine=true;
        var parameters=new SubroutineParameters{SubroutineId=child.Id};root.AddNode(new("group","group","group","Subroutine"){SubroutineId=child.Id,Parameters=parameters});
        child.AddNode(new("receiver","receiver","receiver","ReceiveData"){Parameters=new ReceiveDataParameters{DeviceId="device"}});
        var solution=new Solution();solution.Workflows.Add(root);solution.Workflows.Add(child);var runner=new WorkflowExecutionEngine(catalog,new PluginManager(logger),logger);
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runner.NodeStatusChanged+=(_,e)=>{if(e.NodeId=="receiver"&&e.Status==NodeStatus.Running)started.TrySetResult();};
        using var stop=new CancellationTokenSource();
        var run=runner.ExecuteWorkflowAsync(new WorkflowRunRequest(root.Id,communicationInputs:service){ExecutionOwner=solution.ExecutionOwner},stop.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.False(run.IsCompleted);
            if(cancel)stop.Cancel();else service.Publish("device","",DateTimeOffset.UtcNow,new BinaryPayload(new byte[]{7}));
            try
            {
                using var result=await run.WaitAsync(TimeSpan.FromSeconds(10));
                if(cancel){Assert.False(result.Success);Assert.Equal(ExecutionStatus.Cancelled,result.Status);}
                else Assert.True(result.Success,string.Join(";",result.Errors));
            }
            catch(OperationCanceledException) when(cancel){}
            Assert.Equal(WorkflowExecutionState.Idle,runner.CurrentState);Assert.Null(parameters.ExecuteSubWorkflowAsync);
        }
        finally
        {
            if(!run.IsCompleted){stop.Cancel();try{using var cleanup=await run.WaitAsync(TimeSpan.FromSeconds(10));}catch(OperationCanceledException){}}
        }
    }

}

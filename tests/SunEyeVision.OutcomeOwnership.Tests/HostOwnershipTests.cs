using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Workflow;
using SunEyeVision.OutcomeOwnership.Probe;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;
public sealed class HostOwnershipTests
{
    [Fact]
    public async Task Formal_and_debug_hosts_release_unreturned_output_and_call_scope_when_session_cleanup_fails()
    {
        var metadata=new ToolComponentMetadata("OutcomeOwnership.Runtime.Tests",Definition.Id,ToolComponentRole.Runtime,FoundationContract.Major,FoundationContract.Minor);
        using(var registration=ToolRegistry.PrepareRegistration(Array.Empty<Type>(),new[]{new ToolComponentRegistration(metadata,typeof(Definition))}).EnterCommit())registration.Publish();
        try
        {
            var logger=VisionLogger.Instance;var engine=new WorkflowEngine(logger);var flow=engine.CreateWorkflow("owner-"+Guid.NewGuid().ToString("N"),"owner");
            var node=new WorkflowNodeBase("node","node","node",Definition.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(Definition.Id)};
            flow.Nodes.Add(node);var solution=new Solution();solution.Workflows.Add(flow);
            Definition.FailCleanup=true;Output.DisposeCount=0;Definition.LastResources=null;
            var runner=new WorkflowExecutionEngine(engine,new PluginManager(logger),logger);
            using(var run=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id){ExecutionOwner=solution.ExecutionOwner}).WaitAsync(TimeSpan.FromSeconds(30)))
            {
                Assert.False(run.Success);Assert.Contains(run.Errors,error=>error.Contains("probe-session-cleanup"));Assert.NotNull(Definition.LastResources);Assert.Equal(1,Output.DisposeCount);
                Assert.Throws<ObjectDisposedException>(()=>Definition.LastResources!.CreateScope());
            }
            Assert.Equal(1,Output.DisposeCount);

            Output.DisposeCount=0;Definition.LastResources=null;
            using(var debug=RuntimeDebugInvocation.Capture(flow,node,new ToolExecutionOwner(),0,_=>null,CancellationToken.None))
            {
                var error=await Assert.ThrowsAsync<InvalidOperationException>(async()=>await debug.ExecuteAsync());
                Assert.Equal("probe-session-cleanup",error.Message);Assert.NotNull(Definition.LastResources);
                Assert.Equal(1,Output.DisposeCount);Assert.Throws<ObjectDisposedException>(()=>Definition.LastResources!.CreateScope());
            }
            Assert.Equal(1,Output.DisposeCount);
        }
        finally{Definition.LastResources=null;Definition.FailCleanup=false;ToolRegistry.UnregisterTool(Definition.Id);}
    }
}

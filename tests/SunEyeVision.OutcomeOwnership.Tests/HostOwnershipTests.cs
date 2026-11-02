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
        var payload=Environment.GetEnvironmentVariable("SUNEYE_OUTCOME_TEST_PAYLOAD")??throw new InvalidOperationException("缺少正式封包测试目录。");
        var directory=new ReleasePayloadAuthority(payload).PrepareRuntimeDirectory();
        Assert.True(directory.Contains(Definition.Id));
        try
        {
            var logger=VisionLogger.Instance;var engine=new WorkflowEngine(logger,directory);var flow=engine.CreateWorkflow("owner-"+Guid.NewGuid().ToString("N"),"owner");
            var node=new WorkflowNodeBase("node","node","node",Definition.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(directory,Definition.Id)};
            flow.Nodes.Add(node);var solution=new Solution();solution.Workflows.Add(flow);
            flow.AttachExecutionOwner(solution.ExecutionOwner);
            Definition.FailCleanup=true;Output.DisposeCount=0;Definition.LastResources=null;
            var runner=new WorkflowExecutionEngine(engine,logger);
            using(var run=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id){ExecutionOwner=solution.ExecutionOwner}).WaitAsync(TimeSpan.FromSeconds(30)))
            {
                Assert.False(run.Success);Assert.Contains(run.Errors,error=>error.Contains("probe-session-cleanup"));Assert.NotNull(Definition.LastResources);Assert.Equal(1,Output.DisposeCount);
                Assert.Throws<ObjectDisposedException>(()=>Definition.LastResources!.CreateScope());
            }
            Assert.Equal(1,Output.DisposeCount);

            Output.DisposeCount=0;Definition.LastResources=null;
            var debugOwner=new ToolExecutionOwner();
            try
            {
                using(var debug=RuntimeDebugInvocation.Capture(flow,directory,node.Clone(),debugOwner,0,_=>null,CancellationToken.None))
                {
                    var error=await Assert.ThrowsAsync<InvalidOperationException>(async()=>await debug.ExecuteAsync());
                    Assert.Equal("probe-session-cleanup",error.Message);Assert.NotNull(Definition.LastResources);
                    Assert.Equal(1,Output.DisposeCount);Assert.Throws<ObjectDisposedException>(()=>Definition.LastResources!.CreateScope());
                }
            }
            finally{debugOwner.Retire();solution.ExecutionOwner.Retire();}
            Assert.Equal(1,Output.DisposeCount);
        }
        finally{Definition.LastResources=null;Definition.FailCleanup=false;}
    }
}

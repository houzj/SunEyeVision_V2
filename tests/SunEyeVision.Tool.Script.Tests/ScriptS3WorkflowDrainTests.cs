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

namespace SunEyeVision.Tool.Script.Tests;

public class ScriptS3WorkflowDrainTests
{
    private const string ToolId = "S3DrainProbe";
    private static readonly ConcurrentDictionary<string, Gates> Cases = new();
    private sealed class Gates
    {
        internal TaskCompletionSource CancelStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource HoldStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CancelExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseHold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Immediate;
    }

    [Fact]
    public async Task Stop_DrainsActualIgnoringTask_BeforeSlotAndLeasesAreReusable()
    {
        var caseId = Guid.NewGuid().ToString("N");
        var gates = new Gates();
        Cases[caseId] = gates;
        ToolRegistry.RegisterTool(typeof(DrainProbeTool));
        var logger = Substitute.For<ILogger>();
        var catalog = new WorkflowEngine(logger);
        var workflow = catalog.CreateWorkflow("s3-drain-" + caseId, "drain");
        workflow.AddNode(new WorkflowNodeBase("cancel", "cancel", "cancel", ToolId)
        { Parameters = new DrainProbeParameters { CaseId = caseId } });
        workflow.AddNode(new WorkflowNodeBase("hold", "hold", "hold", ToolId)
        { Parameters = new DrainProbeParameters { CaseId = caseId, Hold = true } });
        var runner = new WorkflowExecutionEngine(catalog, Substitute.For<IPluginManager>(), logger);
        var published = 0;
        runner.NodeExecutionFinished += (_, _) => Interlocked.Increment(ref published);
        var running = runner.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id));
        try
        {
            await Task.WhenAll(gates.CancelStarted.Task, gates.HoldStarted.Task).WaitAsync(TimeSpan.FromSeconds(10));
            runner.StopExecution();
            await gates.CancelExited.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(WorkflowExecutionState.Stopping, runner.CurrentState);
            Assert.False(running.IsCompleted);
            using var rejected = await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id));
            Assert.False(rejected.Success);
            Assert.Contains(rejected.Errors, error => error.Contains("已有工作流正在执行"));
            Assert.Equal(0, published);
        }
        catch
        {
            gates.ReleaseHold.TrySetResult();
            try { using var cleanup = await running.WaitAsync(TimeSpan.FromSeconds(10)); } catch { }
            Cases.TryRemove(caseId, out _); ToolRegistry.UnregisterTool(ToolId);
            throw;
        }
        finally { gates.ReleaseHold.TrySetResult(); }
        try
        {
            using var stopped = await running.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(ExecutionStatus.Cancelled, stopped.Status);
            Assert.Equal(0, published);
            gates.Immediate = true;
            using var restarted = await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(workflow.Id)).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(restarted.Success, string.Join(";", restarted.Errors));
        }
        finally
        {
            gates.ReleaseHold.TrySetResult(); Cases.TryRemove(caseId, out _);
            ToolRegistry.UnregisterTool(ToolId);
        }
    }

    public class DrainProbeParameters : ToolParameters
    {
        public string CaseId { get; set; } = "";
        public bool Hold { get; set; }
    }
    public class DrainProbeResults : ToolResults { }
    [Tool(id: ToolId, displayName: "S3 drain test")]
    public class DrainProbeTool : IToolPlugin<DrainProbeParameters, DrainProbeResults>, IAsyncToolPlugin, IToolExecutionProfileProvider
    {
        public DrainProbeResults Run(DrainProbeParameters parameters) => throw new InvalidOperationException("async only");
        public ToolExecutionProfile DescribeExecution(ToolParameters parameters) => new(ToolConcurrencyKind.ExternalDataWait);
        public async ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        {
            var parameters = (DrainProbeParameters)request.Parameters;
            var gates = Cases[parameters.CaseId];
            if (!gates.Immediate)
            {
                if (parameters.Hold)
                {
                    gates.HoldStarted.TrySetResult();
                    await gates.ReleaseHold.Task;
                }
                else
                {
                    gates.CancelStarted.TrySetResult();
                    try { await Task.Delay(Timeout.Infinite, request.CancellationToken); }
                    finally { gates.CancelExited.TrySetResult(); }
                }
            }
            return new DrainProbeResults { Status = ExecutionStatus.Success };
        }
    }
}

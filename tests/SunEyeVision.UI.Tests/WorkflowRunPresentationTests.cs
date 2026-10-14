using NSubstitute;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public class WorkflowRunPresentationTests
{
    [Fact]
    public void New_iteration_resets_canvas_results_and_snapshot_but_not_other_workflows_or_preview_images()
    {
        var engine = new WorkflowEngine(Substitute.For<ILogger>());
        var root = engine.CreateWorkflow("root", "root");
        var child = engine.CreateWorkflow("child", "child");
        var other = engine.CreateWorkflow("other", "other");
        var receive = new WorkflowNodeBase("receive", "receive", "receive", "ReceiveData");
        var downstream = new WorkflowNodeBase("parse", "parse", "parse", "ProtocolParse");
        root.Nodes.Add(receive); root.Nodes.Add(downstream);
        root.Nodes.Add(new WorkflowNodeBase("call", "call", "call", "Subroutine") { SubroutineId = child.Id });
        var nested = new WorkflowNodeBase("nested", "nested", "nested", "ProtocolParse");
        child.Nodes.Add(nested);
        // Recursion guard must tolerate a malformed recursive definition without modifying scheduling.
        child.Nodes.Add(new WorkflowNodeBase("cycle", "cycle", "cycle", "Subroutine") { SubroutineId = root.Id });
        var untouched = new WorkflowNodeBase("untouched", "untouched", "untouched", "ProtocolParse");
        other.Nodes.Add(untouched);
        var cache = new NodeOutputCache(downstream.Id);
        cache.Update(new CommunicationToolResults(), null);
        var preview = new OutputImageInfo();
        cache.AddOutputImage(preview); downstream.OutputCache = cache;
        foreach (var node in new[] { receive, downstream, nested, untouched })
        {
            node.Status = "成功"; node.LastResult = new CommunicationToolResults();
        }
        var snapshot = new WorkflowExecutionSnapshot(root.Id, root.Name);
        snapshot.UpdateNodeResult(downstream.Id, downstream.Name, new NodeExecutionResult
        {
            NodeId = downstream.Id, Success = true, Status = ExecutionStatus.Success,
            StartTime = DateTime.Now.AddSeconds(-1), EndTime = DateTime.Now, Result = new CommunicationToolResults { ExecutionTimeMs = 12 }
        });
        var manager = new WorkflowExecutionManager(Substitute.For<IInputProvider>(), engine, new RuntimeInputSnapshotService());
        try
        {
        manager.BeginRunPresentation(root.Id, root.Name, snapshot);
        foreach (var node in new[] { receive, downstream, nested })
        {
            Assert.Equal("等待中", node.Status); Assert.Null(node.LastResult);
        }
        Assert.Null(cache.LastResult); Assert.Null(cache.LastExecutionTime);
        Assert.Same(preview, cache.CurrentOutputImage); Assert.Equal(1, cache.Count);
        Assert.Equal("成功", untouched.Status); Assert.NotNull(untouched.LastResult);
        var row = Assert.Single(snapshot.Nodes.Where(n => n.NodeId == downstream.Id));
        Assert.Equal("等待中", row.Status); Assert.Equal(0, row.AlgorithmElapsedMs);
        Assert.Equal(TimeSpan.Zero, row.ModuleElapsed);
        // A receive becoming Running must not make its untouched dependent green again.
        receive.Status = "运行中";
        snapshot.UpdateNodeStatus(receive.Id, receive.Name, "运行中");
        Assert.Equal("等待中", downstream.Status); Assert.Equal("等待中", row.Status);
        }
        finally { manager.Dispose(); }
    }
}

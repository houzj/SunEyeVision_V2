using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;
public sealed class RunConfigurationTests
{
    [Fact]
    public void Captured_directory_is_used_by_all_copy_paths_and_rejects_foreign_nodes()
    {
        var directory = RootCanvasFixture.Directory;
        var flow = new SunEyeVision.Workflow.Workflow("flow", "flow");
        flow.Nodes.Add(RootCanvasFixture.Node("image"));
        var captured = WorkflowRunConfiguration.Capture(new[] { flow }, directory, 4);
        Assert.Equal(4, captured.DocumentRevision);
        var first = Assert.Single(captured.CopyWorkflows());
        Assert.Same(flow.ExecutionOwner, first.ExecutionOwner);
        Assert.Single(first.Nodes);
        var second = captured.CopyWorkflow("flow")!;
        Assert.Same(flow.ExecutionOwner, second.ExecutionOwner);
        Assert.Single(second.Nodes);
        Assert.Null(captured.CopyWorkflow("missing"));
        var resolved = WorkflowRunConfiguration.Capture(flow.Id, id => id == flow.Id ? flow : null, directory);
        Assert.Single(resolved.CopyWorkflow(flow.Id)!.Nodes);
        var payload = Environment.GetEnvironmentVariable("SUNEYE_ROOT_TEST_PAYLOAD")!;
        var foreign = new ReleasePayloadAuthority(payload).PrepareRuntimeDirectory();
        Assert.Throws<InvalidOperationException>(() => WorkflowRunConfiguration.Capture(new[] { flow }, foreign));
    }

    [Fact]
    public void Empty_workflow_is_captured_and_copied_with_explicit_directory()
    {
        var flow = new SunEyeVision.Workflow.Workflow("empty", "empty");
        var snapshot = WorkflowRunConfiguration.Capture(new[] { flow }, RootCanvasFixture.Directory);
        Assert.Empty(snapshot.CopyWorkflow("empty")!.Nodes);
        Assert.Empty(Assert.Single(snapshot.CopyWorkflows()).Nodes);
    }

    [Fact]
    public async Task Engine_accepts_captured_empty_workflow_without_losing_directory()
    {
        var logger = VisionLogger.Instance;
        var engine = new WorkflowEngine(logger, RootCanvasFixture.Directory);
        var flow = engine.CreateWorkflow("run-empty", "run-empty");
        var snapshot = WorkflowRunConfiguration.Capture(new[] { flow }, RootCanvasFixture.Directory);
        var runner = new WorkflowExecutionEngine(engine, logger);
        using var result = await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id) { Configuration = snapshot });
        Assert.True(result.Success, string.Join("; ",result.Errors));
        Assert.Equal(0, flow.ExecutionOwner.ActiveRuns);
    }

    [Fact]
    public async Task Engine_rejects_foreign_empty_snapshot_without_starting_execution()
    {
        var foreign = new ReleasePayloadAuthority(Environment.GetEnvironmentVariable("SUNEYE_ROOT_TEST_PAYLOAD")!).PrepareRuntimeDirectory();
        var logger = VisionLogger.Instance;
        var engine = new WorkflowEngine(logger, RootCanvasFixture.Directory);
        var flow = engine.CreateWorkflow("foreign-empty", "foreign-empty");
        var snapshot = WorkflowRunConfiguration.Capture(new[] { flow }, foreign);
        var runner = new WorkflowExecutionEngine(engine, logger);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id) { Configuration = snapshot }));
        Assert.Contains("目录代次", error.Message);
        Assert.Equal(0, flow.ExecutionOwner.ActiveRuns);
    }
}

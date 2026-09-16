using SunEyeVision.Workflow;
using System.Threading.Tasks;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public class WorkflowRunSessionTests
{
    [Fact]
    public void NewSession_HasIndependentIdentityAndActiveCancellationToken()
    {
        using var first = new WorkflowRunSession("root-a");
        using var second = new WorkflowRunSession("root-b");

        Assert.NotEqual(first.RunId, second.RunId);
        Assert.Equal("root-a", first.RootWorkflowId);
        Assert.False(first.CancellationToken.IsCancellationRequested);
        Assert.Equal(WorkflowExecutionState.Idle, first.State);
    }

    [Fact]
    public void RequestStop_CancelsOnlyTheOwningSession()
    {
        using var first = new WorkflowRunSession("root-a");
        using var second = new WorkflowRunSession("root-b");

        first.RequestStop();

        Assert.True(first.CancellationToken.IsCancellationRequested);
        Assert.False(second.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void ExternalCancellation_CancelsTheLinkedRunSession()
    {
        using var external = new CancellationTokenSource();
        using var session = new WorkflowRunSession("root-a", external.Token);

        external.Cancel();

        Assert.True(session.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Pause_BlocksAtExecutionBoundary_UntilResume()
    {
        using var session = new WorkflowRunSession("root-a");
        Assert.True(session.TryStart());

        Assert.True(session.RequestPause());
        var waitTask = session.WaitIfPausedAsync().AsTask();
        Assert.False(waitTask.IsCompleted);

        Assert.True(session.RequestResume());
        await waitTask;
        Assert.Equal(WorkflowExecutionState.Running, session.State);
    }

    [Fact]
    public async Task Stop_ReleasesPausedWait_WithCancellation()
    {
        using var session = new WorkflowRunSession("root-a");
        Assert.True(session.TryStart());

        session.RequestPause();
        var waitTask = session.WaitIfPausedAsync().AsTask();
        session.RequestStop();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
    }

    [Fact]
    public void ChildScope_HasIndependentIdentityAndLinksToParentRun()
    {
        using var session = new WorkflowRunSession("root-a");

        var child = session.CreateChildScope("sub-a", session.RootScope.RunId);
        var grandchild = session.CreateChildScope("sub-b", child.RunId);

        Assert.NotEqual(session.RootScope.RunId, child.RunId);
        Assert.Equal(session.RootScope.RunId, child.ParentRunId);
        Assert.Equal(child.RunId, grandchild.ParentRunId);
        Assert.Equal(3, session.Scopes.Count);
    }
}

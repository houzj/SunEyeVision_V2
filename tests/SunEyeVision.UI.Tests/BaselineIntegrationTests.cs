using NSubstitute;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class BaselineIntegrationTests
{
    private static WorkflowExecutionManager Manager() => new(
        Substitute.For<IInputProvider>(),
        new WorkflowEngine(Substitute.For<ILogger>()),
        new RuntimeInputSnapshotService());

    [Fact]
    public async Task Solution_transition_rejects_new_communication_execution()
    {
        var manager = Manager();
        using (manager.BeginSolutionTransition())
        {
            var executed = false;
            Assert.False(await manager.RunCommunicationOperationAsync(_ =>
            {
                executed = true;
                return Task.CompletedTask;
            }, default));
            Assert.False(executed);
        }
        Assert.True(await manager.RunCommunicationOperationAsync(_ => Task.CompletedTask, default));
        Assert.False(manager.IsAnyRunning());
        manager.Dispose();
    }

    [Fact]
    public async Task Solution_transition_cancels_and_waits_for_active_communication_execution()
    {
        var manager = Manager();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = manager.RunCommunicationOperationAsync(async token =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(manager.IsAnyRunning());
        using (manager.BeginSolutionTransition())
        {
            await manager.StopAndWaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            Assert.False(manager.IsAnyRunning());
        }
        Assert.True(await manager.RunCommunicationOperationAsync(_ => Task.CompletedTask, default));
        manager.Dispose();
    }

    [Fact]
    public void Solution_clone_preserves_metadata_and_independent_communication_configuration()
    {
        var source = new Solution { Name = "共享基线", Description = "相机与通讯" };
        source.CommunicationEvents.Add(new() { Name = "接收" });
        source.CommunicationSendEvents.Add(new());
        source.CommunicationHeartbeats.Add(new() { ContentA = "alive", Enabled = true });
        source.CommunicationTriggers.Add(new() { Command = "执行流程", TargetId = "flow" });
        source.CommunicationResponses.WorkflowIdentifiers["flow"] = 7;
        var copy = source.Clone();
        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(source.Name, copy.Name);
        Assert.Equal(source.Description, copy.Description);
        Assert.Single(copy.CommunicationEvents);
        Assert.Single(copy.CommunicationSendEvents);
        Assert.Equal("alive", Assert.Single(copy.CommunicationHeartbeats).ContentA);
        Assert.Equal("flow", Assert.Single(copy.CommunicationTriggers).TargetId);
        Assert.Equal(7, copy.CommunicationResponses.WorkflowIdentifiers["flow"]);
        copy.CommunicationHeartbeats[0].ContentA = "changed";
        copy.CommunicationResponses.WorkflowIdentifiers["flow"] = 8;
        Assert.Equal("alive", source.CommunicationHeartbeats[0].ContentA);
        Assert.Equal(7, source.CommunicationResponses.WorkflowIdentifiers["flow"]);
    }
}

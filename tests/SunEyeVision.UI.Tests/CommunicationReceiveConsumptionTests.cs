using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public class CommunicationReceiveConsumptionTests
{
    [Fact]
    public async Task Editor_and_workflow_requests_share_cursor_but_other_workflow_does_not()
    {
        var store = Store(); Publish(store);
        var p = new SunEyeVision.Tool.ReceiveData.ReceiveDataParameters { DeviceId = "d" };
        var tool = new SunEyeVision.Tool.ReceiveData.ReceiveDataTool();
        var first = await tool.ExecuteAsync(new SunEyeVision.Plugin.SDK.Core.ToolExecutionRequest(p)
            { WorkflowId = "flow", NodeId = "node", CommunicationInputs = store });
        Assert.True(first.IsSuccess);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waiting = tool.ExecuteAsync(new SunEyeVision.Plugin.SDK.Core.ToolExecutionRequest(p)
            { WorkflowId = "flow", NodeId = "node", CommunicationInputs = new RuntimeVariableReceiveService(store, null, workflowId: "flow"), CancellationToken = stop.Token }).AsTask();
        Assert.False(waiting.IsCompleted);
        var other = await tool.ExecuteAsync(new SunEyeVision.Plugin.SDK.Core.ToolExecutionRequest(p)
            { WorkflowId = "other", NodeId = "node", CommunicationInputs = store });
        Assert.True(other.IsSuccess); Assert.False(waiting.IsCompleted);
        Publish(store); Assert.True((await waiting).IsSuccess);
    }
    private static CommunicationReceiveService Store()
    {
        var store = new CommunicationReceiveService();
        store.Configure(new[] { new CommunicationReceiveSource("d", "", "TCP", "Binary") });
        return store;
    }
    private static void Publish(CommunicationReceiveService store, byte value = 7) =>
        store.Publish("d", "", DateTimeOffset.UtcNow, new BinaryPayload(new[] { value }));
    [Fact]
    public async Task Same_payload_is_new_only_when_published_again_and_readers_are_independent()
    {
        var store = Store(); Publish(store); long sequence = 0;
        await store.ConsumeAsync("d", "", "node-a", s => sequence = s.Sequence);
        Assert.Equal(1, sequence);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var next = store.ConsumeAsync("d", "", "node-a", s => sequence = s.Sequence, stop.Token);
        Assert.False(next.IsCompleted);
        await store.ConsumeAsync("d", "", "node-b", s => Assert.Equal(1, s.Sequence));
        Assert.False(next.IsCompleted);
        Publish(store); await next; Assert.Equal(2, sequence);
    }
    [Fact]
    public async Task Buffered_messages_are_ordered_and_failed_commit_or_cancel_does_not_consume()
    {
        var store = Store(); Publish(store, 1); Publish(store, 2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync("d", "", "n", _ => throw new InvalidOperationException("publish failed")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ConsumeAsync("d", "", "n", _ => Assert.Fail("cancelled"), new CancellationToken(true)));
        await store.ConsumeAsync("d", "", "n", s => Assert.Equal("01", s.Value.ToString()));
        await store.ConsumeAsync("d", "", "n", s => Assert.Equal("02", s.Value.ToString()));
        using var stop = new CancellationTokenSource();
        var wait = store.ConsumeAsync("d", "", "n", _ => Assert.Fail("old message"), stop.Token);
        Assert.False(wait.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync("d", "", "n", _ => { }));
        stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        Publish(store, 3); await store.ConsumeAsync("d", "", "n", s => Assert.Equal(3, s.Sequence));
    }
    [Theory]
    [InlineData("reset")]
    [InlineData("disconnect")]
    [InlineData("remove")]
    [InlineData("configuration")]
    public async Task Session_change_rejects_old_wait_even_if_new_data_arrives(string change)
    {
        var store = Store();
        var waiting = store.ConsumeAsync("d", "", "n", _ => Assert.Fail("new session cannot satisfy old wait"));
        var sources = store.GetSources();
        if (change == "reset") store.Configure(sources, true);
        else if (change == "disconnect") { store.InvalidateDevice("d"); store.MarkConnected("d"); }
        else if (change == "remove") { store.Configure(Array.Empty<CommunicationReceiveSource>()); store.Configure(sources); }
        else store.Configure(sources.Select(s => s with { ConfigurationKey = "changed" }));
        Publish(store);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
        await store.ConsumeAsync("d", "", "n", s => Assert.Equal(1, s.Sequence));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overflow_reports_gap_without_silent_skip(bool byteLimit)
    {
        var store = Store();
        Publish(store);
        // Consume the initial sequence before creating a gap for the same subscriber.
        await store.ConsumeAsync("d", "", "n", _ => { });
        if (byteLimit) store.Publish("d", "", DateTimeOffset.UtcNow, new BinaryPayload(new byte[CommunicationReceiveService.MaximumBufferedBytes + 1]));
        else for (var i = 0; i <= CommunicationReceiveService.MaximumBufferedMessages; i++) Publish(store);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync("d", "", "n", _ => Assert.Fail("gap")));
        Assert.Contains("溢出", error.Message);
    }
    [Fact]
    public async Task Wrapper_scope_and_stable_child_call_path_isolate_consumers_across_runs()
    {
        var store = Store(); Publish(store);
        using var first = new WorkflowRunSession("root");
        using var second = new WorkflowRunSession("root");
        var a = first.CreateChildScope("child", first.RootScope.RunId, "caller-a");
        var same = second.CreateChildScope("child", second.RootScope.RunId, "caller-a");
        var b = second.CreateChildScope("child", second.RootScope.RunId, "caller-b");
        Assert.Equal(a.ConsumerScope, same.ConsumerScope); Assert.NotEqual(a.ConsumerScope, b.ConsumerScope);
        var service = new RuntimeVariableReceiveService(store, null) { ConsumerScope = a.ConsumerScope };
        await service.ConsumeAsync("d", "", "node", _ => { });
        var other = new RuntimeVariableReceiveService(store, null) { ConsumerScope = b.ConsumerScope };
        await other.ConsumeAsync("d", "", "node", s => Assert.Equal(1, s.Sequence));
        using var stop = new CancellationTokenSource();
        var rerun = new RuntimeVariableReceiveService(store, null) { ConsumerScope = same.ConsumerScope }
            .ConsumeAsync("d", "", "node", _ => Assert.Fail("reused"), stop.Token);
        Assert.False(rerun.IsCompleted); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rerun);
    }
}

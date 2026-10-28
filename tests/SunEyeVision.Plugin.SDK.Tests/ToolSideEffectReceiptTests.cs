using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Communication;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class ToolSideEffectReceiptTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public async Task Rejected_results_keep_only_independent_facts_and_always_drain(bool asynchronous, int mode)
    {
        using var cancellation = new CancellationTokenSource();
        var owner = new ToolExecutionOwner();
        var original = new ReceiptResult { Status = ExecutionStatus.Success, Tag = new object(), ThrowCapture = mode == 2 };
        var resource = new Lease { Throw = mode == 3 };
        original.RegisterOutputResource(resource);
        var request = new ToolExecutionRequest(new Parameters())
            { ExecutionOwner = owner, NodeId = "send", WorkflowId = "flow", CancellationToken = cancellation.Token };
        ReceiptResult Body()
        {
            if (mode == 1) owner.RetireNode("flow/send"); else cancellation.Cancel();
            return original;
        }
        IToolPlugin tool = asynchronous ? new AsyncTool(Body) : new SyncTool(Body);
        var executor = new ToolExecutor(new ParameterResolver());
        var terminal = asynchronous ? await executor.ExecuteAsync(tool, request) : executor.Run(tool, request);
        Assert.NotSame(original, terminal);
        Assert.Equal(mode >= 2 ? ExecutionStatus.Failed : ExecutionStatus.Cancelled, terminal.Status);
        Assert.False(terminal.IsSuccess);
        Assert.Null(terminal.Tag);
        Assert.Null(terminal.Resources);
        Assert.Null(terminal.DeclaredOutput);
        Assert.Equal(1, original.DisposeCount);
        Assert.Equal(1, resource.Count);
        Assert.Equal(0, owner.ActiveRuns);
        Assert.Same(request.ExecutionIdentity, terminal.ExecutionIdentity);
        Assert.False(terminal.ExecutionIdentity!.TryPublish(() => throw new Exception("旧身份不得发布")));
        if (mode >= 2)
        {
            Assert.Null(terminal.SideEffectReceipt);
            Assert.Contains("receipt capture or cleanup failed", terminal.ErrorMessage);
        }
        else
        {
            var receipt = Assert.IsType<ToolSideEffectReceipt>(terminal.SideEffectReceipt);
            Assert.Equal(1, receipt.Submitted);
            Assert.Equal(2, receipt.Skipped);
            Assert.Equal(3, receipt.Failed);
            Assert.Equal("committed", Assert.Single(receipt.SentData));
            Assert.Equal(new byte[] { 7, 8 }, Assert.Single(receipt.Payloads).ToArray());
            var copy = receipt.Payloads[0].ToArray(); copy[0] = 99;
            Assert.Equal(7, receipt.Payloads[0].ToArray()[0]);
            Assert.Empty(original.Messages);
        }
    }
    public sealed class Parameters : ToolParameters { }
    public sealed class ReceiptResult : ToolResults, IToolSideEffectReceiptSource, IDisposable
    {
        public bool ThrowCapture;
        public int DisposeCount;
        public readonly List<string> Messages = new() { "committed" };
        private readonly byte[] _bytes = { 7, 8 };
        public ToolSideEffectReceipt CaptureSideEffectReceipt()
        {
            if (ThrowCapture) throw new InvalidOperationException("capture failed");
            return new(1, 2, 3, Messages, new[] { new BinaryPayload(_bytes) });
        }
        public void Dispose() { DisposeCount++; Messages.Clear(); _bytes[0] = 0; }
    }
    private sealed class Lease : IDisposable
    {
        public int Count; public bool Throw;
        public void Dispose() { Count++; if (Throw) throw new InvalidOperationException("cleanup failed"); }
    }
    private class SyncTool(Func<ReceiptResult> body) : IToolPlugin<Parameters, ReceiptResult>
    { public ReceiptResult Run(Parameters parameters) => body(); }
    private sealed class AsyncTool(Func<ReceiptResult> body) : SyncTool(body), IAsyncToolPlugin
    { public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request) => ValueTask.FromResult<ToolResults>(Run((Parameters)request.Parameters)); }
}

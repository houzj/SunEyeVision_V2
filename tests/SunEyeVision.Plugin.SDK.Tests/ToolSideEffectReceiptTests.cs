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
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    public async Task Rejected_results_keep_only_independent_facts_and_always_drain(bool asynchronous, int mode)
    {
        using var cancellation = new CancellationTokenSource();
        var owner = new ToolExecutionOwner();
        var original = new ReceiptResult { Status = ExecutionStatus.Success, Tag = new object(), ThrowCapture = mode == 2, ThrowDispose = mode == 4 };
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
        Assert.All(terminal.GetOutputVariables(), field => Assert.Null(field.RuntimeValue));
        Assert.Equal(1, original.DisposeCount);
        Assert.Equal(1, resource.Count);
        Assert.Equal(0, owner.ActiveRuns);
        Assert.Same(request.ExecutionIdentity, terminal.ExecutionIdentity);
        Assert.False(terminal.ExecutionIdentity!.TryPublish(() => throw new Exception("旧身份不得发布")));
        if (mode >= 2)
        {
            if (mode == 2) Assert.Null(terminal.SideEffectReceipt);
            else Assert.Equal(1, terminal.SideEffectReceipt!.Submitted);
            Assert.Contains("receipt capture or cleanup failed", terminal.ErrorMessage);
            Assert.Contains(mode == 2 ? "capture failed" : mode == 4 ? "dispose failed" : "cleanup failed", terminal.ErrorStackTrace);
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
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    [InlineData(false, 5)]
    [InlineData(true, 5)]
    public async Task Terminal_constructor_resources_and_inspection_are_rejected_without_reentry(bool asynchronous, int mode)
    {
        using var cancellation = new CancellationTokenSource();
        var state = new ConstructorState(mode); ConstructorState.Current.Value = state;
        try
        {
            var original = new ConstructorResult { Status = ExecutionStatus.Success };
            var owner = new ToolExecutionOwner();
            var request = new ToolExecutionRequest(new Parameters())
                { ExecutionOwner = owner, NodeId = "send", CancellationToken = cancellation.Token };
            ConstructorResult Body() { cancellation.Cancel(); return original; }
            IToolPlugin tool = asynchronous ? new AsyncConstructorTool(Body) : new ConstructorTool(Body);
            var executor = new ToolExecutor(new ParameterResolver());
            var terminal = asynchronous ? await executor.ExecuteAsync(tool, request) : executor.Run(tool, request);
            Assert.IsType<GenericToolResults>(terminal);
            Assert.Equal(mode is 1 or 2 ? ExecutionStatus.Failed : ExecutionStatus.Cancelled, terminal.Status);
            Assert.Equal(2, state.Constructors); // Original and rejected candidate only; no third plugin constructor.
            Assert.Equal(1, terminal.SideEffectReceipt!.Submitted);
            Assert.Null(terminal.Resources); Assert.Null(terminal.Tag); Assert.Null(terminal.DeclaredOutput);
            Assert.Empty(terminal.GetOutputVariables()); Assert.False(terminal.Display.HasItems);
            Assert.Equal(0, owner.ActiveRuns);
            Assert.All(state.Leases, lease => Assert.Equal(1, lease.Count));
            if (mode is 4 or 5) Assert.Single(state.Leases);
            if (mode == 1) Assert.Contains("inspection failed", terminal.ErrorStackTrace);
            if (mode == 2) Assert.Contains("cleanup failed", terminal.ErrorStackTrace);
        }
        finally { ConstructorState.Current.Value = null; }
    }
    private sealed class ConstructorState(int mode)
    {
        public static readonly AsyncLocal<ConstructorState?> Current = new();
        public readonly int Mode = mode;
        public int Constructors;
        public readonly List<Lease> Leases = new();
    }
    public sealed class ConstructorResult : ToolResults, IToolSideEffectReceiptSource
    {
        public ConstructorResult()
        {
            var state = ConstructorState.Current.Value!; state.Constructors++;
            if (state.Mode is 0 or 2)
            {
                var lease = new Lease { Throw = state.Mode == 2 && state.Constructors == 2 };
                state.Leases.Add(lease); RegisterOutputResource(lease);
            }
            if (state.Mode == 0) Tag = new object();
        }
        public ToolSideEffectReceipt CaptureSideEffectReceipt() => new(1, 0, 0, new[] { "sent" }, Array.Empty<BinaryPayload>());
        private void MutateDuringInspection()
        {
            var state = ConstructorState.Current.Value!;
            var lease = new Lease(); state.Leases.Add(lease);
            RegisterOutputResource(lease); Tag = new object();
        }
        public override Dictionary<string, SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage?> GetDisplayImages()
        {
            if (ConstructorState.Current.Value!.Mode == 4) MutateDuringInspection();
            return new();
        }
        public override IReadOnlyList<OutputVariable> GetOutputVariables()
        {
            var mode = ConstructorState.Current.Value!.Mode;
            if (mode == 1) throw new InvalidOperationException("inspection failed");
            if (mode == 5) MutateDuringInspection();
            if (mode is 4 or 5) return Array.Empty<OutputVariable>();
            return new[] { new OutputVariable { Name = "unexpected", RuntimeValue = "default-output" } };
        }
    }
    private class ConstructorTool(Func<ConstructorResult> body) : IToolPlugin<Parameters, ConstructorResult>
    { public ConstructorResult Run(Parameters parameters) => body(); }
    private sealed class AsyncConstructorTool(Func<ConstructorResult> body) : ConstructorTool(body), IAsyncToolPlugin
    { public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request) => ValueTask.FromResult<ToolResults>(Run((Parameters)request.Parameters)); }
    public sealed class Parameters : ToolParameters { }
    public sealed class ReceiptResult : ToolResults, IToolSideEffectReceiptSource, IDisposable
    {
        public bool ThrowCapture, ThrowDispose;
        public int DisposeCount;
        public readonly List<string> Messages = new() { "committed" };
        private readonly byte[] _bytes = { 7, 8 };
        public ToolSideEffectReceipt CaptureSideEffectReceipt()
        {
            if (ThrowCapture) throw new InvalidOperationException("capture failed");
            return new(1, 2, 3, Messages, new[] { new BinaryPayload(_bytes) });
        }
        public void Dispose() { DisposeCount++; Messages.Clear(); _bytes[0] = 0; if (ThrowDispose) throw new InvalidOperationException("dispose failed"); }
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

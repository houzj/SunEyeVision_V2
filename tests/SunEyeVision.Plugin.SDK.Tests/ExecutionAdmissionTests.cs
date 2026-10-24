using System.Reflection;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class ExecutionAdmissionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RejectedReentryDoesNotReleaseWinnerInputs(int mode)
    {
        var owner = new ToolExecutionOwner();
        var request = new ToolExecutionRequest(new Parameters()) { ExecutionOwner = owner, NodeId = "node" };
        var resources = new CountingLease();
        typeof(ToolExecutionRequest).GetProperty("InputResources", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(request, resources);
        var gate = new BodyGate();
        IToolPlugin tool = mode == 0 ? new SyncTool(gate) : new AsyncTool(gate);
        var executor = new ToolExecutor(new ParameterResolver());
        Task<ToolResults> Invoke() => mode == 1 ? executor.ExecuteAsync(tool, request).AsTask()
            : Task.Run(() => executor.Run(tool, request));
        var first = Invoke();
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var identity = request.ExecutionIdentity!;
            Assert.Equal(1, owner.ActiveRuns);
            await Assert.ThrowsAsync<InvalidOperationException>(Invoke);
            Assert.Equal(0, resources.Count);
            Assert.Equal(1, gate.Count);
            Assert.Equal(1, owner.ActiveRuns);
            Assert.True(identity.TryInvokeCallback(() => { }));
            Assert.Same(identity, request.ExecutionIdentity);
        }
        finally { gate.Release.TrySetResult(); }
        var result = await first.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(ExecutionStatus.Success, result.Status);
        Assert.Equal(1, resources.Count);
        Assert.Equal(0, owner.ActiveRuns);
        Assert.False(result.ExecutionIdentity!.TryInvokeCallback(() => throw new Exception()));
        Assert.True(result.ExecutionIdentity.TryPublish(() => { }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContractAliasesShareClosedCallbackGateButAllowFinalPublication(bool closeAlias)
    {
        var owner = new ToolExecutionOwner();
        var identity = owner.Begin("scope/node", Guid.NewGuid(), "declared");
        var alias = identity.WithContractIdentity("compiled");
        using (owner.EnterRun(identity))
        {
            Assert.Throws<InvalidOperationException>(() => owner.EnterRun(alias));
            (closeAlias ? alias : identity).CompleteCallbacks();
            Assert.False(identity.TryInvokeCallback(() => throw new Exception()));
            Assert.False(alias.TryInvokeCallback(() => throw new Exception()));
            var later = alias.WithContractIdentity("final");
            Assert.Equal(identity.RequestId, later.RequestId);
            Assert.False(later.TryInvokeCallback(() => throw new Exception()));
            Assert.True(later.TryPublish(() => { }));
            Assert.Equal("final", identity.ContractIdentity);
        }
        Assert.Equal(0, owner.ActiveRuns);
    }

    public sealed class Parameters : ToolParameters
    {
        public int Number { get; set; } = 7;
        public AvailableDataSource? Choice { get; set; }
    }
    [Fact]
    public async Task ConfigurationCopyPreservesTheExplicitSubworkflowInvocationService()
    {
        var calls = 0;
        var configuration = new SubroutineParameters { SubroutineId = "child", ExecuteSubWorkflowAsync = (id, image, context) =>
        { Assert.Equal("child", id); calls++; return Task.FromResult(ExecutionResult.CreateSuccess()); } };
        var request = new ToolExecutionRequest(configuration) { NodeId = "subroutine" };
        var result = await new ToolExecutor(new ParameterResolver()).ExecuteAsync(new SubworkflowServiceTool(), request);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(1, calls);
        Assert.NotSame(configuration, request.Parameters);
        Assert.NotSame(configuration.Inputs, ((SubroutineParameters)request.Parameters).Inputs);
        Assert.Null(configuration.Context);
        Assert.Equal(0, request.ExecutionIdentity!.Owner.ActiveRuns);
    }
    private sealed class SubworkflowServiceTool : IToolPlugin<SubroutineParameters, Results>, IAsyncToolPlugin
    {
        public Results Run(SubroutineParameters parameters) => throw new InvalidOperationException();
        public async ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        {
            var parameters = (SubroutineParameters)request.Parameters;
            await parameters.ExecuteSubWorkflowAsync!(parameters.SubroutineId, null, null);
            return new Results { Status = ExecutionStatus.Success };
        }
    }
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public async Task BodyCancellationAndRuntimeInjectionLeaveConfigurationOwnedByCaller(bool async, int mode)
    {
        using var cancellation = new CancellationTokenSource();
        using var foreign = new CancellationTokenSource();
        foreign.Cancel();
        var originalImage = NSubstitute.Substitute.For<SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage>();
        var inputImage = NSubstitute.Substitute.For<SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage>();
        var choice = new AvailableDataSource { SourceNodeId = "source", PropertyName = "value" };
        var configuration = new Parameters { Choice = choice, ResolvedInputImage = originalImage };
        var before = System.Text.Json.JsonSerializer.Serialize(configuration);
        var source = new GenericToolResults { Status = ExecutionStatus.Success };
        source.AddResultItem("value", 42d);
        var owner = new ToolExecutionOwner();
        var request = new ToolExecutionRequest(configuration)
        {
            ExecutionOwner = owner, NodeId = "body", CancellationToken = cancellation.Token,
            ResolvedInputImage = inputImage,
            NodeResults = new Dictionary<string, ToolResults> { ["source"] = source },
            ParameterBindings = new(new[] { new ParameterBindingDefinition
            { ParameterName = "Number", BindingType = BindingType.Constant, ConstantValue = 64, TargetType = typeof(int) } })
        };
        var resources = new CountingLease();
        typeof(ToolExecutionRequest).GetProperty("InputResources", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(request, resources);
        var entered = false;
        Results Body(Parameters runtime)
        {
            entered = true;
            Assert.NotSame(configuration, runtime);
            Assert.Equal(64, runtime.Number);
            Assert.Same(inputImage, runtime.ResolvedInputImage);
            Assert.NotSame(choice, runtime.Choice);
            Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(configuration));
            Assert.Same(originalImage, configuration.ResolvedInputImage);
            Assert.Null(configuration.NodeResults);
            Assert.Null(configuration.Context);
            if (mode == 1) { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); }
            if (mode == 2) throw new OperationCanceledException(foreign.Token);
            if (mode == 3) throw new OperationCanceledException();
            return new Results { Status = ExecutionStatus.Success };
        }
        IToolPlugin tool = async ? new AsyncBodyTool(Body) : new BodyTool(Body);
        var executor = new ToolExecutor(new ParameterResolver());
        var result = async ? await executor.ExecuteAsync(tool, request) : executor.Run(tool, request);
        Assert.True(entered);
        Assert.Equal(mode == 0 ? ExecutionStatus.Success : mode == 1 ? ExecutionStatus.Cancelled : ExecutionStatus.Failed, result.Status);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(configuration));
        Assert.Same(originalImage, configuration.ResolvedInputImage);
        Assert.Same(choice, configuration.Choice);
        Assert.Null(configuration.ResolvedPositionCorrection);
        Assert.Equal(1, resources.Count);
        Assert.Equal(0, owner.ActiveRuns);
        Assert.False(result.ExecutionIdentity!.TryInvokeCallback(() => throw new Exception()));
        Assert.Equal(mode != 1, result.ExecutionIdentity.TryPublish(() => { }));
    }
    private class BodyTool(Func<Parameters, Results> body) : IToolPlugin<Parameters, Results>
    { public Results Run(Parameters parameters) => body(parameters); }
    private sealed class AsyncBodyTool(Func<Parameters, Results> body) : BodyTool(body), IAsyncToolPlugin
    { public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request) => ValueTask.FromResult<ToolResults>(Run((Parameters)request.Parameters)); }
    [Theory]
    [InlineData(0, ExecutionStatus.Failed)]
    [InlineData(1, ExecutionStatus.Cancelled)]
    [InlineData(2, ExecutionStatus.Failed)]
    public void CancellationBelongsToTheRequestToken(int mode, ExecutionStatus expected)
    {
        using var requestCancellation = new CancellationTokenSource();
        using var foreignCancellation = new CancellationTokenSource();
        foreignCancellation.Cancel();
        if (mode == 1) requestCancellation.Cancel();
        var owner = new ToolExecutionOwner();
        var request = new ToolExecutionRequest(new Parameters())
        { ExecutionOwner = owner, NodeId = "cancel", CancellationToken = requestCancellation.Token };
        var tool = new CancellationTool(mode == 2 ? foreignCancellation.Token : default);
        var result = new ToolExecutor(new ParameterResolver()).Run((IToolPlugin)tool, request);
        Assert.Equal(expected, result.Status);
        Assert.Equal(0, owner.ActiveRuns);
        Assert.False(result.ExecutionIdentity!.TryInvokeCallback(() => throw new Exception()));
    }
    private sealed class CancellationTool(CancellationToken token) : IToolPlugin<Parameters, Results>
    { public Results Run(Parameters parameters) => throw new OperationCanceledException(token); }
    public sealed class Results : ToolResults { }
    private sealed class CountingLease : IDisposable
    {
        public int Count;
        public void Dispose() => Interlocked.Increment(ref Count);
    }
    private sealed class BodyGate
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Count;
        public async Task<ToolResults> Run()
        {
            Interlocked.Increment(ref Count); Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(15));
            return new Results { Status = ExecutionStatus.Success };
        }
    }
    private sealed class SyncTool(BodyGate gate) : IToolPlugin<Parameters, Results>
    {
        public Results Run(Parameters parameters) => (Results)gate.Run().GetAwaiter().GetResult();
    }
    private sealed class AsyncTool(BodyGate gate) : IToolPlugin<Parameters, Results>, IAsyncToolPlugin
    {
        public Results Run(Parameters parameters) => throw new InvalidOperationException("Unexpected legacy body");
        public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request) => new(gate.Run());
    }
}

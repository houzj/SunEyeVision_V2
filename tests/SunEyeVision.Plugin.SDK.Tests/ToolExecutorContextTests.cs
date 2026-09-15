using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public class ToolExecutorContextTests
{
    [Fact]
    public void Run_UsesExplicitGlobalVariablesInExecutionContext()
    {
        var globalVariables = new Dictionary<string, object?> { ["Exposure"] = 12.5 };
        var tool = new ContextCapturingTool();
        var executor = new ToolExecutor(new ParameterResolver());

        var result = executor.Run<TestParameters, TestResults>(
            tool,
            new TestParameters(),
            globalVariables: globalVariables);

        Assert.True(result.IsSuccess);
        Assert.Same(globalVariables, tool.CapturedGlobalVariables);
        Assert.Equal(12.5, tool.CapturedGlobalVariables!["Exposure"]);
    }

    [Fact]
    public void Run_UsesExplicitCancellationTokenInExecutionContext()
    {
        using var cancellationSource = new CancellationTokenSource();
        var tool = new ContextCapturingTool();
        var executor = new ToolExecutor(new ParameterResolver());

        var result = executor.Run<TestParameters, TestResults>(
            tool,
            new TestParameters(),
            cancellationToken: cancellationSource.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(cancellationSource.Token, tool.CapturedCancellationToken);
    }

    [Fact]
    public void Run_WhenToolThrowsOperationCanceled_ReturnsCancelledInsteadOfFailed()
    {
        var executor = new ToolExecutor(new ParameterResolver());

        var result = executor.Run<TestParameters, TestResults>(
            new CancellingTool(),
            new TestParameters());

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.Equal(ExecutionStatus.Cancelled, result.Status);
        Assert.Equal("执行被取消", result.ErrorMessage);
    }

    [Fact]
    public void CancellationFactories_KeepCancellationAsAnIndependentTerminalState()
    {
        var runResult = RunResult<TestResults>.Cancelled();
        var workflowResult = ExecutionResult.CreateCancelled();

        Assert.Equal(ExecutionStatus.Cancelled, runResult.Status);
        Assert.Equal(ExecutionStatus.Cancelled, workflowResult.Status);
        Assert.True(workflowResult.IsStopped);
        Assert.Empty(workflowResult.Errors);
        Assert.Equal("工作流执行被取消", workflowResult.CompletionMessage);
    }

    [Fact]
    public async Task ExecuteAsync_UsesAsyncContractAndReadOnlyInputs()
    {
        var globalVariables = new Dictionary<string, object?> { ["Recipe"] = "A-01" };
        var executor = new ToolExecutor(new ParameterResolver());
        var tool = new AsyncCapturingTool();

        var result = await executor.ExecuteAsync(tool, new ToolExecutionRequest(new TestParameters())
        {
            GlobalVariables = globalVariables
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("A-01", tool.Recipe);
        Assert.False(tool.LegacyRunWasCalled);
        Assert.Null(tool.ParametersSeen.Context);
        Assert.Null(tool.ParametersSeen.NodeResults);
        Assert.Null(tool.ParametersSeen.ResolvedInputImage);
        Assert.Equal("Test", tool.ContextToolName);
    }

    private sealed class TestParameters : ToolParameters
    {
    }

    private sealed class TestResults : ToolResults
    {
    }

    private sealed class ContextCapturingTool : IToolPlugin<TestParameters, TestResults>
    {
        public IDictionary<string, object?>? CapturedGlobalVariables { get; private set; }
        public CancellationToken CapturedCancellationToken { get; private set; }

        public TestResults Run(TestParameters parameters)
        {
            CapturedGlobalVariables = parameters.Context?.GlobalVariables;
            CapturedCancellationToken = parameters.Context?.CancellationToken ?? default;
            return new TestResults { Status = ExecutionStatus.Success };
        }
    }

    private sealed class CancellingTool : IToolPlugin<TestParameters, TestResults>
    {
        public TestResults Run(TestParameters parameters)
        {
            throw new OperationCanceledException();
        }
    }

    private sealed class AsyncCapturingTool : IToolPlugin<TestParameters, TestResults>, IAsyncToolPlugin
    {
        public string? Recipe { get; private set; }
        public bool LegacyRunWasCalled { get; private set; }
        public TestParameters ParametersSeen { get; private set; } = null!;
        public string? ContextToolName { get; private set; }

        public TestResults Run(TestParameters parameters)
        {
            LegacyRunWasCalled = true;
            throw new InvalidOperationException("异步工具不应走旧 Run 入口。");
        }

        public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        {
            ParametersSeen = (TestParameters)request.Parameters;
            ContextToolName = request.Context.ToolName;
            request.Inputs.TryGetGlobalVariable("Recipe", out string? recipe);
            Recipe = recipe;
            return ValueTask.FromResult<ToolResults>(
                new TestResults { Status = ExecutionStatus.Success });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class SolutionTransitionTests
{
    [Fact]
    public void Unsaved_workspace_transition_routes_to_solution_save_not_single_workflow_export()
    {
        var type = typeof(SunEyeVision.UI.Shell.Windows.MainWindowViewModel);
        var method = type.GetMethod("PrepareSolutionTransitionAsync")!;
        var state = method.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()!.StateMachineType;
        var body = state.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetMethodBody()!.GetILAsByteArray()!;
        bool Calls(string methodName)
        {
            var token = type.GetMethod(methodName, BindingFlags.Instance|BindingFlags.NonPublic)!.MetadataToken;
            for (var i = 0; i + 4 < body.Length; i++)
                if ((body[i] == 0x28 || body[i] == 0x6f) && BitConverter.ToInt32(body, i + 1) == token) return true;
            return false;
        }
        Assert.True(Calls("SaveCurrentDocumentWithPrompt"));
        Assert.False(Calls("ExecuteSaveWorkflow"));
    }

    private static WorkflowExecutionManager Manager() => new(Substitute.For<IInputProvider>(), new WorkflowEngine(Substitute.For<ILogger>()), new RuntimeInputSnapshotService());
    private static object? Invoke(WorkflowExecutionManager manager, string method, params object[] values)
    {
        var target=typeof(WorkflowExecutionManager).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!;
        var arguments=new List<object?>(values);
        foreach(var parameter in target.GetParameters().Skip(values.Length))arguments.Add(parameter.HasDefaultValue?parameter.DefaultValue:throw new InvalidOperationException("测试缺少必要参数"));
        return target.Invoke(manager,arguments.ToArray());
    }
    [Fact]
    public void Transition_blocks_all_execution_acquisition_until_disposed()
    {
        var manager = Manager();
        using (manager.BeginSolutionTransition())
        {
            Assert.False((bool)Invoke(manager, "TryAcquireExecution", "one")!);
            Assert.Throws<InvalidOperationException>(() => manager.BeginSolutionTransition());
        }
        Assert.True((bool)Invoke(manager, "TryAcquireExecution", "one")!);
        Invoke(manager, "ReleaseExecution", "one");
        Assert.False(manager.IsAnyRunning());
    }
    [Fact]
    public async Task Stop_timeout_keeps_transition_blocked_and_original_execution_visible()
    {
        var manager = Manager();
        Assert.True((bool)Invoke(manager, "TryAcquireExecution", "running")!);
        using var transition = manager.BeginSolutionTransition();
        await Assert.ThrowsAsync<TimeoutException>(() => manager.StopAndWaitAsync(TimeSpan.FromMilliseconds(1)));
        Assert.True(manager.IsAnyRunning());
        Assert.False((bool)Invoke(manager, "TryAcquireExecution", "new")!);
        Invoke(manager, "ReleaseExecution", "running");
        await manager.StopAndWaitAsync(TimeSpan.FromSeconds(1));
    }
    [Fact]
    public async Task Cancellation_callbacks_can_finish_on_another_thread_without_execution_lock_deadlock()
    {
        var manager = Manager();
        Invoke(manager, "TryAcquireExecution", "running");
        var tokens = (Dictionary<string, CancellationTokenSource>)typeof(WorkflowExecutionManager).GetField("_cancellationTokens", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager)!;
        using var source = new CancellationTokenSource(); tokens.Add("running", source);
        using var registration = source.Token.Register(() =>
        {
            var completion = Task.Run(() => Invoke(manager, "ReleaseExecution", "running"));
            Assert.True(completion.Wait(TimeSpan.FromSeconds(2)), "取消回调被执行锁阻塞");
        });
        using var transition = manager.BeginSolutionTransition();
        await manager.StopAndWaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(source.IsCancellationRequested); Assert.False(manager.IsAnyRunning());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Single_and_all_stop_do_not_hold_execution_lock_during_cancellation(bool stopAll)
    {
        var manager=Manager();
        var tokens=(Dictionary<string,CancellationTokenSource>)typeof(WorkflowExecutionManager)
            .GetField("_cancellationTokens",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(manager)!;
        using var source=new CancellationTokenSource();tokens.Add("running",source);
        using var registration=source.Token.Register(()=>
        {
            var otherThread=Task.Run(()=>manager.IsRunning("running"));
            Assert.True(otherThread.Wait(TimeSpan.FromSeconds(2)),"停止时取消回调被执行锁阻塞");
        });
        var tab=new SunEyeVision.UI.Features.WorkflowUI.WorkflowTabViewModel{Id="running",Name="running"};
        if(stopAll)manager.StopAllContinuousRun();else Assert.True(manager.StopContinuousRun(tab));
        Assert.True(source.IsCancellationRequested);
        tokens.Clear();manager.Dispose();
    }

    [Fact]
    public void Stop_all_requests_cancellation_for_remaining_runs_when_one_callback_fails()
    {
        var manager=Manager();
        var tokens=(Dictionary<string,CancellationTokenSource>)typeof(WorkflowExecutionManager)
            .GetField("_cancellationTokens",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(manager)!;
        using var failing=new CancellationTokenSource();using var other=new CancellationTokenSource();
        tokens.Add("first",failing);tokens.Add("second",other);
        using var registration=failing.Token.Register(()=>throw new InvalidOperationException("callback"));
        Assert.Throws<AggregateException>(()=>manager.StopAllContinuousRun());
        Assert.True(failing.IsCancellationRequested);Assert.True(other.IsCancellationRequested);
        tokens.Clear();manager.Dispose();
    }
}

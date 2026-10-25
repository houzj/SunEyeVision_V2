using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class WorkflowDocumentPreparationTests
{
    private static (WorkflowExecutionManager Manager, WorkflowTabViewModel Tab, Solution Document) Fixture()
    {
        SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.InitializeSolutionManager(
            System.IO.Path.Combine(AppContext.BaseDirectory, "s1-preparation-data"));
        var engine = new WorkflowEngine(Substitute.For<ILogger>());
        var flow = engine.CreateWorkflow(Guid.NewGuid().ToString(), "document preparation");
        var input = Substitute.For<IInputProvider>(); input.GetInputImageAsync().Returns(Task.FromResult<object?>(null));
        var manager = new WorkflowExecutionManager(input, engine, new RuntimeInputSnapshotService());
        return (manager, new WorkflowTabViewModel { Id = flow.Id, Name = flow.Name }, new Solution { Workflows = new() { flow } });
    }
    [Fact]
    public void RunSingle_and_solution_prepare_document_once_before_nested_execution() => Sta(() =>
    {
        var f = Fixture(); var manager = f.Manager; using var cleanup = new ManagerCleanup(manager); var calls = 0;
        manager.PrepareDocumentEditors = () => { Assert.False(Monitor.IsEntered(GlobalVariable.ValueGate)); calls++; };
        manager.RunSingleAsync(f.Tab).GetAwaiter().GetResult(); Assert.Equal(1, calls);
        calls = 0;
        manager.RunSolutionAsync(new[] { f.Tab }, f.Document).GetAwaiter().GetResult(); Assert.Equal(1, calls);
    });
    [Fact]
    public void Missing_production_document_entry_rejects_before_input_or_legacy_flush() => Sta(() =>
    {
        var f = Fixture(); var manager = f.Manager; using var cleanup = new ManagerCleanup(manager); string? error = null;
        manager.WorkflowExecutionError += (_, e) => error = e.ErrorMessage;
        manager.RunSingleAsync(f.Tab).GetAwaiter().GetResult();
        Assert.NotNull(error); Assert.Contains("统一编辑事务入口", error);
    });
    [Fact]
    public void Communication_admission_prepares_once_and_nested_run_does_not_prepare_again() => Sta(() =>
    {
        var f = Fixture(); var manager = f.Manager; using var cleanup = new ManagerCleanup(manager); var calls = 0;
        manager.PrepareDocumentEditors = () => calls++;
        Assert.True(manager.RunCommunicationOperationAsync(token => manager.RunSingleAsync(f.Tab, token), CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal(1, calls);
    });
    [Fact]
    public void Continuous_admission_prepares_once_per_iteration() => Sta(() =>
    {
        var f = Fixture(); var manager = f.Manager; using var cleanup = new ManagerCleanup(manager); var calls = 0; var completed = 0;
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? failure = null;
        manager.PrepareDocumentEditors = () => Interlocked.Increment(ref calls);
        manager.WorkflowExecutionCompleted += (_, _) => { if (Interlocked.Increment(ref completed) == 2) manager.StopContinuousRun(f.Tab); };
        manager.WorkflowExecutionStopped += (_, _) => stopped.TrySetResult();
        manager.WorkflowExecutionError += (_, e) => { failure = e.ErrorMessage; stopped.TrySetResult(); };
        Assert.True(manager.StartContinuousRun(f.Tab));
        Assert.True(stopped.Task.Wait(TimeSpan.FromSeconds(10)), "Continuous preparation did not finish.");
        Assert.Null(failure); Assert.Equal(2, completed); Assert.Equal(2, calls);
    });
    private sealed class ManagerCleanup(WorkflowExecutionManager manager) : IDisposable { public void Dispose() => manager.Dispose(); }
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(25)), "Run preparation test timed out.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

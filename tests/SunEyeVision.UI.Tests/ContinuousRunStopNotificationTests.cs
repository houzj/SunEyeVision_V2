using NSubstitute;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ContinuousRunStopNotificationTests
{
    [Fact]
    public async Task StopBetweenIterationsPublishesStoppedAndReleasesExecution()
    {
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        WorkflowExecutionManager? manager = null;
        WorkflowTabViewModel? tab = null;
        Exception? setupError = null;
        var thread = new Thread(() =>
        {
            try
            {
                var input = Substitute.For<IInputProvider>();
                input.GetInputImageAsync().Returns(Task.FromResult<object?>(null));
                var engine = new WorkflowEngine(Substitute.For<ILogger>());
                var workflow = engine.CreateWorkflow(Guid.NewGuid().ToString(), "停止通知测试");
                tab = new WorkflowTabViewModel { Id = workflow.Id, Name = workflow.Name };
                manager = new WorkflowExecutionManager(input, engine, new RuntimeInputSnapshotService()) { AllowLegacyEditorCommitForTests = true };
                manager.WorkflowExecutionCompleted += (_, _) => manager.StopContinuousRun(tab);
                manager.WorkflowExecutionStopped += (_, _) => stopped.TrySetResult();
                manager.WorkflowExecutionError += (_, args) => failed.TrySetResult(args.ErrorMessage ?? "执行错误");
                Assert.True(manager.StartContinuousRun(tab));
            }
            catch (Exception ex)
            {
                setupError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "启动测试超时");
        if (setupError != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(setupError).Throw();

        var first = await Task.WhenAny(stopped.Task, failed.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(stopped.Task, first);
        Assert.False(manager!.IsRunning(tab!.RootWorkflowId));
    }
}

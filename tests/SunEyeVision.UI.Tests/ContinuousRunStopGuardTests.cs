using SunEyeVision.UI.Features.WorkflowUI;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ContinuousRunStopGuardTests
{
    [Fact]
    public void RapidClickAfterConfirmedStopDoesNotRestartWorkflow()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var tabs = new WorkflowTabControlViewModel();
                var workflow = tabs.SelectedTab!;
                var starts = 0;
                var stops = 0;
                tabs.StartContinuousExecution = _ => { starts++; return true; };
                tabs.StopContinuousExecution = _ => { stops++; return true; };

                tabs.ToggleContinuous(workflow);
                tabs.ToggleContinuous(workflow);
                tabs.ToggleContinuous(workflow); // 重复停止点击
                workflow.IsRunning = false;       // 执行管理器确认取消
                tabs.ToggleContinuous(workflow); // 窗口消息队列里紧接着的点击

                Assert.Equal(1, starts);
                Assert.Equal(1, stops);
                Assert.False(workflow.IsRunning);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "连续运行交互测试超时");
        if (error != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services.Communication;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    internal void BindCommunicationToCurrentSession() => InitializeCommunicationAutomation();
    private void InitializeCommunicationAutomation()
    {
        if(DocumentManager.CurrentSession is null)return;
        var automation = ServiceInitializer.CommunicationService.Automation;
        automation.Dispatcher = DispatchCommunicationAsync;

    }

    private void PublishCommunicationWorkflowState(string workflowId, bool busy)
    {
        var solution = ServiceInitializer.SolutionManager.CurrentSession?.Document;
        if (solution == null || !solution.Workflows.Any(w => !w.IsSubroutine && w.Id == workflowId)) return;
        var service = ServiceInitializer.CommunicationService;
        if (!solution.CommunicationResponses.WorkflowIdentifiers.ContainsKey(workflowId))
            service.RefreshAutomationConfiguration(solution);
        service.Automation.WorkflowStateChanged(workflowId, busy);
    }

    // 触发执行必须经同一正式 session 的执行快照和已准入设置声明；
    // 在执行宿主接通前明确拒绝，不退回旧 Workflow 节点或按 ID 猜测。
    private Task<bool> DispatchCommunicationAsync(CommunicationTriggerRequest request, CancellationToken token)
        => Task.FromException<bool>(new InvalidOperationException("通讯触发执行尚未接入正式工作流执行会话。"));
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services.Communication;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    private void InitializeCommunicationAutomation()
    {
        var automation = ServiceInitializer.CommunicationService.Automation;
        automation.Dispatcher = DispatchCommunicationAsync;
        _executionManager.WorkflowExecutionStarted += (_, e) => PublishCommunicationWorkflowState(e.WorkflowId, true);
        _executionManager.WorkflowExecutionCompleted += (_, e) => PublishCommunicationWorkflowState(e.WorkflowId, false);
        _executionManager.WorkflowExecutionStopped += (_, e) => PublishCommunicationWorkflowState(e.WorkflowId, false);
        _executionManager.WorkflowExecutionError += (_, e) => PublishCommunicationWorkflowState(e.WorkflowId, false);
    }

    private void PublishCommunicationWorkflowState(string workflowId, bool busy)
    {
        var solution = ServiceInitializer.SolutionManager.CurrentSolution;
        if (solution == null || !solution.Workflows.Any(w => !w.IsSubroutine && w.Id == workflowId)) return;
        var service = ServiceInitializer.CommunicationService;
        if (!solution.CommunicationResponses.WorkflowIdentifiers.ContainsKey(workflowId))
            service.RefreshAutomationConfiguration(solution);
        service.Automation.WorkflowStateChanged(workflowId, busy);
    }

    private Task<bool> DispatchCommunicationAsync(CommunicationTriggerRequest request, CancellationToken token)
        => request.Command == "切换方案"
            ? SwitchCommunicationSolutionAsync(request.TargetId, token)
            : _executionManager.RunCommunicationOperationAsync(async cancellation =>
        {
            var dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException("工作流宿主尚未初始化。");
            var launch = await dispatcher.InvokeAsync(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                var solution = ServiceInitializer.SolutionManager.CurrentSolution
                    ?? throw new InvalidOperationException("当前没有解决方案。");
                _executionManager.SetRuntimeParameters(GetAllRuntimeParameters());
                _executionManager.SetGlobalVariables(solution.GlobalVariables.ToDictionary(v => v.Name, v => v.Value));
                var tabs = WorkflowTabViewModel.Tabs.Where(t => solution.Workflows.Any(w => !w.IsSubroutine && w.Id == t.RootWorkflowId)).ToArray();
                foreach (var assignment in request.Assignments)
                {
                    var flow = solution.Workflows.FirstOrDefault(w => !w.IsSubroutine && w.Nodes.Any(n => n.Id == assignment.NodeId));
                    var node = flow?.Nodes.FirstOrDefault(n => n.Id == assignment.NodeId);
                    if (node == null || (request.Command == "执行流程" && request.TargetId != "*" && flow!.Id != request.TargetId) ||
                        (request.Command == "执行模块" && node.Id != request.TargetId))
                        throw new InvalidOperationException("参数绑定目标不在本次执行范围内。");
                    CommunicationTriggerEvaluation.ValidateTarget(node, assignment);
                }
                return (Func<Task>)(async () =>
                {
                    switch (request.Command)
                    {
                        case "执行流程":
                            if (request.TargetId == "*") await _executionManager.RunSolutionAsync(tabs, solution, cancellation);
                            else
                            {
                                var tab = tabs.FirstOrDefault(t => t.RootWorkflowId == request.TargetId)
                                    ?? throw new InvalidOperationException("触发目标流程已失效。");
                                await _executionManager.RunSingleAsync(tab, cancellation);
                            }
                            break;
                        case "执行模块":
                            var workflow = solution.Workflows.FirstOrDefault(w => w.Nodes.Any(n => n.Id == request.TargetId))
                                ?? throw new InvalidOperationException("触发模块已失效。");
                            await _executionManager.RunCommunicationNodeAsync(workflow.Id, request.TargetId, cancellation);
                            break;
                        default:
                            throw new NotSupportedException("当前工具 SDK 未提供通用模块动作契约，不能执行此命令。");
                    }
                });
            }, System.Windows.Threading.DispatcherPriority.Normal, cancellation);
            cancellation.ThrowIfCancellationRequested();
            await launch();
        }, token, request.Assignments);

    private async Task<bool> SwitchCommunicationSolutionAsync(string solutionId, CancellationToken token)
    {
        // Do not hold the communication execution lease while waiting for all work to stop.
        // The shared transition entry acquires its own gate and preserves unsaved edits.
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("工作流宿主尚未初始化。");
        var transition = await dispatcher.InvokeAsync(async () =>
        {
            token.ThrowIfCancellationRequested();
            if (_executionManager.IsAnyRunning() || !CanAcceptExternalActivation) return false;
            var metadata = ServiceInitializer.SolutionManager.GetMetadata(solutionId)
                ?? throw new InvalidOperationException("触发目标方案已失效。");
            return await OpenExternalSolutionAsync(metadata.FilePath);
        }, System.Windows.Threading.DispatcherPriority.Normal, token);
        return await transition;
    }
}

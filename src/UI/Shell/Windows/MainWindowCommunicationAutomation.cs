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
        => _executionManager.RunCommunicationOperationAsync(async cancellation =>
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
                        case "切换方案":
                            var metadata = ServiceInitializer.SolutionManager.GetMetadata(request.TargetId)
                                ?? throw new InvalidOperationException("触发目标方案已失效。");
                            var loaded = await Task.Run(() => ServiceInitializer.SolutionManager.LoadSolutionOnly(metadata.FilePath), cancellation)
                                ?? throw new InvalidOperationException("触发目标方案加载失败，保留当前方案。");
                            cancellation.ThrowIfCancellationRequested();
                            await dispatcher.InvokeAsync(() => ServiceInitializer.SolutionManager.SetCurrentSolution(loaded),
                                System.Windows.Threading.DispatcherPriority.Normal, cancellation);
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
        }, token);
}

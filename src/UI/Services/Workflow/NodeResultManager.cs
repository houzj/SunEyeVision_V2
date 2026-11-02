using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Workflow;
using WorkflowCore=SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Workflow;

/// <summary>Maintains scope-owned results. Image and overlay ownership belongs to the result workspace.</summary>
public sealed class NodeResultManager
{
    private readonly Func<WorkflowContext?> _getContext;
    private readonly INodeResultSink _viewModel;
    public NodeResultManager(Func<WorkflowContext?> getContext,INodeResultSink viewModel)
    {_getContext=getContext??throw new ArgumentNullException(nameof(getContext));_viewModel=viewModel??throw new ArgumentNullException(nameof(viewModel));}
        public void UpdateNodeResult(WorkflowNodeBase node, DeclaredOutputSnapshot result, bool refreshDisplay = true, WorkflowCore.WorkflowContext? targetContext = null)
        {
            if (node == null || result == null)
                return;
            if (result.ExecutionIdentity?.IsCurrent == false) return;

            // 1. 缓存结果到 WorkflowContext
            var executionResult = new NodeExecutionResult
            {
                NodeId = node.Id,
                // ★ 状态取自结果本身，不得硬编码成功（否则失败态被覆盖为成功，状态显示失真）
                Success = result.IsSuccess,
                Status = result.Status,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now,
                Result = result
            };

            var context = targetContext ?? _getContext();
            if (result.ExecutionIdentity is { } identity)
            {
                if (!identity.TryPublish(() => context?.SetNodeResult(node.Id, executionResult))) return;
            }
            else context?.SetNodeResult(node.Id, executionResult);

            // 2. 如果当前节点被选中，立即刷新UI（图像显示由 ViewModel 统一解析）
            if (refreshDisplay)
            {
                RefreshResultDisplay(node, result);
            }
        }


    public void RefreshResultDisplay(WorkflowNodeBase node,DeclaredOutputSnapshot result)
    {
        using var lease=result.Retain();
        if(result.ExecutionIdentity?.IsCurrent==false)return;
        _viewModel.UpdateModuleResults(result,node.Name,node.ToolType,node.Id);
    }
    public void RefreshDebugPreview(WorkflowNodeBase draft,DeclaredOutputSnapshot result)
    {
        using var lease=result.Retain();
        if(result.ExecutionIdentity?.IsCurrent==false)return;
        _viewModel.UpdateModuleResults(result,draft.Name,draft.ToolType,draft.Id);
    }
    public void ClearResultDisplay()=>_viewModel.ClearResultDisplay();
    public void ClearResultGraphics()=>_viewModel.ClearResultGraphics();
    public void ClearHighlight()=>_viewModel.ClearResultHighlight();
}

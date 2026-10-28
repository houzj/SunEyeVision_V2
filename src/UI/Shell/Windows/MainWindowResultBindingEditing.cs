using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    internal bool TryBindResultVariable(SolutionSession expected,ProducerBinding request,string variableId,out string error)
    {
        error="";
        try
        {
            if(!ReferenceEquals(DocumentManager.Session,expected)||!CanEditRootWorkspace)
                throw new InvalidOperationException("方案或编辑状态已改变，请重新选择当前结果。");
            var root=expected.Document.Workflows.SingleOrDefault(flow=>!flow.IsSubroutine&&flow.Id==request.WorkflowId)
                ??throw new InvalidOperationException("结果所属根流程已失效。");
            var node=root.Nodes.SingleOrDefault(node=>node.Id==request.NodeId)
                ??throw new InvalidOperationException("结果所属节点已删除。");
            if(request.SourceFieldId is null || request.SourceFieldId == Guid.Empty)
                throw new InvalidOperationException("该结果没有正式字段身份，不能建立变量绑定。");
            var host=GetProductionEditorHost(expected);
            var draft=host.BeginVariableEdit();
            var variable=draft.Configuration.GlobalVariables.SingleOrDefault(item=>item.Id==variableId)
                ??throw new InvalidOperationException("所选变量已不属于当前方案。");
            variable.ProducerBinding=new()
            {
                WorkflowId=root.Id,WorkflowName=root.Name,NodeId=node.Id,NodeName=node.DispName,
                ResultName=request.ResultName,SourceFieldId=request.SourceFieldId,ResultDisplayName=request.ResultDisplayName
            };
            var publication=host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),draft);
            try { RefreshProductionPublication(expected,publication); }
            catch(Exception refreshError) { try { _logger?.LogError("结果绑定已接纳，显示刷新失败","MainWindowViewModel",refreshError); } catch { } }
            return true;
        }
        catch(Exception failure) { error=failure.Message; return false; }
    }
}

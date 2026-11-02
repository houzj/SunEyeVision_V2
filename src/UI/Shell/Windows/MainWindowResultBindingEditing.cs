using SunEyeVision.Workflow;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    internal bool TryBindResultVariable(SolutionSession expected,ProducerBinding request,string variableId,out string error)
        =>TryEditResultVariable(expected,request,variableId,false,out error);
    internal bool TryUnbindResultVariable(SolutionSession expected,ProducerBinding request,string variableId,out string error)
        =>TryEditResultVariable(expected,request,variableId,true,out error);
    private bool TryEditResultVariable(SolutionSession expected,ProducerBinding request,string variableId,bool remove,out string error)
    {
        error="";
        try
        {
            if(!ReferenceEquals(DocumentManager.Session,expected)||!CanEditRootWorkspace)
                throw new InvalidOperationException("方案或编辑状态已改变，请重新选择当前结果。");
            var host=GetProductionEditorHost(expected);
            var draft=host.BeginVariableEdit();
            var variable=draft.Configuration.GlobalVariables.SingleOrDefault(item=>item.Id==variableId)
                ??throw new InvalidOperationException("所选变量已不属于当前方案。");
            var validation=GlobalProducerBindingRules.Validate(expected.Document,request,variable,expected.Document.RuntimeDirectory ?? throw new InvalidOperationException("方案缺少已准入运行目录。"));
            if(validation.State is not (BindingCompatibility.Compatible or BindingCompatibility.Conditional))
                throw new InvalidOperationException(validation.Reason);
            if(remove)
            {
                if(variable.ProducerBinding?.ReferenceExpression!=request.ReferenceExpression)
                    throw new InvalidOperationException("变量来源已改变，不能解除其它结果的绑定。");
                variable.ProducerBinding=null;
            }
            else variable.ProducerBinding=new(){ReferenceExpression=request.ReferenceExpression,
                WorkflowName=request.WorkflowName,NodeName=request.NodeName,ResultDisplayName=request.ResultDisplayName};
            var publication=host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),draft);
            try { RefreshProductionPublication(expected,publication); }
            catch(Exception refreshError) { try { _logger?.LogError("结果绑定已接纳，显示刷新失败","MainWindowViewModel",refreshError); } catch { } }
            return true;
        }
        catch(Exception failure) { error=failure.Message; return false; }
    }
}

using System;
using System.Linq;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    internal bool TryBindResultVariable(DocumentSession expected,ProducerBinding request,string variableId,out string error)=>TryEditResultVariable(expected,request,variableId,false,out error);
    internal bool TryUnbindResultVariable(DocumentSession expected,ProducerBinding request,string variableId,out string error)=>TryEditResultVariable(expected,request,variableId,true,out error);
    private bool TryEditResultVariable(DocumentSession expected,ProducerBinding request,string variableId,bool remove,out string error)
    {
        try
        {
            if(!ReferenceEquals(DocumentManager.CurrentSession,expected)||!CanEditRootWorkspace)throw new InvalidOperationException("方案或编辑状态已变化。");
            var host=expected.Editing.Host;using var draft=host.BeginVariableEdit();
            var variable=draft.Variables.SingleOrDefault(item=>item.Id==variableId)??throw new InvalidOperationException("变量已退出当前方案。");
            if(remove&&variable.ProducerBinding?.ReferenceExpression!=request.ReferenceExpression)throw new InvalidOperationException("变量已改绑其他结果。");
            variable.ProducerBinding=remove?null:new ProducerBinding{ReferenceExpression=request.ReferenceExpression,WorkflowName=request.WorkflowName,NodeName=request.NodeName,ResultDisplayName=request.ResultDisplayName};
            var proposal=DocumentEditProposal.Prepare(host,variables:draft);
            expected.ValidateEdit(proposal);
            RefreshProductionPublication(expected,expected.CommitEdit(proposal));
            error="";return true;
        }
        catch(Exception failure){error=failure.Message;return false;}
    }
}

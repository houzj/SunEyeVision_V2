using System;
using System.Linq;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    internal bool TryBindResultVariable(DocumentSession expected,ProducerBinding request,GlobalVariable actualVariable,out string error)=>TryEditResultVariable(expected,request,actualVariable,false,out error);
    internal bool TryUnbindResultVariable(DocumentSession expected,ProducerBinding request,GlobalVariable actualVariable,out string error)=>TryEditResultVariable(expected,request,actualVariable,true,out error);
    private bool TryEditResultVariable(DocumentSession expected,ProducerBinding request,GlobalVariable actualVariable,bool remove,out string error)
    {
        try
        {
            if(!ReferenceEquals(DocumentManager.CurrentSession,expected)||!CanEditRootWorkspace)throw new InvalidOperationException("方案或编辑状态已变化。");
            if(!expected.Document.GlobalVariables.Any(item=>ReferenceEquals(item,actualVariable)))throw new InvalidOperationException("变量实例已退出当前方案。");
            var host=expected.Editing.Host;using var draft=host.BeginVariableEdit();
            var variable=draft.Variables.SingleOrDefault(item=>item.Id==actualVariable.Id)??throw new InvalidOperationException("变量已退出当前方案。");
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

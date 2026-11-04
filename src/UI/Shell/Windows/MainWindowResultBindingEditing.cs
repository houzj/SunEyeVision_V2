using System;
using System.Linq;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.UI.Features.GlobalVariableManager;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    internal bool CanEditResultBindings=>DocumentManager.CurrentSession is not null&&!IsRunning&&!_solutionTransitionPrepared&&!_transitionPreparationInProgress&&_openDebugWindow?.IsLoaded!=true;
    internal bool IsResultVariableCandidate(DocumentSession expected,ProducerBinding request,GlobalVariable actualVariable)
    {
        if(!ReferenceEquals(DocumentManager.CurrentSession,expected)||!CanEditResultBindings||!expected.Document.GlobalVariables.Any(item=>ReferenceEquals(item,actualVariable)))return false;
        try
        {
            using var draft=expected.Editing.Host.BeginVariableEdit();
            var candidate=draft.Variables.Single(item=>item.Id==actualVariable.Id);
            candidate.ProducerBinding=CopyProducer(request);
            expected.ValidateEdit(DocumentEditProposal.Prepare(expected.Editing.Host,variables:draft));
            return true;
        }
        catch(InvalidOperationException){return false;}
        catch(ArgumentException){return false;}
    }
    private static ProducerBinding CopyProducer(ProducerBinding request)=>new(){ReferenceExpression=request.ReferenceExpression,WorkflowName=request.WorkflowName,NodeName=request.NodeName,ResultDisplayName=request.ResultDisplayName};
    internal bool TryBindResultVariable(DocumentSession expected,ProducerBinding request,GlobalVariable actualVariable,out string error)=>TryEditResultVariable(expected,request,actualVariable,false,out error);
    internal bool TryUnbindResultVariable(DocumentSession expected,ProducerBinding request,GlobalVariable actualVariable,out string error)=>TryEditResultVariable(expected,request,actualVariable,true,out error);
    private bool TryEditResultVariable(DocumentSession expected,ProducerBinding request,GlobalVariable actualVariable,bool remove,out string error)
    {
        GlobalVariableManagerViewModel? temporary=null;
        GlobalVariable? candidate=null;ProducerBinding? old=null;bool committed=false;var openingRevision=expected.ChangeRevision;
        try
        {
            if(!ReferenceEquals(DocumentManager.CurrentSession,expected)||!CanEditResultBindings)throw new InvalidOperationException("方案或编辑状态已变化。");
            if(!expected.Document.GlobalVariables.Any(item=>ReferenceEquals(item,actualVariable)))throw new InvalidOperationException("变量实例已退出当前方案。");
            if(!remove&&!IsResultVariableCandidate(expected,request,actualVariable))throw new InvalidOperationException("所选变量声明或存储不能承载该正式结果。");
            var editor=_variableEditors.SingleOrDefault(item=>item.IsValid&&ReferenceEquals(item.EditingSession,expected));
            if(editor is null)editor=temporary=new GlobalVariableManagerViewModel(expected,()=>ReferenceEquals(DocumentManager.CurrentSession,expected),()=>CanEditResultBindings);
            // Flush every registered participant before adding the requested producer to its one real variable draft.
            expected.Editing.Validate();
            candidate=editor.DraftVariables.SingleOrDefault(item=>item.Id==actualVariable.Id)??throw new InvalidOperationException("当前变量编辑已删除此变量。");
            old=candidate.ProducerBinding;
            if(remove&&old?.ReferenceExpression!=request.ReferenceExpression)throw new InvalidOperationException("变量已改绑其他结果。");
            candidate.ProducerBinding=remove?null:CopyProducer(request);
            var receipt=expected.Editing.Apply();committed=true;
            RefreshProductionPublication(expected,receipt);error="";return true;
        }
        catch(AggregateException publishedFailure) when(publishedFailure.Message.StartsWith("编辑已提交",StringComparison.Ordinal))
        {
            committed=true;error="结果绑定已提交，但编辑视图刷新失败："+publishedFailure.Message;StatusText=error;return true;
        }
        catch(Exception failure)
        {
            // On a rejected candidate retain all user edits; remove only this operation's uncommitted binding.
            if(!committed&&candidate is not null&&ReferenceEquals(DocumentManager.CurrentSession,expected)&&expected.ChangeRevision==openingRevision)candidate.ProducerBinding=old;
            error=failure.Message;return false;
        }
        finally{temporary?.Discard();}
    }
}

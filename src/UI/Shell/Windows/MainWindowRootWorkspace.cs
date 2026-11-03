using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.UI.Features.WorkflowUI;
using System;
using System.Linq;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private void RefreshRootTabReadiness(){foreach(var tab in WorkflowTabViewModel.Tabs)tab.RefreshRootReadiness();}
    public bool CanEditRootWorkspace=>CanEditRootWorkspaceState&&!IsDebugWindowOpen;
    private bool CanEditRootWorkspaceState=>DocumentManager.CurrentSession is not null&&!IsRunning&&!_solutionTransitionPrepared&&!_transitionPreparationInProgress&&DocumentManager.CurrentSession.Editing.HasPendingChanges!=true;
    public bool CanUndoRootWorkspace=>CanEditRootWorkspace&&DocumentManager.CurrentSession?.CanUndo==true;
    public bool CanRedoRootWorkspace=>CanEditRootWorkspace&&DocumentManager.CurrentSession?.CanRedo==true;
    internal void UndoRootWorkspace(){try{if(CanUndoRootWorkspace)DocumentManager.CurrentSession!.Undo();}catch(Exception error){StatusText="撤销已停止："+error.Message;}}
    internal void RedoRootWorkspace(){try{if(CanRedoRootWorkspace)DocumentManager.CurrentSession!.Redo();}catch(Exception error){StatusText="重做已停止："+error.Message;}}
    internal bool AddRootWorkspace()=>ApplyRootCollection("新增流程",(session,draft)=>
        draft.Add(AvailableRootName(session),session.GetWorkflows().Count(flow=>!flow.IsSubroutine)));
    internal bool CopyRootWorkspace(WorkflowTabViewModel tab,System.Windows.Point? overviewPosition=null)
    {
        var root=ResolveCollectionRoot(tab);if(root is null){StatusText="复制源流程已退出当前方案，或正在执行。";return false;}
        return ApplyRootCollection("复制流程",(session,draft)=>draft.Copy(root,AvailableRootName(session,root),
            Array.IndexOf(session.GetWorkflows().Where(flow=>!flow.IsSubroutine).ToArray(),root)+1,
            overviewPosition is {} point?new CanvasPoint(point.X,point.Y):null));
    }
    private bool RenameRootWorkspace(WorkflowTabViewModel tab,string name)
    {
        var root=ResolveCollectionRoot(tab);if(root is null){StatusText="名称未提交：流程已退出当前方案，或正在执行。";return false;}
        try{if(RootWorkflowNameRules.Normalize(name)==root.Name)return true;}
        catch(Exception error){StatusText="名称未提交："+error.Message;return false;}
        return ApplyRootCollection("重命名流程",(_,draft)=>{draft.Rename(root,name);return null;});
    }
    internal bool DeleteRootWorkspace(WorkflowTabViewModel tab)
    {
        var root=ResolveCollectionRoot(tab);if(root is null){StatusText="删除源流程已退出当前方案，或正在执行。";return false;}
        return ApplyRootCollection("删除流程",(_,draft)=>{draft.Remove(root);return null;});
    }
    internal WorkflowDefinition? ResolveEditableRoot(WorkflowTabViewModel tab)
    {
        if(!CanEditWorkflowCanvas||tab.IsInSubroutine||!WorkflowTabViewModel.Tabs.Contains(tab))return null;
        return DocumentManager.CurrentSession?.GetWorkflows().SingleOrDefault(flow=>flow.Id==tab.RootWorkflowId&&!flow.IsSubroutine);
    }
    internal sealed record RootSettingsAdmission(DocumentSession Session,WorkflowDefinition Root,RootWorkspaceSettings Opening);
    internal RootSettingsAdmission? PrepareRootSettings(WorkflowTabViewModel tab)=>ResolveEditableRoot(tab) is {} root&&DocumentManager.CurrentSession is {} session?new(session,root,root.WorkspaceSettings):null;
    internal bool ChangeRootSettings(WorkflowTabViewModel tab,RootSettingsAdmission admission,RootWorkspaceSettings settings)
        =>ReferenceEquals(DocumentManager.CurrentSession,admission.Session)&&ChangeRootSettings(tab,admission.Root,admission.Opening,settings);
    internal bool ChangeRootSettings(WorkflowTabViewModel tab,WorkflowDefinition expected,RootWorkspaceSettings opening,RootWorkspaceSettings settings)
    {
        if(!ReferenceEquals(ResolveEditableRoot(tab),expected)||expected.WorkspaceSettings!=opening)return false;
        try
        {
            var session=DocumentManager.CurrentSession!;var draft=session.Editing.Host.BeginRootEdit(expected.Id);
            draft.WorkspaceSettings=draft.WorkspaceSettings with {RunIntervalMs=settings.RunIntervalMs,StopOnNg=settings.StopOnNg};
            var proposal=DocumentEditProposal.Prepare(session.Editing.Host,roots:new[]{draft});
            session.ValidateEdit(proposal);RefreshProductionPublication(session,session.CommitEdit(proposal));return true;
        }
        catch(Exception error){StatusText="流程运行设置未接纳："+error.Message;return false;}
    }
}

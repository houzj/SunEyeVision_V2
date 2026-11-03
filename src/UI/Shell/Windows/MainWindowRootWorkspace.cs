using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.UI.Features.WorkflowUI;
using System;
using System.Linq;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private void RefreshRootTabReadiness(){foreach(var tab in WorkflowTabViewModel.Tabs)tab.RefreshRootReadiness();}
    public bool CanEditRootWorkspace=>CanEditRootWorkspaceState&&!IsDebugWindowOpen;
    private bool CanEditRootWorkspaceState=>DocumentManager.CurrentSession is not null&&!IsRunning&&_solutionTransitionLease is null&&!_transitionPreparationInProgress&&!_executionManager.IsAnyRunning()&&DocumentManager.CurrentSession.Editing.HasPendingChanges!=true;
    public bool CanUndoRootWorkspace=>CanEditRootWorkspace&&DocumentManager.CurrentSession?.CanUndo==true;
    public bool CanRedoRootWorkspace=>CanEditRootWorkspace&&DocumentManager.CurrentSession?.CanRedo==true;
    internal void UndoRootWorkspace(){try{if(CanUndoRootWorkspace)DocumentManager.CurrentSession!.Undo();}catch(Exception error){StatusText="撤销已停止："+error.Message;}}
    internal void RedoRootWorkspace(){try{if(CanRedoRootWorkspace)DocumentManager.CurrentSession!.Redo();}catch(Exception error){StatusText="重做已停止："+error.Message;}}
    private bool UnavailableRootCollection(){StatusText="流程目录增删、复制、改名尚未接入正式会话事务，暂不可用。";return false;}
    internal bool AddRootWorkspace()=>UnavailableRootCollection();
    internal bool CopyRootWorkspace(WorkflowTabViewModel tab,System.Windows.Point? overviewPosition=null)=>UnavailableRootCollection();
    private bool RenameRootWorkspace(WorkflowTabViewModel tab,string name)=>UnavailableRootCollection();
    internal bool DeleteRootWorkspace(WorkflowTabViewModel tab)=>UnavailableRootCollection();
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
        StatusText="流程运行设置正在接入正式会话事务，当前不可提交。";return false;
    }
}

using SunEyeVision.Workflow;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.UI.Controls;
using NodeFactory=SunEyeVision.UI.Services.Workflow.WorkflowNodeFactory;
using Flow=SunEyeVision.Workflow.Workflow;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 private void RefreshRootTabReadiness()
 {foreach(var tab in WorkflowTabViewModel.Tabs)tab.RefreshRootReadiness();}
 private Solution? _rootHistoryDocument;
 private RootWorkflowCollectionHistory? _rootHistory;
 public bool CanEditRootWorkspace => DocumentManager.CurrentSolution!=null&&!IsRunning&&!IsDebugWindowOpen
  &&_solutionTransitionLease==null&&!_transitionPreparationInProgress&&!_executionManager.IsAnyRunning()
  &&DocumentManager.Session?.Editing.HasPendingChanges!=true;
 private RootWorkflowCollectionHistory RootHistory
 {
  get
  {
   var document=DocumentManager.CurrentSolution??throw new InvalidOperationException("当前没有方案。");
   if(!ReferenceEquals(_rootHistoryDocument,document))
   {
    _rootHistoryDocument=document;_rootHistory=new(DocumentManager.Session!.History,()=>DocumentManager.Session!.Editing.Host.AcceptDocumentGraphHistory());
    _rootHistory.Changed+=(_,_)=>{UpdateCurrentSolutionInfo();OnPropertyChanged(nameof(CanUndoRootWorkspace));OnPropertyChanged(nameof(CanRedoRootWorkspace));};
   }
   return _rootHistory!;
  }
 }
 public bool CanUndoRootWorkspace=>CanEditRootWorkspace&&RootHistory.CanUndo;
 public bool CanRedoRootWorkspace=>CanEditRootWorkspace&&RootHistory.CanRedo;
 internal void UndoRootWorkspace(){try{if(CanUndoRootWorkspace)RootHistory.Undo();}catch(Exception error){StatusText="撤销流程管理失败："+error.Message;}}
 internal void RedoRootWorkspace(){try{if(CanRedoRootWorkspace)RootHistory.Redo();}catch(Exception error){StatusText="重做流程管理失败："+error.Message;}}
 internal bool AddRootWorkspace()
 {
  if(!CanEditRootWorkspace)return false;
  var document=DocumentManager.CurrentSolution!;int number=1;while(document.Workflows.Any(w=>w.Name=="流程"+number))number++;
  var root=new Flow(Guid.NewGuid().ToString(),"流程"+number);var tab=new WorkflowTabViewModel{Id=root.Id,Name=root.Name};
  try{ExecuteRootCollectionChange(root,tab,Array.Empty<Flow>(),true,document.Workflows.Count,"新增流程");return true;}catch(Exception error){StatusText="新增流程已取消："+error.Message;return false;}
 }
 internal bool CopyRootWorkspace(WorkflowTabViewModel sourceTab,System.Windows.Point? overviewPosition=null)
 {
  if(!CanEditRootWorkspace||ResolveEditableRoot(sourceTab) is not {} source)return false;
  var root=new Flow(Guid.NewGuid().ToString(),UniqueRootCopyName(source.Name)){WorkspaceSettings=overviewPosition is {} position?source.WorkspaceSettings with{OverviewX=position.X,OverviewY=position.Y}:source.WorkspaceSettings,AdditionalData=source.AdditionalData?.ToDictionary(p=>p.Key,p=>p.Value.Clone())};
  var tab=new WorkflowTabViewModel{Id=root.Id,Name=root.Name};var allocated=new List<(SunEyeVision.UI.Shared.WorkflowNode Node,string Scope)>();
  bool committed=false;var rootNodeIds=new Dictionary<string,string>(StringComparer.Ordinal);
  try
  {
   var factory=new NodeFactory(tab.SequenceManager);
   var fragment=source.Nodes.Count==0?new WorkflowFragment(Array.Empty<WorkflowNodeBase>(),Array.Empty<WorkflowConnection>(),Array.Empty<Flow>())
    :WorkflowFragmentCopy.Prepare(source,source.Nodes.Select(n=>n.Id),root.Id,_workflowEngine.GetWorkflowById,(original,scope)=>
    {
     if(!ToolRegistry.ToolExists(original.ToolType))throw new InvalidOperationException("工具未加载："+original.DispName);
     var node=factory.CreateIndexedNode(original.ToolType,original.LocalIndex,workflowId:scope);node.DispName=original.DispName;node.Name=node.GlobalIndex+" "+original.DispName;allocated.Add((node,scope));if(scope==root.Id)rootNodeIds.Add(original.Id,node.Id);return node;
    });
   root.RootConfiguration=source.RootConfiguration.Remap(rootNodeIds);
   foreach(var node in fragment.Nodes)root.Nodes.Add(node);foreach(var edge in fragment.Connections)root.Connections.Add(edge);
   ExecuteRootCollectionChange(root,tab,fragment.OwnedWorkflows,true,DocumentManager.CurrentSolution!.Workflows.IndexOf(source)+1,"复制流程");
   committed=true;return true;
  }
  catch(Exception error){StatusText="复制流程已取消："+error.Message;return false;}
  finally{if(!committed)foreach(var item in allocated){tab.SequenceManager.ReleaseGlobalIndex(item.Node.GlobalIndex);tab.SequenceManager.ReleaseLocalIndex(item.Scope,item.Node.ToolType,item.Node.LocalIndex);}}
 }
 private string UniqueRootCopyName(string source)
 {
  var stem=source.Length>55?source[..55]:source;var name=stem+" 副本";int index=2;
  while(DocumentManager.CurrentSolution!.Workflows.Any(w=>w.Name==name))name=stem+" 副本"+index++;
  return name;
 }
 private bool RenameRootWorkspace(WorkflowTabViewModel tab,string name)
 {
  var root=_workflowEngine.GetWorkflowById(tab.RootWorkflowId);
  if(root==null)return false;
  if(!CanEditRootWorkspace||tab.IsInSubroutine){tab.SynchronizeRootName(root.Name);return false;}
  try
  {
   if(root.Name==name)return true;
   RootHistory.Execute(new RootWorkflowNameCommand(DocumentManager.CurrentSolution!,root,name,()=>
   {
    tab.SynchronizeRootName(root.Name);
    _executionManager.EnsureExecutionSnapshot(root.Id,root.Name,root.Nodes);
    RefreshStatusBarExecutionSnapshot();
    SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CommunicationService.OutputBindings.ReconcileAll();
    UpdateCurrentSolutionInfo();
   }));return true;
  }
  catch(Exception error){tab.SynchronizeRootName(root.Name);StatusText="流程改名已取消："+error.Message;return false;}
 }
 internal bool DeleteRootWorkspace(WorkflowTabViewModel tab)
 {
  if(!CanEditRootWorkspace||ResolveEditableRoot(tab) is not {} root)return false;
  var document=DocumentManager.CurrentSolution!;
  if(document.Workflows.Where(w=>!ReferenceEquals(w,root)).Any(w=>w.Nodes.Any(n=>n.SubroutineId==root.Id)))
  {StatusText="该流程仍被其他流程引用，不能删除。";return false;}
  var owned=WorkflowOwnership.CollectExclusiveDescendants(document.Workflows,root,root.Nodes);
  try{ExecuteRootCollectionChange(root,tab,owned,false,document.Workflows.IndexOf(root),"删除流程");return true;}catch(Exception error){StatusText="删除流程已取消："+error.Message;return false;}
 }
 private void ExecuteRootCollectionChange(Flow root,WorkflowTabViewModel tab,IReadOnlyList<Flow> owned,bool attach,int index,string description)
 {
  var document=DocumentManager.CurrentSolution!;
  var command=new RootWorkflowCollectionCommand(document,_workflowEngine,root,owned,attach,index,description,isAttached=>
  {
   if(!ReferenceEquals(DocumentManager.CurrentSolution,document))throw new InvalidOperationException("根流程操作所属方案已退出。");
   if(isAttached)
   {
    BindExistingWorkflowTab(tab,root,_workflowEngine.GetWorkflowById);
    if(!WorkflowTabViewModel.Tabs.Contains(tab))
    {
     var rootIndex=document.Workflows.Where(w=>!w.IsSubroutine).ToList().IndexOf(root);
     WorkflowTabViewModel.Tabs.Insert(Math.Clamp(rootIndex,0,WorkflowTabViewModel.Tabs.Count),tab);
    }
    SetSelectedTab(tab);
   }
   else
   {
    var oldIndex=WorkflowTabViewModel.Tabs.IndexOf(tab);WorkflowTabViewModel.Tabs.Remove(tab);
    if(ReferenceEquals(WorkflowTabViewModel.SelectedTab,tab))
     SetSelectedTab(WorkflowTabViewModel.Tabs.Count==0?null:WorkflowTabViewModel.Tabs[Math.Clamp(oldIndex,0,WorkflowTabViewModel.Tabs.Count-1)]);
    foreach(var flow in owned.Prepend(root)){_workflowDsqsCache.Remove(flow.Id);_workflowContextService.Remove(flow.Id);_executionManager.ForgetWorkspaceSnapshot(flow.Id);
     foreach(var node in flow.Nodes.OfType<SunEyeVision.UI.Shared.WorkflowNode>()){node.OutputCache?.Clear();node.LastResult=null;}}
   }
   var bindings=SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CommunicationService.OutputBindings;
   bindings.ReconcileAll();bindings.AcceptHistoryConfiguration(document);ToolEditorCommit.RefreshCommittedBindings();
   CloseExecutionSnapshotDetails();BuildFlatRows();UpdateCurrentSolutionInfo();
  });
  RootHistory.Execute(command);
 }
 internal SunEyeVision.Workflow.Workflow? ResolveEditableRoot(WorkflowTabViewModel tab)
 {
  if(!CanEditWorkflowCanvas||tab.IsInSubroutine||!WorkflowTabViewModel.Tabs.Contains(tab))return null;
  var flow=_workflowEngine.GetWorkflowById(tab.RootWorkflowId);
  return flow is {IsSubroutine:false}&&DocumentManager.CurrentSolution?.Workflows.Any(w=>ReferenceEquals(w,flow))==true?flow:null;
 }
 internal sealed record RootSettingsAdmission(SolutionSession Session,Flow Root,RootWorkspaceSettings Opening);
 internal RootSettingsAdmission? PrepareRootSettings(WorkflowTabViewModel tab)
  =>ResolveEditableRoot(tab) is {} root && DocumentManager.Session is {} session?new(session,root,root.WorkspaceSettings):null;
 internal bool ChangeRootSettings(WorkflowTabViewModel tab,RootSettingsAdmission admission,RootWorkspaceSettings settings)
  =>ReferenceEquals(DocumentManager.Session,admission.Session)&&ChangeRootSettings(tab,admission.Root,admission.Opening,settings);
 internal bool ChangeRootSettings(WorkflowTabViewModel tab,SunEyeVision.Workflow.Workflow expected,RootWorkspaceSettings opening,RootWorkspaceSettings settings)
 {
  if(!ReferenceEquals(ResolveEditableRoot(tab),expected)||expected.WorkspaceSettings!=opening)return false;
  if(settings==opening)return true;
  tab.CommandManager.Execute(new ChangeRootWorkspaceSettingsCommand(expected,settings,()=>{UpdateCurrentSolutionInfo();}));
  return true;
 }
}

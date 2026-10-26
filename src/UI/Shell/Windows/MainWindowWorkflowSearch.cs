using SunEyeVision.UI.Services.Workflow;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 internal IReadOnlyList<WorkflowSearchEntry> SearchWorkflowNodes(string query,bool all)
 {
  if(DocumentManager.CurrentSolution is not {} solution||!all&&WorkflowTabViewModel.SelectedTab==null)return Array.Empty<WorkflowSearchEntry>();
  return WorkflowSearchCatalog.Search(solution.Workflows,all?null:WorkflowTabViewModel.SelectedTab!.RootWorkflowId,query);
 }
 internal bool NavigateSearchResult(WorkflowSearchEntry entry)
 {
  var solution=DocumentManager.CurrentSolution;
  if(!CanEditWorkflowCanvas||solution==null||!WorkflowSearchCatalog.IsCurrent(entry,solution.Workflows))return false;
  if(!NavigateWorkflowContainer(entry.Root,entry.Hops))return false;
  var tab=WorkflowTabViewModel.SelectedTab!;
  foreach(var node in tab.WorkflowNodes)node.IsSelected=ReferenceEquals(node,entry.Node);
  ForceSelectNode(entry.Node);return true;
 }
 internal IReadOnlyList<WorkflowLocationEntry> GetWorkflowLocations()
  => DocumentManager.CurrentSolution is {} solution ? WorkflowLocationCatalog.Build(solution.Workflows) : Array.Empty<WorkflowLocationEntry>();
 internal bool NavigateWorkflowLocation(WorkflowLocationEntry entry)
  => DocumentManager.CurrentSolution is {} solution && WorkflowLocationCatalog.IsCurrent(entry,solution.Workflows)
     && NavigateWorkflowContainer(entry.Root,entry.Hops);
 private bool NavigateWorkflowContainer(SunEyeVision.Workflow.Workflow root,IReadOnlyList<WorkflowSearchHop> hops)
 {
  if(!CanEditWorkflowCanvas)return false;
  var tab=WorkflowTabViewModel.Tabs.SingleOrDefault(t=>t.RootWorkflowId==root.Id);
  if(tab==null||tab.IsRunning||!ReferenceEquals(_workflowEngine.GetWorkflowById(root.Id),root)
     ||hops.Any(h=>!ReferenceEquals(_workflowEngine.GetWorkflowById(h.Child.Id),h.Child)))return false;
  SetSelectedTab(tab);tab.ResetNavigation();NavigateToWorkflowCanvas(root.Id);
  foreach(var hop in hops){tab.NavigateToSubroutine(hop.Child.Id,hop.Owner.DispName,hop.Owner.Icon,hop.Owner.Id);NavigateToWorkflowCanvas(hop.Child.Id);}
  return true;
 }
}

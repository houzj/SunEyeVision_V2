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
  var tab=WorkflowTabViewModel.Tabs.SingleOrDefault(t=>t.RootWorkflowId==entry.Root.Id);if(tab==null||tab.IsRunning)return false;
  if(!ReferenceEquals(_workflowEngine.GetWorkflowById(entry.Root.Id),entry.Root)||entry.Hops.Any(h=>!ReferenceEquals(_workflowEngine.GetWorkflowById(h.Child.Id),h.Child)))return false;
  WorkflowTabViewModel.SelectedTab=tab;tab.ResetNavigation();NavigateToWorkflowCanvas(entry.Root.Id);
  foreach(var hop in entry.Hops){tab.NavigateToSubroutine(hop.Child.Id,hop.Owner.DispName,hop.Owner.Icon,hop.Owner.Id);NavigateToWorkflowCanvas(hop.Child.Id);}
  foreach(var node in tab.WorkflowNodes)node.IsSelected=ReferenceEquals(node,entry.Node);
  ForceSelectNode(entry.Node);return true;
 }
}

using SunEyeVision.UI.Services.Workflow;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 internal IReadOnlyList<WorkflowSearchEntry> SearchWorkflowNodes(string query,bool all)
 {
  if(DocumentManager.CurrentSession?.Document is not {} solution||!all&&WorkflowTabViewModel.SelectedTab==null)return Array.Empty<WorkflowSearchEntry>();
  return WorkflowSearchCatalog.Search(solution.Workflows,all?null:WorkflowTabViewModel.SelectedTab!.RootWorkflowId,query);
 }
 internal IReadOnlyList<WorkflowSearchEntry> FindWorkflowNodesById(string query)
  => DocumentManager.CurrentSession?.Document is {} solution ? WorkflowSearchCatalog.FindById(solution.Workflows,query) : Array.Empty<WorkflowSearchEntry>();
 internal bool NavigateSearchResult(WorkflowSearchEntry entry)
 {
  var solution=DocumentManager.CurrentSession?.Document;
  if(!CanEditWorkflowCanvas||solution==null||!WorkflowSearchCatalog.IsCurrent(entry,solution.Workflows))return false;
  if(!NavigateWorkflowContainer(entry.Root,entry.Hops))return false;
  var tab=WorkflowTabViewModel.SelectedTab!;
  foreach(var node in tab.WorkflowNodes)node.IsSelected=ReferenceEquals(node.Definition,entry.Node);
  ForceSelectNode(entry.Node);return true;
 }
 internal IReadOnlyList<WorkflowLocationEntry> GetWorkflowLocations()
  => DocumentManager.CurrentSession?.Document is {} solution ? WorkflowLocationCatalog.Build(solution.Workflows) : Array.Empty<WorkflowLocationEntry>();
 internal bool NavigateWorkflowLocation(WorkflowLocationEntry entry)
  => DocumentManager.CurrentSession?.Document is {} solution && WorkflowLocationCatalog.IsCurrent(entry,solution.Workflows)
     && (entry.Node is {} node ? NavigateSearchResult(node) : NavigateWorkflowContainer(entry.Root,entry.Hops));
 private bool NavigateWorkflowContainer(SunEyeVision.Domain.Workflows.WorkflowDefinition root,IReadOnlyList<WorkflowSearchHop> hops)
 {
  if(!CanEditWorkflowCanvas)return false;
  var tab=WorkflowTabViewModel.Tabs.SingleOrDefault(t=>t.RootWorkflowId==root.Id);
  if(tab==null||tab.IsRunning||!DocumentManager.CurrentSession!.GetWorkflows().Any(flow=>ReferenceEquals(flow,root))
     ||hops.Any())return false;
  SetSelectedTab(tab);tab.ResetNavigation();NavigateToWorkflowCanvas(root.Id);
  foreach(var hop in hops){tab.NavigateToSubroutine(hop.Child.Id,hop.Owner.DispName,null,hop.Owner.Id);NavigateToWorkflowCanvas(hop.Child.Id);}
  return true;
 }
}

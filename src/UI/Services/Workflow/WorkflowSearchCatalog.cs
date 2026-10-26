using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Services.Workflow;
internal sealed record WorkflowSearchHop(SunEyeVision.Workflow.Workflow Parent,WorkflowNodeBase Owner,SunEyeVision.Workflow.Workflow Child);
internal sealed record WorkflowSearchEntry(SunEyeVision.Workflow.Workflow Root,SunEyeVision.Workflow.Workflow Container,WorkflowNodeBase Node,IReadOnlyList<WorkflowSearchHop> Hops,string Name,string FullPath)
{
 public string Caption=>(Node.GlobalIndex>=0?Node.GlobalIndex+" ":"")+Name;
 public override string ToString()=>Caption+"    "+FullPath;
}
internal static class WorkflowSearchCatalog
{
 public static IReadOnlyList<WorkflowSearchEntry> Search(IEnumerable<SunEyeVision.Workflow.Workflow> definitions,string? rootId,string query)
 {
  if(string.IsNullOrEmpty(query))return Array.Empty<WorkflowSearchEntry>();
  var flows=definitions.ToArray();var byId=flows.ToDictionary(f=>f.Id,StringComparer.Ordinal);var result=new List<WorkflowSearchEntry>();
  void Walk(SunEyeVision.Workflow.Workflow root,SunEyeVision.Workflow.Workflow flow,List<WorkflowSearchHop> hops,string prefix,HashSet<string> path)
  {
   if(!path.Add(flow.Id))return;
   try{foreach(var node in flow.Nodes)
   {
    var name=string.IsNullOrWhiteSpace(node.DispName)?(string.IsNullOrWhiteSpace(node.Name)?node.Id:node.Name):node.DispName;var full=prefix+"."+name;
    if(name.Contains(query,StringComparison.Ordinal))result.Add(new(root,flow,node,hops.ToArray(),name,full));
    if(node.ToolType=="Subroutine"&&node.SubroutineId is {} childId&&byId.TryGetValue(childId,out var child)&&child.IsSubroutine)
    {hops.Add(new(flow,node,child));Walk(root,child,hops,full,path);hops.RemoveAt(hops.Count-1);}
   }}finally{path.Remove(flow.Id);}
  }
  foreach(var root in flows.Where(f=>!f.IsSubroutine&&(rootId==null||f.Id==rootId)))Walk(root,root,new(),root.Name,new(StringComparer.Ordinal));
  return result;
 }
 public static bool IsCurrent(WorkflowSearchEntry entry,IEnumerable<SunEyeVision.Workflow.Workflow> definitions)
 {
  var flows=definitions.ToArray();if(!flows.Any(f=>ReferenceEquals(f,entry.Root))||entry.Root.IsSubroutine)return false;
  var current=entry.Root;var path=new HashSet<string>(StringComparer.Ordinal){current.Id};
  foreach(var hop in entry.Hops)
  {
   if(!ReferenceEquals(hop.Parent,current)||!current.Nodes.Any(n=>ReferenceEquals(n,hop.Owner))||hop.Owner.ToolType!="Subroutine"||hop.Owner.SubroutineId!=hop.Child.Id||!flows.Any(f=>ReferenceEquals(f,hop.Child))||!hop.Child.IsSubroutine||!path.Add(hop.Child.Id))return false;
   current=hop.Child;
  }
  return ReferenceEquals(current,entry.Container)&&current.Nodes.Any(n=>ReferenceEquals(n,entry.Node));
 }
}

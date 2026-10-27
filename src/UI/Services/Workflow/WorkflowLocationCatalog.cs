using Flow = SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Services.Workflow;
internal sealed record WorkflowLocationEntry(Flow Root, Flow Container,
    IReadOnlyList<WorkflowSearchHop> Hops, string Name,
    IReadOnlyList<WorkflowLocationEntry> Children, WorkflowSearchEntry? Node = null);
internal static class WorkflowLocationCatalog
{
    internal static IReadOnlyList<WorkflowLocationEntry> Build(IEnumerable<Flow> definitions)
    {
        var flows = definitions.ToArray();
        var byId = flows.ToDictionary(f => f.Id, StringComparer.Ordinal);
        WorkflowLocationEntry Visit(Flow root, Flow flow, List<WorkflowSearchHop> hops,
            string name, HashSet<string> path)
        {
            var children = new List<WorkflowLocationEntry>();
            path.Add(flow.Id);
            foreach (var owner in flow.Nodes)
            {
                if(owner.ToolType!="Subroutine")
                {
                    // 本批仅补根节点目录；Group内部目录沿用已有容器导航，保持冻结。
                    if(hops.Count!=0)continue;
                    var nodeName=string.IsNullOrWhiteSpace(owner.DispName)?owner.Name:owner.DispName;
                    var full=string.Join(".",new[]{root.Name}.Concat(hops.Select(h=>string.IsNullOrWhiteSpace(h.Owner.DispName)?h.Owner.Name:h.Owner.DispName)).Append(nodeName));
                    var node=new WorkflowSearchEntry(root,flow,owner,hops.ToArray(),nodeName,full);
                    children.Add(new(root,flow,hops.ToArray(),node.Caption,Array.Empty<WorkflowLocationEntry>(),node));
                    continue;
                }
                if (owner.SubroutineId is not {} id ||
                    !byId.TryGetValue(id, out var child) || !child.IsSubroutine || path.Contains(id)) continue;
                hops.Add(new(flow, owner, child));
                children.Add(Visit(root, child, hops,
                    string.IsNullOrWhiteSpace(owner.DispName) ? owner.Name : owner.DispName, path));
                hops.RemoveAt(hops.Count - 1);
            }
            path.Remove(flow.Id);
            return new(root, flow, hops.ToArray(), name, children);
        }
        return flows.Where(f => !f.IsSubroutine)
            .Select(f => Visit(f, f, new(), f.Name, new(StringComparer.Ordinal))).ToArray();
    }
    internal static bool IsCurrent(WorkflowLocationEntry entry, IEnumerable<Flow> definitions)
    {
        var flows = definitions.ToArray();
        if(entry.Node is {} node)return ReferenceEquals(entry.Root,node.Root)&&ReferenceEquals(entry.Container,node.Container)&&WorkflowSearchCatalog.IsCurrent(node,flows);
        if (entry.Hops.Count == 0)
            return ReferenceEquals(entry.Root, entry.Container) && !entry.Root.IsSubroutine &&
                flows.Any(f => ReferenceEquals(f, entry.Root));
        var last = entry.Hops[^1];
        return ReferenceEquals(last.Child, entry.Container) &&
            last.Owner.SubroutineId == entry.Container.Id && entry.Container.IsSubroutine &&
            flows.Any(f => ReferenceEquals(f, entry.Container)) &&
            !entry.Hops.Take(entry.Hops.Count - 1).Any(h => h.Parent.Id == entry.Container.Id || h.Child.Id == entry.Container.Id) &&
            entry.Root.Id != entry.Container.Id &&
            WorkflowSearchCatalog.IsCurrent(new(entry.Root, last.Parent, last.Owner,
                entry.Hops.Take(entry.Hops.Count - 1).ToArray(), entry.Name, entry.Name), flows);
    }
}

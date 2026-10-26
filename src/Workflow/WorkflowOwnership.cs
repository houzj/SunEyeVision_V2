using System;
using System.Collections.Generic;
using System.Linq;
namespace SunEyeVision.Workflow;
/// <summary>按实际定义和拥有者节点收集可移除的独占子树；外部引用保护共享子树。</summary>
public static class WorkflowOwnership
{
    public static IReadOnlyList<Workflow> CollectExclusiveDescendants(IEnumerable<Workflow> source,Workflow target,
        IEnumerable<WorkflowNodeBase> removingOwners,IEnumerable<Workflow>? declaredOwned=null)
    {
        var definitions=source.Append(target).Distinct().ToArray();var selected=removingOwners.ToHashSet();
        var groups=definitions.GroupBy(w=>w.Id).ToArray();
        if(groups.Any(g=>g.Count()!=1))throw new InvalidOperationException("同一流程ID对应了不同定义对象。");
        var byId=groups.ToDictionary(g=>g.Key,g=>g.Single());var candidates=new HashSet<Workflow>();
        void Visit(string? id)
        {
            if(string.IsNullOrEmpty(id)||!byId.TryGetValue(id,out var flow)||ReferenceEquals(flow,target)||!candidates.Add(flow))return;
            foreach(var node in flow.Nodes)Visit(node.SubroutineId);
        }
        foreach(var node in selected)Visit(node.SubroutineId);
        foreach(var flow in declaredOwned??Enumerable.Empty<Workflow>())Visit(flow.Id);
        var retained=new HashSet<Workflow>();
        void Retain(string? id)
        {
            if(string.IsNullOrEmpty(id)||!byId.TryGetValue(id,out var flow)||!candidates.Contains(flow)||!retained.Add(flow))return;
            foreach(var node in flow.Nodes)Retain(node.SubroutineId);
        }
        foreach(var flow in definitions.Where(w=>!candidates.Contains(w)))
            foreach(var node in flow.Nodes.Where(n=>!ReferenceEquals(flow,target)||!selected.Contains(n)))Retain(node.SubroutineId);
        return candidates.Except(retained).ToArray();
    }
}
using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>A single admitted workflow graph, captured atomically by the document session.</summary>
public sealed class ExecutionWorkflowSnapshot
{
    internal ExecutionWorkflowSnapshot(string workflowId, long documentRevision,
        RuntimeDirectorySnapshot directory, ToolExecutionOwner owner,
        IReadOnlyList<ExecutionWorkflowNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> predecessors,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> controlPredecessors,
        IReadOnlyList<ExecutionControlEdge> controlEdges)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(owner);
        if (!directory.IsActive || owner.IsRetired) throw new InvalidOperationException("方案执行 owner 或目录已退役。");
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(predecessors);
        ArgumentNullException.ThrowIfNull(controlPredecessors);
        ArgumentNullException.ThrowIfNull(controlEdges);
        var copy = nodes.ToArray();
        if (copy.Any(item => item is null || item.Node is null || item.Node.WorkflowId != workflowId) ||
            copy.Select(item => item.Node.NodeId).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("执行图包含错作用域或重复节点。", nameof(nodes));
        var ids = copy.Select(item => item.Node.NodeId).ToHashSet(StringComparer.Ordinal);
        if (predecessors.Count != ids.Count || predecessors.Keys.Any(id => !ids.Contains(id)))
            throw new ArgumentException("执行依赖图与节点身份不一致。", nameof(predecessors));
        var edges = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (!predecessors.TryGetValue(id, out var sources) || sources is null)
                throw new ArgumentException("执行依赖图缺少节点。", nameof(predecessors));
            var unique = sources.ToHashSet(StringComparer.Ordinal);
            if (unique.Count != sources.Count || unique.Contains(id) || unique.Any(source => !ids.Contains(source)))
                throw new ArgumentException("执行依赖图包含重复、自引用或不存在的节点。", nameof(predecessors));
            edges.Add(id, Array.AsReadOnly(unique.ToArray()));
        }
        var remaining = edges.ToDictionary(pair => pair.Key,
            pair => pair.Value.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        while (remaining.Count != 0)
        {
            var ready = remaining.Where(pair => pair.Value.Count == 0).Select(pair => pair.Key).ToArray();
            if (ready.Length == 0) throw new ArgumentException("执行依赖图存在循环。", nameof(predecessors));
            foreach (var id in ready) remaining.Remove(id);
            foreach (var pending in remaining.Values) pending.ExceptWith(ready);
        }
        if (copy.Any(item => !ReferenceEquals(item.Node.Owner, owner)
            || !ReferenceEquals(item.Node.Directory, directory)))
            throw new ArgumentException("执行图节点不属于同一方案运行 owner 或目录代次。", nameof(nodes));
        if (controlPredecessors.Count != ids.Count || controlPredecessors.Keys.Any(id => !ids.Contains(id)))
            throw new ArgumentException("控制连线与节点身份不一致。", nameof(controlPredecessors));
        var controls = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (!controlPredecessors.TryGetValue(id, out var sources) || sources is null)
                throw new ArgumentException("控制连线缺少节点。", nameof(controlPredecessors));
            var unique = sources.ToHashSet(StringComparer.Ordinal);
            if (unique.Count != sources.Count || unique.Any(source => !edges[id].Contains(source)))
                throw new ArgumentException("控制连线不是本次执行依赖的子集。", nameof(controlPredecessors));
            controls.Add(id, Array.AsReadOnly(unique.ToArray()));
        }
        var frozenEdges = controlEdges.ToArray();
        if (frozenEdges.Any(edge => edge is null || string.IsNullOrWhiteSpace(edge.Id)
            || !ids.Contains(edge.SourceNodeId) || !ids.Contains(edge.TargetNodeId))
            || frozenEdges.Select(edge => edge.Id).Distinct(StringComparer.Ordinal).Count() != frozenEdges.Length)
            throw new ArgumentException("控制连线身份无效或不唯一。", nameof(controlEdges));
        foreach (var id in ids)
            if (!frozenEdges.Where(edge => edge.TargetNodeId == id).Select(edge => edge.SourceNodeId)
                .ToHashSet(StringComparer.Ordinal).SetEquals(controls[id]))
                throw new ArgumentException("控制连线与控制前驱不一致。", nameof(controlEdges));
        WorkflowId = workflowId;
        DocumentRevision = documentRevision;
        Nodes = Array.AsReadOnly(copy);
        Predecessors = new ReadOnlyDictionary<string, IReadOnlyCollection<string>>(edges);
        ControlPredecessors = new ReadOnlyDictionary<string, IReadOnlyCollection<string>>(controls);
        ControlEdges = Array.AsReadOnly(frozenEdges);
        Directory = directory;
        Owner = owner;
    }

    public string WorkflowId { get; }
    public long DocumentRevision { get; }
    public IReadOnlyList<ExecutionWorkflowNode> Nodes { get; }
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Predecessors { get; }
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> ControlPredecessors { get; }
    public IReadOnlyList<ExecutionControlEdge> ControlEdges { get; }
    internal RuntimeDirectorySnapshot Directory { get; }
    internal ToolExecutionOwner Owner { get; }
}

/// <summary>The execution snapshot and enabled state of the same captured node.</summary>
public sealed record ExecutionWorkflowNode(ExecutionNodeSnapshot Node, bool IsEnabled);
public sealed record ExecutionControlEdge(string Id, string SourceNodeId, string TargetNodeId);

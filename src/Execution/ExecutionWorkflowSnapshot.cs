using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>A single admitted workflow graph, captured atomically by the document session.</summary>
public sealed class ExecutionWorkflowSnapshot
{
    internal ExecutionWorkflowSnapshot(string workflowId, long documentRevision,
        IReadOnlyList<ExecutionWorkflowNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> predecessors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(predecessors);
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
        if (copy.Length > 0 && copy.Any(item => !ReferenceEquals(item.Node.Owner, copy[0].Node.Owner)
            || !ReferenceEquals(item.Node.Directory, copy[0].Node.Directory)))
            throw new ArgumentException("执行图节点不属于同一方案运行 owner 或目录代次。", nameof(nodes));
        WorkflowId = workflowId;
        DocumentRevision = documentRevision;
        Nodes = Array.AsReadOnly(copy);
        Predecessors = new ReadOnlyDictionary<string, IReadOnlyCollection<string>>(edges);
    }

    public string WorkflowId { get; }
    public long DocumentRevision { get; }
    public IReadOnlyList<ExecutionWorkflowNode> Nodes { get; }
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Predecessors { get; }
}

/// <summary>The execution snapshot and enabled state of the same captured node.</summary>
public sealed record ExecutionWorkflowNode(ExecutionNodeSnapshot Node, bool IsEnabled);

using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

/// <summary>Validated control and active input dependencies for an isolated graph candidate.</summary>
internal sealed class GraphDependencySnapshot
{
    private readonly IReadOnlyDictionary<ScopedNodeIdentity, IReadOnlySet<ScopedNodeIdentity>> _next;
    private GraphDependencySnapshot(Dictionary<ScopedNodeIdentity, HashSet<ScopedNodeIdentity>> next)
        => _next = next.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<ScopedNodeIdentity>)pair.Value);

    internal static GraphDependencySnapshot Create(SolutionDefinition document,
        IReadOnlyDictionary<WorkflowDefinition, GraphState> candidates, IReadOnlyList<GlobalVariable> candidateVariables,
        Func<NodeDefinition, ToolConfiguration, AdmittedToolConfiguration> admit)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(candidateVariables);
        ArgumentNullException.ThrowIfNull(admit);
        if (candidateVariables.Any(variable => string.IsNullOrWhiteSpace(variable.Id)) ||
            candidateVariables.Select(variable => variable.Id).Distinct(StringComparer.Ordinal).Count() != candidateVariables.Count)
            throw new InvalidOperationException("全局变量身份为空或不唯一。");
        if (candidates.Keys.Any(flow => document.Workflows.Count(actual => ReferenceEquals(actual, flow)) != 1))
            throw new InvalidOperationException("候选流程不属于当前方案。");
        var flows = document.Workflows.Select(flow => (Flow: flow,
            State: candidates.TryGetValue(flow, out var candidate) ? candidate : GraphState.Capture(flow))).ToArray();
        if (flows.Select(item => item.Flow.Id).Distinct(StringComparer.Ordinal).Count() != flows.Length)
            throw new InvalidOperationException("方案流程身份不唯一。");
        var nodes = new Dictionary<ScopedNodeIdentity, GraphState.NodeState>();
        foreach (var (flow, state) in flows)
            foreach (var item in state.Nodes)
            {
                if (string.IsNullOrWhiteSpace(item.Node.Id) || !nodes.TryAdd(new(flow.Id, item.Node.Id), item))
                    throw new InvalidOperationException("流程节点身份为空或不唯一。");
            }
        var next = nodes.Keys.ToDictionary(id => id, _ => new HashSet<ScopedNodeIdentity>());
        var admitted = nodes.ToDictionary(item => item.Key, item => admit(item.Value.Node, item.Value.Configuration));
        void Add(ScopedNodeIdentity source, ScopedNodeIdentity target, bool missingAllowed)
        {
            if (!nodes.ContainsKey(target)) throw new InvalidOperationException("依赖目标节点不属于方案。");
            if (!nodes.ContainsKey(source))
            {
                if (missingAllowed) return; // explicit stale reference is retained for readiness diagnostics
                throw new InvalidOperationException("连线引用了不存在的节点。");
            }
            if (source == target) throw new InvalidOperationException("执行依赖不能指向自身。");
            next[source].Add(target);
        }
        void AddReference(OutputReference reference, string workflowId, ScopedNodeIdentity target)
        {
            var scope = reference.ScopeId ?? workflowId;
            if (scope != workflowId) throw new InvalidOperationException("节点结果引用跨出流程作用域。");
            Add(new(scope, reference.NodeId), target, missingAllowed: true);
            if (reference.Selection is BindingSelection.AtField index) AddReference(index.Reference, workflowId, target);
        }
        foreach (var (flow, state) in flows)
        {
            foreach (var edge in state.Edges)
                Add(new(flow.Id, edge.SourceNodeId), new(flow.Id, edge.TargetNodeId), missingAllowed: false);
            foreach (var item in state.Nodes)
            {
                var target = new ScopedNodeIdentity(flow.Id, item.Node.Id);
                var config = admitted[target];
                foreach (var setting in BindingRelationCatalog.References(config, executionOnly: true).Values)
                    AddReference(setting.Reference, flow.Id, target);
                foreach (var (_, reference) in BindingRelationCatalog.VariableReferences(config, executionOnly: true))
                    if (reference.Selection is BindingSelection.AtField index) AddReference(index.Reference, flow.Id, target);
            }
        }
        foreach (var variable in candidateVariables)
        {
            if (variable.ProducerBinding is null || !string.IsNullOrEmpty(variable.RebindingReason) ||
                !string.IsNullOrEmpty(variable.BindingValidationPending)) continue;
            var reference = variable.ProducerBinding.SourceReference;
            if (string.IsNullOrWhiteSpace(reference.ScopeId)) continue;
            var producer = new ScopedNodeIdentity(reference.ScopeId, reference.NodeId);
            if (!nodes.ContainsKey(producer)) continue;
            foreach (var (identity, config) in admitted)
                if (BindingRelationCatalog.VariableReferences(config, executionOnly: true)
                    .Any(pair => pair.Reference.SolutionId == document.Id && pair.Reference.VariableId == variable.Id))
                    Add(producer, identity, missingAllowed: false);
        }
        var remaining = nodes.Keys.ToDictionary(id => id, _ => 0);
        foreach (var targets in next.Values) foreach (var target in targets) remaining[target]++;
        var ready = new Queue<ScopedNodeIdentity>(remaining.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var visited = 0;
        while (ready.TryDequeue(out var source))
        {
            visited++;
            foreach (var target in next[source]) if (--remaining[target] == 0) ready.Enqueue(target);
        }
        if (visited != nodes.Count) throw new InvalidOperationException("解决方案依赖图存在循环。");
        return new GraphDependencySnapshot(next);
    }

    internal IReadOnlySet<ScopedNodeIdentity> Descendants(IEnumerable<ScopedNodeIdentity> sources)
    {
        var result = new HashSet<ScopedNodeIdentity>(sources);
        var pending = new Queue<ScopedNodeIdentity>(result);
        while (pending.TryDequeue(out var current))
            if (_next.TryGetValue(current, out var targets))
                foreach (var target in targets) if (result.Add(target)) pending.Enqueue(target);
        return result;
    }

    /// <summary>Frozen direct predecessors only. Missing stale sources never become graph nodes.</summary>
    internal IReadOnlyList<ScopedNodeIdentity> GetPredecessors(ScopedNodeIdentity node)
    {
        if (!_next.ContainsKey(node)) throw new InvalidOperationException("依赖查询节点不属于本次正式候选图。");
        return Array.AsReadOnly(_next.Where(pair => pair.Value.Contains(node)).Select(pair => pair.Key)
            .OrderBy(item => item.WorkflowId, StringComparer.Ordinal).ThenBy(item => item.NodeId, StringComparer.Ordinal).ToArray());
    }

    /// <summary>Actual direct edges for workflow ordering, not the transitive descendant closure.</summary>
    internal IReadOnlyList<(ScopedNodeIdentity Source, ScopedNodeIdentity Target)> GetEdges() => Array.AsReadOnly(
        _next.SelectMany(pair => pair.Value.Select(target => (Source: pair.Key, Target: target)))
            .OrderBy(edge => edge.Source.WorkflowId, StringComparer.Ordinal).ThenBy(edge => edge.Source.NodeId, StringComparer.Ordinal)
            .ThenBy(edge => edge.Target.WorkflowId, StringComparer.Ordinal).ThenBy(edge => edge.Target.NodeId, StringComparer.Ordinal).ToArray());

    internal static IReadOnlySet<ScopedNodeIdentity> ExpandUnion(GraphDependencySnapshot before,
        GraphDependencySnapshot after, IEnumerable<ScopedNodeIdentity> sources)
    {
        var result = new HashSet<ScopedNodeIdentity>(sources);
        var pending = new Queue<ScopedNodeIdentity>(result);
        while (pending.TryDequeue(out var current))
        {
            foreach (var graph in new[] { before, after })
                if (graph._next.TryGetValue(current, out var targets))
                    foreach (var target in targets) if (result.Add(target)) pending.Enqueue(target);
        }
        return result;
    }
}

using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

/// <summary>Derives execution invalidation from actual execution changes, never from visual flags.</summary>
internal static class GraphInvalidationProjection
{
    internal static IReadOnlyList<ScopedNodeIdentity> Compare(
        IReadOnlyDictionary<string, GraphState> before, IReadOnlyDictionary<string, GraphState> after,
        GraphDependencySnapshot beforeDependencies, GraphDependencySnapshot afterDependencies,
        IEnumerable<ScopedNodeIdentity> reconciled,
        Func<NodeDefinition, ToolConfiguration, string> executionIdentity)
    {
        var affected = new HashSet<ScopedNodeIdentity>(reconciled);
        foreach (var flowId in before.Keys.Union(after.Keys, StringComparer.Ordinal))
        {
            var old = before.GetValueOrDefault(flowId);
            var next = after.GetValueOrDefault(flowId);
            var oldNodes = (old?.Nodes ?? Array.Empty<GraphState.NodeState>()).ToDictionary(item => item.Node.Id, StringComparer.Ordinal);
            var newNodes = (next?.Nodes ?? Array.Empty<GraphState.NodeState>()).ToDictionary(item => item.Node.Id, StringComparer.Ordinal);
            foreach (var nodeId in oldNodes.Keys.Union(newNodes.Keys, StringComparer.Ordinal))
            {
                if (!oldNodes.TryGetValue(nodeId, out var first) || !newNodes.TryGetValue(nodeId, out var second) ||
                    !ReferenceEquals(first.Node, second.Node) || first.Name != second.Name ||
                    first.DisplayName != second.DisplayName || first.Enabled != second.Enabled ||
                    (!ReferenceEquals(first.Configuration, second.Configuration) &&
                     executionIdentity(first.Node, first.Configuration) != executionIdentity(second.Node, second.Configuration)))
                    affected.Add(new(flowId, nodeId));
            }
            static HashSet<(string Id, string Source, string SourcePort, string Target, string TargetPort)> Edges(GraphState? state)
                => (state?.Edges ?? Array.Empty<GraphState.EdgeState>())
                    .Select(edge => (edge.Edge.Id, edge.SourceNodeId, edge.SourcePort, edge.TargetNodeId, edge.TargetPort)).ToHashSet();
            var oldEdges = Edges(old); var newEdges = Edges(next);
            foreach (var edge in oldEdges.Except(newEdges).Concat(newEdges.Except(oldEdges)))
            {
                affected.Add(new(flowId, edge.Source));
                affected.Add(new(flowId, edge.Target));
            }
        }
        return GraphDependencySnapshot.ExpandUnion(beforeDependencies, afterDependencies, affected)
            .OrderBy(item => item.WorkflowId, StringComparer.Ordinal)
            .ThenBy(item => item.NodeId, StringComparer.Ordinal).ToArray();
    }
}

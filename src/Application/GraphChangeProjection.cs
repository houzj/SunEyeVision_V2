using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

/// <summary>Projects committed graph states to UI notifications without inferring execution invalidation.</summary>
internal static class GraphChangeProjection
{
    internal sealed record Changes(IReadOnlyList<NodeChange> Nodes, IReadOnlyList<RemovedNode> Removed,
        IReadOnlyList<ConnectionChange> Connections);

    internal static Changes Compare(string workflowId, GraphState before, GraphState after)
    {
        var oldNodes = before.Nodes.ToDictionary(item => item.Node, ReferenceEqualityComparer.Instance);
        var newNodes = after.Nodes.ToDictionary(item => item.Node, ReferenceEqualityComparer.Instance);
        var changed = new List<NodeChange>();
        var removed = new List<RemovedNode>();
        for (var index = 0; index < before.Nodes.Count; index++)
        {
            var item = before.Nodes[index];
            if (!newNodes.ContainsKey(item.Node))
                removed.Add(new(new(workflowId, item.Node.Id), item.Node, index));
        }
        foreach (var item in after.Nodes)
        {
            var identity = new ScopedNodeIdentity(workflowId, item.Node.Id);
            if (!oldNodes.TryGetValue(item.Node, out var old))
            {
                changed.Add(new(identity, item.Node, NodeChangedAspect.Identity | NodeChangedAspect.Configuration |
                    NodeChangedAspect.Geometry | NodeChangedAspect.Presentation));
                continue;
            }
            var aspects = (NodeChangedAspect)0;
            if (old.Position != item.Position) aspects |= NodeChangedAspect.Geometry;
            if (old.Name != item.Name || old.DisplayName != item.DisplayName || old.Enabled != item.Enabled)
                aspects |= NodeChangedAspect.Presentation;
            if (!ReferenceEquals(old.Configuration, item.Configuration) &&
                !GraphState.SameConfiguration(old.Configuration, item.Configuration))
                aspects |= NodeChangedAspect.Configuration;
            if (aspects != 0) changed.Add(new(identity, item.Node, aspects));
        }
        var oldEdges = before.Edges.ToDictionary(item => item.Edge, ReferenceEqualityComparer.Instance);
        var newEdges = after.Edges.ToDictionary(item => item.Edge, ReferenceEqualityComparer.Instance);
        var edges = new List<ConnectionChange>();
        foreach (var item in before.Edges)
            if (!newEdges.ContainsKey(item.Edge))
                edges.Add(new(workflowId, item.Edge, ConnectionChangeKind.Removed,
                    item.SourceNodeId, item.TargetNodeId));
        foreach (var item in after.Edges)
        {
            if (!oldEdges.TryGetValue(item.Edge, out var old))
            {
                edges.Add(new(workflowId, item.Edge, ConnectionChangeKind.Added, null, null));
                continue;
            }
            var pathChanged = old.ManualPath is null != (item.ManualPath is null) ||
                (old.ManualPath is not null && item.ManualPath is not null && !old.ManualPath.SequenceEqual(item.ManualPath));
            if (old.SourceNodeId != item.SourceNodeId || old.TargetNodeId != item.TargetNodeId ||
                old.SourcePort != item.SourcePort || old.TargetPort != item.TargetPort || pathChanged)
                edges.Add(new(workflowId, item.Edge, ConnectionChangeKind.Updated,
                    old.SourceNodeId, old.TargetNodeId));
        }
        return new(changed.AsReadOnly(), removed.AsReadOnly(), edges.AsReadOnly());
    }
}

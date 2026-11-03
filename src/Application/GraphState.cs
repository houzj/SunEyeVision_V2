using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

/// <summary>An editing snapshot containing actual graph identities, never execution-owned resources.</summary>
internal sealed class GraphState
{
    internal sealed record NodeState(NodeDefinition Node, string Name, string DisplayName, bool Enabled,
        CanvasPoint Position, ToolConfiguration Configuration);
    internal sealed record EdgeState(ConnectionDefinition Edge, string SourceNodeId, string TargetNodeId, string SourcePort, string TargetPort,
        IReadOnlyList<CanvasPoint>? ManualPath);
    internal IReadOnlyList<NodeState> Nodes { get; }
    internal IReadOnlyList<EdgeState> Edges { get; }
    private GraphState(IEnumerable<NodeState> nodes, IEnumerable<EdgeState> edges)
    {
        Nodes = Array.AsReadOnly(nodes.ToArray());
        Edges = Array.AsReadOnly(edges.ToArray());
    }
    internal static GraphState Capture(WorkflowDefinition workflow) => new(
        workflow.Nodes.Select(node => new NodeState(node, node.Name, node.DispName, node.IsEnabled, node.Position, node.Configuration)),
        workflow.Connections.Select(edge => new EdgeState(edge, edge.SourceNodeId, edge.TargetNodeId, edge.SourcePort, edge.TargetPort,
            edge.ManualPathPoints is null ? null : Array.AsReadOnly(edge.ManualPathPoints.ToArray()))));

    internal static GraphState FromDraft(GraphEditDraft draft, IReadOnlyDictionary<NodeDefinition, ToolConfiguration> replacements)
    {
        var paths = draft.ManualPaths;
        return new GraphState(draft.Nodes.Select(node =>
        {
            var display = draft.DisplayNames.GetValueOrDefault(node) ?? node.DispName;
            var name = draft.DisplayNames.ContainsKey(node) ? $"{node.GlobalIndex} {display}" : node.Name;
            return new NodeState(node, name, display, draft.EnabledStates.GetValueOrDefault(node, node.IsEnabled),
                draft.Positions.GetValueOrDefault(node, node.Position), replacements.GetValueOrDefault(node) ?? node.Configuration);
        }), draft.Edges.Select(item => new EdgeState(item.Edge, item.Edge.SourceNodeId, item.Edge.TargetNodeId, item.SourcePort, item.TargetPort,
            paths.TryGetValue(item.Edge, out var path) ? path : item.Edge.ManualPathPoints)));
    }

    internal GraphState WithConfigurations(IReadOnlyDictionary<NodeDefinition, ToolConfiguration> replacements)
        => new(Nodes.Select(state => replacements.TryGetValue(state.Node, out var replacement)
            ? state with { Configuration = replacement } : state), Edges);

    internal GraphState WithoutCurrentValues() => new(
        Nodes.Select(state => state with { Configuration = state.Configuration.WithoutCurrentValues() }), Edges);

    // Compare edit structure rather than object identity of a configuration version:
    // a run may legitimately install new CurrentValues while the proposal is prepared.
    internal bool MatchesCurrent(WorkflowDefinition workflow)
    {
        if (Nodes.Count != workflow.Nodes.Count || Edges.Count != workflow.Connections.Count) return false;
        for (var index = 0; index < Nodes.Count; index++)
        {
            var saved = Nodes[index]; var node = workflow.Nodes[index];
            if (!ReferenceEquals(saved.Node, node) || saved.Name != node.Name || saved.DisplayName != node.DispName ||
                saved.Enabled != node.IsEnabled || saved.Position != node.Position ||
                !SameConfiguration(saved.Configuration, node.Configuration)) return false;
        }
        for (var index = 0; index < Edges.Count; index++)
        {
            var saved = Edges[index]; var edge = workflow.Connections[index];
            if (!ReferenceEquals(saved.Edge, edge) || saved.SourceNodeId != edge.SourceNodeId ||
                saved.TargetNodeId != edge.TargetNodeId || saved.SourcePort != edge.SourcePort ||
                saved.TargetPort != edge.TargetPort || (saved.ManualPath is null) != (edge.ManualPathPoints is null) ||
                (saved.ManualPath is not null && edge.ManualPathPoints is not null &&
                 !saved.ManualPath.SequenceEqual(edge.ManualPathPoints))) return false;
        }
        return true;
    }

    internal static bool SameConfiguration(ToolConfiguration first, ToolConfiguration second)
    {
        if (ReferenceEquals(first, second)) return true;
        if (first.ToolId != second.ToolId || !first.ParameterBindingOrder.SequenceEqual(second.ParameterBindingOrder)) return false;
        static byte[] Fields(ToolConfiguration value)
        {
            using var stream = new MemoryStream();
            using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
                ParameterSettingCanonical.Write(writer, value.Fields.Capture());
            return stream.ToArray();
        }
        return Fields(first).AsSpan().SequenceEqual(Fields(second));
    }

    /// <summary>Called under the session gate after full binding reconciliation. The supplied resolver
    /// retains live parameter values for history replay; editing must never replay old runtime values.</summary>
    internal void Apply(WorkflowDefinition workflow, Func<NodeDefinition, ToolConfiguration, ToolConfiguration> resolveConfiguration)
    {
        ArgumentNullException.ThrowIfNull(workflow); ArgumentNullException.ThrowIfNull(resolveConfiguration);
        var configurations = Nodes.Select(state => resolveConfiguration(state.Node, state.Configuration)).ToArray();
        for (var i = 0; i < Nodes.Count; i++)
        {
            var state = Nodes[i]; var node = state.Node;
            node.Name = state.Name; node.DispName = state.DisplayName; node.IsEnabled = state.Enabled;
            node.PositionX = state.Position.X; node.PositionY = state.Position.Y; node.Configuration = configurations[i];
        }
        foreach (var state in Edges)
        {
            state.Edge.SourceNodeId = state.SourceNodeId; state.Edge.TargetNodeId = state.TargetNodeId;
            state.Edge.SourcePort = state.SourcePort; state.Edge.TargetPort = state.TargetPort;
            state.Edge.ManualPathPoints = state.ManualPath;
        }
        workflow.MutableNodes.Clear(); workflow.MutableNodes.AddRange(Nodes.Select(state => state.Node));
        workflow.MutableConnections.Clear(); workflow.MutableConnections.AddRange(Edges.Select(state => state.Edge));
    }
}

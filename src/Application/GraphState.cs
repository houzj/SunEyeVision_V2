using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

/// <summary>An editing snapshot containing actual graph identities, never execution-owned resources.</summary>
internal sealed class GraphState
{
    internal sealed record NodeState(NodeDefinition Node, string Name, string DisplayName, bool Enabled,
        CanvasPoint Position, ToolConfiguration Configuration);
    internal sealed record EdgeState(ConnectionDefinition Edge, string SourcePort, string TargetPort,
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
        workflow.Connections.Select(edge => new EdgeState(edge, edge.SourcePort, edge.TargetPort,
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
        }), draft.Edges.Select(item => new EdgeState(item.Edge, item.SourcePort, item.TargetPort,
            paths.TryGetValue(item.Edge, out var path) ? path : item.Edge.ManualPathPoints)));
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
            state.Edge.SourcePort = state.SourcePort; state.Edge.TargetPort = state.TargetPort;
            state.Edge.ManualPathPoints = state.ManualPath;
        }
        workflow.MutableNodes.Clear(); workflow.MutableNodes.AddRange(Nodes.Select(state => state.Node));
        workflow.MutableConnections.Clear(); workflow.MutableConnections.AddRange(Edges.Select(state => state.Edge));
    }
}

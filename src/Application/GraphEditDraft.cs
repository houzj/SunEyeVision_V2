using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

/// <summary>Private graph proposal; no live node, edge, history or result is changed while preparing.</summary>
public sealed class GraphEditDraft
{
    internal sealed record EdgeState(ConnectionDefinition Edge, string SourcePort, string TargetPort);
    private readonly WorkflowDefinition _workflow;
    private readonly GraphState _opening;
    private readonly long _openingHistoryRevision;
    private readonly List<NodeDefinition> _nodes;
    private readonly List<EdgeState> _edges;
    private readonly Dictionary<NodeDefinition, CanvasPoint> _positions = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<NodeDefinition, string> _displayNames = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<NodeDefinition, bool> _enabled = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ConnectionDefinition, IReadOnlyList<CanvasPoint>?> _manualPaths = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ConnectionDefinition, long> _pathOrder = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<NodeDefinition, long> _moveOrder = new(ReferenceEqualityComparer.Instance);
    private long _operationOrder;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private bool _sealed;
    internal GraphEditDraft(WorkflowDefinition workflow, long openingHistoryRevision)
    {
        _workflow = workflow;
        _opening = GraphState.Capture(workflow);
        _openingHistoryRevision = openingHistoryRevision;
        _nodes = workflow.Nodes.ToList();
        _edges = workflow.Connections.Select(edge => new EdgeState(edge, edge.SourcePort, edge.TargetPort)).ToList();
    }
    internal WorkflowDefinition Workflow => _workflow;
    internal GraphState Opening => _opening;
    internal long OpeningHistoryRevision => _openingHistoryRevision;
    internal IReadOnlyList<NodeDefinition> Nodes => _nodes;
    internal IReadOnlyList<EdgeState> Edges => _edges;
    internal IReadOnlyDictionary<NodeDefinition, CanvasPoint> Positions => _positions;
    internal IReadOnlyDictionary<NodeDefinition, string> DisplayNames => _displayNames;
    internal IReadOnlyDictionary<NodeDefinition, bool> EnabledStates => _enabled;
    internal IReadOnlyDictionary<ConnectionDefinition, IReadOnlyList<CanvasPoint>?> ManualPaths
    {
        get
        {
            var paths = new Dictionary<ConnectionDefinition, IReadOnlyList<CanvasPoint>?>(_manualPaths, ReferenceEqualityComparer.Instance);
            foreach (var edge in _edges)
            {
                var latestMove = _moveOrder.Where(pair => pair.Key.Id == edge.Edge.SourceNodeId || pair.Key.Id == edge.Edge.TargetNodeId)
                    .Select(pair => pair.Value).DefaultIfEmpty(0).Max();
                if (latestMove > _pathOrder.GetValueOrDefault(edge.Edge)) paths[edge.Edge] = null;
            }
            return paths;
        }
    }
    private void EnsureMutable()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("图编辑草稿只能在方案所属线程使用。");
        if (_sealed) throw new InvalidOperationException("图编辑草稿已冻结。");
    }
    internal void Seal() { EnsureMutable(); _sealed = true; }

    public void AddNode(NodeDefinition node)
    {
        EnsureMutable(); ArgumentNullException.ThrowIfNull(node);
        if (_nodes.Any(existing => existing.Id == node.Id)) throw new InvalidOperationException("流程中已有相同节点身份。");
        _nodes.Add(node);
    }
    public void AddConnection(ConnectionDefinition edge)
    {
        EnsureMutable(); ArgumentNullException.ThrowIfNull(edge);
        if (_edges.Any(existing => existing.Edge.Id == edge.Id)) throw new InvalidOperationException("流程中已有相同连线身份。");
        if (!_nodes.Any(node => node.Id == edge.SourceNodeId) || !_nodes.Any(node => node.Id == edge.TargetNodeId))
            throw new InvalidOperationException("连线端点必须属于当前流程。");
        ExecutionConnectionPolicy.EnsureCanAdd(_edges.Select(item => item.Edge), edge);
        _edges.Add(new EdgeState(edge, edge.SourcePort, edge.TargetPort));
    }
    public void MoveNode(NodeDefinition node, CanvasPoint position)
    {
        EnsureMutable(); ArgumentNullException.ThrowIfNull(node);
        if (!_nodes.Any(candidate => ReferenceEquals(candidate, node))) throw new InvalidOperationException("节点不属于当前流程。");
        if (node.Position == position) { _positions.Remove(node); _moveOrder.Remove(node); }
        else { _positions[node] = position; _moveOrder[node] = checked(++_operationOrder); }
    }
    public void RenameNode(NodeDefinition node, string displayName)
    {
        EnsureMutable(); ArgumentNullException.ThrowIfNull(node); ArgumentNullException.ThrowIfNull(displayName);
        if (!_nodes.Any(candidate => ReferenceEquals(candidate, node))) throw new InvalidOperationException("节点不属于当前流程。");
        var value = displayName.Trim();
        if (value.Length is 0 or > 64) throw new ArgumentException("节点名称须为1至64个字符。", nameof(displayName));
        if (value == node.DispName) _displayNames.Remove(node); else _displayNames[node] = value;
    }

    public void SetNodeEnabled(NodeDefinition node, bool enabled)
    {
        EnsureMutable(); ArgumentNullException.ThrowIfNull(node);
        if (!_nodes.Any(candidate => ReferenceEquals(candidate, node))) throw new InvalidOperationException("节点不属于当前流程。");
        if (node.IsEnabled == enabled) _enabled.Remove(node); else _enabled[node] = enabled;
    }
    public void EditConnectionPorts(ConnectionDefinition edge, string sourcePort, string targetPort)
    {
        EnsureMutable(); ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(sourcePort); ArgumentNullException.ThrowIfNull(targetPort);
        var index = _edges.FindIndex(item => ReferenceEquals(item.Edge, edge));
        if (index < 0) throw new InvalidOperationException("连线不属于当前流程。");
        _edges[index] = _edges[index] with { SourcePort = sourcePort, TargetPort = targetPort };
        _manualPaths[edge] = null;
        _pathOrder[edge] = checked(++_operationOrder);
    }
    public void EditConnectionPath(ConnectionDefinition edge, IReadOnlyList<CanvasPoint>? points)
    {
        EnsureMutable(); ArgumentNullException.ThrowIfNull(edge);
        if (!_edges.Any(item => ReferenceEquals(item.Edge, edge))) throw new InvalidOperationException("连线不属于当前流程。");
        _manualPaths[edge] = points is null ? null : Array.AsReadOnly(points.ToArray());
        _pathOrder[edge] = checked(++_operationOrder);
    }
    public void RemoveConnection(ConnectionDefinition edge)
    {
        EnsureMutable(); ArgumentNullException.ThrowIfNull(edge);
        var index = _edges.FindIndex(item => ReferenceEquals(item.Edge, edge));
        if (index < 0) throw new InvalidOperationException("连线不属于当前流程。");
        _edges.RemoveAt(index); _manualPaths.Remove(edge); _pathOrder.Remove(edge);
    }
    /// <summary>Apply the current VM-style predecessor/successor repair in the order nodes are selected.</summary>
    public void DeleteNodesAndConnections(IReadOnlyList<NodeDefinition> nodes, IReadOnlyList<ConnectionDefinition> explicitEdges)
    {
        EnsureMutable(); ArgumentNullException.ThrowIfNull(nodes); ArgumentNullException.ThrowIfNull(explicitEdges);
        if (nodes.Distinct(ReferenceEqualityComparer.Instance).Count() != nodes.Count ||
            nodes.Any(node => !_nodes.Any(current => ReferenceEquals(current, node))) ||
            explicitEdges.Distinct(ReferenceEqualityComparer.Instance).Count() != explicitEdges.Count ||
            explicitEdges.Any(edge => !_edges.Any(current => ReferenceEquals(current.Edge, edge))))
            throw new InvalidOperationException("删除范围包含重复或已退出的节点、连线。");
        foreach (var node in nodes)
        {
            var incoming = _edges.Where(item => item.Edge.TargetNodeId == node.Id).ToArray();
            var outgoing = _edges.Where(item => item.Edge.SourceNodeId == node.Id).ToArray();
            foreach (var next in outgoing)
            foreach (var previous in incoming)
            {
                if (previous.Edge.SourceNodeId == next.Edge.TargetNodeId) continue;
                var index = _edges.FindIndex(item => item.Edge.SourceNodeId == previous.Edge.SourceNodeId && item.Edge.TargetNodeId == next.Edge.TargetNodeId);
                if (index >= 0)
                {
                    _edges[index] = _edges[index] with { SourcePort = previous.SourcePort, TargetPort = next.TargetPort };
                    _manualPaths[_edges[index].Edge] = null;
                    _pathOrder[_edges[index].Edge] = checked(++_operationOrder);
                }
                else
                {
                    var bridge = new ConnectionDefinition(Guid.NewGuid().ToString("N"), previous.Edge.SourceNodeId,
                        next.Edge.TargetNodeId, previous.SourcePort, next.TargetPort);
                    if (ExecutionConnectionPolicy.Rejection(_edges.Select(item => item.Edge), bridge) is null)
                        _edges.Add(new EdgeState(bridge, bridge.SourcePort, bridge.TargetPort));
                }
            }
            foreach (var edge in incoming.Concat(outgoing).Distinct()) { _edges.Remove(edge); _manualPaths.Remove(edge.Edge); _pathOrder.Remove(edge.Edge); }
            _nodes.Remove(node); _positions.Remove(node); _moveOrder.Remove(node); _displayNames.Remove(node); _enabled.Remove(node);
        }
        foreach (var edge in explicitEdges) { _edges.RemoveAll(item => ReferenceEquals(item.Edge, edge)); _manualPaths.Remove(edge); _pathOrder.Remove(edge); }
    }
}









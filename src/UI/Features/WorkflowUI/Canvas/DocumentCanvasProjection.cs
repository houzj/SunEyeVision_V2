using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;

/// <summary>画布对单个正式流程的只读投影；图的编辑和历史始终由方案会话提交。</summary>
public sealed class DocumentCanvasProjection : IDisposable
{
    private readonly DocumentSession _session;
    private readonly string _workflowId;
    private readonly WorkflowDefinition _workflow;
    private readonly ObservableCollection<CanvasNodeProjection> _nodes = new();
    private readonly ObservableCollection<CanvasConnectionProjection> _connections = new();
    private bool _disposed;

    public DocumentCanvasProjection(DocumentSession session, string workflowId)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _workflowId = workflowId ?? throw new ArgumentNullException(nameof(workflowId));
        _workflow = _session.GetWorkflows().Single(flow => flow.Id == _workflowId);
        Nodes = new(_nodes);
        Connections = new(_connections);
        Synchronize();
        _session.Changed += OnChanged;
    }

    public ReadOnlyObservableCollection<CanvasNodeProjection> Nodes { get; }
    public ReadOnlyObservableCollection<CanvasConnectionProjection> Connections { get; }
    public event EventHandler? Changed;
    public event Action<IReadOnlyList<NodeDefinition>>? NodesDeleted;

    public NodeDefinition CreateNode(string toolId, CanvasPoint position, CanvasConnectionProjection? insertion = null,
        CanvasNodeProjection? predecessor = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (insertion is not null && predecessor is not null)
            throw new ArgumentException("不能同时指定插线目标和自动连接前驱。");
        if (insertion is not null && !_connections.Contains(insertion) || predecessor is not null && !_nodes.Contains(predecessor))
            throw new InvalidOperationException("插入目标已不属于当前画布。");
        var draft = _session.PrepareGraphEdit(_workflowId);
        var node = _session.CreateNode(draft, toolId, position);
        if (insertion is not null)
        {
            draft.RemoveConnection(insertion.Definition);
            draft.AddConnection(new ConnectionDefinition(Guid.NewGuid().ToString("N"), insertion.SourceNodeId, node.Id,
                insertion.SourcePort, insertion.TargetPort));
            draft.AddConnection(new ConnectionDefinition(Guid.NewGuid().ToString("N"), node.Id, insertion.TargetNodeId,
                insertion.SourcePort, insertion.TargetPort));
        }
        else if (predecessor is not null)
            draft.AddConnection(new ConnectionDefinition(Guid.NewGuid().ToString("N"), predecessor.Id, node.Id, "BottomPort", "TopPort"));
        _session.CommitGraphEdit(draft, "添加节点");
        return node;
    }

    public void MoveNodes(IReadOnlyDictionary<CanvasNodeProjection, CanvasPoint> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (positions.Keys.Any(node => !_nodes.Contains(node)))
            throw new InvalidOperationException("移动的节点不属于当前画布。");
        Edit("移动节点", draft =>
        {
            foreach (var item in positions) draft.MoveNode(item.Key.Definition, item.Value);
        });
    }

    public void Delete(IReadOnlyList<CanvasNodeProjection> nodes, IReadOnlyList<CanvasConnectionProjection> connections)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(connections);
        if (nodes.Any(node => !_nodes.Contains(node)) || connections.Any(edge => !_connections.Contains(edge)))
            throw new InvalidOperationException("删除的节点或连线不属于当前画布。");
        Edit("删除节点和连线", draft => draft.DeleteNodesAndConnections(nodes.Select(node => node.Definition).ToArray(),
            connections.Select(edge => edge.Definition).ToArray()));
        if(nodes.Count>0) NodesDeleted?.Invoke(nodes.Select(node=>node.Definition).ToArray());
    }

    public void Rename(CanvasNodeProjection node, string name)
    {
        if (!_nodes.Contains(node)) throw new InvalidOperationException("节点不属于当前画布。");
        Edit("重命名节点", draft => draft.RenameNode(node.Definition, name));
    }

    public void Connect(ConnectionDefinition connection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(connection);
        var existing = _connections.FirstOrDefault(edge => edge.SourceNodeId == connection.SourceNodeId &&
            edge.TargetNodeId == connection.TargetNodeId);
        if (existing is not null)
        {
            if (existing.SourcePort != connection.SourcePort || existing.TargetPort != connection.TargetPort)
                EditPorts(existing, connection.SourcePort, connection.TargetPort);
            return;
        }
        Edit("添加连线", draft => draft.AddConnection(connection));
    }

    public void EditPorts(CanvasConnectionProjection connection, string sourcePort, string targetPort)
    {
        if (!_connections.Contains(connection)) throw new InvalidOperationException("连线不属于当前画布。");
        Edit("修改连线端口", draft => draft.EditConnectionPorts(connection.Definition, sourcePort, targetPort));
    }

    public void InsertNode(CanvasNodeProjection node, CanvasConnectionProjection original, CanvasPoint position)
    {
        if (!_nodes.Contains(node) || !_connections.Contains(original) ||
            original.SourceNodeId == node.Id || original.TargetNodeId == node.Id)
            throw new InvalidOperationException("节点或连线已失效，或节点是该连线的端点。");
        var remaining = _connections.Where(edge => !ReferenceEquals(edge, original)).ToArray();
        var pairs = new[] { (original.SourceNodeId, node.Id), (node.Id, original.TargetNodeId) };
        Edit("插入连线", draft =>
        {
            draft.RemoveConnection(original.Definition);
            foreach (var pair in pairs)
            {
                var existing = remaining.SingleOrDefault(edge => edge.SourceNodeId == pair.Item1 && edge.TargetNodeId == pair.Item2);
                if (existing is not null)
                {
                    if (existing.SourcePort != original.SourcePort || existing.TargetPort != original.TargetPort)
                        draft.EditConnectionPorts(existing.Definition, original.SourcePort, original.TargetPort);
                }
                else draft.AddConnection(new ConnectionDefinition(Guid.NewGuid().ToString("N"), pair.Item1, pair.Item2,
                    original.SourcePort, original.TargetPort));
            }
            draft.MoveNode(node.Definition, position);
        });
    }

    public void EditPath(CanvasConnectionProjection connection, IReadOnlyList<CanvasPoint>? points)
    {
        if (!_connections.Contains(connection)) throw new InvalidOperationException("连线不属于当前画布。");
        Edit("修改连线路径", draft => draft.EditConnectionPath(connection.Definition, points));
    }

    public void SetEnabled(CanvasNodeProjection node, bool enabled)
    {
        if (!_nodes.Contains(node)) throw new InvalidOperationException("节点不属于当前画布。");
        Edit("设置节点状态", draft => draft.SetNodeEnabled(node.Definition, enabled));
    }
    public bool Undo() { ObjectDisposedException.ThrowIf(_disposed, this); return _session.Undo(); }
    public bool Redo() { ObjectDisposedException.ThrowIf(_disposed, this); return _session.Redo(); }

    public void Edit(string description, Action<GraphEditDraft> prepare)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(prepare);
        var draft = _session.PrepareGraphEdit(_workflowId);
        prepare(draft);
        _session.CommitGraphEdit(draft, description);
    }

    private void OnChanged(object? sender, DocumentChangeSet change)
    {
        if (_disposed || !ReferenceEquals(sender, _session) || change.EditSessionId != _session.EditSessionId) return;
        // A removed root can still have a live tab until the root collection observer
        // disposes it. Never resolve its id to a replacement root in that interval.
        if (!_session.GetWorkflows().Any(flow => ReferenceEquals(flow, _workflow))) return;
        if (change.ChangedNodes.Any(x => x.Identity.WorkflowId == _workflowId) ||
            change.RemovedNodes.Any(x => x.Identity.WorkflowId == _workflowId) ||
            change.ChangedConnections.Any(x => x.WorkflowId == _workflowId))
        {
            Synchronize();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Synchronize()
    {
        var actualNodes = _session.GetNodes(_workflowId);
        var actualEdges = _session.GetConnections(_workflowId);
        var existing = _nodes.ToDictionary(x => x.Definition, (IEqualityComparer<NodeDefinition>)ReferenceEqualityComparer.Instance);
        var nextNodes = actualNodes.Select(node => existing.TryGetValue(node, out var projection)
            ? projection : new CanvasNodeProjection(node, () => _session.GetNodeCatalog(_workflowId, node))).ToArray();
        Replace(_nodes, nextNodes);
        foreach (var node in nextNodes) node.Refresh();
        var existingEdges = _connections.ToDictionary(x => x.Definition, (IEqualityComparer<ConnectionDefinition>)ReferenceEqualityComparer.Instance);
        var nextEdges = actualEdges.Select(edge => existingEdges.TryGetValue(edge, out var projection)
            ? projection : new CanvasConnectionProjection(edge)).ToArray();
        Replace(_connections, nextEdges);
        foreach (var edge in nextEdges) edge.Refresh();
    }

    private static void Replace<T>(ObservableCollection<T> collection, IReadOnlyList<T> desired) where T : class
    {
        for (var index = 0; index < desired.Count; index++)
        {
            if (index < collection.Count && ReferenceEquals(collection[index], desired[index])) continue;
            var found = -1;
            for (var cursor = index + 1; cursor < collection.Count; cursor++)
                if (ReferenceEquals(collection[cursor], desired[index])) { found = cursor; break; }
            if (found >= 0) collection.Move(found, index); else collection.Insert(index, desired[index]);
        }
        while (collection.Count > desired.Count) collection.RemoveAt(collection.Count - 1);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Changed -= OnChanged;
        Changed = null;
        _nodes.Clear();
        _connections.Clear();
    }
}

/// <summary>连线只读引用及画布临时状态；路径变化来自正式方案通知。</summary>
public sealed class CanvasConnectionProjection : INotifyPropertyChanged
{
    private bool _selected, _hovered, _showPathPoints;
    private CanvasPoint _arrowPosition;
    private double _arrowAngle;
    private int _pathUpdateCounter;
    public CanvasConnectionProjection(ConnectionDefinition definition) => Definition = definition;
    public ConnectionDefinition Definition { get; }
    public string Id => Definition.Id;
    public string SourceNodeId => Definition.SourceNodeId;
    public string TargetNodeId => Definition.TargetNodeId;
    public string SourcePort => Definition.SourcePort;
    public string TargetPort => Definition.TargetPort;
    public IReadOnlyList<CanvasPoint>? ManualPathPoints => Definition.ManualPathPoints;
    public CanvasPoint[]? PreviewPathPoints { get; set; }
    public ObservableCollection<CanvasPoint> PathPoints { get; } = new();
    public CanvasPoint ArrowPosition { get => _arrowPosition; set { _arrowPosition = value; Notify(nameof(ArrowPosition)); Notify(nameof(ArrowX)); Notify(nameof(ArrowY)); } }
    public double ArrowX => ArrowPosition.X;
    public double ArrowY => ArrowPosition.Y;
    public double ArrowAngle { get => _arrowAngle; set { _arrowAngle = value; Notify(nameof(ArrowAngle)); } }
    public int PathUpdateCounter { get => _pathUpdateCounter; set { _pathUpdateCounter = value; Notify(nameof(PathUpdateCounter)); } }
    public bool ShowPathPoints { get => _showPathPoints; set { if (_showPathPoints != value) { _showPathPoints = value; Notify(nameof(ShowPathPoints)); } } }
    public bool IsSelected { get => _selected; set { if (_selected != value) { _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } } }
    public bool IsHovered { get => _hovered; set { if (_hovered != value) { _hovered = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHovered))); } } }
    public void InvalidatePath() => PathUpdateCounter = checked(PathUpdateCounter + 1);
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    internal void Refresh() => Notify(string.Empty);
}

/// <summary>只组合实际节点，不复制或修改正式领域实体。</summary>
public interface ICanvasToolIconSource { string? ToolIconKey { get; } }

public sealed class CanvasNodeProjection : INotifyPropertyChanged, ICanvasToolIconSource
{
    private bool _selected, _editingName;
    private string _renameBuffer = string.Empty;
    private readonly Func<SunEyeVision.Plugin.Contracts.ToolCatalogDescriptor?>? _catalog;
    public CanvasNodeProjection(NodeDefinition definition) => Definition = definition;
    internal CanvasNodeProjection(NodeDefinition definition, Func<SunEyeVision.Plugin.Contracts.ToolCatalogDescriptor?> catalog)
    { Definition = definition; _catalog = catalog; }
    public NodeDefinition Definition { get; }
    public SunEyeVision.Plugin.Contracts.ToolCatalogDescriptor? Catalog
    {
        get { try { return _catalog?.Invoke(); } catch (InvalidOperationException) { return null; } }
    }
    public string? ToolIconKey => Catalog?.IconKey;
    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public string DispName => Definition.DispName;
    public int GlobalIndex => Definition.GlobalIndex;
    public string ToolType => Definition.ToolType;
    /// <summary>节点外观类型，由工具目录声明；未声明时按原件默认卡片呈现。</summary>
    public string NodeDisplayType => Catalog?.NodeDisplayType ?? "DEFAULT";
    public string? SubroutineId => Definition.SubroutineId;
    public bool IsEnabled => Definition.IsEnabled;
    public double PositionX => Definition.PositionX;
    public double PositionY => Definition.PositionY;
    public CanvasPoint Position => Definition.Position;
    public double X => Definition.PositionX;
    public double Y => Definition.PositionY;
    public double Width => Definition.Width;
    public double Height => Definition.Height;
    public bool IsSubroutineNode => string.Equals(ToolType, "Subroutine", StringComparison.OrdinalIgnoreCase);
    public bool IsGroupLoopEnabled => false;
    public bool IsToolboxPreview => false;
    public NodeStyleConfig StyleConfigTyped => IsSubroutineNode ? NodeStyles.Group : NodeStyles.Standard;
    public double VisualWidth => StyleConfigTyped.NodeWidth;
    public double VisualHeight => StyleConfigTyped.NodeHeight;
    public double HitAreaWidth => VisualWidth + 20;
    public double HitAreaHeight => VisualHeight + 20;
    public double HitAreaLeft => X - HitAreaWidth / 2;
    public double HitAreaTop => Y - HitAreaHeight / 2;
    public Rect NodeRect => StyleConfigTyped.GetNodeRect(new Point(X, Y));
    public Point TopPortPosition => StyleConfigTyped.GetTopPortPosition(new Point(X, Y));
    public Point BottomPortPosition => StyleConfigTyped.GetBottomPortPosition(new Point(X, Y));
    public Point LeftPortPosition => StyleConfigTyped.GetLeftPortPosition(new Point(X, Y));
    public Point RightPortPosition => StyleConfigTyped.GetRightPortPosition(new Point(X, Y));
    public bool IsSelected { get => _selected; set { if (_selected != value) { _selected = value; Notify(nameof(IsSelected)); } } }
    public bool IsEditingName { get => _editingName; set { if (_editingName != value) { _editingName = value; Notify(nameof(IsEditingName)); } } }
    public string RenameBuffer { get => _renameBuffer; set { if (_renameBuffer != value) { _renameBuffer = value; Notify(nameof(RenameBuffer)); } } }
    public void BeginRename() { RenameBuffer = DispName; IsEditingName = true; }
    public void CancelRename() { IsEditingName = false; RenameBuffer = DispName; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    internal void Refresh() => Notify(string.Empty);
}

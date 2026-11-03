using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private WorkflowTabViewModel? _canvasGeometryTab;
    private INotifyCollectionChanged? _canvasGeometryNodes, _canvasGeometryConnections;
    private readonly HashSet<CanvasConnectionProjection> _canvasGeometryConnectionSubscriptions = new();
    private readonly HashSet<CanvasNodeProjection> _canvasGeometrySubscriptions = new();
    private int _canvasGeometryGeneration;
    private bool _canvasGeometryQueued;
    private void InitializeCanvasGeometryTracking()
    {
        DataContextChanged += (_, _) => BindCanvasGeometry();
        Loaded += (_, _) => BindCanvasGeometry();
        Unloaded += (_, _) => UnbindCanvasGeometry();
        BindCanvasGeometry();
    }
    private void UnbindCanvasGeometry()
    {
        ++_canvasGeometryGeneration; _canvasGeometryQueued = false;
        if (_canvasGeometryTab != null) _canvasGeometryTab.WorkflowContentChanged -= CanvasGeometryContentChanged;
        if (_canvasGeometryNodes != null) _canvasGeometryNodes.CollectionChanged -= CanvasGeometryCollectionChanged;
        foreach (var node in _canvasGeometrySubscriptions) node.PropertyChanged -= CanvasNodeGeometryChanged;
        if (_canvasGeometryConnections != null) _canvasGeometryConnections.CollectionChanged -= CanvasGeometryCollectionChanged;
        foreach (var edge in _canvasGeometryConnectionSubscriptions) edge.PropertyChanged -= CanvasConnectionGeometryChanged;
        _canvasGeometryConnectionSubscriptions.Clear();
        _canvasGeometrySubscriptions.Clear(); _canvasGeometryTab = null; _canvasGeometryNodes = null; _canvasGeometryConnections = null;
    }
    private void BindCanvasGeometry()
    {
        UnbindCanvasGeometry();
        _canvasGeometryTab = CurrentWorkflowTab;
        if (_canvasGeometryTab == null) return;
        _canvasGeometryTab.WorkflowContentChanged += CanvasGeometryContentChanged;
        _canvasGeometryNodes = _canvasGeometryTab.DocumentCanvas?.Nodes;
        if (_canvasGeometryNodes != null) _canvasGeometryNodes.CollectionChanged += CanvasGeometryCollectionChanged;
        _canvasGeometryConnections = _canvasGeometryTab.DocumentCanvas?.Connections;
        if (_canvasGeometryConnections != null) _canvasGeometryConnections.CollectionChanged += CanvasGeometryCollectionChanged;
        SubscribeCanvasGeometryNodes();
    }
    private void CanvasGeometryContentChanged(object? sender, EventArgs e) { BindCanvasGeometry(); QueueCanvasGeometryRefresh(); }
    private void CanvasGeometryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Resources["SmartPathMultiConverter"] is SunEyeVision.UI.Converters.Path.SmartPathMultiConverter converter) converter.ClearCache();
        SubscribeCanvasGeometryNodes(); QueueCanvasGeometryRefresh();
    }
    private void SubscribeCanvasGeometryNodes()
    {
        var current = _canvasGeometryTab?.DocumentCanvas?.Nodes.ToHashSet() ?? new();
        foreach (var node in _canvasGeometrySubscriptions.Except(current).ToArray())
        { node.PropertyChanged -= CanvasNodeGeometryChanged; _canvasGeometrySubscriptions.Remove(node); }
        foreach (var node in current.Except(_canvasGeometrySubscriptions))
        { node.PropertyChanged += CanvasNodeGeometryChanged; _canvasGeometrySubscriptions.Add(node); }
        var edges = _canvasGeometryTab?.DocumentCanvas?.Connections.ToHashSet() ?? new();
        foreach (var edge in _canvasGeometryConnectionSubscriptions.Except(edges).ToArray())
        { edge.PropertyChanged -= CanvasConnectionGeometryChanged; _canvasGeometryConnectionSubscriptions.Remove(edge); }
        foreach (var edge in edges.Except(_canvasGeometryConnectionSubscriptions))
        { edge.PropertyChanged += CanvasConnectionGeometryChanged; _canvasGeometryConnectionSubscriptions.Add(edge); }
    }
    private void CanvasConnectionGeometryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(CanvasConnectionProjection.SourcePort) or nameof(CanvasConnectionProjection.TargetPort) or
            nameof(CanvasConnectionProjection.SourceNodeId) or nameof(CanvasConnectionProjection.TargetNodeId)) QueueCanvasGeometryRefresh();
    }
    private void CanvasNodeGeometryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasNodeProjection.IsSelected))
        {
            var generation = _canvasGeometryGeneration;
            Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
            {
                if (generation == _canvasGeometryGeneration) DrawCanvasSelectionFrame();
            }));
            return;
        }
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(CanvasNodeProjection.Position) or nameof(CanvasNodeProjection.Width) or nameof(CanvasNodeProjection.Height) or nameof(CanvasNodeProjection.VisualHeight) or nameof(CanvasNodeProjection.VisualWidth))
            QueueCanvasGeometryRefresh();
    }
    private void QueueCanvasGeometryRefresh()
    {
        // 节点也是避障物，移动任何节点都可能改变其他连接的路径。
        _connectionPathCache?.MarkAllDirty();
        if (_canvasGeometryQueued || _canvasGeometryTab == null) return;
        _canvasGeometryQueued = true; var generation = _canvasGeometryGeneration;
        Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
        {
            if (generation != _canvasGeometryGeneration) return;
            _canvasGeometryQueued = false;
            _connectionPathCache?.MarkAllDirty();
            if (_canvasGeometryTab == null) return;
            if (_canvasGeometryTab.DocumentCanvas is { } canvas)
                foreach (var connection in canvas.Connections) connection.InvalidatePath();
            if (_canvasSelectedWire != null) DrawCanvasWireHandles();
            UpdateBoundingRectangle(); DrawCanvasSelectionFrame();
        }));
    }
}

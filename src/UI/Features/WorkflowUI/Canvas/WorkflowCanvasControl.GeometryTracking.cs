using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private WorkflowTabViewModel? _vmGeometryTab;
    private INotifyCollectionChanged? _vmGeometryNodes, _vmGeometryConnections;
    private readonly HashSet<CanvasConnectionProjection> _vmGeometryConnectionSubscriptions = new();
    private readonly HashSet<CanvasNodeProjection> _vmGeometrySubscriptions = new();
    private int _vmGeometryGeneration;
    private bool _vmGeometryQueued;
    private void InitializeVmGeometryTracking()
    {
        DataContextChanged += (_, _) => BindVmGeometry();
        Loaded += (_, _) => BindVmGeometry();
        Unloaded += (_, _) => UnbindVmGeometry();
        BindVmGeometry();
    }
    private void UnbindVmGeometry()
    {
        ++_vmGeometryGeneration; _vmGeometryQueued = false;
        if (_vmGeometryTab != null) _vmGeometryTab.WorkflowContentChanged -= VmGeometryContentChanged;
        if (_vmGeometryNodes != null) _vmGeometryNodes.CollectionChanged -= VmGeometryCollectionChanged;
        foreach (var node in _vmGeometrySubscriptions) node.PropertyChanged -= VmNodeGeometryChanged;
        if (_vmGeometryConnections != null) _vmGeometryConnections.CollectionChanged -= VmGeometryCollectionChanged;
        foreach (var edge in _vmGeometryConnectionSubscriptions) edge.PropertyChanged -= VmConnectionGeometryChanged;
        _vmGeometryConnectionSubscriptions.Clear();
        _vmGeometrySubscriptions.Clear(); _vmGeometryTab = null; _vmGeometryNodes = null; _vmGeometryConnections = null;
    }
    private void BindVmGeometry()
    {
        UnbindVmGeometry();
        _vmGeometryTab = CurrentWorkflowTab;
        if (_vmGeometryTab == null) return;
        _vmGeometryTab.WorkflowContentChanged += VmGeometryContentChanged;
        _vmGeometryNodes = _vmGeometryTab.DocumentCanvas?.Nodes;
        if (_vmGeometryNodes != null) _vmGeometryNodes.CollectionChanged += VmGeometryCollectionChanged;
        _vmGeometryConnections = _vmGeometryTab.DocumentCanvas?.Connections;
        if (_vmGeometryConnections != null) _vmGeometryConnections.CollectionChanged += VmGeometryCollectionChanged;
        SubscribeVmGeometryNodes();
    }
    private void VmGeometryContentChanged(object? sender, EventArgs e) { BindVmGeometry(); QueueVmGeometryRefresh(); }
    private void VmGeometryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Resources["SmartPathMultiConverter"] is SunEyeVision.UI.Converters.Path.SmartPathMultiConverter converter) converter.ClearCache();
        SubscribeVmGeometryNodes(); QueueVmGeometryRefresh();
    }
    private void SubscribeVmGeometryNodes()
    {
        var current = _vmGeometryTab?.DocumentCanvas?.Nodes.ToHashSet() ?? new();
        foreach (var node in _vmGeometrySubscriptions.Except(current).ToArray())
        { node.PropertyChanged -= VmNodeGeometryChanged; _vmGeometrySubscriptions.Remove(node); }
        foreach (var node in current.Except(_vmGeometrySubscriptions))
        { node.PropertyChanged += VmNodeGeometryChanged; _vmGeometrySubscriptions.Add(node); }
        var edges = _vmGeometryTab?.DocumentCanvas?.Connections.ToHashSet() ?? new();
        foreach (var edge in _vmGeometryConnectionSubscriptions.Except(edges).ToArray())
        { edge.PropertyChanged -= VmConnectionGeometryChanged; _vmGeometryConnectionSubscriptions.Remove(edge); }
        foreach (var edge in edges.Except(_vmGeometryConnectionSubscriptions))
        { edge.PropertyChanged += VmConnectionGeometryChanged; _vmGeometryConnectionSubscriptions.Add(edge); }
    }
    private void VmConnectionGeometryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(CanvasConnectionProjection.SourcePort) or nameof(CanvasConnectionProjection.TargetPort) or
            nameof(CanvasConnectionProjection.SourceNodeId) or nameof(CanvasConnectionProjection.TargetNodeId)) QueueVmGeometryRefresh();
    }
    private void VmNodeGeometryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasNodeProjection.IsSelected))
        {
            var generation = _vmGeometryGeneration;
            Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
            {
                if (generation == _vmGeometryGeneration) DrawVmSelectionFrame();
            }));
            return;
        }
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(CanvasNodeProjection.Position) or nameof(CanvasNodeProjection.Width) or nameof(CanvasNodeProjection.Height) or nameof(CanvasNodeProjection.VisualHeight) or nameof(CanvasNodeProjection.VisualWidth))
            QueueVmGeometryRefresh();
    }
    private void QueueVmGeometryRefresh()
    {
        // 节点也是避障物，移动任何节点都可能改变其他连接的路径。
        _connectionPathCache?.MarkAllDirty();
        if (_vmGeometryQueued || _vmGeometryTab == null) return;
        _vmGeometryQueued = true; var generation = _vmGeometryGeneration;
        Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
        {
            if (generation != _vmGeometryGeneration) return;
            _vmGeometryQueued = false;
            _connectionPathCache?.MarkAllDirty();
            if (_vmGeometryTab == null) return;
            if (_vmGeometryTab.DocumentCanvas is { } canvas)
                foreach (var connection in canvas.Connections) connection.InvalidatePath();
            if (_vmSelectedWire != null) DrawVmWireHandles();
            UpdateBoundingRectangle(); DrawVmSelectionFrame();
        }));
    }
}

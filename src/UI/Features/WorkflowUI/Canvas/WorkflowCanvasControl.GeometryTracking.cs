using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private WorkflowTabViewModel? _vmGeometryTab;
    private INotifyCollectionChanged? _vmGeometryNodes;
    private readonly HashSet<WorkflowNode> _vmGeometrySubscriptions = new();
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
        _vmGeometrySubscriptions.Clear(); _vmGeometryTab = null; _vmGeometryNodes = null;
    }
    private void BindVmGeometry()
    {
        UnbindVmGeometry();
        _vmGeometryTab = CurrentWorkflowTab;
        if (_vmGeometryTab == null) return;
        _vmGeometryTab.WorkflowContentChanged += VmGeometryContentChanged;
        _vmGeometryNodes = _vmGeometryTab.WorkflowNodes;
        _vmGeometryNodes.CollectionChanged += VmGeometryCollectionChanged;
        SubscribeVmGeometryNodes();
    }
    private void VmGeometryContentChanged(object? sender, EventArgs e) { BindVmGeometry(); QueueVmGeometryRefresh(); }
    private void VmGeometryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    { SubscribeVmGeometryNodes(); QueueVmGeometryRefresh(); }
    private void SubscribeVmGeometryNodes()
    {
        var current = _vmGeometryTab?.WorkflowNodes.OfType<WorkflowNode>().ToHashSet() ?? new();
        foreach (var node in _vmGeometrySubscriptions.Except(current).ToArray())
        { node.PropertyChanged -= VmNodeGeometryChanged; _vmGeometrySubscriptions.Remove(node); }
        foreach (var node in current.Except(_vmGeometrySubscriptions))
        { node.PropertyChanged += VmNodeGeometryChanged; _vmGeometrySubscriptions.Add(node); }
    }
    private void VmNodeGeometryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkflowNode.Position) or nameof(WorkflowNode.Width) or nameof(WorkflowNode.Height))
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
            foreach (var connection in _vmGeometryTab.WorkflowConnections) connection.InvalidatePath();
            if (_vmSelectedWire != null) DrawVmWireHandles();
            UpdateBoundingRectangle();
        }));
    }
}
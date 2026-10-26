using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Rendering;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private WorkflowTabViewModel? _vmConnectionTab;
    private string? _vmConnectionStamp;
    private WorkflowNode? _vmConnectionTarget;
    private bool _vmConnectionStarted;
    private static readonly string[] VmSides = { "LeftPort", "TopPort", "RightPort", "BottomPort" };
    private void BeginVmConnection(WorkflowNode node, string side)
    {
        CancelVmCanvasGesture();
        var tab = _viewModel?.WorkflowTabViewModel.SelectedTab;
        if (!CanEditVmCanvas || tab == null || !tab.WorkflowNodes.Contains(node) || !VmSides.Contains(side)) return;
        ClearVmWireSelection(); WorkflowCanvas.Focus();
        _vmConnectionTab = tab; _vmConnectionStamp = VmGraphStamp(tab); _vmConnectionTarget = null; _vmConnectionStarted = false;
        _dragConnectionSourceNode = node; _dragConnectionSourcePort = side; _isDraggingConnection = true;
        _dragConnectionStartPoint = GetPortPositionByName(node, side);
    }
    private bool ValidVmConnection() => CanEditVmCanvas && _vmConnectionTab != null &&
        ReferenceEquals(_viewModel?.WorkflowTabViewModel.SelectedTab, _vmConnectionTab) && VmGraphStamp(_vmConnectionTab) == _vmConnectionStamp;
    private static IEnumerable<VmExecutionPort> VmPorts(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is VmExecutionPort port) yield return port;
            foreach (var nested in VmPorts(child)) yield return nested;
        }
    }
    private void PreviewVmConnection(MouseEventArgs e)
    {
        if (!ValidVmConnection() || e.LeftButton != MouseButtonState.Pressed) { CancelVmCanvasGesture(); return; }
        var pointer = e.GetPosition(WorkflowCanvas);
        if (!_vmConnectionStarted)
        {
            _vmConnectionStarted = true;
            WorkflowCanvas.CaptureMouse();
            // 捕获会触发旧持有者的 LostMouseCapture；只继续有效会话。
            if (!_isDraggingConnection) return;
        }
        var source = _dragConnectionSourceNode!;
        _vmConnectionTarget = _vmConnectionTab!.WorkflowNodes.OfType<WorkflowNode>().Reverse().FirstOrDefault(node =>
        {
            if (ReferenceEquals(node, source)) return false;
            var bounds = node.NodeRect; bounds.Inflate(10, 10);
            return bounds.Contains(pointer) || VmSides.Any(side => VmPortHitRect(node, side).Contains(pointer));
        });
        var targetSide = _vmConnectionTarget == null ? null : VmNearestSide(_vmConnectionTarget, pointer);
        foreach (var port in VmPorts(WorkflowCanvas))
        {
            var node = port.DataContext as WorkflowNode;
            port.Visibility = ReferenceEquals(node, source) || ReferenceEquals(node, _vmConnectionTarget) ? Visibility.Visible : Visibility.Collapsed;
            port.IsHighlighted = ReferenceEquals(node, _vmConnectionTarget) && port.Tag as string == targetSide;
        }
        if (_tempConnectionLine != null)
        {
            _tempConnectionLine.Visibility = Visibility.Visible;
            _tempConnectionLine.StrokeDashArray = new DoubleCollection { 5, 2.5 };
        }
        // 恢复预览以鼠标为终点；目标仅作高亮，松开时才计算最终锚点。
        UpdateTempConnectionPath(_dragConnectionStartPoint, pointer);
        e.Handled = true;
    }
    private Rect VmPortHitRect(WorkflowNode node, string side)
    {
        var a = GetPortPositionByName(node, side);
        return side switch
        {
            "LeftPort" => new Rect(a.X - 20, a.Y - 15, 20, 30),
            "RightPort" => new Rect(a.X, a.Y - 15, 20, 30),
            "TopPort" => new Rect(a.X - 15, a.Y - 20, 30, 20),
            _ => new Rect(a.X - 15, a.Y, 30, 20)
        };
    }
    private string VmNearestSide(WorkflowNode node, Point pointer) => VmSides.OrderBy(side => (GetPortPositionByName(node, side) - pointer).LengthSquared).First();
    private void FinishVmConnection(Point pointer)
    {
        var source = _dragConnectionSourceNode; var side = _dragConnectionSourcePort;
        var target = _vmConnectionTarget;
        var commit = _vmConnectionStarted && ValidVmConnection() && VmDropInside(pointer) && source != null && target != null;
        var targetSide = target == null ? null : VmNearestSide(target, pointer);
        // 清状态及释放捕获必须在提交之前，PortUp 不再有第二个创建机会。
        CancelVmCanvasGesture();
        if (commit) CreateConnectionBetweenPorts(source!, side!, target!, targetSide!);
    }
}
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Rendering;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private WorkflowTabViewModel? _canvasConnectionTab;
    private string? _canvasConnectionStamp, _canvasConnectionConfigurationStamp;
    private CanvasNodeProjection? _canvasConnectionTarget;
    private bool _canvasConnectionStarted;
    private static readonly string[] CanvasSides = { "LeftPort", "TopPort", "RightPort", "BottomPort" };
    private void BeginCanvasConnection(CanvasNodeProjection node, string side)
    {
        CancelCanvasGesture();
        var tab = _viewModel?.WorkflowTabViewModel.SelectedTab;
        if (!CanEditCanvas || tab?.DocumentCanvas == null || !tab.DocumentCanvas.Nodes.Contains(node) || !CanvasSides.Contains(side)) return;
        ClearCanvasWireSelection(); WorkflowCanvas.Focus();
        _canvasConnectionTab = tab; _canvasConnectionStamp = CanvasGraphStamp(tab); _canvasConnectionConfigurationStamp = CanvasConfigurationStamp(tab); _canvasConnectionTarget = null; _canvasConnectionStarted = false;
        _dragConnectionSourceNode = node; _dragConnectionSourcePort = side; _isDraggingConnection = true;
        _dragConnectionStartPoint = GetPortPositionByName(node, side);
    }
    private bool ValidCanvasConnection() => CanEditCanvas && _canvasConnectionTab != null &&
        ReferenceEquals(_viewModel?.WorkflowTabViewModel.SelectedTab, _canvasConnectionTab) && CanvasGraphStamp(_canvasConnectionTab) == _canvasConnectionStamp;
    private static IEnumerable<ConnectionPort> CanvasPorts(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ConnectionPort port) yield return port;
            foreach (var nested in CanvasPorts(child)) yield return nested;
        }
    }
    private void PreviewCanvasConnection(MouseEventArgs e)
    {
        if (!ValidCanvasConnection() || e.LeftButton != MouseButtonState.Pressed) { CancelCanvasGesture(); return; }
        var pointer = e.GetPosition(WorkflowCanvas);
        if (!_canvasConnectionStarted)
        {
            _canvasConnectionStarted = true;
            WorkflowCanvas.CaptureMouse();
            // 捕获会触发旧持有者的 LostMouseCapture；只继续有效会话。
            if (!_isDraggingConnection) return;
        }
        var source = _dragConnectionSourceNode!;
        _canvasConnectionTarget = _canvasConnectionTab!.DocumentCanvas!.Nodes.OfType<CanvasNodeProjection>().Reverse().FirstOrDefault(node =>
        {
            if (ReferenceEquals(node, source)) return false;
            var bounds = node.NodeRect; bounds.Inflate(10, 10);
            return bounds.Contains(pointer) || CanvasSides.Any(side => CanvasPortHitRect(node, side).Contains(pointer));
        });
        var targetSide = _canvasConnectionTarget == null ? null : CanvasNearestSide(_canvasConnectionTarget, pointer);
        foreach (var port in CanvasPorts(WorkflowCanvas))
        {
            var node = port.DataContext as CanvasNodeProjection;
            port.IsHited = ReferenceEquals(node, _canvasConnectionTarget) && port.Tag as string == targetSide;
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
    private Rect CanvasPortHitRect(CanvasNodeProjection node, string side)
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
    private string CanvasNearestSide(CanvasNodeProjection node, Point pointer) => CanvasSides.OrderBy(side => (GetPortPositionByName(node, side) - pointer).LengthSquared).First();
    private void FinishCanvasConnection(Point pointer)
    {
        var source = _dragConnectionSourceNode; var side = _dragConnectionSourcePort;
        var target = _canvasConnectionTarget;
        var commit = _canvasConnectionStarted && ValidCanvasConnection() && _canvasConnectionConfigurationStamp != null &&
            _canvasConnectionConfigurationStamp == CanvasConfigurationStamp(_canvasConnectionTab!) && CanvasDropInside(pointer) && source != null && target != null;
        var targetSide = target == null ? null : CanvasNearestSide(target, pointer);
        var canvas = _canvasConnectionTab?.DocumentCanvas;
        // 清状态及释放捕获必须在提交之前，PortUp 不再有第二个创建机会。
        CancelCanvasGesture();
        if (commit && canvas != null)
        {
            try { canvas.Connect(new SunEyeVision.Domain.Workflows.ConnectionDefinition(Guid.NewGuid().ToString("N"), source!.Id, target!.Id, side!, targetSide!)); }
            catch (Exception error) { if (_viewModel != null) _viewModel.StatusText = "连线未创建：" + error.Message; }
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Rendering;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private static void ConnectionProbe(string message)
        => Foundation.Logging.VisionLogger.Instance?.Log(
            Foundation.Logging.LogLevel.Info,
            "[ConnectionProbe] " + message,
            "WorkflowConnectionProbe");

    private WorkflowTabViewModel? _canvasConnectionTab;
    private string? _canvasConnectionStamp, _canvasConnectionConfigurationStamp;
    private CanvasNodeProjection? _canvasConnectionTarget;
    private bool _canvasConnectionStarted;
    private static readonly string[] CanvasSides = { "LeftPort", "TopPort", "RightPort", "BottomPort" };
    private void BeginCanvasConnection(CanvasNodeProjection node, string side)
    {
        CancelCanvasGesture();
        var tab = _viewModel?.WorkflowTabViewModel.SelectedTab;
        if (!CanEditCanvas || tab?.DocumentCanvas == null || !tab.DocumentCanvas.Nodes.Contains(node) || !CanvasSides.Contains(side))
        {
            ConnectionProbe($"begin_rejected node={node.Id} side={side} canEdit={CanEditCanvas} hasCanvas={tab?.DocumentCanvas != null} validSide={CanvasSides.Contains(side)}");
            return;
        }
        ClearCanvasWireSelection(); WorkflowCanvas.Focus();
        _canvasConnectionTab = tab; _canvasConnectionStamp = CanvasGraphStamp(tab); _canvasConnectionConfigurationStamp = CanvasConfigurationStamp(tab); _canvasConnectionTarget = null; _canvasConnectionStarted = false;
        _dragConnectionSourceNode = node; _dragConnectionSourcePort = side; _isDraggingConnection = true;
        _dragConnectionStartPoint = GetPortPositionByName(node, side);
        ConnectionProbe($"begin node={node.Id} side={side} start={_dragConnectionStartPoint}");
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
        ConnectionProbe($"move pointer={pointer} target={_canvasConnectionTarget?.Id ?? "<none>"} targetSide={targetSide ?? "<none>"}");
        foreach (var port in CanvasPorts(WorkflowCanvas))
        {
            var node = port.DataContext as CanvasNodeProjection;
            port.IsHited = ReferenceEquals(node, _canvasConnectionTarget) && port.Tag as string == targetSide;
        }
        // 拖线期间鼠标被画布捕获，节点的 IsMouseOver 不再更新；按原件 HitTesting 的做法主动驱动目标节点显示端口。
        SyncConnectionTargetNodeHited(_canvasConnectionTarget);
        if (_tempConnectionLine != null)
        {
            _tempConnectionLine.Visibility = Visibility.Visible;
            _tempConnectionLine.StrokeDashArray = new DoubleCollection { 5, 2.5 };
        }
        // 恢复预览以鼠标为终点；目标仅作高亮，松开时才计算最终锚点。
        UpdateTempConnectionPath(_dragConnectionStartPoint, pointer);
        e.Handled = true;
    }
    /// <summary>拖线期间主动设置目标节点命中态，使其端口显示（对应原件 Connector.HitTesting）。</summary>
    private void SyncConnectionTargetNodeHited(CanvasNodeProjection? target)
    {
        var canvas = _canvasConnectionTab?.DocumentCanvas;
        if (canvas == null) return;
        foreach (var node in canvas.Nodes)
        {
            if (CanvasNodeItems.ItemContainerGenerator.ContainerFromItem(node) is not DependencyObject container) continue;
            var view = FindVisualChild<WorkflowNodeView>(container);
            if (view == null) continue;
            bool hited = ReferenceEquals(node, target);
            if (view.IsHited != hited) view.IsHited = hited;
        }
    }

    private Rect CanvasPortHitRect(CanvasNodeProjection node, string side)
    {
        var a = GetPortPositionByName(node, side);
        return side switch
        {
            // ConnectionPort is 20x30. Its template margins place the left and
            // right controls outside the node and the top control 25px above it.
            "LeftPort" => new Rect(a.X - 20, a.Y - 15, 20, 30),
            "RightPort" => new Rect(a.X, a.Y - 15, 20, 30),
            "TopPort" => new Rect(a.X - 10, a.Y - 25, 20, 30),
            _ => new Rect(a.X - 10, a.Y, 20, 30)
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
        ConnectionProbe($"finish pointer={pointer} started={_canvasConnectionStarted} valid={ValidCanvasConnection()} inside={CanvasDropInside(pointer)} source={source?.Id ?? "<none>"} sourceSide={side ?? "<none>"} target={target?.Id ?? "<none>"} targetSide={targetSide ?? "<none>"}");
        // 清状态及释放捕获必须在提交之前，PortUp 不再有第二个创建机会。
        CancelCanvasGesture();
        if (commit && canvas != null)
        {
            try { canvas.Connect(new SunEyeVision.Domain.Workflows.ConnectionDefinition(Guid.NewGuid().ToString("N"), source!.Id, target!.Id, side!, targetSide!)); }
            catch (Exception error) { ConnectionProbe($"commit_error message={error.Message}"); if (_viewModel != null) _viewModel.StatusText = "连线未创建：" + error.Message; }
        }
    }
}

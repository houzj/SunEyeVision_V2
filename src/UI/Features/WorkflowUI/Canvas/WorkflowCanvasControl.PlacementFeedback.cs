using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Services.Interaction;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    internal DataTemplate GetCanvasNodeTemplate() => CanvasNodeItems.ItemTemplate;
    private readonly List<Line> _canvasAlignmentGuides = new();
    private Rectangle? _canvasNodeHotRegion;
    private HotPlacement? _canvasHotPlacement;
    // 原件 DiagramItem/Connection 把所有拖拽装饰放进 AdornerPanel（DiagramItem.xaml.cs:642-649, 943）。
    // 装饰若放进 WorkflowCanvas.Children，每次增删都会让整个画布重新 Measure/Arrange，快速拖动会卡顿。
    private DragPreviewAdorner? _canvasFeedbackAdorner;

    private Panel FeedbackLayer
    {
        get
        {
            if (_canvasFeedbackAdorner is not null) return _canvasFeedbackAdorner.Layer;
            var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(WorkflowCanvas);
            if (layer is null) return WorkflowCanvas;
            _canvasFeedbackAdorner = new DragPreviewAdorner(WorkflowCanvas);
            layer.Add(_canvasFeedbackAdorner);
            return _canvasFeedbackAdorner.Layer;
        }
    }

    /// <summary>每帧清反馈：对齐线按原件每帧重建；热区只隐藏复用（原件 CurrentHotRegion 建一次后只改 Margin/Visibility）。</summary>
    internal void ClearCanvasPlacementFeedback()
    {
        var layer = _canvasFeedbackAdorner is null ? (Panel)WorkflowCanvas : _canvasFeedbackAdorner.Layer;
        foreach (var line in _canvasAlignmentGuides) layer.Children.Remove(line);
        _canvasAlignmentGuides.Clear();
        if (_canvasNodeHotRegion != null) _canvasNodeHotRegion.Visibility = Visibility.Collapsed;
        _canvasHotPlacement = null;
    }

    /// <summary>拖拽彻底结束时才销毁热区与装饰 Adorner（原件 DoDragDrop 返回后移除 adornerPanel）。</summary>
    internal void DisposeCanvasPlacementFeedback()
    {
        ClearCanvasPlacementFeedback();
        if (_canvasNodeHotRegion != null)
        {
            FeedbackLayer.Children.Remove(_canvasNodeHotRegion);
            _canvasNodeHotRegion = null;
        }
        if (_canvasFeedbackAdorner is not null)
        {
            System.Windows.Documents.AdornerLayer.GetAdornerLayer(WorkflowCanvas)?.Remove(_canvasFeedbackAdorner);
            _canvasFeedbackAdorner = null;
        }
    }

    internal void UpdateCanvasPlacementFeedback(Point center, double height, IEnumerable<string>? excluded = null)
    {
        var tab = CurrentWorkflowTab;
        if (tab?.DocumentCanvas == null) { ClearCanvasPlacementFeedback(); return; }
        var ids = excluded?.ToHashSet() ?? new HashSet<string>();
        var others = tab.DocumentCanvas.Nodes.Where(n => !ids.Contains(n.Id))
            .Select(n => new PlacementNode(n.Id, n.NodeRect)).ToArray();
        var scale = Math.Max(.001, tab.CurrentScale);
        var result = NodePlacementCalculator.Calculate(new Rect(center.X - 55, center.Y - height / 2, 110, height), others, scale, _canvasHotPlacement);
        ClearCanvasPlacementFeedback();
        _canvasHotPlacement = result.Hot;
        var layer = FeedbackLayer;
        foreach (var guide in result.Guides)
        {
            var line = new Line { X1 = guide.Vertical ? guide.Axis : guide.Start, Y1 = guide.Vertical ? guide.Start : guide.Axis,
                X2 = guide.Vertical ? guide.Axis : guide.End, Y2 = guide.Vertical ? guide.End : guide.Axis,
                Stroke = Brushes.LightGreen, StrokeThickness = 1 / scale, StrokeDashArray = new DoubleCollection { 5, 2.5 },
                StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false };
            Panel.SetZIndex(line, 55);
            layer.Children.Add(line);
            _canvasAlignmentGuides.Add(line);
        }
        if (_canvasHotPlacement != null)
        {
            if (_canvasNodeHotRegion is null)
            {
                _canvasNodeHotRegion = new Rectangle { Width = 66, Height = 22, Fill = Brushes.Orange, IsHitTestVisible = false };
                Panel.SetZIndex(_canvasNodeHotRegion, 56);
                FeedbackLayer.Children.Add(_canvasNodeHotRegion);
            }
            System.Windows.Controls.Canvas.SetLeft(_canvasNodeHotRegion, _canvasHotPlacement.TopLeft.X + 55 - 33);
            System.Windows.Controls.Canvas.SetTop(_canvasNodeHotRegion, _canvasHotPlacement.TopLeft.Y + 15 - 11);
            _canvasNodeHotRegion.Visibility = Visibility.Visible;
        }
    }
    internal Point ResolveCanvasPlacement(Point fallback, double height) => _canvasHotPlacement == null ? fallback : new Point(_canvasHotPlacement.TopLeft.X + 55, _canvasHotPlacement.TopLeft.Y + height / 2);
    internal string? CanvasAvailableHotPredecessor()
    {
        var tab = CurrentWorkflowTab; var predecessor = _canvasHotPlacement?.PredecessorId;
        if (tab?.DocumentCanvas == null || predecessor == null || !tab.DocumentCanvas.Nodes.Any(n => n.Id == predecessor)) return null;
        return tab.DocumentCanvas.Connections.Any(c => c.SourceNodeId == predecessor && c.SourcePort == "BottomPort" ||
            c.TargetNodeId == predecessor && c.TargetPort == "BottomPort") ? null : predecessor;
    }
}

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
    internal void ClearCanvasPlacementFeedback()
    {
        foreach (var line in _canvasAlignmentGuides) WorkflowCanvas.Children.Remove(line);
        _canvasAlignmentGuides.Clear();
        if (_canvasNodeHotRegion != null) WorkflowCanvas.Children.Remove(_canvasNodeHotRegion);
        _canvasNodeHotRegion = null; _canvasHotPlacement = null;
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
        ClearCanvasPlacementFeedback(); _canvasHotPlacement = result.Hot;
        foreach (var guide in result.Guides)
        {
            var line = new Line { X1 = guide.Vertical ? guide.Axis : guide.Start, Y1 = guide.Vertical ? guide.Start : guide.Axis,
                X2 = guide.Vertical ? guide.Axis : guide.End, Y2 = guide.Vertical ? guide.End : guide.Axis,
                Stroke = Brushes.LightGreen, StrokeThickness = 1 / scale, StrokeDashArray = new DoubleCollection { 5, 2.5 },
                StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false };
            Panel.SetZIndex(line, 55); WorkflowCanvas.Children.Add(line); _canvasAlignmentGuides.Add(line);
        }
        if (_canvasHotPlacement != null)
        {
            _canvasNodeHotRegion = new Rectangle { Width = 66, Height = 22, Fill = Brushes.Orange, IsHitTestVisible = false };
            System.Windows.Controls.Canvas.SetLeft(_canvasNodeHotRegion, _canvasHotPlacement.TopLeft.X + 55 - 33);
            System.Windows.Controls.Canvas.SetTop(_canvasNodeHotRegion, _canvasHotPlacement.TopLeft.Y + 15 - 11);
            Panel.SetZIndex(_canvasNodeHotRegion, 56); WorkflowCanvas.Children.Add(_canvasNodeHotRegion);
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

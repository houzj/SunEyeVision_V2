using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Services.Interaction;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private readonly List<Line> _vmAlignmentGuides = new();
    private Rectangle? _vmNodeHotRegion;
    private VmHotPlacement? _vmHotPlacement;
    internal void ClearVmPlacementFeedback()
    {
        foreach (var line in _vmAlignmentGuides) WorkflowCanvas.Children.Remove(line);
        _vmAlignmentGuides.Clear();
        if (_vmNodeHotRegion != null) WorkflowCanvas.Children.Remove(_vmNodeHotRegion);
        _vmNodeHotRegion = null; _vmHotPlacement = null;
    }
    internal void UpdateVmPlacementFeedback(Point center, double height, IEnumerable<string>? excluded = null)
    {
        var tab = CurrentWorkflowTab;
        if (tab == null) { ClearVmPlacementFeedback(); return; }
        var ids = excluded?.ToHashSet() ?? new HashSet<string>();
        var others = tab.WorkflowNodes.OfType<SunEyeVision.UI.Shared.WorkflowNode>().Where(n => !ids.Contains(n.Id))
            .Select(n => new VmPlacementNode(n.Id, n.NodeRect)).ToArray();
        var scale = Math.Max(.001, tab.CurrentScale);
        var result = VmPlacementCalculator.Calculate(new Rect(center.X - 55, center.Y - height / 2, 110, height), others, scale, _vmHotPlacement);
        ClearVmPlacementFeedback(); _vmHotPlacement = result.Hot;
        foreach (var guide in result.Guides)
        {
            var line = new Line { X1 = guide.Vertical ? guide.Axis : guide.Start, Y1 = guide.Vertical ? guide.Start : guide.Axis,
                X2 = guide.Vertical ? guide.Axis : guide.End, Y2 = guide.Vertical ? guide.End : guide.Axis,
                Stroke = Brushes.LightGreen, StrokeThickness = 1 / scale, StrokeDashArray = new DoubleCollection { 5, 2.5 },
                StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false };
            Panel.SetZIndex(line, 55); WorkflowCanvas.Children.Add(line); _vmAlignmentGuides.Add(line);
        }
        if (_vmHotPlacement != null)
        {
            _vmNodeHotRegion = new Rectangle { Width = 66, Height = 22, Fill = Brushes.Orange, IsHitTestVisible = false };
            System.Windows.Controls.Canvas.SetLeft(_vmNodeHotRegion, _vmHotPlacement.TopLeft.X + 55 - 33);
            System.Windows.Controls.Canvas.SetTop(_vmNodeHotRegion, _vmHotPlacement.TopLeft.Y + 15 - 11);
            Panel.SetZIndex(_vmNodeHotRegion, 56); WorkflowCanvas.Children.Add(_vmNodeHotRegion);
        }
    }
    internal Point ResolveVmPlacement(Point fallback, double height) => _vmHotPlacement == null ? fallback : new Point(_vmHotPlacement.TopLeft.X + 55, _vmHotPlacement.TopLeft.Y + height / 2);
    internal string? VmAvailableHotPredecessor()
    {
        var tab = CurrentWorkflowTab; var predecessor = _vmHotPlacement?.PredecessorId;
        if (tab == null || predecessor == null || !tab.WorkflowNodes.Any(n => n.Id == predecessor)) return null;
        return tab.WorkflowConnections.Any(c => c.SourceNodeId == predecessor && c.SourcePort == "BottomPort" ||
            c.TargetNodeId == predecessor && c.TargetPort == "BottomPort") ? null : predecessor;
    }
}
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private CanvasConnectionProjection? _canvasInsertionWire;
    private System.Windows.Shapes.Path? _canvasInsertionView;
    private static IEnumerable<System.Windows.Shapes.Path> CanvasVisualPaths(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Shapes.Path path) yield return path;
            foreach (var nested in CanvasVisualPaths(child)) yield return nested;
        }
    }
    internal CanvasConnectionProjection? FindCanvasInsertionWire(Point pointer, IEnumerable<string> excluded)
    {
        var tab = CurrentWorkflowTab; if (tab?.DocumentCanvas == null) return null;
        var ids = excluded.ToHashSet();
        if (tab.DocumentCanvas.Nodes.Any(n => !ids.Contains(n.Id) && n.NodeRect.Contains(pointer))) return null;
        foreach (var edge in tab.DocumentCanvas.Connections.Reverse())
            if (!ids.Contains(edge.SourceNodeId) && !ids.Contains(edge.TargetNodeId) &&
                _connectionPathCache?.GetPath(edge).StrokeContains(new Pen(Brushes.Black, 10), pointer) == true) return edge;
        return null;
    }
    internal void UpdateCanvasInsertionWire(Point pointer, IEnumerable<string> excluded)
    {
        var candidate = FindCanvasInsertionWire(pointer, excluded);
        ClearCanvasInsertionWire(); _canvasInsertionWire = candidate;
        if (candidate == null) return;
        _canvasInsertionView = CanvasVisualPaths(WorkflowCanvas).FirstOrDefault(p => ReferenceEquals(p.DataContext, candidate) && !p.IsHitTestVisible && p.Stroke != null);
        if (_canvasInsertionView == null) return;
        _canvasInsertionView.SetCurrentValue(Shape.StrokeThicknessProperty, 10d);
        _canvasInsertionView.SetCurrentValue(Shape.StrokeProperty, new SolidColorBrush(Color.FromRgb(255, 167, 15)));
    }
    internal void ClearCanvasInsertionWire()
    {
        if (_canvasInsertionView != null)
        { _canvasInsertionView.ClearValue(Shape.StrokeThicknessProperty); _canvasInsertionView.ClearValue(Shape.StrokeProperty); }
        _canvasInsertionView = null; _canvasInsertionWire = null;
    }
}

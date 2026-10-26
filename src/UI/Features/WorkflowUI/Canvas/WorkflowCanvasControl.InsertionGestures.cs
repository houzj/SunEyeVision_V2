using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private WorkflowConnection? _vmInsertionWire;
    private System.Windows.Shapes.Path? _vmInsertionView;
    private static IEnumerable<System.Windows.Shapes.Path> VmVisualPaths(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Shapes.Path path) yield return path;
            foreach (var nested in VmVisualPaths(child)) yield return nested;
        }
    }
    internal WorkflowConnection? FindVmInsertionWire(Point pointer, IEnumerable<string> excluded)
    {
        var tab = CurrentWorkflowTab; if (tab == null) return null;
        var ids = excluded.ToHashSet();
        if (tab.WorkflowNodes.OfType<WorkflowNode>().Any(n => !ids.Contains(n.Id) && n.NodeRect.Contains(pointer))) return null;
        foreach (var edge in tab.WorkflowConnections.Reverse())
            if (!ids.Contains(edge.SourceNodeId) && !ids.Contains(edge.TargetNodeId) &&
                _connectionPathCache?.GetPath(edge).StrokeContains(new Pen(Brushes.Black, 10), pointer) == true) return edge;
        return null;
    }
    internal void UpdateVmInsertionWire(Point pointer, IEnumerable<string> excluded)
    {
        var candidate = FindVmInsertionWire(pointer, excluded);
        ClearVmInsertionWire(); _vmInsertionWire = candidate;
        if (candidate == null) return;
        _vmInsertionView = VmVisualPaths(WorkflowCanvas).FirstOrDefault(p => ReferenceEquals(p.DataContext, candidate) && !p.IsHitTestVisible && p.Stroke != null);
        if (_vmInsertionView == null) return;
        _vmInsertionView.SetCurrentValue(Shape.StrokeThicknessProperty, 10d);
        _vmInsertionView.SetCurrentValue(Shape.StrokeProperty, new SolidColorBrush(Color.FromRgb(255, 167, 15)));
    }
    internal void ClearVmInsertionWire()
    {
        if (_vmInsertionView != null)
        { _vmInsertionView.ClearValue(Shape.StrokeThicknessProperty); _vmInsertionView.ClearValue(Shape.StrokeProperty); }
        _vmInsertionView = null; _vmInsertionWire = null;
    }
}
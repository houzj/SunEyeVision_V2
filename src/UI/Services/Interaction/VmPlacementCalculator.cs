using System.Windows;
namespace SunEyeVision.UI.Services.Interaction;
public sealed record VmPlacementNode(string Id, Rect Bounds);
public sealed record VmAlignmentGuide(bool Vertical, double Axis, double Start, double End);
public sealed record VmHotPlacement(Point TopLeft, string PredecessorId);
public sealed record VmPlacementResult(IReadOnlyList<VmAlignmentGuide> Guides, VmHotPlacement? Hot);
/// <summary>WorkflowGestures.DrawAlignmentGuides/UpdateNodeHotRegion的宿主坐标适配。</summary>
public static class VmPlacementCalculator
{
    public static VmPlacementResult Calculate(Rect moving, IReadOnlyList<VmPlacementNode> others, double scale, VmHotPlacement? previous)
    {
        var guides = new List<VmAlignmentGuide>(); scale = Math.Max(.001, scale);
        foreach (var vertical in new[] { true, false })
        foreach (var fraction in new[] { 0d, 1d, .5 })
        {
            var axis = (vertical ? moving.X : moving.Y) + (vertical ? moving.Width : moving.Height) * fraction;
            var neighbor = others.Where(n => (vertical ? n.Bounds.X + n.Bounds.Width * fraction : n.Bounds.Y + n.Bounds.Height * fraction) == axis)
                .OrderBy(n => Math.Abs(vertical ? n.Bounds.Y - moving.Y : n.Bounds.X - moving.X)).FirstOrDefault();
            if (neighbor == null) continue;
            var start = Math.Min(vertical ? moving.Y : moving.X, vertical ? neighbor.Bounds.Y : neighbor.Bounds.X) - 5 / scale;
            var end = Math.Max(vertical ? moving.Bottom : moving.Right, vertical ? neighbor.Bounds.Bottom : neighbor.Bounds.Right) + 5 / scale;
            guides.Add(new(vertical, axis, start, end));
        }
        VmHotPlacement? hot = null;
        var nearest = others.Where(n => n.Bounds.X == moving.X && n.Bounds.Y < moving.Y).OrderBy(n => moving.Y - n.Bounds.Y).FirstOrDefault();
        if (nearest != null && nearest.Bounds.Y + 70 - nearest.Bounds.Height < moving.Y && moving.Y < nearest.Bounds.Y + 70 + nearest.Bounds.Height)
            hot = new(new Point(nearest.Bounds.X, nearest.Bounds.Y + 70 - moving.Height / 2 + nearest.Bounds.Height / 2), nearest.Id);
        else if (previous != null && previous.TopLeft.X - 20 < moving.X && moving.X < previous.TopLeft.X + 20 &&
            previous.TopLeft.Y - 30 < moving.Y && moving.Y < previous.TopLeft.Y + 30) hot = previous;
        return new(guides, hot);
    }
}
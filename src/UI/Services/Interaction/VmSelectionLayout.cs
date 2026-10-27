using System.Windows;
namespace SunEyeVision.UI.Services.Interaction;
public enum VmNodeAlignment { Left, CenterX, Right, Top, CenterY, Bottom }
/// <summary>RectDragPanal.menuAlgClick/menuLayoutClick的实际尺寸公式；输入输出均为左上角。</summary>
public static class VmSelectionLayout
{
    public static Rect Bounds(IReadOnlyList<Rect> nodes)
    {
        if (nodes.Count == 0) return Rect.Empty;
        var area = nodes[0]; foreach (var node in nodes.Skip(1)) area.Union(node);
        area.Inflate(10, 10); return area;
    }
    public static Point[] Align(IReadOnlyList<Rect> nodes, VmNodeAlignment alignment)
    {
        var area = Bounds(nodes);
        return nodes.Select(n => alignment switch
        {
            VmNodeAlignment.Left => new Point(area.Left + 10, n.Y),
            VmNodeAlignment.Right => new Point(area.Right - n.Width - 10, n.Y),
            VmNodeAlignment.CenterX => new Point(area.Left + Snap((area.Width - n.Width) / 2), n.Y),
            VmNodeAlignment.Top => new Point(n.X, area.Top + 10),
            VmNodeAlignment.Bottom => new Point(n.X, area.Bottom - n.Height - 10),
            _ => new Point(n.X, area.Top + Snap((area.Height - n.Height) / 2))
        }).ToArray();
    }
    private static double Snap(double value) => Math.Round(value / 5) * 5;
    public static Point[] Distribute(IReadOnlyList<Rect> nodes, bool horizontal)
    {
        if (nodes.Count <= 1) return nodes.Select(n => n.TopLeft).ToArray();
        var area = Bounds(nodes); var required = nodes.Sum(n => (horizontal ? n.Width : n.Height) + 5) + 15;
        if (horizontal && area.Width < required) area.Width = required;
        if (!horizontal && area.Height < required) area.Height = required;
        var gap = Math.Max(0, ((horizontal ? area.Width : area.Height) - required) / (nodes.Count - 1));
        var cursor = Math.Max(Snap((horizontal ? area.Left : area.Top) + 10), 10);
        var result = new Point[nodes.Count];
        foreach (var i in Enumerable.Range(0, nodes.Count).OrderBy(i => horizontal ? nodes[i].X : nodes[i].Y))
        {
            var node = nodes[i];
            result[i] = horizontal
                ? new Point(cursor, Math.Max(Snap(area.Top + area.Height / 2 - node.Height / 2), 5))
                : new Point(Math.Max(Snap(area.Left + area.Width / 2 - node.Width / 2), 5), cursor);
            cursor += Snap((horizontal ? node.Width : node.Height) + 5 + gap);
        }
        return result;
    }
}
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.Shape;

namespace SunEyeVision.Plugin.Wpf.Adapters.Display;

internal sealed record PreparedNativeShapes(object[] Items, int Count, int Vertices, object[]? PresentationItems = null);

/// <summary>Nonvisual projection to original interfaces. Never scales coordinates or owns a view.</summary>
internal static partial class NativeShapeProjection
{
    // Keep semantic rows separate from the original result-list packaging. Other
    // independently owned selection/ROI/cross layers retain their direct items.
    internal static object[] Presentation(object[] items,
        SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.ImageViewBox view)
    {
        var result = new List<object>();
        if(items.Any(item=>item is IOutlinePoint))result.Add(new OutlineBatch());
        for (var index = 0; index < items.Length;)
        {
            var first = items[index];
            if (first is IMultiText) { result.Add(first); index++; continue; }
            var type = first.GetType(); var run = new List<object>();
            do { run.Add(items[index++]); }
            while (index < items.Length && items[index].GetType() == type);
            DrawShapeHelper.DrawShapes(run, result, view, isLowQuality: false);
        }
        return result.ToArray();
    }
    internal static PreparedNativeShapes Texts(IReadOnlyList<NativeTextOverlay> texts, int maximum)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count > maximum) throw new ArgumentException("shape-budget: 完整文字超过绘制预算。");
        var projected = new List<TextProjection>();
        foreach (var text in texts.ToArray())
        {
            if (text is null || text.Content is null || !double.IsFinite(text.Position.X) || !double.IsFinite(text.Position.Y)
                || text.FontSize is < 1 or > 1000 || !double.IsFinite(text.Opacity) || text.Opacity is < 0 or > 1)
                throw new ArgumentException("文字绘制需要有效文本、像素位置、字号和透明度。");
            projected.Add(new(text.Content, text.Position, text.FontSize, text.Color.ToString(), text.Opacity));
        }
        // The original batch has one opacity. Preserve separate opacity groups.
        return new(projected.GroupBy(text => text.Opacity)
            .Select(group => DrawShapeHelper.PretreatTexts(group.Cast<object>().ToList())).ToArray(), projected.Count, 0);
    }

    internal static PreparedNativeShapes Geometry(NativeResultFrameReceipt receipt,
        IReadOnlyList<NativeGeometryOverlay> overlays, int maximum, int maximumVertices, Func<bool> current)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        if (receipt.Coordinates.Domain != CoordinateDomain.Image || receipt.Coordinates.Unit != CoordinateUnit.Pixel)
            throw new ArgumentException("没有已证明的像素坐标映射，不能绘制几何。");
        if (overlays.Count > maximum) throw new ArgumentException("shape-budget: 完整几何超过绘制预算。");
        var shapes = new List<object>(); var vertices = 0;
        foreach (var overlay in overlays.ToArray())
        {
            NativeResultRaster.CheckCurrent(current);
            if (overlay is null || overlay.Geometry is null || overlay.SourceFrame != receipt.Frame
                || overlay.Geometry.Frame != receipt.Coordinates)
                throw new ArgumentException("几何来源与当前物理帧或坐标系不一致。");
            if (!double.IsFinite(overlay.Thickness) || overlay.Thickness <= 0
                || !double.IsFinite(overlay.Opacity) || overlay.Opacity is < 0 or > 1)
                throw new ArgumentException("几何线宽或透明度无效。");
            var style = new Style(overlay.Stroke.ToString(), overlay.Thickness, overlay.Opacity,(overlay.Fill??Colors.Transparent).ToString());
            object shape = overlay.Geometry switch
            {
                PointGeometry point => new PointProjection(style, Point(point.Point)),
                SunEyeVision.Plugin.Contracts.LineGeometry line => new LineProjection(style, Point(line.Start), Point(line.End)),
                CircleGeometry circle => new CircleProjection(style, Point(circle.Center), circle.Radius),
                OrientedRectangleGeometry rectangle => new RectangleProjection(style, Point(rectangle.Center), rectangle.Width,
                    rectangle.Height, Degrees(rectangle.AngleRadians)),
                SunEyeVision.Plugin.Contracts.EllipseGeometry ellipse => new EllipseProjection(style, Point(ellipse.Center), ellipse.RadiusX,
                    ellipse.RadiusY, Degrees(ellipse.AngleRadians)),
                PolygonGeometry polygon => Polygon(style, polygon),
                ArcGeometry arc when Math.Abs(arc.SweepRadians) == 2 * Math.PI
                    => new CircleProjection(style, Point(arc.Center), arc.Radius),
                ArcGeometry => throw new NotSupportedException("原只读绘制链尚无经核证的非整圆Arc入口，不能用折线或圆环近似。"),
                _ => throw new NotSupportedException("未支持的规范几何类型。")
            };
            shapes.Add(shape);
        }
        NativeResultRaster.CheckCurrent(current);
        return new(shapes.ToArray(), shapes.Count, vertices);
        object Polygon(Style style, PolygonGeometry polygon)
        {
            vertices = checked(vertices + polygon.Vertices.Count);
            if (vertices > maximumVertices) throw new ArgumentException("shape-budget: 完整多边形顶点超过准入预算。");
            return new PolygonProjection(style, polygon.Vertices.Select(Point).ToArray());
        }
    }
    private static Point Point(Point2 value) => new(value.X, value.Y);
    private static double Degrees(double value)
    {
        var degrees = value * (180 / Math.PI);
        return double.IsFinite(degrees) ? degrees : throw new ArgumentException("几何角度转换超出原绘制范围。");
    }
    private sealed record TextProjection(string Content, Point Position, int FontSize, string Stroke, double Opacity) : IText;
    private sealed record Style(string Stroke, double Thickness, double Opacity,string Fill);
    private abstract record ShapeProjection(Style Style) : IShape
    {
        public string Stroke => Style.Stroke;
        public double Opacity => Style.Opacity;
        public string Fill => Style.Fill;
        public double StrokeThickness => Style.Thickness;
        public string ToolTip => "";
    }
    private sealed record PointProjection(Style Style, Point Point) : ShapeProjection(Style), IPoint;
    private sealed record LineProjection(Style Style, Point StartPoint, Point EndPoint) : ShapeProjection(Style), ILine
    { public bool IsThroughLine => false; }
    private sealed record CircleProjection(Style Style, Point CenterPoint, double Radius) : ShapeProjection(Style), ICircle;
    private sealed record RectangleProjection(Style Style, Point CenterPoint, double Width, double Height, double Angle)
        : ShapeProjection(Style), IRectangle
    { public double SkewAngleX => 0; public double SkewAngleY => 0; }
    private sealed record EllipseProjection(Style Style, Point CenterPoint, double MajorRadius, double MinorRadius, double Angle)
        : ShapeProjection(Style), IEllipse;
    private sealed record PolygonProjection(Style Style, Point[] Vertices) : ShapeProjection(Style), IPolygon
    { public List<Point> Points => Vertices.ToList(); }
}

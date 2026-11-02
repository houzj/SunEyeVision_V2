using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

namespace SunEyeVision.Plugin.Wpf.Adapters.Display;

internal sealed record PreparedNativeShapes(object[] Items, int Count, int Vertices);

/// <summary>Nonvisual projection to original interfaces. Never scales coordinates or owns a view.</summary>
internal static class NativeShapeProjection
{
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
            if(overlay.RectangleCorners && overlay.Geometry is not PolygonGeometry)throw new ArgumentException("矩形角点声明必须提供有序四点。");
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
                PolygonGeometry polygon when overlay.RectangleCorners => RectangleFromCorners(style,polygon),
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
    // This is a display adaptation of the producer's ordered rectangle corners.
    // Keep zero extents accepted by the original IRectangle/RectShape contract.
    private static object RectangleFromCorners(Style style,PolygonGeometry corners)
    {
        if(corners.Vertices.Count!=4)throw new ArgumentException("矩形需要四个有序角点。");
        var a=corners.Vertices[0];var b=corners.Vertices[1];var c=corners.Vertices[2];var d=corners.Vertices[3];
        var ux=b.X-a.X;var uy=b.Y-a.Y;var vx=c.X-b.X;var vy=c.Y-b.Y;
        var width=Length(ux,uy);var height=Length(vx,vy);
        if(!double.IsFinite(width)||!double.IsFinite(height))throw new ArgumentException("矩形边长超出原绘制范围。");
        if(width==0&&height==0)
        {
            if(d.X!=a.X||d.Y!=a.Y)throw new ArgumentException("退化矩形四角不一致。");
            return new RectangleProjection(style,Point(a),0,0,0);
        }
        var scale=Math.Max(1,Math.Max(width,height));
        var magnitude=corners.Vertices.Max(point=>Math.Max(Math.Abs(point.X),Math.Abs(point.Y)));
        var coordinateError=16*(Math.BitIncrement(magnitude)-magnitude);
        if(!double.IsFinite(coordinateError))throw new ArgumentException("矩形坐标精度超出原绘制范围。");
        var shortest=width==0?height:height==0?width:Math.Min(width,height);
        if(coordinateError>shortest*1e-3)throw new ArgumentException("当前坐标精度不足以确认矩形直角。");
        var tolerance=1e-9*scale+coordinateError;
        var rounding=coordinateError/scale;
        var perpendicularTolerance=1e-9*(width/scale)*(height/scale)
            +2*rounding*((width/scale)+(height/scale))+4*rounding*rounding;
        if(Math.Abs((d.X-a.X)-vx)>tolerance||Math.Abs((d.Y-a.Y)-vy)>tolerance
            ||Math.Abs((c.X-d.X)-ux)>tolerance||Math.Abs((c.Y-d.Y)-uy)>tolerance
            ||Math.Abs((ux/scale)*(vx/scale)+(uy/scale)*(vy/scale))>perpendicularTolerance)
            throw new ArgumentException("声明的四点不构成有序直角矩形。");
        var center=new Point2(a.X*.5+c.X*.5,a.Y*.5+c.Y*.5);
        var angle=width>0?Math.Atan2(uy,ux):height>0?Math.Atan2(-vx,vy):0;
        return new RectangleProjection(style,Point(center),width,height,Degrees(angle));
    }
    private static double Length(double x,double y)
    {
        var scale=Math.Max(Math.Abs(x),Math.Abs(y));
        if(scale==0)return 0;
        return scale*Math.Sqrt((x/scale)*(x/scale)+(y/scale)*(y/scale));
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

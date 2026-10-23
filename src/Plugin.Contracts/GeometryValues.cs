namespace SunEyeVision.Plugin.Contracts;

public enum CoordinateDomain { Image, Model }
public enum CoordinateUnit { Pixel, Millimeter }

/// <summary>
/// Stable logical coordinate space (for example the associated input field), never a run or image-buffer ID.
/// X points right, Y points down. Angles are radians, positive toward positive Y.
/// Runtime image provenance belongs to invocation/resource identity and is checked separately by its host.
/// </summary>
public sealed record CoordinateFrame
{
    public CoordinateFrame(string id, CoordinateDomain domain, CoordinateUnit unit)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Coordinate frame identity is required.", nameof(id));
        if (!Enum.IsDefined(domain) || !Enum.IsDefined(unit)) throw new ArgumentOutOfRangeException(nameof(domain));
        Id = id; Domain = domain; Unit = unit;
    }
    public string Id { get; }
    public CoordinateDomain Domain { get; }
    public CoordinateUnit Unit { get; }
}

public readonly record struct Point2
{
    public Point2(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentException("Coordinates must be finite.");
        X=x; Y=y;
    }
    public double X { get; }
    public double Y { get; }
}

/// <summary>Closed, immutable geometric data; no WPF, editor state or backend handle.</summary>
public abstract class GeometryValue
{
    private protected GeometryValue(CoordinateFrame frame, GeometryKind kind)
    { Frame=frame ?? throw new ArgumentNullException(nameof(frame)); Kind=kind; }
    public CoordinateFrame Frame { get; }
    public GeometryKind Kind { get; }
    private protected static double Positive(double value)
        => double.IsFinite(value) && value>0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private protected static double Angle(double value)
        => double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}
public sealed class PointGeometry(CoordinateFrame frame, Point2 point) : GeometryValue(frame,GeometryKind.Point)
{ public Point2 Point { get; }=point; }
public sealed class LineGeometry : GeometryValue
{
    public LineGeometry(CoordinateFrame frame,Point2 start,Point2 end):base(frame,GeometryKind.Line)
    { if(start==end)throw new ArgumentException("Line endpoints must differ."); Start=start; End=end; }
    public Point2 Start { get; } public Point2 End { get; }
}
public sealed class CircleGeometry(CoordinateFrame frame,Point2 center,double radius):GeometryValue(frame,GeometryKind.Circle)
{ public Point2 Center { get; }=center; public double Radius { get; }=Positive(radius); }
public sealed class ArcGeometry : GeometryValue
{
    public ArcGeometry(CoordinateFrame frame,Point2 center,double radius,double startRadians,double sweepRadians):base(frame,GeometryKind.Arc)
    {
        Center=center; Radius=Positive(radius); StartRadians=Angle(startRadians); SweepRadians=Angle(sweepRadians);
        if(sweepRadians==0 || Math.Abs(sweepRadians)>2*Math.PI)throw new ArgumentOutOfRangeException(nameof(sweepRadians));
    }
    public Point2 Center { get; } public double Radius { get; } public double StartRadians { get; } public double SweepRadians { get; }
}
public sealed class OrientedRectangleGeometry(CoordinateFrame frame,Point2 center,double width,double height,double angleRadians):GeometryValue(frame,GeometryKind.OrientedRectangle)
{
    public Point2 Center { get; }=center; public double Width { get; }=Positive(width); public double Height { get; }=Positive(height);
    public double AngleRadians { get; }=Angle(angleRadians);
}
public sealed class EllipseGeometry(CoordinateFrame frame,Point2 center,double radiusX,double radiusY,double angleRadians):GeometryValue(frame,GeometryKind.Ellipse)
{
    public Point2 Center { get; }=center; public double RadiusX { get; }=Positive(radiusX); public double RadiusY { get; }=Positive(radiusY);
    public double AngleRadians { get; }=Angle(angleRadians);
}
public sealed class PolygonGeometry : GeometryValue
{
    public PolygonGeometry(CoordinateFrame frame,IEnumerable<Point2> vertices):base(frame,GeometryKind.Polygon)
    {
        var copy=(vertices ?? throw new ArgumentNullException(nameof(vertices))).ToArray();
        if(copy.Length<3)throw new ArgumentException("A polygon requires at least three vertices.",nameof(vertices));
        Vertices=Array.AsReadOnly(copy);
    }
    public IReadOnlyList<Point2> Vertices { get; }
}

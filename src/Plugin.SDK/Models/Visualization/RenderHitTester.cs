using System;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Models.Visualization;

/// <summary>
/// 对 Tile 空间索引返回的候选执行几何级精确命中测试。
/// </summary>
public static class RenderHitTester
{
    public static bool IsHit(GraphicsDisplayItem item, Point point, double tolerance = 4.0)
    {
        if (tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        foreach (var geometry in item.Geometries)
            if (IsGeometryHit(geometry, point, tolerance))
                return true;
        return false;
    }

    private static bool IsGeometryHit(object geometry, Point point, double tolerance)
    {
        switch (geometry)
        {
            case Point p:
                return (p - point).Length <= tolerance;
            case LineSegment line:
                return DistanceToSegment(point, line.Start, line.End) <= tolerance;
            case ContourGeometry contour:
                return IsPolylineHit(Transform(contour.Points, contour.Transform), point, tolerance, contour.IsClosed);
            case BatchedContourGeometry batch:
                foreach (var contourPoints in batch.Contours)
                    if (IsPolylineHit(contourPoints, point, tolerance, batch.IsClosed)) return true;
                return false;
            case Circle2d circle:
                return Math.Abs((circle.Center - point).Length - circle.Radius) <= tolerance;
            case RotatedRect2d rectangle:
                return IsPolygonHit(rectangle.GetCorners(), point, tolerance);
            case Polygon2d polygon:
                return IsPolygonHit(polygon.Vertices, point, tolerance, polygon.IsClosed);
            case Arc2d arc:
                return IsPolylineHit(arc.GetSamplePoints(64), point, tolerance);
            case ArcGeometry arc:
                return IsPolylineHit(SampleArc(arc), point, tolerance);
            default:
                return false;
        }
    }

    private static bool IsPolygonHit(Point[] points, Point point, double tolerance, bool closed = true)
    {
        if (points.Length < 2) return false;
        if (closed && points.Length >= 3 && IsInsidePolygon(points, point)) return true;
        return IsPolylineHit(points, point, tolerance, closed);
    }

    private static bool IsInsidePolygon(Point[] points, Point point)
    {
        var inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            if ((points[i].Y > point.Y) != (points[j].Y > point.Y) &&
                point.X < (points[j].X - points[i].X) * (point.Y - points[i].Y) /
                (points[j].Y - points[i].Y) + points[i].X)
                inside = !inside;
        }
        return inside;
    }

    private static bool IsPolylineHit(Point[] points, Point point, double tolerance, bool closed = false)
    {
        if (points.Length < 2) return points.Length == 1 && (points[0] - point).Length <= tolerance;
        for (var i = 1; i < points.Length; i++)
            if (DistanceToSegment(point, points[i - 1], points[i]) <= tolerance) return true;
        return closed && DistanceToSegment(point, points[^1], points[0]) <= tolerance;
    }

    private static double DistanceToSegment(Point point, Point start, Point end)
    {
        var delta = end - start;
        var lengthSquared = delta.X * delta.X + delta.Y * delta.Y;
        if (lengthSquared <= double.Epsilon) return (point - start).Length;
        var t = Math.Clamp(((point.X - start.X) * delta.X + (point.Y - start.Y) * delta.Y) / lengthSquared, 0, 1);
        var projection = new Point(start.X + t * delta.X, start.Y + t * delta.Y);
        return (point - projection).Length;
    }

    private static Point[] Transform(Point[] points, Matrix? transform)
    {
        if (!transform.HasValue) return points;
        var result = new Point[points.Length];
        for (var i = 0; i < points.Length; i++) result[i] = transform.Value.Transform(points[i]);
        return result;
    }

    private static Point[] SampleArc(ArcGeometry arc)
    {
        var start = arc.StartAngle * Math.PI / 180;
        var end = arc.EndAngle * Math.PI / 180;
        var count = Math.Max(16, (int)(Math.Abs(end - start) * arc.Radius / 8));
        var points = new Point[count + 1];
        for (var i = 0; i <= count; i++)
        {
            var angle = start + (end - start) * i / count;
            points[i] = new Point(arc.Center.X + arc.Radius * Math.Cos(angle), arc.Center.Y + arc.Radius * Math.Sin(angle));
        }
        return points;
    }

}

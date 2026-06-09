using System;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.Models.Geometry;

/// <summary>
/// 通用形状结果 — 引擎无关，用于节点间传递图形结果
/// 对齐区域编辑器的 ShapeType + ShapeParameters，不含 UI 属性
/// </summary>
public readonly struct ShapeResult : IEquatable<ShapeResult>
{
    /// <summary>形状类型</summary>
    public ShapeType Type { get; }

    /// <summary>中心/位置 X（矩形/旋转矩形/圆形/圆环/弧形）</summary>
    public double CenterX { get; }

    /// <summary>中心/位置 Y</summary>
    public double CenterY { get; }

    /// <summary>宽度（矩形/旋转矩形）</summary>
    public double Width { get; }

    /// <summary>高度（矩形/旋转矩形）</summary>
    public double Height { get; }

    /// <summary>角度（旋转矩形，度数，顺时针+，0°向右）</summary>
    public double Angle { get; }

    /// <summary>半径（圆形/弧形）</summary>
    public double Radius { get; }

    /// <summary>外径（圆环）</summary>
    public double OuterRadius { get; }

    /// <summary>起点 X（直线）</summary>
    public double StartX { get; }

    /// <summary>起点 Y（直线）</summary>
    public double StartY { get; }

    /// <summary>终点 X（直线）</summary>
    public double EndX { get; }

    /// <summary>终点 Y（直线）</summary>
    public double EndY { get; }

    /// <summary>起始角度（弧形/圆环，度数）</summary>
    public double StartAngle { get; }

    /// <summary>结束角度（弧形/圆环，度数）</summary>
    public double EndAngle { get; }

    /// <summary>多边形顶点（Polygon 类型）</summary>
    public System.Windows.Point[] PolygonPoints { get; }

    private ShapeResult(ShapeType type,
        double cx = 0, double cy = 0,
        double w = 0, double h = 0, double angle = 0,
        double r = 0, double outerR = 0,
        double sx = 0, double sy = 0, double ex = 0, double ey = 0,
        double startAng = 0, double endAng = 0,
        System.Windows.Point[]? polygonPoints = null)
    {
        Type = type;
        CenterX = cx; CenterY = cy;
        Width = w; Height = h; Angle = angle;
        Radius = r; OuterRadius = outerR;
        StartX = sx; StartY = sy; EndX = ex; EndY = ey;
        StartAngle = startAng; EndAngle = endAng;
        PolygonPoints = polygonPoints ?? Array.Empty<System.Windows.Point>();
    }

    // ===== 工厂方法 =====

    /// <summary>轴对齐矩形</summary>
    public static ShapeResult Rectangle(double x, double y, double w, double h) =>
        new(ShapeType.Rectangle, cx: x + w / 2, cy: y + h / 2, w: w, h: h);

    /// <summary>旋转矩形（中心+宽高+角度）</summary>
    public static ShapeResult RotatedRect(double cx, double cy, double w, double h, double angle) =>
        new(ShapeType.RotatedRectangle, cx: cx, cy: cy, w: w, h: h, angle: angle);

    /// <summary>圆形</summary>
    public static ShapeResult Circle(double cx, double cy, double r) =>
        new(ShapeType.Circle, cx: cx, cy: cy, r: r);

    /// <summary>直线</summary>
    public static ShapeResult Line(double x1, double y1, double x2, double y2) =>
        new(ShapeType.Line, sx: x1, sy: y1, ex: x2, ey: y2);

    /// <summary>点</summary>
    public static ShapeResult Point(double x, double y) =>
        new(ShapeType.Point, cx: x, cy: y);

    /// <summary>多边形</summary>
    public static ShapeResult Polygon(System.Windows.Point[] points) =>
        new(ShapeType.Polygon, polygonPoints: points);

    /// <summary>圆环</summary>
    public static ShapeResult Annulus(double cx, double cy, double innerR, double outerR) =>
        new(ShapeType.Annulus, cx: cx, cy: cy, r: innerR, outerR: outerR);

    /// <summary>弧形</summary>
    public static ShapeResult Arc(double cx, double cy, double r, double startAng, double endAng) =>
        new(ShapeType.Arc, cx: cx, cy: cy, r: r, startAng: startAng, endAng: endAng);

    // ===== IEquatable =====

    public bool Equals(ShapeResult other) =>
        Type == other.Type &&
        CenterX.Equals(other.CenterX) && CenterY.Equals(other.CenterY) &&
        Width.Equals(other.Width) && Height.Equals(other.Height) && Angle.Equals(other.Angle) &&
        Radius.Equals(other.Radius) && OuterRadius.Equals(other.OuterRadius) &&
        StartX.Equals(other.StartX) && StartY.Equals(other.StartY) &&
        EndX.Equals(other.EndX) && EndY.Equals(other.EndY) &&
        StartAngle.Equals(other.StartAngle) && EndAngle.Equals(other.EndAngle);

    public override bool Equals(object? obj) => obj is ShapeResult other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        Type, CenterX, CenterY, Width, Height, Angle, Radius);

    public override string ToString() => Type switch
    {
        ShapeType.Rectangle => $"Rect(C={CenterX:F1},{CenterY:F1} S={Width:F1}x{Height:F1})",
        ShapeType.RotatedRectangle => $"RotRect(C={CenterX:F1},{CenterY:F1} S={Width:F1}x{Height:F1} A={Angle:F1})",
        ShapeType.Circle => $"Circle(C={CenterX:F1},{CenterY:F1} R={Radius:F1})",
        ShapeType.Line => $"Line({StartX:F1},{StartY:F1}→{EndX:F1},{EndY:F1})",
        ShapeType.Point => $"Point({CenterX:F1},{CenterY:F1})",
        ShapeType.Polygon => $"Polygon({PolygonPoints.Length}pts)",
        ShapeType.Annulus => $"Annulus(C={CenterX:F1},{CenterY:F1} R={Radius:F1}~{OuterRadius:F1})",
        ShapeType.Arc => $"Arc(C={CenterX:F1},{CenterY:F1} R={Radius:F1} A={StartAngle:F1}~{EndAngle:F1})",
        _ => $"ShapeResult({Type})"
    };
}

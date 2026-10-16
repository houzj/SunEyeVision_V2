using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Helpers;

/// <summary>
/// Validation and derived geometry for point measurements only. No image processing,
/// fitting, binding resolution or position correction is performed here.
/// </summary>
public static class PointMeasurementGeometry
{
    public static double RequireFinite(double value, string label)
    {
        if (!double.IsFinite(value))
            throw new InvalidOperationException($"{label} 不是有限数值（NaN／Infinity或计算溢出），本次测量失败。");
        return value;
    }

    public static Point RequireFinite(Point point, string label)
    {
        RequireFinite(point.X, $"{label}.X");
        RequireFinite(point.Y, $"{label}.Y");
        return point;
    }

    public static Point ResolveSinglePoint(Point2d[]? points, string label)
    {
        if (points == null || points.Length == 0)
            throw new InvalidOperationException($"{label} 按图形输入未绑定或为空，请选择上游点，或切换按参数输入X/Y。");
        if (points.Length != 1)
            throw new InvalidOperationException($"{label} 包含 {points.Length} 个点，本工具当前只测量单点；请显式选择索引或切换按参数输入，不自动取首项或截断数组。");
        return RequireFinite(new Point(points[0].X, points[0].Y), label);
    }

    public static (double Distance, double DeltaX, double DeltaY, double AngleDeg) MeasurePair(Point first, Point second)
    {
        RequireFinite(first, "点1");
        RequireFinite(second, "点2");
        var dx = RequireFinite(second.X - first.X, "ΔX");
        var dy = RequireFinite(second.Y - first.Y, "ΔY");
        // Hypot avoids overflowing the intermediate squares for representable distances.
        var distance = RequireFinite(double.Hypot(dx, dy), "点距");
        var angle = RequireFinite(Math.Atan2(dy, dx) * 180.0 / Math.PI, "连线角度");
        return (distance, dx, dy, angle);
    }

    public static (Point Foot, double Parameter) ProjectOnLine(Point point, Point start, Point end)
    {
        RequireFinite(point, "点1");
        RequireFinite(start, "拟合直线起点");
        RequireFinite(end, "拟合直线终点");
        var dx = RequireFinite(end.X - start.X, "拟合直线ΔX");
        var dy = RequireFinite(end.Y - start.Y, "拟合直线ΔY");
        var length = RequireFinite(double.Hypot(dx, dy), "拟合直线长度");
        if (length <= 1e-6)
            throw new InvalidOperationException("拟合直线端点重合或退化，垂足未定义，不以起点替代。");
        var ux = dx / length;
        var uy = dy / length;
        var px = RequireFinite(point.X - start.X, "投影ΔX");
        var py = RequireFinite(point.Y - start.Y, "投影ΔY");
        var projection = RequireFinite(px * ux + py * uy, "投影长度");
        var foot = RequireFinite(new Point(start.X + projection * ux, start.Y + projection * uy), "垂足");
        return (foot, RequireFinite(projection / length, "垂足参数"));
    }
}

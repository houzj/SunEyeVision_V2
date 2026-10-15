using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.HalAdapter;

/// <summary>Shared HALCON geometric circle fit; preserves the existing fixed-threshold pipeline.</summary>
public static class MeasuredCircleFitting
{
    public static (Point Center, double Radius, Point2d[] Inliers, double Rmse) Fit(
        IReadOnlyList<Point2d> points, double distanceThreshold, EdgeFitDiagnostics diagnostic, string label)
    {
        diagnostic.CandidatePoints = points.ToArray();
        diagnostic.DistanceThreshold = distanceThreshold;
        diagnostic.Stage = $"{label}点集校验";
        try
        {
            if (!double.IsFinite(distanceThreshold) || distanceThreshold <= 0)
                throw new InvalidOperationException("距离阈值必须是有限正数");
            if (points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
                throw new InvalidOperationException("取点结果包含非有限坐标");
            var unique = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
            diagnostic.DuplicateCount = points.Count - unique.Length;
            ValidateSupport(unique);

            diagnostic.Stage = $"{label}geotukey预拟合";
            var model = FitCore(unique, "geotukey");
            CaptureModel(model);
            diagnostic.Stage = $"{label}内点筛选";
            var inliers = unique.Where(p => Math.Abs(model.Residual(p)) <= distanceThreshold).ToArray();
            diagnostic.Inliers = inliers;
            diagnostic.Outliers = unique.Where(p => Math.Abs(model.Residual(p)) > distanceThreshold).ToArray();
            if (inliers.Length < 3)
                throw new InvalidOperationException($"拟合内点不足：独立候选={unique.Length}，内点={inliers.Length}，" +
                    $"离群点={diagnostic.Outliers.Length}，距离阈值={distanceThreshold:G6}px");
            diagnostic.Stage = $"{label}内点支持校验";
            ValidateSupport(inliers);
            if (inliers.Length != unique.Length)
            {
                diagnostic.Stage = $"{label}geometric精化";
                model = FitCore(inliers, "geometric");
                CaptureModel(model);
            }
            var rmse = Math.Sqrt(inliers.Average(p => Math.Pow(model.Residual(p), 2)));
            if (!double.IsFinite(rmse)) throw new InvalidOperationException("拟合残差包含非有限结果");
            diagnostic.Stage = $"{label}拟合完成";
            return (model.Center, model.Radius, inliers, rmse);

            void CaptureModel(CircleModel value)
            {
                diagnostic.CircleCenterX = value.Center.X;
                diagnostic.CircleCenterY = value.Center.Y;
                diagnostic.CircleRadius = value.Radius;
            }
        }
        catch (Exception ex)
        {
            diagnostic.Error = ex.Message;
            throw new InvalidOperationException($"{diagnostic.Stage}失败：{ex.Message}", ex);
        }
    }

    private readonly record struct CircleModel(Point Center, double Radius)
    {
        public double Residual(Point2d point)
        {
            var dx = point.X - Center.X;
            var dy = point.Y - Center.Y;
            return Math.Sqrt(dx * dx + dy * dy) - Radius;
        }
    }

    private static void ValidateSupport(Point2d[] points)
    {
        if (points.Length < 3)
            throw new InvalidOperationException($"有效独立测量点不足：{points.Length}，拟合圆至少需要3点");
        var origin = points[0];
        var farthest = points.OrderByDescending(p => SquaredDistance(p, origin)).First();
        var dx = farthest.X - origin.X;
        var dy = farthest.Y - origin.Y;
        var scale2 = dx * dx + dy * dy;
        if (!double.IsFinite(scale2) || scale2 < 1e-12)
            throw new InvalidOperationException("圆拟合退化：独立点的支持范围不足");
        // Numerical rank guard, not an edge-distance or user quality threshold.
        var cross = points.Max(p => Math.Abs((dx / Math.Sqrt(scale2)) * ((p.Y - origin.Y) / Math.Sqrt(scale2)) -
            (dy / Math.Sqrt(scale2)) * ((p.X - origin.X) / Math.Sqrt(scale2))));
        if (!double.IsFinite(cross) || cross <= 1e-12)
            throw new InvalidOperationException("圆拟合退化：独立测量点共线或数值上无法确定唯一圆");
    }

    private static double SquaredDistance(Point2d a, Point2d b)
        => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    private static CircleModel FitCore(Point2d[] points, string algorithm)
    {
        var cx = points.Average(p => p.X);
        var cy = points.Average(p => p.Y);
        var scale = Math.Sqrt(points.Max(p => (p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)));
        if (!double.IsFinite(cx) || !double.IsFinite(cy) || !double.IsFinite(scale) || scale < 1e-6)
            throw new InvalidOperationException("圆拟合退化：点集无法归一化");
        // Native arc-endpoint outputs are intentionally unused: these are unordered samples,
        // not a traced arc. The tool retains its explicit guide-arc angle contract.
        var ordered = points.OrderBy(p => Math.Atan2(p.Y - cy, p.X - cx)).ToArray();
        using var rows = new HTuple(ordered.Select(p => (p.Y - cy) / scale).ToArray());
        using var columns = new HTuple(ordered.Select(p => (p.X - cx) / scale).ToArray());
        HOperatorSet.GenContourPolygonXld(out var contour, rows, columns);
        using (contour)
        {
            HOperatorSet.FitCircleContourXld(contour, algorithm, -1, 0.0, 0, 3, 2.0,
                out var row, out var column, out var radius, out var start, out var end, out var order);
            using (row) using (column) using (radius) using (start) using (end) using (order)
            {
                if (row.Length != 1 || column.Length != 1 || radius.Length != 1)
                    throw new InvalidOperationException($"{algorithm}未返回唯一圆模型");
                var center = new Point(cx + column.D * scale, cy + row.D * scale);
                var r = radius.D * scale;
                if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) || !double.IsFinite(r) || r <= 0)
                    throw new InvalidOperationException($"{algorithm}返回无效圆心或非正半径");
                return new CircleModel(center, r);
            }
        }
    }
}

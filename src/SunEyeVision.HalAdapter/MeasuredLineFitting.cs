using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.HalAdapter;

/// <summary>Shared edge capture and order-independent line fitting. No threshold relaxation or fallback algorithm.</summary>
public static class MeasuredLineFitting
{
    public static Point2d[] Capture(HMetrologyModel model, HTuple index, string transition,
        EdgeFitDiagnostics diagnostic, string label)
    {
        diagnostic.AlgorithmPath = "HALCON取点＋轮廓拟合（不设卡尺支持率门槛）";
        diagnostic.Stage = $"{label}取点";
        try
        {
            using var nativeTransition = model.GetMetrologyObjectParam(index, "measure_transition");
            using var nativeSelection = model.GetMetrologyObjectParam(index, "measure_select");
            using var objectType = model.GetMetrologyObjectParam(index, "object_type");
            using var geometry = model.GetMetrologyObjectParam(index, "object_params");
            diagnostic.Transition = nativeTransition.S;
            diagnostic.Selection = nativeSelection.S;
            if (nativeTransition.S != transition)
                throw new InvalidOperationException($"取点极性{transition}与实际测量极性{nativeTransition.S}不一致");
            if (objectType.S == "line" && geometry.Length == 4)
            {
                double dx = geometry[3].D - geometry[1].D;
                double dy = geometry[2].D - geometry[0].D;
                double length = Math.Sqrt(dx * dx + dy * dy);
                diagnostic.ScanDirection = length > 0
                    ? $"图像法向({-dy / length:G6},{dx / length:G6})，起点看向终点的左侧→右侧"
                    : "退化引导线";
            }
            else diagnostic.ScanDirection = "内侧→外侧（径向外）";
            using var contours = model.GetMetrologyObjectMeasures(index, transition, out var rows, out var columns);
            using (rows)
            using (columns)
            {
                if (rows.Length != columns.Length)
                    throw new InvalidOperationException($"行列坐标数量不一致：{rows.Length}/{columns.Length}");
                diagnostic.RawPoints = Enumerable.Range(0, rows.Length)
                    .Select(i => new Point2d(columns[i].D, rows[i].D)).ToArray();
                diagnostic.CandidatePoints = diagnostic.RawPoints;
            }
            diagnostic.Stage = $"{label}测量带解析";
            diagnostic.Bands = Enumerable.Range(1, contours.CountObj()).Select(i =>
            {
                using var contour = contours.SelectObj(i);
                contour.GetContourXld(out var r, out var c);
                using (r)
                using (c)
                    return Enumerable.Range(0, r.Length).Select(j => new Point(c[j].D, r[j].D)).ToArray();
            }).ToArray();
            // Native classification only. Point order is undefined: never zip points with measurement bands.
            using var positiveBands = model.GetMetrologyObjectMeasures(index, "positive", out var positiveRows, out var positiveColumns);
            using (positiveRows)
            using (positiveColumns) diagnostic.PositivePointCount = positiveRows.Length;
            using var negativeBands = model.GetMetrologyObjectMeasures(index, "negative", out var negativeRows, out var negativeColumns);
            using (negativeRows)
            using (negativeColumns) diagnostic.NegativePointCount = negativeRows.Length;
            diagnostic.Stage = $"{label}取点完成";
            return diagnostic.RawPoints;
        }
        catch (Exception ex)
        {
            diagnostic.Error = ex.Message;
            throw new InvalidOperationException($"{diagnostic.Stage}失败：{ex.Message}", ex);
        }
    }

    public static (Point Start, Point End, Point2d[] Inliers, double Rmse) Fit(
        IReadOnlyList<Point2d> points, double distanceThreshold, Point guideStart, Point guideEnd,
        EdgeFitDiagnostics diagnostic, string label)
    {
        diagnostic.CandidatePoints = points.ToArray();
        diagnostic.AlgorithmPath = "HALCON取点＋轮廓拟合（不设卡尺支持率门槛）";
        diagnostic.NativeScore = diagnostic.NativeMinimumScore = double.NaN;
        diagnostic.DistanceThreshold = distanceThreshold;
        diagnostic.Stage = $"{label}点集校验";
        try
        {
            if (!double.IsFinite(distanceThreshold) || distanceThreshold <= 0)
                throw new InvalidOperationException("距离阈值必须是有限正数");
            if (points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
                throw new InvalidOperationException("取点结果包含非有限坐标");
            // Sorting makes fitting deterministic, but geometry never depends on contour endpoints.
            var unique = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
            diagnostic.DuplicateCount = points.Count - unique.Length;
            if (unique.Length < 2)
                throw new InvalidOperationException($"有效独立测量点不足：候选={points.Count}，去重后={unique.Length}，至少需要2点");
            var direction = guideEnd - guideStart;
            if (!double.IsFinite(direction.X) || !double.IsFinite(direction.Y) || direction.Length < 1e-6)
                throw new InvalidOperationException("修正后的引导方向无效");

            diagnostic.Stage = $"{label}Tukey预拟合";
            var model = FitCore(unique, "tukey");
            CaptureModel(model);
            diagnostic.Stage = $"{label}内点筛选";
            var inliers = unique.Where(p => Math.Abs(model.SignedDistance(p)) <= distanceThreshold).ToArray();
            diagnostic.Inliers = inliers;
            diagnostic.Outliers = unique.Where(p => Math.Abs(model.SignedDistance(p)) > distanceThreshold).ToArray();
            if (inliers.Length < 2)
                throw new InvalidOperationException($"拟合内点不足：独立候选={unique.Length}，内点={inliers.Length}，" +
                    $"离群点={diagnostic.Outliers.Length}，距离阈值={distanceThreshold:G6}px");
            if (inliers.Length != unique.Length)
            {
                diagnostic.Stage = $"{label}内点精化";
                model = FitCore(inliers, "regression");
                CaptureModel(model);
            }

            diagnostic.Stage = $"{label}支持范围校验";
            var tangent = new Vector(-model.Ny, model.Nx);
            var alignment = Vector.Multiply(tangent, direction);
            if (alignment < 0 || (alignment == 0 && (tangent.X < 0 || (tangent.X == 0 && tangent.Y < 0))))
                tangent = -tangent;
            var projections = inliers.Select(p => (p.X - model.Anchor.X) * tangent.X +
                (p.Y - model.Anchor.Y) * tangent.Y).ToArray();
            var low = projections.Min();
            var high = projections.Max();
            if (!double.IsFinite(low) || !double.IsFinite(high) || high - low < 1e-6)
                throw new InvalidOperationException("拟合模型退化：独立内点沿直线的支持范围不足");
            var start = model.Anchor + tangent * low;
            var end = model.Anchor + tangent * high;
            var rmse = Math.Sqrt(inliers.Average(p => Math.Pow(model.SignedDistance(p), 2)));
            if (!double.IsFinite(start.X) || !double.IsFinite(start.Y) || !double.IsFinite(end.X) ||
                !double.IsFinite(end.Y) || !double.IsFinite(rmse))
                throw new InvalidOperationException("拟合模型包含非有限结果");
            diagnostic.Stage = $"{label}拟合完成";
            return (start, end, inliers, rmse);

            void CaptureModel(LineModel value)
            {
                diagnostic.NormalX = value.Nx;
                diagnostic.NormalY = value.Ny;
                diagnostic.AnchorX = value.Anchor.X;
                diagnostic.AnchorY = value.Anchor.Y;
            }
        }
        catch (Exception ex)
        {
            diagnostic.Error = ex.Message;
            throw new InvalidOperationException($"{diagnostic.Stage}失败：{ex.Message}", ex);
        }
    }

    private readonly record struct LineModel(double Nx, double Ny, Point Anchor)
    {
        public double SignedDistance(Point2d point) => (point.X - Anchor.X) * Nx + (point.Y - Anchor.Y) * Ny;
    }

    private static LineModel FitCore(Point2d[] points, string algorithm)
    {
        if (points.Length == 2)
        {
            var delta = new Vector(points[1].X - points[0].X, points[1].Y - points[0].Y);
            if (!double.IsFinite(delta.Length) || delta.Length < 1e-6)
                throw new InvalidOperationException("拟合模型退化：两个独立点间距不足");
            return new LineModel(-delta.Y / delta.Length, delta.X / delta.Length, new Point(points[0].X, points[0].Y));
        }
        var cx = points.Average(p => p.X);
        var cy = points.Average(p => p.Y);
        using var rows = new HTuple(points.Select(p => p.Y - cy).ToArray());
        using var columns = new HTuple(points.Select(p => p.X - cx).ToArray());
        HOperatorSet.GenContourPolygonXld(out var contour, rows, columns);
        using (contour)
        {
            HOperatorSet.FitLineContourXld(contour, algorithm, -1, 0, 3, 2.0,
                out var rb, out var cb, out var re, out var ce, out var nr, out var nc, out var distance);
            using (rb) using (cb) using (re) using (ce) using (nr) using (nc) using (distance)
            {
                if (nr.Length != 1 || nc.Length != 1 || distance.Length != 1)
                    throw new InvalidOperationException($"{algorithm}未返回唯一的直线方程");
                var norm = Math.Sqrt(nr.D * nr.D + nc.D * nc.D);
                if (!double.IsFinite(norm) || norm < 1e-12 || !double.IsFinite(distance.D))
                    throw new InvalidOperationException($"{algorithm}返回无效直线方程");
                var nx = nc.D / norm;
                var ny = nr.D / norm;
                var offset = distance.D / norm;
                return new LineModel(nx, ny, new Point(cx + nx * offset, cy + ny * offset));
            }
        }
    }
}

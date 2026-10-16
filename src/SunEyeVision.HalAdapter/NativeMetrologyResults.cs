using System;
using System.Linq;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.HalAdapter;

/// <summary>Reads native Metrology models and used edges. Never reselects points or refits geometry.</summary>
public static class NativeMetrologyResults
{
    public static (Point Start, Point End, Point2d[] Inliers, double Rmse) ReadLine(
        HMetrologyModel model, HTuple index, Point guideStart, Point guideEnd, EdgeFitDiagnostics diagnostic, string label)
    {
        Begin(diagnostic, label);
        try
        {
            ReadNativeSettings(model, index, diagnostic);
            using var values = model.GetMetrologyObjectResult(index.I, "all", "result_type", "all_param");
            ValidateModel(values, 4, diagnostic);
            var start = new Point(values[1].D, values[0].D);
            var end = new Point(values[3].D, values[2].D);
            var direction = end - start;
            if (!double.IsFinite(direction.Length) || direction.Length < 1e-6)
                throw new InvalidOperationException("原生直线模型退化，起终点没有有效间距");
            var guide = guideEnd - guideStart;
            if (Vector.Multiply(direction, guide) < 0) (start, end) = (end, start);
            var used = ReadUsedEdges(model, index, diagnostic, label, 2);
            using var rows = new HTuple(used.Select(p => p.Y).ToArray());
            using var columns = new HTuple(used.Select(p => p.X).ToArray());
            HOperatorSet.DistancePl(rows, columns, start.Y, start.X, end.Y, end.X, out var distances);
            using (distances)
            {
                var rmse = Math.Sqrt(distances.DArr.Average(d => d * d));
                if (!double.IsFinite(rmse)) throw new InvalidOperationException("原生直线的残差无效");
                diagnostic.Stage = $"{label}原生拟合完成";
                return (start, end, used, rmse);
            }
        }
        catch (Exception ex) { throw RecordFailure(diagnostic, ex); }
    }

    public static (Point Center, double Radius, Point2d[] Inliers, double Rmse) ReadCircle(
        HMetrologyModel model, HTuple index, EdgeFitDiagnostics diagnostic, string label)
    {
        Begin(diagnostic, label);
        try
        {
            ReadNativeSettings(model, index, diagnostic);
            using var values = model.GetMetrologyObjectResult(index.I, "all", "result_type", "all_param");
            ValidateModel(values, 3, diagnostic);
            var center = new Point(values[1].D, values[0].D);
            var radius = values[2].D;
            if (radius <= 0) throw new InvalidOperationException("原生圆模型返回非正半径");
            var used = ReadUsedEdges(model, index, diagnostic, label, 3);
            // Derived output only: no filtering and no second fit.
            var rmse = Math.Sqrt(used.Average(p => Math.Pow(
                Math.Sqrt((p.X - center.X) * (p.X - center.X) + (p.Y - center.Y) * (p.Y - center.Y)) - radius, 2)));
            if (!double.IsFinite(rmse)) throw new InvalidOperationException("原生圆的残差无效");
            diagnostic.CircleCenterX = center.X;
            diagnostic.CircleCenterY = center.Y;
            diagnostic.CircleRadius = radius;
            diagnostic.Stage = $"{label}原生拟合完成";
            return (center, radius, used, rmse);
        }
        catch (Exception ex) { throw RecordFailure(diagnostic, ex); }
    }

    private static void Begin(EdgeFitDiagnostics diagnostic, string label)
    {
        diagnostic.AlgorithmPath = "HALCON原生Metrology";
        diagnostic.Stage = $"{label}原生模型读取";
    }

    private static void ReadNativeSettings(HMetrologyModel model, HTuple index, EdgeFitDiagnostics diagnostic)
    {
        using var score = model.GetMetrologyObjectParam(index, "min_score");
        using var threshold = model.GetMetrologyObjectParam(index, "distance_threshold");
        if (score.Length != 1 || threshold.Length != 1 || !double.IsFinite(score.D) || !double.IsFinite(threshold.D))
            throw new InvalidOperationException("原生拟合门槛配置无效");
        diagnostic.NativeMinimumScore = score.D;
        diagnostic.DistanceThreshold = threshold.D;
        diagnostic.DuplicateCount = diagnostic.RawPoints.Length - diagnostic.RawPoints.Distinct().Count();
    }

    private static void ValidateModel(HTuple values, int expected, EdgeFitDiagnostics diagnostic)
    {
        if (values.Length == 0)
            throw new InvalidOperationException($"HALCON未返回有效拟合实例；原始检测点={diagnostic.RawPoints.Length}。" +
                $"实际测量带={diagnostic.Bands.Length}，原生min_score={diagnostic.NativeMinimumScore:G6}。" +
                (diagnostic.Bands.Length > 0 && diagnostic.RawPoints.Length < Math.Ceiling(diagnostic.Bands.Length * diagnostic.NativeMinimumScore)
                    ? "即使全部检测点均被采用，也不足以达到当前原生支持度要求；未降低门槛或回退重拟合。" : "") +
                "请结合测量带、边缘点和原生支持度条件检查，不代表一定是阈值错误。");
        if (values.Length != expected)
            throw new InvalidOperationException($"当前槽位要求一个原生模型（{expected}个参数），实际返回{values.Length}个参数，未自动截取实例");
        if (values.DArr.Any(v => !double.IsFinite(v)))
            throw new InvalidOperationException("HALCON返回非有限模型参数");
    }

    private static Point2d[] ReadUsedEdges(HMetrologyModel model, HTuple index,
        EdgeFitDiagnostics diagnostic, string label, int minimum)
    {
        diagnostic.Stage = $"{label}原生实际拟合点读取";
        using var score = model.GetMetrologyObjectResult(index.I, "all", "result_type", "score");
        using var rows = model.GetMetrologyObjectResult(index.I, "all", "used_edges", "row");
        using var columns = model.GetMetrologyObjectResult(index.I, "all", "used_edges", "column");
        using var amplitudes = model.GetMetrologyObjectResult(index.I, "all", "used_edges", "amplitude");
        if (score.Length != 1 || !double.IsFinite(score.D))
            throw new InvalidOperationException("原生模型分数数量或数值无效");
        diagnostic.NativeScore = score.D;
        diagnostic.UsedAmplitudes = amplitudes.DArr;
        if (amplitudes.Length != rows.Length || diagnostic.UsedAmplitudes.Any(a => !double.IsFinite(a)))
            throw new InvalidOperationException("原生实际采用点与边缘幅值数量或数值不一致");
        if ((diagnostic.Transition == "negative" && diagnostic.UsedAmplitudes.Any(a => a >= 0)) ||
            (diagnostic.Transition == "positive" && diagnostic.UsedAmplitudes.Any(a => a <= 0)))
            throw new InvalidOperationException("原生采用点的边缘幅值符号与配置极性不一致，拒绝作为有效结果输出");
        if (rows.Length != columns.Length)
            throw new InvalidOperationException($"原生拟合点行列数量不一致：{rows.Length}/{columns.Length}");
        diagnostic.Inliers = Enumerable.Range(0, rows.Length).Select(i => new Point2d(columns[i].D, rows[i].D)).ToArray();
        if (diagnostic.Inliers.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
            throw new InvalidOperationException("原生实际拟合点包含非有限坐标");
        if (diagnostic.Inliers.Length < minimum)
            throw new InvalidOperationException($"原生模型实际拟合点不足：{diagnostic.Inliers.Length}，至少需要{minimum}点");
        // Diagnostic set difference only, never fed back into the model or measurement.
        diagnostic.Outliers = diagnostic.RawPoints.Except(diagnostic.Inliers).ToArray();
        return diagnostic.Inliers;
    }

    private static InvalidOperationException RecordFailure(EdgeFitDiagnostics diagnostic, Exception ex)
    {
        diagnostic.Error = ex.Message;
        return new InvalidOperationException($"{diagnostic.Stage}失败：{ex.Message}", ex);
    }
}

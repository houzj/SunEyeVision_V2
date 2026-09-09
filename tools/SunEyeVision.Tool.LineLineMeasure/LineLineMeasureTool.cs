using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Tool.LineLineMeasure
{
    /// <summary>
    /// 线-线测量工具：两条引导线各自做 2D Metrology measure（Halcon），
    /// 拟合两条被测直线 → 计算锐角夹角（主）与平行间距（辅助）→ MeasureJudge 判定 OK/NG。
    /// 引擎无关契约：公开 API 不暴露 HImage；工具引 HalAdapter（v4 B.3 首期统一）。
    /// </summary>
    [Tool(
        id: "LineLineMeasure",
        displayName: "线-线测量",
        Description = "线-线测量工具：两卡尺沿引导线测量拟合直线，输出夹角与平行间距并判定",
        Icon = "📐",
        Category = "测量工具",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class LineLineMeasureTool : IToolPlugin<LineLineMeasureParameters, LineLineMeasureResults>
    {
        private readonly ToolAttribute? _attribute;
        public LineLineMeasureTool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

        public LineLineMeasureResults Run(LineLineMeasureParameters parameters)
        {
            var result = new LineLineMeasureResults
            {
                ToolName = _attribute?.DisplayName ?? "线-线测量",
                ToolId = _attribute?.Id ?? "LineLineMeasure",
                Timestamp = DateTime.Now,
            };

            try
            {
                // ===== 参数校验（Fail-Fast）=====
                var e1Start = new Point(parameters.E1StartX.Value, parameters.E1StartY.Value);
                var e1End = new Point(parameters.E1EndX.Value, parameters.E1EndY.Value);
                var e2Start = new Point(parameters.E2StartX.Value, parameters.E2StartY.Value);
                var e2End = new Point(parameters.E2EndX.Value, parameters.E2EndY.Value);

                if ((e1End - e1Start).Length < 1e-6)
                    throw new InvalidOperationException("卡尺1 引导直线未设置或长度无效");
                if ((e2End - e2Start).Length < 1e-6)
                    throw new InvalidOperationException("卡尺2 引导直线未设置或长度无效");

                // ===== 输入图像 =====
                var hImage = parameters.GetHalconImage();
                if (hImage == null || !hImage.IsInitialized())
                    throw new InvalidOperationException("输入图像为空或无效");

                // ===== 位置修正（同源：交互层与 Run 一致）=====
                AffineMatrix? matrix = null;
                if (parameters.IsPositionCorrectionEnabled)
                    matrix = PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource);

                // ===== 单 metrology 模型承载两条引导 =====
                using var metrologyModel = new HMetrologyModel();
                metrologyModel.CreateMetrologyModel();
                HOperatorSet.GetImageSize(hImage, out var width, out var height);
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);

                // 元素1 引导 → measure 对象
                var e1StartT = Transform(e1Start, matrix);
                var e1EndT = Transform(e1End, matrix);
                var e1Index = metrologyModel.AddMetrologyObjectLineMeasure(
                    e1StartT.Y, e1StartT.X, e1EndT.Y, e1EndT.X,
                    parameters.E1MeasureLength1.Value, parameters.E1MeasureLength2.Value,
                    parameters.E1Sigma.Value, parameters.E1Threshold.Value,
                    BuildGenParamName(), BuildGenParamValue(
                        parameters.E1NumMeasures.Value,
                        TransitionToHalcon(parameters.E1Transition.Value),
                        SelectToHalcon(parameters.E1Select.Value),
                        parameters.E1DistanceThreshold.Value));

                // 元素2 引导 → measure 对象
                var e2StartT = Transform(e2Start, matrix);
                var e2EndT = Transform(e2End, matrix);
                var e2Index = metrologyModel.AddMetrologyObjectLineMeasure(
                    e2StartT.Y, e2StartT.X, e2EndT.Y, e2EndT.X,
                    parameters.E2MeasureLength1.Value, parameters.E2MeasureLength2.Value,
                    parameters.E2Sigma.Value, parameters.E2Threshold.Value,
                    BuildGenParamName(), BuildGenParamValue(
                        parameters.E2NumMeasures.Value,
                        TransitionToHalcon(parameters.E2Transition.Value),
                        SelectToHalcon(parameters.E2Select.Value),
                        parameters.E2DistanceThreshold.Value));

                // 执行测量（两引导一次 apply）
                metrologyModel.ApplyMetrologyModel(hImage);

                // 逐引导提取测点 + 全点鲁棒拟合
                var e1Points = ExtractDiagnosticPoints(metrologyModel, e1Index, TransitionToHalcon(parameters.E1Transition.Value));
                var (e1FitStart, e1FitEnd, e1Inliers) = FitLineRobust(e1Points, parameters.E1DistanceThreshold.Value);

                var e2Points = ExtractDiagnosticPoints(metrologyModel, e2Index, TransitionToHalcon(parameters.E2Transition.Value));
                var (e2FitStart, e2FitEnd, e2Inliers) = FitLineRobust(e2Points, parameters.E2DistanceThreshold.Value);

                // 拟合内点收集（卡尺拟合点显示）
                result.Element1FitPoints = e1Inliers.Select(p => new Point(p.X, p.Y)).ToArray();
                result.Element2FitPoints = e2Inliers.Select(p => new Point(p.X, p.Y)).ToArray();

                // ===== 结果落位 =====
                result.Element1Line = new[] { Line2d.FromTwoPoints(e1FitStart, e1FitEnd) };
                result.Element1StartPoint = e1FitStart;
                result.Element1EndPoint = e1FitEnd;
                result.Element2Line = new[] { Line2d.FromTwoPoints(e2FitStart, e2FitEnd) };
                result.Element2StartPoint = e2FitStart;
                result.Element2EndPoint = e2FitEnd;

                // 主测量：锐角夹角 0~90°
                result.Angle = ComputeAcuteAngle(e1FitStart, e1FitEnd, e2FitStart, e2FitEnd);

                // 平行 → 间距；非平行 → 交点（无限线延长）
                result.IsParallel = result.Angle < 0.5;
                if (result.IsParallel)
                {
                    result.Distance = ParallelDistance(e1FitStart, e1FitEnd, e2FitStart, e2FitEnd);
                }
                else
                {
                    var intersection = LineIntersection(e1FitStart, e1FitEnd, e2FitStart, e2FitEnd);
                    result.IntersectionX = intersection.X;
                    result.IntersectionY = intersection.Y;
                }

                // 单线属性：中心/方向角
                result.Element1Center = MidPoint(e1FitStart, e1FitEnd);
                result.Element1Angle = NormalizeAngle360(ComputeLineAngleDeg(e1FitStart, e1FitEnd));
                result.Element2Center = MidPoint(e2FitStart, e2FitEnd);
                result.Element2Angle = NormalizeAngle360(ComputeLineAngleDeg(e2FitStart, e2FitEnd));

                // ===== 判定（v4 C1 基座；启用项无有效值 → NG，rule-015）=====
                var judged = MeasureJudge.EvaluateAll(
                    parameters.Judgments,
                    t => t.Name switch
                    {
                        "角度" => result.Angle,
                        "距离" => double.IsNaN(result.Distance) ? null : result.Distance,
                        "交点X" => double.IsNaN(result.IntersectionX) ? null : result.IntersectionX,
                        "交点Y" => double.IsNaN(result.IntersectionY) ? null : result.IntersectionY,
                        _ => null,
                    });
                result.IsPass = MeasureJudge.IsAllPass(judged);

                result.FillDisplay(result.IsPass, parameters);
                result.SetSuccess();
            }
            catch (Exception ex)
            {
                parameters.LogError($"线-线测量失败: {ex.Message}", ex);
                result.SetError($"测量失败: {ex.Message}", ex);
            }
            return result;
        }

        // ============ 单引导 measure + 拟合（对两条线各执行一次） ============

        private static Point Transform(Point p, AffineMatrix? matrix)
            => matrix == null ? p : matrix.TransformWpfPoint(p);

        private static Point2d[] ExtractDiagnosticPoints(HMetrologyModel model, HTuple objectIndex, string transition)
        {
            try
            {
                var contour = model.GetMetrologyObjectMeasures(objectIndex, transition, out var row, out var col);
                contour?.Dispose();
                if (row.Length == 0) return Array.Empty<Point2d>();
                var pts = new Point2d[row.Length];
                for (int i = 0; i < row.Length; i++)
                    pts[i] = new Point2d(col[i].D, row[i].D);
                return pts;
            }
            catch
            {
                return Array.Empty<Point2d>();
            }
        }

        private static (Point Start, Point End, Point2d[] Inliers) FitLineRobust(IReadOnlyList<Point2d> points, double distanceThreshold)
        {
            if (points.Count < 2)
                throw new InvalidOperationException($"有效测量点不足({points.Count} < 2)，无法拟合直线");

            var (s0, e0) = FitLineCore(points, "tukey");
            var inliers = points
                .Where(p => DistancePointToLine(p, s0, e0) <= distanceThreshold)
                .ToArray();
            if (inliers.Length < 2)
                throw new InvalidOperationException(
                    $"拟合内点不足({inliers.Length} < 2)：distance_threshold={distanceThreshold:F1} 过小或引导偏差过大");
            var (start, end) = inliers.Length == points.Count
                ? (s0, e0)
                : FitLineCore(inliers, "regression");
            return (start, end, inliers);
        }

        private static (Point Start, Point End) FitLineCore(IReadOnlyList<Point2d> points, string algorithm)
        {
            var rows = new HTuple(points.Select(p => (double)p.Y).ToArray());
            var cols = new HTuple(points.Select(p => (double)p.X).ToArray());
            HOperatorSet.GenContourPolygonXld(out var contour, rows, cols);
            try
            {
                HOperatorSet.FitLineContourXld(contour, algorithm, -1, 0, 3, 2.0,
                    out var rowBegin, out var colBegin, out var rowEnd, out var colEnd, out _, out _, out _);
                if (rowBegin.Length < 1 || colBegin.Length < 1 || rowEnd.Length < 1 || colEnd.Length < 1)
                    throw new InvalidOperationException($"直线拟合失败(algorithm={algorithm})：未返回有效结果");
                return (new Point(colBegin[0].D, rowBegin[0].D), new Point(colEnd[0].D, rowEnd[0].D));
            }
            finally
            {
                contour.Dispose();
            }
        }

        private static double DistancePointToLine(Point2d p, Point a, Point b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len2 = dx * dx + dy * dy;
            if (len2 < 1e-12)
                return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            return Math.Abs(dx * (p.Y - a.Y) - dy * (p.X - a.X)) / Math.Sqrt(len2);
        }

        // ============ 几何量 ============

        private static double ComputeAcuteAngle(Point a1, Point a2, Point b1, Point b2)
        {
            var d1 = new Vector(a2.X - a1.X, a2.Y - a1.Y);
            var d2 = new Vector(b2.X - b1.X, b2.Y - b1.Y);
            var l1 = d1.Length;
            var l2 = d2.Length;
            if (l1 < 1e-9 || l2 < 1e-9) return 0.0;
            var cos = Math.Abs(Vector.Multiply(d1, d2)) / (l1 * l2);
            cos = Math.Clamp(cos, -1.0, 1.0);
            return Math.Acos(cos) * 180.0 / Math.PI;   // 锐角 0~90°
        }

        private static double ParallelDistance(Point a1, Point a2, Point b1, Point b2)
        {
            var d = new Vector(a2.X - a1.X, a2.Y - a1.Y);
            var len = d.Length;
            if (len < 1e-9) return 0.0;
            var n = new Vector(-d.Y / len, d.X / len);          // 单位法线
            var midA = new Point((a1.X + a2.X) / 2, (a1.Y + a2.Y) / 2);
            var midB = new Point((b1.X + b2.X) / 2, (b1.Y + b2.Y) / 2);
            var delta = new Vector(midB.X - midA.X, midB.Y - midA.Y);
            return Math.Abs(Vector.Multiply(delta, n));          // 两平行线间距
        }

        /// <summary>两无限直线交点（非平行场景）</summary>
        private static Point LineIntersection(Point a1, Point a2, Point b1, Point b2)
        {
            double denom = (a1.X - a2.X) * (b1.Y - b2.Y) - (a1.Y - a2.Y) * (b1.X - b2.X);
            double t = ((a1.X - b1.X) * (b1.Y - b2.Y) - (a1.Y - b1.Y) * (b1.X - b2.X)) / denom;
            return new Point(a1.X + t * (a2.X - a1.X), a1.Y + t * (a2.Y - a1.Y));
        }

        private static Point MidPoint(Point a, Point b)
            => new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);

        /// <summary>线段方向角（度，atan2 未归一化）</summary>
        private static double ComputeLineAngleDeg(Point start, Point end)
            => Math.Atan2(end.Y - start.Y, end.X - start.X) * 180.0 / Math.PI;

        private static double NormalizeAngle360(double angle)
        {
            angle %= 360.0;
            return angle < 0 ? angle + 360.0 : angle;
        }

        // ============ Halcon gen-param 翻译 ============

        private static HTuple BuildGenParamName() => new(new[]
        {
            "num_measures", "measure_transition", "measure_select", "distance_threshold",
        });

        private static HTuple BuildGenParamValue(int numMeasures, string transition, string select, double distanceThreshold)
            => new(new object[] { numMeasures, transition, select, distanceThreshold });

        private static string TransitionToHalcon(EdgeTransition transition) => transition switch
        {
            EdgeTransition.Positive => "positive",
            EdgeTransition.Negative => "negative",
            _ => "all",
        };

        private static string SelectToHalcon(EdgeSelect select) => select switch
        {
            EdgeSelect.First => "first",
            EdgeSelect.Last => "last",
            _ => "all",
        };

        // ============ 控件创建 ============

        public FrameworkElement? CreateDebugControl(IToolPlugin tool)
            => new Views.LineLineMeasureToolDebugControl();

        public FrameworkElement? CreateDataAnalysisControl() => null;
    }
}

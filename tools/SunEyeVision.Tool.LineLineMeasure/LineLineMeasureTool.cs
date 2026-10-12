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
        Icon = "line-line", Order = 40,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Measurement,
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class LineLineMeasureTool : IToolPlugin<LineLineMeasureParameters, LineLineMeasureResults>, IToolEditorFactory
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
                var e1Start = new Point(parameters.E1StartX, parameters.E1StartY);
                var e1End = new Point(parameters.E1EndX, parameters.E1EndY);
                var e2Start = new Point(parameters.E2StartX, parameters.E2StartY);
                var e2End = new Point(parameters.E2EndX, parameters.E2EndY);

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
                result.ImageWidth = (int)width.L;      // 供结果显示把"无限直线延长段"裁剪到图像范围
                result.ImageHeight = (int)height.L;

                // 元素1 引导 → measure 对象
                var e1StartT = Transform(e1Start, matrix);
                var e1EndT = Transform(e1End, matrix);
                var e1Index = metrologyModel.AddMetrologyObjectLineMeasure(
                    e1StartT.Y, e1StartT.X, e1EndT.Y, e1EndT.X,
                    parameters.E1MeasureLength1, parameters.E1MeasureLength2,
                    parameters.E1Sigma, parameters.E1Threshold,
                    BuildGenParamName(), BuildGenParamValue(
                        parameters.E1NumMeasures,
                        TransitionToHalcon(parameters.E1Transition),
                        SelectToHalcon(parameters.E1Select),
                        parameters.E1DistanceThreshold));

                // 元素2 引导 → measure 对象
                var e2StartT = Transform(e2Start, matrix);
                var e2EndT = Transform(e2End, matrix);
                var e2Index = metrologyModel.AddMetrologyObjectLineMeasure(
                    e2StartT.Y, e2StartT.X, e2EndT.Y, e2EndT.X,
                    parameters.E2MeasureLength1, parameters.E2MeasureLength2,
                    parameters.E2Sigma, parameters.E2Threshold,
                    BuildGenParamName(), BuildGenParamValue(
                        parameters.E2NumMeasures,
                        TransitionToHalcon(parameters.E2Transition),
                        SelectToHalcon(parameters.E2Select),
                        parameters.E2DistanceThreshold));

                // 执行测量（两引导一次 apply）
                metrologyModel.ApplyMetrologyModel(hImage);

                // 逐引导提取测点 + 全点鲁棒拟合
                var e1Points = ExtractDiagnosticPoints(metrologyModel, e1Index, TransitionToHalcon(parameters.E1Transition));
                var (e1FitStart, e1FitEnd, e1Inliers, e1Rmse) = FitLineRobust(e1Points, parameters.E1DistanceThreshold);

                var e2Points = ExtractDiagnosticPoints(metrologyModel, e2Index, TransitionToHalcon(parameters.E2Transition));
                var (e2FitStart, e2FitEnd, e2Inliers, e2Rmse) = FitLineRobust(e2Points, parameters.E2DistanceThreshold);

                // 拟合内点/残差/点数（模块结果："输入/卡尺"检测点 + "拟合结果"）
                result.Element1FitPoints = e1Inliers;          // Point2d[]（契约可入表，显示层再转 WPF Point）
                result.Element2FitPoints = e2Inliers;
                result.Element1RMSE = e1Rmse;
                result.Element2RMSE = e2Rmse;
                result.Element1EdgePointCount = e1Inliers.Length;
                result.Element2EdgePointCount = e2Inliers.Length;

                // ===== 结果落位 =====
                result.Element1Line = new[] { Line2d.FromTwoPoints(e1FitStart, e1FitEnd) };
                result.Element1StartPoint = e1FitStart;
                result.Element1EndPoint = e1FitEnd;
                result.Element2Line = new[] { Line2d.FromTwoPoints(e2FitStart, e2FitEnd) };
                result.Element2StartPoint = e2FitStart;
                result.Element2EndPoint = e2FitEnd;

                // 主测量：锐角夹角 0~90°（HALCON angle_ll：有向角 -π~π → 锐角化）
                result.Angle = ComputeAcuteAngle(e1FitStart, e1FitEnd, e2FitStart, e2FitEnd);

                // 距离（HALCON distance_pl，正交/法向投影）：
                //   主值 Distance      = 线1 中点 → 线2 无限直线的法向距离
                //   极值 Min/Max       = 线1 **所有拟合点** → 线2 无限直线的法向距离的 min/max
                //                        （平行时 min≈max=间距；有夹角时反映间距一致性/平行度）
                var e1Mid = MidPoint(e1FitStart, e1FitEnd);
                HOperatorSet.DistancePl(
                    e1Mid.Y, e1Mid.X,
                    e2FitStart.Y, e2FitStart.X, e2FitEnd.Y, e2FitEnd.X,
                    out var normalDistance);
                result.Distance = normalDistance.D;

                if (e1Inliers.Length > 0)
                {
                    // distance_pl 支持参数广播：N 个点 + 1 条直线 → N 个正交距离
                    var inlierRows = new HTuple(e1Inliers.Select(p => p.Y).ToArray());
                    var inlierCols = new HTuple(e1Inliers.Select(p => p.X).ToArray());
                    HOperatorSet.DistancePl(
                        inlierRows, inlierCols,
                        e2FitStart.Y, e2FitStart.X, e2FitEnd.Y, e2FitEnd.X,
                        out var inlierDistances);
                    HOperatorSet.TupleMin(inlierDistances, out var distanceMin);
                    HOperatorSet.TupleMax(inlierDistances, out var distanceMax);
                    result.DistanceMin = distanceMin.D;
                    result.DistanceMax = distanceMax.D;

                    // 记录取到极值的拟合点（供图形显示：该点 → 线2 的法向投影段）
                    var inlierDistanceArray = inlierDistances.DArr;
                    int minIndex = Array.IndexOf(inlierDistanceArray, result.DistanceMin);
                    int maxIndex = Array.IndexOf(inlierDistanceArray, result.DistanceMax);
                    if (minIndex >= 0)
                        result.DistanceMinPoint = new Point(e1Inliers[minIndex].X, e1Inliers[minIndex].Y);
                    if (maxIndex >= 0)
                        result.DistanceMaxPoint = new Point(e1Inliers[maxIndex].X, e1Inliers[maxIndex].Y);
                }
                else
                {
                    result.DistanceMin = result.Distance;
                    result.DistanceMax = result.Distance;
                }

                // 交点 + 平行判定：HALCON intersection_ll（自带 isParallel，替代自研 0.5° 硬阈值）
                HOperatorSet.IntersectionLl(
                    e1FitStart.Y, e1FitStart.X, e1FitEnd.Y, e1FitEnd.X,
                    e2FitStart.Y, e2FitStart.X, e2FitEnd.Y, e2FitEnd.X,
                    out var intersectionRow, out var intersectionColumn, out var isParallel);
                result.IsParallel = isParallel.I != 0;
                if (!result.IsParallel)
                {
                    result.IntersectionX = intersectionColumn.D;
                    result.IntersectionY = intersectionRow.D;
                }

                // 单线属性：中心/方向角
                result.Element1Center = MidPoint(e1FitStart, e1FitEnd);
                result.Element1Angle = NormalizeAngle360(ComputeLineAngleDeg(e1FitStart, e1FitEnd));
                result.Element2Center = MidPoint(e2FitStart, e2FitEnd);
                result.Element2Angle = NormalizeAngle360(ComputeLineAngleDeg(e2FitStart, e2FitEnd));

                // ===== 输入回显（模块结果"输入/卡尺"，对齐 LineDetect 规范）=====
                result.GuideGeometry1 = BuildGuideGeometry(e1Start, e1End);
                result.GuideGeometry2 = BuildGuideGeometry(e2Start, e2End);
                result.E1CaliberCount = parameters.E1NumMeasures;
                result.E1CaliberWidth = parameters.E1MeasureLength1 * 2;   // 半长 → 全长
                result.E1CaliberLength = parameters.E1MeasureLength2 * 2;
                result.E1CaliberTransition = parameters.E1Transition.ToString();
                result.E2CaliberCount = parameters.E2NumMeasures;
                result.E2CaliberWidth = parameters.E2MeasureLength1 * 2;
                result.E2CaliberLength = parameters.E2MeasureLength2 * 2;
                result.E2CaliberTransition = parameters.E2Transition.ToString();

                // ===== 判定（v4 C1 基座；启用项无有效值 → NG，rule-015）=====
                var judged = ResultJudgmentEvaluator.EvaluateRanges(
                    parameters.ResultPresentation.Judgments,
                    key => parameters.ResultPresentation.GetJudgment(key).Name switch
                    {
                        "角度" => result.Angle,
                        "距离" => double.IsNaN(result.Distance) ? null : result.Distance,
                        "交点X" => double.IsNaN(result.IntersectionX) ? null : result.IntersectionX,
                        "交点Y" => double.IsNaN(result.IntersectionY) ? null : result.IntersectionY,
                        _ => null,
                    });
                result.IsPass = ResultJudgmentEvaluator.IsAllPass(judged);
                result.JudgmentDetails = judged.Select(j => j.Message).ToArray();

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

        private static (Point Start, Point End, Point2d[] Inliers, double Rmse) FitLineRobust(IReadOnlyList<Point2d> points, double distanceThreshold)
        {
            if (points.Count < 2)
                throw new InvalidOperationException($"有效测量点不足({points.Count} < 2)，无法拟合直线");

            var (s0, e0, rmse0) = FitLineCore(points, "tukey");
            var inliers = points
                .Where(p => DistancePointToLine(p, s0, e0) <= distanceThreshold)
                .ToArray();
            if (inliers.Length < 2)
                throw new InvalidOperationException(
                    $"拟合内点不足({inliers.Length} < 2)：distance_threshold={distanceThreshold:F1} 过小或引导偏差过大");
            var (start, end, rmse) = inliers.Length == points.Count
                ? (s0, e0, rmse0)
                : FitLineCore(inliers, "regression");
            return (start, end, inliers, rmse);
        }

        private static (Point Start, Point End, double Rmse) FitLineCore(IReadOnlyList<Point2d> points, string algorithm)
        {
            var rows = new HTuple(points.Select(p => (double)p.Y).ToArray());
            var cols = new HTuple(points.Select(p => (double)p.X).ToArray());
            HOperatorSet.GenContourPolygonXld(out var contour, rows, cols);
            try
            {
                HOperatorSet.FitLineContourXld(contour, algorithm, -1, 0, 3, 2.0,
                    out var rowBegin, out var colBegin, out var rowEnd, out var colEnd, out _, out _, out var dist);
                if (rowBegin.Length < 1 || colBegin.Length < 1 || rowEnd.Length < 1 || colEnd.Length < 1)
                    throw new InvalidOperationException($"直线拟合失败(algorithm={algorithm})：未返回有效结果");

                // 拟合残差 RMSE：HALCON FitLineContourXld 输出的 Dist（各点到拟合线的距离）均方根
                double rmse = 0.0;
                if (dist.Length > 0)
                {
                    var distances = dist.DArr;
                    double sum = 0.0;
                    for (int i = 0; i < distances.Length; i++)
                        sum += distances[i] * distances[i];
                    rmse = Math.Sqrt(sum / distances.Length);
                }
                return (new Point(colBegin[0].D, rowBegin[0].D), new Point(colEnd[0].D, rowEnd[0].D), rmse);
            }
            finally
            {
                contour.Dispose();
            }
        }

        /// <summary>引导线段 → RoiShape[]（模块结果"引导几何"回显，对齐 LineDetect 规范）</summary>
        private static RoiShape[]? BuildGuideGeometry(Point start, Point end)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-6) return null;

            return new[]
            {
                new RoiShape
                {
                    Geometry = new Line2d(new Point(start.X, start.Y), new Vector(dx / length, dy / length)),
                },
            };
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

        /// <summary>
        /// 两线锐角夹角（0~90°）。HALCON angle_ll 返回有向角（-π~π，逆时针 A→B，依赖点序），
        /// 取绝对值后补角化得到锐角，保持判定表"角度 0~90"语义。
        /// </summary>
        private static double ComputeAcuteAngle(Point a1, Point a2, Point b1, Point b2)
        {
            HOperatorSet.AngleLl(
                a1.Y, a1.X, a2.Y, a2.X,
                b1.Y, b1.X, b2.Y, b2.X,
                out var angle);
            double abs = Math.Abs(angle.D);                             // [0, π]
            double acute = abs > Math.PI / 2 ? Math.PI - abs : abs;     // 锐角化
            return acute * 180.0 / Math.PI;
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

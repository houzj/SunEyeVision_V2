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

namespace SunEyeVision.Tool.LineCircleMeasure
{
    /// <summary>
    /// 线-圆测量工具：元素1 引导线、元素2 引导圆各自做 2D Metrology measure（Halcon），
    /// 拟合被测直线与圆 → 计算圆心到直线正交距离（主）与直线-圆交点（辅助）→ MeasureJudge 判定 OK/NG。
    /// 引擎无关契约：公开 API 不暴露 HImage；工具引 HalAdapter（v4 B.3 首期统一）。
    /// </summary>
    [Tool(
        id: "LineCircleMeasure",
        displayName: "线-圆测量",
        Description = "线-圆测量工具：两卡尺分别沿引导线/引导圆测量，输出圆心到直线距离与交点并判定",
        Icon = "📐",
        Category = "测量工具",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class LineCircleMeasureTool : IToolPlugin<LineCircleMeasureParameters, LineCircleMeasureResults>
    {
        private readonly ToolAttribute? _attribute;
        public LineCircleMeasureTool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

        public LineCircleMeasureResults Run(LineCircleMeasureParameters parameters)
        {
            var result = new LineCircleMeasureResults
            {
                ToolName = _attribute?.DisplayName ?? "线-圆测量",
                ToolId = _attribute?.Id ?? "LineCircleMeasure",
                Timestamp = DateTime.Now,
            };

            try
            {
                // ===== 参数校验（Fail-Fast）=====
                var e1Start = new Point(parameters.E1StartX.Value, parameters.E1StartY.Value);
                var e1End = new Point(parameters.E1EndX.Value, parameters.E1EndY.Value);
                var e2Center = new Point(parameters.E2CenterX.Value, parameters.E2CenterY.Value);
                double e2Radius = parameters.E2Radius.Value;

                if ((e1End - e1Start).Length < 1e-6)
                    throw new InvalidOperationException("卡尺1 引导直线未设置或长度无效");
                if (e2Radius <= 1e-6)
                    throw new InvalidOperationException("卡尺2 引导圆未设置或半径无效");

                // ===== 输入图像 =====
                var hImage = parameters.GetHalconImage();
                if (hImage == null || !hImage.IsInitialized())
                    throw new InvalidOperationException("输入图像为空或无效");

                // ===== 位置修正（同源：交互层与 Run 一致）=====
                AffineMatrix? matrix = null;
                if (parameters.IsPositionCorrectionEnabled)
                    matrix = PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource);

                // ===== 单 metrology 模型承载"线 + 圆"两条引导 =====
                using var metrologyModel = new HMetrologyModel();
                metrologyModel.CreateMetrologyModel();
                HOperatorSet.GetImageSize(hImage, out var width, out var height);
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);
                result.ImageWidth = (int)width.L;      // 供结果显示把"无限直线延长段"裁剪到图像范围
                result.ImageHeight = (int)height.L;

                // 元素1 引导直线 → measure 对象
                var e1StartT = Transform(e1Start, matrix);
                var e1EndT = Transform(e1End, matrix);
                var e1Index = metrologyModel.AddMetrologyObjectLineMeasure(
                    e1StartT.Y, e1StartT.X, e1EndT.Y, e1EndT.X,
                    parameters.E1MeasureLength1.Value, parameters.E1MeasureLength2.Value,
                    parameters.E1Sigma.Value, parameters.E1Threshold.Value,
                    BuildLineGenParamName(), BuildLineGenParamValue(
                        parameters.E1NumMeasures.Value,
                        TransitionToHalcon(parameters.E1Transition.Value),
                        SelectToHalcon(parameters.E1Select.Value),
                        parameters.E1DistanceThreshold.Value));

                // 元素2 引导圆 → measure 对象
                // 位置修正：圆心随矩阵变换 + 半径随缩放因子缩放 + 环形卡尺阵列起始相位随旋转
                //（与交互层/结果层 BuildCircleBands 同口径，保证三层一致）
                var e2CenterT = Transform(e2Center, matrix);
                double e2RadiusT = matrix != null ? e2Radius * matrix.ScaleFactor() : e2Radius;
                double startPhase = matrix != null
                    ? PositionCorrectionResolver.DisplayAngleDeg(matrix, 0.0) * Math.PI / 180.0
                    : 0.0;
                var e2Index = metrologyModel.AddMetrologyObjectCircleMeasure(
                    e2CenterT.Y, e2CenterT.X, e2RadiusT,
                    parameters.E2MeasureLength1.Value, parameters.E2MeasureLength2.Value,
                    parameters.E2Sigma.Value, parameters.E2Threshold.Value,
                    BuildCircleGenParamName(), BuildCircleGenParamValue(
                        parameters.E2NumMeasures.Value,
                        TransitionToHalcon(parameters.E2Transition.Value),
                        SelectToHalcon(parameters.E2Select.Value),
                        parameters.E2DistanceThreshold.Value,
                        startPhase));

                // 执行测量（两引导一次 apply）
                metrologyModel.ApplyMetrologyModel(hImage);

                // 逐引导提取测点 + 全点鲁棒拟合
                var e1Points = ExtractDiagnosticPoints(metrologyModel, e1Index, TransitionToHalcon(parameters.E1Transition.Value));
                var (e1FitStart, e1FitEnd, e1Inliers, e1Rmse) = FitLineRobust(e1Points, parameters.E1DistanceThreshold.Value);

                var e2Points = ExtractDiagnosticPoints(metrologyModel, e2Index, TransitionToHalcon(parameters.E2Transition.Value));
                var (e2FitCenter, e2FitRadius, e2Inliers, e2Rmse) = FitCircleRobust(e2Points, parameters.E2DistanceThreshold.Value);

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
                result.Element1Center = MidPoint(e1FitStart, e1FitEnd);
                result.Element1Angle = NormalizeAngle360(ComputeLineAngleDeg(e1FitStart, e1FitEnd));

                result.Element2Circle = new[] { new Circle2d(e2FitCenter, e2FitRadius) };
                result.Element2Center = e2FitCenter;
                result.Element2Radius = e2FitRadius;

                // 主测量：圆心 → 元素1 **无限直线**的正交距离（HALCON distance_pl）
                HOperatorSet.DistancePl(
                    e2FitCenter.Y, e2FitCenter.X,
                    e1FitStart.Y, e1FitStart.X, e1FitEnd.Y, e1FitEnd.X,
                    out var distance);
                result.Distance = distance.D;

                // 辅助测量：直线与圆交点（HALCON intersection_line_circle）
                // 元素2 恒为整圆 → start_phi=0 / end_phi=2π / point_order="positive"（官方整圆语义）
                HOperatorSet.IntersectionLineCircle(
                    e1FitStart.Y, e1FitStart.X, e1FitEnd.Y, e1FitEnd.X,
                    e2FitCenter.Y, e2FitCenter.X, e2FitRadius,
                    0.0, 2.0 * Math.PI, "positive",
                    out var intersectionRows, out var intersectionCols);
                FillIntersections(result, e1FitStart, e1FitEnd, intersectionRows, intersectionCols);

                // ===== 输入回显（模块结果"输入/卡尺"，对齐 LineDetect 规范）=====
                result.GuideGeometry1 = BuildLineGuideGeometry(e1Start, e1End);
                result.GuideGeometry2 = BuildCircleGuideGeometry(e2Center, e2Radius);
                result.E1CaliberCount = parameters.E1NumMeasures.Value;
                result.E1CaliberWidth = parameters.E1MeasureLength1.Value * 2;   // 半长 → 全长
                result.E1CaliberLength = parameters.E1MeasureLength2.Value * 2;
                result.E1CaliberTransition = parameters.E1Transition.Value.ToString();
                result.E2CaliberCount = parameters.E2NumMeasures.Value;
                result.E2CaliberWidth = parameters.E2MeasureLength1.Value * 2;   // 径向半宽 → 全宽
                result.E2CaliberLength = parameters.E2MeasureLength2.Value * 2;  // 切向半长 → 全长
                result.E2CaliberTransition = parameters.E2Transition.Value.ToString();

                // ===== 判定（v4 C1 基座；启用项无有效值 → NG，rule-015）=====
                var judged = MeasureJudge.EvaluateAll(
                    parameters.Judgments,
                    t => t.Name switch
                    {
                        "距离" => double.IsNaN(result.Distance) ? null : result.Distance,
                        "交点X" => double.IsNaN(result.IntersectionX) ? null : result.IntersectionX,
                        "交点Y" => double.IsNaN(result.IntersectionY) ? null : result.IntersectionY,
                        "半径" => result.Element2Radius,
                        _ => null,
                    });
                result.IsPass = MeasureJudge.IsAllPass(judged);
                result.JudgmentDetails = judged.Select(j => j.Message).ToArray();

                result.FillDisplay(result.IsPass, parameters);
                result.SetSuccess();
            }
            catch (Exception ex)
            {
                parameters.LogError($"线-圆测量失败: {ex.Message}", ex);
                result.SetError($"测量失败: {ex.Message}", ex);
            }
            return result;
        }

        // ============ 交点落位（沿元素1 直线方向参数 t 排序，保证输出顺序确定） ============

        /// <summary>
        /// 把 HALCON 交点回填到结果：按"沿元素1 拟合线段方向的参数 t"升序排序，
        /// t 较小者为**主交点**（IntersectionX/Y），次者为 Intersection2X/Y。
        /// HALCON 未定义交点返回顺序，此处显式定序，避免同一场景两次运行结果漂移。
        /// </summary>
        private static void FillIntersections(
            LineCircleMeasureResults result, Point lineStart, Point lineEnd, HTuple rows, HTuple cols)
        {
            if (rows == null || cols == null || rows.Length == 0)
            {
                result.IntersectionCount = 0;
                return;
            }
            int count = rows.Length;

            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double len2 = dx * dx + dy * dy;

            var points = new List<(double T, double X, double Y)>(count);
            for (int i = 0; i < count; i++)
            {
                double y = rows[i].D;
                double x = cols[i].D;
                double t = len2 > 1e-12
                    ? ((x - lineStart.X) * dx + (y - lineStart.Y) * dy) / len2
                    : 0.0;
                points.Add((t, x, y));
            }
            points.Sort((a, b) => a.T.CompareTo(b.T));

            result.IntersectionCount = points.Count;
            result.IntersectionX = points[0].X;
            result.IntersectionY = points[0].Y;
            if (points.Count > 1)
            {
                result.Intersection2X = points[1].X;
                result.Intersection2Y = points[1].Y;
            }
        }

        // ============ 单引导 measure + 拟合（线/圆各一套） ============

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

        /// <summary>
        /// 全点鲁棒拟合直线：tukey 预拟合剔除离群 → 按 distance_threshold 划内点
        /// → 内点 regression 精化（对齐线-线测量范式）。
        /// </summary>
        private static (Point Start, Point End, Point2d[] Inliers, double Rmse) FitLineRobust(
            IReadOnlyList<Point2d> points, double distanceThreshold)
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

        /// <summary>
        /// 全点鲁棒拟合圆：geotukey 预拟合剔除离群 → 按 distance_threshold 划内点（测量点）
        /// → 内点 geometric 精化提高精度（对齐圆查找工具 FitCircleRobust）。
        /// ★ fit_circle_contour_xld 无 'tukey'（官方 List: ahuber/algebraic/atukey/geohuber/geometric/geotukey），
        ///   取几何 Tukey 'geotukey'（与直线 tukey 同族的几何加权鲁棒算法）。
        /// </summary>
        private static (Point Center, double Radius, Point2d[] Inliers, double Rmse) FitCircleRobust(
            IReadOnlyList<Point2d> points, double distanceThreshold)
        {
            if (points.Count < 3)
                throw new InvalidOperationException($"有效测量点不足({points.Count} < 3)，无法拟合圆");

            var (c0, r0) = FitCircleCore(points, "geotukey");

            var inliers = points
                .Where(p => Math.Abs(
                    Math.Sqrt((p.X - c0.X) * (p.X - c0.X) + (p.Y - c0.Y) * (p.Y - c0.Y)) - r0)
                    <= distanceThreshold)
                .ToArray();

            if (inliers.Length < 3)
                throw new InvalidOperationException(
                    $"拟合内点不足({inliers.Length} < 3)：distance_threshold={distanceThreshold:F1} 过小或引导偏差过大");

            var (center, radius) = inliers.Length == points.Count
                ? (c0, r0)
                : FitCircleCore(inliers, "geometric");

            return (center, radius, inliers, ComputeCircleRmse(inliers, center, radius));
        }

        /// <summary>
        /// 用指定算法对点集拟合圆（点集包成多边形 XLD 后调用 fit_circle_contour_xld）。
        /// </summary>
        private static (Point Center, double Radius) FitCircleCore(
            IReadOnlyList<Point2d> points, string algorithm)
        {
            var rows = new HTuple(points.Select(p => (double)p.Y).ToArray());
            var cols = new HTuple(points.Select(p => (double)p.X).ToArray());
            HOperatorSet.GenContourPolygonXld(out var contour, rows, cols);
            try
            {
                HOperatorSet.FitCircleContourXld(contour, algorithm, -1, 0.0, 0, 3, 2.0,
                    out var row, out var col, out var radius, out _, out _, out _);
                if (row.Length < 1 || col.Length < 1 || radius.Length < 1)
                    throw new InvalidOperationException($"圆拟合失败(algorithm={algorithm})：未返回有效结果");
                return (new Point(col[0].D, row[0].D), radius[0].D);
            }
            finally
            {
                contour.Dispose();
            }
        }

        /// <summary>计算点到拟合圆的均方根残差（RMSE）</summary>
        private static double ComputeCircleRmse(IReadOnlyList<Point2d> points, Point center, double radius)
        {
            if (points.Count == 0) return 0.0;
            double sumSq = 0;
            foreach (var p in points)
            {
                var dist = Math.Sqrt((p.X - center.X) * (p.X - center.X) + (p.Y - center.Y) * (p.Y - center.Y));
                var diff = dist - radius;
                sumSq += diff * diff;
            }
            return Math.Sqrt(sumSq / points.Count);
        }

        /// <summary>引导线段 → RoiShape[]（模块结果"引导几何"回显，对齐 LineDetect 规范）</summary>
        private static RoiShape[]? BuildLineGuideGeometry(Point start, Point end)
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

        /// <summary>引导圆 → RoiShape[]（模块结果"引导几何"回显）</summary>
        private static RoiShape[]? BuildCircleGuideGeometry(Point center, double radius)
        {
            if (radius <= 1e-6) return null;
            return new[]
            {
                new RoiShape { Geometry = new Circle2d(center, radius) },
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

        private static HTuple BuildLineGenParamName() => new(new[]
        {
            "num_measures", "measure_transition", "measure_select", "distance_threshold",
        });

        private static HTuple BuildLineGenParamValue(int numMeasures, string transition, string select, double distanceThreshold)
            => new(new object[] { numMeasures, transition, select, distanceThreshold });

        /// <summary>
        /// 圆 measure 的 gen-param 名（对齐圆查找：圆/弧带 start_phi/end_phi + point_order）。
        /// </summary>
        private static HTuple BuildCircleGenParamName() => new(new[]
        {
            "num_measures", "measure_transition", "measure_select",
            "start_phi", "end_phi", "point_order", "distance_threshold",
        });

        /// <summary>
        /// 圆 measure 的 gen-param 值（元素2 恒为整圆）。
        /// ★ HALCON 整圆语义：闭合圆 = start_phi 0 + end_phi 2π + point_order "positive"；
        ///   位置修正含旋转时，起始相位随矩阵旋转（start_phi 取 -相位 归一到 [0, 2π)），
        ///   与交互层/结果层 BuildCircleBands(startAngle) 同口径，保证卡尺阵列三层一致。
        /// </summary>
        private static HTuple BuildCircleGenParamValue(
            int numMeasures, string transition, string select, double distanceThreshold, double startPhase)
        {
            const double TwoPi = 2.0 * Math.PI;
            double phi = (-startPhase) % TwoPi;
            if (phi < 0) phi += TwoPi;

            return new(new object[]
            {
                numMeasures, transition, select,
                phi, phi + TwoPi, "positive",
                distanceThreshold,
            });
        }

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
            => new Views.LineCircleMeasureToolDebugControl();

        public FrameworkElement? CreateDataAnalysisControl() => null;
    }
}

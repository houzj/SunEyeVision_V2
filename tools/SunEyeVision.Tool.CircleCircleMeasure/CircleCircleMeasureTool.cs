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

namespace SunEyeVision.Tool.CircleCircleMeasure
{
    /// <summary>
    /// 圆-圆测量工具：元素1/元素2 引导圆各自做 2D Metrology measure（Halcon），
    /// 拟合两个被测圆 → 计算圆心距（主）与两圆交点/位置关系（辅助）→ MeasureJudge 判定 OK/NG。
    /// 引擎无关契约：公开 API 不暴露 HImage；工具引 HalAdapter（v4 B.3 首期统一）。
    /// </summary>
    [Tool(
        id: "CircleCircleMeasure",
        displayName: "圆-圆测量",
        Description = "圆-圆测量工具：两卡尺分别沿引导圆测量，输出圆心距、两圆交点与位置关系并判定",
        Icon = "◎",
        Category = "测量工具",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class CircleCircleMeasureTool : IToolPlugin<CircleCircleMeasureParameters, CircleCircleMeasureResults>, IToolEditorFactory
    {
        private readonly ToolAttribute? _attribute;
        public CircleCircleMeasureTool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

        public CircleCircleMeasureResults Run(CircleCircleMeasureParameters parameters)
        {
            var result = new CircleCircleMeasureResults
            {
                ToolName = _attribute?.DisplayName ?? "圆-圆测量",
                ToolId = _attribute?.Id ?? "CircleCircleMeasure",
                Timestamp = DateTime.Now,
            };

            try
            {
                // ===== 参数校验（Fail-Fast）=====
                var e1Center = new Point(parameters.E1CenterX.Value, parameters.E1CenterY.Value);
                double e1Radius = parameters.E1Radius.Value;
                var e2Center = new Point(parameters.E2CenterX.Value, parameters.E2CenterY.Value);
                double e2Radius = parameters.E2Radius.Value;

                if (e1Radius <= 1e-6)
                    throw new InvalidOperationException("卡尺1 引导圆未设置或半径无效");
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

                // ===== 单 metrology 模型承载"圆 + 圆"两条引导 =====
                using var metrologyModel = new HMetrologyModel();
                metrologyModel.CreateMetrologyModel();
                HOperatorSet.GetImageSize(hImage, out var width, out var height);
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);

                // 位置修正：圆心随矩阵变换 + 半径随缩放因子缩放 + 环形卡尺阵列起始相位随旋转
                //（与交互层/结果层 BuildCircleBands 同口径，保证三层一致）
                double startPhase = matrix != null
                    ? PositionCorrectionResolver.DisplayAngleDeg(matrix, 0.0) * Math.PI / 180.0
                    : 0.0;

                var e1CenterT = Transform(e1Center, matrix);
                double e1RadiusT = matrix != null ? e1Radius * matrix.ScaleFactor() : e1Radius;
                var e1Index = metrologyModel.AddMetrologyObjectCircleMeasure(
                    e1CenterT.Y, e1CenterT.X, e1RadiusT,
                    parameters.E1MeasureLength1.Value, parameters.E1MeasureLength2.Value,
                    parameters.E1Sigma.Value, parameters.E1Threshold.Value,
                    BuildCircleGenParamName(), BuildCircleGenParamValue(
                        parameters.E1NumMeasures.Value,
                        TransitionToHalcon(parameters.E1Transition.Value),
                        SelectToHalcon(parameters.E1Select.Value),
                        parameters.E1DistanceThreshold.Value,
                        startPhase));

                var e2CenterT = Transform(e2Center, matrix);
                double e2RadiusT = matrix != null ? e2Radius * matrix.ScaleFactor() : e2Radius;
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
                var (e1FitCenter, e1FitRadius, e1Inliers, e1Rmse) = FitCircleRobust(e1Points, parameters.E1DistanceThreshold.Value);

                var e2Points = ExtractDiagnosticPoints(metrologyModel, e2Index, TransitionToHalcon(parameters.E2Transition.Value));
                var (e2FitCenter, e2FitRadius, e2Inliers, e2Rmse) = FitCircleRobust(e2Points, parameters.E2DistanceThreshold.Value);

                result.Element1FitPoints = e1Inliers;
                result.Element2FitPoints = e2Inliers;
                result.Element1RMSE = e1Rmse;
                result.Element2RMSE = e2Rmse;
                result.Element1EdgePointCount = e1Inliers.Length;
                result.Element2EdgePointCount = e2Inliers.Length;

                // ===== 结果落位 =====
                result.Element1Circle = new[] { new Circle2d(e1FitCenter, e1FitRadius) };
                result.Element1Center = e1FitCenter;
                result.Element1Radius = e1FitRadius;

                result.Element2Circle = new[] { new Circle2d(e2FitCenter, e2FitRadius) };
                result.Element2Center = e2FitCenter;
                result.Element2Radius = e2FitRadius;

                // 主测量：两拟合圆心距离（HALCON distance_pp）
                HOperatorSet.DistancePp(
                    e1FitCenter.Y, e1FitCenter.X,
                    e2FitCenter.Y, e2FitCenter.X,
                    out var distance);
                result.Distance = distance.D;

                // 辅助测量：两圆交点（HALCON intersection_circles）
                // 两元素恒为整圆 → start_phi=0 / end_phi=2π / point_order="positive"（官方整圆语义）
                HOperatorSet.IntersectionCircles(
                    e1FitCenter.Y, e1FitCenter.X, e1FitRadius, 0.0, 2.0 * Math.PI, "positive",
                    e2FitCenter.Y, e2FitCenter.X, e2FitRadius, 0.0, 2.0 * Math.PI, "positive",
                    out var intersectionRows, out var intersectionCols, out var isOverlapping);
                result.IsOverlapping = isOverlapping.Length > 0 && isOverlapping[0].I != 0;
                FillIntersections(result, e1FitCenter, e2FitCenter, intersectionRows, intersectionCols);

                // 位置关系（由圆心距与两半径判定；重合优先，其次按 相离/外切/相交/内切/内含）
                result.Relation = DescribeRelation(
                    result.Distance, e1FitRadius, e2FitRadius, result.IntersectionCount, result.IsOverlapping);

                // ===== 输入回显（模块结果"输入/卡尺"）=====
                result.GuideGeometry1 = BuildCircleGuideGeometry(e1Center, e1Radius);
                result.GuideGeometry2 = BuildCircleGuideGeometry(e2Center, e2Radius);
                result.E1CaliberCount = parameters.E1NumMeasures.Value;
                result.E1CaliberWidth = parameters.E1MeasureLength1.Value * 2;   // 径向半宽 → 全宽
                result.E1CaliberLength = parameters.E1MeasureLength2.Value * 2;  // 切向半长 → 全长
                result.E1CaliberTransition = parameters.E1Transition.Value.ToString();
                result.E2CaliberCount = parameters.E2NumMeasures.Value;
                result.E2CaliberWidth = parameters.E2MeasureLength1.Value * 2;
                result.E2CaliberLength = parameters.E2MeasureLength2.Value * 2;
                result.E2CaliberTransition = parameters.E2Transition.Value.ToString();

                // ===== 判定（v4 C1 基座；启用项无有效值 → NG，rule-015）=====
                var judged = ResultJudgmentEvaluator.EvaluateRanges(
                    parameters.ResultPresentation.Judgments,
                    key => parameters.ResultPresentation.GetJudgment(key).Name switch
                    {
                        "圆心距" => double.IsNaN(result.Distance) ? null : result.Distance,
                        "交点X" => double.IsNaN(result.IntersectionX) ? null : result.IntersectionX,
                        "交点Y" => double.IsNaN(result.IntersectionY) ? null : result.IntersectionY,
                        "半径1" => result.Element1Radius,
                        "半径2" => result.Element2Radius,
                        _ => null,
                    });
                result.IsPass = ResultJudgmentEvaluator.IsAllPass(judged);
                result.JudgmentDetails = judged.Select(j => j.Message).ToArray();

                result.FillDisplay(result.IsPass, parameters);
                result.SetSuccess();
            }
            catch (Exception ex)
            {
                parameters.LogError($"圆-圆测量失败: {ex.Message}", ex);
                result.SetError($"测量失败: {ex.Message}", ex);
            }
            return result;
        }

        // ============ 交点落位（沿"元素1圆心 → 元素2圆心"方向参数 t 排序，保证输出顺序确定） ============

        /// <summary>
        /// 把 HALCON 交点回填到结果：按"沿两圆心连线方向的参数 t"升序排序，
        /// t 较小者为**主交点**（IntersectionX/Y），次者为 Intersection2X/Y。
        /// HALCON 未定义交点返回顺序，此处显式定序，避免同一场景两次运行结果漂移。
        /// </summary>
        private static void FillIntersections(
            CircleCircleMeasureResults result, Point center1, Point center2, HTuple rows, HTuple cols)
        {
            if (rows == null || cols == null || rows.Length == 0)
            {
                result.IntersectionCount = 0;
                return;
            }
            int count = rows.Length;

            double dx = center2.X - center1.X;
            double dy = center2.Y - center1.Y;
            double len2 = dx * dx + dy * dy;

            var points = new List<(double T, double X, double Y)>(count);
            for (int i = 0; i < count; i++)
            {
                double y = rows[i].D;
                double x = cols[i].D;
                double t = len2 > 1e-12
                    ? ((x - center1.X) * dx + (y - center1.Y) * dy) / len2
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

        /// <summary>
        /// 两圆位置关系：重合 &gt; 相离 &gt; 外切 &gt; 相交 &gt; 内切 &gt; 内含（圆心距 d 与 r1+r2 / |r1-r2| 比较）。
        /// 容差用相对尺度（半径和的 1e-6），避免不同分辨率下切点误判为相交。
        /// </summary>
        private static string DescribeRelation(
            double d, double r1, double r2, int intersectionCount, bool isOverlapping)
        {
            if (isOverlapping) return "重合";

            double sum = r1 + r2;
            double diff = Math.Abs(r1 - r2);
            double eps = Math.Max(sum, 1.0) * 1e-6;

            if (d > sum + eps) return "相离";
            if (Math.Abs(d - sum) <= eps) return "外切";
            if (d < diff - eps) return "内含";
            if (Math.Abs(d - diff) <= eps) return "内切";
            return intersectionCount > 0 ? "相交" : "相交";
        }

        // ============ 单引导 measure + 拟合 ============

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
        /// 全点鲁棒拟合圆：geotukey 预拟合剔除离群 → 按 distance_threshold 划内点（测量点）
        /// → 内点 geometric 精化提高精度（对齐圆查找工具 FitCircleRobust）。
        /// ★ fit_circle_contour_xld 无 'tukey'（官方 List: ahuber/algebraic/atukey/geohuber/geometric/geotukey），
        ///   取几何 Tukey 'geotukey'。
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

        /// <summary>引导圆 → RoiShape[]（模块结果"引导几何"回显）</summary>
        private static RoiShape[]? BuildCircleGuideGeometry(Point center, double radius)
        {
            if (radius <= 1e-6) return null;
            return new[]
            {
                new RoiShape { Geometry = new Circle2d(center, radius) },
            };
        }

        // ============ Halcon gen-param 翻译 ============

        /// <summary>
        /// 圆 measure 的 gen-param 名（对齐圆查找：圆/弧带 start_phi/end_phi + point_order）。
        /// </summary>
        private static HTuple BuildCircleGenParamName() => new(new[]
        {
            "num_measures", "measure_transition", "measure_select",
            "start_phi", "end_phi", "point_order", "distance_threshold",
        });

        /// <summary>
        /// 圆 measure 的 gen-param 值（两元素恒为整圆）。
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
            => new Views.CircleCircleMeasureToolDebugControl();

        public FrameworkElement? CreateDataAnalysisControl() => null;
    }
}

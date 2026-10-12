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
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Tool.PointCircleMeasure
{
    /// <summary>
    /// 点-圆测量工具：元素1 = 点（仅参数订阅 X/Y，无绘制入口），
    /// 元素2 = 圆（引导圆心/半径 + 径向卡尺 measure 拟合），
    /// 主测量 = 点到拟合圆心距离（HALCON distance_pp），
    /// 辅助测量 = 圆周最近点与圆周距。
    /// 引擎无关契约：公开 API 不暴露 HImage；工具引 HalAdapter（v4 B.3 首期统一）。
    /// </summary>
[Tool(
        id: "PointCircleMeasure",
        displayName: "点-圆测量",
        Description = "点-圆测量工具：点元素仅参数订阅 X/Y（无绘制入口），圆元素引导 + 卡尺测量，输出点到圆心距离、圆周距与圆周最近点",
        Icon = "point-circle", Order = 30,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Measurement,
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class PointCircleMeasureTool : IToolPlugin<PointCircleMeasureParameters, PointCircleMeasureResults>, IToolEditorFactory
    {
        private readonly ToolAttribute? _attribute;
        public PointCircleMeasureTool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

        /// <summary>
        /// 解析点1 坐标：按图形（整体绑上游点图形输出）/ 按参数（X/Y 逐参数）。
        /// 点元素无引导绘制入口，两种方式均由参数订阅承载，不产生 measure 调用。
        /// </summary>
        private static Point ResolvePoint1(PointCircleMeasureParameters parameters)
        {
            if (parameters.P1InputMode == PointInputMode.ByCoordinate)
                return new Point(parameters.P1X, parameters.P1Y);

            var pts = parameters.P1Point;
            if (pts == null || pts.Length == 0)
                throw new InvalidOperationException(
                    "点1 输入方式为「按图形」，但未绑定上游点图形输出或图形为空：请在基本参数中选择数据源，或切换为「按参数」输入 X/Y。");
            if (pts.Length > 1)
                throw new InvalidOperationException($"点1 绑定的上游点图形含 {pts.Length} 个点：多元素配对（向量化）尚未启用，请改绑单个点，或切换为「按参数」逐值输入。");
            return new Point(pts[0].X, pts[0].Y);
        }
        public PointCircleMeasureResults Run(PointCircleMeasureParameters parameters)
        {
            var result = new PointCircleMeasureResults
            {
                ToolName = _attribute?.DisplayName ?? "点-圆测量",
                ToolId = _attribute?.Id ?? "PointCircleMeasure",
                Timestamp = DateTime.Now,
            };

            try
            {
                // ===== 校验（Fail-Fast）=====
                var p1 = ResolvePoint1(parameters);
                var e2Center = new Point(parameters.E2CenterX, parameters.E2CenterY);
                double e2Radius = parameters.E2Radius;
                if (e2Radius <= 1e-6)
                    throw new InvalidOperationException("卡尺2 引导圆未设置或半径无效");

                var hImage = parameters.GetHalconImage();
                if (hImage == null || !hImage.IsInitialized())
                    throw new InvalidOperationException("输入图像为空或无效");

                // ===== 位置修正 =====
                AffineMatrix? matrix = null;
                if (parameters.IsPositionCorrectionEnabled)
                    matrix = PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource);

                // 圆心随矩阵变换 + 半径随缩放因子缩放 + 环形卡尺阵列起始相位随旋转
                //（与交互层/结果层 BuildCircleBands 同口径，保证三层一致）
                double startPhase = matrix != null
                    ? PositionCorrectionResolver.DisplayAngleDeg(matrix, 0.0) * Math.PI / 180.0
                    : 0.0;

                // ===== 单 metrology 模型 =====
                using var metrologyModel = new HMetrologyModel();
                metrologyModel.CreateMetrologyModel();
                HOperatorSet.GetImageSize(hImage, out var width, out var height);
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);

                var e2CenterT = Transform(e2Center, matrix);
                double e2RadiusT = matrix != null ? e2Radius * matrix.ScaleFactor() : e2Radius;

                var e2Index = metrologyModel.AddMetrologyObjectCircleMeasure(
                    e2CenterT.Y, e2CenterT.X, e2RadiusT,
                    parameters.E2MeasureLength1, parameters.E2MeasureLength2,
                    parameters.E2Sigma, parameters.E2Threshold,
                    BuildCircleGenParamName(), BuildCircleGenParamValue(
                        parameters.E2NumMeasures,
                        TransitionToHalcon(parameters.E2Transition),
                        SelectToHalcon(parameters.E2Select),
                        parameters.E2DistanceThreshold,
                        startPhase));

                metrologyModel.ApplyMetrologyModel(hImage);

                // 拟合
                var e2Points = ExtractDiagnosticPoints(metrologyModel, e2Index, TransitionToHalcon(parameters.E2Transition));
                var (e2FitCenter, e2FitRadius, e2Inliers, e2Rmse) = FitCircleRobust(e2Points, parameters.E2DistanceThreshold);

                result.Element2FitPoints = e2Inliers;
                result.Element2RMSE = e2Rmse;
                result.Element2EdgePointCount = e2Inliers.Length;

                // ===== 结果落位 =====
                result.Point1 = new[] { new Point2d(p1.X, p1.Y) };
                result.Element2Circle = new[] { new Circle2d(e2FitCenter, e2FitRadius) };
                result.Element2Center = new[] { new Point2d(e2FitCenter.X, e2FitCenter.Y) };
                result.Element2Radius = e2FitRadius;

                // 主测量：点到拟合圆心距离（HALCON distance_pp）
                HOperatorSet.DistancePp(
                    p1.Y, p1.X,
                    e2FitCenter.Y, e2FitCenter.X,
                    out var distance);
                result.DistanceToCenter = distance.D;

                // 辅助：圆周最近点 = 圆心 + 半径 × 单位(点1 − 圆心)；圆周距 = |圆心距 − 半径|
                double dx = p1.X - e2FitCenter.X;
                double dy = p1.Y - e2FitCenter.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len <= 1e-9)
                    throw new InvalidOperationException("点1 与拟合圆2 圆心重合，圆周最近点方向未定义");
                result.ClosestPoint = new[]
                {
                    new Point2d(
                        e2FitCenter.X + e2FitRadius * dx / len,
                        e2FitCenter.Y + e2FitRadius * dy / len),
                };
                result.DistanceToCircle = Math.Abs(result.DistanceToCenter - e2FitRadius);

                // ===== 输入回显 =====
                result.GuideGeometry2 = BuildCircleGuideGeometry(e2Center, e2Radius);
                result.E2CaliberCount = parameters.E2NumMeasures;
                result.E2CaliberWidth = parameters.E2MeasureLength1 * 2;   // 径向半宽 → 全宽
                result.E2CaliberLength = parameters.E2MeasureLength2 * 2;  // 切向半长 → 全长
                result.E2CaliberTransition = parameters.E2Transition.ToString();

                // ===== 判定 =====
                var judged = ResultJudgmentEvaluator.EvaluateRanges(
                    parameters.ResultPresentation.Judgments,
                    key => parameters.ResultPresentation.GetJudgment(key).Name switch
                    {
                        "圆心距" => double.IsNaN(result.DistanceToCenter) ? null : result.DistanceToCenter,
                        "圆周距" => double.IsNaN(result.DistanceToCircle) ? null : result.DistanceToCircle,
                        "半径" => result.Element2Radius,
                        "最近点X" => double.IsNaN(result.ClosestPointX) ? null : result.ClosestPointX,
                        "最近点Y" => double.IsNaN(result.ClosestPointY) ? null : result.ClosestPointY,
                        _ => null,
                    });
                result.IsPass = ResultJudgmentEvaluator.IsAllPass(judged);
                result.JudgmentDetails = judged.Select(j => j.Message).ToArray();

                result.FillDisplay(result.IsPass, parameters);
                result.SetSuccess();
            }
            catch (Exception ex)
            {
                parameters.LogError($"点-圆测量失败: {ex.Message}", ex);
                result.SetError($"测量失败: {ex.Message}", ex);
            }
            return result;
        }

        // ============ 测点提取 + 鲁棒圆拟合 ============

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
        /// 全点鲁棒拟合圆：geotukey 预拟合剔除离群 → 按 distance_threshold 划内点
        /// → 内点 geometric 精化提高精度（对齐圆查找/圆-圆工具 FitCircleRobust）。
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

        // ============ Halcon gen-param 翻译（圆 measure） ============

        private static HTuple BuildCircleGenParamName() => new(new[]
        {
            "num_measures", "measure_transition", "measure_select",
            "start_phi", "end_phi", "point_order", "distance_threshold",
        });

        /// <summary>
        /// 圆 measure 的 gen-param 值（元素2 恒为整圆）。
        /// ★ HALCON 整圆语义：闭合圆 = start_phi 0 + end_phi 2π + point_order "positive"；
        ///   位置修正含旋转时起始相位随矩阵旋转（start_phi 取 -相位 归一到 [0, 2π)）。
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
            => new Views.PointCircleMeasureToolDebugControl();

        public FrameworkElement? CreateDataAnalysisControl() => null;
    }
}

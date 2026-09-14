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

namespace SunEyeVision.Tool.PointLineMeasure
{
    /// <summary>
    /// 点-线测量工具：元素1 = 点（仅参数订阅 X/Y，无绘制入口），
    /// 元素2 = 线（引导 + 卡尺 measure 拟合），
    /// 主测量 = 点到拟合线距离（HALCON distance_pl），
    /// 辅助测量 = 垂足位置（点在线上的正交投影）。
    /// 引擎无关契约：公开 API 不暴露 HImage；工具引 HalAdapter（v4 B.3 首期统一）。
    /// </summary>
    [Tool(
        id: "PointLineMeasure",
        displayName: "点-线测量",
        Description = "点-线测量工具：点元素仅参数订阅 X/Y（无绘制入口），线元素引导 + 卡尺测量，输出点到线距离与垂足位置",
        Icon = "⊙|",
        Category = "测量工具",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class PointLineMeasureTool : IToolPlugin<PointLineMeasureParameters, PointLineMeasureResults>
    {
        private readonly ToolAttribute? _attribute;
        public PointLineMeasureTool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

        /// <summary>
        /// 解析点1 坐标：按图形（整体绑上游点图形输出）/ 按参数（X/Y 逐参数）。
        /// 点元素无引导绘制入口，两种方式均由参数订阅承载，不产生 measure 调用。
        /// </summary>
        private static Point ResolvePoint1(PointLineMeasureParameters parameters)
        {
            if (parameters.P1InputMode == PointInputMode.ByCoordinate)
                return new Point(parameters.P1X.Value, parameters.P1Y.Value);

            var config = parameters.P1Point.BindingConfig;
            if (config == null || config.BindingType != BindingType.Binding || string.IsNullOrEmpty(config.SourceNodeId))
                throw new InvalidOperationException(
                    "点1 输入方式为「按图形」，但未绑定上游点图形输出（词目 Point）：请在基本参数中选择数据源，或切换为「按参数」输入 X/Y。");

            // 数组口子（决策 4）：单元素即"一次绑定即得 X/Y"；
            // 多元素配对属向量化（6d），当前**显式报错**（不静默取首元素、不静默用默认值，rule-015）。
            var pts = parameters.P1Point.Value;
            if (pts == null || pts.Length == 0)
                throw new InvalidOperationException(
                    "点1 绑定的上游点图形为空（0 个点）：请在基本参数中重新选择数据源。");

            if (pts.Length > 1)
                throw new InvalidOperationException(
                    $"点1 绑定的上游点图形含 {pts.Length} 个点：多元素配对（向量化）尚未启用，"
                    + "请改绑单个点，或切换为「按参数」逐值输入。");

            return new Point(pts[0].X, pts[0].Y);
        }

        public PointLineMeasureResults Run(PointLineMeasureParameters parameters)
        {
            var result = new PointLineMeasureResults
            {
                ToolName = _attribute?.DisplayName ?? "点-线测量",
                ToolId = _attribute?.Id ?? "PointLineMeasure",
                Timestamp = DateTime.Now,
            };

            try
            {
                // ===== 校验（Fail-Fast）=====
                var p1 = ResolvePoint1(parameters);
                var lineStart = new Point(parameters.E2StartX.Value, parameters.E2StartY.Value);
                var lineEnd = new Point(parameters.E2EndX.Value, parameters.E2EndY.Value);
                if ((lineEnd - lineStart).Length <= 1e-6)
                    throw new InvalidOperationException("线2 引导线未设置或长度无效");

                var hImage = parameters.GetHalconImage();
                if (hImage == null || !hImage.IsInitialized())
                    throw new InvalidOperationException("输入图像为空或无效");

                // ===== 位置修正 =====
                AffineMatrix? matrix = null;
                if (parameters.IsPositionCorrectionEnabled)
                    matrix = PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource);

                // ===== 单 metrology 模型 =====
                using var metrologyModel = new HMetrologyModel();
                metrologyModel.CreateMetrologyModel();
                HOperatorSet.GetImageSize(hImage, out var width, out var height);
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);

                var lineStartT = Transform(lineStart, matrix);
                var lineEndT = Transform(lineEnd, matrix);

                var e2Index = metrologyModel.AddMetrologyObjectLineMeasure(
                    lineStartT.Y, lineStartT.X, lineEndT.Y, lineEndT.X,
                    parameters.E2MeasureLength1.Value, parameters.E2MeasureLength2.Value,
                    parameters.E2Sigma.Value, parameters.E2Threshold.Value,
                    BuildLineGenParamName(), BuildLineGenParamValue(
                        parameters.E2NumMeasures.Value,
                        TransitionToHalcon(parameters.E2Transition.Value),
                        SelectToHalcon(parameters.E2Select.Value),
                        parameters.E2DistanceThreshold.Value));

                metrologyModel.ApplyMetrologyModel(hImage);

                // 拟合
                var e2Points = ExtractDiagnosticPoints(metrologyModel, e2Index, TransitionToHalcon(parameters.E2Transition.Value));
                var (e2Start, e2End, e2Inliers, e2Rmse) = FitLineRobust(e2Points, parameters.E2DistanceThreshold.Value);

                result.Element2FitPoints = e2Inliers;
                result.Element2RMSE = e2Rmse;
                result.Element2EdgePointCount = e2Inliers.Length;

                // ===== 结果落位 =====
                result.Point1 = new[] { new Point2d(p1.X, p1.Y) };
                result.Element2Line = new[] { Line2d.FromTwoPoints(e2Start, e2End) };
                result.Element2StartPoint = e2Start;
                result.Element2EndPoint = e2End;

                // 主测量：点到拟合线距离（HALCON distance_pl）
                HOperatorSet.DistancePl(
                    p1.Y, p1.X,
                    e2Start.Y, e2Start.X,
                    e2End.Y, e2End.X,
                    out var distance);
                result.Distance = distance.D;

                // 垂足 = 点在拟合线上的正交投影
                double dx = e2End.X - e2Start.X;
                double dy = e2End.Y - e2Start.Y;
                double len2 = dx * dx + dy * dy;
                double t = len2 > 1e-12
                    ? ((p1.X - e2Start.X) * dx + (p1.Y - e2Start.Y) * dy) / len2
                    : 0.0;
                result.PerpendicularFoot = new Point(
                    e2Start.X + t * dx,
                    e2Start.Y + t * dy);
                result.PerpendicularT = t;

                // ===== 输入回显 =====
                result.GuideGeometry2 = BuildLineGuideGeometry(lineStart, lineEnd);
                result.E2CaliberCount = parameters.E2NumMeasures.Value;
                result.E2CaliberWidth = parameters.E2MeasureLength1.Value * 2;
                result.E2CaliberLength = parameters.E2MeasureLength2.Value * 2;
                result.E2CaliberTransition = parameters.E2Transition.Value.ToString();

                // ===== 判定 =====
                var judged = MeasureJudge.EvaluateAll(
                    parameters.Judgments,
                    key => key.Name switch
                    {
                        "距离" => double.IsNaN(result.Distance) ? null : result.Distance,
                        "垂足X" => double.IsNaN(result.PerpendicularFoot.X) ? null : result.PerpendicularFoot.X,
                        "垂足Y" => double.IsNaN(result.PerpendicularFoot.Y) ? null : result.PerpendicularFoot.Y,
                        _ => null,
                    });
                result.IsPass = MeasureJudge.IsAllPass(judged);
                result.JudgmentDetails = judged.Select(j => j.Message).ToArray();

                result.FillDisplay(result.IsPass, parameters);
                result.SetSuccess();
            }
            catch (Exception ex)
            {
                parameters.LogError($"点-线测量失败: {ex.Message}", ex);
                result.SetError($"测量失败: {ex.Message}", ex);
            }
            return result;
        }

        // ============ 测点提取 + 鲁棒线拟合 ============

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
        /// 全点鲁棒拟合线：tukey 预拟合剔除离群 → 按 distance_threshold 划内点 →
        /// 内点足够时用 regression 精化提高精度（与 FitCircleRobust 思路一致）。
        /// </summary>
        private static (Point Start, Point End, Point2d[] Inliers, double Rmse) FitLineRobust(
            IReadOnlyList<Point2d> points, double distanceThreshold)
        {
            if (points.Count < 2)
                throw new InvalidOperationException($"有效测量点不足({points.Count} < 2)，无法拟合线");

            var (s0, e0) = FitLineCore(points, "tukey");
            double dx = e0.X - s0.X, dy = e0.Y - s0.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 < 1e-12)
                throw new InvalidOperationException("线拟合退化（起点=终点）");

            var inliers = points.Where(p =>
            {
                double tt = ((p.X - s0.X) * dx + (p.Y - s0.Y) * dy) / len2;
                double projX = s0.X + tt * dx, projY = s0.Y + tt * dy;
                double dist = Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
                return dist <= distanceThreshold;
            }).ToArray();

            if (inliers.Length < 2)
                throw new InvalidOperationException(
                    $"拟合内点不足({inliers.Length} < 2)：distance_threshold={distanceThreshold:F1} 过小或引导偏差过大");

            var (finalStart, finalEnd) = inliers.Length == points.Count
                ? (s0, e0)
                : FitLineCore(inliers, "regression");

            return (finalStart, finalEnd, inliers, ComputeLineRmse(inliers, finalStart, finalEnd));
        }

        private static (Point Start, Point End) FitLineCore(IReadOnlyList<Point2d> points, string algorithm)
        {
            var rows = new HTuple(points.Select(p => (double)p.Y).ToArray());
            var cols = new HTuple(points.Select(p => (double)p.X).ToArray());
            HOperatorSet.GenContourPolygonXld(out var contour, rows, cols);
            try
            {
                HOperatorSet.FitLineContourXld(contour, algorithm, -1, 0.0, 3, 2.0,
                    out var rowBegin, out var colBegin, out var rowEnd, out var colEnd, out _, out _, out _);
                if (rowBegin.Length < 1 || colBegin.Length < 1 || rowEnd.Length < 1 || colEnd.Length < 1)
                    throw new InvalidOperationException($"线拟合失败(algorithm={algorithm})：未返回有效结果");
                return (new Point(colBegin[0].D, rowBegin[0].D), new Point(colEnd[0].D, rowEnd[0].D));
            }
            finally
            {
                contour.Dispose();
            }
        }

        private static double ComputeLineRmse(IReadOnlyList<Point2d> points, Point start, Point end)
        {
            if (points.Count == 0) return 0.0;
            double dx = end.X - start.X, dy = end.Y - start.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 < 1e-12) return 0.0;
            double sumSq = 0;
            foreach (var p in points)
            {
                double tt = ((p.X - start.X) * dx + (p.Y - start.Y) * dy) / len2;
                double projX = start.X + tt * dx, projY = start.Y + tt * dy;
                double dist = Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
                sumSq += dist * dist;
            }
            return Math.Sqrt(sumSq / points.Count);
        }

        /// <summary>引导线 → RoiShape[]（模块结果"引导几何"回显）</summary>
        private static RoiShape[]? BuildLineGuideGeometry(Point start, Point end)
        {
            if ((end - start).Length <= 1e-6) return null;
            return new[]
            {
                new RoiShape { Geometry = Line2d.FromTwoPoints(start, end) },
            };
        }

        // ============ gen-param 翻译（线 measure） ============

        private static HTuple BuildLineGenParamName() => new(new[]
        {
            "num_measures", "measure_transition", "measure_select", "distance_threshold",
        });

        private static HTuple BuildLineGenParamValue(int numMeasures, string transition, string select, double distanceThreshold)
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
            => new Views.PointLineMeasureToolDebugControl();

        public FrameworkElement? CreateDataAnalysisControl() => null;
    }
}
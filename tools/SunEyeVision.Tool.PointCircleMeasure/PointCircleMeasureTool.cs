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
            return parameters.P1InputMode switch
            {
                PointInputMode.ByCoordinate => PointMeasurementGeometry.RequireFinite(new Point(parameters.P1X, parameters.P1Y), "点1"),
                PointInputMode.ByShape => PointMeasurementGeometry.ResolveSinglePoint(parameters.P1Point, "点1"),
                _ => throw new InvalidOperationException($"点1 输入方式无效：{(int)parameters.P1InputMode}。")
            };
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
                result.CaptureExecutionParameters(parameters);
                // ===== 校验（Fail-Fast）=====
                var p1 = ResolvePoint1(parameters);
                var e2Center = PointMeasurementGeometry.RequireFinite(new Point(parameters.E2CenterX, parameters.E2CenterY), "圆2引导圆心");
                double e2Radius = PointMeasurementGeometry.RequireFinite(parameters.E2Radius, "圆2引导半径");
                if (e2Radius <= 1e-6)
                    throw new InvalidOperationException("卡尺2 引导圆未设置或半径无效");

                var hImage = parameters.GetHalconImage();
                if (hImage == null || !hImage.IsInitialized())
                    throw new InvalidOperationException("输入图像为空或无效");

                // ===== 位置修正 =====
                AffineMatrix? matrix = null;
                if (parameters.IsPositionCorrectionEnabled)
                    matrix = PositionCorrectionResolver.Resolve(parameters);

                // 圆心随矩阵变换、半径随缩放；整圆采用HALCON标准闭合参数
                //（与交互层/结果层 BuildCircleBands 同口径，保证三层一致）

                // ===== 单 metrology 模型 =====
                using var metrologyModel = new HMetrologyModel();
                metrologyModel.CreateMetrologyModel();
                HOperatorSet.GetImageSize(hImage, out var width, out var height);
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);

                var e2CenterT = PointMeasurementGeometry.RequireFinite(Transform(e2Center, matrix), "修正后圆2圆心");
                double e2RadiusT = PointMeasurementGeometry.RequireFinite(matrix != null ? e2Radius * matrix.ScaleFactor() : e2Radius, "修正后圆2半径");
                if (e2RadiusT <= 1e-6) throw new InvalidOperationException("修正后圆2半径无效或退化。");

                var e2Index = metrologyModel.AddMetrologyObjectCircleMeasure(
                    e2CenterT.Y, e2CenterT.X, e2RadiusT,
                    parameters.E2MeasureLength1, parameters.E2MeasureLength2,
                    parameters.E2Sigma, parameters.E2Threshold,
                    BuildCircleGenParamName(), BuildCircleGenParamValue(
                        parameters.E2NumMeasures,
                        TransitionToHalcon(parameters.E2Transition),
                        SelectToHalcon(parameters.E2Select),
                        parameters.E2DistanceThreshold));

                metrologyModel.ApplyMetrologyModel(hImage);

                // 拟合
                var e2Points = MeasuredLineFitting.Capture(metrologyModel, e2Index, TransitionToHalcon(parameters.E2Transition), result.FitDiagnostics2, "圆2");
                var (e2FitCenter, e2FitRadius, e2Inliers, e2Rmse) = MeasuredCircleFitting.Fit(e2Points, parameters.E2DistanceThreshold, result.FitDiagnostics2, "圆2");

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
                result.DistanceToCenter = PointMeasurementGeometry.RequireFinite(distance.D, "点到圆心距离");

                // 辅助：圆周最近点 = 圆心 + 半径 × 单位(点1 − 圆心)；圆周距 = |圆心距 − 半径|
                double dx = PointMeasurementGeometry.RequireFinite(p1.X - e2FitCenter.X, "点到圆心ΔX");
                double dy = PointMeasurementGeometry.RequireFinite(p1.Y - e2FitCenter.Y, "点到圆心ΔY");
                double len = result.DistanceToCenter;
                if (len <= 1e-9)
                    throw new InvalidOperationException("点1 与拟合圆2 圆心重合，圆周最近点方向未定义");
                var closest = PointMeasurementGeometry.RequireFinite(new Point(
                    e2FitCenter.X + e2FitRadius * (dx / len),
                    e2FitCenter.Y + e2FitRadius * (dy / len)), "圆周最近点");
                result.ClosestPoint = new[]
                {
                    new Point2d(closest.X, closest.Y),
                };
                result.DistanceToCircle = PointMeasurementGeometry.RequireFinite(Math.Abs(result.DistanceToCenter - e2FitRadius), "点到圆周距离");

                // ===== 输入回显 =====
                result.GuideGeometry2 = BuildCircleGuideGeometry(e2Center, e2Radius);
                result.E2CaliberCount = parameters.E2NumMeasures;
                result.E2CaliberWidth = parameters.E2MeasureLength1 * 2;   // 径向半宽 → 全宽
                result.E2CaliberLength = parameters.E2MeasureLength2 * 2;  // 切向半长 → 全长
                result.E2CaliberTransition = parameters.E2Transition.ToString();

                // ===== 判定 =====
                var judged = ResultJudgmentEvaluator.EvaluateRanges(
                    parameters.ResultPresentation.Judgments,
                    key => key switch
                    {
                        "center-distance" => result.DistanceToCenter,
                        "circumference-distance" => result.DistanceToCircle,
                        "radius" => result.Element2Radius,
                        "closest-x" => result.ClosestPointX,
                        "closest-y" => result.ClosestPointY,
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
                parameters.LogWarning(result.FitDiagnostics2.Summary);
                try { result.FillFailureDisplay(parameters); }
                catch (Exception displayEx) { parameters.LogWarning($"失败诊断绘制异常：{displayEx.Message}"); }
            }
            return result;
        }

        // ============ 测点提取 + 鲁棒圆拟合 ============

        private static Point Transform(Point p, AffineMatrix? matrix)
            => matrix == null ? p : matrix.TransformWpfPoint(p);


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
        ///   整圆仅修正圆心与半径，测量带从当前图像0°等分；圆弧另按有向角度修正。
        /// </summary>
        private static HTuple BuildCircleGenParamValue(
            int numMeasures, string transition, string select, double distanceThreshold)
        {
            var (startPhi, endPhi, pointOrder) = HalconArcAdapter.FromFullCircle();

            return new(new object[]
            {
                numMeasures, transition, select,
                startPhi, endPhi, pointOrder,
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

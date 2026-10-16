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
        Icon = "point-line", Order = 20,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Measurement,
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class PointLineMeasureTool : IToolPlugin<PointLineMeasureParameters, PointLineMeasureResults>, IToolEditorFactory
    {
        private readonly ToolAttribute? _attribute;
        public PointLineMeasureTool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

        /// <summary>
        /// 解析点1 坐标：按图形（整体绑上游点图形输出）/ 按参数（X/Y 逐参数）。
        /// 点元素无引导绘制入口，两种方式均由参数订阅承载，不产生 measure 调用。
        /// </summary>
        private static Point ResolvePoint1(PointLineMeasureParameters parameters)
        {
            return parameters.P1InputMode switch
            {
                PointInputMode.ByCoordinate => PointMeasurementGeometry.RequireFinite(new Point(parameters.P1X, parameters.P1Y), "点1"),
                PointInputMode.ByShape => PointMeasurementGeometry.ResolveSinglePoint(parameters.P1Point, "点1"),
                _ => throw new InvalidOperationException($"点1 输入方式无效：{(int)parameters.P1InputMode}。")
            };
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
                result.CaptureExecutionParameters(parameters);
                // ===== 校验（Fail-Fast）=====
                var p1 = ResolvePoint1(parameters);
                var lineStart = PointMeasurementGeometry.RequireFinite(new Point(parameters.E2StartX, parameters.E2StartY), "线2引导起点");
                var lineEnd = PointMeasurementGeometry.RequireFinite(new Point(parameters.E2EndX, parameters.E2EndY), "线2引导终点");
                if ((lineEnd - lineStart).Length <= 1e-6)
                    throw new InvalidOperationException("线2 引导线未设置或长度无效");

                var hImage = parameters.GetHalconImage();
                if (hImage == null || !hImage.IsInitialized())
                    throw new InvalidOperationException("输入图像为空或无效");

                // ===== 位置修正 =====
                AffineMatrix? matrix = null;
                if (parameters.IsPositionCorrectionEnabled)
                    matrix = PositionCorrectionResolver.Resolve(parameters);

                // ===== 单 metrology 模型 =====
                using var metrologyModel = new HMetrologyModel();
                metrologyModel.CreateMetrologyModel();
                HOperatorSet.GetImageSize(hImage, out var width, out var height);
                result.ImageWidth = (int)width.L;
                result.ImageHeight = (int)height.L;
                metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);

                var lineStartT = PointMeasurementGeometry.RequireFinite(Transform(lineStart, matrix), "修正后线2起点");
                var lineEndT = PointMeasurementGeometry.RequireFinite(Transform(lineEnd, matrix), "修正后线2终点");

                var e2Index = metrologyModel.AddMetrologyObjectLineMeasure(
                    lineStartT.Y, lineStartT.X, lineEndT.Y, lineEndT.X,
                    parameters.E2MeasureLength1, parameters.E2MeasureLength2,
                    parameters.E2Sigma, parameters.E2Threshold,
                    BuildLineGenParamName(), BuildLineGenParamValue(
                        parameters.E2NumMeasures,
                        TransitionToHalcon(parameters.E2Transition),
                        SelectToHalcon(parameters.E2Select),
                        parameters.E2DistanceThreshold));

                metrologyModel.ApplyMetrologyModel(hImage);

                // 拟合
                var e2Points = MeasuredLineFitting.Capture(metrologyModel, e2Index, TransitionToHalcon(parameters.E2Transition), result.FitDiagnostics2, "元素2");
                var (e2Start, e2End, e2Inliers, e2Rmse) = MeasuredLineFitting.Fit(e2Points, parameters.E2DistanceThreshold, lineStartT, lineEndT, result.FitDiagnostics2, "直线2");

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
                result.Distance = PointMeasurementGeometry.RequireFinite(distance.D, "点线距离");

                // 垂足 = 点在拟合线上的正交投影
                var (foot, parameter) = PointMeasurementGeometry.ProjectOnLine(p1, e2Start, e2End);
                result.PerpendicularFoot = foot;
                result.PerpendicularT = parameter;

                // ===== 输入回显 =====
                result.GuideGeometry2 = BuildLineGuideGeometry(lineStart, lineEnd);
                result.E2CaliberCount = parameters.E2NumMeasures;
                result.E2CaliberWidth = parameters.E2MeasureLength1 * 2;
                result.E2CaliberLength = parameters.E2MeasureLength2 * 2;
                result.E2CaliberTransition = parameters.E2Transition.ToString();

                // ===== 判定 =====
                var judged = ResultJudgmentEvaluator.EvaluateRanges(
                    parameters.ResultPresentation.Judgments,
                    key => key switch
                    {
                        "distance" => double.IsNaN(result.Distance) ? null : result.Distance,
                        "foot-x" => double.IsNaN(result.PerpendicularFoot.X) ? null : result.PerpendicularFoot.X,
                        "foot-y" => double.IsNaN(result.PerpendicularFoot.Y) ? null : result.PerpendicularFoot.Y,
                        _ => null,
                    });
                result.IsPass = ResultJudgmentEvaluator.IsAllPass(judged);
                result.JudgmentDetails = judged.Select(j => j.Message).ToArray();

                result.FillDisplay(result.IsPass, parameters);
                result.SetSuccess();
            }
            catch (Exception ex)
            {
                parameters.LogError($"点-线测量失败: {ex.Message}", ex);
                result.SetError($"测量失败: {ex.Message}", ex);
                parameters.LogWarning(result.FitDiagnostics2.Summary);
                try { result.FillFailureDisplay(parameters); }
                catch (Exception displayEx) { parameters.LogWarning($"失败诊断绘制异常：{displayEx.Message}"); }
            }
            return result;
        }

        // ============ 测点提取 + 鲁棒线拟合 ============

        private static Point Transform(Point p, AffineMatrix? matrix)
            => matrix == null ? p : matrix.TransformWpfPoint(p);


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

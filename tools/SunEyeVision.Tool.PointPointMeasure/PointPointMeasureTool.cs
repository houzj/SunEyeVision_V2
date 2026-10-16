using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using ToolExecutionContext = SunEyeVision.Plugin.SDK.Execution.Parameters.ExecutionContext;

namespace SunEyeVision.Tool.PointPointMeasure
{
    /// <summary>
    /// 点-点测量工具：两个元素均为点（仅参数订阅，无绘制入口），
    /// 主测量 = 两点距离（纯几何计算，无 measure 引擎调用），
    /// 辅助测量 = ΔX / ΔY / 连线角度。
    /// 引擎无关契约：公开 API 不暴露 HImage；本工具不调用任何引擎算子，故不引 HalAdapter。
    /// </summary>
[Tool(
        id: "PointPointMeasure",
        displayName: "点-点测量",
        Description = "点-点测量工具：两个点元素均仅参数订阅（按图形/按参数），输出两点距离、ΔX、ΔY 与连线角度",
        Icon = "point-point", Order = 10,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Measurement,
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class PointPointMeasureTool : IToolPlugin<PointPointMeasureParameters, PointPointMeasureResults>, IAsyncToolPlugin, IToolEditorFactory
    {
        private readonly ToolAttribute? _attribute;
        public PointPointMeasureTool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

        /// <summary>
        /// 解析点坐标：按图形（整体绑上游点图形输出）/ 按参数（X/Y 逐参数）。
        /// 点元素无引导绘制入口，两种方式均由参数订阅承载，不产生 measure 调用。
        /// </summary>
        private static Point ResolvePoint(
            PointInputMode mode,
            Point2d[]? shapePoint,
            double x,
            double y,
            string label)
        {
            return mode switch
            {
                PointInputMode.ByCoordinate => PointMeasurementGeometry.RequireFinite(new Point(x, y), label),
                PointInputMode.ByShape => PointMeasurementGeometry.ResolveSinglePoint(shapePoint, label),
                _ => throw new InvalidOperationException($"{label} 输入方式无效：{(int)mode}，请重新选择按图形或按参数。")
            };
        }
        public PointPointMeasureResults Run(PointPointMeasureParameters parameters)
            => ExecuteCore(parameters, parameters.ResolvedInputImage, parameters.Context);

        public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        {
            var result = ExecuteCore(
                (PointPointMeasureParameters)request.Parameters,
                request.Inputs.Image,
                request.Context);
            return ValueTask.FromResult<ToolResults>(result);
        }

        private PointPointMeasureResults ExecuteCore(
            PointPointMeasureParameters parameters,
            IVisionImage? inputImage,
            ToolExecutionContext? context)
        {
            var result = new PointPointMeasureResults
            {
                ToolName = _attribute?.DisplayName ?? "点-点测量",
                ToolId = _attribute?.Id ?? "PointPointMeasure",
                Timestamp = DateTime.Now,
            };

            var stage = "输入图像校验";
            try
            {
                // ===== 校验（Fail-Fast）=====
                if (inputImage == null || inputImage.IsEmpty)
                    throw new InvalidOperationException("输入图像为空或无效");

                stage = "点1解析";
                var p1 = ResolvePoint(parameters.P1InputMode, parameters.P1Point, parameters.P1X, parameters.P1Y, "点1");
                stage = "点2解析";
                var p2 = ResolvePoint(parameters.P2InputMode, parameters.P2Point, parameters.P2X, parameters.P2Y, "点2");

                // ★ 不做位置修正：两个点元素均为上游订阅结果（上游已完成其自身修正），
                //   坐标已是最终位置，此处再叠加修正矩阵即为重复修正。

                // ===== 主测量：两点距离（纯几何计算）=====
                stage = "几何计算";
                var (distance, dx, dy, angleDeg) = PointMeasurementGeometry.MeasurePair(p1, p2);

                result.Point1 = new[] { new Point2d(p1.X, p1.Y) };
                result.Point2 = new[] { new Point2d(p2.X, p2.Y) };
                result.Distance = distance;
                result.DeltaX = dx;
                result.DeltaY = dy;
                result.AngleDeg = angleDeg;

                // ===== 判定 =====
                stage = "结果判定";
                var judged = ResultJudgmentEvaluator.EvaluateRanges(
                    parameters.ResultPresentation.Judgments,
                    key => key switch
                    {
                        "point-distance" => distance,
                        "delta-x" => dx,
                        "delta-y" => dy,
                        "line-angle" => angleDeg,
                        _ => null,
                    });
                result.IsPass = ResultJudgmentEvaluator.IsAllPass(judged);
                result.JudgmentDetails = judged.Select(j => j.Message).ToArray();

                stage = "结果显示";
                result.FillDisplay(result.IsPass, parameters);
                result.SetSuccess();
            }
            catch (Exception ex)
            {
                context?.Logger?.Error(
                    $"点-点测量失败（{stage}）: {ex.Message}",
                    context.GetSourceName(),
                    ex);
                result.SetError($"测量失败（{stage}）: {ex.Message}", ex);
            }
            return result;
        }

        // ============ 控件创建 ============

        public FrameworkElement? CreateDebugControl(IToolPlugin tool)
            => new Views.PointPointMeasureToolDebugControl();

        public FrameworkElement? CreateDataAnalysisControl() => null;
    }
}

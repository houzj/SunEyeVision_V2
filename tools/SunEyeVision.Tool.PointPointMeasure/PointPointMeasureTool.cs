using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;

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
        Icon = "⊙⊙",
        Category = "测量工具",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class PointPointMeasureTool : IToolPlugin<PointPointMeasureParameters, PointPointMeasureResults>
    {
        private readonly ToolAttribute? _attribute;
        public PointPointMeasureTool() => _attribute = GetType().GetCustomAttribute<ToolAttribute>();

        /// <summary>
        /// 解析点坐标：按图形（整体绑上游点图形输出）/ 按参数（X/Y 逐参数）。
        /// 点元素无引导绘制入口，两种方式均由参数订阅承载，不产生 measure 调用。
        /// </summary>
        private static Point ResolvePoint(
            PointInputMode mode,
            ParamValue<Point2d[]> shapePoint,
            ParamValue<double> x,
            ParamValue<double> y,
            string label)
        {
            if (mode == PointInputMode.ByCoordinate)
                return new Point(x.Value, y.Value);

            var config = shapePoint.BindingConfig;
            if (config == null || config.BindingType != BindingType.Binding || string.IsNullOrEmpty(config.SourceNodeId))
                throw new InvalidOperationException(
                    $"{label} 输入方式为「按图形」，但未绑定上游点图形输出（词目 Point）：请在基本参数中选择数据源，或切换为「按参数」输入 X/Y。");

            // 数组口子（决策 4）：单元素即"一次绑定即得 X/Y"；
            // 多元素配对属向量化（6d），当前**显式报错**——不静默取首元素、不静默用默认值（rule-015 报错优先）。
            var pts = shapePoint.Value;
            if (pts == null || pts.Length == 0)
                throw new InvalidOperationException(
                    $"{label} 绑定的上游点图形为空（0 个点）：请在基本参数中重新选择数据源。");

            if (pts.Length > 1)
                throw new InvalidOperationException(
                    $"{label} 绑定的上游点图形含 {pts.Length} 个点：多元素配对（向量化）尚未启用，"
                    + "请改绑单个点，或切换为「按参数」逐值输入。");

            return new Point(pts[0].X, pts[0].Y);
        }

        public PointPointMeasureResults Run(PointPointMeasureParameters parameters)
        {
            var result = new PointPointMeasureResults
            {
                ToolName = _attribute?.DisplayName ?? "点-点测量",
                ToolId = _attribute?.Id ?? "PointPointMeasure",
                Timestamp = DateTime.Now,
            };

            try
            {
                // ===== 校验（Fail-Fast）=====
                if (parameters.ResolvedInputImage == null || parameters.ResolvedInputImage.IsEmpty)
                    throw new InvalidOperationException("输入图像为空或无效");

                var p1 = ResolvePoint(parameters.P1InputMode, parameters.P1Point, parameters.P1X, parameters.P1Y, "点1");
                var p2 = ResolvePoint(parameters.P2InputMode, parameters.P2Point, parameters.P2X, parameters.P2Y, "点2");

                // ★ 不做位置修正：两个点元素均为上游订阅结果（上游已完成其自身修正），
                //   坐标已是最终位置，此处再叠加修正矩阵即为重复修正。

                // ===== 主测量：两点距离（纯几何计算）=====
                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;

                result.Point1 = new[] { new Point2d(p1.X, p1.Y) };
                result.Point2 = new[] { new Point2d(p2.X, p2.Y) };
                result.Distance = distance;
                result.DeltaX = dx;
                result.DeltaY = dy;
                result.AngleDeg = angleDeg;

                // ===== 判定 =====
                var judged = MeasureJudge.EvaluateAll(
                    parameters.Judgments,
                    key => key.Name switch
                    {
                        "点距" => distance,
                        "ΔX" => dx,
                        "ΔY" => dy,
                        "连线角度" => angleDeg,
                        _ => null,
                    });
                result.IsPass = MeasureJudge.IsAllPass(judged);
                result.JudgmentDetails = judged.Select(j => j.Message).ToArray();

                result.FillDisplay(result.IsPass, parameters);
                result.SetSuccess();
            }
            catch (Exception ex)
            {
                parameters.LogError($"点-点测量失败: {ex.Message}", ex);
                result.SetError($"测量失败: {ex.Message}", ex);
            }
            return result;
        }

        // ============ 控件创建 ============

        public FrameworkElement? CreateDebugControl(IToolPlugin tool)
            => new Views.PointPointMeasureToolDebugControl();

        public FrameworkElement? CreateDataAnalysisControl() => null;
    }
}

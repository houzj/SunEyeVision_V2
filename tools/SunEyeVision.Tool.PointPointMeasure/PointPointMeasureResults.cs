using System;
using System.Collections.Generic;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Tool.PointPointMeasure
{
    /// <summary>
    /// 点-点测量结果。
    /// 主测量 = 两点距离；辅助 = ΔX / ΔY / 连线角度。
    /// 两个元素均为点（无引导几何、无 measure 拟合），故结果不含拟合点集。
    /// </summary>
    /// <remarks>
    /// 元素点以契约类型 <see cref="Point2d"/> 数组输出（词目 Point），供下游"按图形"整体绑定；
    /// X/Y 拆解属性保留，供"按参数"逐值绑定。
    /// </remarks>
    // P0.6-1 能力声明：图像输入 + 位置修正 + 测量引导（**不含区域输入**——测量工具不使用检测区/屏蔽区）
    [ToolCapability(ToolCapabilities.ImageSource | ToolCapabilities.PositionCorrection
        | ToolCapabilities.MeasureGuide)]
    public class PointPointMeasureResults : ToolResults
    {
        // ===== 元素结果 =====
        /// <summary>元素1：点（订阅解析所得，坐标为最终位置）。契约输出（词目 Point）</summary>
        public Point2d[] Point1 { get; set; } = Array.Empty<Point2d>();

        /// <summary>元素2：点（订阅解析所得，坐标为最终位置）。契约输出（词目 Point）</summary>
        public Point2d[] Point2 { get; set; } = Array.Empty<Point2d>();

        // ===== 几何量 =====
        /// <summary>主测量：两点距离（px）</summary>
        public double Distance { get; set; }

        /// <summary>辅助：X 分量（点2 − 点1）</summary>
        public double DeltaX { get; set; }

        /// <summary>辅助：Y 分量（点2 − 点1）</summary>
        public double DeltaY { get; set; }

        /// <summary>辅助：连线角度（度，点1 → 点2，值域 −180~180）</summary>
        public double AngleDeg { get; set; }

        // 词表暴露：整体点为 Point2d[]（词目 Point）；另拆 X/Y double 供"按参数"绑定
        public double Point1X => Point1.Length > 0 ? Point1[0].X : 0;
        public double Point1Y => Point1.Length > 0 ? Point1[0].Y : 0;
        public double Point2X => Point2.Length > 0 ? Point2[0].X : 0;
        public double Point2Y => Point2.Length > 0 ? Point2[0].Y : 0;

        /// <summary>判定明细（逐项"名称: 实测值 ∈ [min,max] 通过/越界"）</summary>
        public string[] JudgmentDetails { get; set; } = Array.Empty<string>();

        /// <summary>几何量摘要（结果树展示）</summary>
        public string GeometryDescription => $"点距 {Distance:F3}px (ΔX {DeltaX:F3}, ΔY {DeltaY:F3})";

        /// <summary>填充结果显示：两点标记 + 点距连线 + 分量辅助线 + 文本。</summary>
        public void FillDisplay(bool isOk, PointPointMeasureParameters parameters)
        {
            _display.Clear();

            var p1 = ToWpfPoint(Point1);
            var p2 = ToWpfPoint(Point2);

            // ---- 两点标记（十字，与其他点类工具同构）----
                _display.AddCrosses(parameters.ResultPresentation.GetGraphicsStyle("point1"), new[] { p1 });
                _display.AddCrosses(parameters.ResultPresentation.GetGraphicsStyle("point2"), new[] { p2 });

            // ---- 主测量：两点连线 ----
                _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("distance-line"), p1, p2, isOk);

            // ---- 分量辅助线：直角顶点 = (点2.X, 点1.Y) ----
            var corner = new Point(p2.X, p1.Y);
                _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("delta-xline"), p1, corner, isOk);
                _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("delta-yline"), corner, p2, isOk);

            // ---- 文本 ----
                _display.AddText(parameters.ResultPresentation.GetTextStyle("point1-text"), p1.X, p1.Y,
                    $"P1 ({p1.X:F1},{p1.Y:F1})", 6, 0, isOk);
                _display.AddText(parameters.ResultPresentation.GetTextStyle("point2-text"), p2.X, p2.Y,
                    $"P2 ({p2.X:F1},{p2.Y:F1})", 6, 0, isOk);
            {
                var midX = p1.X / 2 + p2.X / 2;
                var midY = p1.Y / 2 + p2.Y / 2;
                _display.AddText(parameters.ResultPresentation.GetTextStyle("distance-text"), midX, midY,
                    $"点距={Distance:F3}px (ΔX={DeltaX:F3}, ΔY={DeltaY:F3})", 6, 0, isOk);
            }
        }

        /// <summary>契约点数组 → WPF 单点（显示层转换；空数组返回原点）</summary>
        private static Point ToWpfPoint(Point2d[] points)
            => points.Length > 0 ? new Point(points[0].X, points[0].Y) : new Point(0, 0);

        /// <summary>属性映射（结果树/输出契约 SSOT；必须合并基类获得执行耗时）</summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                .. base.GetPropertyMap(),
                // ===== 输入：元素1（点：订阅/手输，整体 Point2d[] + X/Y 拆解）=====
                new("Point1", "点1", "输入/点1"),
                new("Point1X", "点1X(px)", "输入/点1"),
                new("Point1Y", "点1Y(px)", "输入/点1"),
                // ===== 输入：元素2（点：订阅/手输，整体 Point2d[] + X/Y 拆解）=====
                new("Point2", "点2", "输入/点2"),
                new("Point2X", "点2X(px)", "输入/点2"),
                new("Point2Y", "点2Y(px)", "输入/点2"),
                // ===== 结果（两点之间的派生量）=====
                new("Distance", "点距(px)", "结果"),
                new("DeltaX", "ΔX(px)", "结果"),
                new("DeltaY", "ΔY(px)", "结果"),
                new("AngleDeg", "连线角度(°)", "结果"),
                new("GeometryDescription", "几何量摘要", "结果"),
            ];
        }
    }
}

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
            if (parameters.Point1Style.IsVisible)
                _display.AddCrosses(parameters.Point1Style, new[] { p1 });
            if (parameters.Point2Style.IsVisible)
                _display.AddCrosses(parameters.Point2Style, new[] { p2 });

            // ---- 主测量：两点连线 ----
            if (parameters.DistanceLineStyle.IsVisible)
                _display.AddLine(parameters.DistanceLineStyle, p1, p2, isOk);

            // ---- 分量辅助线：直角顶点 = (点2.X, 点1.Y) ----
            var corner = new Point(p2.X, p1.Y);
            if (parameters.DeltaXLineStyle.IsVisible)
                _display.AddLine(parameters.DeltaXLineStyle, p1, corner, isOk);
            if (parameters.DeltaYLineStyle.IsVisible)
                _display.AddLine(parameters.DeltaYLineStyle, corner, p2, isOk);

            // ---- 文本 ----
            if (parameters.Point1TextStyle.IsVisible)
                _display.AddText(parameters.Point1TextStyle, p1.X, p1.Y,
                    $"P1 ({p1.X:F1},{p1.Y:F1})", 6, 0, isOk);
            if (parameters.Point2TextStyle.IsVisible)
                _display.AddText(parameters.Point2TextStyle, p2.X, p2.Y,
                    $"P2 ({p2.X:F1},{p2.Y:F1})", 6, 0, isOk);
            if (parameters.DistanceTextStyle.IsVisible)
            {
                var midX = (p1.X + p2.X) / 2;
                var midY = (p1.Y + p2.Y) / 2;
                _display.AddText(parameters.DistanceTextStyle, midX, midY,
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
                // ===== 元素1（点：整体 Point2d[] + X/Y 拆解）=====
                new("Point1", "点1", "元素1"),
                new("Point1X", "点1X(px)", "元素1"),
                new("Point1Y", "点1Y(px)", "元素1"),
                // ===== 元素2（点：整体 Point2d[] + X/Y 拆解）=====
                new("Point2", "点2", "元素2"),
                new("Point2X", "点2X(px)", "元素2"),
                new("Point2Y", "点2Y(px)", "元素2"),
                // ===== 测量结果（两点之间的派生量）=====
                new("Distance", "点距(px)", "测量结果"),
                new("DeltaX", "ΔX(px)", "测量结果"),
                new("DeltaY", "ΔY(px)", "测量结果"),
                new("AngleDeg", "连线角度(°)", "测量结果"),
                new("GeometryDescription", "几何量摘要", "测量结果"),
            ];
        }
    }
}

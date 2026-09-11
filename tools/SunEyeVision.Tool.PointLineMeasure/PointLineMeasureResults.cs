using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Tool.PointLineMeasure
{
    /// <summary>
    /// 点-线测量结果：主测量 = 点到拟合线距离；辅助 = 垂足位置。
    /// </summary>
    public class PointLineMeasureResults : ToolResults
    {
        /// <summary>元素1：点（仅订阅，无引导）。契约输出（词目 Point）</summary>
        public Point2d[] Point1 { get; set; } = Array.Empty<Point2d>();

        /// <summary>元素2：拟合被测线段（无限直线，点向式）</summary>
        public Line2d[]? Element2Line { get; set; }

        /// <summary>元素2 拟合线段起点/终点（Line2d 无起终点，显示层线段由此二点确定）</summary>
        public Point Element2StartPoint { get; set; }
        public Point Element2EndPoint { get; set; }

        /// <summary>元素2 拟合内点</summary>
        public Point2d[] Element2FitPoints { get; set; } = Array.Empty<Point2d>();

        /// <summary>主测量：点到拟合线距离（px；HALCON distance_pl）</summary>
        public double Distance { get; set; }

        /// <summary>辅助：垂足位置（点1 在拟合线上的正交投影）</summary>
        public Point PerpendicularFoot { get; set; }

        /// <summary>垂足沿拟合线的归一化参数（0=起点，1=终点，&lt;0 或 &gt;1 表示垂足在线外）</summary>
        public double PerpendicularT { get; set; }

        // 元素1：整体点为 Point2d[]（词目 Point）；另拆 X/Y double 供"按参数"绑定
        public double Point1X => Point1.Length > 0 ? Point1[0].X : 0;
        public double Point1Y => Point1.Length > 0 ? Point1[0].Y : 0;

        // 垂足同样拆 X/Y double 暴露（Point 无法入词表）
        public double PerpendicularFootX => PerpendicularFoot.X;
        public double PerpendicularFootY => PerpendicularFoot.Y;

        #region 输入回显（模块结果"输入区域/卡尺"）

        [JsonIgnore]
        public RoiShape[]? GuideGeometry2 { get; set; }

        public int E2CaliberCount { get; set; }
        public double E2CaliberWidth { get; set; }
        public double E2CaliberLength { get; set; }
        public string? E2CaliberTransition { get; set; }

        #endregion

        #region 拟合产物（模块结果"拟合结果/元素2"）

        public double Element2RMSE { get; set; }
        public int Element2EdgePointCount { get; set; }

        #endregion

        /// <summary>判定明细（逐项"名称: 实测值 ∈ [min,max] 通过/越界"；无判定逻辑时为空）</summary>
        public string[] JudgmentDetails { get; set; } = Array.Empty<string>();

        public string GeometryDescription =>
            Element2Line is { Length: > 0 }
                ? $"距离 {Distance:F3}px"
                : "未生成有效拟合结果";

        /// <summary>填充结果显示：元素2 拟合线 + 卡尺带 + 检测点 + 点1 + 垂足/垂线/距离文字。</summary>
        public void FillDisplay(bool isOk, PointLineMeasureParameters parameters)
        {
            _display.Clear();

            // ---- 检测点（卡尺2）----
            if (parameters.CaliberPoint2Style.IsVisible && Element2FitPoints.Length > 0)
                _display.AddCrosses(parameters.CaliberPoint2Style, ToWpfPoints(Element2FitPoints));

            // ---- 位置修正：引导几何/卡尺带显示与交互层同源变换 ----
            var matrix = parameters.IsPositionCorrectionEnabled
                ? PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource)
                : null;
            Point T(Point p) => matrix == null ? p : matrix.TransformWpfPoint(p);
            double scale = matrix?.ScaleFactor() ?? 1.0;

            // ---- 元素2：引导线 + 矩形卡尺带 + 拟合线 ----
            var g2Start = T(new Point(parameters.E2StartX.Value, parameters.E2StartY.Value));
            var g2End = T(new Point(parameters.E2EndX.Value, parameters.E2EndY.Value));
            double len2g = (g2End - g2Start).Length;
            if (parameters.GuideLine2Style.IsVisible && len2g > 1e-6)
                _display.AddLine(parameters.GuideLine2Style, g2Start, g2End, isOk);

            if (parameters.CaliberBand2Style.IsVisible && len2g > 1e-6)
            {
                var bands = MeasureLineBands(g2Start, g2End,
                    parameters.E2MeasureLength1.Value * scale,
                    parameters.E2MeasureLength2.Value,
                    parameters.E2NumMeasures.Value);
                if (bands.Count > 0)
                    _display.AddContours(parameters.CaliberBand2Style, bands, isClosed: false);
            }

            if (Element2Line is { Length: > 0 })
                _display.AddLine(parameters.FitLine2Style, Element2StartPoint, Element2EndPoint, isOk);

            // ---- 点1（十字标记）----
            var p1Wpf = Point1.Length > 0 ? new Point(Point1[0].X, Point1[0].Y) : new Point(0, 0);
            // 点1 为订阅/常量所得，坐标即当前图像真实位置；仅引导几何随位置修正变换（避免二次修正）
            var p1T = p1Wpf;
            if (parameters.Point1Style.IsVisible)
                _display.AddCrosses(parameters.Point1Style, new[] { p1T });

            // ---- 垂足 + 垂线（点1 → 拟合线投影）----
            if (Element2Line is { Length: > 0 })
            {
                var footT = T(PerpendicularFoot);
                if (parameters.FootPointStyle.IsVisible)
                    _display.AddCrosses(parameters.FootPointStyle, new[] { footT });
                if (parameters.FootLineStyle.IsVisible)
                    _display.AddLine(parameters.FootLineStyle, p1T, footT, isOk);
            }

            // ---- 文本 ----
            if (parameters.Point1TextStyle.IsVisible)
                _display.AddText(parameters.Point1TextStyle, p1T.X, p1T.Y,
                    $"P1 ({p1T.X:F1},{p1T.Y:F1})", 6, 0, isOk);
            if (parameters.LineCenterTextStyle.IsVisible && Element2Line is { Length: > 0 })
            {
                var center = new Point(
                    (Element2StartPoint.X + Element2EndPoint.X) / 2,
                    (Element2StartPoint.Y + Element2EndPoint.Y) / 2);
                _display.AddText(parameters.LineCenterTextStyle, center.X, center.Y,
                    $"({Element2StartPoint.X:F1},{Element2StartPoint.Y:F1})-({Element2EndPoint.X:F1},{Element2EndPoint.Y:F1})", 6, 0, isOk);
            }
            if (parameters.LineAngleTextStyle.IsVisible && Element2Line is { Length: > 0 })
            {
                var center = new Point(
                    (Element2StartPoint.X + Element2EndPoint.X) / 2,
                    (Element2StartPoint.Y + Element2EndPoint.Y) / 2);
                double angle = Math.Atan2(Element2EndPoint.Y - Element2StartPoint.Y, Element2EndPoint.X - Element2StartPoint.X) * 180 / Math.PI;
                _display.AddText(parameters.LineAngleTextStyle, center.X, center.Y + 18,
                    $"∠{angle:F2}°", 6, 0, isOk);
            }
            if (parameters.FootTextStyle.IsVisible && Element2Line is { Length: > 0 })
                _display.AddText(parameters.FootTextStyle, PerpendicularFoot.X, PerpendicularFoot.Y,
                    $"垂足 ({PerpendicularFoot.X:F2},{PerpendicularFoot.Y:F2})", 6, -16, isOk);
            if (parameters.DistanceTextStyle.IsVisible && Element2Line is { Length: > 0 })
            {
                var midX = (p1T.X + PerpendicularFoot.X) / 2;
                var midY = (p1T.Y + PerpendicularFoot.Y) / 2;
                _display.AddText(parameters.DistanceTextStyle, midX, midY,
                    $"距离={Distance:F3}", 6, 0, isOk);
            }
        }

        /// <summary>Point2d[] → WPF Point[]（显示层转换；契约类型保持 Point2d[] 以命中词表）</summary>
        private static Point[] ToWpfPoints(Point2d[] points)
        {
            var result = new Point[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = new Point(points[i].X, points[i].Y);
            return result;
        }

        /// <summary>矩形卡尺测量带四边形（与交互层/结果层共用 BuildLineBands；半长 ×2 = 全宽/全长）</summary>
        private static List<Point[]> MeasureLineBands(Point start, Point end, double halfWidth, double halfLength, int count)
            => SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering.MeasureBandOverlay
                .BuildLineBands(start, end, halfWidth * 2, halfLength * 2, count)
                .ToList();

        /// <summary>属性映射（结果树/输出契约 SSOT；必须合并基类获得执行耗时）</summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                .. base.GetPropertyMap(),
                // ===== 输入（对齐 6 工具规范：输入/卡尺）=====
                // 点1：订阅/手输的点（不是拟合产物）→ 归「输入」
                new("Point1", "点1", "输入/点1"),
                new("Point1X", "点1X(px)", "输入/点1"),
                new("Point1Y", "点1Y(px)", "输入/点1"),
                new("GuideGeometry2", "直线2引导几何", "输入/卡尺/引导线"),
                new("E2CaliberCount", "卡尺2测量带数量", "输入/卡尺/测量带"),
                new("E2CaliberWidth", "卡尺2测量带宽度", "输入/卡尺/测量带"),
                new("E2CaliberLength", "卡尺2测量带长度", "输入/卡尺/测量带"),
                new("E2CaliberTransition", "卡尺2边缘极性", "输入/卡尺/测量带"),
                new("Element2FitPoints", "卡尺2检测点", "输入/卡尺/测量带"),
                // ===== 拟合结果（过程）=====
                new("Element2Line", "直线2拟合结果", "拟合结果/直线2"),
                new("Element2RMSE", "直线2拟合残差", "拟合结果/直线2"),
                new("Element2EdgePointCount", "直线2边缘点数", "拟合结果/直线2"),
                // ===== 结果（点1 与 直线2 之间的派生量）=====
                new("Distance", "距离(px)", "结果"),
                new("PerpendicularT", "垂足参数t", "结果"),
                // 垂足 WPF Point 同样拆 X/Y
                new("PerpendicularFootX", "垂足X(px)", "结果"),
                new("PerpendicularFootY", "垂足Y(px)", "结果"),
            ];
        }
    }
}
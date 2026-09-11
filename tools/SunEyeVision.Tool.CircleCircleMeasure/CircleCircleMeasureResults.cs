using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Tool.CircleCircleMeasure
{
    /// <summary>
    /// 圆-圆测量结果。
    /// 契约：元素1/元素2 拟合圆 + 几何量（圆心距 / 两圆交点 / 位置关系）
    /// + 单元素属性（圆心/半径）+ OK/NG。
    /// GetOutputVariables 由 GetPropertyMap 派生（数值按 Double 词目暴露，单位仅显示层）。
    /// </summary>
    public class CircleCircleMeasureResults : ToolResults
    {
        /// <summary>元素1 拟合被测圆</summary>
        public Circle2d[]? Element1Circle { get; set; }

        /// <summary>元素2 拟合被测圆</summary>
        public Circle2d[]? Element2Circle { get; set; }

        /// <summary>主测量：两拟合圆心距离（px；HALCON distance_pp）</summary>
        public double Distance { get; set; }

        /// <summary>两圆交点个数（0=相离/内含，1=相切，2=相交；HALCON intersection_circles）</summary>
        public int IntersectionCount { get; set; }

        /// <summary>主交点 X（沿"元素1圆心→元素2圆心"方向参数 t 较小者；无交点时 NaN）</summary>
        public double IntersectionX { get; set; } = double.NaN;

        /// <summary>主交点 Y（同上）</summary>
        public double IntersectionY { get; set; } = double.NaN;

        /// <summary>次交点 X（仅两交点时有效；否则 NaN）</summary>
        public double Intersection2X { get; set; } = double.NaN;

        /// <summary>次交点 Y（同上）</summary>
        public double Intersection2Y { get; set; } = double.NaN;

        /// <summary>两圆是否相交（交点数 &gt; 0）</summary>
        public bool IsIntersecting => IntersectionCount > 0;

        /// <summary>两圆是否存在公共弧（重合；HALCON intersection_circles 的 is_overlapping 输出）</summary>
        public bool IsOverlapping { get; set; }

        /// <summary>位置关系文字（相离/外切/相交/内切/内含/重合；由圆心距与两半径判定）</summary>
        public string Relation { get; set; } = "未知";

        /// <summary>元素1 拟合圆心/半径</summary>
        public Point Element1Center { get; set; }
        public double Element1Radius { get; set; }

        /// <summary>元素2 拟合圆心/半径</summary>
        public Point Element2Center { get; set; }
        public double Element2Radius { get; set; }

        // 属性表可输出用：Point 无法入词表，中心拆 X/Y double 暴露
        public double Element1CenterX => Element1Center.X;
        public double Element1CenterY => Element1Center.Y;
        public double Element2CenterX => Element2Center.X;
        public double Element2CenterY => Element2Center.Y;

        /// <summary>元素1 拟合内点</summary>
        public Point2d[] Element1FitPoints { get; set; } = Array.Empty<Point2d>();

        /// <summary>元素2 拟合内点</summary>
        public Point2d[] Element2FitPoints { get; set; } = Array.Empty<Point2d>();

        #region 输入回显（模块结果"输入区域/卡尺"）

        /// <summary>元素1 引导几何（引导圆 → RoiShape 包装）</summary>
        [JsonIgnore]
        public RoiShape[]? GuideGeometry1 { get; set; }

        /// <summary>元素2 引导几何（引导圆 → RoiShape 包装）</summary>
        [JsonIgnore]
        public RoiShape[]? GuideGeometry2 { get; set; }

        public int E1CaliberCount { get; set; }
        public double E1CaliberWidth { get; set; }
        public double E1CaliberLength { get; set; }
        public string? E1CaliberTransition { get; set; }
        public int E2CaliberCount { get; set; }
        public double E2CaliberWidth { get; set; }
        public double E2CaliberLength { get; set; }
        public string? E2CaliberTransition { get; set; }

        #endregion

        #region 拟合产物（模块结果"拟合结果/元素N"）

        public double Element1RMSE { get; set; }
        public double Element2RMSE { get; set; }
        public int Element1EdgePointCount { get; set; }
        public int Element2EdgePointCount { get; set; }

        #endregion

        /// <summary>判定明细（逐项"名称: 实测值 ∈ [min,max] 通过/越界"；无判定逻辑时为空）</summary>
        public string[] JudgmentDetails { get; set; } = Array.Empty<string>();

        public string GeometryDescription =>
            (Element1Circle is { Length: > 0 } && Element2Circle is { Length: > 0 })
                ? $"圆心距 {Distance:F3}px / {Relation}（交点 {IntersectionCount} 个）"
                : "未生成有效拟合结果";

        /// <summary>填充结果显示：两卡尺"拟合点"十字（可配样式开关；其余引导/卡尺带/拟合几何/文字标注按样式开关绘制）。</summary>
        public void FillDisplay(bool isOk, CircleCircleMeasureParameters parameters)
        {
            _display.Clear();

            if (parameters.CaliberPoint1Style.IsVisible && Element1FitPoints.Length > 0)
                _display.AddCrosses(parameters.CaliberPoint1Style, ToWpfPoints(Element1FitPoints));
            if (parameters.CaliberPoint2Style.IsVisible && Element2FitPoints.Length > 0)
                _display.AddCrosses(parameters.CaliberPoint2Style, ToWpfPoints(Element2FitPoints));

            // 位置修正：引导几何/卡尺带显示与交互层同源变换
            var matrix = parameters.IsPositionCorrectionEnabled
                ? PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource)
                : null;
            Point T(Point p) => matrix == null ? p : matrix.TransformWpfPoint(p);
            double scale = matrix?.ScaleFactor() ?? 1.0;
            double startPhase = matrix != null
                ? PositionCorrectionResolver.DisplayAngleDeg(matrix, 0.0) * Math.PI / 180.0
                : 0.0;

            // ---- 元素1：引导圆 + 环形卡尺带 + 拟合圆 ----
            var g1c = T(new Point(parameters.E1CenterX.Value, parameters.E1CenterY.Value));
            double g1r = parameters.E1Radius.Value * scale;

            if (parameters.GuideCircle1Style.IsVisible && g1r > 1e-6)
                _display.AddCircle(parameters.GuideCircle1Style, new Circle2d(g1c, g1r), isOk);

            if (parameters.CaliberBand1Style.IsVisible && g1r > 1e-6)
            {
                var bands = MeasureCircleBands(
                    new Circle2d(g1c, g1r),
                    parameters.E1MeasureLength1.Value, parameters.E1MeasureLength2.Value,
                    parameters.E1NumMeasures.Value, startPhase);
                if (bands.Count > 0)
                    _display.AddContours(parameters.CaliberBand1Style, bands, isClosed: true);
            }
            if (Element1Circle is { Length: > 0 })
                _display.AddCircle(parameters.FitCircle1Style, Element1Circle[0], isOk);

            // ---- 元素2：引导圆 + 环形卡尺带 + 拟合圆 ----
            var g2c = T(new Point(parameters.E2CenterX.Value, parameters.E2CenterY.Value));
            double g2r = parameters.E2Radius.Value * scale;

            if (parameters.GuideCircle2Style.IsVisible && g2r > 1e-6)
                _display.AddCircle(parameters.GuideCircle2Style, new Circle2d(g2c, g2r), isOk);

            if (parameters.CaliberBand2Style.IsVisible && g2r > 1e-6)
            {
                var bands = MeasureCircleBands(
                    new Circle2d(g2c, g2r),
                    parameters.E2MeasureLength1.Value, parameters.E2MeasureLength2.Value,
                    parameters.E2NumMeasures.Value, startPhase);
                if (bands.Count > 0)
                    _display.AddContours(parameters.CaliberBand2Style, bands, isClosed: true);
            }
            if (Element2Circle is { Length: > 0 })
                _display.AddCircle(parameters.FitCircle2Style, Element2Circle[0], isOk);

            // ---- 交点标记（1 或 2 个）----
            if (IsIntersecting && parameters.IntersectionPointStyle.IsVisible)
            {
                var points = new List<Point> { new(IntersectionX, IntersectionY) };
                if (IntersectionCount > 1 && !double.IsNaN(Intersection2X))
                    points.Add(new Point(Intersection2X, Intersection2Y));
                _display.AddCrosses(parameters.IntersectionPointStyle, points);
            }

            // ---- 圆心连线（测量基准：距离取自两圆心连线；长度 = distance_pp 的圆心距）----
            if (Element1Circle is { Length: > 0 } && Element2Circle is { Length: > 0 })
            {
                if (parameters.CenterLineStyle.IsVisible)
                    _display.AddLine(parameters.CenterLineStyle, Element1Center, Element2Center, isOk);

                if (parameters.DistanceTextStyle.IsVisible)
                {
                    var mid = new Point((Element1Center.X + Element2Center.X) / 2, (Element1Center.Y + Element2Center.Y) / 2);
                    _display.AddText(parameters.DistanceTextStyle, mid.X, mid.Y,
                        $"圆心距 {Distance:F2}px", 6, 0, isOk);
                }
            }

            // ---- 文本：卡尺1中心/半径、卡尺2中心/半径、交点 ----
            if (Element1Circle is { Length: > 0 })
            {
                if (parameters.Element1CenterTextStyle.IsVisible)
                    _display.AddText(parameters.Element1CenterTextStyle, Element1Center.X, Element1Center.Y,
                        $"卡尺1中心 ({Element1Center.X:F1}, {Element1Center.Y:F1})", isOk);
                if (parameters.Element1RadiusTextStyle.IsVisible)
                    _display.AddText(parameters.Element1RadiusTextStyle, Element1Center.X, Element1Center.Y,
                        $"卡尺1半径 {Element1Radius:F2}px", isOk);
            }
            if (Element2Circle is { Length: > 0 })
            {
                if (parameters.Element2CenterTextStyle.IsVisible)
                    _display.AddText(parameters.Element2CenterTextStyle, Element2Center.X, Element2Center.Y,
                        $"卡尺2中心 ({Element2Center.X:F1}, {Element2Center.Y:F1})", isOk);
                if (parameters.Element2RadiusTextStyle.IsVisible)
                    _display.AddText(parameters.Element2RadiusTextStyle, Element2Center.X, Element2Center.Y,
                        $"卡尺2半径 {Element2Radius:F2}px", isOk);
            }
            if (IsIntersecting && parameters.IntersectionTextStyle.IsVisible)
                _display.AddText(parameters.IntersectionTextStyle, IntersectionX, IntersectionY,
                    $"交点 ({IntersectionX:F1}, {IntersectionY:F1})", 0, 16, isOk);
        }

        /// <summary>Point2d[] → WPF Point[]（显示层转换；契约类型保持 Point2d[] 以命中词表）</summary>
        private static Point[] ToWpfPoints(Point2d[] points)
        {
            var result = new Point[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = new Point(points[i].X, points[i].Y);
            return result;
        }

        /// <summary>环形卡尺测量带四边形（与交互层/结果层共用 BuildCircleBands；半长 ×2 = 全宽/全长）</summary>
        private static List<Point[]> MeasureCircleBands(Circle2d circle, double halfWidth, double halfLength, int count, double startPhase)
            => SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering.MeasureBandOverlay
                .BuildCircleBands(circle, halfWidth * 2, halfLength * 2, count, startPhase)
                .ToList();

        /// <summary>属性映射（结果树/输出契约 SSOT；必须合并基类获得执行耗时）</summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                .. base.GetPropertyMap(),   // 执行耗时 + 节点状态 → 执行信息
                // ===== 输入（对齐 LineDetect 规范：输入/卡尺）=====
                new("GuideGeometry1", "圆1引导几何", "输入/卡尺/引导圆"),
                new("GuideGeometry2", "圆2引导几何", "输入/卡尺/引导圆"),
                new("E1CaliberCount", "卡尺1测量带数量", "输入/卡尺/测量带"),
                new("E1CaliberWidth", "卡尺1测量带宽度", "输入/卡尺/测量带"),
                new("E1CaliberLength", "卡尺1测量带长度", "输入/卡尺/测量带"),
                new("E1CaliberTransition", "卡尺1边缘极性", "输入/卡尺/测量带"),
                new("Element1FitPoints", "卡尺1检测点", "输入/卡尺/测量带"),
                new("E2CaliberCount", "卡尺2测量带数量", "输入/卡尺/测量带"),
                new("E2CaliberWidth", "卡尺2测量带宽度", "输入/卡尺/测量带"),
                new("E2CaliberLength", "卡尺2测量带长度", "输入/卡尺/测量带"),
                new("E2CaliberTransition", "卡尺2边缘极性", "输入/卡尺/测量带"),
                new("Element2FitPoints", "卡尺2检测点", "输入/卡尺/测量带"),
                // ===== 拟合结果（组内顺序：几何 → 残差 → 点数 → 派生量）=====
                new("Element1Circle", "圆1拟合结果", "拟合结果/圆1"),
                new("Element1RMSE", "圆1拟合残差", "拟合结果/圆1"),
                new("Element1EdgePointCount", "圆1边缘点数", "拟合结果/圆1"),
                // 注：Element1Center/Element2Center 为 WPF Point，词表无该词目 → 拆 X/Y double 输出
                new("Element1CenterX", "圆1中心X(px)", "拟合结果/圆1"),
                new("Element1CenterY", "圆1中心Y(px)", "拟合结果/圆1"),
                new("Element1Radius", "圆1半径(px)", "拟合结果/圆1"),
                new("Element2Circle", "圆2拟合结果", "拟合结果/圆2"),
                new("Element2RMSE", "圆2拟合残差", "拟合结果/圆2"),
                new("Element2EdgePointCount", "圆2边缘点数", "拟合结果/圆2"),
                new("Element2CenterX", "圆2中心X(px)", "拟合结果/圆2"),
                new("Element2CenterY", "圆2中心Y(px)", "拟合结果/圆2"),
                new("Element2Radius", "圆2半径(px)", "拟合结果/圆2"),
                // ===== 结果（两元素之间的派生量）=====
                new("Distance", "圆心距(px)", "结果"),
                new("Relation", "位置关系", "结果"),
                new("IntersectionCount", "交点数", "结果"),
                new("IntersectionX", "交点X(px)", "结果"),
                new("IntersectionY", "交点Y(px)", "结果"),
            ];
        }
    }
}

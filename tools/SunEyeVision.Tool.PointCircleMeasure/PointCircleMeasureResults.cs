using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Tool.PointCircleMeasure
{
    /// <summary>
    /// 点-圆测量结果：主测量 = 点到拟合圆心距离（HALCON distance_pp）；
    /// 辅助 = 圆周最近点与圆周距（|圆心距 − 半径|）。
    /// </summary>
    /// <remarks>
    /// 几何点结果（点1 / 元素2 圆心 / 圆周最近点）以契约类型 <see cref="Point2d"/> 数组输出（词目 Point），
    /// 供下游"按图形"整体绑定；X/Y 拆解属性保留，供"按参数"逐值绑定。
    /// </remarks>
    // P0.6-1 能力声明：图像输入 + 位置修正 + 测量引导（**不含区域输入**——测量工具不使用检测区/屏蔽区）
    [ToolCapability(ToolCapabilities.ImageSource | ToolCapabilities.PositionCorrection
        | ToolCapabilities.MeasureGuide)]
    public class PointCircleMeasureResults : ToolResults
    {
        /// <summary>元素1：点（仅订阅，无引导）。契约输出（词目 Point）</summary>
        public Point2d[] Point1 { get; set; } = Array.Empty<Point2d>();

        /// <summary>元素2：拟合被测圆</summary>
        public Circle2d[]? Element2Circle { get; set; }

        /// <summary>元素2 拟合圆心（契约输出，词目 Point）</summary>
        public Point2d[] Element2Center { get; set; } = Array.Empty<Point2d>();

        /// <summary>元素2 拟合半径</summary>
        public double Element2Radius { get; set; }

        /// <summary>元素2 拟合内点</summary>
        public Point2d[] Element2FitPoints { get; set; } = Array.Empty<Point2d>();

        /// <summary>主测量：点到拟合圆心距离（px；HALCON distance_pp）</summary>
        public double DistanceToCenter { get; set; }

        /// <summary>辅助：点到圆周最近距离（px；|DistanceToCenter − Element2Radius|）</summary>
        public double DistanceToCircle { get; set; }

        /// <summary>辅助：圆周上离点1 最近的点（契约输出，词目 Point）</summary>
        public Point2d[] ClosestPoint { get; set; } = Array.Empty<Point2d>();

        // 元素1 拆 X/Y double 暴露（供"按参数"逐值绑定）
        public double Point1X => Point1.Length > 0 ? Point1[0].X : 0;
        public double Point1Y => Point1.Length > 0 ? Point1[0].Y : 0;

        // 元素2 圆心拆 X/Y double 暴露
        public double Element2CenterX => Element2Center.Length > 0 ? Element2Center[0].X : 0;
        public double Element2CenterY => Element2Center.Length > 0 ? Element2Center[0].Y : 0;

        // 圆周最近点拆 X/Y double 暴露
        public double ClosestPointX => ClosestPoint.Length > 0 ? ClosestPoint[0].X : 0;
        public double ClosestPointY => ClosestPoint.Length > 0 ? ClosestPoint[0].Y : 0;

        #region 输入回显（模块结果"输入/卡尺"）

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
            Element2Circle is { Length: > 0 }
                ? $"圆心距 {DistanceToCenter:F3}px / 圆周距 {DistanceToCircle:F3}px"
                : "未生成有效拟合结果";

        /// <summary>填充结果显示：元素2 拟合圆 + 卡尺带 + 检测点 + 点1 + 最近点/连线/文字。</summary>
        public void FillDisplay(bool isOk, PointCircleMeasureParameters parameters)
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
            double startPhase = matrix != null
                ? PositionCorrectionResolver.DisplayAngleDeg(matrix, 0.0) * Math.PI / 180.0
                : 0.0;

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

            // ---- 点1（十字标记）----
            var p1T = ToWpfPoint(Point1);
            if (parameters.Point1Style.IsVisible)
                _display.AddCrosses(parameters.Point1Style, new[] { p1T });

            // ---- 圆周最近点 + 圆心连线 + 圆周距连线 ----
            if (Element2Circle is { Length: > 0 })
            {
                var centerT = ToWpfPoint(Element2Center);
                var closestT = ToWpfPoint(ClosestPoint);

                if (parameters.ClosestPointStyle.IsVisible)
                    _display.AddCrosses(parameters.ClosestPointStyle, new[] { closestT });
                if (parameters.CenterLineStyle.IsVisible)
                    _display.AddLine(parameters.CenterLineStyle, p1T, centerT, isOk);
                if (parameters.DistanceLineStyle.IsVisible)
                    _display.AddLine(parameters.DistanceLineStyle, p1T, closestT, isOk);
            }

            // ---- 文本 ----
            if (parameters.Point1TextStyle.IsVisible)
                _display.AddText(parameters.Point1TextStyle, p1T.X, p1T.Y,
                    $"P1 ({p1T.X:F1},{p1T.Y:F1})", 6, 0, isOk);
            if (parameters.CircleCenterTextStyle.IsVisible && Element2Circle is { Length: > 0 })
            {
                var centerT = ToWpfPoint(Element2Center);
                _display.AddText(parameters.CircleCenterTextStyle, centerT.X, centerT.Y,
                    $"圆2中心 ({centerT.X:F1},{centerT.Y:F1})", isOk);
                _display.AddText(parameters.CircleRadiusTextStyle, centerT.X, centerT.Y + 18,
                    $"半径 {Element2Radius:F2}px", isOk);
            }
            if (parameters.ClosestPointTextStyle.IsVisible && Element2Circle is { Length: > 0 })
            {
                var closestT = ToWpfPoint(ClosestPoint);
                _display.AddText(parameters.ClosestPointTextStyle, closestT.X, closestT.Y,
                    $"最近点 ({closestT.X:F2},{closestT.Y:F2})", 6, -16, isOk);
            }
            if (parameters.DistanceTextStyle.IsVisible && Element2Circle is { Length: > 0 })
            {
                var centerT = ToWpfPoint(Element2Center);
                var midX = (p1T.X + centerT.X) / 2;
                var midY = (p1T.Y + centerT.Y) / 2;
                _display.AddText(parameters.DistanceTextStyle, midX, midY,
                    $"圆心距={DistanceToCenter:F3} / 圆周距={DistanceToCircle:F3}", 6, 0, isOk);
            }
        }

        /// <summary>契约点数组 → WPF 单点（显示层转换；空数组返回原点）</summary>
        private static Point ToWpfPoint(Point2d[] points)
            => points.Length > 0 ? new Point(points[0].X, points[0].Y) : new Point(0, 0);

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
                .. base.GetPropertyMap(),
                // ===== 输入（对齐 6 工具规范：输入/卡尺）=====
                // 点1：订阅/手输的点（不是拟合产物）→ 归「输入」；
                // 整体（词目 Point，供下游"按图形"绑定）+ X/Y 拆解（供"按参数"绑定）
                new("Point1", "点1", "输入/点1"),
                new("Point1X", "点1X(px)", "输入/点1"),
                new("Point1Y", "点1Y(px)", "输入/点1"),
                new("GuideGeometry2", "圆2引导几何", "输入/卡尺/引导圆"),
                new("E2CaliberCount", "卡尺2测量带数量", "输入/卡尺/测量带"),
                new("E2CaliberWidth", "卡尺2测量带宽度", "输入/卡尺/测量带"),
                new("E2CaliberLength", "卡尺2测量带长度", "输入/卡尺/测量带"),
                new("E2CaliberTransition", "卡尺2边缘极性", "输入/卡尺/测量带"),
                new("Element2FitPoints", "卡尺2检测点", "输入/卡尺/测量带"),
                // ===== 拟合结果（过程）=====
                new("Element2Circle", "圆2拟合结果", "拟合结果/圆2"),
                new("Element2RMSE", "圆2拟合残差", "拟合结果/圆2"),
                new("Element2EdgePointCount", "圆2边缘点数", "拟合结果/圆2"),
                new("Element2Center", "圆2圆心", "拟合结果/圆2"),
                new("Element2CenterX", "圆2中心X(px)", "拟合结果/圆2"),
                new("Element2CenterY", "圆2中心Y(px)", "拟合结果/圆2"),
                new("Element2Radius", "圆2半径(px)", "拟合结果/圆2"),
                // ===== 结果（点1 与 圆2 之间的派生量）=====
                new("DistanceToCenter", "圆心距(px)", "结果"),
                new("DistanceToCircle", "圆周距(px)", "结果"),
                new("ClosestPoint", "圆周最近点", "结果"),
                new("ClosestPointX", "最近点X(px)", "结果"),
                new("ClosestPointY", "最近点Y(px)", "结果"),
            ];
        }
    }
}

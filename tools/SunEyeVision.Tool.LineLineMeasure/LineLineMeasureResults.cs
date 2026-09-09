using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Tool.LineLineMeasure
{
    /// <summary>
    /// 线-线测量结果。
    /// 契约：两元素拟合几何 + 几何量（锐角夹角 / 平行间距 / 交点坐标）+ 单线属性（中心/方向角）+ OK/NG。
    /// GetOutputVariables 由 GetPropertyMap 派生（数值按 Double 词目暴露，单位仅显示层）。
    /// </summary>
    public class LineLineMeasureResults : ToolResults
    {
        /// <summary>元素1 拟合被测直线（线段几何）</summary>
        public Line2d[]? Element1Line { get; set; }

        /// <summary>元素2 拟合被测直线</summary>
        public Line2d[]? Element2Line { get; set; }

        /// <summary>元素1 拟合线段起点/终点</summary>
        public Point Element1StartPoint { get; set; }
        public Point Element1EndPoint { get; set; }

        /// <summary>元素2 拟合线段起点/终点</summary>
        public Point Element2StartPoint { get; set; }
        public Point Element2EndPoint { get; set; }

        /// <summary>主测量：两线锐角夹角（0~90°，单位仅显示层）</summary>
        public double Angle { get; set; }

        /// <summary>平行间距（px；两线接近平行时有效，否则 NaN）</summary>
        public double Distance { get; set; }

        /// <summary>两线是否接近平行（用于 距离/交点 语义切换）</summary>
        public bool IsParallel { get; set; }

        /// <summary>交点 X（两线非平行时两条无限线延长交点；平行时 NaN）</summary>
        public double IntersectionX { get; set; } = double.NaN;

        /// <summary>交点 Y（同上）</summary>
        public double IntersectionY { get; set; } = double.NaN;

        /// <summary>元素1 拟合线中点/方向角（度）</summary>
        public Point Element1Center { get; set; }
        public double Element1Angle { get; set; }

        /// <summary>元素2 拟合线中点/方向角（度）</summary>
        public Point Element2Center { get; set; }
        public double Element2Angle { get; set; }

        // 属性表可输出用：Point 无法入词表，中心拆 X/Y double 暴露
        public double Element1CenterX => Element1Center.X;
        public double Element1CenterY => Element1Center.Y;
        public double Element2CenterX => Element2Center.X;
        public double Element2CenterY => Element2Center.Y;

        /// <summary>元素1 拟合内点（参与最终拟合的测量点，用于图上显示；不作为属性表输出）</summary>
        public Point[] Element1FitPoints { get; set; } = Array.Empty<Point>();

        /// <summary>元素2 拟合内点</summary>
        public Point[] Element2FitPoints { get; set; } = Array.Empty<Point>();

        /// <summary>整体判定 OK/NG（Judge 聚合，任一启用项越界/无值则 NG）</summary>
        public bool IsPass { get; set; }

        public string GeometryDescription =>
            (Element1Line is { Length: > 0 } && Element2Line is { Length: > 0 })
                ? (IsParallel
                    ? $"平行：间距 {Distance:F3}px"
                    : $"夹角 {Angle:F3}° / 交点({IntersectionX:F1}, {IntersectionY:F1})")
                : "未生成有效拟合结果";

        /// <summary>填充结果显示：两卡尺"拟合点"十字（可配样式开关；其余引导/卡尺带/拟合线/文字标注不画）。</summary>
        public void FillDisplay(bool isOk, LineLineMeasureParameters parameters)
        {
            _display.Clear();

            if (parameters.CaliberPoint1Style.IsVisible && Element1FitPoints.Length > 0)
                _display.AddCrosses(parameters.CaliberPoint1Style, Element1FitPoints);
            if (parameters.CaliberPoint2Style.IsVisible && Element2FitPoints.Length > 0)
                _display.AddCrosses(parameters.CaliberPoint2Style, Element2FitPoints);

            // 位置修正：引导线/带显示与交互层同源变换
            var matrix = parameters.IsPositionCorrectionEnabled
                ? PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource)
                : null;
            Point T(Point p) => matrix == null ? p : matrix.TransformWpfPoint(p);

            // ---- 元素1：引导线 + 卡尺带 + 拟合线 ----
            if (parameters.GuideLine1Style.IsVisible)
            {
                var g1s = T(new Point(parameters.E1StartX.Value, parameters.E1StartY.Value));
                var g1e = T(new Point(parameters.E1EndX.Value, parameters.E1EndY.Value));
                if ((g1e - g1s).Length > 1e-6)
                    _display.AddLine(parameters.GuideLine1Style, g1s, g1e, isOk);
            }
            if (parameters.CaliberBand1Style.IsVisible)
            {
                var g1s = T(new Point(parameters.E1StartX.Value, parameters.E1StartY.Value));
                var g1e = T(new Point(parameters.E1EndX.Value, parameters.E1EndY.Value));
                var bands = MeasureBands(g1s, g1e, parameters.E1MeasureLength1.Value, parameters.E1MeasureLength2.Value, parameters.E1NumMeasures.Value);
                if (bands.Count > 0)
                    _display.AddContours(parameters.CaliberBand1Style, bands, isClosed: true);
            }
            if (Element1Line is { Length: > 0 })
                _display.AddLine(parameters.FitLine1Style, Element1StartPoint, Element1EndPoint, isOk);

            // ---- 元素2：引导线 + 卡尺带 + 拟合线 ----
            if (parameters.GuideLine2Style.IsVisible)
            {
                var g2s = T(new Point(parameters.E2StartX.Value, parameters.E2StartY.Value));
                var g2e = T(new Point(parameters.E2EndX.Value, parameters.E2EndY.Value));
                if ((g2e - g2s).Length > 1e-6)
                    _display.AddLine(parameters.GuideLine2Style, g2s, g2e, isOk);
            }
            if (parameters.CaliberBand2Style.IsVisible)
            {
                var g2s = T(new Point(parameters.E2StartX.Value, parameters.E2StartY.Value));
                var g2e = T(new Point(parameters.E2EndX.Value, parameters.E2EndY.Value));
                var bands = MeasureBands(g2s, g2e, parameters.E2MeasureLength1.Value, parameters.E2MeasureLength2.Value, parameters.E2NumMeasures.Value);
                if (bands.Count > 0)
                    _display.AddContours(parameters.CaliberBand2Style, bands, isClosed: true);
            }
            if (Element2Line is { Length: > 0 })
                _display.AddLine(parameters.FitLine2Style, Element2StartPoint, Element2EndPoint, isOk);

            // ---- 角点（交点标记）----
            if (!IsParallel && !double.IsNaN(IntersectionX) && parameters.IntersectionPointStyle.IsVisible)
                _display.AddCrosses(parameters.IntersectionPointStyle, new[] { new Point(IntersectionX, IntersectionY) });

            // ---- 文本：卡尺1中心/角度、卡尺2中心/角度、交点 ----
            if (Element1Line is { Length: > 0 })
            {
                if (parameters.Element1CenterTextStyle.IsVisible)
                    _display.AddText(parameters.Element1CenterTextStyle, Element1Center.X, Element1Center.Y - 8,
                        $"卡尺1中心 ({Element1Center.X:F1}, {Element1Center.Y:F1})", isOk);
                if (parameters.Element1AngleTextStyle.IsVisible)
                    _display.AddText(parameters.Element1AngleTextStyle, Element1Center.X, Element1Center.Y + 6,
                        $"卡尺1角度 {Element1Angle:G3}°", isOk);
            }
            if (Element2Line is { Length: > 0 })
            {
                if (parameters.Element2CenterTextStyle.IsVisible)
                    _display.AddText(parameters.Element2CenterTextStyle, Element2Center.X, Element2Center.Y - 8,
                        $"卡尺2中心 ({Element2Center.X:F1}, {Element2Center.Y:F1})", isOk);
                if (parameters.Element2AngleTextStyle.IsVisible)
                    _display.AddText(parameters.Element2AngleTextStyle, Element2Center.X, Element2Center.Y + 6,
                        $"卡尺2角度 {Element2Angle:G3}°", isOk);
            }
            if (!IsParallel && !double.IsNaN(IntersectionX) && parameters.IntersectionTextStyle.IsVisible)
                _display.AddText(parameters.IntersectionTextStyle, IntersectionX, IntersectionY + 16,
                    $"交点 ({IntersectionX:F1}, {IntersectionY:F1})", isOk);
        }

        /// <summary>卡尺测量带四边形（与交互层/结果层共用 BuildLineBands；参数半长 ×2 = 全宽/全长）</summary>
        private static List<Point[]> MeasureBands(Point start, Point end, double halfWidth, double halfLength, int count)
            => SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering.MeasureBandOverlay
                .BuildLineBands(start, end, halfWidth * 2, halfLength * 2, count)
                .ToList();

        /// <summary>属性映射（结果树/输出契约 SSOT；必须合并基类获得执行耗时）</summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                .. base.GetPropertyMap(),
                // 注：Element1Center/Element2Center 为 Point 类型，SdkTypeVocabulary 无 Point 词目，
                //     不得写入属性表（否则处理结果时抛"不是通用类型"）。如需输出请用 X/Y double。
                new("Element1CenterX", "元素1中心X(px)", "测量结果/元素1"),
                new("Element1CenterY", "元素1中心Y(px)", "测量结果/元素1"),
                new("Element1Angle", "元素1角度(°)", "测量结果/元素1"),
                new("Element2CenterX", "元素2中心X(px)", "测量结果/元素2"),
                new("Element2CenterY", "元素2中心Y(px)", "测量结果/元素2"),
                new("Element2Angle", "元素2角度(°)", "测量结果/元素2"),
                new("Angle", "两线夹角(°)", "测量结果"),
                new("Distance", "平行间距(px)", "测量结果"),
                new("IntersectionX", "交点X(px)", "测量结果"),
                new("IntersectionY", "交点Y(px)", "测量结果"),
                new("IsPass", "判定结果", "测量结果"),
            ];
        }
    }
}

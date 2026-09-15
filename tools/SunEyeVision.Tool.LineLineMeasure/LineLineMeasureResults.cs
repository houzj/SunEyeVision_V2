using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Tool.LineLineMeasure
{
    /// <summary>
    /// 线-线测量结果。
    /// 契约：两元素拟合几何 + 几何量（锐角夹角 / 平行间距 / 交点坐标）+ 单线属性（中心/方向角）+ OK/NG。
    /// GetOutputVariables 由 GetPropertyMap 派生（数值按 Double 词目暴露，单位仅显示层）。
    /// </summary>
    // P0.6-1 能力声明：图像输入 + 位置修正 + 测量引导（**不含区域输入**——测量工具不使用检测区/屏蔽区）
    [ToolCapability(ToolCapabilities.ImageSource | ToolCapabilities.PositionCorrection
        | ToolCapabilities.MeasureGuide)]
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

        /// <summary>两线法向投影距离（px；HALCON distance_pl：线1 中点 → 线2 无限直线的正交距离）</summary>
        public double Distance { get; set; }

        /// <summary>法向投影距离最小值（px；线1 全部拟合点 → 线2 无限直线的正交距离取 min）</summary>
        public double DistanceMin { get; set; }

        /// <summary>法向投影距离最大值（px；线1 全部拟合点 → 线2 无限直线的正交距离取 max）</summary>
        public double DistanceMax { get; set; }

        /// <summary>取到最小法向距离的元素1 拟合点（供图上画最小距离投影段；不参与属性表输出）</summary>
        public Point DistanceMinPoint { get; set; }

        /// <summary>取到最大法向距离的元素1 拟合点（供图上画最大距离投影段；不参与属性表输出）</summary>
        public Point DistanceMaxPoint { get; set; }

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

        /// <summary>元素1 拟合内点（参与最终拟合的测量点；Point2d[] 可入输出契约，显示层再转 WPF Point）</summary>
        public Point2d[] Element1FitPoints { get; set; } = Array.Empty<Point2d>();

        /// <summary>元素2 拟合内点</summary>
        public Point2d[] Element2FitPoints { get; set; } = Array.Empty<Point2d>();

        /// <summary>输入图像宽/高（px，Run 填充；用于把"无限直线延长段"裁剪到图像范围）</summary>
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }

        #region 输入回显（模块结果"输入/卡尺"；Run 从参数回填，对齐 LineDetect 规范）

        /// <summary>元素1 引导几何（引导直线 → RoiShape 包装）</summary>
        [JsonIgnore]
        public RoiShape[]? GuideGeometry1 { get; set; }

        /// <summary>元素2 引导几何</summary>
        [JsonIgnore]
        public RoiShape[]? GuideGeometry2 { get; set; }

        /// <summary>元素1 测量带数量（卡尺数）</summary>
        public int E1CaliberCount { get; set; }

        /// <summary>元素1 测量带宽度（法线全长）</summary>
        public double E1CaliberWidth { get; set; }

        /// <summary>元素1 测量带长度（切线全长）</summary>
        public double E1CaliberLength { get; set; }

        /// <summary>元素1 边缘极性（文本回显）</summary>
        public string? E1CaliberTransition { get; set; }

        /// <summary>元素2 测量带数量</summary>
        public int E2CaliberCount { get; set; }

        /// <summary>元素2 测量带宽度</summary>
        public double E2CaliberWidth { get; set; }

        /// <summary>元素2 测量带长度</summary>
        public double E2CaliberLength { get; set; }

        /// <summary>元素2 边缘极性</summary>
        public string? E2CaliberTransition { get; set; }

        #endregion

        #region 拟合产物（模块结果"拟合结果/元素N"）

        /// <summary>元素1 拟合残差（RMSE，HALCON FitLineContourXld 的 Dist 均方根）</summary>
        public double Element1RMSE { get; set; }

        /// <summary>元素2 拟合残差</summary>
        public double Element2RMSE { get; set; }

        /// <summary>元素1 参与拟合的边缘点数</summary>
        public int Element1EdgePointCount { get; set; }

        /// <summary>元素2 参与拟合的边缘点数</summary>
        public int Element2EdgePointCount { get; set; }

        #endregion

        /// <summary>判定明细（逐项"名称: 实测值 ∈ [min,max] 通过/越界"；无判定逻辑时为空）</summary>
        public string[] JudgmentDetails { get; set; } = Array.Empty<string>();

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

            if (parameters.ResultPresentation.GetGraphicsStyle("caliber-point1").IsVisible && Element1FitPoints.Length > 0)
                _display.AddCrosses(parameters.ResultPresentation.GetGraphicsStyle("caliber-point1"), ToWpfPoints(Element1FitPoints));
            if (parameters.ResultPresentation.GetGraphicsStyle("caliber-point2").IsVisible && Element2FitPoints.Length > 0)
                _display.AddCrosses(parameters.ResultPresentation.GetGraphicsStyle("caliber-point2"), ToWpfPoints(Element2FitPoints));

            // 位置修正：引导线/带显示与交互层同源变换
            var matrix = parameters.IsPositionCorrectionEnabled
                ? PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource)
                : null;
            Point T(Point p) => matrix == null ? p : matrix.TransformWpfPoint(p);

            // ---- 元素1：引导线 + 卡尺带 + 拟合线 ----
            if (parameters.ResultPresentation.GetGraphicsStyle("guide-line1").IsVisible)
            {
                var g1s = T(new Point(parameters.E1StartX.Value, parameters.E1StartY.Value));
                var g1e = T(new Point(parameters.E1EndX.Value, parameters.E1EndY.Value));
                if ((g1e - g1s).Length > 1e-6)
                    _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("guide-line1"), g1s, g1e, isOk);
            }
            if (parameters.ResultPresentation.GetGraphicsStyle("caliber-band1").IsVisible)
            {
                var g1s = T(new Point(parameters.E1StartX.Value, parameters.E1StartY.Value));
                var g1e = T(new Point(parameters.E1EndX.Value, parameters.E1EndY.Value));
                var bands = MeasureBands(g1s, g1e, parameters.E1MeasureLength1.Value, parameters.E1MeasureLength2.Value, parameters.E1NumMeasures.Value);
                if (bands.Count > 0)
                    _display.AddContours(parameters.ResultPresentation.GetGraphicsStyle("caliber-band1"), bands, isClosed: true);
            }
            if (Element1Line is { Length: > 0 })
                _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("fit-line1"), Element1StartPoint, Element1EndPoint, isOk);

            // ---- 元素2：引导线 + 卡尺带 + 拟合线 ----
            if (parameters.ResultPresentation.GetGraphicsStyle("guide-line2").IsVisible)
            {
                var g2s = T(new Point(parameters.E2StartX.Value, parameters.E2StartY.Value));
                var g2e = T(new Point(parameters.E2EndX.Value, parameters.E2EndY.Value));
                if ((g2e - g2s).Length > 1e-6)
                    _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("guide-line2"), g2s, g2e, isOk);
            }
            if (parameters.ResultPresentation.GetGraphicsStyle("caliber-band2").IsVisible)
            {
                var g2s = T(new Point(parameters.E2StartX.Value, parameters.E2StartY.Value));
                var g2e = T(new Point(parameters.E2EndX.Value, parameters.E2EndY.Value));
                var bands = MeasureBands(g2s, g2e, parameters.E2MeasureLength1.Value, parameters.E2MeasureLength2.Value, parameters.E2NumMeasures.Value);
                if (bands.Count > 0)
                    _display.AddContours(parameters.ResultPresentation.GetGraphicsStyle("caliber-band2"), bands, isClosed: true);
            }
            if (Element2Line is { Length: > 0 })
                _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("fit-line2"), Element2StartPoint, Element2EndPoint, isOk);

            // ---- 角点（交点标记）----
            if (!IsParallel && !double.IsNaN(IntersectionX) && parameters.ResultPresentation.GetGraphicsStyle("intersection-point").IsVisible)
                _display.AddCrosses(parameters.ResultPresentation.GetGraphicsStyle("intersection-point"), new[] { new Point(IntersectionX, IntersectionY) });

            // ---- 文本：卡尺1中心/角度、卡尺2中心/角度、交点 ----
            if (Element1Line is { Length: > 0 })
            {
                // 同锚点（元素1中心）：中心/角度两行由渲染层自动换行（行高随字号自适应，缩放不错位）
                if (parameters.ResultPresentation.GetTextStyle("element1-center-text").IsVisible)
                    _display.AddText(parameters.ResultPresentation.GetTextStyle("element1-center-text"), Element1Center.X, Element1Center.Y,
                        $"卡尺1中心 ({Element1Center.X:F1}, {Element1Center.Y:F1})", isOk);
                if (parameters.ResultPresentation.GetTextStyle("element1-angle-text").IsVisible)
                    _display.AddText(parameters.ResultPresentation.GetTextStyle("element1-angle-text"), Element1Center.X, Element1Center.Y,
                        $"卡尺1角度 {Element1Angle:G3}°", isOk);
            }
            if (Element2Line is { Length: > 0 })
            {
                if (parameters.ResultPresentation.GetTextStyle("element2-center-text").IsVisible)
                    _display.AddText(parameters.ResultPresentation.GetTextStyle("element2-center-text"), Element2Center.X, Element2Center.Y,
                        $"卡尺2中心 ({Element2Center.X:F1}, {Element2Center.Y:F1})", isOk);
                if (parameters.ResultPresentation.GetTextStyle("element2-angle-text").IsVisible)
                    _display.AddText(parameters.ResultPresentation.GetTextStyle("element2-angle-text"), Element2Center.X, Element2Center.Y,
                        $"卡尺2角度 {Element2Angle:G3}°", isOk);
            }
            if (!IsParallel && !double.IsNaN(IntersectionX) && parameters.ResultPresentation.GetTextStyle("intersection-text").IsVisible)
                _display.AddText(parameters.ResultPresentation.GetTextStyle("intersection-text"), IntersectionX, IntersectionY,
                    $"交点 ({IntersectionX:F1}, {IntersectionY:F1})", 0, 16, isOk);

            // ---- 无限直线延长（虚线，裁到图像范围）：拟合输出是线段，而交点取自无限直线，延长段让语义可见 ----
            if (ImageWidth > 0 && ImageHeight > 0)
            {
                if (parameters.ResultPresentation.GetGraphicsStyle("extension-line1").IsVisible && Element1Line is { Length: > 0 }
                    && TryClipInfiniteLine(Element1StartPoint, Element1EndPoint, ImageWidth, ImageHeight, out var e1a, out var e1b))
                    _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("extension-line1"), e1a, e1b, isOk);

                if (parameters.ResultPresentation.GetGraphicsStyle("extension-line2").IsVisible && Element2Line is { Length: > 0 }
                    && TryClipInfiniteLine(Element2StartPoint, Element2EndPoint, ImageWidth, ImageHeight, out var e2a, out var e2b))
                    _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("extension-line2"), e2a, e2b, isOk);
            }

            // ---- 夹角角标弧 + 角度文字（非平行；弧取两线朝交点的锐角侧）----
            if (!IsParallel && !double.IsNaN(IntersectionX) && !double.IsNaN(IntersectionY))
            {
                var center = new Point(IntersectionX, IntersectionY);
                double r = parameters.AngleArcRadius > 1.0 ? parameters.AngleArcRadius : 50.0;

                // 方向取"交点 → 各拟合线中点"，并把张角校正到锐角侧（|Δ| <= 90°），与 Angle 语义一致
                double a1 = Math.Atan2(Element1Center.Y - center.Y, Element1Center.X - center.X);
                double a2 = Math.Atan2(Element2Center.Y - center.Y, Element2Center.X - center.X);
                double delta = NormalizeToPi(a2 - a1);
                if (Math.Abs(delta) > Math.PI / 2)
                {
                    a2 = NormalizeToPi(a2 + Math.PI);
                    delta = NormalizeToPi(a2 - a1);
                }

                if (parameters.ResultPresentation.GetGraphicsStyle("angle-arc").IsVisible)
                    _display.AddArc(parameters.ResultPresentation.GetGraphicsStyle("angle-arc"), new Arc2d(center, r, a1, a2, isMajorArc: false), isOk);

                if (parameters.ResultPresentation.GetTextStyle("angle-text").IsVisible)
                {
                    double mid = a1 + delta / 2.0;                       // 角平分方向（文字落在弧外侧）
                    _display.AddText(parameters.ResultPresentation.GetTextStyle("angle-text"),
                        center.X + Math.Cos(mid) * r * 1.25,
                        center.Y + Math.Sin(mid) * r * 1.25,
                        $"夹角 {Angle:F2}°", isOk);
                }
            }

            // ---- 距离可视化：线1 中点 → 线2 的法向投影段（长度 = HALCON distance_pl 的法向距离）+ 数值文字 ----
            if (Element1Line is { Length: > 0 } && Element2Line is { Length: > 0 }
                && TryNormalFoot(Element1Center, Element2StartPoint, Element2EndPoint, out var foot))
            {
                if (parameters.ResultPresentation.GetGraphicsStyle("distance-line").IsVisible)
                    _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("distance-line"), Element1Center, foot, isOk);

                if (parameters.ResultPresentation.GetTextStyle("distance-text").IsVisible)
                {
                    var mid = new Point((Element1Center.X + foot.X) / 2, (Element1Center.Y + foot.Y) / 2);
                    // 同锚点（连线中点）+ 屏幕恒定右移 6px：两行自动换行，缩放不错位
                    _display.AddText(parameters.ResultPresentation.GetTextStyle("distance-text"), mid.X, mid.Y,
                        $"法向距离 {Distance:F2}px", 6, 0, isOk);
                    _display.AddText(parameters.ResultPresentation.GetTextStyle("distance-text"), mid.X, mid.Y,
                        $"范围 {DistanceMin:F2}~{DistanceMax:F2}px", 6, 0, isOk);
                }
            }

            // ---- 最小/最大法向距离投影段（默认隐藏，按需开启）：极值拟合点 → 线2 垂足 ----
            if (Element1Line is { Length: > 0 } && Element2Line is { Length: > 0 })
            {
                if (parameters.ResultPresentation.GetGraphicsStyle("distance-min-line").IsVisible
                    && DistanceMinPoint != default
                    && TryNormalFoot(DistanceMinPoint, Element2StartPoint, Element2EndPoint, out var footMin))
                    _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("distance-min-line"), DistanceMinPoint, footMin, isOk);

                if (parameters.ResultPresentation.GetGraphicsStyle("distance-max-line").IsVisible
                    && DistanceMaxPoint != default
                    && TryNormalFoot(DistanceMaxPoint, Element2StartPoint, Element2EndPoint, out var footMax))
                    _display.AddLine(parameters.ResultPresentation.GetGraphicsStyle("distance-max-line"), DistanceMaxPoint, footMax, isOk);
            }
        }

        /// <summary>
        /// 点 p 在过 s1s2 的**无限直线**上的垂足（法向投影，纯显示层几何，命中"显示层"自研例外）。
        /// 不裁剪到线段区间：保证连线是真正的法向投影段，长度与 distance_pl 的法向距离严格一致。
        /// </summary>
        private static bool TryNormalFoot(Point p, Point s1, Point s2, out Point foot)
        {
            foot = default;
            var d = s2 - s1;
            double len2 = d.LengthSquared;
            if (len2 < 1e-12) return false;
            double t = Vector.Multiply(p - s1, d) / len2;
            foot = s1 + d * t;
            return true;
        }

        /// <summary>Point2d[] → WPF Point[]（显示层转换；契约类型保持 Point2d[] 以命中词表）</summary>
        private static Point[] ToWpfPoints(Point2d[] points)
        {
            var result = new Point[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = new Point(points[i].X, points[i].Y);
            return result;
        }

        /// <summary>把过两点的无限直线裁剪到 [0,w]×[0,h] 图像矩形（参数化裁剪）</summary>
        private static bool TryClipInfiniteLine(Point a, Point b, double w, double h, out Point p1, out Point p2)
        {
            p1 = default;
            p2 = default;
            if (w <= 0 || h <= 0) return false;

            var d = b - a;
            if (d.Length < 1e-9) return false;

            double tMin = double.NegativeInfinity, tMax = double.PositiveInfinity;
            if (!ClipAxis(a.X, d.X, 0, w, ref tMin, ref tMax)) return false;
            if (!ClipAxis(a.Y, d.Y, 0, h, ref tMin, ref tMax)) return false;
            if (tMin > tMax) return false;

            p1 = new Point(a.X + tMin * d.X, a.Y + tMin * d.Y);
            p2 = new Point(a.X + tMax * d.X, a.Y + tMax * d.Y);
            return true;
        }

        /// <summary>单轴裁剪（更新参数区间；轴平行且区间外时返回 false）</summary>
        private static bool ClipAxis(double p, double d, double lo, double hi, ref double tMin, ref double tMax)
        {
            if (Math.Abs(d) < 1e-12)
                return p >= lo && p <= hi;

            double t1 = (lo - p) / d, t2 = (hi - p) / d;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            return tMin <= tMax;
        }

        /// <summary>角度归一化到 (-π, π]</summary>
        private static double NormalizeToPi(double angle)
        {
            double x = (angle + Math.PI) % (2 * Math.PI);
            if (x < 0) x += 2 * Math.PI;
            return x - Math.PI;
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
                .. base.GetPropertyMap(),   // 执行耗时 + 节点状态 → 执行信息
                // ===== 输入（对齐 LineDetect 规范：输入/卡尺）=====
                new("GuideGeometry1", "直线1引导几何", "输入/卡尺/引导线", IsGroupPlaceholder: true),
                new("GuideGeometry2", "直线2引导几何", "输入/卡尺/引导线", IsGroupPlaceholder: true),
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
                new("Element1Line", "直线1拟合结果", "拟合结果/直线1"),
                new("Element1RMSE", "直线1拟合残差", "拟合结果/直线1"),
                new("Element1EdgePointCount", "直线1边缘点数", "拟合结果/直线1"),
                // 注：Element1Center/Element2Center 为 WPF Point，词表无该词目 → 拆 X/Y double 输出
                new("Element1CenterX", "直线1中心X(px)", "拟合结果/直线1"),
                new("Element1CenterY", "直线1中心Y(px)", "拟合结果/直线1"),
                new("Element1Angle", "直线1角度(°)", "拟合结果/直线1"),
                new("Element2Line", "直线2拟合结果", "拟合结果/直线2"),
                new("Element2RMSE", "直线2拟合残差", "拟合结果/直线2"),
                new("Element2EdgePointCount", "直线2边缘点数", "拟合结果/直线2"),
                new("Element2CenterX", "直线2中心X(px)", "拟合结果/直线2"),
                new("Element2CenterY", "直线2中心Y(px)", "拟合结果/直线2"),
                new("Element2Angle", "直线2角度(°)", "拟合结果/直线2"),
                // ===== 结果（两元素之间的派生量）=====
                new("Angle", "两线夹角(°)", "结果"),
                new("Distance", "法向投影距离(px)", "结果"),
                new("DistanceMin", "法向距离最小值(px)", "结果"),
                new("DistanceMax", "法向距离最大值(px)", "结果"),
                new("IsParallel", "是否平行", "结果"),
                new("IntersectionX", "交点X(px)", "结果"),
                new("IntersectionY", "交点Y(px)", "结果"),
            ];
        }
    }
}

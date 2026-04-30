using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;

// 类型别名解决歧义
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using WpfRect = System.Windows.Rect;
using SdkLineSegment = SunEyeVision.Plugin.SDK.Models.Visualization.LineSegment;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 结果渲染层 - 基于 DrawingContext 的高性能渲染
    /// </summary>
    public sealed class ResultRenderLayer : FrameworkElement
    {
        #region 私有字段

        private DisplayCollector? _collector;
        private IReadOnlyList<DisplayItemBase>? _regionItems;
        private IReadOnlyList<DisplayItemBase>? _highlightItems;
        private readonly Dictionary<uint, SolidColorBrush> _brushCache = new();
        private readonly Dictionary<uint, Pen> _penCache = new();

        #endregion

        #region 公共接口

        /// <summary>设置显示收集器（工具输出）</summary>
        public void SetDisplayCollector(DisplayCollector? collector)
        {
            UnsubscribeItems(_collector?.Items);
            _collector = collector;
            SubscribeItems(_collector?.Items);
            InvalidateVisual();
        }

        /// <summary>设置区域显示项（检测区域覆盖）</summary>
        public void SetRegionItems(IEnumerable<DisplayItemBase>? items)
        {
            UnsubscribeItems(_regionItems);
            _regionItems = items?.ToList();
            SubscribeItems(_regionItems);
            InvalidateVisual();
        }

        /// <summary>设置显示项（兼容旧接口，内部转为 collector 模式）</summary>
        public void SetItems(IReadOnlyList<DisplayItemBase>? items)
        {
            UnsubscribeItems(_collector?.Items);
            _collector = null; // 直接设置 items 时清除 collector 引用
            UnsubscribeItems(_regionItems);
            _regionItems = items;
            SubscribeItems(_regionItems);
            InvalidateVisual();
        }

        /// <summary>设置高亮显示项</summary>
        public void SetHighlightItems(IEnumerable<DisplayItemBase>? items)
        {
            UnsubscribeItems(_highlightItems);
            _highlightItems = items?.ToList();
            SubscribeItems(_highlightItems);
            InvalidateVisual();
        }

        /// <summary>清空所有内容</summary>
        public void Clear()
        {
            UnsubscribeItems(_collector?.Items);
            UnsubscribeItems(_regionItems);
            UnsubscribeItems(_highlightItems);
            _collector = null;
            _regionItems = null;
            _highlightItems = null;
            InvalidateVisual();
        }

        /// <summary>清空高亮元素</summary>
        public void ClearHighlight()
        {
            UnsubscribeItems(_highlightItems);
            _highlightItems = null;
            InvalidateVisual();
        }

        #endregion

        #region 属性订阅（UI操作自动触发重绘）

        private void SubscribeItems(IReadOnlyList<DisplayItemBase>? items)
        {
            if (items == null) return;
            foreach (var item in items)
                item.PropertyChanged += OnItemPropertyChanged;
        }

        private void UnsubscribeItems(IReadOnlyList<DisplayItemBase>? items)
        {
            if (items == null) return;
            foreach (var item in items)
                item.PropertyChanged -= OnItemPropertyChanged;
        }

        private void OnItemPropertyChanged(object? sender, EventArgs e) => InvalidateVisual();

        #endregion

        #region 幂律线宽支持

        private double _viewportScale = 1.0;

        public double ViewportScale
        {
            get => _viewportScale;
            set
            {
                if (Math.Abs(_viewportScale - value) > 0.001)
                {
                    _viewportScale = value;
                    InvalidateVisual();
                }
            }
        }

        public void SetViewportScale(double scale) => ViewportScale = scale;

        private double GetScaledLineWidth(double rawWidth) =>
            _viewportScale > 0.001 ? rawWidth / _viewportScale : rawWidth;

        #endregion

        #region 渲染

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            _brushCache.Clear();
            _penCache.Clear();

            // 渲染顺序：区域(底层) → 工具输出(中层) → 高亮(顶层)
            // DrawingContext 后画覆盖先画，通道顺序即层次

            if (_regionItems != null)
            {
                foreach (var item in _regionItems)
                {
                    if (!item.IsVisible) continue;
                    RenderItem(dc, item);
                }
            }

            if (_collector?.Items != null)
            {
                foreach (var item in _collector.Items)
                {
                    if (!item.IsVisible) continue;
                    RenderItem(dc, item);
                }
            }

            if (_highlightItems != null)
            {
                foreach (var item in _highlightItems)
                {
                    if (!item.IsVisible) continue;
                    RenderItem(dc, item);
                }
            }
        }

        private void RenderItem(DrawingContext dc, DisplayItemBase item)
        {
            if (item is GraphicsDisplayItem g)
                RenderGraphics(dc, g);
            else if (item is TextDisplayItem t)
                RenderText(dc, t);
        }

        #endregion

        #region 图形渲染

        private void RenderGraphics(DrawingContext dc, GraphicsDisplayItem item)
        {
            var style = item.Style;
            if (style == null) return;

            // Opacity 支持：PushOpacity / Pop 包裹整个渲染过程
            dc.PushOpacity(style.Opacity);
            try
            {
                var defaultColorArgb = style.GetEffectiveColorArgb(true);
                var brush = GetBrush(defaultColorArgb);
                var pen = GetPen(defaultColorArgb, GetScaledLineWidth(style.LineWidth));

                switch (item.Type)
                {
                    case GraphicsType.Point:
                        RenderPoints(dc, item, style, brush); break;
                    case GraphicsType.Line:
                        RenderLines(dc, item, pen); break;
                    case GraphicsType.Rectangle:
                        RenderRectangles(dc, item, style, brush, pen); break;
                    case GraphicsType.Circle:
                        RenderCircles(dc, item, style, brush, pen); break;
                    case GraphicsType.Arc:
                        RenderArcs(dc, item, pen); break;
                    case GraphicsType.Polygon:
                        RenderPolygons(dc, item, style, brush, pen); break;
                    case GraphicsType.Contour:
                        RenderContours(dc, item, style, brush, pen); break;
                    case GraphicsType.Arrow:
                        RenderArrows(dc, item, pen); break;
                    case GraphicsType.Cross:
                        RenderCrosses(dc, item, style, pen); break;
                    case GraphicsType.CoordinateSystem:
                        RenderCoordinateSystem(dc, item, style); break;
                }
            }
            finally
            {
                dc.Pop(); // 恢复 Opacity 状态
            }
        }

        private void RenderPoints(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, SolidColorBrush defaultBrush)
        {
            for (int i = 0; i < item.Geometries.Length; i++)
            {
                if (item.Geometries[i] is Point2d pt)
                {
                    bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                    var effectiveBrush = isPass ? defaultBrush : GetBrush(style.GetEffectiveColorArgb(false));
                    var size = GetScaledLineWidth(style.LineWidth) * 2;
                    dc.DrawEllipse(effectiveBrush, null, new WpfPoint(pt.X, pt.Y), size / 2, size / 2);
                }
            }
        }

        private void RenderLines(DrawingContext dc, GraphicsDisplayItem item, Pen defaultPen)
        {
            for (int i = 0; i < item.Geometries.Length; i++)
            {
                if (item.Geometries[i] is SdkLineSegment line)
                {
                    bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                    var effectivePen = isPass ? defaultPen : GetPen(item.Style!.GetEffectiveColorArgb(false), GetScaledLineWidth(item.Style.LineWidth));
                    dc.DrawLine(effectivePen, new WpfPoint(line.Start.X, line.Start.Y), new WpfPoint(line.End.X, line.End.Y));
                }
            }
        }

        private void RenderRectangles(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, SolidColorBrush brush, Pen defaultPen)
        {
            for (int i = 0; i < item.Geometries.Length; i++)
            {
                var geom = item.Geometries[i];
                // 逐元素 isPass → OkColor / NgColor（不再硬编码 0xFFFF0000）
                bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                var effectiveColorArgb = style.GetEffectiveColorArgb(isPass);
                var effectivePen = isPass ? defaultPen : GetPen(effectiveColorArgb, GetScaledLineWidth(style.LineWidth));
                var effectiveBrush = item.IsFilled ? GetBrush(effectiveColorArgb) : null;

                if (geom is Rect2d rect)
                    dc.DrawRectangle(effectiveBrush, effectivePen, new WpfRect(rect.X, rect.Y, rect.Width, rect.Height));
                else if (geom is RotatedRect rr)
                {
                    var points = Cv2.BoxPoints(rr);
                    var wpfPoints = Array.ConvertAll(points, p => new WpfPoint(p.X, p.Y));
                    DrawPolygon(dc, wpfPoints, effectiveBrush, effectivePen);
                }
            }
        }

        private void RenderCircles(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, SolidColorBrush brush, Pen defaultPen)
        {
            for (int i = 0; i < item.Geometries.Length; i++)
            {
                if (item.Geometries[i] is Circle2d circle)
                {
                    bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                    var effectiveColorArgb = style.GetEffectiveColorArgb(isPass);
                    var effectivePen = isPass ? defaultPen : GetPen(effectiveColorArgb, GetScaledLineWidth(style.LineWidth));
                    var effectiveBrush = item.IsFilled ? (isPass ? brush : GetBrush(effectiveColorArgb)) : null;
                    dc.DrawEllipse(effectiveBrush, effectivePen,
                        new WpfPoint(circle.Center.X, circle.Center.Y), circle.Radius, circle.Radius);
                }
            }
        }

        private void RenderArcs(DrawingContext dc, GraphicsDisplayItem item, Pen defaultPen)
        {
            for (int i = 0; i < item.Geometries.Length; i++)
            {
                if (item.Geometries[i] is ArcGeometry arc)
                {
                    bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                    var effectivePen = isPass ? defaultPen : GetPen(item.Style!.GetEffectiveColorArgb(false), GetScaledLineWidth(item.Style.LineWidth));
                    var geometry = CreateArcGeometry(arc);
                    dc.DrawGeometry(null, effectivePen, geometry);
                }
            }
        }

        private void RenderPolygons(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, SolidColorBrush defaultBrush, Pen defaultPen)
        {
            for (int i = 0; i < item.Geometries.Length; i++)
            {
                if (item.Geometries[i] is Point2d[] points)
                {
                    bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                    var effectiveColorArgb = style.GetEffectiveColorArgb(isPass);
                    var effectivePen = isPass ? defaultPen : GetPen(effectiveColorArgb, GetScaledLineWidth(style.LineWidth));
                    var effectiveBrush = item.IsFilled ? (isPass ? defaultBrush : GetBrush(effectiveColorArgb)) : null;
                    var wpfPoints = Array.ConvertAll(points, p => new WpfPoint(p.X, p.Y));
                    DrawPolygon(dc, wpfPoints, effectiveBrush, effectivePen);
                }
            }
        }

        private void RenderContours(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, SolidColorBrush defaultBrush, Pen defaultPen)
        {
            for (int i = 0; i < item.Geometries.Length; i++)
            {
                if (item.Geometries[i] is ContourGeometry contour && contour.Points != null && contour.Points.Length >= 2)
                {
                    bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                    var effectiveColorArgb = style.GetEffectiveColorArgb(isPass);
                    var effectivePen = isPass ? defaultPen : GetPen(effectiveColorArgb, GetScaledLineWidth(style.LineWidth));
                    var effectiveBrush = item.IsFilled ? (isPass ? defaultBrush : GetBrush(effectiveColorArgb)) : null;
                    var points = Array.ConvertAll(contour.Points, p => new WpfPoint(p.X, p.Y));
                    if (contour.IsClosed && points.Length >= 3)
                        DrawPolygon(dc, points, effectiveBrush, effectivePen);
                    else
                        DrawPolyline(dc, points, effectivePen);
                }
            }
        }

        private void RenderArrows(DrawingContext dc, GraphicsDisplayItem item, Pen defaultPen)
        {
            for (int i = 0; i < item.Geometries.Length; i++)
            {
                if (item.Geometries[i] is SdkLineSegment line)
                {
                    var style = item.Style;
                    var lineWidth = style != null ? style.LineWidth : 1.0;
                    bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                    var effectivePen = isPass ? defaultPen : GetPen(style!.GetEffectiveColorArgb(false), GetScaledLineWidth(lineWidth));
                    var geometry = CreateArrowGeometry(line.Start, line.End, GetScaledLineWidth(lineWidth));
                    dc.DrawGeometry(null, effectivePen, geometry);
                }
            }
        }

        private void RenderCrosses(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, Pen defaultPen)
        {
            for (int i = 0; i < item.Geometries.Length; i++)
            {
                if (item.Geometries[i] is Point2d center)
                {
                    bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                    var effectivePen = isPass ? defaultPen : GetPen(style.GetEffectiveColorArgb(false), GetScaledLineWidth(style.LineWidth));
                    var s = style.CrossSize;
                    var cx = center.X; var cy = center.Y;
                    dc.DrawLine(effectivePen, new WpfPoint(cx - s, cy), new WpfPoint(cx + s, cy));
                    dc.DrawLine(effectivePen, new WpfPoint(cx, cy - s), new WpfPoint(cx, cy + s));
                }
            }
        }

        private void RenderCoordinateSystem(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style)
        {
            foreach (var geom in item.Geometries)
            {
                if (geom is CoordinateSystemGeometry csg)
                {
                    var xPen = GetPen(csg.XAxisColor, GetScaledLineWidth(style.LineWidth));
                    var yPen = GetPen(csg.YAxisColor, GetScaledLineWidth(style.LineWidth));
                    dc.DrawLine(xPen, new WpfPoint(0, 0), new WpfPoint(csg.AxisLength, 0));
                    dc.DrawLine(yPen, new WpfPoint(0, 0), new WpfPoint(0, csg.AxisLength));
                }
            }
        }

        #endregion

        #region 文本渲染（双模式：统一锚点自动换行 / 独立锚点逐位渲染）

        private void RenderText(DrawingContext dc, TextDisplayItem item)
        {
            if (item.Texts.Length == 0) return;

            var style = item.Style;
            if (style == null) return;

            // Opacity 支持
            dc.PushOpacity(style.Opacity);
            try
            {
                var texts = item.Texts;
                var isPassArray = item.IsPassArray;

                // 检测是否为独立定位模式：所有文本的 X/Y 是否完全相同
                bool useIndividualPositions = texts.Any(t =>
                    Math.Abs(t.X - texts[0].X) > 0.001 ||
                    Math.Abs(t.Y - texts[0].Y) > 0.001);

                if (useIndividualPositions)
                {
                    // ===== 独立锚点模式：每个文本使用自己的 X/Y，不折行 =====
                    for (int i = 0; i < texts.Length; i++)
                    {
                        var td = texts[i];
                        bool isPass = isPassArray.Length > i ? isPassArray[i] : true;
                        var color = ArgbToColor(style.GetEffectiveColorArgb(isPass));
                        var formattedText = CreateFormattedText(td.Content, color, style.FontSize, style.IsBold);
                        dc.DrawText(formattedText, new WpfPoint(td.X, td.Y));
                    }
                }
                else
                {
                    // ===== 统一锚点 + 自动换行模式：首个文本的坐标即锚点 =====
                    // 从 style（实时引用）读取位置，确保位置随样式同步更新
                    double baseX = style.PositionX;
                    double baseY = style.PositionY;
                    var lineHeight = style.FontSize * 1.4;

                    for (int i = 0; i < texts.Length; i++)
                    {
                        var td = texts[i];
                        double x = baseX;
                        double y = baseY + i * lineHeight;

                        bool isPass = isPassArray.Length > i ? isPassArray[i] : true;
                        var color = ArgbToColor(style.GetEffectiveColorArgb(isPass));
                        var formattedText = CreateFormattedText(td.Content, color, style.FontSize, style.IsBold);
                        dc.DrawText(formattedText, new WpfPoint(x, y));
                    }
                }
            }
            finally
            {
                dc.Pop(); // 恢复 Opacity 状态
            }
        }

        #endregion

        #region 辅助方法

        private static void DrawPolygon(DrawingContext dc, WpfPoint[] points, Brush? fill, Pen stroke)
        {
            if (points == null || points.Length < 2) return;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], fill != null, fill != null);
                for (int i = 1; i < points.Length; i++)
                    ctx.LineTo(points[i], true, true);
            }
            geometry.Freeze();
            dc.DrawGeometry(fill, stroke, geometry);
        }

        private static void DrawPolyline(DrawingContext dc, WpfPoint[] points, Pen pen)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], false, false);
                for (int i = 1; i < points.Length; i++)
                    ctx.LineTo(points[i], true, true);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }

        private SolidColorBrush GetBrush(uint argb)
        {
            if (_brushCache.TryGetValue(argb, out var cached)) return cached;
            var brush = new SolidColorBrush(ArgbToColor(argb));
            brush.Freeze();
            _brushCache[argb] = brush;
            return brush;
        }

        private Pen GetPen(uint argb, double thickness)
        {
            var key = argb ^ ((uint)(thickness * 100) << 16);
            if (_penCache.TryGetValue(key, out var cached)) return cached;
            var brush = GetBrush(argb);
            var pen = new Pen(brush, thickness);
            pen.Freeze();
            _penCache[key] = pen;
            return pen;
        }

        private static Color ArgbToColor(uint argb) => Color.FromArgb(
            (byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));

        private static FormattedText CreateFormattedText(string text, Color color, double fontSize, bool isBold)
        {
            var typeface = new Typeface(new FontFamily("Segoe UI"),
                FontStyles.Normal, isBold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
            return new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, fontSize, new SolidColorBrush(color),
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
        }

        private static StreamGeometry CreateArcGeometry(ArcGeometry arc)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var startRad = arc.StartAngle * Math.PI / 180;
                var endRad = arc.EndAngle * Math.PI / 180;
                ctx.BeginFigure(new WpfPoint(
                    arc.Center.X + arc.Radius * Math.Cos(startRad),
                    arc.Center.Y + arc.Radius * Math.Sin(startRad)), false, false);
                ctx.ArcTo(
                    new WpfPoint(arc.Center.X + arc.Radius * Math.Cos(endRad),
                                arc.Center.Y + arc.Radius * Math.Sin(endRad)),
                    new WpfSize(arc.Radius, arc.Radius), 0, false,
                    SweepDirection.Clockwise, true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        private static StreamGeometry CreateArrowGeometry(Point2d start, Point2d end, double thickness)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new WpfPoint(start.X, start.Y), false, false);
                ctx.LineTo(new WpfPoint(end.X, end.Y), true, true);
                var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
                var arrowSize = thickness * 4;
                double a1 = angle + Math.PI * 0.85, a2 = angle - Math.PI * 0.85;
                ctx.BeginFigure(new WpfPoint(end.X, end.Y), true, false);
                ctx.LineTo(new WpfPoint(end.X + arrowSize * Math.Cos(a1), end.Y + arrowSize * Math.Sin(a1)), true, true);
                ctx.BeginFigure(new WpfPoint(end.X, end.Y), true, false);
                ctx.LineTo(new WpfPoint(end.X + arrowSize * Math.Cos(a2), end.Y + arrowSize * Math.Sin(a2)), true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        #endregion
    }
}

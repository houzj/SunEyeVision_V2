using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
// 解决 System.Windows.Media.LineSegment 与 SDK LineSegment 的命名冲突
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
        private readonly Dictionary<long, Pen> _penCache = new();

        // 视口裁剪（图像坐标系）
        private double _viewLeft, _viewTop, _viewRight, _viewBottom;
        private bool _hasViewport;

        // LOD 阈值：当缩放倍率低于此值且 Cross 数量超过阈值时启用密度采样
        private const double LOD_ZOOM_THRESHOLD = 0.15;
        private const int LOD_MAX_CROSSES = 2000;

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

        /// <summary>设置视口范围（图像坐标系），用于视口裁剪</summary>
        public void SetViewport(double left, double top, double right, double bottom)
        {
            _viewLeft = left;
            _viewTop = top;
            _viewRight = right;
            _viewBottom = bottom;
            _hasViewport = true;
            InvalidateVisual();
        }

        private double GetScaledLineWidth(double rawWidth, double power = 1.0) =>
            _viewportScale > 0.001 ? rawWidth / Math.Pow(_viewportScale, power) : rawWidth;

        #endregion

        #region 渲染

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // ★ 不再清空 _brushCache / _penCache，缓存可跨渲染复用
            // Pen 键已包含厚度信息，不同线宽自动对应不同缓存项

            // 渲染顺序：区域(底层) → 工具输出(中层) → 高亮(顶层)
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

            dc.PushOpacity(style.Opacity);
            try
            {
                var defaultColorArgb = style.GetEffectiveColorArgb(true);
                var brush = GetBrush(defaultColorArgb);
                var pen = GetPen(defaultColorArgb, GetScaledLineWidth(style.LineWidth));

                switch (item.Type)
                {
                    case GraphicsType.Point:
                        RenderPoints(dc, item, style, pen); break;
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

        private void RenderPoints(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, Pen defaultPen)
        {
            bool isUnified = item.RenderMode == DisplayRenderMode.Unified;
            double s = GetScaledLineWidth(2.5, 0.7);
            double crossThickness = GetScaledLineWidth(1.0, 0.7);
            var defaultColorArgb = style.GetEffectiveColorArgb(true);
            var crossPen = GetPen(defaultColorArgb, crossThickness);

            int totalCount = item.Geometries.Length;

            bool useLod = _viewportScale < LOD_ZOOM_THRESHOLD && totalCount > LOD_MAX_CROSSES;
            int lodStep = useLod ? Math.Max(1, totalCount / LOD_MAX_CROSSES) : 1;

            if (isUnified)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    for (int i = 0; i < totalCount; i += lodStep)
                    {
                        if (item.Geometries[i] is Point pt)
                        {
                            if (_hasViewport && !IsInViewport(pt))
                                continue;

                            double x = pt.X, y = pt.Y;
                            ctx.BeginFigure(new Point(x - s, y), false, false);
                            ctx.LineTo(new Point(x + s, y), true, false);
                            ctx.BeginFigure(new Point(x, y - s), false, false);
                            ctx.LineTo(new Point(x, y + s), true, false);
                        }
                    }
                }
                geometry.Freeze();
                dc.DrawGeometry(null, crossPen, geometry);
            }
            else
            {
                var ngColorArgb = style.GetEffectiveColorArgb(false);
                var ngPen = GetPen(ngColorArgb, crossThickness);

                var okGeo = new StreamGeometry();
                var ngGeo = new StreamGeometry();
                using (var okCtx = okGeo.Open())
                using (var ngCtx = ngGeo.Open())
                {
                    for (int i = 0; i < totalCount; i += lodStep)
                    {
                        if (item.Geometries[i] is Point pt)
                        {
                            if (_hasViewport && !IsInViewport(pt))
                                continue;

                            double x = pt.X, y = pt.Y;
                            bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                            var ctx = isPass ? okCtx : ngCtx;
                            ctx.BeginFigure(new Point(x - s, y), false, false);
                            ctx.LineTo(new Point(x + s, y), true, false);
                            ctx.BeginFigure(new Point(x, y - s), false, false);
                            ctx.LineTo(new Point(x, y + s), true, false);
                        }
                    }
                }
                okGeo.Freeze();
                ngGeo.Freeze();
                dc.DrawGeometry(null, crossPen, okGeo);
                dc.DrawGeometry(null, ngPen, ngGeo);
            }
        }

        private void RenderLines(DrawingContext dc, GraphicsDisplayItem item, Pen defaultPen)
        {
            bool isUnified = item.RenderMode == DisplayRenderMode.Unified;
            var scale = GetScaledLineWidth(item.Style?.LineWidth ?? 1.0);

            if (isUnified)
            {
                var okPoints = new List<Point[]>();
                for (int i = 0; i < item.Geometries.Length; i++)
                {
                    if (item.Geometries[i] is SdkLineSegment line)
                        okPoints.Add(new[] { line.Start, line.End });
                }
                DrawMultipleFigures(dc, okPoints, null, defaultPen, false);
            }
            else
            {
                var ngColorArgb = item.Style!.GetEffectiveColorArgb(false);
                var ngPen = GetPen(ngColorArgb, scale);
                var okPoints = new List<Point[]>();
                var ngPoints = new List<Point[]>();

                for (int i = 0; i < item.Geometries.Length; i++)
                {
                    if (item.Geometries[i] is SdkLineSegment line)
                    {
                        var pts = new[] { line.Start, line.End };
                        bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                        if (isPass)
                            okPoints.Add(pts);
                        else
                            ngPoints.Add(pts);
                    }
                }
                DrawMultipleFigures(dc, okPoints, null, defaultPen, false);
                DrawMultipleFigures(dc, ngPoints, null, ngPen, false);
            }
        }

        private void RenderRectangles(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, SolidColorBrush brush, Pen defaultPen)
        {
            bool isUnified = item.RenderMode == DisplayRenderMode.Unified;
            var scale = GetScaledLineWidth(style.LineWidth);
            var ngColorArgb = style.GetEffectiveColorArgb(false);
            var ngPen = GetPen(ngColorArgb, scale);

            for (int i = 0; i < item.Geometries.Length; i++)
            {
                var geom = item.Geometries[i];
                bool isPass = isUnified || item.IsPassArray.Length <= i || item.IsPassArray[i];
                var effectivePen = isPass ? defaultPen : ngPen;
                var effectiveBrush = item.IsFilled
                    ? (isPass ? brush : GetBrush(ngColorArgb))
                    : null;

                if (geom is Rect rect)
                    dc.DrawRectangle(effectiveBrush, effectivePen, rect);
                else if (geom is Point[] polygonPoints && polygonPoints.Length >= 3)
                {
                    DrawPolygon(dc, polygonPoints, effectiveBrush, effectivePen);
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
                        circle.Center, circle.Radius, circle.Radius);
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
            bool isUnified = item.RenderMode == DisplayRenderMode.Unified;
            var scale = GetScaledLineWidth(style.LineWidth);
            var ngColorArgb = style.GetEffectiveColorArgb(false);
            var ngPen = GetPen(ngColorArgb, scale);
            var ngBrush = item.IsFilled ? GetBrush(ngColorArgb) : null;

            if (isUnified)
            {
                var allPoints = new List<Point[]>();
                for (int i = 0; i < item.Geometries.Length; i++)
                {
                    if (item.Geometries[i] is Point[] points && points.Length >= 2)
                        allPoints.Add(points);
                }
                DrawMultipleFigures(dc, allPoints, item.IsFilled ? defaultBrush : null, defaultPen, true);
            }
            else
            {
                var okPoints = new List<Point[]>();
                var ngPoints = new List<Point[]>();

                for (int i = 0; i < item.Geometries.Length; i++)
                {
                    if (item.Geometries[i] is Point[] points && points.Length >= 2)
                    {
                        bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                        if (isPass)
                            okPoints.Add(points);
                        else
                            ngPoints.Add(points);
                    }
                }
                DrawMultipleFigures(dc, okPoints, item.IsFilled ? defaultBrush : null, defaultPen, true);
                DrawMultipleFigures(dc, ngPoints, ngBrush, ngPen, true);
            }
        }

        private void RenderContours(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, SolidColorBrush defaultBrush, Pen defaultPen)
        {
            bool isUnified = item.RenderMode == DisplayRenderMode.Unified;
            var scale = GetScaledLineWidth(style.LineWidth);
            var ngColorArgb = style.GetEffectiveColorArgb(false);
            var ngPen = GetPen(ngColorArgb, scale);
            var ngBrush = item.IsFilled ? GetBrush(ngColorArgb) : null;

            // 分离有/无变换的轮廓：有变换的逐个绘制，无变换的批量合并以保持性能
            var batchPolygons = new List<Point[]>();
            var batchPolylines = new List<Point[]>();
            var batchOkPolygons = new List<Point[]>();
            var batchNgPolygons = new List<Point[]>();
            var batchOkPolylines = new List<Point[]>();
            var batchNgPolylines = new List<Point[]>();

            for (int i = 0; i < item.Geometries.Length; i++)
            {
                if (item.Geometries[i] is ContourGeometry contour && contour.Points != null)
                {
                    var pts = contour.Points;
                    if (pts.Length < 2) continue;

                    bool isPass = isUnified || (item.IsPassArray.Length > i ? item.IsPassArray[i] : true);
                    bool isClosed = contour.IsClosed && pts.Length >= 3;

                    if (contour.Transform.HasValue)
                    {
                        // 有变换的轮廓逐个绘制，各自应用 PushTransform
                        dc.PushTransform(new MatrixTransform(contour.Transform.Value));
                        if (isClosed)
                            DrawPolygon(dc, pts, (isPass && item.IsFilled) ? defaultBrush : null, isPass ? defaultPen : ngPen);
                        else
                            DrawPolyline(dc, pts, isPass ? defaultPen : ngPen);
                        dc.Pop();
                    }
                    else
                    {
                        // 无变换的轮廓继续批量合并
                        if (isUnified)
                        {
                            if (isClosed) batchPolygons.Add(pts);
                            else batchPolylines.Add(pts);
                        }
                        else
                        {
                            if (isPass)
                            {
                                if (isClosed) batchOkPolygons.Add(pts);
                                else batchOkPolylines.Add(pts);
                            }
                            else
                            {
                                if (isClosed) batchNgPolygons.Add(pts);
                                else batchNgPolylines.Add(pts);
                            }
                        }
                    }
                }
            }

            // 批量绘制无变换的轮廓
            if (isUnified)
            {
                DrawMultipleFigures(dc, batchPolygons, item.IsFilled ? defaultBrush : null, defaultPen, true);
                DrawMultipleFigures(dc, batchPolylines, null, defaultPen, false);
            }
            else
            {
                DrawMultipleFigures(dc, batchOkPolygons, item.IsFilled ? defaultBrush : null, defaultPen, true);
                DrawMultipleFigures(dc, batchOkPolylines, null, defaultPen, false);
                DrawMultipleFigures(dc, batchNgPolygons, ngBrush, ngPen, true);
                DrawMultipleFigures(dc, batchNgPolylines, null, ngPen, false);
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
            bool isUnified = item.RenderMode == DisplayRenderMode.Unified;
            var scale = GetScaledLineWidth(style.LineWidth);
            var ngColorArgb = style.GetEffectiveColorArgb(false);
            var ngPen = GetPen(ngColorArgb, scale);
            var s = GetScaledLineWidth(style.CrossSize, 0.7);

            int totalCount = item.Geometries.Length;

            bool useLod = _viewportScale < LOD_ZOOM_THRESHOLD && totalCount > LOD_MAX_CROSSES;
            int lodStep = useLod ? Math.Max(1, totalCount / LOD_MAX_CROSSES) : 1;

            if (isUnified)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    for (int i = 0; i < totalCount; i += lodStep)
                    {
                        if (item.Geometries[i] is Point center)
                        {
                            if (_hasViewport && !IsInViewport(center))
                                continue;

                            double cx = center.X, cy = center.Y;
                            ctx.BeginFigure(new Point(cx - s, cy), false, false);
                            ctx.LineTo(new Point(cx + s, cy), true, false);
                            ctx.BeginFigure(new Point(cx, cy - s), false, false);
                            ctx.LineTo(new Point(cx, cy + s), true, false);
                        }
                    }
                }
                geometry.Freeze();
                dc.DrawGeometry(null, defaultPen, geometry);
            }
            else
            {
                var okGeo = new StreamGeometry();
                var ngGeo = new StreamGeometry();
                using (var okCtx = okGeo.Open())
                using (var ngCtx = ngGeo.Open())
                {
                    for (int i = 0; i < totalCount; i += lodStep)
                    {
                        if (item.Geometries[i] is Point center)
                        {
                            if (_hasViewport && !IsInViewport(center))
                                continue;

                            double cx = center.X, cy = center.Y;
                            bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                            var ctx = isPass ? okCtx : ngCtx;
                            ctx.BeginFigure(new Point(cx - s, cy), false, false);
                            ctx.LineTo(new Point(cx + s, cy), true, false);
                            ctx.BeginFigure(new Point(cx, cy - s), false, false);
                            ctx.LineTo(new Point(cx, cy + s), true, false);
                        }
                    }
                }
                okGeo.Freeze();
                ngGeo.Freeze();
                dc.DrawGeometry(null, defaultPen, okGeo);
                dc.DrawGeometry(null, ngPen, ngGeo);
            }
        }

        /// <summary>快速视口内测试（图像坐标系）</summary>
        private bool IsInViewport(Point pt) =>
            pt.X >= _viewLeft && pt.X <= _viewRight &&
            pt.Y >= _viewTop && pt.Y <= _viewBottom;

        private void RenderCoordinateSystem(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style)
        {
            foreach (var geom in item.Geometries)
            {
                if (geom is CoordinateSystemGeometry csg)
                {
                    var xPen = GetPen(csg.XAxisColor, GetScaledLineWidth(style.LineWidth));
                    var yPen = GetPen(csg.YAxisColor, GetScaledLineWidth(style.LineWidth));

                    // 使用 Matrix.Transform
                    var origin = csg.TransformMatrix.Transform(new Point(0, 0));
                    var xEnd = csg.TransformMatrix.Transform(new Point(csg.AxisLength, 0));
                    var yEnd = csg.TransformMatrix.Transform(new Point(0, csg.AxisLength));

                    dc.DrawLine(xPen, origin, xEnd);
                    dc.DrawLine(yPen, origin, yEnd);
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

            dc.PushOpacity(style.Opacity);
            try
            {
                var texts = item.Texts;
                var isPassArray = item.IsPassArray;

                bool useIndividualPositions = texts.Any(t =>
                    Math.Abs(t.X - texts[0].X) > 0.001 ||
                    Math.Abs(t.Y - texts[0].Y) > 0.001);

                var scaledFontSize = _viewportScale > 0.001 ? style.FontSize / _viewportScale : style.FontSize;
                var lineHeight = scaledFontSize * 1.4;

                if (useIndividualPositions)
                {
                    // 混合模式：连续同锚点文字自动分组，组内 Y 偏移实现换行
                    int idx = 0;
                    while (idx < texts.Length)
                    {
                        var anchorText = texts[idx];
                        double anchorX = anchorText.X;
                        double anchorY = anchorText.Y;

                        int groupStart = idx;
                        while (idx < texts.Length &&
                               Math.Abs(texts[idx].X - anchorX) < 0.001 &&
                               Math.Abs(texts[idx].Y - anchorY) < 0.001)
                        {
                            idx++;
                        }
                        int groupCount = idx - groupStart;

                        for (int j = 0; j < groupCount; j++)
                        {
                            var td = texts[groupStart + j];
                            bool isPass = isPassArray.Length > groupStart + j
                                ? isPassArray[groupStart + j] : true;
                            var color = ArgbToColor(style.GetEffectiveColorArgb(isPass));
                            var formattedText = CreateFormattedText(td.Content, color,
                                scaledFontSize, style.IsBold);
                            dc.DrawText(formattedText,
                                new Point(anchorX, anchorY + j * lineHeight));
                        }
                    }
                }
                else
                {
                    double baseX = texts[0].X;
                    double baseY = texts[0].Y;

                    for (int i = 0; i < texts.Length; i++)
                    {
                        var td = texts[i];
                        double x = baseX;
                        double y = baseY + i * lineHeight;

                        bool isPass = isPassArray.Length > i ? isPassArray[i] : true;
                        var color = ArgbToColor(style.GetEffectiveColorArgb(isPass));
                        var formattedText = CreateFormattedText(td.Content, color, scaledFontSize, style.IsBold);
                        dc.DrawText(formattedText, new Point(x, y));
                    }
                }
            }
            finally
            {
                dc.Pop();
            }
        }

        #endregion

        #region 辅助方法

        private static void DrawPolygon(DrawingContext dc, Point[] points, Brush? fill, Pen stroke)
        {
            if (points == null || points.Length < 2) return;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], fill != null, true);
                for (int i = 1; i < points.Length; i++)
                    ctx.LineTo(points[i], true, true);
            }
            geometry.Freeze();
            dc.DrawGeometry(fill, stroke, geometry);
        }

        private static void DrawPolyline(DrawingContext dc, Point[] points, Pen pen)
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

        private static void DrawMultipleFigures(DrawingContext dc, List<Point[]> allPoints, Brush? fill, Pen stroke, bool closeFigures)
        {
            if (allPoints == null || allPoints.Count == 0) return;

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                foreach (var points in allPoints)
                {
                    if (points.Length < 2) continue;
                    bool isFilled = fill != null;
                    bool isClosed = closeFigures && points.Length >= 3;
                    ctx.BeginFigure(points[0], isFilled, isClosed);
                    for (int i = 1; i < points.Length; i++)
                        ctx.LineTo(points[i], true, true);
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(fill, stroke, geometry);
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
            var thicknessInt = (uint)(thickness * 100);
            var key = ((long)argb) | ((long)thicknessInt << 32);
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
                ctx.BeginFigure(new Point(
                    arc.Center.X + arc.Radius * Math.Cos(startRad),
                    arc.Center.Y + arc.Radius * Math.Sin(startRad)), false, false);
                ctx.ArcTo(
                    new Point(arc.Center.X + arc.Radius * Math.Cos(endRad),
                              arc.Center.Y + arc.Radius * Math.Sin(endRad)),
                    new Size(arc.Radius, arc.Radius), 0, false,
                    SweepDirection.Clockwise, true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        private static StreamGeometry CreateArrowGeometry(Point start, Point end, double thickness)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(start, false, false);
                ctx.LineTo(end, true, true);
                var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
                var arrowSize = thickness * 6;
                double a1 = angle + Math.PI * 0.85, a2 = angle - Math.PI * 0.85;
                ctx.BeginFigure(end, true, false);
                ctx.LineTo(new Point(end.X + arrowSize * Math.Cos(a1), end.Y + arrowSize * Math.Sin(a1)), true, true);
                ctx.BeginFigure(end, true, false);
                ctx.LineTo(new Point(end.X + arrowSize * Math.Cos(a2), end.Y + arrowSize * Math.Sin(a2)), true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        #endregion
    }
}

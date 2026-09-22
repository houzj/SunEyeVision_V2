using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Logging;
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
        private readonly Dictionary<BatchedContourGeometry, IReadOnlyDictionary<(int X, int Y), IReadOnlyList<StreamGeometry>>> _batchGeometryCache = new();
        private CancellationTokenSource? _batchBuildCancellation;
        private int _batchBuildVersion;

        // 视口裁剪（图像坐标系）
        private double _viewLeft, _viewTop, _viewRight, _viewBottom;
        private bool _hasViewport;

        // 渲染诊断：图元数量只由工具参数（如查找数量）决定，渲染层不做任何数量截断。
        private const long RenderSlowThresholdMs = 100;
        private readonly Stopwatch _renderStopwatch = new();
        private int _diagItems, _diagContours, _diagCrosses, _diagTexts;
        private int _diagDrawCalls, _diagBlocks, _diagContourVertices;
        private int _diagLastItems = -1, _diagLastContours = -1, _diagLastCrosses = -1, _diagLastTexts = -1;
        private int _diagLastDrawCalls = -1, _diagLastBlocks = -1, _diagLastContourVertices = -1;
        private long _diagLastSlowLogTimestamp;

        // 文本绘制缓存（按层：区域层 / 工具输出层 / 高亮层）：
        // 文本排版取决于缩放、内容与样式，三者都不变时复用已录制的绘制内容，一次提交完成。
        private const int TextCacheLayerCount = 3;
        private readonly DrawingGroup?[] _textDrawingCaches = new DrawingGroup?[TextCacheLayerCount];
        private readonly int[] _textCacheTextCounts = new int[TextCacheLayerCount];
        private readonly double[] _textCacheZooms = new double[TextCacheLayerCount];
        private readonly int[] _textCacheVersions = new int[TextCacheLayerCount];
        private int _textSourceVersion;

        #endregion

        #region 公共接口

        /// <summary>设置显示收集器（工具输出）</summary>
        public void SetDisplayCollector(DisplayCollector? collector)
        {
            UnsubscribeItems(_collector?.Items);
            _collector = collector;
            _textSourceVersion++;
            var buildVersion = ++_batchBuildVersion;
            CancelBatchBuild();
            _batchGeometryCache.Clear();
            SubscribeItems(_collector?.Items);
            ScheduleBatchGeometryBuild(GetAllItems(), buildVersion);
            InvalidateVisual();
        }

        /// <summary>设置区域显示项（检测区域覆盖）</summary>
        public void SetRegionItems(IEnumerable<DisplayItemBase>? items)
        {
            UnsubscribeItems(_regionItems);
            _regionItems = items?.ToList();
            _textSourceVersion++;
            var buildVersion = ++_batchBuildVersion;
            CancelBatchBuild();
            _batchGeometryCache.Clear();
            ScheduleBatchGeometryBuild(GetAllItems(), buildVersion);
            SubscribeItems(_regionItems);
            InvalidateVisual();
        }

        /// <summary>设置显示项（兼容旧接口，内部转为 collector 模式）</summary>
        public void SetItems(IReadOnlyList<DisplayItemBase>? items)
        {
            UnsubscribeItems(_collector?.Items);
            _collector = null; // 直接设置 items 时清除 collector 引用
            _textSourceVersion++;
            var buildVersion = ++_batchBuildVersion;
            CancelBatchBuild();
            _batchGeometryCache.Clear();
            UnsubscribeItems(_regionItems);
            _regionItems = items;
            ScheduleBatchGeometryBuild(GetAllItems(), buildVersion);
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
            _textSourceVersion++;
            ++_batchBuildVersion;
            CancelBatchBuild();
            _batchGeometryCache.Clear();
            InvalidateVisual();
        }

        private void ScheduleBatchGeometryBuild(IReadOnlyList<DisplayItemBase>? items, int buildVersion)
        {
            var batches = items?
                .OfType<GraphicsDisplayItem>()
                .SelectMany(i => i.Geometries.OfType<BatchedContourGeometry>())
                .Distinct()
                .ToList();
            if (batches == null || batches.Count == 0) return;

            var cancellation = new CancellationTokenSource();
            _batchBuildCancellation = cancellation;
            _ = Task.Run(() =>
            {
                var built = new List<(BatchedContourGeometry Batch, IReadOnlyDictionary<(int X, int Y), IReadOnlyList<StreamGeometry>> Geometries)>();
                foreach (var batch in batches)
                {
                    if (cancellation.IsCancellationRequested) return;
                    var geometries = BuildBatchGeometries(batch);
                    if (geometries.Count > 0)
                        built.Add((batch, geometries));
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        if (ReferenceEquals(_batchBuildCancellation, cancellation))
                            _batchBuildCancellation = null;
                        cancellation.Dispose();
                        return;
                    }
                    if (buildVersion != _batchBuildVersion)
                    {
                        if (ReferenceEquals(_batchBuildCancellation, cancellation))
                            _batchBuildCancellation = null;
                        cancellation.Dispose();
                        return;
                    }
                    foreach (var (batch, geometries) in built)
                        _batchGeometryCache[batch] = geometries;
                    if (ReferenceEquals(_batchBuildCancellation, cancellation))
                        _batchBuildCancellation = null;
                    cancellation.Dispose();
                    InvalidateVisual();
                }));
            }, cancellation.Token);
        }

        private void CancelBatchBuild()
        {
            var cancellation = _batchBuildCancellation;
            _batchBuildCancellation = null;
            if (cancellation == null) return;
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 后台完成回调可能已先释放取消源，清理路径必须幂等。
            }
        }

        private IReadOnlyList<DisplayItemBase> GetAllItems()
        {
            return (_collector?.Items ?? Array.Empty<DisplayItemBase>())
                .Concat(_regionItems ?? Array.Empty<DisplayItemBase>())
                .Concat(_highlightItems ?? Array.Empty<DisplayItemBase>())
                .ToList();
        }

        private const int BatchGeometryChunkSize = 1000;
        private const double GeometryTileSize = 512.0;

        private static IReadOnlyDictionary<(int X, int Y), IReadOnlyList<StreamGeometry>> BuildBatchGeometries(BatchedContourGeometry batch)
        {
            if (batch.Contours.Count == 0)
                return new Dictionary<(int X, int Y), IReadOnlyList<StreamGeometry>>();
            var tiles = new Dictionary<(int X, int Y), List<StreamGeometry>>();
            for (var start = 0; start < batch.Contours.Count; start += BatchGeometryChunkSize)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    var end = Math.Min(start + BatchGeometryChunkSize, batch.Contours.Count);
                    for (var i = start; i < end; i++)
                    {
                        var points = batch.Contours[i];
                        if (points == null || points.Length < 2) continue;
                        var closed = batch.IsClosed && points.Length >= 3;
                        ctx.BeginFigure(points[0], false, closed);
                        for (var pointIndex = 1; pointIndex < points.Length; pointIndex++)
                            ctx.LineTo(points[pointIndex], true, true);
                    }
                }
                geometry.Freeze();
                var bounds = geometry.Bounds;
                var minX = (int)Math.Floor(bounds.Left / GeometryTileSize);
                var maxX = (int)Math.Floor(bounds.Right / GeometryTileSize);
                var minY = (int)Math.Floor(bounds.Top / GeometryTileSize);
                var maxY = (int)Math.Floor(bounds.Bottom / GeometryTileSize);
                for (var tileX = minX; tileX <= maxX; tileX++)
                for (var tileY = minY; tileY <= maxY; tileY++)
                {
                    var key = (tileX, tileY);
                    if (!tiles.TryGetValue(key, out var tileGeometries))
                        tiles[key] = tileGeometries = new List<StreamGeometry>();
                    tileGeometries.Add(geometry);
                }
            }
            return tiles.ToDictionary(p => p.Key, p => (IReadOnlyList<StreamGeometry>)p.Value);
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

        private void OnItemPropertyChanged(object? sender, EventArgs e)
        {
            // 样式（颜色/字号/可见性等）或图元属性变化 → 文本绘制缓存必须失效（NotifyChanged 会转发样式变化）。
            _textSourceVersion++;
            InvalidateVisual();
        }

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
            ViewportScaling.Power(_viewportScale, rawWidth, power);

        #endregion

        #region 渲染

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // ★ 不再清空 _brushCache / _penCache，缓存可跨渲染复用
            // Pen 键已包含厚度信息，不同线宽自动对应不同缓存项

            _diagItems = _diagContours = _diagCrosses = _diagTexts = 0;
            _diagDrawCalls = _diagBlocks = _diagContourVertices = 0;
            // GetDpi(this) 在本元素未连接可视树时返回默认 96 DPI，不依赖 MainWindow。
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            _renderStopwatch.Restart();

            // 渲染顺序：区域(底层) → 工具输出(中层) → 高亮(顶层)
            // 每层内：图形项立即绘制，文本项收集后统一分组排版（同锚点协同换行）
            RenderLayer(dc, _regionItems, 0);
            RenderLayer(dc, _collector?.Items, 1);
            RenderLayer(dc, _highlightItems, 2);

            _renderStopwatch.Stop();
            WriteRenderDiagnostics();
        }

        /// <summary>
        /// 渲染诊断：输出本帧实际提交的图元量与耗时。
        /// 内容变化时记录一条 Info；单帧耗时超阈值时按 1 秒节流升为 Warning。
        /// </summary>
        private void WriteRenderDiagnostics()
        {
            var elapsedMs = _renderStopwatch.ElapsedMilliseconds;
            var isSlow = elapsedMs >= RenderSlowThresholdMs;
            var contentChanged = _diagItems != _diagLastItems || _diagContours != _diagLastContours
                || _diagCrosses != _diagLastCrosses || _diagTexts != _diagLastTexts
                || _diagDrawCalls != _diagLastDrawCalls || _diagBlocks != _diagLastBlocks
                || _diagContourVertices != _diagLastContourVertices;
            if (!isSlow && !contentChanged) return;

            var now = Stopwatch.GetTimestamp();
            if (isSlow)
            {
                if (now - _diagLastSlowLogTimestamp < Stopwatch.Frequency) return;
                _diagLastSlowLogTimestamp = now;
            }

            _diagLastItems = _diagItems;
            _diagLastContours = _diagContours;
            _diagLastCrosses = _diagCrosses;
            _diagLastTexts = _diagTexts;
            _diagLastDrawCalls = _diagDrawCalls;
            _diagLastBlocks = _diagBlocks;
            _diagLastContourVertices = _diagContourVertices;

            VisionLogger.Instance.Log(
                isSlow ? LogLevel.Warning : LogLevel.Info,
                $"[RenderDiag] items={_diagItems} contours={_diagContours} crosses={_diagCrosses} texts={_diagTexts}"
                + $" drawCalls={_diagDrawCalls} blocks={_diagBlocks} contourVerts={_diagContourVertices} renderMs={elapsedMs}",
                "ResultRenderLayer");
        }

        /// <summary>
        /// 渲染一层：图形项按原顺序立即绘制；文本项收集后统一分组排版，
        /// 保证"同一锚点的多条文本（可跨 TextStyle）"协同换行而非相互重叠。
        /// </summary>
        private void RenderLayer(DrawingContext dc, IReadOnlyList<DisplayItemBase>? items, int layerIndex)
        {
            if (items == null) return;

            List<TextDisplayItem>? texts = null;
            foreach (var item in items)
            {
                if (!item.IsVisible) continue;
                _diagItems++;

                if (item is TextDisplayItem t)
                {
                    (texts ??= new List<TextDisplayItem>()).Add(t);
                    continue;
                }
                RenderItem(dc, item);
            }

            if (texts != null)
                RenderTextsGrouped(dc, texts, layerIndex);
        }

        private void RenderItem(DrawingContext dc, DisplayItemBase item)
        {
            if (item is GraphicsDisplayItem g)
                RenderGraphics(dc, g);
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
                var pen = GetPen(defaultColorArgb, GetScaledLineWidth(style.LineWidth), style.LineDashStyle);

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
            // 点标记同样接入 Clamped（放大不低于基准，缩小时最多放大 1.5 倍）。
            double s = ViewportScaling.Clamped(_viewportScale, 2.5, ViewportScaling.MarkerPower, 2.5, 3.75);
            double crossThickness = ViewportScaling.Clamped(_viewportScale, 1.0, ViewportScaling.MarkerPower, 1.0, 1.5);
            var defaultColorArgb = style.GetEffectiveColorArgb(true);
            var crossPen = GetPen(defaultColorArgb, crossThickness);

            int totalCount = item.Geometries.Length;

            if (isUnified)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    for (int i = 0; i < totalCount; i++)
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
                _diagDrawCalls++;
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
                    for (int i = 0; i < totalCount; i++)
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
                _diagDrawCalls += 2;
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
                if (okPoints.Count > 0) _diagDrawCalls++;
                DrawMultipleFigures(dc, okPoints, null, defaultPen, false);
            }
            else
            {
                var ngColorArgb = item.Style!.GetEffectiveColorArgb(false);
                var ngPen = GetPen(ngColorArgb, scale, item.Style.LineDashStyle);
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
                if (okPoints.Count > 0) _diagDrawCalls++;
                DrawMultipleFigures(dc, okPoints, null, defaultPen, false);
                if (ngPoints.Count > 0) _diagDrawCalls++;
                DrawMultipleFigures(dc, ngPoints, null, ngPen, false);
            }
        }

        private void RenderRectangles(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, SolidColorBrush brush, Pen defaultPen)
        {
            bool isUnified = item.RenderMode == DisplayRenderMode.Unified;
            var scale = GetScaledLineWidth(style.LineWidth);
            var ngColorArgb = style.GetEffectiveColorArgb(false);
            var ngPen = GetPen(ngColorArgb, scale, style.LineDashStyle);

            for (int i = 0; i < item.Geometries.Length; i++)
            {
                var geom = item.Geometries[i];
                bool isPass = isUnified || item.IsPassArray.Length <= i || item.IsPassArray[i];
                var effectivePen = isPass ? defaultPen : ngPen;
                var effectiveBrush = item.IsFilled
                    ? (isPass ? brush : GetBrush(ngColorArgb))
                    : null;

                if (geom is Rect rect)
                {
                    _diagDrawCalls++;
                    dc.DrawRectangle(effectiveBrush, effectivePen, rect);
                }
                else if (geom is Point[] polygonPoints && polygonPoints.Length >= 3)
                {
                    _diagDrawCalls++;
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
                    var effectivePen = isPass ? defaultPen : GetPen(effectiveColorArgb, GetScaledLineWidth(style.LineWidth), style.LineDashStyle);
                    var effectiveBrush = item.IsFilled ? (isPass ? brush : GetBrush(effectiveColorArgb)) : null;
                    _diagDrawCalls++;
                    dc.DrawEllipse(effectiveBrush, effectivePen,
                        circle.Center, circle.Radius, circle.Radius);
                }
            }
        }

        private void RenderArcs(DrawingContext dc, GraphicsDisplayItem item, Pen defaultPen)
        {
            for (int i = 0; i < item.Geometries.Length; i++)
            {
                bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                var effectivePen = isPass ? defaultPen : GetPen(item.Style!.GetEffectiveColorArgb(false), GetScaledLineWidth(item.Style.LineWidth), item.Style.LineDashStyle);
                if (item.Geometries[i] is Arc2d arc2d)
                {
                    // ★ Arc2d（弧度 + IsMajorArc/IsClockwise 连续模型）：按 AngularSpan 连续采样折线，
                    //   与交互层 MeasureBandOverlay.AddArc 同源，兼容大/小弧、顺/逆时针、跨 2π。
                    _diagDrawCalls++;
                    dc.DrawGeometry(null, effectivePen, CreateArc2dGeometry(arc2d));
                }
                else if (item.Geometries[i] is ArcGeometry arc)
                {
                    var geometry = CreateArcGeometry(arc);
                    _diagDrawCalls++;
                    dc.DrawGeometry(null, effectivePen, geometry);
                }
            }
        }

        /// <summary>
        /// 创建 Arc2d 的采样折线几何（沿 AngularSpan 连续扫掠，首尾精确落在弧端点）
        /// </summary>
        private static Geometry CreateArc2dGeometry(Arc2d arc)
        {
            var points = arc.GetSamplePoints(48);
            if (points.Length < 2) return Geometry.Empty;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], false, false);
                for (int i = 1; i < points.Length; i++)
                {
                    ctx.LineTo(points[i], true, true);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private void RenderPolygons(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, SolidColorBrush defaultBrush, Pen defaultPen)
        {
            bool isUnified = item.RenderMode == DisplayRenderMode.Unified;
            var scale = GetScaledLineWidth(style.LineWidth);
            var ngColorArgb = style.GetEffectiveColorArgb(false);
            var ngPen = GetPen(ngColorArgb, scale, style.LineDashStyle);
            var ngBrush = item.IsFilled ? GetBrush(ngColorArgb) : null;

            if (isUnified)
            {
                var allPoints = new List<Point[]>();
                for (int i = 0; i < item.Geometries.Length; i++)
                {
                    if (item.Geometries[i] is Point[] points && points.Length >= 2)
                        allPoints.Add(item.IsFixedScreenSize ? ScaleScreenStable(points) : points);
                }
                if (allPoints.Count > 0) _diagDrawCalls++;
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
                        if (item.IsFixedScreenSize) points = ScaleScreenStable(points);
                        if (isPass)
                            okPoints.Add(points);
                        else
                            ngPoints.Add(points);
                    }
                }
                if (okPoints.Count > 0) _diagDrawCalls++;
                DrawMultipleFigures(dc, okPoints, item.IsFilled ? defaultBrush : null, defaultPen, true);
                if (ngPoints.Count > 0) _diagDrawCalls++;
                DrawMultipleFigures(dc, ngPoints, ngBrush, ngPen, true);
            }
        }

        /// <summary>
        /// 屏幕恒定标记反除：锚点（首顶点）贴图定位，其余顶点相对锚点按 viewportScale 反除缩放。
        /// 用于方向标记/三角等装饰，使图形屏幕尺寸恒定（几何数据存 zoom=1 基准形态）。
        /// </summary>
        private Point[] ScaleScreenStable(Point[] points)
        {
            double s = Math.Max(_viewportScale, 0.001);
            var anchor = points[0];
            var scaled = new Point[points.Length];
            scaled[0] = anchor;
            for (int i = 1; i < points.Length; i++)
                scaled[i] = anchor + (points[i] - anchor) / s;
            return scaled;
        }

        private void RenderContours(DrawingContext dc, GraphicsDisplayItem item, GraphicsStyle style, SolidColorBrush defaultBrush, Pen defaultPen)
        {
            bool isUnified = item.RenderMode == DisplayRenderMode.Unified;
            var scale = GetScaledLineWidth(style.LineWidth);
            var ngColorArgb = style.GetEffectiveColorArgb(false);
            var ngPen = GetPen(ngColorArgb, scale, style.LineDashStyle);
            var ngBrush = item.IsFilled ? GetBrush(ngColorArgb) : null;

            // 分离有/无变换的轮廓：有变换的逐个绘制，无变换的批量合并以保持性能
            var batchPolygons = new List<Point[]>();
            var batchPolylines = new List<Point[]>();
            var batchOkPolygons = new List<Point[]>();
            var batchNgPolygons = new List<Point[]>();
            var batchOkPolylines = new List<Point[]>();
            var batchNgPolylines = new List<Point[]>();

            var contourCount = item.Geometries.Length;

            for (int i = 0; i < contourCount; i++)
            {
                if (item.Geometries[i] is BatchedContourGeometry batch)
                {
                    _diagContours += batch.Contours.Count;
                    foreach (var batchPoints in batch.Contours)
                        if (batchPoints != null) _diagContourVertices += batchPoints.Length;
                    var isPass = isUnified || (item.IsPassArray.Length > i && item.IsPassArray[i]);
                    if (_batchGeometryCache.TryGetValue(batch, out var cachedGeometries))
                    {
                        var cachedFill = item.IsFilled
                            ? (isUnified || isPass ? defaultBrush : ngBrush)
                            : null;
                        var cachedPen = isUnified || isPass ? defaultPen : ngPen;
                        IEnumerable<StreamGeometry> visibleGeometries;
                        if (!_hasViewport)
                        {
                            visibleGeometries = new HashSet<StreamGeometry>(cachedGeometries.Values.SelectMany(g => g));
                        }
                        else
                        {
                            var minTileX = (int)Math.Floor(_viewLeft / GeometryTileSize);
                            var maxTileX = (int)Math.Floor(_viewRight / GeometryTileSize);
                            var minTileY = (int)Math.Floor(_viewTop / GeometryTileSize);
                            var maxTileY = (int)Math.Floor(_viewBottom / GeometryTileSize);
                            var selected = new HashSet<StreamGeometry>();
                            for (var tileX = minTileX; tileX <= maxTileX; tileX++)
                            for (var tileY = minTileY; tileY <= maxTileY; tileY++)
                                if (cachedGeometries.TryGetValue((tileX, tileY), out var tileGeometries))
                                    foreach (var geometry in tileGeometries)
                                        selected.Add(geometry);
                            visibleGeometries = selected;
                        }
                        foreach (var cachedGeometry in visibleGeometries)
                        {
                            _diagDrawCalls++;
                            _diagBlocks++;
                            dc.DrawGeometry(cachedFill, cachedPen, cachedGeometry);
                        }
                        continue;
                    }
                    foreach (var pts in batch.Contours)
                    {
                        if (pts == null || pts.Length < 2) continue;
                        if (batch.IsClosed && pts.Length >= 3)
                        {
                            if (isUnified) batchPolygons.Add(pts);
                            else if (isPass) batchOkPolygons.Add(pts);
                            else batchNgPolygons.Add(pts);
                        }
                        else
                        {
                            if (isUnified) batchPolylines.Add(pts);
                            else if (isPass) batchOkPolylines.Add(pts);
                            else batchNgPolylines.Add(pts);
                        }
                    }
                    continue;
                }

                if (item.Geometries[i] is ContourGeometry contour && contour.Points != null)
                {
                    var pts = contour.Points;
                    if (pts.Length < 2) continue;
                    _diagContourVertices += pts.Length;

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
                if (batchPolygons.Count > 0) { _diagDrawCalls++; _diagBlocks++; }
                DrawMultipleFigures(dc, batchPolygons, item.IsFilled ? defaultBrush : null, defaultPen, true);
                if (batchPolylines.Count > 0) { _diagDrawCalls++; _diagBlocks++; }
                DrawMultipleFigures(dc, batchPolylines, null, defaultPen, false);
            }
            else
            {
                if (batchOkPolygons.Count > 0) { _diagDrawCalls++; _diagBlocks++; }
                DrawMultipleFigures(dc, batchOkPolygons, item.IsFilled ? defaultBrush : null, defaultPen, true);
                if (batchOkPolylines.Count > 0) { _diagDrawCalls++; _diagBlocks++; }
                DrawMultipleFigures(dc, batchOkPolylines, null, defaultPen, false);
                if (batchNgPolygons.Count > 0) { _diagDrawCalls++; _diagBlocks++; }
                DrawMultipleFigures(dc, batchNgPolygons, ngBrush, ngPen, true);
                if (batchNgPolylines.Count > 0) { _diagDrawCalls++; _diagBlocks++; }
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
                    var effectivePen = isPass ? defaultPen : GetPen(style!.GetEffectiveColorArgb(false), GetScaledLineWidth(lineWidth), style.LineDashStyle);
                    var geometry = CreateArrowGeometry(line.Start, line.End, GetScaledLineWidth(lineWidth));
                    _diagDrawCalls++;
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
            // 十字尺寸接入 Clamped：放大时不低于配置尺寸，缩小时最多放大 1.5 倍，保证始终可见。
            var s = ViewportScaling.Clamped(_viewportScale, style.CrossSize,
                ViewportScaling.MarkerPower, style.CrossSize, style.CrossSize * 1.5);

            int totalCount = item.Geometries.Length;

            if (isUnified)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    for (int i = 0; i < totalCount; i++)
                    {
                        if (item.Geometries[i] is Point center)
                        {
                            if (_hasViewport && !IsInViewport(center))
                                continue;

                            _diagCrosses++;
                            double cx = center.X, cy = center.Y;
                            ctx.BeginFigure(new Point(cx - s, cy), false, false);
                            ctx.LineTo(new Point(cx + s, cy), true, false);
                            ctx.BeginFigure(new Point(cx, cy - s), false, false);
                            ctx.LineTo(new Point(cx, cy + s), true, false);
                        }
                    }
                }
                geometry.Freeze();
                _diagDrawCalls++;
                dc.DrawGeometry(null, defaultPen, geometry);
            }
            else
            {
                var okGeo = new StreamGeometry();
                var ngGeo = new StreamGeometry();
                using (var okCtx = okGeo.Open())
                using (var ngCtx = ngGeo.Open())
                {
                    for (int i = 0; i < totalCount; i++)
                    {
                        if (item.Geometries[i] is Point center)
                        {
                            if (_hasViewport && !IsInViewport(center))
                                continue;

                            _diagCrosses++;
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
                _diagDrawCalls += 2;
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

        #region 文本渲染（同锚点分组自动排版：屏幕恒定行高，字号变更自动适配间距）

        /// <summary>同锚点判定容差（屏幕像素；缩放时容差自适应，避免放大后误分组）</summary>
        private const double TextAnchorToleranceScreen = 2.0;

        /// <summary>行高系数（行高 = 该行字号 × 系数；字号变更时间距自动跟随）</summary>
        private const double TextLineHeightFactor = 1.4;

        /// <summary>展开后的单行文本（每个 DisplayTextData = 一行）</summary>
        private sealed class TextLine
        {
            public TextDisplayItem Item = null!;
            public DisplayTextData Data = null!;
            public bool IsPass;
            public double AnchorX;
            public double AnchorY;
        }

        /// <summary>
        /// 文本渲染：按锚点分组 → 组内按添加顺序换行（可跨 TextStyle，解决"同锚点多文本重叠"）。
        /// 锚点 = 图像坐标（跟随几何，随缩放移动）；行高与屏幕偏移均与字号同一缩放模型（屏幕恒定），
        /// 因此缩放时不会"字变小、间距变大"，改字号时行距自动适配。
        /// </summary>
        private void RenderTextsGrouped(DrawingContext dc, List<TextDisplayItem> items, int layerIndex)
        {
            // 缩放、内容、样式三者都不变时复用已录制的绘制内容：一次提交替代逐行提交。
            if (_textDrawingCaches[layerIndex] is { } cached
                && _textCacheVersions[layerIndex] == _textSourceVersion
                && Math.Abs(_textCacheZooms[layerIndex] - _viewportScale) < 0.0001)
            {
                _diagTexts += _textCacheTextCounts[layerIndex];
                _diagDrawCalls++;
                dc.DrawDrawing(cached);
                return;
            }

            var lines = CollectTextLines(items);
            _diagTexts += lines.Count;
            if (lines.Count == 0) return;

            var textGroup = new DrawingGroup();
            using (var textContext = textGroup.Open())
                DrawTextLines(textContext, lines);
            textGroup.Freeze();

            _textDrawingCaches[layerIndex] = textGroup;
            _textCacheZooms[layerIndex] = _viewportScale;
            _textCacheVersions[layerIndex] = _textSourceVersion;
            _textCacheTextCounts[layerIndex] = lines.Count;

            _diagDrawCalls++;
            dc.DrawDrawing(textGroup);
        }

        /// <summary>展开一层内的全部文本行（每个 DisplayTextData = 一行）。</summary>
        private static List<TextLine> CollectTextLines(List<TextDisplayItem> items)
        {
            var lines = new List<TextLine>();
            foreach (var item in items)
            {
                if (item.Style == null) continue;

                var texts = item.Texts;
                var passArray = item.IsPassArray;
                for (int i = 0; i < texts.Length; i++)
                {
                    var td = texts[i];
                    lines.Add(new TextLine
                    {
                        Item = item,
                        Data = td,
                        IsPass = passArray.Length > i ? passArray[i] : true,
                        AnchorX = td.X,
                        AnchorY = td.Y,
                    });
                }
            }
            return lines;
        }

        /// <summary>文本排版绘制：分桶分组 → 组内按添加顺序换行。</summary>
        private void DrawTextLines(DrawingContext dc, List<TextLine> lines)
        {

            // 同锚点分组（锚点屏幕距离 ≤ 容差）：按容差划格，只在 3×3 邻格内查找，
            // 避免"逐组线性扫描"在文本上千行时的平方级开销；候选组按加入顺序排序，结果与线性实现一致。
            double anchorTolerance = ViewportScaling.InvScale(_viewportScale, TextAnchorToleranceScreen);
            double cellSize = Math.Max(anchorTolerance, 0.5);
            var groups = new List<List<TextLine>>();
            var buckets = new Dictionary<(int X, int Y), List<int>>();
            var candidateIndices = new List<int>();
            foreach (var line in lines)
            {
                int cellX = (int)Math.Floor(line.AnchorX / cellSize);
                int cellY = (int)Math.Floor(line.AnchorY / cellSize);

                candidateIndices.Clear();
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (buckets.TryGetValue((cellX + dx, cellY + dy), out var indices))
                        candidateIndices.AddRange(indices);
                candidateIndices.Sort();

                List<TextLine>? target = null;
                foreach (var index in candidateIndices)
                {
                    var group = groups[index];
                    if (Math.Abs(group[0].AnchorX - line.AnchorX) <= anchorTolerance &&
                        Math.Abs(group[0].AnchorY - line.AnchorY) <= anchorTolerance)
                    {
                        target = group;
                        break;
                    }
                }

                if (target == null)
                {
                    target = new List<TextLine>();
                    groups.Add(target);
                    if (!buckets.TryGetValue((cellX, cellY), out var cellGroups))
                        buckets[(cellX, cellY)] = cellGroups = new List<int>();
                    cellGroups.Add(groups.Count - 1);
                }
                target.Add(line);
            }

            // 逐组排版：组内按添加顺序换行；行高 = 该行字号 × 系数（屏幕恒定 → 逻辑尺寸）
            foreach (var group in groups)
            {
                double lineOffsetY = 0.0;   // 组内累计行偏移（逻辑尺寸）
                foreach (var line in group)
                {
                    var style = line.Item.Style!;
                    // 字号统一走唯一缩放源：放大时不低于配置字号，缩小时最多放大 1.5 倍，保证任何缩放档都可见。
                    double scaledFontSize = ViewportScaling.Clamped(_viewportScale, style.FontSize,
                        ViewportScaling.DecorPower, style.FontSize, style.FontSize * 1.5);

                    double drawX = line.AnchorX
                        + ViewportScaling.InvScale(_viewportScale, line.Data.ScreenOffsetX);
                    double drawY = line.AnchorY
                        + ViewportScaling.InvScale(_viewportScale, line.Data.ScreenOffsetY)
                        + lineOffsetY;

                    var color = ArgbToColor(style.GetEffectiveColorArgb(line.IsPass));
                    var formattedText = CreateFormattedText(line.Data.Content, color, scaledFontSize, style.IsBold);

                    dc.PushOpacity(style.Opacity);
                    try
                    {
                        dc.DrawText(formattedText, new Point(drawX, drawY));
                    }
                    finally
                    {
                        dc.Pop();
                    }

                    lineOffsetY += scaledFontSize * TextLineHeightFactor;
                }
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

        private Pen GetPen(uint argb, double thickness, LineDashStyle dash = LineDashStyle.Solid)
        {
            var thicknessInt = (uint)(thickness * 100);
            var key = ((long)argb) | ((long)thicknessInt << 32) | ((long)(int)dash << 48);
            if (_penCache.TryGetValue(key, out var cached)) return cached;
            var brush = GetBrush(argb);
            var pen = new Pen(brush, thickness);
            var dashes = GetDashArray(dash);
            if (dashes != null)
                pen.DashStyle = new DashStyle(dashes, 0);
            pen.Freeze();
            _penCache[key] = pen;
            return pen;
        }

        /// <summary>线型 → WPF 虚线节距（null=实线；节距单位为线宽倍数，缩放时观感稳定）</summary>
        private static DoubleCollection? GetDashArray(LineDashStyle dash) => dash switch
        {
            LineDashStyle.Dash => new DoubleCollection(new double[] { 4, 2 }),
            LineDashStyle.Dot => new DoubleCollection(new double[] { 1, 2 }),
            LineDashStyle.DashDot => new DoubleCollection(new double[] { 4, 2, 1, 2 }),
            _ => null,
        };

        private static Color ArgbToColor(uint argb) => Color.FromArgb(
            (byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));

        // 字体对象与画刷都是可复用的重对象：逐行重建纯属浪费，改为静态/缓存复用；
        // DPI 每帧取一次（见 OnRender），不再逐行调用 VisualTreeHelper.GetDpi。
        private static readonly Typeface RegularTypeface = new(new FontFamily("Segoe UI"),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Typeface BoldTypeface = new(new FontFamily("Segoe UI"),
            FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        private double _pixelsPerDip = 1.0;

        private static uint ColorToArgb(Color color) =>
            (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);

        private FormattedText CreateFormattedText(string text, Color color, double fontSize, bool isBold)
            => new(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                isBold ? BoldTypeface : RegularTypeface, fontSize, GetBrush(ColorToArgb(color)), _pixelsPerDip);

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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private bool _batchRefreshPending;

        // 视口裁剪（图像坐标系）
        private double _viewLeft, _viewTop, _viewRight, _viewBottom;
        private bool _hasViewport;

        // 耗时诊断：_renderSw 记录整帧 OnRender 耗时；_intervalSw 记录相邻两帧间隔；
        // _contourMs/_vertexCount 记录轮廓批次
        private readonly Stopwatch _renderSw = Stopwatch.StartNew();
        private readonly Stopwatch _intervalSw = Stopwatch.StartNew();
        private double _contourMs;
        private int _vertexCount;

        // 上一帧真实 OnRender 耗时（_renderSw 在进入绘制前重启、绘制结束处读取）
        private double _lastFrameMs;

        // 诊断计数：缓存失效次数 / 批量缓存命中与未命中 / 回退路径规模
        private int _invalidateCount;
        private int _cacheHits;
        private int _cacheMisses;
        private int _fallbackContours;
        private int _fallbackVertices;
        private double _textMs;
        private int _textCount;

        // 文本排版缓存：同一（内容 / 颜色 / 字号 / 粗体）只排版一次并跨帧复用（显示结果完全一致）
        private readonly Dictionary<(string Content, uint Argb, double FontSize, bool Bold), FormattedText> _formattedTextCache = new();
        private const int FormattedTextCacheLimit = 8192;
        private TextDrawingCacheEntry? _regionTextDrawingCache;
        private TextDrawingCacheEntry? _collectorTextDrawingCache;
        private TextDrawingCacheEntry? _highlightTextDrawingCache;

        private sealed class TextDrawingCacheEntry
        {
            public required int Fingerprint { get; init; }
            public required DrawingGroup Drawing { get; init; }
            public required int TextCount { get; init; }
            public required IReadOnlyList<TextGroupDrawingEntry> Groups { get; init; }
        }

        private sealed class TextGroupDrawingEntry
        {
            public required Point Anchor { get; init; }
            public required DrawingGroup Drawing { get; init; }
            public required ScaleTransform Transform { get; init; }
        }

        // LOD 阈值：当缩放倍率低于此值且 Cross 数量超过阈值时启用密度采样
        private const double LOD_ZOOM_THRESHOLD = 0.15;
        private const int LOD_MAX_CROSSES = 2000;
        private const int LOD_MAX_CONTOURS = 5000;

        #endregion

        #region 公共接口

        /// <summary>设置显示收集器（工具输出）</summary>
        public void SetDisplayCollector(DisplayCollector? collector)
        {
            UnsubscribeItems(_collector?.Items);
            _collector = collector;
            _invalidateCount++;
            InvalidateTextDrawingCaches();
            SubscribeItems(_collector?.Items);
            RequestBatchGeometryRefresh();
            InvalidateVisual();
        }

        /// <summary>设置区域显示项（检测区域覆盖）</summary>
        public void SetRegionItems(IEnumerable<DisplayItemBase>? items)
        {
            UnsubscribeItems(_regionItems);
            _regionItems = items?.ToList();
            _invalidateCount++;
            InvalidateTextDrawingCaches();
            SubscribeItems(_regionItems);
            RequestBatchGeometryRefresh();
            InvalidateVisual();
        }

        /// <summary>设置显示项（兼容旧接口，内部转为 collector 模式）</summary>
        public void SetItems(IReadOnlyList<DisplayItemBase>? items)
        {
            UnsubscribeItems(_collector?.Items);
            _collector = null; // 直接设置 items 时清除 collector 引用
            _invalidateCount++;
            InvalidateTextDrawingCaches();
            UnsubscribeItems(_regionItems);
            _regionItems = items;
            SubscribeItems(_regionItems);
            RequestBatchGeometryRefresh();
            InvalidateVisual();
        }

        /// <summary>设置高亮显示项</summary>
        public void SetHighlightItems(IEnumerable<DisplayItemBase>? items)
        {
            UnsubscribeItems(_highlightItems);
            _highlightItems = items?.ToList();
            InvalidateTextDrawingCaches();
            SubscribeItems(_highlightItems);
            RequestBatchGeometryRefresh();
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
            _invalidateCount++;
            InvalidateTextDrawingCaches();
            ++_batchBuildVersion;
            CancelBatchBuild();
            _batchGeometryCache.Clear();
            InvalidateVisual();
        }

        /// <summary>
        /// 合并同一 Dispatcher 周期内的结果、区域和高亮更新，只启动一次批量几何构建。
        /// 结果刷新通常会连续调用 SetDisplayCollector/SetRegionItems；如果每个入口立即
        /// 构建，会先构建不完整集合再立刻取消，造成无效 CPU 消耗。
        /// </summary>
        private void RequestBatchGeometryRefresh()
        {
            ++_batchBuildVersion;
            CancelBatchBuild();
            _batchGeometryCache.Clear();

            if (_batchRefreshPending) return;
            _batchRefreshPending = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _batchRefreshPending = false;
                var buildVersion = _batchBuildVersion;
                ScheduleBatchGeometryBuild(GetAllItems(), buildVersion);
            }), System.Windows.Threading.DispatcherPriority.DataBind);
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

            // 先按单条轮廓的真实 Bounds 分配 Tile，再在 Tile 内合并几何。
            // 旧实现按 1000 条轮廓组成的大块 Bounds 分配：只要大块碰到当前视口，
            // 就会把整块轮廓提交给 WPF，导致局部视口也绘制大量不可见顶点。
            var tileContours = new Dictionary<(int X, int Y), List<Point[]>>();
            foreach (var points in batch.Contours)
            {
                if (points == null || points.Length < 2) continue;

                var bounds = new Rect(points[0], points[0]);
                for (var pointIndex = 1; pointIndex < points.Length; pointIndex++)
                    bounds.Union(points[pointIndex]);

                var minX = (int)Math.Floor(bounds.Left / GeometryTileSize);
                var maxX = (int)Math.Floor(bounds.Right / GeometryTileSize);
                var minY = (int)Math.Floor(bounds.Top / GeometryTileSize);
                var maxY = (int)Math.Floor(bounds.Bottom / GeometryTileSize);
                for (var tileX = minX; tileX <= maxX; tileX++)
                for (var tileY = minY; tileY <= maxY; tileY++)
                {
                    var key = (tileX, tileY);
                    if (!tileContours.TryGetValue(key, out var contours))
                        tileContours[key] = contours = new List<Point[]>();
                    contours.Add(points);
                }
            }

            var tiles = new Dictionary<(int X, int Y), IReadOnlyList<StreamGeometry>>();
            foreach (var (key, contours) in tileContours)
            {
                var geometries = new List<StreamGeometry>();
                for (var start = 0; start < contours.Count; start += BatchGeometryChunkSize)
                {
                    var geometry = new StreamGeometry();
                    using (var ctx = geometry.Open())
                    {
                        var end = Math.Min(start + BatchGeometryChunkSize, contours.Count);
                        for (var i = start; i < end; i++)
                        {
                            var points = contours[i];
                            var closed = batch.IsClosed && points.Length >= 3;
                            ctx.BeginFigure(points[0], false, closed);
                            for (var pointIndex = 1; pointIndex < points.Length; pointIndex++)
                                ctx.LineTo(points[pointIndex], true, true);
                        }
                    }
                    geometry.Freeze();
                    geometries.Add(geometry);
                }
                tiles[key] = geometries;
            }
            return tiles;
        }

        /// <summary>清空高亮元素</summary>
        public void ClearHighlight()
        {
            UnsubscribeItems(_highlightItems);
            _highlightItems = null;
            InvalidateTextDrawingCaches();
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
            InvalidateTextDrawingCaches();
            InvalidateVisual();
        }

        private void InvalidateTextDrawingCaches()
        {
            _regionTextDrawingCache = null;
            _collectorTextDrawingCache = null;
            _highlightTextDrawingCache = null;
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
                    // 文本缓存使用基础字号和基础布局；缩放只更新组级幂律矩阵，
                    // 不得因为 viewport scale 变化而重建全部 FormattedText/DrawingGroup。
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

            // 诊断口径：
            //   total    = 上一帧【真实 OnRender 耗时】（_renderSw 在进入绘制前重启、绘制结束处读取）
            //   interval = 相邻两帧 OnRender 开始点的间隔（含帧间空档）
            //   idle     = interval - total ⇒ 帧间空档（不等于绘制耗时）
            //   contour / text = 【本帧】归属耗时（每帧入口清零，分支内累加）
            var intervalMs = _intervalSw.Elapsed.TotalMilliseconds;
            _intervalSw.Restart();
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RenderDiag] OnRender prev: total={_lastFrameMs:F1}ms, contour={_contourMs:F1}ms, text={_textMs:F1}ms, other={Math.Max(0, _lastFrameMs - _contourMs - _textMs):F1}ms, interval={intervalMs:F1}ms, idle={Math.Max(0, intervalMs - _lastFrameMs):F1}ms, invalidate={_invalidateCount}, cacheHits={_cacheHits}, cacheMisses={_cacheMisses}, fallbackContours={_fallbackContours}, fallbackVertices={_fallbackVertices}, textCount={_textCount}, textCache={_formattedTextCache.Count}, contourVerticesUpperBound={_vertexCount}, scale={_viewportScale:F3}",
                "ResultRenderLayer");
            _renderSw.Restart();
            _intervalSw.Restart();
            _invalidateCount = 0;
            _cacheHits = 0;
            _cacheMisses = 0;
            _fallbackContours = 0;
            _fallbackVertices = 0;
            _contourMs = 0;
            _textMs = 0;

            // ★ 不再清空 _brushCache / _penCache，缓存可跨渲染复用
            // Pen 键已包含厚度信息，不同线宽自动对应不同缓存项

            // 渲染顺序：区域(底层) → 工具输出(中层) → 高亮(顶层)
            // 每层内：图形项立即绘制，文本项收集后统一分组排版（同锚点协同换行）
            RenderLayer(dc, _regionItems, ref _regionTextDrawingCache);
            RenderLayer(dc, _collector?.Items, ref _collectorTextDrawingCache);
            RenderLayer(dc, _highlightItems, ref _highlightTextDrawingCache);

            // 本帧真实绘制耗时（_renderSw 在进入绘制前重启，此处读取）
            _lastFrameMs = _renderSw.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// 渲染一层：图形项按原顺序立即绘制；文本项收集后统一分组排版，
        /// 保证"同一锚点的多条文本（可跨 TextStyle）"协同换行而非相互重叠。
        /// </summary>
        private void RenderLayer(
            DrawingContext dc,
            IReadOnlyList<DisplayItemBase>? items,
            ref TextDrawingCacheEntry? textDrawingCache)
        {
            if (items == null) return;

            List<TextDisplayItem>? texts = null;
            foreach (var item in items)
            {
                if (!item.IsVisible) continue;

                if (item is TextDisplayItem t)
                {
                    (texts ??= new List<TextDisplayItem>()).Add(t);
                    continue;
                }
                RenderItem(dc, item);
            }

            if (texts != null)
                RenderTextsGrouped(dc, texts, ref textDrawingCache);
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
            double s = GetScaledLineWidth(2.5, ViewportScaling.MarkerPower);
            double crossThickness = GetScaledLineWidth(1.0, ViewportScaling.MarkerPower);
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
                DrawMultipleFigures(dc, okPoints, null, defaultPen, false);
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
                    var effectivePen = isPass ? defaultPen : GetPen(effectiveColorArgb, GetScaledLineWidth(style.LineWidth), style.LineDashStyle);
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
                bool isPass = item.IsPassArray.Length > i ? item.IsPassArray[i] : true;
                var effectivePen = isPass ? defaultPen : GetPen(item.Style!.GetEffectiveColorArgb(false), GetScaledLineWidth(item.Style.LineWidth), item.Style.LineDashStyle);
                if (item.Geometries[i] is Arc2d arc2d)
                {
                    // ★ Arc2d（弧度 + IsMajorArc/IsClockwise 连续模型）：按 AngularSpan 连续采样折线，
                    //   与交互层 MeasureBandOverlay.AddArc 同源，兼容大/小弧、顺/逆时针、跨 2π。
                    dc.DrawGeometry(null, effectivePen, CreateArc2dGeometry(arc2d));
                }
                else if (item.Geometries[i] is ArcGeometry arc)
                {
                    var geometry = CreateArcGeometry(arc);
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
                DrawMultipleFigures(dc, okPoints, item.IsFilled ? defaultBrush : null, defaultPen, true);
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
            var contourStep = contourCount > LOD_MAX_CONTOURS
                ? (int)Math.Ceiling((double)contourCount / LOD_MAX_CONTOURS)
                : 1;

            for (int i = 0; i < contourCount; i += contourStep)
            {
                if (item.Geometries[i] is BatchedContourGeometry batch)
                {
                    var isPass = isUnified || (item.IsPassArray.Length > i && item.IsPassArray[i]);
                    if (_batchGeometryCache.TryGetValue(batch, out var cachedGeometries))
                    {
                        _cacheHits++;
                        var cachedFill = item.IsFilled
                            ? (isUnified || isPass ? defaultBrush : ngBrush)
                            : null;
                        var cachedPen = isUnified || isPass ? defaultPen : ngPen;
                        HashSet<StreamGeometry> visibleGeometries;
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
                        var contours = batch.Contours;
                        var verticesUpperBound = 0;
                        for (var c = 0; c < contours.Count; c++) verticesUpperBound += contours[c].Length;
                        var drawSw = Stopwatch.StartNew();
                        var drawn = 0;
                        foreach (var cachedGeometry in visibleGeometries)
                        {
                            dc.DrawGeometry(cachedFill, cachedPen, cachedGeometry);
                            drawn++;
                        }
                        drawSw.Stop();
                        _contourMs = drawSw.Elapsed.TotalMilliseconds;
                        _vertexCount = verticesUpperBound;
                        VisionLogger.Instance.Log(LogLevel.Info,
                            $"[RenderDiag] batch draw: viewport={_hasViewport}, rect=({_viewLeft:F0},{_viewTop:F0},{_viewRight:F0},{_viewBottom:F0}), tiles={cachedGeometries.Count}, selectedGeometries={visibleGeometries.Count}, drawn={drawn}, contours={contours.Count}, verticesUpperBound={verticesUpperBound}, drawMs={drawSw.Elapsed.TotalMilliseconds:F1}, pass={isPass}",
                            "ResultRenderLayer");
                        continue;
                    }
                    _cacheMisses++;
                    // 批量几何正在后台构建时跳过本帧绘制，避免在 UI 线程回退展开
                    // 整批轮廓。构建完成后会 InvalidateVisual，下一帧直接命中缓存。
                    continue;
                }

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
                    var effectivePen = isPass ? defaultPen : GetPen(style!.GetEffectiveColorArgb(false), GetScaledLineWidth(lineWidth), style.LineDashStyle);
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
            var s = GetScaledLineWidth(style.CrossSize, ViewportScaling.MarkerPower);

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

        #region 文本渲染（同锚点分组自动排版：屏幕恒定行高，字号变更自动适配间距）

        /// <summary>同锚点判定容差（基础图像坐标）。缓存布局不随 viewport zoom 变化。</summary>
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
        private void RenderTextsGrouped(
            DrawingContext dc,
            List<TextDisplayItem> items,
            ref TextDrawingCacheEntry? textDrawingCache)
        {
            var textSw = Stopwatch.StartNew();
            var fingerprint = ComputeTextFingerprint(items);
            if (textDrawingCache != null && textDrawingCache.Fingerprint == fingerprint)
            {
                ApplyTextTransforms(textDrawingCache);
                dc.DrawDrawing(textDrawingCache.Drawing);
                _textMs = textSw.Elapsed.TotalMilliseconds;
                _textCount = textDrawingCache.TextCount;
                return;
            }

            var lines = new List<TextLine>();
            foreach (var item in items)
            {
                if (item.Style == null) continue;

                var texts = item.Texts;
                var passArray = item.IsPassArray;
                // 所见即所得：不因缩放级别、文本数量或采样步长丢弃任何结果文本。
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
            if (lines.Count == 0) return;

            // 同锚点分组（锚点屏幕距离 ≤ 容差）：以"锚点网格"定位候选组（仅查相邻 9 格），
            // 命中后仍用原判定式精确比较，并按组的创建顺序取最靠前者 ⇒ 分组结果与原 O(n²)
            // 实现完全一致，复杂度由 O(n²) 降为 O(n)。
            // 分组属于内容布局，不属于视口变换；固定在基础图像坐标中，
            // 避免 zoom 改变时仅因容差变化而让整层文本缓存失效。
            const double anchorTolerance = TextAnchorToleranceScreen;
            var groups = new List<List<TextLine>>();
            var groupGrid = new Dictionary<(long X, long Y), List<(int Index, List<TextLine> Group)>>();
            foreach (var line in lines)
            {
                long cellX = (long)Math.Floor(line.AnchorX / anchorTolerance);
                long cellY = (long)Math.Floor(line.AnchorY / anchorTolerance);
                List<TextLine>? target = null;
                var bestIndex = int.MaxValue;
                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (!groupGrid.TryGetValue((cellX + dx, cellY + dy), out var candidates)) continue;
                    foreach (var (candidateIndex, candidate) in candidates)
                    {
                        if (candidateIndex >= bestIndex) continue;
                        if (Math.Abs(candidate[0].AnchorX - line.AnchorX) <= anchorTolerance &&
                            Math.Abs(candidate[0].AnchorY - line.AnchorY) <= anchorTolerance)
                        {
                            target = candidate;
                            bestIndex = candidateIndex;
                        }
                    }
                }
                if (target == null)
                {
                    target = new List<TextLine>();
                    groups.Add(target);
                    if (!groupGrid.TryGetValue((cellX, cellY), out var bucket))
                    {
                        bucket = new List<(int Index, List<TextLine> Group)>();
                        groupGrid[(cellX, cellY)] = bucket;
                    }
                    bucket.Add((groups.Count - 1, target));
                }
                target.Add(line);
            }

            // 以 zoom=1 的基础字号构建内容。缩放不再改变 FormattedText，
            // 只更新每个锚点组的矩阵；这保留幂律视觉规则，同时避免逐条文本重建。
            var drawing = new DrawingGroup();
            var groupEntries = new List<TextGroupDrawingEntry>(groups.Count);
            using (var textDc = drawing.Open())
            {
                foreach (var group in groups)
                {
                    if (group.Count == 0) continue;

                    var groupDrawing = new DrawingGroup();
                    using (var groupDc = groupDrawing.Open())
                    {
                        // 基础字号和基础间距只创建一次，缩放交给组级矩阵。
                        RenderTextLines(groupDc, new List<List<TextLine>> { group });
                    }
                    var anchor = new Point(group[0].AnchorX, group[0].AnchorY);
                    var transform = new ScaleTransform(1.0, 1.0, anchor.X, anchor.Y);
                    groupDrawing.Transform = transform;
                    textDc.DrawDrawing(groupDrawing);
                    groupEntries.Add(new TextGroupDrawingEntry
                    {
                        Anchor = anchor,
                        Drawing = groupDrawing,
                        Transform = transform,
                    });
                }
            }

            textDrawingCache = new TextDrawingCacheEntry
            {
                Fingerprint = fingerprint,
                Drawing = drawing,
                TextCount = lines.Count,
                Groups = groupEntries,
            };
            ApplyTextTransforms(textDrawingCache);
            dc.DrawDrawing(drawing);
            _textMs = textSw.Elapsed.TotalMilliseconds;
            _textCount = lines.Count;
        }

        private void ApplyTextTransforms(TextDrawingCacheEntry cache)
        {
            // ImageControl 还会对结果层整体应用 viewport zoom，因此这里使用
            // 1 / zoom^(1+power)，最终屏幕字号为 base / zoom^power。
            var zoom = Math.Max(_viewportScale, 0.001);
            var localScale = ViewportScaling.Power(zoom, 1.0, 1.0 + ViewportScaling.DecorPower);
            foreach (var group in cache.Groups)
            {
                group.Transform.CenterX = group.Anchor.X;
                group.Transform.CenterY = group.Anchor.Y;
                group.Transform.ScaleX = localScale;
                group.Transform.ScaleY = localScale;
            }
        }

        private void RenderTextLines(DrawingContext dc, List<List<TextLine>> groups)
        {
            // 逐组排版：组内按添加顺序换行；行高 = 该行字号 × 系数（屏幕恒定 → 逻辑尺寸）
            foreach (var group in groups)
            {
                double lineOffsetY = 0.0;   // 组内累计行偏移（逻辑尺寸）
                foreach (var line in group)
                {
                    var style = line.Item.Style!;
                    double drawX = line.AnchorX
                        + line.Data.ScreenOffsetX;
                    double drawY = line.AnchorY
                        + line.Data.ScreenOffsetY
                        + lineOffsetY;

                    var textArgb = style.GetEffectiveColorArgb(line.IsPass);
                    var color = ArgbToColor(textArgb);
                    var textKey = (line.Data.Content, textArgb, style.FontSize, style.IsBold);
                    if (!_formattedTextCache.TryGetValue(textKey, out var formattedText))
                    {
                        formattedText = CreateFormattedText(line.Data.Content, color, style.FontSize, style.IsBold);
                        if (_formattedTextCache.Count < FormattedTextCacheLimit)
                            _formattedTextCache[textKey] = formattedText;
                    }

                    dc.PushOpacity(style.Opacity);
                    try
                    {
                        dc.DrawText(formattedText, new Point(drawX, drawY));
                    }
                    finally
                    {
                        dc.Pop();
                    }

                    lineOffsetY += style.FontSize * TextLineHeightFactor;
                }
            }
        }

        private static int ComputeTextFingerprint(IReadOnlyList<TextDisplayItem> items)
        {
            unchecked
            {
                var hash = 17;
                foreach (var item in items)
                {
                    hash = hash * 31 + RuntimeHelpers.GetHashCode(item);
                    hash = hash * 31 + RuntimeHelpers.GetHashCode(item.Texts);
                    hash = hash * 31 + item.Texts.Length;
                }
                return hash;
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

        private FormattedText CreateFormattedText(string text, Color color, double fontSize, bool isBold)
        {
            var typeface = new Typeface(new FontFamily("Segoe UI"),
                FontStyles.Normal, isBold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
            // 使用自身实例的 DPI，而非依赖 Application.Current.MainWindow。
            // 依赖 MainWindow 在脚本工具/独立上下文渲染时可能为 null，导致 VisualTreeHelper.GetDpi(null) 抛 NullReferenceException。
            // GetDpi(this) 即使本元素尚未连接可视树也不会抛异常，返回默认 96 DPI。
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            return new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, fontSize, new SolidColorBrush(color), pixelsPerDip);
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

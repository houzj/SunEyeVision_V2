using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Logging;
// 解决 System.Windows.Media.LineSegment 与 SDK LineSegment 的命名冲突
using SdkLineSegment = SunEyeVision.Plugin.SDK.Models.Visualization.LineSegment;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 结果渲染层 - 基于 DrawingContext 的高性能渲染
    /// </summary>
    public sealed class ResultRenderLayer : ResultTileHost
    {
        #region 私有字段

        private DisplayCollector? _collector;
        private IReadOnlyList<DisplayItemBase>? _regionItems;
        private IReadOnlyList<DisplayItemBase>? _highlightItems;
        private readonly Dictionary<uint, SolidColorBrush> _brushCache = new();
        private readonly Dictionary<long, Pen> _penCache = new();
        private readonly Dictionary<(string Text, uint Color, int FontSize, bool Bold), FormattedText> _formattedTextCache = new();
        private readonly Dictionary<BatchedContourGeometry, BatchGeometryCacheEntry> _batchGeometryCache = new();
        private readonly HashSet<BatchedContourGeometry> _batchCacheMissLogged = new();
        private CancellationTokenSource? _batchBuildCancellation;
        private int _batchBuildVersion;
        private bool _batchBuildPending;
        private IReadOnlyList<DisplayItemBase>? _lastBatchBuildItems;

        /// <summary>
        /// 同一轮廓批次的双形态缓存：大视口使用一个完整冻结几何，局部视口使用 Tile 几何。
        /// Tile 仅承担空间索引，不再强迫全视口逐 Tile 提交 DrawGeometry。
        /// </summary>
        private sealed class BatchGeometryCacheEntry
        {
            public required StreamGeometry FullGeometry { get; init; }
            public required IReadOnlyDictionary<(int X, int Y), IReadOnlyList<StreamGeometry>> TileGeometries { get; init; }
            public required int ContourCount { get; init; }
            public required int PointCount { get; init; }
        }

        // 视口裁剪（图像坐标系）
        private double _viewLeft, _viewTop, _viewRight, _viewBottom;
        private bool _hasViewport;
        private bool _renderInvalidationPending;
        private DispatcherTimer? _viewportRefreshTimer;
        private readonly RenderSceneCompiler _sceneCompiler = new();
        private readonly RenderBatchCompiler _batchCompiler = new();
        private RenderBatchScene? _batchScene;
        private readonly Dictionary<RenderBatch, StreamGeometry> _compiledBatchGeometries = new();
        private CancellationTokenSource? _batchSceneBuildCancellation;
        private int _batchSceneBuildVersion;
        private IReadOnlyList<DisplayItemBase>? _lastBatchSceneBuildItems;
        private long _imageIdentity;
        private CancellationTokenSource? _sceneBuildCancellation;
        private int _sceneBuildVersion;
        private RenderScene? _scene;
        private RenderTileKey? _activeTileKey;

        // LOD 阈值：当缩放倍率低于此值且 Cross 数量超过阈值时启用密度采样
        private const double LOD_ZOOM_THRESHOLD = 0.15;
        private const int LOD_MAX_CROSSES = 2000;
        private const int LOD_MAX_CONTOURS = 5000;

        #endregion

        public ResultRenderLayer()
        {
            SetRenderBackend(RenderTile);
        }

        #region 公共接口

        /// <summary>设置显示收集器（工具输出）</summary>
        public void SetDisplayCollector(DisplayCollector? collector)
        {
            UnsubscribeItems(_collector?.Items);
            _collector = collector;
            var buildVersion = ++_batchBuildVersion;
            SetDiagnosticTraceId(buildVersion);
            CancelBatchBuild();
            _batchGeometryCache.Clear();
            _batchCacheMissLogged.Clear();
            SubscribeItems(_collector?.Items);
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RenderDiag] SetDisplayCollector: items={_collector?.Items.Count ?? 0}, allItems={GetAllItems().Count}, buildVersion={buildVersion}",
                "ResultRenderLayer");
            ScheduleBatchGeometryBuild(GetAllItems(), buildVersion);
            ScheduleRenderBatchBuild(GetAllItems(), buildVersion);
            RebuildScene();
        }

        /// <summary>设置区域显示项（检测区域覆盖）</summary>
        public void SetRegionItems(IEnumerable<DisplayItemBase>? items)
        {
            UnsubscribeItems(_regionItems);
            _regionItems = items?.ToList();
            var buildVersion = ++_batchBuildVersion;
            SetDiagnosticTraceId(buildVersion);
            CancelBatchBuild();
            _batchGeometryCache.Clear();
            _batchCacheMissLogged.Clear();
            ScheduleBatchGeometryBuild(GetAllItems(), buildVersion);
            ScheduleRenderBatchBuild(GetAllItems(), buildVersion);
            SubscribeItems(_regionItems);
            RebuildScene();
        }

        /// <summary>设置显示项（兼容旧接口，内部转为 collector 模式）</summary>
        public void SetItems(IReadOnlyList<DisplayItemBase>? items)
        {
            UnsubscribeItems(_collector?.Items);
            _collector = null; // 直接设置 items 时清除 collector 引用
            var buildVersion = ++_batchBuildVersion;
            SetDiagnosticTraceId(buildVersion);
            CancelBatchBuild();
            _batchGeometryCache.Clear();
            _batchCacheMissLogged.Clear();
            UnsubscribeItems(_regionItems);
            _regionItems = items;
            ScheduleBatchGeometryBuild(GetAllItems(), buildVersion);
            ScheduleRenderBatchBuild(GetAllItems(), buildVersion);
            SubscribeItems(_regionItems);
            RebuildScene();
        }

        /// <summary>设置高亮显示项</summary>
        public void SetHighlightItems(IEnumerable<DisplayItemBase>? items)
        {
            UnsubscribeItems(_highlightItems);
            _highlightItems = items?.ToList();
            SubscribeItems(_highlightItems);
            RebuildScene();
        }

        /// <summary>清空所有内容</summary>
        public new void Clear()
        {
            UnsubscribeItems(_collector?.Items);
            UnsubscribeItems(_regionItems);
            UnsubscribeItems(_highlightItems);
            _collector = null;
            _regionItems = null;
            _highlightItems = null;
            ++_batchBuildVersion;
            CancelBatchBuild();
            _batchGeometryCache.Clear();
            _batchCacheMissLogged.Clear();
            _batchBuildPending = false;
            _lastBatchBuildItems = null;
            _lastBatchSceneBuildItems = null;
            CancelSceneBuild();
            CancelRenderBatchBuild();
            _lastBatchBuildItems = null;
            _lastBatchSceneBuildItems = null;
            _batchScene = null;
            _compiledBatchGeometries.Clear();
            _scene = null;
            base.Clear();
        }

        /// <summary>Starts a new image generation; old compiled batches can never be reused.</summary>
        public void SetImageIdentity(long imageIdentity)
        {
            if (_imageIdentity == imageIdentity) return;
            _imageIdentity = imageIdentity;
            ++_batchSceneBuildVersion;
            CancelRenderBatchBuild();
            _lastBatchBuildItems = null;
            _lastBatchSceneBuildItems = null;
            _batchScene = null;
            _compiledBatchGeometries.Clear();
            RequestRender("image-identity-change");
        }

        private void ScheduleRenderBatchBuild(IReadOnlyList<DisplayItemBase> items, int generation)
        {
            if (SameItemReferences(_lastBatchSceneBuildItems, items)) return;
            _lastBatchSceneBuildItems = items;
            var version = ++_batchSceneBuildVersion;
            var imageIdentity = _imageIdentity;
            CancelRenderBatchBuild();
            var cancellation = new CancellationTokenSource();
            _batchSceneBuildCancellation = cancellation;
            _ = Task.Run(() =>
            {
                if (cancellation.IsCancellationRequested) return;
                var scene = _batchCompiler.Compile(items, imageIdentity, generation);
                var geometries = new Dictionary<RenderBatch, StreamGeometry>();
                foreach (var batch in scene.Batches)
                {
                    if (cancellation.IsCancellationRequested) return;
                    geometries[batch] = BuildBatchGeometry(batch, null);
                }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (cancellation.IsCancellationRequested || version != _batchSceneBuildVersion ||
                        scene.ImageIdentity != _imageIdentity)
                    {
                        cancellation.Dispose();
                        return;
                    }
                    _batchScene = scene;
                    _compiledBatchGeometries.Clear();
                    foreach (var pair in geometries) _compiledBatchGeometries[pair.Key] = pair.Value;
                    cancellation.Dispose();
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[RenderDiag] GPU batch scene ready: imageIdentity={scene.ImageIdentity}, renderGeneration={scene.RenderGeneration}, backend=wpf-batch, batchCount={scene.Batches.Count}, vertexCount={scene.VertexCount}, drawRanges={scene.DrawRangeCount}",
                        "ResultRenderLayer");
                    RequestRender("batch-scene-ready");
                }));
            }, cancellation.Token);
        }

        private void CancelRenderBatchBuild()
        {
            var cancellation = _batchSceneBuildCancellation;
            _batchSceneBuildCancellation = null;
            if (cancellation == null) return;
            try
            {
                // Completion callbacks dispose the source on the Dispatcher. A source
                // may therefore already be disposed when an image switch races with
                // that callback; cancellation is best-effort and must be idempotent.
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The requested generation is already complete and discarded.
            }
        }

        private static StreamGeometry BuildBatchGeometry(RenderBatch batch, Rect? clip)
        {
            var geometry = new StreamGeometry();
            using var context = geometry.Open();
            foreach (var range in batch.Ranges)
            {
                if (clip.HasValue && !range.Bounds.IntersectsWith(clip.Value)) continue;
                if (range.VertexCount < 2) continue;
                context.BeginFigure(batch.Vertices[range.StartVertex], batch.Closed, false);
                for (var i = 1; i < range.VertexCount; i++)
                    context.LineTo(batch.Vertices[range.StartVertex + i], true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        private static bool SameItemReferences(IReadOnlyList<DisplayItemBase>? left, IReadOnlyList<DisplayItemBase> right)
        {
            if (left == null || left.Count != right.Count) return false;
            for (var i = 0; i < right.Count; i++)
                if (!ReferenceEquals(left[i], right[i])) return false;
            return true;
        }

        private void ScheduleBatchGeometryBuild(IReadOnlyList<DisplayItemBase>? items, int buildVersion)
        {
            var batches = items?
                .OfType<GraphicsDisplayItem>()
                .SelectMany(i => i.Geometries.OfType<BatchedContourGeometry>())
                .Distinct()
                .ToList();
            if (batches == null || batches.Count == 0)
            {
                _batchBuildPending = false;
                return;
            }
            if (SameItemReferences(_lastBatchBuildItems, items!)) return;
            _lastBatchBuildItems = items;

            var cancellation = new CancellationTokenSource();
            _batchBuildCancellation = cancellation;
            _batchBuildPending = true;
            var buildTimer = Stopwatch.StartNew();
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RenderDiag] Batch geometry build begin: version={buildVersion}, batches={batches.Count}, contours={batches.Sum(b => b.Contours.Count)}",
                "ResultRenderLayer");
            _ = Task.Run(() =>
            {
                var built = new List<(BatchedContourGeometry Batch, BatchGeometryCacheEntry Cache)>();
                foreach (var batch in batches)
                {
                    if (cancellation.IsCancellationRequested) return;
                    var cache = BuildBatchGeometries(batch);
                    if (cache != null)
                        built.Add((batch, cache));
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
                    foreach (var (batch, cache) in built)
                        _batchGeometryCache[batch] = cache;
                    _batchBuildPending = false;
                    if (ReferenceEquals(_batchBuildCancellation, cancellation))
                        _batchBuildCancellation = null;
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[RenderDiag] Batch geometry build done: version={buildVersion}, batches={built.Count}, tiles={built.Sum(p => p.Cache.TileGeometries.Count)}, fullGeometries={built.Count}, contours={built.Sum(p => p.Cache.ContourCount)}, points={built.Sum(p => p.Cache.PointCount)}, ms={buildTimer.Elapsed.TotalMilliseconds:F1}",
                        "ResultRenderLayer");
                    if (buildTimer.Elapsed.TotalMilliseconds >= 500)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"[RenderAnalysis] trace={buildVersion}, stage=batch-cache, diagnosis=batch-cache-build-heavy, batches={built.Count}, tiles={built.Sum(p => p.Cache.TileGeometries.Count)}, fullGeometries={built.Count}, contours={built.Sum(p => p.Cache.ContourCount)}, points={built.Sum(p => p.Cache.PointCount)}, ms={buildTimer.Elapsed.TotalMilliseconds:F1}",
                            "ResultRenderLayer");
                    }
                    cancellation.Dispose();
                    // 场景编译可能先完成。缓存未就绪时不提交场景，避免先绘制一次
                    // 未命中缓存的结果、缓存完成后又把全部可见 tile 重绘一次。
                    if (_scene != null)
                        SetScene(_scene);
                    else
                        RequestRender("batch-cache-ready");
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

        private const double GeometryTileSize = 512.0;

        private static BatchGeometryCacheEntry? BuildBatchGeometries(BatchedContourGeometry batch)
        {
            if (batch.Contours.Count == 0)
                return null;

            // 先按轮廓所属 Tile 分组，再为每个 Tile 构建自己的几何。
            // 旧实现先把 1000 条轮廓合并成大几何，再按大几何 Bounds 复制到多个 Tile；
            // 这样单个 Tile 仍可能解析整块大几何，裁剪只发生在解析之后。
            var tileContours = new Dictionary<(int X, int Y), List<Point[]>>();
            for (var contourIndex = 0; contourIndex < batch.Contours.Count; contourIndex++)
            {
                var points = batch.Contours[contourIndex];
                if (points == null || points.Length < 2) continue;

                var minPoint = points[0];
                var maxPoint = points[0];
                for (var pointIndex = 1; pointIndex < points.Length; pointIndex++)
                {
                    minPoint.X = Math.Min(minPoint.X, points[pointIndex].X);
                    minPoint.Y = Math.Min(minPoint.Y, points[pointIndex].Y);
                    maxPoint.X = Math.Max(maxPoint.X, points[pointIndex].X);
                    maxPoint.Y = Math.Max(maxPoint.Y, points[pointIndex].Y);
                }

                var minX = (int)Math.Floor(minPoint.X / GeometryTileSize);
                var maxX = (int)Math.Floor(maxPoint.X / GeometryTileSize);
                var minY = (int)Math.Floor(minPoint.Y / GeometryTileSize);
                var maxY = (int)Math.Floor(maxPoint.Y / GeometryTileSize);
                for (var tileX = minX; tileX <= maxX; tileX++)
                for (var tileY = minY; tileY <= maxY; tileY++)
                {
                    var key = (tileX, tileY);
                    if (!tileContours.TryGetValue(key, out var contours))
                        tileContours[key] = contours = new List<Point[]>();
                    contours.Add(points);
                }
            }

            var validContours = batch.Contours
                .Where(points => points != null && points.Length >= 2)
                .ToList();
            if (validContours.Count == 0)
                return null;

            var tileGeometries = tileContours.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<StreamGeometry>)new[] { BuildTileGeometry(pair.Value, batch.IsClosed) });
            return new BatchGeometryCacheEntry
            {
                FullGeometry = BuildTileGeometry(validContours, batch.IsClosed),
                TileGeometries = tileGeometries,
                ContourCount = validContours.Count,
                PointCount = validContours.Sum(points => points.Length)
            };
        }

        private static StreamGeometry BuildTileGeometry(IReadOnlyList<Point[]> contours, bool isClosed)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            foreach (var points in contours)
            {
                var closed = isClosed && points.Length >= 3;
                ctx.BeginFigure(points[0], false, closed);
                for (var pointIndex = 1; pointIndex < points.Length; pointIndex++)
                    ctx.LineTo(points[pointIndex], true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        /// <summary>清空高亮元素</summary>
        public void ClearHighlight()
        {
            UnsubscribeItems(_highlightItems);
            _highlightItems = null;
            RequestRender("highlight-clear");
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

        private void OnItemPropertyChanged(object? sender, EventArgs e) => RebuildScene();

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
                    RequestRender("scale-change");
                }
            }
        }

        public void SetViewportScale(double scale) => ViewportScale = scale;

        /// <summary>
        /// 获取指定图像坐标附近的结果命中候选。返回值来自 Tile 空间索引，
        /// 调用方应继续执行几何级精确命中测试。
        /// </summary>
        public IReadOnlyList<DisplayItemBase> QueryHitCandidates(Point imagePoint, double tolerance = 4.0)
        {
            return _scene?.QueryCandidates(imagePoint, tolerance)
                ?? Array.Empty<DisplayItemBase>();
        }

        /// <summary>通过 Tile 候选索引执行精确几何命中，返回最上层命中的结果项。</summary>
        public DisplayItemBase? HitTest(Point imagePoint, double tolerance = 4.0)
        {
            var candidates = QueryHitCandidates(imagePoint, tolerance);
            for (var i = candidates.Count - 1; i >= 0; i--)
            {
                var item = candidates[i];
                if (!item.IsVisible) continue;
                if (item is GraphicsDisplayItem graphics && RenderHitTester.IsHit(graphics, imagePoint, tolerance))
                    return item;
            }
            return null;
        }

        /// <summary>设置视口范围（图像坐标系），用于视口裁剪</summary>
        public void SetViewport(double left, double top, double right, double bottom)
        {
            _viewLeft = left;
            _viewTop = top;
            _viewRight = right;
            _viewBottom = bottom;
            _hasViewport = true;
            base.SetViewport(new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)));
            ScheduleViewportRefresh();
        }

        private void ScheduleViewportRefresh()
        {
            _viewportRefreshTimer ??= new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _viewportRefreshTimer.Stop();
            _viewportRefreshTimer.Tick -= ViewportRefreshTimerOnTick;
            _viewportRefreshTimer.Tick += ViewportRefreshTimerOnTick;
            _viewportRefreshTimer.Start();
        }

        private void ViewportRefreshTimerOnTick(object? sender, EventArgs e)
        {
            _viewportRefreshTimer?.Stop();
            RequestRender("viewport-debounce");
        }

        private string _pendingRenderReason = "layer-invalidation";

        private void RequestRender(string reason = "layer-invalidation")
        {
            if (_renderInvalidationPending) return;
            _renderInvalidationPending = true;
            _pendingRenderReason = reason;
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                _renderInvalidationPending = false;
                return;
            }
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                _renderInvalidationPending = false;
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                {
                    var renderReason = _pendingRenderReason;
                    _pendingRenderReason = "layer-invalidation";
                    Refresh(renderReason);
                }
            }));
        }

        private double GetScaledLineWidth(double rawWidth, double power = 1.0) =>
            ViewportScaling.Power(_viewportScale, rawWidth, power);

        private void RebuildScene()
        {
            var items = GetAllItems();
            var version = ++_sceneBuildVersion;
            var compileTimer = Stopwatch.StartNew();
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RenderDiag] Scene compile begin: version={version}, items={items.Count}",
                "ResultRenderLayer");
            CancelSceneBuild();
            if (items.Count == 0)
            {
                _scene = null;
                SetScene(null);
                return;
            }

            var cancellation = new CancellationTokenSource();
            _sceneBuildCancellation = cancellation;
            _ = Task.Run(() => _sceneCompiler.Compile(items), cancellation.Token)
                .ContinueWith(t =>
                {
                    if (t.IsCanceled || t.IsFaulted || cancellation.IsCancellationRequested)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"[RenderDiag] Scene compile ended without scene: version={version}, canceled={t.IsCanceled}, faulted={t.IsFaulted}, canceledToken={cancellation.IsCancellationRequested}, ms={compileTimer.Elapsed.TotalMilliseconds:F1}",
                            "ResultRenderLayer");
                        cancellation.Dispose();
                        return;
                    }
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (version != _sceneBuildVersion || cancellation.IsCancellationRequested)
                                return;
                            _scene = t.Result;
                            VisionLogger.Instance.Log(LogLevel.Info,
                                $"[RenderDiag] Scene compile done: version={version}, tiles={_scene.Tiles.Count}, ms={compileTimer.Elapsed.TotalMilliseconds:F1}",
                                "ResultRenderLayer");
                            if (compileTimer.Elapsed.TotalMilliseconds >= 200)
                            {
                                VisionLogger.Instance.Log(LogLevel.Warning,
                                    $"[RenderAnalysis] trace={_batchBuildVersion}, stage=scene-compile, diagnosis=scene-compile-heavy, version={version}, items={items.Count}, tiles={_scene.Tiles.Count}, ms={compileTimer.Elapsed.TotalMilliseconds:F1}",
                                    "ResultRenderLayer");
                            }
                            if (_batchBuildPending)
                            {
                                VisionLogger.Instance.Log(LogLevel.Info,
                                    $"[RenderDiag] Scene commit deferred: version={version}, batchCachePending=true",
                                    "ResultRenderLayer");
                            }
                            else
                            {
                                SetScene(_scene);
                            }
                        }
                        finally
                        {
                            if (ReferenceEquals(_sceneBuildCancellation, cancellation))
                                _sceneBuildCancellation = null;
                            cancellation.Dispose();
                        }
                    }), DispatcherPriority.Render);
                }, TaskScheduler.Default);
        }

        private void CancelSceneBuild()
        {
            var cancellation = _sceneBuildCancellation;
            _sceneBuildCancellation = null;
            if (cancellation == null) return;
            cancellation.Cancel();
        }

        #endregion

        #region 渲染

        private void RenderTile(DrawingContext dc, RenderTileScene tile)
        {
            dc.PushClip(new RectangleGeometry(tile.Bounds));
            _activeTileKey = tile.Key == new RenderTileKey(int.MinValue, int.MinValue)
                ? null
                : tile.Key;

            // ★ 不再清空 _brushCache / _penCache，缓存可跨渲染复用
            // Pen 键已包含厚度信息，不同线宽自动对应不同缓存项

            // 渲染顺序：区域(底层) → 工具输出(中层) → 高亮(顶层)
            // 每层内：图形项立即绘制，文本项收集后统一分组排版（同锚点协同换行）
            try
            {
                RenderCompiledBatchScene(dc, tile);
                RenderLayer(dc, tile.Items);
            }
            finally
            {
                _activeTileKey = null;
                dc.Pop();
            }
        }

        private void RenderCompiledBatchScene(DrawingContext dc, RenderTileScene tile)
        {
            var scene = _batchScene;
            if (scene == null || scene.Batches.Count == 0) return;
            var clip = _activeTileKey.HasValue ? tile.Bounds : (Rect?)null;
            foreach (var batch in scene.Batches)
            {
                if (clip.HasValue && !batch.Bounds.IntersectsWith(clip.Value)) continue;
                StreamGeometry geometry;
                if (!clip.HasValue)
                {
                    if (!_compiledBatchGeometries.TryGetValue(batch, out geometry!)) continue;
                }
                else
                {
                    // Local views use the same contiguous batch data, restricted by range bounds.
                    geometry = BuildBatchGeometry(batch, clip);
                }
                var pen = GetPen(batch.Color, GetScaledLineWidth(batch.LineWidth), LineDashStyle.Solid);
                dc.DrawGeometry(null, pen, geometry);
            }
        }

        /// <summary>
        /// 渲染一层：图形项按原顺序立即绘制；文本项收集后统一分组排版，
        /// 保证"同一锚点的多条文本（可跨 TextStyle）"协同换行而非相互重叠。
        /// </summary>
        private void RenderLayer(DrawingContext dc, IReadOnlyList<DisplayItemBase>? items)
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
                RenderTextsGrouped(dc, texts);
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
            if (style == null || !style.IsVisible) return;

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
                    // Batch scene owns all visible, non-transformed batched contours.
                    // Keep the legacy geometry cache only while the batch scene is compiling.
                    if (_batchScene != null)
                        continue;
                    var isPass = isUnified || (item.IsPassArray.Length > i && item.IsPassArray[i]);
                    if (_batchGeometryCache.TryGetValue(batch, out var cachedGeometry))
                    {
                        var cachedFill = item.IsFilled
                            ? (isUnified || isPass ? defaultBrush : ngBrush)
                            : null;
                        var cachedPen = isUnified || isPass ? defaultPen : ngPen;
                        IEnumerable<StreamGeometry> visibleGeometries;
                        var renderPath = "tile";
                        if (_activeTileKey is RenderTileKey activeTileKey)
                        {
                            visibleGeometries = cachedGeometry.TileGeometries.TryGetValue(
                                (activeTileKey.X, activeTileKey.Y), out var tileGeometries)
                                ? tileGeometries
                                : Array.Empty<StreamGeometry>();
                        }
                        else if (!_hasViewport)
                        {
                            visibleGeometries = new[] { cachedGeometry.FullGeometry };
                            renderPath = "full-batch";
                        }
                        else
                        {
                            var minTileX = (int)Math.Floor(_viewLeft / GeometryTileSize);
                            var maxTileX = (int)Math.Floor(_viewRight / GeometryTileSize);
                            var minTileY = (int)Math.Floor(_viewTop / GeometryTileSize);
                            var maxTileY = (int)Math.Floor(_viewBottom / GeometryTileSize);
                            var selected = new HashSet<StreamGeometry>();
                            var selectedTileCount = 0;
                            for (var tileX = minTileX; tileX <= maxTileX; tileX++)
                            for (var tileY = minTileY; tileY <= maxTileY; tileY++)
                                if (cachedGeometry.TileGeometries.TryGetValue((tileX, tileY), out var tileGeometries))
                                {
                                    selectedTileCount++;
                                    foreach (var geometry in tileGeometries)
                                        selected.Add(geometry);
                                }

                            // 大视口下逐 Tile DrawGeometry 的调用和 WPF 命令提交成本高于一次完整
                            // 批次绘制。覆盖批次 60% 以上 Tile 时直接提交一个完整冻结几何；
                            // 局部视口继续按 Tile 裁剪，避免把不可见几何送入绘制管线。
                            if (cachedGeometry.TileGeometries.Count > 0 &&
                                selectedTileCount * 5 >= cachedGeometry.TileGeometries.Count * 3)
                            {
                                visibleGeometries = new[] { cachedGeometry.FullGeometry };
                                renderPath = "full-batch";
                            }
                            else
                            {
                                visibleGeometries = selected;
                                renderPath = "tile-range";
                            }
                        }
                        var drawCalls = 0;
                        foreach (var geometry in visibleGeometries)
                        {
                            dc.DrawGeometry(cachedFill, cachedPen, geometry);
                            drawCalls++;
                        }
                        if (renderPath == "full-batch")
                        {
                            VisionLogger.Instance.Log(LogLevel.Debug,
                                $"[RenderDiag] Contour batch draw: path={renderPath}, contours={cachedGeometry.ContourCount}, points={cachedGeometry.PointCount}, drawCalls={drawCalls}, totalTiles={cachedGeometry.TileGeometries.Count}",
                                "ResultRenderLayer");
                        }
                        continue;
                    }
                    if (_batchCacheMissLogged.Add(batch))
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"[RenderDiag] Batch geometry cache not ready; skip fallback expansion: contours={batch.Contours.Count}, viewport={_hasViewport}",
                            "ResultRenderLayer");
                    }
                    // 缓存未完成时禁止逐轮廓回退。该回退会在每个可见 Tile 重复展开整批轮廓，
                    // 造成 UI 线程长时间占用和大对象内存峰值；缓存完成后由后台回调 RequestRender。
                    continue;
                }

                if (item.Geometries[i] is ContourGeometry contour && contour.Points != null)
                {
                    var pts = contour.Points;
                    if (pts.Length < 2) continue;

                    // Non-transformed contours are already emitted by the immutable batch scene.
                    // Transformed contours remain on the compatibility path because their matrix
                    // is part of the per-item interaction semantics.
                    if (_batchScene != null && !contour.Transform.HasValue)
                        continue;

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
        private void RenderTextsGrouped(DrawingContext dc, List<TextDisplayItem> items)
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
                    if (_hasViewport && !IsInViewport(new Point(td.X, td.Y)))
                        continue;
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

            // 同锚点分组（锚点屏幕距离 ≤ 容差）
            double anchorTolerance = ViewportScaling.InvScale(_viewportScale, TextAnchorToleranceScreen);
            var groups = new List<List<TextLine>>();
            foreach (var line in lines)
            {
                List<TextLine>? target = null;
                foreach (var group in groups)
                {
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
                    double scaledFontSize = ViewportScaling.InvScale(_viewportScale, style.FontSize);

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

        private FormattedText CreateFormattedText(string text, Color color, double fontSize, bool isBold)
        {
            var colorKey = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
            var key = (text, colorKey, (int)Math.Round(fontSize * 100), isBold);
            if (_formattedTextCache.TryGetValue(key, out var cached))
                return cached;
            if (_formattedTextCache.Count >= 4096)
                _formattedTextCache.Clear();
            var typeface = new Typeface(new FontFamily("Segoe UI"),
                FontStyles.Normal, isBold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
            // 使用自身实例的 DPI，而非依赖 Application.Current.MainWindow。
            // 依赖 MainWindow 在脚本工具/独立上下文渲染时可能为 null，导致 VisualTreeHelper.GetDpi(null) 抛 NullReferenceException。
            // GetDpi(this) 即使本元素尚未连接可视树也不会抛异常，返回默认 96 DPI。
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, fontSize, new SolidColorBrush(color), pixelsPerDip);
            _formattedTextCache[key] = formatted;
            return formatted;
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

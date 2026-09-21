using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.UI.Controls;

public readonly record struct ResultTileHostStats(
    int ViewportRefreshes,
    int TileCreates,
    int TileRenders,
    int TileReuses,
    int TileEvictions);

/// <summary>
/// Tile 化结果宿主。它只管理当前视口内的 DrawingVisual，具体图元绘制由后端回调完成。
/// </summary>
public class ResultTileHost : FrameworkElement
{
    private const int CompositeVisibleTileThreshold = 24;
    private static readonly RenderTileKey CompositeTileKey = new(int.MinValue, int.MinValue);
    private readonly VisualCollection _children;
    private readonly Dictionary<RenderTileKey, DrawingVisual> _visuals = new();
    private DrawingVisual? _compositeVisual;
    private RenderScene? _scene;
    private Rect _viewport;
    private bool _hasViewport;
    private Action<DrawingContext, RenderTileScene>? _renderTile;
    private int _viewportRefreshes;
    private int _tileCreates;
    private int _tileRenders;
    private int _tileReuses;
    private int _tileEvictions;
    private int _diagnosticTraceId;

    public ResultTileHost()
    {
        _children = new VisualCollection(this);
        IsHitTestVisible = false;
    }

    public void SetRenderBackend(Action<DrawingContext, RenderTileScene>? renderTile)
    {
        _renderTile = renderTile;
        RefreshVisibleTiles(redrawExisting: true, reason: "backend-change");
    }

    public void SetDiagnosticTraceId(int traceId) => _diagnosticTraceId = traceId;

    public void SetScene(RenderScene? scene)
    {
        _scene = scene;
        RefreshVisibleTiles(redrawExisting: true, reason: "scene-commit");
    }

    public void SetViewport(Rect viewport)
    {
        _viewport = viewport;
        _hasViewport = true;
        RefreshVisibleTiles(redrawExisting: false, reason: "viewport-change");
    }

    public void Refresh() => Refresh("explicit-redraw");

    protected void Refresh(string reason) => RefreshVisibleTiles(redrawExisting: true, reason);

    public ResultTileHostStats GetStats() => new(
        _viewportRefreshes, _tileCreates, _tileRenders, _tileReuses, _tileEvictions);

    public void ResetStats()
    {
        _viewportRefreshes = 0;
        _tileCreates = 0;
        _tileRenders = 0;
        _tileReuses = 0;
        _tileEvictions = 0;
    }

    public void Clear()
    {
        _scene = null;
        _hasViewport = false;
        foreach (var visual in _visuals.Values)
            _children.Remove(visual);
        _visuals.Clear();
        if (_compositeVisual != null)
            _children.Remove(_compositeVisual);
        _compositeVisual = null;
    }

    private void RefreshVisibleTiles(bool redrawExisting, string reason)
    {
        var refreshTimer = Stopwatch.StartNew();
        _viewportRefreshes++;
        // 场景可能先于图像布局完成。没有可靠视口时禁止渲染整个场景，
        // 等 ImageControl 首次提交视口后再创建可见 Tile，避免大结果一次性展开。
        if (_scene == null || _renderTile == null || !_hasViewport)
        {
            foreach (var visual in _visuals.Values)
                _children.Remove(visual);
            _visuals.Clear();
            if (_compositeVisual != null)
                _children.Remove(_compositeVisual);
            _compositeVisual = null;
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RenderDiag] Refresh skip: trace={_diagnosticTraceId}, reason={reason}, scene={_scene != null}, backend={_renderTile != null}, viewport={_hasViewport}, redraw={redrawExisting}, ms={refreshTimer.Elapsed.TotalMilliseconds:F1}",
                "ResultTileHost");
            return;
        }

        var visible = (_hasViewport ? _scene.Query(_viewport) : _scene.Tiles.Values)
            .ToDictionary(tile => tile.Key);
        var rendersBefore = _tileRenders;
        var createsBefore = _tileCreates;
        var tileRenderTotalMs = 0.0;
        var tileRenderMaxMs = 0.0;

        if (visible.Count >= CompositeVisibleTileThreshold)
        {
            foreach (var visual in _visuals.Values)
                _children.Remove(visual);
            _visuals.Clear();

            var wasComposite = _compositeVisual != null;
            _compositeVisual ??= new DrawingVisual();
            if (!wasComposite)
            {
                _children.Add(_compositeVisual);
                _tileCreates++;
            }

            // 大视口下多个 Tile 会重复进入 WPF DrawingVisual 提交路径。
            // 合并相交 Tile 的结果项并按引用去重，只提交一个 Visual。
            var items = visible.Values
                .SelectMany(tile => tile.Items)
                .Distinct()
                .ToList();
            var renderTimer = Stopwatch.StartNew();
            using (var dc = _compositeVisual.RenderOpen())
            {
                _renderTile(dc, new RenderTileScene(CompositeTileKey, _viewport, items));
            }
            renderTimer.Stop();
            _tileRenders++;
            var elapsedMs = refreshTimer.Elapsed.TotalMilliseconds;
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RenderDiag] Refresh done: trace={_diagnosticTraceId}, reason={reason}, mode=composite, sceneTiles={_scene.Tiles.Count}, visible={visible.Count}, viewport={FormatViewport(_viewport)}, redraw={redrawExisting}, items={items.Count}, renders={_tileRenders - rendersBefore}, renderMs={renderTimer.Elapsed.TotalMilliseconds:F1}, cached={_tileReuses}, evicted={_tileEvictions}, ms={elapsedMs:F1}",
                "ResultTileHost");
            AnalyzeRefresh("composite", reason, visible.Count, 1, elapsedMs,
                renderTimer.Elapsed.TotalMilliseconds, renderTimer.Elapsed.TotalMilliseconds);
            return;
        }

        if (_compositeVisual != null)
        {
            _children.Remove(_compositeVisual);
            _compositeVisual = null;
        }

        foreach (var key in _visuals.Keys.Where(key => !visible.ContainsKey(key)).ToList())
        {
            _children.Remove(_visuals[key]);
            _visuals.Remove(key);
            _tileEvictions++;
        }

        foreach (var pair in visible)
        {
            var isNew = !_visuals.TryGetValue(pair.Key, out var visual);
            if (isNew)
            {
                visual = new DrawingVisual();
                _visuals[pair.Key] = visual;
                _children.Add(visual);
                _tileCreates++;
            }
            else if (!redrawExisting)
                _tileReuses++;

            if (redrawExisting || isNew)
            {
                var tileTimer = Stopwatch.StartNew();
                using var dc = visual.RenderOpen();
                _renderTile(dc, pair.Value);
                tileTimer.Stop();
                tileRenderTotalMs += tileTimer.Elapsed.TotalMilliseconds;
                tileRenderMaxMs = Math.Max(tileRenderMaxMs, tileTimer.Elapsed.TotalMilliseconds);
                _tileRenders++;
            }
        }

        var totalMs = refreshTimer.Elapsed.TotalMilliseconds;
        var renderedCount = _tileRenders - rendersBefore;
        VisionLogger.Instance.Log(LogLevel.Info,
            $"[RenderDiag] Refresh done: trace={_diagnosticTraceId}, reason={reason}, mode=tiles, sceneTiles={_scene.Tiles.Count}, visible={visible.Count}, viewport={FormatViewport(_viewport)}, redraw={redrawExisting}, new={_tileCreates - createsBefore}, renders={renderedCount}, tileTotalMs={tileRenderTotalMs:F1}, tileMaxMs={tileRenderMaxMs:F1}, cached={_tileReuses}, evicted={_tileEvictions}, ms={totalMs:F1}",
            "ResultTileHost");
        AnalyzeRefresh("tiles", reason, visible.Count, renderedCount, totalMs,
            tileRenderTotalMs, tileRenderMaxMs);
    }

    private void AnalyzeRefresh(
        string mode,
        string reason,
        int visibleCount,
        int renderedCount,
        double elapsedMs,
        double renderMs,
        double maxTileMs)
    {
        if (elapsedMs < 100) return;

        var overheadMs = Math.Max(0, elapsedMs - renderMs);
        var diagnosis = mode == "composite" && renderMs >= elapsedMs * 0.70
            ? "composite-geometry-heavy"
            : mode == "tiles" && maxTileMs >= 50
                ? "single-tile-heavy"
                : mode == "tiles" && renderedCount >= 16 && renderMs >= elapsedMs * 0.70
                    ? "tile-count-heavy"
                    : overheadMs >= 100
                        ? "visual-management-heavy"
                        : "mixed-render-cost";

        using var process = Process.GetCurrentProcess();
        var managedMb = GC.GetTotalMemory(false) / 1024d / 1024d;
        var workingSetMb = process.WorkingSet64 / 1024d / 1024d;
        VisionLogger.Instance.Log(LogLevel.Warning,
            $"[RenderAnalysis] trace={_diagnosticTraceId}, diagnosis={diagnosis}, reason={reason}, mode={mode}, visible={visibleCount}, renders={renderedCount}, totalMs={elapsedMs:F1}, drawMs={renderMs:F1}, overheadMs={overheadMs:F1}, maxTileMs={maxTileMs:F1}, managedMB={managedMb:F1}, workingSetMB={workingSetMb:F1}",
            "ResultTileHost");
    }

    private static string FormatViewport(Rect viewport) =>
        $"{viewport.X:F0},{viewport.Y:F0},{viewport.Width:F0}x{viewport.Height:F0}";

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index) => _children[index];
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Rendering
{
    /// <summary>
    /// ROI 区域渲染器（轻量版）：
    /// 以单个 RoiVisualHost（UIElement）承载"每区域一个 DrawingVisual"，替代旧的三池 UIElement 对象树，
    /// 使大量区域（数百~数千）不再物化为控件，消除布局/命中/渲染的对象树成本（ROI 渲染轻量化方案）。
    /// - 视觉由 RegionDcPainter 单一实现（形状/方向箭头/标签全在 DC）
    /// - 增量语义保留：Render 时按区域数据快照做 diff，只重画实际变化的区域
    /// - 命中与交互均为数学命中，与本渲染器无关；接口语义与旧版完全一致，调用方无感知
    /// </summary>
    public class WpfShapeRenderer : IRoiRenderer
    {
        private sealed class RegionVisual
        {
            public DrawingVisual Visual = new();
            public RoiRenderContext? Last;
        }

        private readonly Canvas _targetCanvas;
        private readonly ILogger? _logger;
        private readonly string _rendererInstanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
        private readonly RoiVisualHost _host = new();
        private readonly Dictionary<Guid, RegionVisual> _regions = new();

        private double _viewportScale = 1.0;
        private Point _viewportOffset = new(0, 0);
        private bool _forceAllRedraw;
        private double _pixelsPerDip = 1.0;

        private int _width;
        public int Width { get => _width; set => _width = value; }

        private int _height;
        public int Height { get => _height; set => _height = value; }

        private double _dpiScale = 1.0;
        public double DpiScale
        {
            get => _dpiScale;
            set => _dpiScale = Math.Max(0.1, value);
        }

        public ImageSource? RenderTarget => null;

        public WpfShapeRenderer(Canvas targetCanvas, ILogger? logger = null)
        {
            _targetCanvas = targetCanvas ?? throw new ArgumentNullException(nameof(targetCanvas));
            _logger = logger;

            // 唯一常驻 UIElement：视觉宿主
            _targetCanvas.Children.Add(_host);

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[WpfShapeRenderer.ctor] 轻量渲染器已创建 | InstanceId={_rendererInstanceId} | Canvas={targetCanvas.GetType().Name}",
                "WpfShapeRenderer");
        }

        public void Render(IEnumerable<RoiRenderContext> regions, RoiRenderOptions? options = null)
        {
            options ??= new RoiRenderOptions();

            var visible = new List<RoiRenderContext>();
            foreach (var region in regions)
            {
                if (!region.IsVisible) continue;
                if (options.ClipRect.HasValue && !options.ClipRect.Value.IntersectsWith(region.Bounds))
                    continue;
                visible.Add(region);
            }

            var visibleIds = new HashSet<Guid>(visible.Select(r => r.Id));
            RemoveInvisibleRegions(visibleIds);

            foreach (var region in visible)
                RedrawIfChanged(region);
        }

        public void UpdateRegion(RoiRenderContext region)
        {
            var rv = GetOrCreateVisual(region.Id);
            Redraw(rv, region);
        }

        public void Clear()
        {
            _host.Clear();
            _regions.Clear();
            _forceAllRedraw = false;

            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[WpfShapeRenderer.Clear] 清空渲染器 | InstanceId={_rendererInstanceId} | RegionVisuals={_regions.Count}",
                "WpfShapeRenderer");
        }

        public void SetViewportTransform(double scale, Point offset)
        {
            bool scaleChanged = Math.Abs(_viewportScale - scale) > 0.001;
            _viewportScale = scale;
            _viewportOffset = offset;
            if (scaleChanged)
            {
                // 缩放变化影响线宽/字号反除 → 全量重画一次（低频）
                _forceAllRedraw = true;
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[SetViewportTransform] _viewportScale={_viewportScale:F3} → 触发全量重画", "WpfShapeRenderer");
            }
        }

        public void Dispose()
        {
            _host.Clear();
            _regions.Clear();
            if (_targetCanvas.Children.Contains(_host))
                _targetCanvas.Children.Remove(_host);

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[WpfShapeRenderer.Dispose] 销毁完成 | InstanceId={_rendererInstanceId}", "WpfShapeRenderer");
        }

        #region 私有实现

        private void RemoveInvisibleRegions(HashSet<Guid> visibleIds)
        {
            if (_regions.Count == 0) return;
            var toRemove = _regions.Where(kv => !visibleIds.Contains(kv.Key)).Select(kv => kv.Key).ToList();
            if (toRemove.Count == 0) return;

            foreach (var id in toRemove)
            {
                if (_regions.Remove(id, out var rv))
                    _host.Remove(rv.Visual);
            }

            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[WpfShapeRenderer] 移除不可见区域 | InstanceId={_rendererInstanceId} | RemoveCount={toRemove.Count} | Remaining={_regions.Count}",
                "WpfShapeRenderer");
        }

        private RegionVisual GetOrCreateVisual(Guid id)
        {
            if (_regions.TryGetValue(id, out var rv))
                return rv;

            rv = new RegionVisual();
            _regions[id] = rv;
            _host.Add(rv.Visual);
            return rv;
        }

        /// <summary>区域级 diff：仅在数据变化或强制全量时重画该区域 visual。</summary>
        private void RedrawIfChanged(RoiRenderContext region)
        {
            var rv = GetOrCreateVisual(region.Id);
            if (_forceAllRedraw || rv.Last == null || !SnapshotsEqual(rv.Last, region))
                Redraw(rv, region);
        }

        private void Redraw(RegionVisual rv, RoiRenderContext region)
        {
            using (var dc = rv.Visual.RenderOpen())
            {
                RegionDcPainter.DrawRegion(dc, region, _viewportScale, RefreshPixelsPerDip());
            }

            // 选中区域置顶（对齐旧版 Canvas.ZIndex(selected)=100）
            if (region.IsSelected)
                _host.MoveToTop(rv.Visual);

            rv.Last = region;
        }

        private double RefreshPixelsPerDip()
        {
            if (_host.IsLoaded || VisualTreeHelper.GetParent(_host) != null)
                _pixelsPerDip = Math.Max(1.0, VisualTreeHelper.GetDpi(_host).PixelsPerDip);
            return _pixelsPerDip;
        }

        /// <summary>比较两次渲染上下文是否视觉等价（决定是否需要重画）。</summary>
        private static bool SnapshotsEqual(RoiRenderContext a, RoiRenderContext b)
        {
            if (a.ShapeType != b.ShapeType || a.RoiType != b.RoiType) return false;
            if (a.IsSelected != b.IsSelected || a.IsPreview != b.IsPreview) return false;
            if (a.FillColor != b.FillColor || a.StrokeColor != b.StrokeColor) return false;
            if (a.Opacity != b.Opacity || a.StrokeThickness != b.StrokeThickness) return false;
            if (!string.Equals(a.Label, b.Label, StringComparison.Ordinal)) return false;

            switch (a.ShapeType)
            {
                case ShapeType.Rectangle:
                case ShapeType.RotatedRectangle:
                    return PointsEqual(a.Center, b.Center) && a.Width == b.Width && a.Height == b.Height
                        && (a.ShapeType != ShapeType.RotatedRectangle || a.Angle == b.Angle);

                case ShapeType.Circle:
                    return PointsEqual(a.Center, b.Center) && a.Radius == b.Radius;

                case ShapeType.Line:
                    return PointsEqual(a.LineStart, b.LineStart) && PointsEqual(a.LineEnd, b.LineEnd);

                case ShapeType.Polygon:
                    if (a.PolygonPoints.Count != b.PolygonPoints.Count) return false;
                    for (int i = 0; i < a.PolygonPoints.Count; i++)
                        if (!PointsEqual(a.PolygonPoints[i], b.PolygonPoints[i]))
                            return false;
                    return true;

                default:
                    return false;
            }
        }

        private static bool PointsEqual(Point a, Point b) => a.X == b.X && a.Y == b.Y;

        #endregion
    }
}

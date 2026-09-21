using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.Models.Visualization;

/// <summary>
/// Compiles compatible contour geometry into contiguous ranges. This is deliberately
/// independent of WPF DrawingContext so a native GPU backend can upload the same scene.
/// </summary>
public sealed class RenderBatchCompiler : IRenderBatchCompiler
{
    public RenderBatchScene Compile(IReadOnlyList<DisplayItemBase> items, long imageIdentity, long renderGeneration)
    {
        ArgumentNullException.ThrowIfNull(items);
        var groups = new Dictionary<(uint Color, double Width, bool Closed), Builder>();

        foreach (var graphics in items.OfType<GraphicsDisplayItem>().Where(x => x.IsVisible))
        {
            var style = graphics.Style;
            if (style == null) continue;
            var width = Math.Max(0.1, style.LineWidth);
            for (var i = 0; i < graphics.Geometries.Length; i++)
            {
                if (graphics.Geometries[i] is ContourGeometry contour)
                {
                    if (contour.Transform.HasValue || contour.Points.Length < 2) continue;
                    Add(groups, style, graphics, width, i, contour.Points, contour.IsClosed);
                }
                else if (graphics.Geometries[i] is BatchedContourGeometry batch)
                {
                    for (var contourIndex = 0; contourIndex < batch.Contours.Count; contourIndex++)
                    {
                        var points = batch.Contours[contourIndex];
                        if (points.Length < 2) continue;
                        // BatchedContourGeometry carries one pass flag per contour in the
                        // owning display item. The batch compiler preserves that semantic.
                        var passIndex = i;
                        Add(groups, style, graphics, width, passIndex, points, batch.IsClosed);
                    }
                }
            }
        }

        return new RenderBatchScene(imageIdentity, renderGeneration,
            groups.Values.Select(x => x.Build()).Where(x => x.Ranges.Count > 0).ToArray());
    }

    private static void Add(Dictionary<(uint Color, double Width, bool Closed), Builder> groups,
        GraphicsStyle style, GraphicsDisplayItem graphics, double width, int passIndex,
        Point[] points, bool closed)
    {
        var pass = graphics.RenderMode == DisplayRenderMode.Unified ||
                   graphics.IsPassArray.Length <= passIndex || graphics.IsPassArray[passIndex];
        var color = style.GetEffectiveColorArgb(pass);
        var key = (color, width, closed);
        if (!groups.TryGetValue(key, out var builder))
            groups[key] = builder = new Builder(color, width, closed);
        builder.Add(points);
    }

    private sealed class Builder
    {
        private readonly List<Point> _vertices = new();
        private readonly List<RenderBatchRange> _ranges = new();
        private readonly uint _color;
        private readonly double _width;
        private readonly bool _closed;
        private Rect _bounds = Rect.Empty;

        public Builder(uint color, double width, bool closed) => (_color, _width, _closed) = (color, width, closed);

        public void Add(Point[] points)
        {
            var bounds = Bounds(points);
            if (bounds.IsEmpty) return;
            var start = _vertices.Count;
            _vertices.AddRange(points);
            _ranges.Add(new RenderBatchRange(start, points.Length, bounds));
            _bounds = _bounds.IsEmpty ? bounds : Rect.Union(_bounds, bounds);
        }

        public RenderBatch Build() => new(
            $"{_color:X8}:{_width:R}:{_closed}", _color, _width, _closed, _vertices, _ranges, _bounds);

        private static Rect Bounds(IReadOnlyList<Point> points)
        {
            var minX = points.Min(x => x.X); var minY = points.Min(x => x.Y);
            var maxX = points.Max(x => x.X); var maxY = points.Max(x => x.Y);
            return new Rect(new Point(minX, minY), new Point(maxX, maxY));
        }
    }
}

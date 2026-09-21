using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.Models.Visualization;

/// <summary>
/// 默认场景编译器。第一阶段按结果项的图像边界归入 Tile；后续可替换为按单条几何细分的编译器。
/// </summary>
public sealed class RenderSceneCompiler : IRenderSceneCompiler
{
    public RenderScene Compile(IReadOnlyList<DisplayItemBase> items, double tileSize = RenderScene.DefaultTileSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (tileSize <= 0) throw new ArgumentOutOfRangeException(nameof(tileSize));

        var tiles = new Dictionary<RenderTileKey, List<DisplayItemBase>>();
        foreach (var item in items)
        {
            var bounds = GetBounds(item);
            if (bounds.IsEmpty)
                bounds = new Rect(0, 0, tileSize, tileSize);

            var minX = (int)Math.Floor(bounds.Left / tileSize);
            var maxX = (int)Math.Floor(bounds.Right / tileSize);
            var minY = (int)Math.Floor(bounds.Top / tileSize);
            var maxY = (int)Math.Floor(bounds.Bottom / tileSize);
            for (var x = minX; x <= maxX; x++)
            for (var y = minY; y <= maxY; y++)
            {
                var key = new RenderTileKey(x, y);
                if (!tiles.TryGetValue(key, out var list))
                    tiles[key] = list = new List<DisplayItemBase>();
                list.Add(item);
            }
        }

        var scenes = tiles.ToDictionary(
            pair => pair.Key,
            pair => new RenderTileScene(pair.Key,
                new Rect(pair.Key.X * tileSize, pair.Key.Y * tileSize, tileSize, tileSize),
                pair.Value));
        return new RenderScene(tileSize, scenes);
    }

    private static Rect GetBounds(DisplayItemBase item)
    {
        if (item is TextDisplayItem text && text.Texts.Length > 0)
        {
            var points = text.Texts.Select(t => new Point(t.X, t.Y));
            return Bounds(points);
        }

        if (item is not GraphicsDisplayItem graphics)
            return Rect.Empty;

        var geometryPoints = new List<Point>();
        foreach (var geometry in graphics.Geometries)
        {
            switch (geometry)
            {
                case Point point:
                    geometryPoints.Add(point);
                    break;
                case LineSegment line:
                    geometryPoints.Add(line.Start); geometryPoints.Add(line.End);
                    break;
                case ContourGeometry contour:
                    geometryPoints.AddRange(contour.Points);
                    break;
                case BatchedContourGeometry batch:
                    foreach (var contourPoints in batch.Contours)
                        geometryPoints.AddRange(contourPoints);
                    break;
            }
        }
        return Bounds(geometryPoints);
    }

    private static Rect Bounds(IEnumerable<Point> points)
    {
        var array = points.ToArray();
        if (array.Length == 0) return Rect.Empty;
        var minX = array.Min(p => p.X);
        var minY = array.Min(p => p.Y);
        var maxX = array.Max(p => p.X);
        var maxY = array.Max(p => p.Y);
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }
}

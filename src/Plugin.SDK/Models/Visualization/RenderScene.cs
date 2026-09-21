using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.Models.Visualization;

/// <summary>
/// 图像坐标系中的 Tile 标识。Tile 只表示空间分区，不包含 WPF 控件。
/// </summary>
public readonly record struct RenderTileKey(int X, int Y);

/// <summary>
/// 一次结果编译产生的不可变渲染场景。
/// </summary>
public sealed class RenderScene
{
    public const double DefaultTileSize = 512.0;

    public RenderScene(double tileSize, IReadOnlyDictionary<RenderTileKey, RenderTileScene> tiles)
    {
        if (tileSize <= 0) throw new ArgumentOutOfRangeException(nameof(tileSize));
        TileSize = tileSize;
        Tiles = new ReadOnlyDictionary<RenderTileKey, RenderTileScene>(
            new Dictionary<RenderTileKey, RenderTileScene>(tiles));
    }

    public double TileSize { get; }
    public IReadOnlyDictionary<RenderTileKey, RenderTileScene> Tiles { get; }

    public IEnumerable<RenderTileScene> Query(Rect viewport)
    {
        var minX = (int)Math.Floor(viewport.Left / TileSize);
        var maxX = (int)Math.Floor(viewport.Right / TileSize);
        var minY = (int)Math.Floor(viewport.Top / TileSize);
        var maxY = (int)Math.Floor(viewport.Bottom / TileSize);

        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
            if (Tiles.TryGetValue(new RenderTileKey(x, y), out var tile))
                yield return tile;
    }

    /// <summary>
    /// 查询命中候选。空间索引只负责缩小范围，调用方仍需对候选几何执行精确命中测试。
    /// </summary>
    public IReadOnlyList<DisplayItemBase> QueryCandidates(Rect area)
    {
        var candidates = new List<DisplayItemBase>();
        var seen = new HashSet<DisplayItemBase>();
        foreach (var tile in Query(area))
        foreach (var item in tile.Items)
            if (seen.Add(item))
                candidates.Add(item);
        return candidates;
    }

    public IReadOnlyList<DisplayItemBase> QueryCandidates(Point point, double tolerance = 0)
    {
        if (tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        return QueryCandidates(new Rect(
            point.X - tolerance, point.Y - tolerance,
            tolerance * 2, tolerance * 2));
    }
}

/// <summary>
/// Tile 内的结果项集合。算法层只提供数据，WPF 后端负责将其编译为 DrawingVisual。
/// </summary>
public sealed class RenderTileScene
{
    public RenderTileScene(RenderTileKey key, Rect bounds, IReadOnlyList<DisplayItemBase> items)
    {
        Key = key;
        Bounds = bounds;
        Items = new ReadOnlyCollection<DisplayItemBase>(items.ToList());
    }

    public RenderTileKey Key { get; }
    public Rect Bounds { get; }
    public IReadOnlyList<DisplayItemBase> Items { get; }
}

/// <summary>
/// 结果层与具体 WPF/Direct2D 后端之间的编译边界。
/// </summary>
public interface IRenderSceneCompiler
{
    RenderScene Compile(IReadOnlyList<DisplayItemBase> items, double tileSize = RenderScene.DefaultTileSize);
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.Models.Visualization;

/// <summary>
/// Immutable, backend-neutral geometry batch. The renderer owns the GPU/WPF resource
/// represented by this data; the SDK never owns a graphics device.
/// </summary>
public sealed class RenderBatch
{
    public RenderBatch(string key, uint color, double lineWidth, bool closed,
        IReadOnlyList<Point> vertices, IReadOnlyList<RenderBatchRange> ranges, Rect bounds)
    {
        Key = key;
        Color = color;
        LineWidth = Math.Max(0.1, lineWidth);
        Closed = closed;
        Vertices = new ReadOnlyCollection<Point>(vertices.ToArray());
        Ranges = new ReadOnlyCollection<RenderBatchRange>(ranges.ToArray());
        Bounds = bounds;
    }

    public string Key { get; }
    public uint Color { get; }
    public double LineWidth { get; }
    public bool Closed { get; }
    public IReadOnlyList<Point> Vertices { get; }
    public IReadOnlyList<RenderBatchRange> Ranges { get; }
    public Rect Bounds { get; }
}

public readonly record struct RenderBatchRange(int StartVertex, int VertexCount, Rect Bounds);

/// <summary>Compiled result scene consumed by D3D11, Direct2D or WPF fallback backends.</summary>
public sealed class RenderBatchScene
{
    public RenderBatchScene(long imageIdentity, long renderGeneration, IReadOnlyList<RenderBatch> batches)
    {
        ImageIdentity = imageIdentity;
        RenderGeneration = renderGeneration;
        Batches = new ReadOnlyCollection<RenderBatch>(batches.ToArray());
    }

    public long ImageIdentity { get; }
    public long RenderGeneration { get; }
    public IReadOnlyList<RenderBatch> Batches { get; }
    public int VertexCount => Batches.Sum(x => x.Vertices.Count);
    public int DrawRangeCount => Batches.Sum(x => x.Ranges.Count);
}

public interface IRenderBatchCompiler
{
    RenderBatchScene Compile(IReadOnlyList<DisplayItemBase> items, long imageIdentity, long renderGeneration);
}

using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>Owns only resources created in this scope. Readers retain their frozen storage independently.</summary>
public sealed class FrozenOutputResources : IOutputResourceScope
{
    private readonly object _gate = new();
    private List<IDisposable>? _owned = new();

    public IOutputResourceScope CreateScope()
    {
        lock (_gate) { RequireOpen(); return new FrozenOutputResources(); }
    }

    private void RequireOpen() => ObjectDisposedException.ThrowIf(_owned is null, this);
    private T Own<T>(T resource) where T : IDisposable
    {
        lock (_gate)
        {
            if (_owned is null) { resource.Dispose(); throw new ObjectDisposedException(nameof(FrozenOutputResources)); }
            _owned.Add(resource); return resource;
        }
    }

    public IReadOnlyImage CreateGray8(int width, int height, ReadOnlySpan<byte> pixels)
    {
        var layout = new ImageLayout(width, height, width, PixelLayout.Gray8);
        var frame = new ImageFrameAddress(Guid.NewGuid(), "generated");
        var coordinates = new CoordinateFrame("image:generated", CoordinateDomain.Image, CoordinateUnit.Pixel);
        var runs = Enumerable.Range(0, height).Select(row => new RegionRun(row, 0, width - 1)).ToArray();
        return Own(new FrozenImageResource(layout, frame, coordinates, pixels, runs));
    }

    public IReadOnlyImage CreateImage(ImageLayout layout, ImageFrameAddress frame, IReadOnlyRegion effectiveDomain, ReadOnlySpan<byte> pixels)
    {
        using var domain = effectiveDomain.AcquireReader();
        if (domain.SourceFrame != frame) throw new ArgumentException("Output image and actual Domain must have the same physical frame.");
        return Own(new FrozenImageResource(layout, frame, domain.Frame, pixels, CopyRuns(domain)));
    }

    public IReadOnlyImage RetainInput(IReadOnlyImage borrowedInput)
    {
        ArgumentNullException.ThrowIfNull(borrowedInput);
        if (borrowedInput is FrozenImageResource frozen) return Own(frozen.RetainSnapshot());
        using var reader = borrowedInput.AcquirePixels();
        using var domain = reader.AcquireDomainReader();
        if (domain.SourceFrame != reader.Frame) throw new ArgumentException("Input pixels and Domain have different physical frames.");
        var pixels = new byte[reader.Layout.BufferLength];
        for (int row = 0; row < reader.Layout.Height; row++)
        {
            var source = reader.GetRow(row);
            if (source.Length != reader.Layout.RowStrideBytes) throw new InvalidDataException("Pixel reader returned an invalid row.");
            source.CopyTo(pixels.AsSpan(row * reader.Layout.RowStrideBytes));
        }
        return Own(new FrozenImageResource(reader.Layout, reader.Frame, domain.Frame, pixels, CopyRuns(domain)));
    }

    public IReadOnlyRegion CreateRegion(CoordinateFrame frame, ImageFrameAddress sourceFrame, ReadOnlySpan<RegionRun> runs)
        => Own(new FrozenRegionResource(frame, sourceFrame, runs));

    public IReadOnlyRegion RetainRegion(IReadOnlyRegion borrowedRegion)
    {
        using var reader = borrowedRegion.AcquireReader();
        return CreateRegion(reader.Frame, reader.SourceFrame, CopyRuns(reader));
    }

    private static RegionRun[] CopyRuns(IRegionReadLease reader)
    {
        if (reader.RunCount < 0) throw new InvalidDataException("Negative region run count.");
        var result = new RegionRun[reader.RunCount];
        for (int offset = 0; offset < result.Length;)
        {
            int requested = Math.Min(ResourceContractRules.MaximumRegionBatch, result.Length - offset);
            var batch = reader.ReadRuns(offset, requested);
            if (batch.Length == 0 || batch.Length > requested) throw new InvalidDataException("Region reader returned an invalid batch.");
            batch.Span.CopyTo(result.AsSpan(offset)); offset += batch.Length;
        }
        ResourceContractRules.ValidateRuns(result); return result;
    }

    public void Dispose()
    {
        List<IDisposable>? resources;
        lock (_gate) { resources = _owned; _owned = null; }
        if (resources is null) return;
        var errors = new List<Exception>();
        for (int i = resources.Count - 1; i >= 0; i--)
            try { resources[i].Dispose(); } catch (Exception e) { errors.Add(e); }
        resources.Clear();
        if (errors.Count != 0) throw new AggregateException(errors);
    }
}

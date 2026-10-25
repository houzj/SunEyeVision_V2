using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public readonly record struct BlobBounds(double X, double Y, double Width, double Height);
public sealed record BlobBoundaryPath(long Id, long? ParentId, string Role, IReadOnlyList<Point2> Points);
public sealed record BlobMeasurement(long ElementId, int Label, double Area, double Perimeter,
    double CentroidX, double CentroidY, double Circularity, double Rectangularity, BlobBounds Bounds,
    IReadOnlyList<Point2> RotatedBounds, IReadOnlyList<BlobBoundaryPath> Boundary, IReadOnlyRegion Region, bool JudgmentPassed);

/// <summary>One final ordered result set. Independent resource readers can outlive this output owner.</summary>
public sealed class BlobOutput : IToolOutput, IDisposable
{
    private readonly IDisposable[] _owned;
    private int _disposed;
    internal BlobOutput(IEnumerable<BlobMeasurement> measurements, IReadOnlyImage? gray, IReadOnlyImage? binary,
        IReadOnlyRegion region, IReadOnlyRegion ok, IReadOnlyRegion ng, double whiteRatio,
        double[] histogram, double[] means, bool isPass, IEnumerable<string> messages, IEnumerable<IDisposable> owned)
    {
        Measurements = Array.AsReadOnly(measurements.ToArray()); GrayImage = gray; BinaryImage = binary;
        Region = region; OkRegion = ok; NgRegion = ng; WhitePixelRatio = whiteRatio;
        Histogram = Array.AsReadOnly(histogram.ToArray()); ChannelMeans = Array.AsReadOnly(means.ToArray());
        IsPass = isPass; JudgmentMessages = Array.AsReadOnly(messages.ToArray()); _owned = owned.Distinct<IDisposable>(ReferenceEqualityComparer.Instance).ToArray();
    }
    public IReadOnlyList<BlobMeasurement> Measurements { get; }
    public IReadOnlyImage? GrayImage { get; }
    public IReadOnlyImage? BinaryImage { get; }
    public IReadOnlyRegion Region { get; }
    public IReadOnlyRegion OkRegion { get; }
    public IReadOnlyRegion NgRegion { get; }
    public double WhitePixelRatio { get; }
    public IReadOnlyList<double> Histogram { get; }
    public IReadOnlyList<double> ChannelMeans { get; }
    public bool IsPass { get; }
    public IReadOnlyList<string> JudgmentMessages { get; }
    public int Count => Measurements.Count;
    public double TotalArea => Measurements.Sum(x => x.Area);
    public double AverageArea => Count == 0 ? 0 : TotalArea / Count;
    public double MinimumArea => Count == 0 ? 0 : Measurements.Min(x => x.Area);
    public double MaximumArea => Count == 0 ? 0 : Measurements.Max(x => x.Area);
    public IReadOnlyDictionary<string, object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?> {
            [BlobSchema.BlobsPort] = new SequenceValue(BlobSchema.BlobsType, Measurements.Select(x => (object?)BlobSchema.Freeze(x))),
            [BlobSchema.Field(5)] = GrayImage, [BlobSchema.Field(6)] = BinaryImage,
            [BlobSchema.Field(11)] = (long)Count, [BlobSchema.Field(12)] = TotalArea,
            [BlobSchema.Field(13)] = AverageArea, [BlobSchema.Field(16)] = WhitePixelRatio,
            [BlobSchema.RegionPort] = Region
        });
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        var faults = new List<Exception>();
        foreach (var resource in _owned) try { resource.Dispose(); } catch (Exception e) { faults.Add(e); }
        if (faults.Count != 0) throw new AggregateException(faults);
    }
}

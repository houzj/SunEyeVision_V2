using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public readonly record struct BlobBounds(double X, double Y, double Width, double Height);
public readonly record struct BlobOrientedBounds(double CenterX,double CenterY,double Width,double Height,double AngleDegrees);
public sealed record BlobBoundaryPath(long Id, long? ParentId, string Role, IReadOnlyList<Point2> Points);
public sealed record BlobMeasurement(long ElementId, int Label, double Area, double Perimeter,
    double CentroidX, double CentroidY, double Circularity, double Rectangularity, BlobBounds Bounds,
    BlobOrientedBounds OrientedBounds, IReadOnlyList<Point2> RotatedBounds, IReadOnlyList<BlobBoundaryPath> Boundary, IReadOnlyRegion Region, bool JudgmentPassed,
    double? MinGrayValue = null, double? MaxGrayValue = null, double? GrayContrast = null);

/// <summary>One final ordered result set. Independent resource readers can outlive this output owner.</summary>
public sealed class BlobOutput : IToolOutput, IDisposable, IToolResultGeometryOutput
{
    private readonly IDisposable[] _owned;
    private int _disposed;
    internal BlobOutput(BlobSettings settings,BlobThresholdExecutionSnapshot threshold,IEnumerable<Diagnostic> diagnostics,IEnumerable<BlobSelectionEntry> selection, IEnumerable<BlobMeasurement> measurements, IReadOnlyImage? blobImage, IReadOnlyImage? binary,
        IReadOnlyRegion region, IReadOnlyRegion ok, IReadOnlyRegion ng, double whiteRatio,
        double[] histogram, double[] means, bool isPass, IEnumerable<string> messages, IEnumerable<IDisposable> owned,ResultGeometrySnapshot resultGeometry)
    {
        ResultGeometry=resultGeometry;
        ThresholdExecution=threshold;ExecutionDiagnostics=Array.AsReadOnly(diagnostics.ToArray());
        SelectionExecution=Array.AsReadOnly(selection.ToArray());
        ThresholdMinUsed=threshold.Lower;ThresholdMaxUsed=threshold.Upper;TypeUsed=threshold.Polarity;
        SegmentationModeUsed=settings.SegmentationMode.ToString();
        Measurements = Array.AsReadOnly(measurements.ToArray()); BlobImage = blobImage; BinaryImage = binary;
        VmResult = BlobVmResultProjection.Create(Measurements,binary,blobImage);
        Region = region; OkRegion = ok; NgRegion = ng; WhitePixelRatio = whiteRatio;
        Histogram = Array.AsReadOnly(histogram.ToArray()); ChannelMeans = Array.AsReadOnly(means.ToArray());
        IsPass = isPass; JudgmentMessages = Array.AsReadOnly(messages.ToArray()); _owned = owned.Distinct<IDisposable>(ReferenceEqualityComparer.Instance).ToArray();
    }
    public ResultGeometrySnapshot ResultGeometry {get;}
    public IReadOnlyList<BlobMeasurement> Measurements { get; }
    /// <summary>VM BlobFindResult 的中立投影，Unavailable 中的成员没有本方等价资源。</summary>
    public BlobVmResultProjection VmResult { get; }
    public BlobThresholdExecutionSnapshot ThresholdExecution {get;}
    public IReadOnlyList<Diagnostic> ExecutionDiagnostics {get;}
    public IReadOnlyList<BlobSelectionEntry> SelectionExecution {get;}
    public int? ThresholdMinUsed { get; }
    public int? ThresholdMaxUsed { get; }
    public string TypeUsed { get; }
    public string SegmentationModeUsed { get; }
    public IReadOnlyImage? BlobImage { get; }
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
    public double AverageCircularity => Count == 0 ? 0 : Measurements.Average(x => x.Circularity);
    public double AverageRectangularity => Count == 0 ? 0 : Measurements.Average(x => x.Rectangularity);
    private static RecordValue? ImageValue(IReadOnlyImage? image,TypeRef declaration)
    {
        if(image is null)return null;
        ResourceContractRules.ValidateImage(image);
        if(image.Layout.Format!=PixelLayout.Gray8)throw new NotSupportedException("Blob图像四成员发布仅支持真实Gray8格式。");
        return new(declaration,new Dictionary<string,object?>{
            ["data"]=image,["width"]=(long)image.Width,["height"]=(long)image.Height,["pixel-format"]=17301505L});
    }
    public IReadOnlyDictionary<string, object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?> {
            [BlobSchema.BlobsPort] = new SequenceValue(BlobSchema.BlobsType, Measurements.Select(x => (object?)BlobSchema.Freeze(x))),
            [BlobSchema.Field(5)] = ImageValue(BlobImage,BlobSchema.BlobImageType), [BlobSchema.Field(6)] = ImageValue(BinaryImage,BlobSchema.BinaryImageType),
            [BlobSchema.Field(4)] = IsPass,
            [BlobSchema.Field(8)] = ThresholdMinUsed is int lower ? (object)(long)lower : null, [BlobSchema.Field(9)] = ThresholdMaxUsed is int upper ? (object)(long)upper : null, [BlobSchema.Field(10)] = TypeUsed, [BlobSchema.Field(102)] = SegmentationModeUsed,
            [BlobSchema.Field(11)] = (long)Count, [BlobSchema.Field(12)] = TotalArea,
            [BlobSchema.Field(13)] = AverageArea, [BlobSchema.Field(16)] = WhitePixelRatio,
            [BlobSchema.Field(30)] = MinimumArea, [BlobSchema.Field(31)] = MaximumArea,
            [BlobSchema.Field(14)] = AverageCircularity,
            [BlobSchema.Field(15)] = AverageRectangularity,
            [BlobSchema.Field(17)] = Histogram.ToArray(), [BlobSchema.Field(18)] = ChannelMeans.ToArray(),
            [BlobSchema.Field(19)] = Measurements.Select(x=>x.Area).ToArray(), [BlobSchema.Field(20)] = Measurements.Select(x=>x.Perimeter).ToArray(),
            [BlobSchema.Field(21)] = Measurements.Select(x=>x.CentroidX).ToArray(), [BlobSchema.Field(22)] = Measurements.Select(x=>x.CentroidY).ToArray(),
            [BlobSchema.Field(23)] = Measurements.Select(x=>x.Circularity).ToArray(), [BlobSchema.Field(24)] = Measurements.Select(x=>x.Rectangularity).ToArray(),
            [BlobSchema.Field(25)] = Measurements.Select(x=>x.Bounds.Width).ToArray(), [BlobSchema.Field(26)] = Measurements.Select(x=>x.Bounds.Height).ToArray(),
            [BlobSchema.Field(27)] = new SequenceValue(TypeRef.SequenceOf((TypeRef)DataType.Text), Measurements.Select(x=>(object?)$"{(int)x.Bounds.Width}×{(int)x.Bounds.Height}")),
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

using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobLabels.Runtime;

public sealed record BlobLabelsBounds(double X, double Y, double Width, double Height);

public sealed record BlobLabelsOrientedBounds(double CenterX, double CenterY, double Width, double Height, double AngleDegrees)
{
    public double LongAxis => Math.Max(Width, Height);
    public double ShortAxis => Math.Min(Width, Height);
    public double AxisRatio => LongAxis <= 0 ? 0 : ShortAxis / LongAxis;
}

/// <summary>一次成功调用产生的单条标签结果；资源、判定与几何都属于同一次调用。</summary>
public sealed record BlobLabelsMeasurement(
    long ElementId,
    int Label,
    double Area,
    double Perimeter,
    double CentroidX,
    double CentroidY,
    double Circularity,
    double Rectangularity,
    BlobLabelsBounds Bounds,
    BlobLabelsOrientedBounds OrientedBounds,
    double AxisAngleDegrees,
    IReadOnlyList<Point2> Contour,
    IReadOnlyRegion Region,
    bool Passed,
    IReadOnlyList<string> FailedLimits);

/// <summary>
/// 原件 26 项输出的中立投影。原件没有等价资源或语义尚未取证的成员保持 <c>null</c> 并列入
/// <see cref="Unavailable"/>，结果页显示“不可用”而不是 0。
/// </summary>
public sealed record BlobLabelsResultProjection(
    int ModuleStatus,
    double ModuleRunTime,
    IReadOnlyList<int> BlobStatuses,
    int Count,
    IReadOnlyList<float> Areas,
    float TotalArea,
    IReadOnlyList<Point2> Centroids,
    IReadOnlyList<BlobLabelsOrientedBounds> MinimumBoundingRectangles,
    IReadOnlyList<float> BlobAngles,
    IReadOnlyList<float> Perimeters,
    IReadOnlyList<float> LongAxes,
    IReadOnlyList<float> ShortAxes,
    IReadOnlyList<float> Circularities,
    IReadOnlyList<float> Rectangularities,
    IReadOnlyList<float>? Scores,
    IReadOnlyList<BlobLabelsOrientedBounds>? InspectionRectangles,
    IReadOnlyList<BlobLabelsAnnulus>? InspectionAnnuli,
    RecordValue? ContourPoints,
    IReadOnlyList<string>? ClassLabels,
    IReadOnlyList<int>? LabelValues,
    IReadOnlyList<Point2>? BlindPolygonPoints,
    IReadOnlyList<string>? BlindPolygonStrings,
    RecordValue? OutputFixture,
    IReadOnlySet<string> Unavailable)
{
    /// <summary>
    /// 从同一次调用的测量结果投影原厂 26 项输出。原件没有等价资源的能力一律置空并列入
    /// <see cref="Unavailable"/>：分数、类别标签、屏蔽区多边形、检测区域列表目前都未取到原厂语义。
    /// </summary>
    public static BlobLabelsResultProjection Create(IReadOnlyList<BlobLabelsMeasurement> measurements, bool modulePassed,
        double runTimeMilliseconds, RecordValue? fixture)
    {
        var unavailable = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Scores), nameof(ClassLabels), nameof(LabelValues), nameof(BlindPolygonPoints),
            nameof(BlindPolygonStrings), nameof(InspectionRectangles), nameof(InspectionAnnuli)
        };
        var contourCount = measurements.Sum(item => item.Contour.Count);
        var contourPoints = contourCount == 0 ? null : Contour(measurements, contourCount);
        if (contourPoints is null) unavailable.Add(nameof(ContourPoints));
        return new(
            ModuleStatus: modulePassed ? 1 : 0,
            ModuleRunTime: runTimeMilliseconds,
            BlobStatuses: Array.AsReadOnly(measurements.Select(item => item.Passed ? 1 : 0).ToArray()),
            Count: measurements.Count,
            Areas: Array.AsReadOnly(measurements.Select(item => (float)item.Area).ToArray()),
            TotalArea: Float32Sum(measurements.Select(item => item.Area)),
            Centroids: Array.AsReadOnly(measurements.Select(item => new Point2(item.CentroidX, item.CentroidY)).ToArray()),
            MinimumBoundingRectangles: Array.AsReadOnly(measurements.Select(item => item.OrientedBounds).ToArray()),
            // 原厂把 MinBoudingRect.Angle 与顶层 BlobAngle 分开输出。
            // 前者来自最小外接矩形，后者对应 Blob 的区域主方向。这里沿用
            // Blob 工具已验证的 OrientationRegion 映射，不能把两个角度合并。
            BlobAngles: Array.AsReadOnly(measurements.Select(item => (float)item.AxisAngleDegrees).ToArray()),
            Perimeters: Array.AsReadOnly(measurements.Select(item => (float)item.Perimeter).ToArray()),
            LongAxes: Array.AsReadOnly(measurements.Select(item => (float)item.OrientedBounds.LongAxis).ToArray()),
            ShortAxes: Array.AsReadOnly(measurements.Select(item => (float)item.OrientedBounds.ShortAxis).ToArray()),
            Circularities: Array.AsReadOnly(measurements.Select(item => (float)item.Circularity).ToArray()),
            Rectangularities: Array.AsReadOnly(measurements.Select(item => (float)item.Rectangularity).ToArray()),
            Scores: null,
            InspectionRectangles: null,
            InspectionAnnuli: null,
            ContourPoints: contourPoints,
            ClassLabels: null,
            LabelValues: null,
            BlindPolygonPoints: null,
            BlindPolygonStrings: null,
            OutputFixture: fixture,
            Unavailable: unavailable);
    }

    private static float Float32Sum(IEnumerable<double> values)
    {
        var sum = 0f;
        foreach (var value in values) sum += (float)value;
        return sum;
    }

    private static RecordValue Contour(IReadOnlyList<BlobLabelsMeasurement> measurements, int count)
    {
        var x = new double[count];
        var y = new double[count];
        var score = new double[count];
        var index = new long[count];
        var position = 0;
        for (var blob = 0; blob < measurements.Count; blob++)
        {
            var measurement = measurements[blob];
            foreach (var point in measurement.Contour)
            {
                x[position] = (float)point.X;
                y[position] = (float)point.Y;
                score[position] = measurement.Passed ? 0 : 2;
                index[position++] = blob;
            }
        }
        return new(BlobLabelsSchema.ContourInfoType, new Dictionary<string, object?>
        {
            ["x"] = x, ["y"] = y, ["score"] = score, ["blob-index"] = index, ["count"] = (long)count
        });
    }
}

public sealed record BlobLabelsAnnulus(double CenterX, double CenterY, double InnerRadius, double OuterRadius, double StartDegrees, double SweepDegrees);

/// <summary>
/// BlobLabels 的输出对象。与 Blob 工具一致：调用成功即构造完整声明，
/// 资源所有权随本次调用的输出作用域释放。
/// </summary>
public sealed class BlobLabelsOutput : IToolOutput, IDisposable, IToolResultRecordOutput, IToolResultGeometryOutput
{
    private readonly IReadOnlyList<IDisposable> _owned;
    private readonly Dictionary<string, object?> _values;
    private int _disposed;

    public BlobLabelsOutput(BlobLabelsSettings settings, BlobLabelsResultProjection projection,
        IReadOnlyList<BlobLabelsMeasurement> measurements, IReadOnlyList<string> limitMessages,
        ResultGeometrySnapshot geometry, IReadOnlyImage? binaryImage, IReadOnlyImage? blobImage,
        IReadOnlyList<IDisposable> owned)
    {
        Settings = settings;
        Projection = projection;
        Measurements = measurements;
        LimitMessages = limitMessages;
        ResultGeometry = geometry;
        BinaryImage = binaryImage;
        BlobImage = blobImage;
        _owned = owned.Distinct<IDisposable>(ReferenceEqualityComparer.Instance).ToArray();
        _values = BuildValues();
        _ = new InputValues(BlobLabelsSchema.Outputs, _values);
    }

    public BlobLabelsSettings Settings { get; }
    public BlobLabelsResultProjection Projection { get; }
    public IReadOnlyList<BlobLabelsMeasurement> Measurements { get; }
    public IReadOnlyList<string> LimitMessages { get; }
    public ResultGeometrySnapshot ResultGeometry { get; }
    public IReadOnlyImage? BinaryImage { get; }
    public IReadOnlyImage? BlobImage { get; }
    public bool IsPass => Projection.ModuleStatus != 0;

    public IReadOnlyDictionary<string, object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _values;
    }

    private Dictionary<string, object?> BuildValues()
    {
        var projection = Projection;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BlobLabelsSchema.RunTime] = (double)(float)projection.ModuleRunTime,
            [BlobLabelsSchema.Status] = (long)projection.ModuleStatus,
            [BlobLabelsSchema.BlobStatuses] = new SequenceValue(BlobLabelsSchema.Outputs.Single(port => port.Id == BlobLabelsSchema.BlobStatuses).Type,
                projection.BlobStatuses.Select(status => (object?)(long)status)),
            [BlobLabelsSchema.Count] = (long)projection.Count,
            [BlobLabelsSchema.Areas] = new SequenceValue(BlobLabelsSchema.Outputs.Single(port => port.Id == BlobLabelsSchema.Areas).Type,
                Doubles(projection.Areas)),
            [BlobLabelsSchema.TotalArea] = (double)projection.TotalArea,
            [BlobLabelsSchema.Centroids] = new SequenceValue(BlobLabelsSchema.Outputs.Single(port => port.Id == BlobLabelsSchema.Centroids).Type,
                projection.Centroids.Select(point => (object?)new PointGeometry(BlobLabelsSchema.ImageFrame, point))),
            [BlobLabelsSchema.MinimumBoundingRectangles] = new SequenceValue(
                BlobLabelsSchema.Outputs.Single(port => port.Id == BlobLabelsSchema.MinimumBoundingRectangles).Type,
                projection.MinimumBoundingRectangles.Select(Rectangle)),
            [BlobLabelsSchema.BlobAngles] = Numbers(BlobLabelsSchema.BlobAngles, projection.BlobAngles),
            [BlobLabelsSchema.Perimeters] = Numbers(BlobLabelsSchema.Perimeters, projection.Perimeters),
            [BlobLabelsSchema.LongAxes] = Numbers(BlobLabelsSchema.LongAxes, projection.LongAxes),
            [BlobLabelsSchema.ShortAxes] = Numbers(BlobLabelsSchema.ShortAxes, projection.ShortAxes),
            [BlobLabelsSchema.Circularities] = Numbers(BlobLabelsSchema.Circularities, projection.Circularities),
            [BlobLabelsSchema.Rectangularities] = Numbers(BlobLabelsSchema.Rectangularities, projection.Rectangularities),
            [BlobLabelsSchema.Scores] = projection.Scores is null ? null : Numbers(BlobLabelsSchema.Scores, projection.Scores),
            [BlobLabelsSchema.InspectionRectangles] = projection.InspectionRectangles is null ? null : new SequenceValue(
                BlobLabelsSchema.Outputs.Single(port => port.Id == BlobLabelsSchema.InspectionRectangles).Type,
                projection.InspectionRectangles.Select(Rectangle)),
            [BlobLabelsSchema.InspectionAnnuli] = projection.InspectionAnnuli is null ? null : new SequenceValue(
                BlobLabelsSchema.Outputs.Single(port => port.Id == BlobLabelsSchema.InspectionAnnuli).Type,
                projection.InspectionAnnuli.Select(Annulus)),
            [BlobLabelsSchema.ContourPoints] = projection.ContourPoints,
            [BlobLabelsSchema.ClassLabels] = projection.ClassLabels is null ? null : new SequenceValue(
                BlobLabelsSchema.Outputs.Single(port => port.Id == BlobLabelsSchema.ClassLabels).Type,
                projection.ClassLabels.Select(text => (object?)text)),
            [BlobLabelsSchema.LabelValues] = projection.LabelValues is null ? null : new SequenceValue(
                BlobLabelsSchema.Outputs.Single(port => port.Id == BlobLabelsSchema.LabelValues).Type,
                projection.LabelValues.Select(value => (object?)(long)value)),
            [BlobLabelsSchema.BlindPolygon] = projection.BlindPolygonPoints is null ? null : new SequenceValue(
                BlobLabelsSchema.Outputs.Single(port => port.Id == BlobLabelsSchema.BlindPolygon).Type,
                projection.BlindPolygonPoints.Select(point => (object?)new PointGeometry(BlobLabelsSchema.ImageFrame, point))),
            [BlobLabelsSchema.BlindPolygonString] = projection.BlindPolygonStrings is null ? null : new SequenceValue(
                BlobLabelsSchema.Outputs.Single(port => port.Id == BlobLabelsSchema.BlindPolygonString).Type,
                projection.BlindPolygonStrings.Select(text => (object?)text)),
            [BlobLabelsSchema.OutputFixture] = projection.OutputFixture,
            [BlobLabelsSchema.BinaryImage] = BinaryImage is null ? null : BlobLabelsSchema.FreezeImage(BlobLabelsSchema.BinaryImageType,
                BinaryImage.Layout, BinaryImage.Width, BinaryImage.Height, BinaryImage),
            [BlobLabelsSchema.BlobImage] = BlobImage is null ? null : BlobLabelsSchema.FreezeImage(BlobLabelsSchema.BlobImageType,
                BlobImage.Layout, BlobImage.Width, BlobImage.Height, BlobImage),
            [BlobLabelsSchema.OutputMask] = null,
            [BlobLabelsSchema.Rows] = Rows()
        };
    }

    private SequenceValue Numbers(string portId, IReadOnlyList<float> values)
        => new(BlobLabelsSchema.Outputs.Single(port => port.Id == portId).Type, values.Select(value => (object?)(double)value));

    private static IEnumerable<object?> Doubles(IReadOnlyList<float> values) => values.Select(value => (object?)(double)value);

    private static RecordValue Rectangle(BlobLabelsOrientedBounds bounds)
        => BlobLabelsSchema.FreezeRectangle(bounds.CenterX, bounds.CenterY, bounds.Width, bounds.Height, bounds.AngleDegrees);

    private static RecordValue Annulus(BlobLabelsAnnulus annulus)
        => new(BlobLabelsSchema.AnnulusType, new Dictionary<string, object?>
        {
            ["center"] = BlobLabelsSchema.FreezePoint(new(annulus.CenterX, annulus.CenterY)),
            ["inner-radius"] = (double)(float)annulus.InnerRadius,
            ["outer-radius"] = (double)(float)annulus.OuterRadius,
            ["start-degrees"] = (double)(float)annulus.StartDegrees,
            ["sweep-degrees"] = (double)(float)annulus.SweepDegrees
        });

    private SequenceValue Rows()
        => new(BlobLabelsSchema.RowsType, Measurements.Select(measurement => (object?)Row(measurement)));

    private static RecordValue Row(BlobLabelsMeasurement measurement)
        => new(BlobLabelsSchema.ResultType, new Dictionary<string, object?>
        {
            ["element-id"] = measurement.ElementId,
            ["label"] = (long)measurement.Label,
            ["area"] = measurement.Area,
            ["perimeter"] = measurement.Perimeter,
            ["centroid-x"] = measurement.CentroidX,
            ["centroid-y"] = measurement.CentroidY,
            ["centroid"] = new PointGeometry(BlobLabelsSchema.ImageFrame, new(measurement.CentroidX, measurement.CentroidY)),
            ["circularity"] = measurement.Circularity,
            ["rectangularity"] = measurement.Rectangularity,
            ["bounds"] = BlobLabelsSchema.FreezeBounds(measurement.Bounds.X, measurement.Bounds.Y, measurement.Bounds.Width, measurement.Bounds.Height),
            ["oriented-bounds"] = BlobLabelsSchema.FreezeRectangle(measurement.OrientedBounds.CenterX, measurement.OrientedBounds.CenterY,
                measurement.OrientedBounds.Width, measurement.OrientedBounds.Height, measurement.OrientedBounds.AngleDegrees),
            ["long-axis"] = measurement.OrientedBounds.LongAxis,
            ["short-axis"] = measurement.OrientedBounds.ShortAxis,
            ["contour"] = new SequenceValue(TypeRef.SequenceOf(BlobLabelsSchema.PointType),
                measurement.Contour.Select(point => (object?)new PointGeometry(BlobLabelsSchema.ImageFrame, point))),
            ["region"] = measurement.Region,
            ["judgment-passed"] = measurement.Passed,
            ["failed-limits"] = new SequenceValue(TypeRef.SequenceOf(DataType.Text), measurement.FailedLimits.Select(text => (object?)text))
        });

    public IEnumerable<string> DescribeResultRecords()
    {
        yield return $"Blob个数：{Projection.Count}";
        yield return $"总面积：{Projection.TotalArea:0.###}";
        foreach (var message in LimitMessages) yield return message;
    }

    /// <summary>本次调用产生的输出资源由本对象独占；重复释放按幂等处理。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        var errors = new List<Exception>();
        foreach (var owned in _owned)
            try { owned.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) throw new AggregateException("BlobLabels 输出资源释放失败。", errors);
    }
}

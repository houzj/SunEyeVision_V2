using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.MarkFind.Runtime;

public sealed record MarkFindBox(double CenterX, double CenterY, double Width, double Height, double AngleDegrees);

/// <summary>一次成功调用产生的单条匹配结果；资源、判定与几何都属于同一次调用。</summary>
public sealed record MarkFindMeasurement(
    long ElementId,
    double CenterX,
    double CenterY,
    double AngleDegrees,
    double Score,
    MarkFindBox Box,
    IReadOnlyList<Point2> Outline,
    IReadOnlyRegion Region,
    bool Passed,
    IReadOnlyList<string> FailedLimits);

/// <summary>
/// 原件输出的中立投影。原件支持的缩放搜索在本框架未实现，缩放保持 <c>null</c> 并列入
/// <see cref="Unavailable"/>，结果页显示“不可用”而不是 0。
/// </summary>
public sealed record MarkFindResultProjection(
    int ModuleStatus,
    double ModuleRunTime,
    int Count,
    IReadOnlyList<float> Scores,
    IReadOnlyList<Point2> MatchPoints,
    IReadOnlyList<float> Angles,
    IReadOnlyList<MarkFindBox> Boxes,
    RecordValue? OutlinePoints,
    RecordValue? OutputFixture,
    IReadOnlySet<string> Unavailable)
{
    public static MarkFindResultProjection Create(IReadOnlyList<MarkFindMeasurement> measurements, bool modulePassed,
        double runTimeMilliseconds, RecordValue? fixture)
    {
        var unavailable = new HashSet<string>(StringComparer.Ordinal) { nameof(Scales) };
        var outline = Outline(measurements);
        if (outline is null) unavailable.Add(nameof(OutlinePoints));
        return new(
            ModuleStatus: modulePassed ? 1 : 0,
            ModuleRunTime: runTimeMilliseconds,
            Count: measurements.Count,
            Scores: Array.AsReadOnly(measurements.Select(item => (float)item.Score).ToArray()),
            MatchPoints: Array.AsReadOnly(measurements.Select(item => new Point2(item.CenterX, item.CenterY)).ToArray()),
            Angles: Array.AsReadOnly(measurements.Select(item => (float)item.AngleDegrees).ToArray()),
            Boxes: Array.AsReadOnly(measurements.Select(item => item.Box).ToArray()),
            OutlinePoints: outline,
            OutputFixture: fixture,
            Unavailable: unavailable);
    }

    /// <summary>缩放搜索未实现：该端口任何情况下都不可用，不补 1 或 0 伪装成功。</summary>
    public IReadOnlyList<float>? Scales => null;

    private static RecordValue? Outline(IReadOnlyList<MarkFindMeasurement> measurements)
    {
        var count = measurements.Sum(item => item.Outline.Count);
        if (count == 0) return null;
        var x = new double[count];
        var y = new double[count];
        var index = new long[count];
        var position = 0;
        for (var match = 0; match < measurements.Count; match++)
            foreach (var point in measurements[match].Outline)
            {
                x[position] = (float)point.X;
                y[position] = (float)point.Y;
                index[position++] = match;
            }
        return new(MarkFindSchema.OutlineSetType, new Dictionary<string, object?>
        {
            ["x"] = x, ["y"] = y, ["match-index"] = index, ["count"] = (long)count
        });
    }
}

/// <summary>
/// 标记查找的输出对象。调用成功即构造完整声明，输出资源所有权随本次调用的输出作用域释放。
/// </summary>
public sealed class MarkFindOutput : IToolOutput, IDisposable, IToolResultRecordOutput, IToolResultGeometryOutput
{
    private readonly IReadOnlyList<IDisposable> _owned;
    private readonly Dictionary<string, object?> _values;
    private int _disposed;

    public MarkFindOutput(MarkFindSettings settings, MarkFindResultProjection projection,
        IReadOnlyList<MarkFindMeasurement> measurements, IReadOnlyList<string> limitMessages,
        ResultGeometrySnapshot geometry, IReadOnlyList<IDisposable> owned)
    {
        Settings = settings;
        Projection = projection;
        Measurements = measurements;
        LimitMessages = limitMessages;
        ResultGeometry = geometry;
        _owned = owned.Distinct<IDisposable>(ReferenceEqualityComparer.Instance).ToArray();
        _values = BuildValues();
        _ = new InputValues(MarkFindSchema.Outputs, _values);
    }

    public MarkFindSettings Settings { get; }
    public MarkFindResultProjection Projection { get; }
    public IReadOnlyList<MarkFindMeasurement> Measurements { get; }
    public IReadOnlyList<string> LimitMessages { get; }
    public ResultGeometrySnapshot ResultGeometry { get; }
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
            [MarkFindSchema.RunTime] = (double)(float)projection.ModuleRunTime,
            [MarkFindSchema.Status] = (long)projection.ModuleStatus,
            [MarkFindSchema.Count] = (long)projection.Count,
            [MarkFindSchema.Scores] = Numbers(MarkFindSchema.Scores, projection.Scores),
            [MarkFindSchema.MatchPoints] = new SequenceValue(
                MarkFindSchema.Outputs.Single(port => port.Id == MarkFindSchema.MatchPoints).Type,
                projection.MatchPoints.Select(point => (object?)MarkFindSchema.Point(point.X, point.Y))),
            [MarkFindSchema.Angles] = Numbers(MarkFindSchema.Angles, projection.Angles),
            [MarkFindSchema.Scales] = null,
            [MarkFindSchema.Boxes] = new SequenceValue(
                MarkFindSchema.Outputs.Single(port => port.Id == MarkFindSchema.Boxes).Type,
                projection.Boxes.Select(Box)),
            [MarkFindSchema.OutlinePoints] = projection.OutlinePoints,
            [MarkFindSchema.OutputFixture] = projection.OutputFixture,
            [MarkFindSchema.Rows] = Rows()
        };
    }

    private SequenceValue Numbers(string portId, IReadOnlyList<float> values)
        => new(MarkFindSchema.Outputs.Single(port => port.Id == portId).Type, values.Select(value => (object?)(double)value));

    private static RecordValue Box(MarkFindBox box)
        => MarkFindSchema.FreezeBox(box.CenterX, box.CenterY, box.Width, box.Height, box.AngleDegrees);

    private SequenceValue Rows()
        => new(MarkFindSchema.RowsType, Measurements.Select(measurement => (object?)Row(measurement)));

    private static RecordValue Row(MarkFindMeasurement measurement)
        => new(MarkFindSchema.ResultType, new Dictionary<string, object?>
        {
            ["element-id"] = measurement.ElementId,
            ["match-point"] = MarkFindSchema.Point(measurement.CenterX, measurement.CenterY),
            ["center-x"] = measurement.CenterX,
            ["center-y"] = measurement.CenterY,
            ["angle-degrees"] = measurement.AngleDegrees,
            ["scale"] = null,
            ["score"] = measurement.Score,
            ["box"] = MarkFindSchema.FreezeBox(measurement.Box.CenterX, measurement.Box.CenterY,
                measurement.Box.Width, measurement.Box.Height, measurement.Box.AngleDegrees),
            ["region"] = measurement.Region,
            ["outline"] = new SequenceValue(TypeRef.SequenceOf(MarkFindSchema.PointType),
                measurement.Outline.Select(point => (object?)MarkFindSchema.Point(point.X, point.Y))),
            ["judgment-passed"] = measurement.Passed,
            ["failed-limits"] = new SequenceValue(TypeRef.SequenceOf(DataType.Text),
                measurement.FailedLimits.Select(text => (object?)text))
        });

    public IEnumerable<string> DescribeResultRecords()
    {
        yield return $"匹配个数：{Projection.Count}";
        if (Projection.Count > 0) yield return $"最高分数：{Projection.Scores.Max():0.###}";
        foreach (var message in LimitMessages) yield return message;
    }

    /// <summary>本次调用产生的输出资源由本对象独占；重复释放按幂等处理。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        var errors = new List<Exception>();
        foreach (var owned in _owned)
            try { owned.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) throw new AggregateException("MarkFind 输出资源释放失败。", errors);
    }
}

using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

public sealed record ContourMatchBox(double CenterX, double CenterY, double Width, double Height, double AngleDegrees);

/// <summary>一次成功调用产生的单条匹配结果；资源、判定与几何都属于同一次调用。</summary>
public sealed record ContourMatchMeasurement(
    long ElementId,
    double CenterX,
    double CenterY,
    double AngleDegrees,
    double Score,
    double Scale,
    ContourMatchBox Box,
    IReadOnlyList<Point2> Outline,
    IReadOnlyRegion Region,
    bool Passed,
    IReadOnlyList<string> FailedLimits);

/// <summary>原件输出的中立投影；尺度搜索已按 0x0307/0308 逻辑对照实现（创建宽范围模型 + 运行期筛选）。</summary>
public sealed record ContourMatchResultProjection(
    int ModuleStatus,
    double ModuleRunTime,
    int Count,
    IReadOnlyList<float> Scores,
    IReadOnlyList<Point2> MatchPoints,
    IReadOnlyList<float> Angles,
    IReadOnlyList<float> Scales,
    IReadOnlyList<ContourMatchBox> Boxes,
    RecordValue? OutlinePoints,
    RecordValue? OutputFixture,
    IReadOnlySet<string> Unavailable)
{
    public static ContourMatchResultProjection Create(IReadOnlyList<ContourMatchMeasurement> measurements, bool modulePassed,
        double runTimeMilliseconds, RecordValue? fixture)
    {
        var unavailable = new HashSet<string>(StringComparer.Ordinal);
        var outline = Outline(measurements);
        if (outline is null) unavailable.Add(nameof(OutlinePoints));
        return new(
            ModuleStatus: modulePassed ? 1 : 0,
            ModuleRunTime: runTimeMilliseconds,
            Count: measurements.Count,
            Scores: Array.AsReadOnly(measurements.Select(item => (float)item.Score).ToArray()),
            MatchPoints: Array.AsReadOnly(measurements.Select(item => new Point2(item.CenterX, item.CenterY)).ToArray()),
            Angles: Array.AsReadOnly(measurements.Select(item => (float)item.AngleDegrees).ToArray()),
            Scales: Array.AsReadOnly(measurements.Select(item => (float)item.Scale).ToArray()),
            Boxes: Array.AsReadOnly(measurements.Select(item => item.Box).ToArray()),
            OutlinePoints: outline,
            OutputFixture: fixture,
            Unavailable: unavailable);
    }

    private static RecordValue? Outline(IReadOnlyList<ContourMatchMeasurement> measurements)
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
        return new(ContourMatchSchema.OutlineSetType, new Dictionary<string, object?>
        {
            ["x"] = x, ["y"] = y, ["match-index"] = index, ["count"] = (long)count
        });
    }
}

/// <summary>
/// 轮廓匹配的输出对象。调用成功即构造完整声明，输出资源所有权随本次调用的输出作用域释放。
/// </summary>
public sealed class ContourMatchOutput : IToolOutput, IDisposable, IToolResultRecordOutput, IToolResultGeometryOutput
{
    private readonly IReadOnlyList<IDisposable> _owned;
    private readonly Dictionary<string, object?> _values;
    private int _disposed;

    public ContourMatchOutput(ContourMatchSettings settings, ContourMatchResultProjection projection,
        IReadOnlyList<ContourMatchMeasurement> measurements, IReadOnlyList<string> limitMessages,
        ResultGeometrySnapshot geometry, IReadOnlyList<IDisposable> owned)
    {
        Settings = settings;
        Projection = projection;
        Measurements = measurements;
        LimitMessages = limitMessages;
        ResultGeometry = geometry;
        _owned = owned.Distinct<IDisposable>(ReferenceEqualityComparer.Instance).ToArray();
        _values = BuildValues();
        _ = new InputValues(ContourMatchSchema.Outputs, _values);
    }

    public ContourMatchSettings Settings { get; }
    public ContourMatchResultProjection Projection { get; }
    public IReadOnlyList<ContourMatchMeasurement> Measurements { get; }
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
            [ContourMatchSchema.RunTime] = (double)(float)projection.ModuleRunTime,
            [ContourMatchSchema.Status] = (long)projection.ModuleStatus,
            [ContourMatchSchema.Count] = (long)projection.Count,
            [ContourMatchSchema.Scores] = Numbers(ContourMatchSchema.Scores, projection.Scores),
            [ContourMatchSchema.MatchPoints] = new SequenceValue(
                ContourMatchSchema.Outputs.Single(port => port.Id == ContourMatchSchema.MatchPoints).Type,
                projection.MatchPoints.Select(point => (object?)ContourMatchSchema.Point(point.X, point.Y))),
            [ContourMatchSchema.Angles] = Numbers(ContourMatchSchema.Angles, projection.Angles),
            [ContourMatchSchema.Scales] = Numbers(ContourMatchSchema.Scales, projection.Scales),
            [ContourMatchSchema.Boxes] = new SequenceValue(
                ContourMatchSchema.Outputs.Single(port => port.Id == ContourMatchSchema.Boxes).Type,
                projection.Boxes.Select(Box)),
            [ContourMatchSchema.OutlinePoints] = projection.OutlinePoints,
            [ContourMatchSchema.OutputFixture] = projection.OutputFixture,
            [ContourMatchSchema.Rows] = Rows()
        };
    }

    private SequenceValue Numbers(string portId, IReadOnlyList<float> values)
        => new(ContourMatchSchema.Outputs.Single(port => port.Id == portId).Type, values.Select(value => (object?)(double)value));

    private static RecordValue Box(ContourMatchBox box)
        => ContourMatchSchema.FreezeBox(box.CenterX, box.CenterY, box.Width, box.Height, box.AngleDegrees);

    private SequenceValue Rows()
        => new(ContourMatchSchema.RowsType, Measurements.Select(measurement => (object?)Row(measurement)));

    private static RecordValue Row(ContourMatchMeasurement measurement)
        => new(ContourMatchSchema.ResultType, new Dictionary<string, object?>
        {
            ["element-id"] = measurement.ElementId,
            ["match-point"] = ContourMatchSchema.Point(measurement.CenterX, measurement.CenterY),
            ["center-x"] = measurement.CenterX,
            ["center-y"] = measurement.CenterY,
            ["angle-degrees"] = measurement.AngleDegrees,
            ["scale"] = measurement.Scale,
            ["score"] = measurement.Score,
            ["box"] = ContourMatchSchema.FreezeBox(measurement.Box.CenterX, measurement.Box.CenterY,
                measurement.Box.Width, measurement.Box.Height, measurement.Box.AngleDegrees),
            ["region"] = measurement.Region,
            ["outline"] = new SequenceValue(TypeRef.SequenceOf(ContourMatchSchema.PointType),
                measurement.Outline.Select(point => (object?)ContourMatchSchema.Point(point.X, point.Y))),
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
        if (errors.Count != 0) throw new AggregateException("ContourMatch 输出资源释放失败。", errors);
    }
}

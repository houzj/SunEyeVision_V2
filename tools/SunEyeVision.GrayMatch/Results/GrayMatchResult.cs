using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.GrayMatch.Runtime;

public sealed record GrayMatchBox(double CenterX, double CenterY, double Width, double Height, double AngleDegrees);

/// <summary>一次成功调用产生的单条匹配结果；资源、判定与几何都属于同一次调用。</summary>
public sealed record GrayMatchMeasurement(
    long ElementId,
    double CenterX,
    double CenterY,
    double AngleDegrees,
    double Score,
    GrayMatchBox Box,
    IReadOnlyRegion Region,
    bool Passed,
    IReadOnlyList<string> FailedLimits);

/// <summary>
/// 原件输出的中立投影。原件 13 个输出中与本工具对位的是 `MatchNum/Status/Match Point/MatchRect/Score`；
/// 尺度与轮廓点原件本就没有，因此不含对应端口（不是「不可用」，而是不存在）。
/// </summary>
public sealed record GrayMatchResultProjection(
    int ModuleStatus,
    double ModuleRunTime,
    int Count,
    IReadOnlyList<float> Scores,
    IReadOnlyList<Point2> MatchPoints,
    IReadOnlyList<float> Angles,
    IReadOnlyList<GrayMatchBox> Boxes,
    RecordValue? OutputFixture)
{
    public static GrayMatchResultProjection Create(IReadOnlyList<GrayMatchMeasurement> measurements, bool modulePassed,
        double runTimeMilliseconds, RecordValue? fixture)
        => new(
            ModuleStatus: modulePassed ? 1 : 0,
            ModuleRunTime: runTimeMilliseconds,
            Count: measurements.Count,
            Scores: Array.AsReadOnly(measurements.Select(item => (float)item.Score).ToArray()),
            MatchPoints: Array.AsReadOnly(measurements.Select(item => new Point2(item.CenterX, item.CenterY)).ToArray()),
            Angles: Array.AsReadOnly(measurements.Select(item => (float)item.AngleDegrees).ToArray()),
            Boxes: Array.AsReadOnly(measurements.Select(item => item.Box).ToArray()),
            OutputFixture: fixture);
}

/// <summary>
/// 灰度匹配的输出对象。调用成功即构造完整声明，输出资源所有权随本次调用的输出作用域释放。
/// </summary>
public sealed class GrayMatchOutput : IToolOutput, IDisposable, IToolResultRecordOutput, IToolResultGeometryOutput
{
    private readonly IReadOnlyList<IDisposable> _owned;
    private readonly Dictionary<string, object?> _values;
    private int _disposed;

    public GrayMatchOutput(GrayMatchSettings settings, GrayMatchResultProjection projection,
        IReadOnlyList<GrayMatchMeasurement> measurements, IReadOnlyList<string> limitMessages,
        ResultGeometrySnapshot geometry, IReadOnlyList<IDisposable> owned)
    {
        Settings = settings;
        Projection = projection;
        Measurements = measurements;
        LimitMessages = limitMessages;
        ResultGeometry = geometry;
        _owned = owned.Distinct<IDisposable>(ReferenceEqualityComparer.Instance).ToArray();
        _values = BuildValues();
        _ = new InputValues(GrayMatchSchema.Outputs, _values);
    }

    public GrayMatchSettings Settings { get; }
    public GrayMatchResultProjection Projection { get; }
    public IReadOnlyList<GrayMatchMeasurement> Measurements { get; }
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
            [GrayMatchSchema.RunTime] = (double)(float)projection.ModuleRunTime,
            [GrayMatchSchema.Status] = (long)projection.ModuleStatus,
            [GrayMatchSchema.Count] = (long)projection.Count,
            [GrayMatchSchema.Scores] = Numbers(GrayMatchSchema.Scores, projection.Scores),
            [GrayMatchSchema.MatchPoints] = new SequenceValue(
                GrayMatchSchema.Outputs.Single(port => port.Id == GrayMatchSchema.MatchPoints).Type,
                projection.MatchPoints.Select(point => (object?)GrayMatchSchema.Point(point.X, point.Y))),
            [GrayMatchSchema.Angles] = Numbers(GrayMatchSchema.Angles, projection.Angles),
            [GrayMatchSchema.Boxes] = new SequenceValue(
                GrayMatchSchema.Outputs.Single(port => port.Id == GrayMatchSchema.Boxes).Type,
                projection.Boxes.Select(Box)),
            [GrayMatchSchema.OutputFixture] = projection.OutputFixture,
            [GrayMatchSchema.Rows] = Rows()
        };
    }

    private SequenceValue Numbers(string portId, IReadOnlyList<float> values)
        => new(GrayMatchSchema.Outputs.Single(port => port.Id == portId).Type, values.Select(value => (object?)(double)value));

    private static RecordValue Box(GrayMatchBox box)
        => GrayMatchSchema.FreezeBox(box.CenterX, box.CenterY, box.Width, box.Height, box.AngleDegrees);

    private SequenceValue Rows()
        => new(GrayMatchSchema.RowsType, Measurements.Select(measurement => (object?)Row(measurement)));

    private static RecordValue Row(GrayMatchMeasurement measurement)
        => new(GrayMatchSchema.ResultType, new Dictionary<string, object?>
        {
            ["element-id"] = measurement.ElementId,
            ["match-point"] = GrayMatchSchema.Point(measurement.CenterX, measurement.CenterY),
            ["center-x"] = measurement.CenterX,
            ["center-y"] = measurement.CenterY,
            ["angle-degrees"] = measurement.AngleDegrees,
            ["score"] = measurement.Score,
            ["box"] = GrayMatchSchema.FreezeBox(measurement.Box.CenterX, measurement.Box.CenterY,
                measurement.Box.Width, measurement.Box.Height, measurement.Box.AngleDegrees),
            ["region"] = measurement.Region,
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
        if (errors.Count != 0) throw new AggregateException("GrayMatch 输出资源释放失败。", errors);
    }
}

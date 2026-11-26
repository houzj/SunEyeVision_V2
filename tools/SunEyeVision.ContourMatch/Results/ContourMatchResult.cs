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
    IReadOnlyList<string> FailedLimits,
    int ModelIndex = 0,
    string ModelName = "",
    double ScaleX = 0,
    double ScaleY = 0);

/// <summary>原件输出的中立投影；尺度搜索已按 0x0307/0308 逻辑对照实现（创建宽范围模型 + 运行期筛选）。</summary>
public sealed record ContourMatchResultProjection(
    int ModuleStatus,
    double ModuleRunTime,
    int Count,
    IReadOnlyList<float> Scores,
    IReadOnlyList<Point2> MatchPoints,
    IReadOnlyList<float> Angles,
    IReadOnlyList<float> Scales,
    IReadOnlyList<float> ScaleX,
    IReadOnlyList<float> ScaleY,
    IReadOnlyList<float> SkewX,
    IReadOnlyList<long> ModelIndexes,
    IReadOnlyList<string> ModelNames,
    ContourMatchBox? DetectRoi,
    IReadOnlyList<ContourMatchBox> Boxes,
    RecordValue? OutlinePoints,
    RecordValue? OutputFixture,
    IReadOnlySet<string> Unavailable)
{
    public static ContourMatchResultProjection Create(IReadOnlyList<ContourMatchMeasurement> measurements, bool modulePassed,
        double runTimeMilliseconds, RecordValue? fixture, ContourMatchBox? detectRoi = null, string modelName = "")
    {
        var unavailable = new HashSet<string>(StringComparer.Ordinal);
        var outline = Outline(measurements);
        if (outline is null) unavailable.Add(nameof(OutlinePoints));
        var scales = measurements.Select(item => (float)item.Scale).ToArray();
        return new(
            ModuleStatus: modulePassed ? 1 : 0,
            ModuleRunTime: runTimeMilliseconds,
            Count: measurements.Count,
            Scores: Array.AsReadOnly(measurements.Select(item => (float)item.Score).ToArray()),
            MatchPoints: Array.AsReadOnly(measurements.Select(item => new Point2(item.CenterX, item.CenterY)).ToArray()),
            Angles: Array.AsReadOnly(measurements.Select(item => (float)item.AngleDegrees).ToArray()),
            Scales: Array.AsReadOnly(scales),
            // 原厂 MatchScaleX/MatchScaleY：各向异性模型取列/行真实尺度；等比模型两者相同。
            ScaleX: Array.AsReadOnly(measurements.Select(item => (float)(item.ScaleX != 0 ? item.ScaleX : item.Scale)).ToArray()),
            ScaleY: Array.AsReadOnly(measurements.Select(item => (float)(item.ScaleY != 0 ? item.ScaleY : item.Scale)).ToArray()),
            // 横切角度语义在 HALCON 形状模板中没有对应实现，恒为 0（与原厂字段存在性对齐，不造假数据）。
            SkewX: Array.AsReadOnly(new float[measurements.Count]),
            // 原厂 MatchModelIndex：多模板时为真实模板序号，单模板为 0。
            ModelIndexes: Array.AsReadOnly(measurements.Select(item => (long)item.ModelIndex).ToArray()),
            // 原厂 MatchModelName：逐匹配取所属模板名（单模板沿用训练样本名）。
            ModelNames: Array.AsReadOnly(measurements
                .Select(item => string.IsNullOrEmpty(item.ModelName) ? modelName : item.ModelName).ToArray()),
            DetectRoi: detectRoi,
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
        var score = new double[count];
        var index = new long[count];
        var position = 0;
        for (var match = 0; match < measurements.Count; match++)
            foreach (var point in measurements[match].Outline)
            {
                x[position] = (float)point.X;
                y[position] = (float)point.Y;
                // 原件 MatchOutlineScore 是逐点三态；本方 HALCON 形状模板只给匹配级判定，
                // 因此按该匹配的判定写 0/2，不伪造中间态。
                score[position] = measurements[match].Passed ? 0f : 2f;
                index[position++] = match;
            }
        return new(ContourMatchSchema.OutlineSetType, new Dictionary<string, object?>
        {
            ["count"] = (long)count,
            ["info"] = new RecordValue(ContourMatchSchema.OutlineInfoType, new Dictionary<string, object?>
            {
                ["x"] = x, ["y"] = y, ["score"] = score, ["match-index"] = index
            })
        });
    }
}

/// <summary>
/// 轮廓匹配的输出对象。调用成功即构造完整声明，输出资源所有权随本次调用的输出作用域释放。
/// </summary>
public sealed class ContourMatchOutput : IToolOutput, IDisposable, IToolResultRecordOutput, IToolResultGeometryOutput,
    IToolResultOutlineOutput
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
        ResultOutline = BuildOutline(measurements, geometry.SourceFrame);
        _owned = owned.Distinct<IDisposable>(ReferenceEqualityComparer.Instance).ToArray();
        _values = BuildValues();
        _ = new InputValues(ContourMatchSchema.Outputs, _values);
    }

    public ContourMatchSettings Settings { get; }
    public ContourMatchResultProjection Projection { get; }
    public IReadOnlyList<ContourMatchMeasurement> Measurements { get; }
    public IReadOnlyList<string> LimitMessages { get; }
    public ResultGeometrySnapshot ResultGeometry { get; }
    /// <summary>系统级轮廓点（原件 MatchOutlinePoints 的统一点集形态）。</summary>
    public ResultOutlineSet? ResultOutline { get; }
    public bool IsPass => Projection.ModuleStatus != 0;
    private static ResultOutlineSet? BuildOutline(IReadOnlyList<ContourMatchMeasurement> measurements,
        ImageFrameAddress frame)
    {
        var points = new List<Point2>();
        var indices = new List<int>();
        var scores = new List<float>();
        for (var match = 0; match < measurements.Count; match++)
            foreach (var point in measurements[match].Outline)
            {
                points.Add(point);
                indices.Add(match);
                scores.Add(measurements[match].Passed ? 0f : 2f);
            }
        return points.Count == 0 ? null : new ResultOutlineSet(frame, points, indices, scores);
    }

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
            [ContourMatchSchema.ModelIndex] = new SequenceValue(
                ContourMatchSchema.Outputs.Single(port => port.Id == ContourMatchSchema.ModelIndex).Type,
                projection.ModelIndexes.Select(value => (object?)value)),
            [ContourMatchSchema.ModelName] = new SequenceValue(
                ContourMatchSchema.Outputs.Single(port => port.Id == ContourMatchSchema.ModelName).Type,
                projection.ModelNames.Select(value => (object?)value)),
            [ContourMatchSchema.Scores] = Numbers(ContourMatchSchema.Scores, projection.Scores),
            [ContourMatchSchema.MatchPoints] = new SequenceValue(
                ContourMatchSchema.Outputs.Single(port => port.Id == ContourMatchSchema.MatchPoints).Type,
                projection.MatchPoints.Select(point => (object?)ContourMatchSchema.Point(point.X, point.Y))),
            [ContourMatchSchema.Angles] = Numbers(ContourMatchSchema.Angles, projection.Angles),
            [ContourMatchSchema.Scales] = Numbers(ContourMatchSchema.Scales, projection.Scales),
            [ContourMatchSchema.ScaleX] = Numbers(ContourMatchSchema.ScaleX, projection.ScaleX),
            [ContourMatchSchema.ScaleY] = Numbers(ContourMatchSchema.ScaleY, projection.ScaleY),
            [ContourMatchSchema.SkewX] = Numbers(ContourMatchSchema.SkewX, projection.SkewX),
            [ContourMatchSchema.DetectRoi] = projection.DetectRoi is { } detect
                ? ContourMatchSchema.FreezeBox(detect.CenterX, detect.CenterY, detect.Width, detect.Height, detect.AngleDegrees)
                : null,
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

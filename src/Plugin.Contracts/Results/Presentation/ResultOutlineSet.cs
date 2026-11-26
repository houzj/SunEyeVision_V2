namespace SunEyeVision.Plugin.Contracts.Results.Presentation;

/// <summary>
/// 系统级轮廓点值。对应原件 <c>outlinepoint</c> 显示对象 + <c>IMultiPoint.OutlinePoints</c>：
/// 一组像素点，每点带归属结果序号与逐点分数（0=OK、1=中间态、其余=NG）。
/// 所有工具共用这一种类型，工具层只负责产出数据，不再自定义打包字段名。
/// </summary>
public sealed class ResultOutlineSet
{
    public const int MaximumPoints = 1_000_000;

    public ResultOutlineSet(ImageFrameAddress sourceFrame, IReadOnlyList<Point2> points,
        IReadOnlyList<int> resultIndices, IReadOnlyList<float> scores)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(resultIndices);
        ArgumentNullException.ThrowIfNull(scores);
        if (points.Count != resultIndices.Count || points.Count != scores.Count)
            throw new ArgumentException("轮廓点的坐标、结果序号与分数数量必须一致。");
        if (points.Count > MaximumPoints)
            throw new ArgumentException("轮廓点数量超过系统预算。", nameof(points));
        for (var index = 0; index < points.Count; index++)
        {
            if (!double.IsFinite(points[index].X) || !double.IsFinite(points[index].Y))
                throw new ArgumentException("轮廓点坐标必须是有限值。", nameof(points));
            if (resultIndices[index] < 0)
                throw new ArgumentException("轮廓点结果序号不能为负。", nameof(resultIndices));
            if (!float.IsFinite(scores[index]))
                throw new ArgumentException("轮廓点分数必须是有限值。", nameof(scores));
        }
        SourceFrame = sourceFrame;
        Coordinates = RoiDeclarations.ImageFrame;
        Points = points;
        ResultIndices = resultIndices;
        Scores = scores;
    }

    public ImageFrameAddress SourceFrame { get; }
    public CoordinateFrame Coordinates { get; }
    public IReadOnlyList<Point2> Points { get; }
    /// <summary>每个点归属的结果序号（原件 blob-index / match-index 的统一形态）。</summary>
    public IReadOnlyList<int> ResultIndices { get; }
    /// <summary>逐点分数：0=OK 色、1=中间态黄色、其余=NG 色（原件 MatchOutlineScore 语义）。</summary>
    public IReadOnlyList<float> Scores { get; }
    public int Count => Points.Count;
}

/// <summary>
/// 工具定义侧的轮廓点出口声明（对应原件 IO 名映射 CONTOURPOINTS → outlinepoint）。
/// 平台据此在运行前请求该输出；无轮廓点输出的工具不实现本接口。
/// </summary>
public interface IToolResultOutlineDeclaration
{
    string OutlinePortId { get; }
}

/// <summary>工具输出侧的轮廓点数据（对应原件 outlinepoint 的 OutLineInfo chunk）。</summary>
public interface IToolResultOutlineOutput
{
    ResultOutlineSet? ResultOutline { get; }
}

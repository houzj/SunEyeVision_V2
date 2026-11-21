namespace SunEyeVision.Tool.DivideImage.Runtime;

/// <summary>一片分片在图像像素列/行坐标系中的位置（含首末端点）。</summary>
/// <param name="Index">自 0 起的顺序号（先横后竖）。</param>
/// <param name="Column">分片首列（含）。</param>
/// <param name="Row">分片首行（含）。</param>
/// <param name="Width">分片列跨度（像素个数）。</param>
/// <param name="Height">分片行跨度（像素个数）。</param>
public readonly record struct DivideImageTile(int Index, double Column, double Row, double Width, double Height);

/// <summary>
/// 划片几何：把给定图像尺寸切成 <paramref name="columns"/> × <paramref name="rows"/> 片，
/// 相邻分片在 X/Y 方向按重叠率互相覆盖。
/// </summary>
/// <remarks>
/// 已取证的部分：原件按 X/Y 方向划片数量与 X/Y 方向重叠率划片
/// （<c>decompiled\Lang\zh-cn.xaml:4568</c> 的 <c>IMVSDivideImageModu_HELP</c>）。
/// 原生 ProcessAlg 已直接以单精度浮点写出中心、宽、高。当前实现保持浮点几何，
/// 不把结果提前取整为像素索引。原厂像素采样或下游裁剪的取整行为仍未取证。
/// 本实现的取值规则：
/// <list type="bullet">
/// <item>基础片宽/高 = ROI 尺寸 / 方向数量；</item>
/// <item>单片尺寸 = min(ROI 尺寸, (ROI 尺寸 - 基础片尺寸) × 重叠率 + 基础片尺寸)；</item>
/// <item>步进 = 单片尺寸 - ROI 尺寸 × 重叠率，首片偏移为 0，其余按步进累加；</item>
/// <item>顺序为先横后竖（行优先）。</item>
/// </list>
/// </remarks>
public static class DivideImageTiling
{
    /// <summary>按图像宽高与划片参数计算全部分片；参数非法时抛 <see cref="ArgumentException"/>，不做静默修正。</summary>
    public static IReadOnlyList<DivideImageTile> Compute(int imageWidth, int imageHeight, int columns, int rows,
        int overlapXPercent, int overlapYPercent)
    {
        var horizontal = Split(imageWidth, columns, overlapXPercent, "X");
        var vertical = Split(imageHeight, rows, overlapYPercent, "Y");
        var tiles = new List<DivideImageTile>(checked(horizontal.Length * vertical.Length));
        var index = 0;
        for (var row = 0; row < vertical.Length; row++)
        for (var column = 0; column < horizontal.Length; column++)
            tiles.Add(new(index++, horizontal[column].Start, vertical[row].Start,
                horizontal[column].Size, vertical[row].Size));
        return tiles.AsReadOnly();
    }

    /// <summary>单轴分片跨度；<c>Start + Size</c> 恒不超过 <paramref name="extent"/>（浮点几何）。</summary>
    public static (double Start, double Size)[] Split(int extent, int count, int overlapPercent, string axis)
    {
        if (extent < 1) throw new ArgumentException($"图像{axis}方向尺寸必须≥1，实际 {extent}。");
        if (count < 1) throw new ArgumentException($"{axis}方向划片数量必须≥1，实际 {count}。");
        if (overlapPercent is < DivideImageFields.MinimumOverlapPercent or > DivideImageFields.MaximumOverlapPercent)
            throw new ArgumentException(
                $"{axis}方向重叠率必须在 {DivideImageFields.MinimumOverlapPercent}..{DivideImageFields.MaximumOverlapPercent} 之间（%），实际 {overlapPercent}。");
        if (count == 1) return new[] { (0d, (double)extent) };

        var ratio = overlapPercent / 100d;
        var baseSize = extent / (double)count;
        var size = Math.Min(extent, (extent - baseSize) * ratio + baseSize);
        var step = size - extent * ratio;
        var spans = new (double Start, double Size)[count];
        for (var index = 0; index < count; index++)
            spans[index] = (index == 0 ? 0d : step * index, size);
        return spans;
    }
}

namespace SunEyeVision.Tool.DivideImage.Runtime;

/// <summary>一片分片在图像像素列/行坐标系中的位置（含首末端点）。</summary>
/// <param name="Index">自 0 起的顺序号（先横后竖）。</param>
/// <param name="Column">分片首列（含）。</param>
/// <param name="Row">分片首行（含）。</param>
/// <param name="Width">分片列跨度（像素个数）。</param>
/// <param name="Height">分片行跨度（像素个数）。</param>
public readonly record struct DivideImageTile(int Index, int Column, int Row, int Width, int Height);

/// <summary>
/// 划片几何：把给定图像尺寸切成 <paramref name="columns"/> × <paramref name="rows"/> 片，
/// 相邻分片在 X/Y 方向按重叠率互相覆盖。
/// </summary>
/// <remarks>
/// 已取证的部分：原件按 X/Y 方向划片数量与 X/Y 方向重叠率划片
/// （<c>decompiled\Lang\zh-cn.xaml:4568</c> 的 <c>IMVSDivideImageModu_HELP</c>）。
/// 未取证的部分（见交接单）：重叠率单位按百分比的假设、除不尽时的取整与余量分配、分片顺序。
/// 本实现的取值规则：
/// <list type="bullet">
/// <item>分片边长 = ⌈边长 / (数量 − (数量−1) × 重叠率)⌉，重叠率 = 相邻分片重叠区占分片边长的比例；</item>
/// <item>向上取整保证不遗漏覆盖，除不尽的余量分散到分片间距里，末片贴住图像边界；</item>
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

    /// <summary>单轴分片跨度；<c>Start + Size</c> 恒不超过 <paramref name="extent"/>。</summary>
    public static (int Start, int Size)[] Split(int extent, int count, int overlapPercent, string axis)
    {
        if (extent < 1) throw new ArgumentException($"图像{axis}方向尺寸必须≥1，实际 {extent}。");
        if (count < 1) throw new ArgumentException($"{axis}方向划片数量必须≥1，实际 {count}。");
        if (overlapPercent is < DivideImageFields.MinimumOverlapPercent or > DivideImageFields.MaximumOverlapPercent)
            throw new ArgumentException(
                $"{axis}方向重叠率必须在 {DivideImageFields.MinimumOverlapPercent}..{DivideImageFields.MaximumOverlapPercent} 之间（%），实际 {overlapPercent}。");
        if (count == 1) return new[] { (0, extent) };

        // divisor ∈ (1, count]：重叠率 ≤99% 且数量 ≥2，因此 size 落在 [1, extent]，
        // 分片永远至少 1 像素且不越界，不存在需要静默修正的边界情形。
        var ratio = overlapPercent / 100d;
        var divisor = count - (count - 1) * ratio;
        var size = (int)Math.Ceiling(extent / divisor);

        var step = (extent - size) / (double)(count - 1);
        var spans = new (int Start, int Size)[count];
        for (var index = 0; index < count - 1; index++)
            spans[index] = ((int)Math.Round(index * step, MidpointRounding.AwayFromZero), size);
        spans[count - 1] = (extent - size, size);
        return spans;
    }
}

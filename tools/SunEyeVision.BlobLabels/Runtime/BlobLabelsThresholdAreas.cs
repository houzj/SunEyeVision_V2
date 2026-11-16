namespace SunEyeVision.Tool.BlobLabels.Runtime;

/// <summary>
/// 原厂 <c>ThresholdArea</c> 用户字符串的列表视图。
/// 原件每行编码 <c>序号#新增#灰度#下限#上限#反选</c>，界面条数上限 255（见
/// <c>docs/08-工具预览/ui/Location-IMVSBlobFindLabelsModu.html</c>）。
/// 本工具把多行按换行拼接保存在同一设置字段中，行顺序即界面顺序。
/// </summary>
public static class BlobLabelsThresholdAreas
{
    public const int MaximumCount = 255;
    public const int MinimumGray = 1;
    public const int MaximumGray = 255;
    public const int MinimumArea = 1;
    public const int MaximumArea = 999_999_999;

    public static IReadOnlyList<BlobLabelsThresholdArea> Parse(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return Array.Empty<BlobLabelsThresholdArea>();
        var lines = encoded.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > MaximumCount) throw new ArgumentException($"阈值区域最多 {MaximumCount} 条。");
        var items = new List<BlobLabelsThresholdArea>(lines.Length);
        foreach (var line in lines)
        {
            var item = BlobLabelsThresholdArea.Decode(line);
            if (items.Any(existing => existing.GrayValue == item.GrayValue))
                throw new ArgumentException($"灰度值 {item.GrayValue} 重复，原件同一灰度只允许一条阈值区域。");
            items.Add(new(items.Count + 1, item.IsAdd, item.GrayValue, item.MinimumArea, item.MaximumArea, item.IsInverse));
        }
        return items.AsReadOnly();
    }

    public static string Encode(IEnumerable<BlobLabelsThresholdArea> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var list = items.ToArray();
        if (list.Length > MaximumCount) throw new ArgumentException($"阈值区域最多 {MaximumCount} 条。");
        return string.Join('\n', list.Select((item, index) => item with { Index = index + 1 }).Select(item =>
        {
            if (item.GrayValue is < MinimumGray or > MaximumGray) throw new ArgumentException("灰度值必须在 1 到 255 之间。");
            if (item.MinimumArea < MinimumArea || item.MaximumArea < MinimumArea || item.MinimumArea > item.MaximumArea)
                throw new ArgumentException("阈值区域面积范围无效。");
            return item.Encode();
        }));
    }

    /// <summary>新建一条：序号续排，灰度取未占用值，面积沿用原厂面板默认 10..999999999。</summary>
    public static BlobLabelsThresholdArea Append(IReadOnlyList<BlobLabelsThresholdArea> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        if (existing.Count >= MaximumCount) throw new ArgumentException($"阈值区域最多 {MaximumCount} 条。");
        var used = existing.Select(item => item.GrayValue).ToHashSet();
        var gray = Enumerable.Range(MinimumGray, MaximumGray).First(candidate => !used.Contains(candidate));
        return new(existing.Count + 1, true, gray, 10, MaximumArea, false);
    }
}

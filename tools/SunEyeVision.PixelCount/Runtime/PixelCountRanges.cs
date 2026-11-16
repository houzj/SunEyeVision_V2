namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>一个阈值区间（闭区间，灰度 0..255 整数）。</summary>
public sealed record PixelCountRange(int Minimum, int Maximum);

/// <summary>
/// 原厂 <c>PixelModuRangeConfigList</c> 的区间列表视图（反编译证据 <c>PixelModuRangeConfigList.cs:23-33</c>：
/// 参数 <c>Threshold_nums</c> 记录条数、<c>min_Threshold_i</c>/<c>max_Threshold_i</c> 逐条存上下限，上限 32 条；
/// 新增项默认 <c>MinValue=0 / MaxValue=255 / IncValue=1</c>，见同文件 <c>:122-150</c>）。
/// 框架的字段声明是类型级静态声明，无法承载原厂的动态增删行，故把整张列表编码成一个文本字段，
/// 行间用分号或换行分隔，行内写作 <c>下限-上限</c>；解析严格，任何非法项直接报错，不做兜底。
/// </summary>
public static class PixelCountRanges
{
    /// <summary>原厂 <c>MaxNum = 32</c>（<c>PixelModuRangeConfigList.cs:31</c>）。</summary>
    public const int MaximumCount = 32;
    /// <summary>原厂每个 Integer 参数的 MinValue/MaxValue（<c>PixelModuRangeConfigList.cs:136-148</c>）。</summary>
    public const int MinimumGray = 0;
    public const int MaximumGray = 255;
    /// <summary>原厂新增一条区间的默认值（<c>PixelModuRangeConfigList.cs:126-128</c>）。</summary>
    public static PixelCountRange Default => new(MinimumGray, MaximumGray);
    public const string DefaultEncoded = "0-255";

    public static IReadOnlyList<PixelCountRange> Parse(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            throw new ArgumentException("阈值区间不能为空，至少需要一条区间。");
        var lines = encoded.Split([';', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > MaximumCount)
            throw new ArgumentException($"阈值区间最多 {MaximumCount} 条。");
        var ranges = new List<PixelCountRange>(lines.Length);
        foreach (var line in lines)
        {
            var separator = line.IndexOf('-', 1);
            if (separator < 0)
                throw new ArgumentException($"阈值区间“{line}”缺少“下限-上限”格式。");
            var lowerText = line[..separator].Trim();
            var upperText = line[(separator + 1)..].Trim();
            if (!int.TryParse(lowerText, out var lower) || !int.TryParse(upperText, out var upper))
                throw new ArgumentException($"阈值区间“{line}”的下限与上限必须是整数。");
            if (lower is < MinimumGray or > MaximumGray || upper is < MinimumGray or > MaximumGray)
                throw new ArgumentException($"阈值区间“{line}”必须落在 {MinimumGray} 到 {MaximumGray} 之内。");
            if (lower > upper)
                throw new ArgumentException($"阈值区间“{line}”的下限不能大于上限。");
            ranges.Add(new(lower, upper));
        }
        return ranges.AsReadOnly();
    }

    public static string Format(IEnumerable<PixelCountRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var items = ranges.ToArray();
        if (items.Length is 0 or > MaximumCount)
            throw new ArgumentException($"阈值区间必须是 1 到 {MaximumCount} 条。");
        return string.Join(';', items.Select(range =>
        {
            if (range.Minimum is < MinimumGray or > MaximumGray || range.Maximum is < MinimumGray or > MaximumGray)
                throw new ArgumentException("阈值区间必须落在 0 到 255 之内。");
            if (range.Minimum > range.Maximum) throw new ArgumentException("阈值区间的下限不能大于上限。");
            return $"{range.Minimum}-{range.Maximum}";
        }));
    }
}

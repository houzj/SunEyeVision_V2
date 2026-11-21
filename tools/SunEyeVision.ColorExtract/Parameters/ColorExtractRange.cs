using System.Globalization;

namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>原厂 RangeInfo.ColorTypes 取值：1=RGB、2=HSV、3=HSI（IMVSColorExtractModuControl/RangeInfo.cs 第 51-79 行）。</summary>
public enum ColorExtractColorSpace
{
    Rgb = 1,
    Hsv = 2,
    Hsi = 3
}

/// <summary>
/// 原厂 <c>ColorExtractList.Value</c> 的一项（RangeInfo.ToString 的 9 个整数）。
/// 顺序固定为 C1High、C1Low、C2High、C2Low、C3High、C3Low、色彩空间、反色、启用。
/// </summary>
public readonly record struct ColorExtractRange(
    int C1High, int C1Low, int C2High, int C2Low, int C3High, int C3Low,
    ColorExtractColorSpace ColorSpace, bool IsColorInversion, bool IsChecked)
{
    public int HighOf(int channel) => channel switch
    {
        1 => C1High,
        2 => C2High,
        3 => C3High,
        _ => throw new ArgumentOutOfRangeException(nameof(channel))
    };

    public int LowOf(int channel) => channel switch
    {
        1 => C1Low,
        2 => C2Low,
        3 => C3Low,
        _ => throw new ArgumentOutOfRangeException(nameof(channel))
    };
}

/// <summary>
/// 原厂颜色区间集合的持久化文本格式（'#' 分隔多项、',' 分隔 9 个字段）。
/// 解析失败一律抛异常，不跳过、不补默认值（rule-015）。
/// </summary>
public static class ColorExtractRanges
{
    /// <summary>原厂 ColorExtractList.MAX_NUMBER=16；AddCommandExcute 超限弹窗拒绝添加。</summary>
    public const int MaximumCount = 16;
    /// <summary>原厂 AlgorithmTab.xml 三个通道滑块的 Minimum=0、Maximum=255。</summary>
    public const int MaximumChannelValue = 255;
    public const char RangeSeparator = '#';
    public const char FieldSeparator = ',';
    public const int FieldCount = 9;
    /// <summary>原厂 IMVSColorExtractModuAlgorithm.xml InputColorRange DefaultValue。</summary>
    public const string DefaultSerialized = "255,0,255,0,255,0,1,0,1";
    public static ColorExtractRange DefaultRange { get; } =
        new(255, 0, 255, 0, 255, 0, ColorExtractColorSpace.Rgb, false, true);

    /// <summary>原厂 Serialize(string) 对空串直接返回，保留构造时的单个默认区间。</summary>
    public static IReadOnlyList<ColorExtractRange> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text)) return Array.AsReadOnly(new[] { DefaultRange });
        var items = text.Split(RangeSeparator, StringSplitOptions.None);
        if (items.Length > MaximumCount)
            throw new ArgumentException($"颜色区间最多 {MaximumCount} 项，实际 {items.Length} 项。");
        var ranges = new ColorExtractRange[items.Length];
        for (var index = 0; index < items.Length; index++) ranges[index] = ParseRange(items[index], index);
        return Array.AsReadOnly(ranges);
    }

    /// <summary>与 RangeInfo.ToString 完全一致的字段顺序，供设置回读与持久化往返。</summary>
    public static string Format(IEnumerable<ColorExtractRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        return string.Join(RangeSeparator, ranges.Select(range => string.Join(FieldSeparator, new[]
        {
            Text(range.C1High), Text(range.C1Low), Text(range.C2High), Text(range.C2Low),
            Text(range.C3High), Text(range.C3Low), Text((int)range.ColorSpace),
            range.IsColorInversion ? "1" : "0", range.IsChecked ? "1" : "0"
        })));
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static ColorExtractRange ParseRange(string item, int index)
    {
        var cells = item.Split(FieldSeparator, StringSplitOptions.None);
        if (cells.Length != FieldCount)
            throw new ArgumentException(
                $"第 {index + 1} 个颜色区间应为 {FieldCount} 个整数，实际 {cells.Length} 个：\"{item}\"。");
        var numbers = new int[FieldCount];
        for (var cell = 0; cell < FieldCount; cell++)
            if (!int.TryParse(cells[cell], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[cell]))
                throw new ArgumentException($"第 {index + 1} 个颜色区间第 {cell + 1} 项不是整数：\"{cells[cell]}\"。");
        // 原厂 Serialize 只接受 "0"/"1" 作为反色与启用标志。
        var inversion = Flag(numbers[7], index, "反色");
        var checkedState = Flag(numbers[8], index, "启用");
        if (numbers[6] is not (1 or 2 or 3))
            throw new ArgumentException($"第 {index + 1} 个颜色区间的色彩空间必须是 1(RGB)、2(HSV) 或 3(HSI)，实际 {numbers[6]}。");
        var range = new ColorExtractRange(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5],
            (ColorExtractColorSpace)numbers[6], inversion, checkedState);
        Validate(range, index);
        return range;
    }

    private static bool Flag(int value, int index, string name) => value switch
    {
        0 => false,
        1 => true,
        _ => throw new ArgumentException($"第 {index + 1} 个颜色区间的{name}标志必须是 0 或 1，实际 {value}。")
    };

    /// <summary>单区间校验：六个通道值必须在 0..255，且下限不得大于上限。</summary>
    public static void Validate(ColorExtractRange range, int? index = null)
    {
        if (!Enum.IsDefined(range.ColorSpace))
            throw new ArgumentException($"{Prefix(index)}色彩空间取值无效。");
        for (var channel = 1; channel <= 3; channel++)
        {
            var high = range.HighOf(channel);
            var low = range.LowOf(channel);
            if (low is < 0 or > MaximumChannelValue || high is < 0 or > MaximumChannelValue)
                throw new ArgumentException(
                    $"{Prefix(index)}第 {channel} 通道上下限必须在 0..{MaximumChannelValue}，实际 {low}..{high}。");
            // 原件对 H 通道跨 0 环带只在取色预览里做 +255 处理；执行期语义未取证，
            // 因此上下限颠倒一律显式报错，不猜测环绕语义（rule-015）。
            if (low > high)
                throw new ArgumentException($"{Prefix(index)}第 {channel} 通道下限 {low} 大于上限 {high}。");
        }
    }

    private static string Prefix(int? index) => index is { } value ? $"第 {value + 1} 个颜色区间：" : "颜色区间：";
}

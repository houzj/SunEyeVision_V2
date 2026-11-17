using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// 像素统计的唯一持久化设置：阈值区间列表 + 原厂「结果显示」页的数量/比率判定。
/// 原厂默认值证据：<c>IMVSPixelCountModuVAAlgorithm.xml</c> 的 ParamItem
/// （CountLimitEnable=False、CountLimitLow=0、CountLimitHigh=999999999、
/// RatioLimitEnable=False、RatioLimitLow=0、RatioLimitHigh=1）。
/// </summary>
public sealed record PixelCountSettings : IToolSettings<PixelCountSettings>
{
    /// <summary>阈值区间文本，编码规则与校验见 <see cref="PixelCountRanges"/>；默认一条整幅灰度区间。</summary>
    public string ThresholdRanges { get; init; } = PixelCountRanges.DefaultEncoded;
    /// <summary>原厂 CountLimitEnable（数量判断）。</summary>
    public bool CountLimitEnabled { get; init; }
    public long CountLimitLow { get; init; }
    public long CountLimitHigh { get; init; } = PixelCountFields.MaximumCountLimit;
    /// <summary>原厂 RatioLimitEnable（比率判断）。</summary>
    public bool RatioLimitEnabled { get; init; }
    public double RatioLimitLow { get; init; }
    public double RatioLimitHigh { get; init; } = PixelCountFields.MaximumRatioLimit;

    public PixelCountSettings Freeze() => this;

    /// <summary>解析并返回阈值区间；非法文本直接报错，不返回空集合。</summary>
    public IReadOnlyList<PixelCountRange> ReadRanges() => PixelCountRanges.Parse(ThresholdRanges);

    /// <summary>只校验设置本身：默认设置（一条 0-255 区间、两个判定都关闭）必须通过准入。</summary>
    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        try { _ = ReadRanges(); }
        catch (ArgumentException error)
        {
            errors.Add(new("PIXELCOUNT_THRESHOLD", error.Message, PixelCountFields.ThresholdRanges));
        }
        if (CountLimitEnabled || CountLimitLow != 0 || CountLimitHigh != PixelCountFields.MaximumCountLimit)
        {
            if (CountLimitLow < 0 || CountLimitHigh > PixelCountFields.MaximumCountLimit)
                errors.Add(new("PIXELCOUNT_COUNT_LIMIT",
                    $"数量范围必须落在 0 到 {PixelCountFields.MaximumCountLimit} 之内。", PixelCountFields.CountLimitLow));
            else if (CountLimitLow > CountLimitHigh)
                errors.Add(new("PIXELCOUNT_COUNT_LIMIT", "数量范围的下限不能大于上限。", PixelCountFields.CountLimitLow));
        }
        if (RatioLimitEnabled || RatioLimitLow != 0 || RatioLimitHigh != PixelCountFields.MaximumRatioLimit)
        {
            if (!double.IsFinite(RatioLimitLow) || !double.IsFinite(RatioLimitHigh)
                || RatioLimitLow < 0 || RatioLimitHigh > PixelCountFields.MaximumRatioLimit)
                errors.Add(new("PIXELCOUNT_RATIO_LIMIT", "比率范围必须落在 0 到 1 之内。", PixelCountFields.RatioLimitLow));
            else if (RatioLimitLow > RatioLimitHigh)
                errors.Add(new("PIXELCOUNT_RATIO_LIMIT", "比率范围的下限不能大于上限。", PixelCountFields.RatioLimitLow));
        }
        return errors.AsReadOnly();
    }
}

using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// 像素统计的唯一持久化设置：固定槽位的阈值区间 + 原厂「结果显示」页的数量/比率判定。
/// 原厂 <c>PixelModuRangeConfigList</c> 用 <c>Threshold_nums</c> 记录有效条数，
/// 每条 <c>min_Threshold_i</c>/<c>max_Threshold_i</c> 是独立参数；本方按同样语义保留 32 组端点槽位，
/// 只有数量以内的槽位参与执行和显示。
/// </summary>
public sealed record PixelCountSettings : IToolSettings<PixelCountSettings>
{
    public long RoiAreaType { get; init; }
    public bool OutputMaskEnabled { get; init; }
    public bool PositionCorrectionEnabled { get; init; }

    /// <summary>原厂 <c>Threshold_nums</c>，1..32。</summary>
    public long RangeCount { get; init; } = 1;

    /// <summary>按序号保存的 32 组阈值区间；只有前 <see cref="RangeCount"/> 组生效。</summary>
    public IReadOnlyList<PixelCountRange> Ranges { get; init; } = PixelCountRanges.CreateSlots(1);

    /// <summary>原厂 CountLimitEnable（数量判断）。</summary>
    public bool CountLimitEnabled { get; init; }
    public long CountLimitLow { get; init; }
    public long CountLimitHigh { get; init; } = PixelCountFields.MaximumCountLimit;
    /// <summary>原厂 RatioLimitEnable（比率判断）。</summary>
    public bool RatioLimitEnabled { get; init; }
    public double RatioLimitLow { get; init; }
    public double RatioLimitHigh { get; init; } = PixelCountFields.MaximumRatioLimit;

    public PixelCountSettings Freeze() => this;

    /// <summary>返回当前生效的阈值区间（前 RangeCount 组）。</summary>
    public IReadOnlyList<PixelCountRange> ReadRanges()
        => Ranges.Take((int)Math.Clamp(RangeCount, 1, PixelCountRanges.MaximumCount)).ToArray();

    /// <summary>只校验设置本身：默认设置（一条 0-255 区间、两个判定都关闭）必须通过准入。</summary>
    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        if (RangeCount < 1 || RangeCount > PixelCountRanges.MaximumCount)
            errors.Add(new("PIXELCOUNT_THRESHOLD_COUNT",
                $"阈值区间必须是 1 到 {PixelCountRanges.MaximumCount} 条。", PixelCountFields.RangeCount));
        else if (Ranges.Count != PixelCountRanges.MaximumCount)
            errors.Add(new("PIXELCOUNT_THRESHOLD_COUNT", "阈值区间槽位数量与声明不一致。", PixelCountFields.RangeCount));
        else
            for (var i = 0; i < RangeCount; i++)
            {
                var range = Ranges[i];
                if (range.Minimum < PixelCountRanges.MinimumGray || range.Maximum > PixelCountRanges.MaximumGray
                    || range.Minimum > range.Maximum)
                {
                    errors.Add(new("PIXELCOUNT_THRESHOLD",
                        $"第 {i} 条阈值区间必须落在 {PixelCountRanges.MinimumGray} 到 {PixelCountRanges.MaximumGray} 之内，且下限不能大于上限。",
                        PixelCountFields.RangeMinimum(i)));
                    break;
                }
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

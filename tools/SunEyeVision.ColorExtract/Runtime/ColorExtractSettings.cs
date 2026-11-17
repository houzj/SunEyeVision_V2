using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>唯一的持久化设置来源；一次调用只使用一份冻结值。</summary>
public sealed record ColorExtractSettings : IToolSettings<ColorExtractSettings>
{
    /// <summary>原厂 InputColorRange（ColorExtractList 序列化文本）。</summary>
    public string ColorRange { get; init; } = ColorExtractRanges.DefaultSerialized;
    /// <summary>原厂 AreaLimitEnable，DefaultValue=False。</summary>
    public bool AreaLimitEnabled { get; init; }
    /// <summary>原厂 RangeAreaLimit 下限，对应 AreaLimitLow，DefaultValue=1。</summary>
    public long AreaLimitLow { get; init; } = 1;
    /// <summary>原厂 RangeAreaLimit 上限，对应 AreaLimitHigh，DefaultValue=9999999。</summary>
    public long AreaLimitHigh { get; init; } = ColorExtractFields.MaximumAreaLimit;

    public ColorExtractSettings Freeze() => this;

    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        try
        {
            // 区间数量（1..16）、每项字段数与取值范围都在解析阶段一次校验，失败即报错。
            _ = ColorExtractRanges.Parse(ColorRange);
        }
        catch (ArgumentException error)
        {
            errors.Add(new("COLOR_EXTRACT_RANGE_INVALID", error.Message, ColorExtractFields.ColorRange));
            return errors.AsReadOnly();
        }
        if (AreaLimitEnabled)
        {
            if (AreaLimitLow < ColorExtractFields.MinimumAreaLimit || AreaLimitLow > ColorExtractFields.MaximumAreaLimit)
                errors.Add(new("COLOR_EXTRACT_AREA_LIMIT",
                    $"面积下限必须在 {ColorExtractFields.MinimumAreaLimit}..{ColorExtractFields.MaximumAreaLimit}，实际 {AreaLimitLow}。",
                    ColorExtractFields.AreaLimitLow));
            if (AreaLimitHigh < AreaLimitLow || AreaLimitHigh > ColorExtractFields.MaximumAreaLimit)
                errors.Add(new("COLOR_EXTRACT_AREA_LIMIT",
                    $"面积上限必须在 {AreaLimitLow}..{ColorExtractFields.MaximumAreaLimit}，实际 {AreaLimitHigh}。",
                    ColorExtractFields.AreaLimitHigh));
        }
        if (errors.Count != 0) return errors.AsReadOnly();
        // 控件状态不在第一个区间就报错：原厂允许全部区间不勾选，此时执行期显式失败而不是静默输出空结果。
        return Array.Empty<Diagnostic>();
    }

    /// <summary>解析后的区间表；调用方必须已通过 Validate。</summary>
    public IReadOnlyList<ColorExtractRange> Ranges() => ColorExtractRanges.Parse(ColorRange);
}

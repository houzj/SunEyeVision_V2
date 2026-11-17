using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 亮度测量的唯一持久化设置：五类判据的启用开关与上下限。
/// 默认值照抄原件 ParamItem（docs/07-工具逆向/tools/imvsintensitymeasuremodu/parameters.json:12-278）：
/// 五类判据全部为 False，上下限分别为 0 与 255。
/// </summary>
public sealed record IntensityMeasureSettings : IToolSettings<IntensityMeasureSettings>
{
    /// <summary>原件 <c>MaxValueLimitEnable</c>。</summary>
    public bool MaxValueLimitEnabled { get; init; }
    public long MaxValueLimitLow { get; init; }
    public long MaxValueLimitHigh { get; init; } = IntensityMeasureFields.MaximumGray;
    /// <summary>原件 <c>MinValueLimitEnable</c>。</summary>
    public bool MinValueLimitEnabled { get; init; }
    public long MinValueLimitLow { get; init; }
    public long MinValueLimitHigh { get; init; } = IntensityMeasureFields.MaximumGray;
    /// <summary>原件 <c>MeanLimitEnable</c>。</summary>
    public bool MeanLimitEnabled { get; init; }
    public double MeanLimitLow { get; init; }
    public double MeanLimitHigh { get; init; } = IntensityMeasureFields.MaximumGray;
    /// <summary>原件 <c>StdLimitEnable</c>。</summary>
    public bool StdLimitEnabled { get; init; }
    public double StdLimitLow { get; init; }
    public double StdLimitHigh { get; init; } = IntensityMeasureFields.MaximumGray;
    /// <summary>原件 <c>ContrastLimitEnable</c>。</summary>
    public bool ContrastLimitEnabled { get; init; }
    public double ContrastLimitLow { get; init; }
    public double ContrastLimitHigh { get; init; } = IntensityMeasureFields.MaximumGray;

    public IntensityMeasureSettings Freeze() => this;

    /// <summary>
    /// 只校验设置本身：限值必须落在 8 位灰度量程内且下限不大于上限。
    /// 判据关闭时也校验限值，避免把非法数值持久化后再被启用。
    /// </summary>
    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Integer(string id, string label, long low, long high)
        {
            if (low < IntensityMeasureFields.MinimumGray || low > IntensityMeasureFields.MaximumGray
                || high < IntensityMeasureFields.MinimumGray || high > IntensityMeasureFields.MaximumGray)
                errors.Add(new("INTENSITYMEASURE_LIMIT_RANGE",
                    $"{label}范围必须落在 0 到 255 之内。", id));
            else if (low > high)
                errors.Add(new("INTENSITYMEASURE_LIMIT_RANGE", $"{label}范围的下限不能大于上限。", id));
        }
        void Number(string id, string label, double low, double high)
        {
            if (!double.IsFinite(low) || !double.IsFinite(high)
                || low < IntensityMeasureFields.MinimumGray || low > IntensityMeasureFields.MaximumGray
                || high < IntensityMeasureFields.MinimumGray || high > IntensityMeasureFields.MaximumGray)
                errors.Add(new("INTENSITYMEASURE_LIMIT_RANGE",
                    $"{label}范围必须落在 0 到 255 之内。", id));
            else if (low > high)
                errors.Add(new("INTENSITYMEASURE_LIMIT_RANGE", $"{label}范围的下限不能大于上限。", id));
        }
        Integer(IntensityMeasureFields.MaxValueLimitLow, "最大值", MaxValueLimitLow, MaxValueLimitHigh);
        Integer(IntensityMeasureFields.MinValueLimitLow, "最小值", MinValueLimitLow, MinValueLimitHigh);
        Number(IntensityMeasureFields.MeanLimitLow, "均值", MeanLimitLow, MeanLimitHigh);
        Number(IntensityMeasureFields.StdLimitLow, "标准差", StdLimitLow, StdLimitHigh);
        Number(IntensityMeasureFields.ContrastLimitLow, "直方图对比度", ContrastLimitLow, ContrastLimitHigh);
        return errors.AsReadOnly();
    }
}

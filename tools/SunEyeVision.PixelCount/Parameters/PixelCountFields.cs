using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// PixelCount 的公共字段门面。声明、页签、分组和条件来自 PixelCountParameters.xml；
/// 阈值区间使用 32 组固定声明槽位（原厂 IDynamicChild 的静态等价实现），
/// 有效条数由 <c>range.count</c> 控制，端点字段身份为 <c>range.{i}.min/max</c>。
/// </summary>
public static class PixelCountFields
{
    public const string RoiAreaType = "roi.area-type";
    public const string OutputMaskEnabled = "roi.output-mask";
    public const string PositionCorrectionEnabled = "roi.position-correction";
    public const string RangeCount = "range.count";
    public const string CountLimitEnabled = "limit.count.enabled";
    public const string CountLimitLow = "limit.count.low";
    public const string CountLimitHigh = "limit.count.high";
    public const string RatioLimitEnabled = "limit.ratio.enabled";
    public const string RatioLimitLow = "limit.ratio.low";
    public const string RatioLimitHigh = "limit.ratio.high";

    public const long MaximumCountLimit = 999_999_999;
    public const double MaximumRatioLimit = 1d;
    public const double RatioIncrement = 0.01d;

    /// <summary>原厂 <c>min_Threshold_i</c> 的字段身份。</summary>
    public static string RangeMinimum(int index) => $"range.{index}.min";
    /// <summary>原厂 <c>max_Threshold_i</c> 的字段身份。</summary>
    public static string RangeMaximum(int index) => $"range.{index}.max";

    public static ToolSchema Schema => PixelCountParameterDefinition.Instance.SettingsSchema;

    public static InputValues Read(PixelCountSettings settings)
        => PixelCountParameterDefinition.Instance.Read(settings);

    public static PixelCountSettings Materialize(InputValues values)
        => PixelCountParameterDefinition.Instance.Materialize(values);
}

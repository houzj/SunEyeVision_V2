using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// 参数页字段身份与布局。原厂三页签（<c>IMVSPixelCountModuVAAlgorithmTab.xml</c>）中，
/// 图像/ROI/掩膜属「基本参数」的输入组（<c>ImageSourceGroup</c>、<c>RoiSelectGroup</c>、<c>InputMaskImageGroup</c>），
/// 在本方由输入端口承载；「运行参数」页是 <c>PixelModuRangeConfigList</c>（阈值区间列表）；
/// 「结果显示」页是 <c>CountLimitEnable/RangCountLimit/RatioLimitEnable/RangRatioLimit</c>（数量与比率判定）。
/// </summary>
public static class PixelCountFields
{
    public const string ThresholdRanges = "range.thresholds";
    public const string CountLimitEnabled = "limit.count.enabled";
    public const string CountLimitLow = "limit.count.low";
    public const string CountLimitHigh = "limit.count.high";
    public const string RatioLimitEnabled = "limit.ratio.enabled";
    public const string RatioLimitLow = "limit.ratio.low";
    public const string RatioLimitHigh = "limit.ratio.high";

    /// <summary>原厂 IntegerBettween 的 MinValue/MaxValue（<c>IMVSPixelCountModuVAAlgorithmTab.xml:86-111</c>）。</summary>
    public const long MaximumCountLimit = 999_999_999;
    /// <summary>原厂 FloatBettween 的 MinValue/MaxValue（<c>IMVSPixelCountModuVAAlgorithmTab.xml:144-169</c>）。</summary>
    public const double MaximumRatioLimit = 1d;
    public const double RatioIncrement = 0.01d;

    private const string RunPage = "运行参数";
    private const string ResultPage = "结果显示";
    private const string ThresholdSection = "阈值范围";
    private const string LimitSection = "结果判断";

    private static readonly TypeRef Boolean = TypeRef.ScalarOf(ScalarKind.Boolean);
    private static readonly TypeRef Integer = TypeRef.ScalarOf(ScalarKind.Int64);
    private static readonly TypeRef Number = TypeRef.ScalarOf(ScalarKind.Float64);
    private static readonly TypeRef Text = TypeRef.ScalarOf(ScalarKind.Text);

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(ThresholdRanges, Text, EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(CountLimitEnabled, Boolean, EditModes: FieldEditModes.Constant),
        new Port(CountLimitLow, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(CountLimitHigh, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(RatioLimitEnabled, Boolean, EditModes: FieldEditModes.Constant),
        new Port(RatioLimitLow, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(RatioLimitHigh, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant)
    }, Array.Empty<Port>(), new ToolPresentation(new[]
    {
        new FieldPresentation(ThresholdRanges, "阈值区间", ThresholdSection, 0, FieldEditorKind.Scalar)
        {
            // 原厂同一控件的多条区间在框架里合并成一个文本字段：条数与顺序由文本本身给出。
            Layout = new(RunPage, ThresholdSection, AllowReference: false)
        },
        new FieldPresentation(CountLimitEnabled, "数量判断", LimitSection, 10, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, AllowReference: false, BooleanTips: new("已启用", "未启用"))
        },
        new FieldPresentation(CountLimitLow, "数量范围下限", LimitSection, 11, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, new(CountLimitEnabled, true),
                PairWith: CountLimitHigh, Minimum: 0, Maximum: MaximumCountLimit, DecimalPlaces: 0,
                Increment: 1, PairMinimum: 0, PairMaximum: MaximumCountLimit, PairDisplayName: "数量范围")
        },
        new FieldPresentation(CountLimitHigh, "数量范围上限", LimitSection, 12, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, new(CountLimitEnabled, true),
                Minimum: 0, Maximum: MaximumCountLimit, DecimalPlaces: 0, Increment: 1)
        },
        new FieldPresentation(RatioLimitEnabled, "比率判断", LimitSection, 20, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, AllowReference: false, BooleanTips: new("已启用", "未启用"))
        },
        new FieldPresentation(RatioLimitLow, "比率范围下限", LimitSection, 21, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, new(RatioLimitEnabled, true),
                PairWith: RatioLimitHigh, Minimum: 0, Maximum: MaximumRatioLimit, DecimalPlaces: 2,
                Increment: RatioIncrement, PairMinimum: 0, PairMaximum: MaximumRatioLimit, PairDisplayName: "比率范围")
        },
        new FieldPresentation(RatioLimitHigh, "比率范围上限", LimitSection, 22, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, new(RatioLimitEnabled, true),
                Minimum: 0, Maximum: MaximumRatioLimit, DecimalPlaces: 2, Increment: RatioIncrement)
        }
    }));

    public static InputValues Read(PixelCountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(Schema.Inputs, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ThresholdRanges] = settings.ThresholdRanges,
            [CountLimitEnabled] = settings.CountLimitEnabled,
            [CountLimitLow] = settings.CountLimitLow,
            [CountLimitHigh] = settings.CountLimitHigh,
            [RatioLimitEnabled] = settings.RatioLimitEnabled,
            [RatioLimitLow] = settings.RatioLimitLow,
            [RatioLimitHigh] = settings.RatioLimitHigh
        });
    }

    public static PixelCountSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var candidate = new InputValues(Schema.Inputs, values.CopyValues());
        var result = new PixelCountSettings
        {
            ThresholdRanges = candidate.Get<string>(ThresholdRanges),
            CountLimitEnabled = candidate.Get<bool>(CountLimitEnabled),
            CountLimitLow = ToInteger(candidate.Get<object?>(CountLimitLow), CountLimitLow),
            CountLimitHigh = ToInteger(candidate.Get<object?>(CountLimitHigh), CountLimitHigh),
            RatioLimitEnabled = candidate.Get<bool>(RatioLimitEnabled),
            RatioLimitLow = ToNumber(candidate.Get<object?>(RatioLimitLow), RatioLimitLow),
            RatioLimitHigh = ToNumber(candidate.Get<object?>(RatioLimitHigh), RatioLimitHigh)
        };
        var errors = result.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return result.Freeze();
    }

    /// <summary>整数参数按声明归一；非整数来源直接报错，不做兜底转换。</summary>
    private static long ToInteger(object? value, string field) => value switch
    {
        long integer => integer,
        int integer => integer,
        _ => throw new ArgumentException($"字段 {field} 必须是整数，实际为 {value?.GetType().Name ?? "空值"}。")
    };

    private static double ToNumber(object? value, string field) => value switch
    {
        double number when double.IsFinite(number) => number,
        float single when float.IsFinite(single) => single,
        long integer => integer,
        int integer => integer,
        _ => throw new ArgumentException($"字段 {field} 必须是有限数值，实际为 {value?.GetType().Name ?? "空值"}。")
    };
}

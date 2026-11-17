using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 亮度测量的参数页字段身份与布局。
/// 原件 15 个 <c>ParamItem</c> 逐条对应（五类判据 × Enable/LimitLow/LimitHigh）：
/// 证据 docs/07-工具逆向/tools/imvsintensitymeasuremodu/parameters.json:12-278
/// （<c>StdLimitHigh</c>=255、<c>MeanLimitLow</c>=0、<c>MaxValueLimitHigh</c>=255、<c>StdLimitEnable</c>=False、
/// <c>ContrastLimitEnable</c>=False、<c>ContrastLimitLow</c>=0、<c>ContrastLimitHigh</c>=255）。
/// 原件工具栏只有两个页签（<c>Tab_Basic Params</c>、<c>ResultShow</c>，见 resources.json 的 uiTabs），
/// 判据归入「结果显示」页的「结果判断」分区；判据落在哪个页签未取到逐控件绑定证据，结论标未确认。
/// </summary>
public static class IntensityMeasureFields
{
    public const string MaxValueLimitEnabled = "limit.max-value.enabled";
    public const string MaxValueLimitLow = "limit.max-value.low";
    public const string MaxValueLimitHigh = "limit.max-value.high";
    public const string MinValueLimitEnabled = "limit.min-value.enabled";
    public const string MinValueLimitLow = "limit.min-value.low";
    public const string MinValueLimitHigh = "limit.min-value.high";
    public const string MeanLimitEnabled = "limit.mean.enabled";
    public const string MeanLimitLow = "limit.mean.low";
    public const string MeanLimitHigh = "limit.mean.high";
    public const string StdLimitEnabled = "limit.std.enabled";
    public const string StdLimitLow = "limit.std.low";
    public const string StdLimitHigh = "limit.std.high";
    public const string ContrastLimitEnabled = "limit.contrast.enabled";
    public const string ContrastLimitLow = "limit.contrast.low";
    public const string ContrastLimitHigh = "limit.contrast.high";

    /// <summary>原件 <c>IntegerBettween</c>/<c>FloatBettween</c> 的 MinValue/MaxValue：灰度 8 位量化范围 0..255。</summary>
    public const long MinimumGray = 0;
    public const long MaximumGray = 255;
    public const double NumberIncrement = 0.01d;

    /// <summary>原件第二个页签 <c>ResultShow</c>：判据参数与显示项所在页。</summary>
    public const string ResultPage = "结果显示";
    /// <summary>原件 <c>ResultShow</c> 页的「结果判断」分类。</summary>
    public const string LimitSection = "结果判断";

    private static readonly TypeRef Boolean = TypeRef.ScalarOf(ScalarKind.Boolean);
    private static readonly TypeRef Integer = TypeRef.ScalarOf(ScalarKind.Int64);
    private static readonly TypeRef Number = TypeRef.ScalarOf(ScalarKind.Float64);

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(MaxValueLimitEnabled, Boolean, EditModes: FieldEditModes.Constant),
        new Port(MaxValueLimitLow, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(MaxValueLimitHigh, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(MinValueLimitEnabled, Boolean, EditModes: FieldEditModes.Constant),
        new Port(MinValueLimitLow, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(MinValueLimitHigh, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(MeanLimitEnabled, Boolean, EditModes: FieldEditModes.Constant),
        new Port(MeanLimitLow, Number, EditModes: FieldEditModes.Constant),
        new Port(MeanLimitHigh, Number, EditModes: FieldEditModes.Constant),
        new Port(StdLimitEnabled, Boolean, EditModes: FieldEditModes.Constant),
        new Port(StdLimitLow, Number, EditModes: FieldEditModes.Constant),
        new Port(StdLimitHigh, Number, EditModes: FieldEditModes.Constant),
        new Port(ContrastLimitEnabled, Boolean, EditModes: FieldEditModes.Constant),
        new Port(ContrastLimitLow, Number, EditModes: FieldEditModes.Constant),
        new Port(ContrastLimitHigh, Number, EditModes: FieldEditModes.Constant)
    }, Array.Empty<Port>(), new ToolPresentation(new[]
    {
        BooleanField(MaxValueLimitEnabled, "最大值判断", 10),
        IntegerField(MaxValueLimitLow, "最大值范围下限", 11, MaxValueLimitHigh, "最大值范围"),
        IntegerField(MaxValueLimitHigh, "最大值范围上限", 12, null, null),
        BooleanField(MinValueLimitEnabled, "最小值判断", 20),
        IntegerField(MinValueLimitLow, "最小值范围下限", 21, MinValueLimitHigh, "最小值范围"),
        IntegerField(MinValueLimitHigh, "最小值范围上限", 22, null, null),
        BooleanField(MeanLimitEnabled, "均值判断", 30),
        NumberField(MeanLimitLow, "均值范围下限", 31, MeanLimitHigh, "均值范围"),
        NumberField(MeanLimitHigh, "均值范围上限", 32, null, null),
        BooleanField(StdLimitEnabled, "标准差判断", 40),
        NumberField(StdLimitLow, "标准差范围下限", 41, StdLimitHigh, "标准差范围"),
        NumberField(StdLimitHigh, "标准差范围上限", 42, null, null),
        BooleanField(ContrastLimitEnabled, "直方图对比度判断", 50),
        NumberField(ContrastLimitLow, "对比度范围下限", 51, ContrastLimitHigh, "对比度范围"),
        NumberField(ContrastLimitHigh, "对比度范围上限", 52, null, null)
    }));

    public static InputValues Read(IntensityMeasureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(Schema.Inputs, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MaxValueLimitEnabled] = settings.MaxValueLimitEnabled,
            [MaxValueLimitLow] = settings.MaxValueLimitLow,
            [MaxValueLimitHigh] = settings.MaxValueLimitHigh,
            [MinValueLimitEnabled] = settings.MinValueLimitEnabled,
            [MinValueLimitLow] = settings.MinValueLimitLow,
            [MinValueLimitHigh] = settings.MinValueLimitHigh,
            [MeanLimitEnabled] = settings.MeanLimitEnabled,
            [MeanLimitLow] = settings.MeanLimitLow,
            [MeanLimitHigh] = settings.MeanLimitHigh,
            [StdLimitEnabled] = settings.StdLimitEnabled,
            [StdLimitLow] = settings.StdLimitLow,
            [StdLimitHigh] = settings.StdLimitHigh,
            [ContrastLimitEnabled] = settings.ContrastLimitEnabled,
            [ContrastLimitLow] = settings.ContrastLimitLow,
            [ContrastLimitHigh] = settings.ContrastLimitHigh
        });
    }

    public static IntensityMeasureSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var candidate = new InputValues(Schema.Inputs, values.CopyValues());
        var result = new IntensityMeasureSettings
        {
            MaxValueLimitEnabled = candidate.Get<bool>(MaxValueLimitEnabled),
            MaxValueLimitLow = ToInteger(candidate.Get<object?>(MaxValueLimitLow), MaxValueLimitLow),
            MaxValueLimitHigh = ToInteger(candidate.Get<object?>(MaxValueLimitHigh), MaxValueLimitHigh),
            MinValueLimitEnabled = candidate.Get<bool>(MinValueLimitEnabled),
            MinValueLimitLow = ToInteger(candidate.Get<object?>(MinValueLimitLow), MinValueLimitLow),
            MinValueLimitHigh = ToInteger(candidate.Get<object?>(MinValueLimitHigh), MinValueLimitHigh),
            MeanLimitEnabled = candidate.Get<bool>(MeanLimitEnabled),
            MeanLimitLow = ToNumber(candidate.Get<object?>(MeanLimitLow), MeanLimitLow),
            MeanLimitHigh = ToNumber(candidate.Get<object?>(MeanLimitHigh), MeanLimitHigh),
            StdLimitEnabled = candidate.Get<bool>(StdLimitEnabled),
            StdLimitLow = ToNumber(candidate.Get<object?>(StdLimitLow), StdLimitLow),
            StdLimitHigh = ToNumber(candidate.Get<object?>(StdLimitHigh), StdLimitHigh),
            ContrastLimitEnabled = candidate.Get<bool>(ContrastLimitEnabled),
            ContrastLimitLow = ToNumber(candidate.Get<object?>(ContrastLimitLow), ContrastLimitLow),
            ContrastLimitHigh = ToNumber(candidate.Get<object?>(ContrastLimitHigh), ContrastLimitHigh)
        };
        var errors = result.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return result.Freeze();
    }

    private static FieldPresentation BooleanField(string id, string label, int order)
        => new(id, label, LimitSection, order, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, AllowReference: false, BooleanTips: new("已启用", "未启用"))
        };

    private static FieldPresentation IntegerField(string id, string label, int order, string? pairWith, string? pairLabel)
        => new(id, label, LimitSection, order, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, EnabledGate(id), PairWith: pairWith,
                Minimum: MinimumGray, Maximum: MaximumGray, DecimalPlaces: 0, Increment: 1,
                PairMinimum: pairWith is null ? null : MinimumGray,
                PairMaximum: pairWith is null ? null : MaximumGray,
                PairDisplayName: pairLabel, AllowReference: false)
        };

    private static FieldPresentation NumberField(string id, string label, int order, string? pairWith, string? pairLabel)
        => new(id, label, LimitSection, order, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, EnabledGate(id), PairWith: pairWith,
                Minimum: MinimumGray, Maximum: MaximumGray, DecimalPlaces: 2, Increment: NumberIncrement,
                PairMinimum: pairWith is null ? null : MinimumGray,
                PairMaximum: pairWith is null ? null : MaximumGray,
                PairDisplayName: pairLabel, AllowReference: false)
        };

    /// <summary>判据范围只在对应判据启用时可编辑；判据开关本身始终可用。</summary>
    private static FieldCondition EnabledGate(string limitFieldId) => limitFieldId switch
    {
        MaxValueLimitLow or MaxValueLimitHigh => new(MaxValueLimitEnabled, true),
        MinValueLimitLow or MinValueLimitHigh => new(MinValueLimitEnabled, true),
        MeanLimitLow or MeanLimitHigh => new(MeanLimitEnabled, true),
        StdLimitLow or StdLimitHigh => new(StdLimitEnabled, true),
        ContrastLimitLow or ContrastLimitHigh => new(ContrastLimitEnabled, true),
        _ => throw new ArgumentException("判据范围字段没有对应的启用开关：" + limitFieldId, nameof(limitFieldId))
    };

    /// <summary>整数判据按声明归一；非整数来源直接报错，不做兜底转换。</summary>
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

using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>稳定设置身份只在声明与绑定中使用，不依赖 CLR 反射。</summary>
public static class ColorExtractFields
{
    /// <summary>原厂 InputColorRange：ColorExtractList 序列化文本（'#' 分隔区间，',' 分隔 9 项）。</summary>
    public const string ColorRange = "proc.color-range";
    /// <summary>原厂 AreaLimitEnable。</summary>
    public const string AreaLimitEnabled = "proc.area-limit-enabled";
    /// <summary>原厂 RangeAreaLimit 下限（AreaLimitLow）。</summary>
    public const string AreaLimitLow = "proc.area-limit-low";
    /// <summary>原厂 RangeAreaLimit 上限（AreaLimitHigh）。</summary>
    public const string AreaLimitHigh = "proc.area-limit-high";

    /// <summary>
    /// 面积下界 0：原厂 <c>IMVSColorExtractModuAlgorithmTab.xml</c> 声明
    /// <c>AreaLimitLow/AreaLimitHigh</c> 范围均为 0~9999999（验收 D2 证据）。
    /// </summary>
    public const int MinimumAreaLimit = 0;
    /// <summary>原厂 AreaLimitHigh DefaultValue=9999999。</summary>
    public const int MaximumAreaLimit = 9999999;

    // 原厂 AlgorithmTab 控件归属：预览页把 7 个控件放在 Tab_Basic Params，
    // Tab_Run Params / ResultShow 归属未取证，因此本工具只声明一页「基本参数」。
    // 原厂 AlgorithmTab 页签：Tab_Basic Params / Tab_Run Params / ResultShow。
    public const string BasicPage = "基本参数";
    public const string RunPage = "运行参数";
    public const string ResultPage = "结果显示";
    public const string RangeSection = "区间列表";
    public const string LimitSection = "结果判断";

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(ColorRange, TypeRef.ScalarOf(ScalarKind.Text), EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(AreaLimitEnabled, TypeRef.ScalarOf(ScalarKind.Boolean), EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(AreaLimitLow, DataType.Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault),
        new Port(AreaLimitHigh, DataType.Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant,
            DefaultPresence: FieldDefaultPresence.UseDeclaredDefault)
    }, Array.Empty<Port>(), new ToolPresentation(new[]
    {
        new FieldPresentation(ColorRange, "颜色区间", RangeSection, 0, FieldEditorKind.Scalar)
        {
            Layout = new(RunPage, RangeSection, AllowReference: false, DecimalPlaces: 0)
        },
        new FieldPresentation(AreaLimitEnabled, "颜色面积判断", LimitSection, 1, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, AllowReference: false)
        },
        new FieldPresentation(AreaLimitLow, "面积下限", LimitSection, 2, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, VisibleWhen: new(AreaLimitEnabled, true), PairWith: AreaLimitHigh,
                Minimum: MinimumAreaLimit, Maximum: MaximumAreaLimit, DecimalPlaces: 0, AllowReference: false,
                PairMinimum: MinimumAreaLimit, PairMaximum: MaximumAreaLimit, PairDisplayName: "面积上限")
        },
        new FieldPresentation(AreaLimitHigh, "面积上限", LimitSection, 3, FieldEditorKind.Scalar)
        {
            Layout = new(ResultPage, LimitSection, VisibleWhen: new(AreaLimitEnabled, true),
                Minimum: MinimumAreaLimit, Maximum: MaximumAreaLimit, DecimalPlaces: 0, AllowReference: false)
        }
    }));

    public static InputValues Read(ColorExtractSettings settings) => new(Schema.Inputs,
        new Dictionary<string, object?>
        {
            [ColorRange] = settings.ColorRange,
            [AreaLimitEnabled] = settings.AreaLimitEnabled,
            [AreaLimitLow] = settings.AreaLimitLow,
            [AreaLimitHigh] = settings.AreaLimitHigh
        });

    public static ColorExtractSettings Apply(ColorExtractSettings original, IReadOnlyDictionary<string, object?> changes)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(changes);
        var values = Read(original).CopyValues().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var pair in changes)
        {
            if (!Schema.Inputs.Any(port => port.Id == pair.Key)) throw new ArgumentException("未声明的色彩提取字段：" + pair.Key);
            values[pair.Key] = pair.Value;
        }
        return Materialize(new InputValues(Schema.Inputs, values));
    }

    public static ColorExtractSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var candidate = new InputValues(Schema.Inputs, values.CopyValues());
        var result = new ColorExtractSettings
        {
            ColorRange = candidate.Get<string>(ColorRange),
            AreaLimitEnabled = candidate.Get<bool>(AreaLimitEnabled),
            AreaLimitLow = candidate.Get<long>(AreaLimitLow),
            AreaLimitHigh = candidate.Get<long>(AreaLimitHigh)
        };
        var errors = result.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return result.Freeze();
    }
}

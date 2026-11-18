using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.PointPointMeasure.Runtime;

/// <summary>
/// 点点测量的参数页字段、默认值与布局。14 个 ParamItem 及默认值照抄
/// docs/07-工具逆向/tools/imvsp2pmeasuremodu/parameters.json:3-269（逐项见本文件定义注释）。
/// 原厂三页签 Tab_Basic Params / Tab_Run Params / ResultShow（reference-codex Measurement-IMVSP2PMeasureModu 页 uiTabs）；
/// 两点输入由输入端口承载，运行参数页无持久字段，判据归结果显示页「结果判断」分区（页签归属为推断，缺逐控件绑定）。
/// </summary>
public static class PointPointMeasureFields
{
    public const string BasicPage = "基本参数";
    public const string RunPage = "运行参数";
    public const string ResultPage = "结果显示";
    public const string MeasureSection = "测量设置";
    public const string LimitSection = "结果判断";

    public const double AngleMinimum = -180d;
    public const double AngleMaximum = 180d;
    public const double DistanceMaximum = 99999d;

    private static readonly TypeRef Boolean = TypeRef.ScalarOf(ScalarKind.Boolean);
    private static readonly TypeRef Integer = TypeRef.ScalarOf(ScalarKind.Int64);
    private static readonly TypeRef Number = TypeRef.ScalarOf(ScalarKind.Float64);

    private sealed record Definition(string Id, string Name, object Default, TypeRef Type,
        string Page, string Section, int Order, string? Gate = null, string? Pair = null, bool Angle = false);

    private static readonly Definition[] Definitions =
    {
        new("settings.choose-coordinate", "坐标系", 1L, Integer, BasicPage, MeasureSection, 0),          // ChooseCoordinate=0x1
        new("settings.angle-output-range", "角度输出范围", 1L, Integer, BasicPage, MeasureSection, 1),    // OutputAngleRange=1

        new("settings.angle-limit-enabled", "角度判断", false, Boolean, ResultPage, LimitSection, 10),
        new("settings.angle-limit-low", "角度下限", -180d, Number, ResultPage, LimitSection, 11,
            "settings.angle-limit-enabled", "settings.angle-limit-high", true),
        new("settings.angle-limit-high", "角度上限", 180d, Number, ResultPage, LimitSection, 12,
            "settings.angle-limit-enabled", null, true),
        new("settings.dist-limit-enabled", "距离判断", false, Boolean, ResultPage, LimitSection, 20),
        new("settings.dist-limit-low", "距离下限", 0d, Number, ResultPage, LimitSection, 21,
            "settings.dist-limit-enabled", "settings.dist-limit-high"),
        new("settings.dist-limit-high", "距离上限", 99999d, Number, ResultPage, LimitSection, 22,
            "settings.dist-limit-enabled"),
        new("settings.mid-x-limit-enabled", "中点X判断", false, Boolean, ResultPage, LimitSection, 30),
        new("settings.mid-x-limit-low", "中点X下限", 0d, Number, ResultPage, LimitSection, 31,
            "settings.mid-x-limit-enabled", "settings.mid-x-limit-high"),
        new("settings.mid-x-limit-high", "中点X上限", 99999d, Number, ResultPage, LimitSection, 32,
            "settings.mid-x-limit-enabled"),
        new("settings.mid-y-limit-enabled", "中点Y判断", false, Boolean, ResultPage, LimitSection, 40),
        new("settings.mid-y-limit-low", "中点Y下限", 0d, Number, ResultPage, LimitSection, 41,
            "settings.mid-y-limit-enabled", "settings.mid-y-limit-high"),
        new("settings.mid-y-limit-high", "中点Y上限", 99999d, Number, ResultPage, LimitSection, 42,
            "settings.mid-y-limit-enabled")
    };

    public static ToolSchema Schema { get; } = new(
        Definitions.Select(def => new Port(def.Id, def.Type,
            Storage: def.Type == Integer ? NumericStorage.Int32 : null, EditModes: FieldEditModes.Constant)).ToArray(),
        Array.Empty<Port>(),
        new ToolPresentation(Definitions.Select(def =>
        {
            var layout = new FieldLayout(def.Page, def.Section,
                def.Gate is null ? null : new FieldCondition(def.Gate, true),
                PairWith: def.Pair,
                Minimum: def.Angle ? AngleMinimum : 0d,
                Maximum: def.Angle ? AngleMaximum : DistanceMaximum,
                DecimalPlaces: def.Type == Number ? 2 : 0,
                Increment: def.Type == Number ? 0.01d : 1,
                PairMinimum: def.Pair is null ? null : def.Angle ? AngleMinimum : 0d,
                PairMaximum: def.Pair is null ? null : def.Angle ? AngleMaximum : DistanceMaximum,
                PairDisplayName: def.Pair is null ? null : def.Name.Replace("下限", "") + "范围",
                AllowReference: false,
                BooleanTips: def.Type == Boolean ? new("已启用", "未启用") : null);
            return new FieldPresentation(def.Id, def.Name, def.Section, def.Order, FieldEditorKind.Scalar)
            { Layout = layout };
        }).ToArray()));

    public static IReadOnlyDictionary<string, object?> Defaults { get; } =
        Definitions.ToDictionary(def => def.Id, def => (object?)def.Default, StringComparer.Ordinal);

    public static InputValues Read(PointPointMeasureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new InputValues(Schema.Inputs, new Dictionary<string, object?>(Defaults, StringComparer.Ordinal));
    }

    public static PointPointMeasureSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var settings = new PointPointMeasureSettings(values.CopyValues());
        var errors = settings.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return settings.Freeze();
    }
}

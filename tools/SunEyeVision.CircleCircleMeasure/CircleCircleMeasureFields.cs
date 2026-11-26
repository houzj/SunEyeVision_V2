using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.CircleCircleMeasure.Runtime;

/// <summary>
/// 圆圆测量的参数页字段身份、默认值与布局。全部 46 个 <c>ParamItem</c> 及其默认值照抄
/// docs/07-工具逆向/tools/imvsc2cmeasuremodu/parameters.json（逐项见本文件 <see cref="Definitions"/> 注释）。
/// 原厂三页签为 <c>Tab_Basic Params</c> / <c>Tab_Run Params</c> / <c>ResultShow</c>
/// （reference-codex Measurement-IMVSC2CMeasureModu 页 uiTabs，identityCheck=original-file-sha256-rechecked）；
/// 逐控件绑定证据缺失，页签/分区归属按页名语义归置并标为推断，不冒充已证实。
/// </summary>
public static class CircleCircleMeasureFields
{
    public const string BasicPage = "基本参数";
    public const string RunPage = "运行参数";
    public const string ResultPage = "结果显示";
    public const string MeasureSection = "测量设置";
    public const string Circle1Section = "圆1边缘与拟合";
    public const string Circle2Section = "圆2边缘与拟合";
    public const string LimitSection = "结果判断";

    /// <summary>原厂 FloatBettween 的上下限（parameters.json Inter*/Angle*/Dist* 的 LimitLow/LimitHigh）。</summary>
    public const double MinimumLimit = -180d;
    public const double MaximumLimit = 99999d;
    public const double AngleMinimum = -180d;
    public const double AngleMaximum = 180d;

    private static readonly TypeRef Boolean = TypeRef.ScalarOf(ScalarKind.Boolean);
    private static readonly TypeRef Integer = TypeRef.ScalarOf(ScalarKind.Int64);
    private static readonly TypeRef Number = TypeRef.ScalarOf(ScalarKind.Float64);

    private sealed record Definition(string Id, string Name, object Default, TypeRef Type,
        string Page, string Section, int Order, string? GateGroup = null);

    // 每项末尾注释为 parameters.json 中的 ParamItem Name=DefaultValue。
    private static readonly Definition[] Definitions =
    {
        new("settings.choose-coordinate", "坐标系", 1L, Integer, BasicPage, MeasureSection, 0),      // ChooseCoordinate=0x1
        new("settings.source-select", "来源选择", 1L, Integer, BasicPage, MeasureSection, 1),        // SourceSelect=1
        new("settings.circle-choose", "圆选择", 1L, Integer, BasicPage, MeasureSection, 2),          // CircleChoose=1
        new("settings.angle-output-range", "角度输出范围", 1L, Integer, BasicPage, MeasureSection, 3),// OutputAngleRange=1

        // 圆1：边缘搜索、极性、阈值、拟合与剔除（parameters.json 中后缀 1 的 12 项）。
        new("settings.find-mode-1", "圆1查找模式", 1L, Integer, RunPage, Circle1Section, 10),        // FindMode1=1
        new("settings.edge-polarity-1", "圆1边缘极性", 3L, Integer, RunPage, Circle1Section, 11),    // EdgePolarity1=3
        new("settings.edge-threshold-1", "圆1边缘阈值", 15L, Integer, RunPage, Circle1Section, 12),  // EdgeThresh1=15
        new("settings.edge-width-1", "圆1边缘宽度", 1L, Integer, RunPage, Circle1Section, 13),       // EdgeWidth1=1
        new("settings.reject-num-1", "圆1剔除点数", 0L, Integer, RunPage, Circle1Section, 14),       // RejectNum1=0
        new("settings.coarse-detect-1", "圆1粗定位", false, Boolean, RunPage, Circle1Section, 15),   // CoarseDetectFlag1=False
        new("settings.sample-scale-1", "圆1采样倍率", 8L, Integer, RunPage, Circle1Section, 16),     // CCDSampleScale1=8
        new("settings.circle-threshold-1", "圆1圆阈值", 10L, Integer, RunPage, Circle1Section, 17),  // CCDCircleThresh1=10
        new("settings.reject-distance-1", "圆1剔除距离", 5L, Integer, RunPage, Circle1Section, 18),  // RejectDist1=5
        new("settings.init-type-1", "圆1初始类型", 2L, Integer, RunPage, Circle1Section, 19),        // InitType1=2
        new("settings.fit-function-1", "圆1拟合方式", 2L, Integer, RunPage, Circle1Section, 20),     // FitFun1=2
        new("settings.pro-length-1", "圆1延伸长度", 5L, Integer, RunPage, Circle1Section, 21),       // ProLength1=5

        // 圆2：同上（parameters.json 中后缀 2 的 12 项）。
        new("settings.find-mode-2", "圆2查找模式", 1L, Integer, RunPage, Circle2Section, 30),        // FindMode2=1
        new("settings.edge-polarity-2", "圆2边缘极性", 3L, Integer, RunPage, Circle2Section, 31),    // EdgePolarity2=3
        new("settings.edge-threshold-2", "圆2边缘阈值", 15L, Integer, RunPage, Circle2Section, 32),  // EdgeThresh2=15
        new("settings.edge-width-2", "圆2边缘宽度", 1L, Integer, RunPage, Circle2Section, 33),       // EdgeWidth2=1
        new("settings.reject-num-2", "圆2剔除点数", 0L, Integer, RunPage, Circle2Section, 34),       // RejectNum2=0
        new("settings.coarse-detect-2", "圆2粗定位", false, Boolean, RunPage, Circle2Section, 35),   // CoarseDetectFlag2=False
        new("settings.sample-scale-2", "圆2采样倍率", 8L, Integer, RunPage, Circle2Section, 36),     // CCDSampleScale2=8
        new("settings.circle-threshold-2", "圆2圆阈值", 10L, Integer, RunPage, Circle2Section, 37),  // CCDCircleThresh2=10
        new("settings.reject-distance-2", "圆2剔除距离", 5L, Integer, RunPage, Circle2Section, 38),  // RejectDist2=5
        new("settings.init-type-2", "圆2初始类型", 2L, Integer, RunPage, Circle2Section, 39),        // InitType2=2
        new("settings.fit-function-2", "圆2拟合方式", 2L, Integer, RunPage, Circle2Section, 40),     // FitFun2=2
        new("settings.pro-length-2", "圆2延伸长度", 5L, Integer, RunPage, Circle2Section, 41),       // ProLength2=5

        // 结果判断：7 组 × Enable/Low/High（parameters.json 的 Angle/Dist/Inter1X/Inter1Y/Inter2X/Inter2Y Limit*）。
        new("settings.angle-limit-enabled", "角度判断", false, Boolean, ResultPage, LimitSection, 50),
        new("settings.angle-limit-low", "角度下限", -180d, Number, ResultPage, LimitSection, 51),
        new("settings.angle-limit-high", "角度上限", 180d, Number, ResultPage, LimitSection, 52),
        new("settings.dist-limit-enabled", "距离判断", false, Boolean, ResultPage, LimitSection, 53),
        new("settings.dist-limit-low", "距离下限", 0d, Number, ResultPage, LimitSection, 54),
        new("settings.dist-limit-high", "距离上限", 99999d, Number, ResultPage, LimitSection, 55),
        new("settings.inter1-x-limit-enabled", "交点1 X判断", false, Boolean, ResultPage, LimitSection, 56),
        new("settings.inter1-x-limit-low", "交点1 X下限", 0d, Number, ResultPage, LimitSection, 57),
        new("settings.inter1-x-limit-high", "交点1 X上限", 99999d, Number, ResultPage, LimitSection, 58),
        new("settings.inter1-y-limit-enabled", "交点1 Y判断", false, Boolean, ResultPage, LimitSection, 59),
        new("settings.inter1-y-limit-low", "交点1 Y下限", 0d, Number, ResultPage, LimitSection, 60),
        new("settings.inter1-y-limit-high", "交点1 Y上限", 99999d, Number, ResultPage, LimitSection, 61),
        new("settings.inter2-x-limit-enabled", "交点2 X判断", false, Boolean, ResultPage, LimitSection, 62),
        new("settings.inter2-x-limit-low", "交点2 X下限", 0d, Number, ResultPage, LimitSection, 63),
        new("settings.inter2-x-limit-high", "交点2 X上限", 99999d, Number, ResultPage, LimitSection, 64),
        new("settings.inter2-y-limit-enabled", "交点2 Y判断", false, Boolean, ResultPage, LimitSection, 65),
        new("settings.inter2-y-limit-low", "交点2 Y下限", 0d, Number, ResultPage, LimitSection, 66),
        new("settings.inter2-y-limit-high", "交点2 Y上限", 99999d, Number, ResultPage, LimitSection, 67),
    };

    public static ToolSchema Schema { get; } = new(
        Definitions.Select(def => new Port(def.Id, def.Type, Storage: def.Type == Integer ? NumericStorage.Int32 : null,
            EditModes: FieldEditModes.Constant)).ToArray(),
        Array.Empty<Port>(),
        new ToolPresentation(Definitions.Select(def => Present(def)).ToArray()));

    public static IReadOnlyDictionary<string, object?> Defaults { get; } =
        Definitions.ToDictionary(def => def.Id, def => (object?)def.Default, StringComparer.Ordinal);

    public static InputValues Read(CircleCircleMeasureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new InputValues(Schema.Inputs, new Dictionary<string, object?>(Defaults, StringComparer.Ordinal));
    }

    public static CircleCircleMeasureSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var settings = new CircleCircleMeasureSettings(values.CopyValues());
        var errors = settings.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return settings.Freeze();
    }

    /// <summary>判据范围成对：下限字段启用后绑定上限字段，保证联动在单次编辑内生效。</summary>
    private static FieldPresentation Present(Definition def)
    {
        var gate = Gate(def.Id);
        var pair = Pair(def.Id);
        var angle = def.Id.Contains("angle-limit");
        var layout = new FieldLayout(def.Page, def.Section, gate, PairWith: pair,
            Minimum: angle ? AngleMinimum : 0d, Maximum: angle ? AngleMaximum : MaximumLimit,
            DecimalPlaces: def.Type == Number ? 2 : 0, Increment: def.Type == Number ? 0.01d : 1,
            PairMinimum: pair is null ? null : angle ? AngleMinimum : 0d,
            PairMaximum: pair is null ? null : angle ? AngleMaximum : MaximumLimit,
            PairDisplayName: pair is null ? null : def.Name.Replace("下限", "") + "范围",
            AllowReference: false, BooleanTips: def.Type == Boolean ? new("已启用", "未启用") : null);
        // 原厂 DisplayType：成对→Bettween；布尔→BooleanTips（本工具布尔都带启用/未启用提示）。
        var displayType = pair is not null
            ? def.Type == Integer ? DeclaredFieldTemplateKey.IntegerBettween : DeclaredFieldTemplateKey.FloatBettween
            : def.Type == Boolean ? DeclaredFieldTemplateKey.BooleanTips
            : def.Type == Integer ? DeclaredFieldTemplateKey.Integer
            : DeclaredFieldTemplateKey.Float;
        return new FieldPresentation(def.Id, def.Name, def.Section, def.Order,
            def.Type == Boolean ? FieldEditorKind.Scalar : FieldEditorKind.Scalar,
            null, null, displayType) { Layout = layout };
    }

    private static FieldCondition? Gate(string id)
        => id switch
        {
            var s when s.Contains("angle-limit-") && !s.EndsWith("enabled") => new("settings.angle-limit-enabled", true),
            var s when s.Contains("dist-limit-") && !s.EndsWith("enabled") => new("settings.dist-limit-enabled", true),
            var s when s.Contains("inter1-x-limit-") && !s.EndsWith("enabled") => new("settings.inter1-x-limit-enabled", true),
            var s when s.Contains("inter1-y-limit-") && !s.EndsWith("enabled") => new("settings.inter1-y-limit-enabled", true),
            var s when s.Contains("inter2-x-limit-") && !s.EndsWith("enabled") => new("settings.inter2-x-limit-enabled", true),
            var s when s.Contains("inter2-y-limit-") && !s.EndsWith("enabled") => new("settings.inter2-y-limit-enabled", true),
            _ => null
        };

    private static string? Pair(string id) => id.EndsWith("-low") ? id.Replace("-low", "-high") : null;
}

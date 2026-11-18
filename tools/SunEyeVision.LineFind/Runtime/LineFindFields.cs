using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.LineFind.Runtime;

/// <summary>
/// 直线查找字段身份与页签布局。原厂三页签（<c>resources.json:41-51</c>）：
/// Tab_Basic Params（图像/ROI 输入，本方由输入端口承载）、Tab_Run Params（运行参数）、
/// ResultShow（结果判断）。字段默认值来自 <c>parameters.json</c>，范围/枚举来自 <c>LineFindParam.cs</c>。
/// </summary>
public static class LineFindFields
{
    // Tab_Run Params / 边缘与卡尺
    public const string FindMode = "find-mode";
    public const string EdgePolarity = "edge-polarity";
    public const string EdgeStrength = "edge-strength";
    public const string KernelSize = "kernel-size";
    public const string RayNum = "ray-num";
    public const string RejectNum = "reject-num";
    public const string RevertFindOrient = "revert-find-orient";
    public const string AngleNormalization = "angle-normalization";
    public const string RejectDist = "reject-dist";
    public const string RegionWidth = "region-width";
    public const string InitType = "init-type";
    public const string FitFun = "fit-fun";
    // Tab_Run Params / 角度
    public const string LineAngleEnable = "line-angle-enable";
    public const string ExpectAngle = "expect-angle";
    public const string RotateTolerance = "rotate-tolerance";
    // ResultShow / 结果判断
    public const string FitPointsLimitEnable = "fitpoints-limit-enable";
    public const string FitPointsLimitLow = "fitpoints-limit-low";
    public const string FitPointsLimitHigh = "fitpoints-limit-high";
    public const string AngleLimitEnable = "angle-limit-enable";
    public const string AngleLimitLow = "angle-limit-low";
    public const string AngleLimitHigh = "angle-limit-high";
    public const string ScoreLimitEnable = "score-limit-enable";
    public const string ScoreLimitLow = "score-limit-low";
    public const string ScoreLimitHigh = "score-limit-high";

    private const string RunPage = "运行参数";
    private const string ResultPage = "结果显示";
    private const string EdgeSection = "边缘与卡尺";
    private const string AngleSection = "角度";
    private const string JudgeSection = "结果判断";

    private static readonly TypeRef Boolean = TypeRef.ScalarOf(ScalarKind.Boolean);
    private static readonly TypeRef Integer = TypeRef.ScalarOf(ScalarKind.Int64);
    private static readonly TypeRef Number = TypeRef.ScalarOf(ScalarKind.Float64);

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(FindMode, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(EdgePolarity, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(EdgeStrength, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(KernelSize, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(RayNum, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(RejectNum, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(RevertFindOrient, Boolean, EditModes: FieldEditModes.Constant),
        new Port(AngleNormalization, Boolean, EditModes: FieldEditModes.Constant),
        new Port(RejectDist, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(RegionWidth, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(InitType, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(FitFun, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(LineAngleEnable, Boolean, EditModes: FieldEditModes.Constant),
        new Port(ExpectAngle, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(RotateTolerance, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(FitPointsLimitEnable, Boolean, EditModes: FieldEditModes.Constant),
        new Port(FitPointsLimitLow, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(FitPointsLimitHigh, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(AngleLimitEnable, Boolean, EditModes: FieldEditModes.Constant),
        new Port(AngleLimitLow, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(AngleLimitHigh, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(ScoreLimitEnable, Boolean, EditModes: FieldEditModes.Constant),
        new Port(ScoreLimitLow, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(ScoreLimitHigh, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant)
    }, Array.Empty<Port>(), new ToolPresentation(new[]
    {
        new FieldPresentation(FindMode, "边缘类型(1最强/2第一/3最后/4近中线)", EdgeSection, 0, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 1, Maximum: 4, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(EdgePolarity, "边缘极性(1黑到白/2白到黑/3任意)", EdgeSection, 1, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 1, Maximum: 3, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(EdgeStrength, "边缘阈值", EdgeSection, 2, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 1, Maximum: 255, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(KernelSize, "滤波尺寸", EdgeSection, 3, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 1, Maximum: 50, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(RayNum, "卡尺数量", EdgeSection, 4, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 2, Maximum: 1000, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(RejectNum, "剔除点数", EdgeSection, 5, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 0, Maximum: 998, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(RevertFindOrient, "反向查找", EdgeSection, 6, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(AngleNormalization, "角度归一化", EdgeSection, 7, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(RejectDist, "剔除距离", EdgeSection, 8, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 1, Maximum: 1000, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(RegionWidth, "投影宽度", EdgeSection, 9, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 1, Maximum: 100, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(InitType, "初始拟合(1全局/2局部)", EdgeSection, 10, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 1, Maximum: 2, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(FitFun, "拟合方式(1最小二乘/2Huber/3Tukey)", EdgeSection, 11, FieldEditorKind.Scalar)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 1, Maximum: 3, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(LineAngleEnable, "角度使能", AngleSection, 12, FieldEditorKind.Scalar)
        { Layout = new(RunPage, AngleSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(ExpectAngle, "期望角度", AngleSection, 13, FieldEditorKind.Scalar)
        { Layout = new(RunPage, AngleSection, new(LineAngleEnable, true), Minimum: -180, Maximum: 180, DecimalPlaces: 2, Increment: 1) },
        new FieldPresentation(RotateTolerance, "旋转容忍", AngleSection, 14, FieldEditorKind.Scalar)
        { Layout = new(RunPage, AngleSection, new(LineAngleEnable, true), Minimum: 0, Maximum: 180, DecimalPlaces: 2, Increment: 1) },
        new FieldPresentation(FitPointsLimitEnable, "拟合点数判断", JudgeSection, 20, FieldEditorKind.Scalar)
        { Layout = new(ResultPage, JudgeSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(FitPointsLimitLow, "拟合点数范围下限", JudgeSection, 21, FieldEditorKind.Scalar)
        { Layout = new(ResultPage, JudgeSection, new(FitPointsLimitEnable, true), PairWith: FitPointsLimitHigh, Minimum: 2, Maximum: 99999, DecimalPlaces: 0, Increment: 1, PairMinimum: 2, PairMaximum: 99999, PairDisplayName: "拟合点数范围") },
        new FieldPresentation(FitPointsLimitHigh, "拟合点数范围上限", JudgeSection, 22, FieldEditorKind.Scalar)
        { Layout = new(ResultPage, JudgeSection, new(FitPointsLimitEnable, true), Minimum: 2, Maximum: 99999, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(AngleLimitEnable, "角度判断", JudgeSection, 23, FieldEditorKind.Scalar)
        { Layout = new(ResultPage, JudgeSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(AngleLimitLow, "角度范围下限", JudgeSection, 24, FieldEditorKind.Scalar)
        { Layout = new(ResultPage, JudgeSection, new(AngleLimitEnable, true), PairWith: AngleLimitHigh, Minimum: -180, Maximum: 180, DecimalPlaces: 2, Increment: 1, PairMinimum: -180, PairMaximum: 180, PairDisplayName: "角度范围") },
        new FieldPresentation(AngleLimitHigh, "角度范围上限", JudgeSection, 25, FieldEditorKind.Scalar)
        { Layout = new(ResultPage, JudgeSection, new(AngleLimitEnable, true), Minimum: -180, Maximum: 180, DecimalPlaces: 2, Increment: 1) },
        new FieldPresentation(ScoreLimitEnable, "拟合误差判断", JudgeSection, 26, FieldEditorKind.Scalar)
        { Layout = new(ResultPage, JudgeSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(ScoreLimitLow, "拟合误差范围下限", JudgeSection, 27, FieldEditorKind.Scalar)
        { Layout = new(ResultPage, JudgeSection, new(ScoreLimitEnable, true), PairWith: ScoreLimitHigh, Minimum: 0, Maximum: 9999, DecimalPlaces: 2, Increment: 1, PairMinimum: 0, PairMaximum: 9999, PairDisplayName: "拟合误差范围") },
        new FieldPresentation(ScoreLimitHigh, "拟合误差范围上限", JudgeSection, 28, FieldEditorKind.Scalar)
        { Layout = new(ResultPage, JudgeSection, new(ScoreLimitEnable, true), Minimum: 0, Maximum: 9999, DecimalPlaces: 2, Increment: 1) }
    }));

    public static InputValues Read(LineFindSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(Schema.Inputs, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [FindMode] = (long)settings.FindMode, [EdgePolarity] = (long)settings.EdgePolarity,
            [EdgeStrength] = (long)settings.EdgeStrength, [KernelSize] = (long)settings.KernelSize,
            [RayNum] = (long)settings.RayNum, [RejectNum] = (long)settings.RejectNum,
            [RevertFindOrient] = settings.RevertFindOrient, [AngleNormalization] = settings.AngleNormalization,
            [RejectDist] = (long)settings.RejectDist, [RegionWidth] = (long)settings.RegionWidth,
            [InitType] = (long)settings.InitType, [FitFun] = (long)settings.FitFun,
            [LineAngleEnable] = settings.LineAngleEnable, [ExpectAngle] = settings.ExpectAngle, [RotateTolerance] = settings.RotateTolerance,
            [FitPointsLimitEnable] = settings.FitPointsLimitEnable, [FitPointsLimitLow] = (long)settings.FitPointsLimitLow, [FitPointsLimitHigh] = (long)settings.FitPointsLimitHigh,
            [AngleLimitEnable] = settings.AngleLimitEnable, [AngleLimitLow] = settings.AngleLimitLow, [AngleLimitHigh] = settings.AngleLimitHigh,
            [ScoreLimitEnable] = settings.ScoreLimitEnable, [ScoreLimitLow] = settings.ScoreLimitLow, [ScoreLimitHigh] = settings.ScoreLimitHigh
        });
    }

    public static LineFindSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var candidate = new InputValues(Schema.Inputs, values.CopyValues());
        long I(string id) => ToInteger(candidate.Get<object?>(id), id);
        double N(string id) => ToNumber(candidate.Get<object?>(id), id);
        var result = new LineFindSettings
        {
            FindMode = (int)I(FindMode), EdgePolarity = (int)I(EdgePolarity), EdgeStrength = (int)I(EdgeStrength),
            KernelSize = (int)I(KernelSize), RayNum = (int)I(RayNum), RejectNum = (int)I(RejectNum),
            RevertFindOrient = candidate.Get<bool>(RevertFindOrient), AngleNormalization = candidate.Get<bool>(AngleNormalization),
            RejectDist = (int)I(RejectDist), RegionWidth = (int)I(RegionWidth), InitType = (int)I(InitType), FitFun = (int)I(FitFun),
            LineAngleEnable = candidate.Get<bool>(LineAngleEnable), ExpectAngle = N(ExpectAngle), RotateTolerance = N(RotateTolerance),
            FitPointsLimitEnable = candidate.Get<bool>(FitPointsLimitEnable), FitPointsLimitLow = (int)I(FitPointsLimitLow), FitPointsLimitHigh = (int)I(FitPointsLimitHigh),
            AngleLimitEnable = candidate.Get<bool>(AngleLimitEnable), AngleLimitLow = N(AngleLimitLow), AngleLimitHigh = N(AngleLimitHigh),
            ScoreLimitEnable = candidate.Get<bool>(ScoreLimitEnable), ScoreLimitLow = N(ScoreLimitLow), ScoreLimitHigh = N(ScoreLimitHigh)
        };
        var errors = result.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return result.Freeze();
    }

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

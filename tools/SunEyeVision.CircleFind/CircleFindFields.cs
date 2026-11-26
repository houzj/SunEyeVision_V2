using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.CircleFind.Runtime;

/// <summary>
/// 圆查找字段身份与页签布局。原厂三页签（<c>resources.json:41-51</c>）：
/// Tab_Basic Params（图像/ROI 输入，本方由输入端口承载）、Tab_Run Params（运行参数）、
/// ResultShow（结果判断）。字段默认值来自 <c>parameters.json</c>，范围/枚举来自
/// <c>CircleFindParam.cs</c>。页签/分组按 uiControls 顺序与控件名映射，逐项见交接单。
/// </summary>
public static class CircleFindFields
{
    // 运行参数（Tab_Run Params）
    public const string FindMode = "find-mode";
    public const string EdgePolarity = "edge-polarity";
    public const string EdgeThresh = "edge-thresh";
    public const string EdgeWidth = "edge-width";
    public const string RadNum = "rad-num";
    public const string RejectNum = "reject-num";
    public const string CoarseDetectFlag = "coarse-detect";
    public const string CcdSampleScale = "ccd-sample-scale";
    public const string CcdCircleThresh = "ccd-circle-thresh";
    public const string RejectDist = "reject-dist";
    public const string ProLength = "pro-length";
    public const string InitType = "init-type";
    public const string FitFun = "fit-fun";
    // 结果判断（ResultShow）
    public const string RadiusLimitEnable = "radius-limit-enable";
    public const string RadiusLimitLow = "radius-limit-low";
    public const string RadiusLimitHigh = "radius-limit-high";
    public const string CenterXLimitEnable = "centerx-limit-enable";
    public const string CenterXLimitLow = "centerx-limit-low";
    public const string CenterXLimitHigh = "centerx-limit-high";
    public const string CenterYLimitEnable = "centery-limit-enable";
    public const string CenterYLimitLow = "centery-limit-low";
    public const string CenterYLimitHigh = "centery-limit-high";
    public const string FitErrorLimitEnable = "fiterror-limit-enable";
    public const string FitErrorLimitLow = "fiterror-limit-low";
    public const string FitErrorLimitHigh = "fiterror-limit-high";
    public const string FitPointsLimitEnable = "fitpoints-limit-enable";
    public const string FitPointsLimitLow = "fitpoints-limit-low";
    public const string FitPointsLimitHigh = "fitpoints-limit-high";

    private const string RunPage = "运行参数";
    private const string ResultPage = "结果显示";
    private const string EdgeSection = "边缘与卡尺";
    private const string LocateSection = "定位与拟合";
    private const string JudgeSection = "结果判断";

    private static readonly TypeRef Boolean = TypeRef.ScalarOf(ScalarKind.Boolean);
    private static readonly TypeRef Integer = TypeRef.ScalarOf(ScalarKind.Int64);
    private static readonly TypeRef Number = TypeRef.ScalarOf(ScalarKind.Float64);
    private static readonly TypeRef Text = TypeRef.ScalarOf(ScalarKind.Text);

    // 原厂枚举在 XML 中以整数存储，界面以下拉中文显示；取值集合与显示名一一对应。
    private static TextChoiceConstraint CodeChoice(params string[] codes) => new(codes);
    private static IReadOnlyDictionary<string, string> CodeLabels(params (string Code, string Label)[] labels)
        => labels.ToDictionary(item => item.Code, item => item.Label, StringComparer.Ordinal);

    private static readonly string[] FindModeCodes = { "1", "2", "3" };
    private static readonly IReadOnlyDictionary<string, string> FindModeLabels =
        CodeLabels(("1", "最强"), ("2", "最后"), ("3", "第一条"));
    private static readonly string[] EdgePolarityCodes = { "1", "2", "3" };
    private static readonly IReadOnlyDictionary<string, string> EdgePolarityLabels =
        CodeLabels(("1", "黑到白"), ("2", "白到黑"), ("3", "任意极性"));
    private static readonly string[] InitTypeCodes = { "1", "2" };
    private static readonly IReadOnlyDictionary<string, string> InitTypeLabels =
        CodeLabels(("1", "全局ALS"), ("2", "局部LLS"));
    private static readonly string[] FitFunCodes = { "1", "2", "3" };
    private static readonly IReadOnlyDictionary<string, string> FitFunLabels =
        CodeLabels(("1", "最小二乘"), ("2", "Huber"), ("3", "Tukey"));

    public static ToolSchema Schema { get; } = new(new[]
    {
        // Tab_Run Params / 边缘与卡尺
        new Port(FindMode, Text, Choices: CodeChoice(FindModeCodes), EditModes: FieldEditModes.Constant),
        new Port(EdgePolarity, Text, Choices: CodeChoice(EdgePolarityCodes), EditModes: FieldEditModes.Constant),
        new Port(EdgeThresh, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(EdgeWidth, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(RadNum, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(RejectNum, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        // Tab_Run Params / 定位与拟合
        new Port(CoarseDetectFlag, Boolean, EditModes: FieldEditModes.Constant),
        new Port(CcdSampleScale, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(CcdCircleThresh, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(RejectDist, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(ProLength, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(InitType, Text, Choices: CodeChoice(InitTypeCodes), EditModes: FieldEditModes.Constant),
        new Port(FitFun, Text, Choices: CodeChoice(FitFunCodes), EditModes: FieldEditModes.Constant),
        // ResultShow / 结果判断
        new Port(RadiusLimitEnable, Boolean, EditModes: FieldEditModes.Constant),
        new Port(RadiusLimitLow, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(RadiusLimitHigh, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(CenterXLimitEnable, Boolean, EditModes: FieldEditModes.Constant),
        new Port(CenterXLimitLow, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(CenterXLimitHigh, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(CenterYLimitEnable, Boolean, EditModes: FieldEditModes.Constant),
        new Port(CenterYLimitLow, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(CenterYLimitHigh, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(FitErrorLimitEnable, Boolean, EditModes: FieldEditModes.Constant),
        new Port(FitErrorLimitLow, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(FitErrorLimitHigh, Number, Storage: NumericStorage.Float32, EditModes: FieldEditModes.Constant),
        new Port(FitPointsLimitEnable, Boolean, EditModes: FieldEditModes.Constant),
        new Port(FitPointsLimitLow, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant),
        new Port(FitPointsLimitHigh, Integer, Storage: NumericStorage.Int32, EditModes: FieldEditModes.Constant)
    }, Array.Empty<Port>(), new ToolPresentation(new[]
    {
        // Tab_Run Params / 边缘与卡尺
        new FieldPresentation(FindMode, "边缘类型", EdgeSection, 0, FieldEditorKind.Choice, FindModeCodes, FindModeLabels, DeclaredFieldTemplateKey.Enumeration)
        { Layout = new(RunPage, EdgeSection, AllowReference: false) },
        new FieldPresentation(EdgePolarity, "边缘极性", EdgeSection, 1, FieldEditorKind.Choice, EdgePolarityCodes, EdgePolarityLabels, DeclaredFieldTemplateKey.Enumeration)
        { Layout = new(RunPage, EdgeSection, AllowReference: false) },
        new FieldPresentation(EdgeThresh, "边缘阈值", EdgeSection, 2, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 0, Maximum: 255, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(EdgeWidth, "滤波尺寸", EdgeSection, 3, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 1, Maximum: 50, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(RadNum, "卡尺数量", EdgeSection, 4, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 3, Maximum: 1000, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(RejectNum, "剔除点数", EdgeSection, 5, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer)
        { Layout = new(RunPage, EdgeSection, AllowReference: false, Minimum: 0, Maximum: 997, DecimalPlaces: 0, Increment: 1) },
        // Tab_Run Params / 定位与拟合
        new FieldPresentation(CoarseDetectFlag, "初定位", LocateSection, 10, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.BooleanTips)
        { Layout = new(RunPage, LocateSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(CcdSampleScale, "下采样系数", LocateSection, 11, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer)
        { Layout = new(RunPage, LocateSection, AllowReference: false, Minimum: 1, Maximum: 8, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(CcdCircleThresh, "圆定位敏感度", LocateSection, 12, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer)
        { Layout = new(RunPage, LocateSection, AllowReference: false, Minimum: 1, Maximum: 1000, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(RejectDist, "剔除距离", LocateSection, 13, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer)
        { Layout = new(RunPage, LocateSection, AllowReference: false, Minimum: 1, Maximum: 1000, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(ProLength, "投影宽度", LocateSection, 14, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer)
        { Layout = new(RunPage, LocateSection, AllowReference: false, Minimum: 1, Maximum: 500, DecimalPlaces: 0, Increment: 1) },
        new FieldPresentation(InitType, "初始拟合", LocateSection, 15, FieldEditorKind.Choice, InitTypeCodes, InitTypeLabels, DeclaredFieldTemplateKey.Enumeration)
        { Layout = new(RunPage, LocateSection, AllowReference: false) },
        new FieldPresentation(FitFun, "拟合方式", LocateSection, 16, FieldEditorKind.Choice, FitFunCodes, FitFunLabels, DeclaredFieldTemplateKey.Enumeration)
        { Layout = new(RunPage, LocateSection, AllowReference: false) },
        // ResultShow / 结果判断（原厂 LimitEnable + FloatBettween/IntegerBettween）
        new FieldPresentation(RadiusLimitEnable, "半径判断", JudgeSection, 20, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.BooleanTips)
        { Layout = new(ResultPage, JudgeSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(RadiusLimitLow, "半径范围下限", JudgeSection, 21, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.FloatBettween)
        { Layout = new(ResultPage, JudgeSection, new(RadiusLimitEnable, true), PairWith: RadiusLimitHigh, Minimum: 0, Maximum: 99999, DecimalPlaces: 2, Increment: 1, PairMinimum: 0, PairMaximum: 99999, PairDisplayName: "半径范围") },
        new FieldPresentation(RadiusLimitHigh, "半径范围上限", JudgeSection, 22, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Float)
        { Layout = new(ResultPage, JudgeSection, new(RadiusLimitEnable, true), Minimum: 0, Maximum: 99999, DecimalPlaces: 2, Increment: 1) },
        new FieldPresentation(CenterXLimitEnable, "圆心X判断", JudgeSection, 23, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.BooleanTips)
        { Layout = new(ResultPage, JudgeSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(CenterXLimitLow, "圆心X范围下限", JudgeSection, 24, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.FloatBettween)
        { Layout = new(ResultPage, JudgeSection, new(CenterXLimitEnable, true), PairWith: CenterXLimitHigh, Minimum: -99999, Maximum: 99999, DecimalPlaces: 2, Increment: 1, PairMinimum: -99999, PairMaximum: 99999, PairDisplayName: "圆心X范围") },
        new FieldPresentation(CenterXLimitHigh, "圆心X范围上限", JudgeSection, 25, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Float)
        { Layout = new(ResultPage, JudgeSection, new(CenterXLimitEnable, true), Minimum: -99999, Maximum: 99999, DecimalPlaces: 2, Increment: 1) },
        new FieldPresentation(CenterYLimitEnable, "圆心Y判断", JudgeSection, 26, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.BooleanTips)
        { Layout = new(ResultPage, JudgeSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(CenterYLimitLow, "圆心Y范围下限", JudgeSection, 27, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.FloatBettween)
        { Layout = new(ResultPage, JudgeSection, new(CenterYLimitEnable, true), PairWith: CenterYLimitHigh, Minimum: -99999, Maximum: 99999, DecimalPlaces: 2, Increment: 1, PairMinimum: -99999, PairMaximum: 99999, PairDisplayName: "圆心Y范围") },
        new FieldPresentation(CenterYLimitHigh, "圆心Y范围上限", JudgeSection, 28, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Float)
        { Layout = new(ResultPage, JudgeSection, new(CenterYLimitEnable, true), Minimum: -99999, Maximum: 99999, DecimalPlaces: 2, Increment: 1) },
        new FieldPresentation(FitErrorLimitEnable, "拟合误差判断", JudgeSection, 29, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.BooleanTips)
        { Layout = new(ResultPage, JudgeSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(FitErrorLimitLow, "拟合误差范围下限", JudgeSection, 30, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.FloatBettween)
        { Layout = new(ResultPage, JudgeSection, new(FitErrorLimitEnable, true), PairWith: FitErrorLimitHigh, Minimum: 0, Maximum: 100, DecimalPlaces: 2, Increment: 1, PairMinimum: 0, PairMaximum: 100, PairDisplayName: "拟合误差范围") },
        new FieldPresentation(FitErrorLimitHigh, "拟合误差范围上限", JudgeSection, 31, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Float)
        { Layout = new(ResultPage, JudgeSection, new(FitErrorLimitEnable, true), Minimum: 0, Maximum: 100, DecimalPlaces: 2, Increment: 1) },
        new FieldPresentation(FitPointsLimitEnable, "拟合点数判断", JudgeSection, 32, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.BooleanTips)
        { Layout = new(ResultPage, JudgeSection, AllowReference: false, BooleanTips: new("启用", "关闭")) },
        new FieldPresentation(FitPointsLimitLow, "拟合点数范围下限", JudgeSection, 33, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.IntegerBettween)
        { Layout = new(ResultPage, JudgeSection, new(FitPointsLimitEnable, true), PairWith: FitPointsLimitHigh, Minimum: 3, Maximum: 99999, DecimalPlaces: 0, Increment: 1, PairMinimum: 3, PairMaximum: 99999, PairDisplayName: "拟合点数范围") },
        new FieldPresentation(FitPointsLimitHigh, "拟合点数范围上限", JudgeSection, 34, FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer)
        { Layout = new(ResultPage, JudgeSection, new(FitPointsLimitEnable, true), Minimum: 3, Maximum: 99999, DecimalPlaces: 0, Increment: 1) }
    }));

    public static InputValues Read(CircleFindSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(Schema.Inputs, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [FindMode] = settings.FindMode.ToString(CultureInfo.InvariantCulture),
            [EdgePolarity] = settings.EdgePolarity.ToString(CultureInfo.InvariantCulture),
            [EdgeThresh] = (long)settings.EdgeThresh, [EdgeWidth] = (long)settings.EdgeWidth,
            [RadNum] = (long)settings.RadNum, [RejectNum] = (long)settings.RejectNum,
            [CoarseDetectFlag] = settings.CoarseDetectFlag, [CcdSampleScale] = (long)settings.CcdSampleScale,
            [CcdCircleThresh] = (long)settings.CcdCircleThresh, [RejectDist] = (long)settings.RejectDist,
            [ProLength] = (long)settings.ProLength,
            [InitType] = settings.InitType.ToString(CultureInfo.InvariantCulture),
            [FitFun] = settings.FitFun.ToString(CultureInfo.InvariantCulture),
            [RadiusLimitEnable] = settings.RadiusLimitEnable, [RadiusLimitLow] = settings.RadiusLimitLow, [RadiusLimitHigh] = settings.RadiusLimitHigh,
            [CenterXLimitEnable] = settings.CenterXLimitEnable, [CenterXLimitLow] = settings.CenterXLimitLow, [CenterXLimitHigh] = settings.CenterXLimitHigh,
            [CenterYLimitEnable] = settings.CenterYLimitEnable, [CenterYLimitLow] = settings.CenterYLimitLow, [CenterYLimitHigh] = settings.CenterYLimitHigh,
            [FitErrorLimitEnable] = settings.FitErrorLimitEnable, [FitErrorLimitLow] = settings.FitErrorLimitLow, [FitErrorLimitHigh] = settings.FitErrorLimitHigh,
            [FitPointsLimitEnable] = settings.FitPointsLimitEnable, [FitPointsLimitLow] = (long)settings.FitPointsLimitLow, [FitPointsLimitHigh] = (long)settings.FitPointsLimitHigh
        });
    }

    public static CircleFindSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var candidate = new InputValues(Schema.Inputs, values.CopyValues());
        long I(string id) => ToInteger(candidate.Get<object?>(id), id);
        double N(string id) => ToNumber(candidate.Get<object?>(id), id);
        int C(string id) => ToCode(candidate.Get<string>(id), id);
        var result = new CircleFindSettings
        {
            FindMode = C(FindMode), EdgePolarity = C(EdgePolarity), EdgeThresh = (int)I(EdgeThresh),
            EdgeWidth = (int)I(EdgeWidth), RadNum = (int)I(RadNum), RejectNum = (int)I(RejectNum),
            CoarseDetectFlag = candidate.Get<bool>(CoarseDetectFlag), CcdSampleScale = (int)I(CcdSampleScale),
            CcdCircleThresh = (int)I(CcdCircleThresh), RejectDist = (int)I(RejectDist), ProLength = (int)I(ProLength),
            InitType = C(InitType), FitFun = C(FitFun),
            RadiusLimitEnable = candidate.Get<bool>(RadiusLimitEnable), RadiusLimitLow = N(RadiusLimitLow), RadiusLimitHigh = N(RadiusLimitHigh),
            CenterXLimitEnable = candidate.Get<bool>(CenterXLimitEnable), CenterXLimitLow = N(CenterXLimitLow), CenterXLimitHigh = N(CenterXLimitHigh),
            CenterYLimitEnable = candidate.Get<bool>(CenterYLimitEnable), CenterYLimitLow = N(CenterYLimitLow), CenterYLimitHigh = N(CenterYLimitHigh),
            FitErrorLimitEnable = candidate.Get<bool>(FitErrorLimitEnable), FitErrorLimitLow = N(FitErrorLimitLow), FitErrorLimitHigh = N(FitErrorLimitHigh),
            FitPointsLimitEnable = candidate.Get<bool>(FitPointsLimitEnable), FitPointsLimitLow = (int)I(FitPointsLimitLow), FitPointsLimitHigh = (int)I(FitPointsLimitHigh)
        };
        var errors = result.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return result.Freeze();
    }

    /// <summary>把原厂整数枚举码（下拉选项文本）还原为整数值；未声明选项直接报错，不静默回退。</summary>
    private static int ToCode(object? value, string field)
    {
        if (value is string text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            return code;
        throw new ArgumentException($"字段 {field} 必须是已声明的枚举选项，实际为 {value ?? "空值"}。");
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

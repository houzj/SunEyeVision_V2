using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.MarkFind.Runtime;

/// <summary>一个原厂参数在编辑/持久化上的完整声明，不包含执行语义。</summary>
public sealed class MarkFindFieldSpec
{
    public required string VirtualMachineName { get; init; }
    public required string Name { get; init; }
    public required string Id { get; init; }
    public required Type Type { get; init; }
    public required object Default { get; init; }
    public required string Label { get; init; }
    public required string Page { get; init; }
    public required string Section { get; init; }
    public string? VisibleWhen { get; init; }
    public string? PairWith { get; init; }
    public string? PairLabel { get; init; }
    public bool Advanced { get; init; }
    public bool AllowReference { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public double? PairMinimum { get; init; }
    public double? PairMaximum { get; init; }
    public int DecimalPlaces { get; init; }
    public double Increment { get; init; } = 1;
    public IReadOnlyList<string> Choices { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> ChoiceLabels { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// 标记查找（原件 <c>IMVSMarkFindModu</c>）的参数清单。数据来源：
/// <list type="bullet">
/// <item>模板参数：<c>ModuleControls/IMVSMarkFindModuControl/Apps.XmlParser.ParamTab/MarkFindDialogData.cs</c>
/// 字段默认值（_RoughPyramidScale=3、_HighThreshold=20、_FeatureMatchType=1、_RotationAngle=10、
/// _RotationStep=0.5f、_ProjectionInterval=3、_MarkType=2、_MaskType=0）与
/// <c>MarkFindDialogUI.xaml</c> 的控件顺序/范围/显隐（第 566–600 行、第 584–595 行）。</item>
/// <item>运行/判断参数：<c>docs/07-工具逆向/tools/imvsmarkfindmodu/parameters.json</c> 的 47 项 ParamItem
/// （名称与默认值逐条对应）与 <c>ui-preview.json</c> 的页签/控件顺序。</item>
/// </list>
/// 字段 ID 由字段名确定性派生，保存方案时不随版本变化。
/// </summary>
public static class MarkFindFields
{
    public const string BasicPage = "基本参数";
    public const string TemplatePage = "特征模型";
    public const string RunPage = "运行参数";
    public const string TemplateSection = "特征模型";
    public const string RunSection = "运行参数";
    public const string JudgeSection = "结果判断";

    private static readonly IReadOnlyDictionary<string, PropertyInfo> Properties =
        typeof(MarkFindSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, property => property, StringComparer.Ordinal);
    private static readonly IReadOnlyList<MarkFindFieldSpec> FieldList = Build();
    private static readonly IReadOnlyDictionary<string, MarkFindFieldSpec> ByName =
        new Dictionary<string, MarkFindFieldSpec>(FieldList.ToDictionary(item => item.Name, StringComparer.Ordinal), StringComparer.Ordinal);

    public static IReadOnlyList<MarkFindFieldSpec> All => FieldList;

    public static string ModelData => IdOf(nameof(MarkFindSettings.ModelData));
    public static string PyramidScaleMode => IdOf(nameof(MarkFindSettings.PyramidScaleMode));
    public static string RoughPyramidScale => IdOf(nameof(MarkFindSettings.RoughPyramidScale));
    public static string FinePyramidScale => IdOf(nameof(MarkFindSettings.FinePyramidScale));
    public static string ThresholdMode => IdOf(nameof(MarkFindSettings.ThresholdMode));
    public static string HighThreshold => IdOf(nameof(MarkFindSettings.HighThreshold));
    public static string FeatureMatchType => IdOf(nameof(MarkFindSettings.FeatureMatchType));
    public static string RotationAngle => IdOf(nameof(MarkFindSettings.RotationAngle));
    public static string RotationStep => IdOf(nameof(MarkFindSettings.RotationStep));
    public static string ProjectionInterval => IdOf(nameof(MarkFindSettings.ProjectionInterval));
    public static string MarkType => IdOf(nameof(MarkFindSettings.MarkType));
    public static string MaskType => IdOf(nameof(MarkFindSettings.MaskType));
    public static string MinScore => IdOf(nameof(MarkFindSettings.MinScore));
    public static string MaxMatchCount => IdOf(nameof(MarkFindSettings.MaxMatchCount));
    public static string Polarity => IdOf(nameof(MarkFindSettings.Polarity));
    public static string AngleStart => IdOf(nameof(MarkFindSettings.AngleStart));
    public static string AngleEnd => IdOf(nameof(MarkFindSettings.AngleEnd));
    public static string ScaleStart => IdOf(nameof(MarkFindSettings.ScaleStart));
    public static string ScaleEnd => IdOf(nameof(MarkFindSettings.ScaleEnd));
    public static string ScaleXStart => IdOf(nameof(MarkFindSettings.ScaleXStart));
    public static string ScaleXEnd => IdOf(nameof(MarkFindSettings.ScaleXEnd));
    public static string ScaleYStart => IdOf(nameof(MarkFindSettings.ScaleYStart));
    public static string ScaleYEnd => IdOf(nameof(MarkFindSettings.ScaleYEnd));
    public static string MaxOverlap => IdOf(nameof(MarkFindSettings.MaxOverlap));
    public static string SortType => IdOf(nameof(MarkFindSettings.SortType));
    public static string MatchThresholdMode => IdOf(nameof(MarkFindSettings.MatchThresholdMode));
    public static string SpotterEnabled => IdOf(nameof(MarkFindSettings.SpotterEnabled));
    public static string MatchExtentRate => IdOf(nameof(MarkFindSettings.MatchExtentRate));
    public static string TimeOut => IdOf(nameof(MarkFindSettings.TimeOut));
    public static string OutlineEnabled => IdOf(nameof(MarkFindSettings.OutlineEnabled));
    public static string MarkLocationType => IdOf(nameof(MarkFindSettings.MarkLocationType));
    public static string CountLimitEnabled => IdOf(nameof(MarkFindSettings.CountLimitEnabled));
    public static string CountLimitLow => IdOf(nameof(MarkFindSettings.CountLimitLow));
    public static string CountLimitHigh => IdOf(nameof(MarkFindSettings.CountLimitHigh));
    public static string ScoreLimitEnabled => IdOf(nameof(MarkFindSettings.ScoreLimitEnabled));
    public static string ScoreLimitLow => IdOf(nameof(MarkFindSettings.ScoreLimitLow));
    public static string ScoreLimitHigh => IdOf(nameof(MarkFindSettings.ScoreLimitHigh));

    public static string IdOf(string name)
        => ByName.TryGetValue(name, out var field) ? field.Id : throw new ArgumentException("未知的 MarkFind 设置字段：" + name, nameof(name));

    public static MarkFindFieldSpec Spec(string name)
        => ByName.TryGetValue(name, out var field) ? field : throw new ArgumentException("未知的 MarkFind 设置字段：" + name, nameof(name));

    public static MarkFindFieldSpec SpecByVmName(string virtualMachineName)
        => FieldList.FirstOrDefault(field => field.VirtualMachineName == virtualMachineName)
            ?? throw new ArgumentException("无关的原厂参数名：" + virtualMachineName, nameof(virtualMachineName));

    private static string StableId(string name)
        => "field:" + new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("SunEyeVision.Tool.MarkFind:" + name)).AsSpan(0, 16)).ToString("N");

    private static MarkFindFieldSpec B(string vm, string name, string label, string section, bool value,
        string? when = null, bool advanced = false, string page = RunPage)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(bool), Default = value,
            Label = label, Page = page, Section = section, VisibleWhen = when, Advanced = advanced
        };

    private static MarkFindFieldSpec I(string vm, string name, string label, string section, int value, double minimum, double maximum,
        string? when = null, string? pair = null, string? pairLabel = null, bool advanced = false, bool reference = true,
        string page = RunPage)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(int), Default = value,
            Label = label, Page = page, Section = section, VisibleWhen = when, PairWith = pair, PairLabel = pairLabel,
            Advanced = advanced, AllowReference = reference, Minimum = minimum, Maximum = maximum,
            PairMinimum = pair is null ? null : minimum, PairMaximum = pair is null ? null : maximum, DecimalPlaces = 0, Increment = 1
        };

    private static MarkFindFieldSpec N(string vm, string name, string label, string section, double value, double minimum, double maximum,
        string? when = null, string? pair = null, string? pairLabel = null, int decimals = 2, double increment = 0.01,
        bool advanced = false, bool reference = true, string page = RunPage)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(double), Default = value,
            Label = label, Page = page, Section = section, VisibleWhen = when, PairWith = pair, PairLabel = pairLabel,
            Advanced = advanced, AllowReference = reference, Minimum = minimum, Maximum = maximum,
            PairMinimum = pair is null ? null : minimum, PairMaximum = pair is null ? null : maximum,
            DecimalPlaces = decimals, Increment = increment
        };

    private static MarkFindFieldSpec E(string vm, string name, string label, string section, Type enumType, int value,
        IEnumerable<string> keys, IEnumerable<string> labels, string? when = null, bool advanced = false, string page = RunPage)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = enumType,
            Default = Enum.ToObject(enumType, value), Label = label, Page = page, Section = section,
            VisibleWhen = when, Advanced = advanced,
            Choices = keys.ToArray(), ChoiceLabels = keys.Zip(labels).ToDictionary(item => item.First, item => item.Second, StringComparer.Ordinal)
        };

    private static MarkFindFieldSpec S(string vm, string name, string label, string section, string value, string page)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(string), Default = value,
            Label = label, Page = page, Section = section, DecimalPlaces = 0
        };

    private static IReadOnlyList<MarkFindFieldSpec> Build()
    {
        var fields = new List<MarkFindFieldSpec>
        {
            // 模板参数（原件模型配置窗口；Page=模板参数，全部不走变量引用）
            S("ModelData", nameof(MarkFindSettings.ModelData), "模板数据", TemplateSection, "", TemplatePage),
            E("PyramidscaleMode", nameof(MarkFindSettings.PyramidScaleMode), "金字塔比例", TemplateSection, typeof(MarkFindPyramidMode), 0,
                Enum.GetNames<MarkFindPyramidMode>(), new[] { "自动", "手动" }, page: TemplatePage),
            I("PyramidScale", nameof(MarkFindSettings.RoughPyramidScale), "压缩比", TemplateSection, 3, 1, 20,
                when: nameof(MarkFindSettings.PyramidScaleMode), reference: false, page: TemplatePage),
            I("FinePyramidscale", nameof(MarkFindSettings.FinePyramidScale), "特征尺度", TemplateSection, 3, 1, 20,
                when: nameof(MarkFindSettings.PyramidScaleMode), reference: false, page: TemplatePage),
            E("ThresholdMode", nameof(MarkFindSettings.ThresholdMode), "阈值模式", TemplateSection, typeof(MarkFindThresholdMode), 0,
                Enum.GetNames<MarkFindThresholdMode>(), new[] { "自动", "手动" }, page: TemplatePage),
            I("HighThreshold", nameof(MarkFindSettings.HighThreshold), "对比度阈值", TemplateSection, 20, 1, 255,
                when: nameof(MarkFindSettings.ThresholdMode), reference: false, page: TemplatePage),
            E("FeatureMatchType", nameof(MarkFindSettings.FeatureMatchType), "定位方式", TemplateSection, typeof(MarkFindMatchMode), 1,
                Enum.GetNames<MarkFindMatchMode>(), new[] { "快速特征匹配", "高精度特征匹配" }, page: TemplatePage),
            I("RotationAngle", nameof(MarkFindSettings.RotationAngle), "旋转角度", TemplateSection, 10, 5, 45,
                advanced: true, reference: false, page: TemplatePage),
            N("RotationStep", nameof(MarkFindSettings.RotationStep), "旋转步长", TemplateSection, 0.5, 0.1, 1,
                decimals: 1, increment: 0.1, advanced: true, reference: false, page: TemplatePage),
            I("ProjectionInterval", nameof(MarkFindSettings.ProjectionInterval), "投影区间", TemplateSection, 3, 1, 10,
                advanced: true, reference: false, page: TemplatePage),
            E("MarkType", nameof(MarkFindSettings.MarkType), "Mark类型", TemplateSection, typeof(MarkFindMarkType), 2,
                Enum.GetNames<MarkFindMarkType>(), new[] { "自动判断", "空心十字", "实心十字", "方框" },
                advanced: true, page: TemplatePage),
            E("MaskType", nameof(MarkFindSettings.MaskType), "标记类型", TemplateSection, typeof(MarkFindMaskType), 0,
                Enum.GetNames<MarkFindMaskType>(), new[] { "十字标记", "矩形标记" }, page: TemplatePage),

            // 运行参数 / 运行参数（原件 ParamItem）
            N("MinScore", nameof(MarkFindSettings.MinScore), "最小分数", RunSection, 0.5, 0, 1),
            I("MaxMatchNum", nameof(MarkFindSettings.MaxMatchCount), "最大匹配数", RunSection, 1, 1, 1000),
            I("Polarity", nameof(MarkFindSettings.Polarity), "极性", RunSection, 1, 1, 3),
            I("AngleStart", nameof(MarkFindSettings.AngleStart), "角度下限", RunSection, -45, -180, 180,
                pair: nameof(MarkFindSettings.AngleEnd), pairLabel: "角度范围"),
            I("AngleEnd", nameof(MarkFindSettings.AngleEnd), "角度上限", RunSection, 45, -180, 180),
            N("ScaleStart", nameof(MarkFindSettings.ScaleStart), "缩放下限", RunSection, 1, 0.1, 10,
                pair: nameof(MarkFindSettings.ScaleEnd), pairLabel: "缩放范围"),
            N("ScaleEnd", nameof(MarkFindSettings.ScaleEnd), "缩放上限", RunSection, 1, 0.1, 10),
            // 原厂 AlgorithmTab.xml 的 Expand（Tab_Run Params 页）收起时隐藏项之一。
            I("MaxOverlap", nameof(MarkFindSettings.MaxOverlap), "最大重叠率", RunSection, 80, 0, 100, advanced: true),
            E("SortType", nameof(MarkFindSettings.SortType), "排序方式", RunSection, typeof(MarkFindSortType), 2,
                Enum.GetNames<MarkFindSortType>(), new[] { "升序", "降序", "不排序" }, advanced: true),
            // 证据 IMVSMarkFindModuAlgorithmTab.xml：MatchThresholdFlag 是 Enumeration
            // （EnumEntrys = Auto / Model / Manual，CurValue 默认 0）。
            E("MatchThresholdFlag", nameof(MarkFindSettings.MatchThresholdMode), "匹配阈值使能", RunSection,
                typeof(MarkFindMatchThresholdMode), 0, Enum.GetNames<MarkFindMatchThresholdMode>(),
                new[] { "自动", "模型", "手动" }, advanced: true),
            // 证据同上的 MultiTrigger：CurValue=2（Manual）时显示 MatchThresholdHigh。
            I("MatchThresholdHigh", nameof(MarkFindSettings.MatchThreshold), "匹配阈值", RunSection, 40, 0, 255,
                when: nameof(MarkFindSettings.MatchThresholdMode)),
            B("SpotterFlag", nameof(MarkFindSettings.SpotterEnabled), "斑点定位", RunSection, false, advanced: true),
            I("MatchExtentRate", nameof(MarkFindSettings.MatchExtentRate), "匹配范围比例", RunSection, 0, 0, 100, advanced: true),
            I("TimeOut", nameof(MarkFindSettings.TimeOut), "时间限制", RunSection, 0, 0, 600_000, advanced: true),
            // 原厂 Expand（Tab_Run Params 页）收起时隐藏项之一。
            B("OutLineEnable", nameof(MarkFindSettings.OutlineEnabled), "轮廓输出使能", RunSection, true, advanced: true),
            I("MarkLocationType", nameof(MarkFindSettings.MarkLocationType), "标记定位方式", RunSection, 0, 0, 10, advanced: true),

            // 运行参数 / 高级参数：X/Y 缩放范围（原件 RangScaleX/RangScaleY）
            N("ScaleXStart", nameof(MarkFindSettings.ScaleXStart), "X缩放下限", RunSection, 1, 0.1, 10,
                pair: nameof(MarkFindSettings.ScaleXEnd), pairLabel: "X缩放范围", advanced: false),
            N("ScaleXEnd", nameof(MarkFindSettings.ScaleXEnd), "X缩放上限", RunSection, 1, 0.1, 10, advanced: false),
            N("ScaleYStart", nameof(MarkFindSettings.ScaleYStart), "Y缩放下限", RunSection, 1, 0.1, 10,
                pair: nameof(MarkFindSettings.ScaleYEnd), pairLabel: "Y缩放范围", advanced: false),
            N("ScaleYEnd", nameof(MarkFindSettings.ScaleYEnd), "Y缩放上限", RunSection, 1, 0.1, 10, advanced: false),

            // 结果判断：数量限位
            B("NumLimitEnable", nameof(MarkFindSettings.CountLimitEnabled), "匹配个数判断", JudgeSection, false),
            I("NumLimitLow", nameof(MarkFindSettings.CountLimitLow), "匹配个数下限", JudgeSection, 0, 0, 99_999,
                when: nameof(MarkFindSettings.CountLimitEnabled), pair: nameof(MarkFindSettings.CountLimitHigh), pairLabel: "匹配个数范围"),
            I("NumLimitHigh", nameof(MarkFindSettings.CountLimitHigh), "匹配个数上限", JudgeSection, 99_999, 0, 99_999,
                when: nameof(MarkFindSettings.CountLimitEnabled)),

            // 结果判断：位置/角度/分数限位
            B("AngleLimitEnable", nameof(MarkFindSettings.AngleLimitEnabled), "角度判断", JudgeSection, false, advanced: false),
            N("AngleLimitLow", nameof(MarkFindSettings.AngleLimitLow), "角度下限", JudgeSection, -180, -180, 180,
                when: nameof(MarkFindSettings.AngleLimitEnabled), pair: nameof(MarkFindSettings.AngleLimitHigh), pairLabel: "角度范围", advanced: false),
            N("AngleLimitHigh", nameof(MarkFindSettings.AngleLimitHigh), "角度上限", JudgeSection, 180, -180, 180,
                when: nameof(MarkFindSettings.AngleLimitEnabled), advanced: false),
            // 原厂 Expand（ResultShow 页）收起时隐藏的 5 个判定开关之一。
            B("ScoreLimitEnable", nameof(MarkFindSettings.ScoreLimitEnabled), "分数判断", JudgeSection, false, advanced: true),
            N("ScoreLimitLow", nameof(MarkFindSettings.ScoreLimitLow), "分数下限", JudgeSection, 0, 0, 1,
                when: nameof(MarkFindSettings.ScoreLimitEnabled), pair: nameof(MarkFindSettings.ScoreLimitHigh), pairLabel: "分数范围"),
            N("ScoreLimitHigh", nameof(MarkFindSettings.ScoreLimitHigh), "分数上限", JudgeSection, 1, 0, 1,
                when: nameof(MarkFindSettings.ScoreLimitEnabled)),
            B("ScaleXLimitEnable", nameof(MarkFindSettings.ScaleXLimitEnabled), "X缩放判断", JudgeSection, false, advanced: false),
            N("ScaleXLimitLow", nameof(MarkFindSettings.ScaleXLimitLow), "X缩放下限", JudgeSection, 0.1, 0.1, 10,
                when: nameof(MarkFindSettings.ScaleXLimitEnabled), pair: nameof(MarkFindSettings.ScaleXLimitHigh), pairLabel: "X缩放范围", advanced: false),
            N("ScaleXLimitHigh", nameof(MarkFindSettings.ScaleXLimitHigh), "X缩放上限", JudgeSection, 10, 0.1, 10,
                when: nameof(MarkFindSettings.ScaleXLimitEnabled), advanced: false),
            B("ScaleYLimitEnable", nameof(MarkFindSettings.ScaleYLimitEnabled), "Y缩放判断", JudgeSection, false, advanced: false),
            N("ScaleYLimitLow", nameof(MarkFindSettings.ScaleYLimitLow), "Y缩放下限", JudgeSection, 0.1, 0.1, 10,
                when: nameof(MarkFindSettings.ScaleYLimitEnabled), pair: nameof(MarkFindSettings.ScaleYLimitHigh), pairLabel: "Y缩放范围", advanced: false),
            N("ScaleYLimitHigh", nameof(MarkFindSettings.ScaleYLimitHigh), "Y缩放上限", JudgeSection, 10, 0.1, 10,
                when: nameof(MarkFindSettings.ScaleYLimitEnabled), advanced: false),

            // 结果判断：匹配点/Box 点限位
            B("MatchPointXLimitEnable", nameof(MarkFindSettings.MatchPointXLimitEnabled), "匹配点X判断", JudgeSection, false, advanced: true),
            N("MatchPointXLimitLow", nameof(MarkFindSettings.MatchPointXLimitLow), "匹配点X下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(MarkFindSettings.MatchPointXLimitEnabled), pair: nameof(MarkFindSettings.MatchPointXLimitHigh), pairLabel: "匹配点X范围", advanced: false),
            N("MatchPointXLimitHigh", nameof(MarkFindSettings.MatchPointXLimitHigh), "匹配点X上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(MarkFindSettings.MatchPointXLimitEnabled), advanced: false),
            B("MatchPointYLimitEnable", nameof(MarkFindSettings.MatchPointYLimitEnabled), "匹配点Y判断", JudgeSection, false, advanced: true),
            N("MatchPointYLimitLow", nameof(MarkFindSettings.MatchPointYLimitLow), "匹配点Y下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(MarkFindSettings.MatchPointYLimitEnabled), pair: nameof(MarkFindSettings.MatchPointYLimitHigh), pairLabel: "匹配点Y范围", advanced: false),
            N("MatchPointYLimitHigh", nameof(MarkFindSettings.MatchPointYLimitHigh), "匹配点Y上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(MarkFindSettings.MatchPointYLimitEnabled), advanced: false),
            B("BoxPointXLimitEnable", nameof(MarkFindSettings.BoxPointXLimitEnabled), "Box点X判断", JudgeSection, false, advanced: true),
            N("BoxPointXLimitLow", nameof(MarkFindSettings.BoxPointXLimitLow), "Box点X下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(MarkFindSettings.BoxPointXLimitEnabled), pair: nameof(MarkFindSettings.BoxPointXLimitHigh), pairLabel: "Box点X范围", advanced: false),
            N("BoxPointXLimitHigh", nameof(MarkFindSettings.BoxPointXLimitHigh), "Box点X上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(MarkFindSettings.BoxPointXLimitEnabled), advanced: false),
            B("BoxPointYLimitEnable", nameof(MarkFindSettings.BoxPointYLimitEnabled), "Box点Y判断", JudgeSection, false, advanced: true),
            N("BoxPointYLimitLow", nameof(MarkFindSettings.BoxPointYLimitLow), "Box点Y下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(MarkFindSettings.BoxPointYLimitEnabled), pair: nameof(MarkFindSettings.BoxPointYLimitHigh), pairLabel: "Box点Y范围", advanced: false),
            N("BoxPointYLimitHigh", nameof(MarkFindSettings.BoxPointYLimitHigh), "Box点Y上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(MarkFindSettings.BoxPointYLimitEnabled), advanced: false)
        };
        var names = fields.Select(field => field.Name).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new InvalidOperationException("MarkFind 参数字段名重复。");
        if (fields.Select(field => field.Id).Distinct(StringComparer.Ordinal).Count() != fields.Count)
            throw new InvalidOperationException("MarkFind 参数稳定 ID 重复。");
        if (!Properties.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(names))
            throw new InvalidOperationException("MarkFind 参数表与设置模型不一致。");
        return fields.AsReadOnly();
    }

    public static ToolSchema Schema { get; } = CreateSchema();

    private static ToolSchema CreateSchema()
    {
        var ports = new List<Port>(FieldList.Count);
        var presentations = new List<FieldPresentation>(FieldList.Count);
        foreach (var (field, index) in FieldList.Select((field, index) => (field, index)))
        {
            var choices = field.Choices.Count == 0 ? null : new TextChoiceConstraint(field.Choices);
            ports.Add(new Port(field.Id, PortType(field), EditModes: FieldEditModes.Constant | (field.AllowReference ? FieldEditModes.Reference : FieldEditModes.None), Choices: choices));
            var condition = field.VisibleWhen is { } controller ? new FieldCondition(IdOf(controller), VisibilityValue(Spec(controller))) : null;
            var layout = new FieldLayout(field.Page, field.Section, condition,
                PairWith: field.PairWith is { } partner ? IdOf(partner) : null,
                Advanced: field.Advanced,
                Minimum: field.Minimum, Maximum: field.Maximum,
                DecimalPlaces: field.Type == typeof(string) ? 0 : field.DecimalPlaces,
                AllowReference: field.AllowReference,
                Increment: field.Increment,
                PairMinimum: field.PairWith is null ? null : field.PairMinimum,
                PairMaximum: field.PairWith is null ? null : field.PairMaximum,
                PairDisplayName: field.PairLabel);
            var displayType = DisplayTypeOf(field);
            presentations.Add(new FieldPresentation(field.Id, field.Label, field.Section, index,
                field.Choices.Count == 0 ? FieldEditorKind.Scalar : FieldEditorKind.Choice,
                field.Choices, field.ChoiceLabels, displayType) { Layout = layout });
        }
        return new ToolSchema(ports, Array.Empty<Port>(), new ToolPresentation(presentations));
    }

    /// <summary>
    /// 可见性条件的取值：原件两条联动都是“选择手动才显示压缩比/对比度阈值”，
    /// 因此枚举控制器取 <c>Manual</c>，布尔控制器取 <c>true</c>。
    /// </summary>
    private static object VisibilityValue(MarkFindFieldSpec controller)
        => controller.Type.IsEnum ? "Manual" : true;

    /// <summary>
    /// 原厂 DisplayType：选项→Enumeration；成对→Bettween；布尔→Boolean；文本→String；整数/浮点→Integer/Float。
    /// manifest 复用同一规则，避免两处各写一份。
    /// </summary>
    internal static string DisplayTypeOf(MarkFindFieldSpec field)
        => field.Choices.Count != 0 ? DeclaredFieldTemplateKey.Enumeration
            : field.PairWith is not null
                ? field.Type == typeof(int) ? DeclaredFieldTemplateKey.IntegerBettween : DeclaredFieldTemplateKey.FloatBettween
            : field.Type == typeof(bool) ? DeclaredFieldTemplateKey.Boolean
            : field.Type == typeof(string) ? DeclaredFieldTemplateKey.String
            : field.Type == typeof(int) ? DeclaredFieldTemplateKey.Integer
            : DeclaredFieldTemplateKey.Float;

    internal static TypeRef PortType(MarkFindFieldSpec field)
        => field.Type == typeof(bool) ? DataType.Boolean
            : field.Type == typeof(int) ? DataType.Integer
            : field.Type == typeof(double) ? DataType.Number
            : field.Type.IsEnum ? TypeRef.ScalarOf(ScalarKind.Text)
            : DataType.Text;

    /// <summary>按冻结设置生成规范值；越界或类型不符直接报错。</summary>
    public static InputValues Read(MarkFindSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var values = new Dictionary<string, object?>(FieldList.Count, StringComparer.Ordinal);
        foreach (var field in FieldList) values[field.Id] = ReadField(field, settings);
        return new InputValues(Schema.Inputs, values);
    }

    private static object? ReadField(MarkFindFieldSpec field, MarkFindSettings settings)
    {
        var raw = Properties[field.Name].GetValue(settings);
        object? value = field.Type == typeof(int) ? (long)(int)raw!
            : field.Type.IsEnum ? raw!.ToString()
            : raw;
        return ValueRules.Freeze(Schema.Inputs.Single(port => port.Id == field.Id), value);
    }

    /// <summary>从完整解析值物化设置；不接受缺项，也不从旧设置补默认。</summary>
    public static MarkFindSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var settings = new MarkFindSettings();
        foreach (var field in FieldList) settings = Write(settings, field, values.Get<object?>(field.Id));
        var errors = settings.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return settings.Freeze();
    }

    internal static MarkFindSettings Write(MarkFindSettings settings, MarkFindFieldSpec field, object? value)
    {
        var port = Schema.Inputs.Single(item => item.Id == field.Id);
        ValueRules.Freeze(port, value);
        var property = Properties[field.Name];
        object? converted = field.Type == typeof(int) ? checked((int)(long)value!)
            : field.Type.IsEnum ? Enum.Parse(field.Type, (string)value!, false)
            : value;
        property.SetValue(settings, converted);
        return settings;
    }

    /// <summary>引用字段的绑定记录排序键：使用原厂参数名，保证保存记录的稳定顺序。</summary>
    public static IReadOnlyDictionary<string, string> BindingNames { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
        FieldList.Where(field => field.AllowReference).ToDictionary(field => field.Id, field => field.VirtualMachineName, StringComparer.Ordinal));

    public static string DefaultText(MarkFindFieldSpec field)
        => field.Default switch
        {
            bool flag => flag ? "True" : "False",
            int number => number.ToString(CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(field.Default, CultureInfo.InvariantCulture) ?? ""
        };
}

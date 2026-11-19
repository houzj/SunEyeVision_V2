using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>一个原厂参数在编辑/持久化上的完整声明，不包含执行语义。</summary>
public sealed class ContourMatchFieldSpec
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
/// 轮廓匹配（原件 <c>IMVSContourMatchModu</c>）的参数清单。数据来源：
/// <list type="bullet">
/// <item>模板参数：<c>ModuleControls/IMVSContourMatchModuControl/Apps.XmlParser.ParamTab/HPPyramidFeaturepatMatchDialogData.cs:7-23</c>
/// 字段默认值，控件顺序/类型/范围见 <c>IMVSContourMatchModuControl/hppyramidfeaturepatmatchdialoguistyle.xaml:879-906</c>
/// （压缩比/特征尺度滑条 1..10、边缘阈值滑条 1..255、最小链长滑条 2..1000、四个下拉的选项顺序）。</item>
/// <item>运行/判断参数：<c>docs/07-工具逆向/tools/imvscontourmatchmodu/parameters.json</c> 的 56 项 ParamItem
/// （名称与默认值逐条对应）。</item>
/// </list>
/// 字段 ID 由字段名确定性派生，保存方案时不随版本变化。
/// </summary>
public static class ContourMatchFields
{
    public const string BasicPage = "基本参数";
    public const string TemplatePage = "特征模板";
    public const string RunPage = "运行参数";
    public const string ResultPage = "结果显示";
    public const string ImageInputSection = "Tab_Image Input";
    public const string RoiInputSection = "Tab_ROI Area";
    public const string BasicSection = ImageInputSection;
    public const string TemplateSection = "特征模板";
    public const string RunSection = "运行参数";
    public const string JudgeSection = "结果显示";

    private static readonly IReadOnlyDictionary<string, PropertyInfo> Properties =
        typeof(ContourMatchSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, property => property, StringComparer.Ordinal);
    private static readonly IReadOnlyList<ContourMatchFieldSpec> FieldList = Build();
    private static readonly IReadOnlyDictionary<string, ContourMatchFieldSpec> ByName =
        new Dictionary<string, ContourMatchFieldSpec>(FieldList.ToDictionary(item => item.Name, StringComparer.Ordinal), StringComparer.Ordinal);

    public static IReadOnlyList<ContourMatchFieldSpec> All => FieldList;

    public static string ModelData => IdOf(nameof(ContourMatchSettings.ModelData));
    public static string PyramidMode => IdOf(nameof(ContourMatchSettings.PyramidMode));
    public static string PyramidScaleLevel => IdOf(nameof(ContourMatchSettings.PyramidScaleLevel));
    public static string PyramidScaleRLevel => IdOf(nameof(ContourMatchSettings.PyramidScaleRLevel));
    public static string EdgeThresholdMode => IdOf(nameof(ContourMatchSettings.EdgeThresholdMode));
    public static string EdgeThreshold => IdOf(nameof(ContourMatchSettings.EdgeThreshold));
    public static string ChainMode => IdOf(nameof(ContourMatchSettings.ChainMode));
    public static string MinChainLen => IdOf(nameof(ContourMatchSettings.MinChainLen));
    public static string MaskModel => IdOf(nameof(ContourMatchSettings.MaskModel));
    public static string MinScore => IdOf(nameof(ContourMatchSettings.MinScore));
    public static string MaxMatchCount => IdOf(nameof(ContourMatchSettings.MaxMatchCount));
    public static string Polarity => IdOf(nameof(ContourMatchSettings.Polarity));
    public static string AngleStart => IdOf(nameof(ContourMatchSettings.AngleStart));
    public static string AngleEnd => IdOf(nameof(ContourMatchSettings.AngleEnd));
    public static string ScaleStart => IdOf(nameof(ContourMatchSettings.ScaleStart));
    public static string ScaleEnd => IdOf(nameof(ContourMatchSettings.ScaleEnd));
    public static string ScaleXStart => IdOf(nameof(ContourMatchSettings.ScaleXStart));
    public static string ScaleXEnd => IdOf(nameof(ContourMatchSettings.ScaleXEnd));
    public static string ScaleYStart => IdOf(nameof(ContourMatchSettings.ScaleYStart));
    public static string ScaleYEnd => IdOf(nameof(ContourMatchSettings.ScaleYEnd));
    public static string MaxOverlap => IdOf(nameof(ContourMatchSettings.MaxOverlap));
    public static string MultiModelMaxOverlap => IdOf(nameof(ContourMatchSettings.MultiModelMaxOverlap));
    public static string SortType => IdOf(nameof(ContourMatchSettings.SortType));
    public static string MatchThresholdMode => IdOf(nameof(ContourMatchSettings.MatchThresholdMode));
    public static string MatchThreshold => IdOf(nameof(ContourMatchSettings.MatchThreshold));
    public static string SpotterEnabled => IdOf(nameof(ContourMatchSettings.SpotterEnabled));
    public static string MatchExtentRate => IdOf(nameof(ContourMatchSettings.MatchExtentRate));
    public static string TimeOut => IdOf(nameof(ContourMatchSettings.TimeOut));
    public static string OutlineEnabled => IdOf(nameof(ContourMatchSettings.OutlineEnabled));
    public static string OutlinePointsEnabled => IdOf(nameof(ContourMatchSettings.OutlinePointsEnabled));
    public static string UseMatchAllMode => IdOf(nameof(ContourMatchSettings.UseMatchAllMode));
    public static string RangeScaleType => IdOf(nameof(ContourMatchSettings.RangeScaleType));
    public static string OkWhenCountIsZero => IdOf(nameof(ContourMatchSettings.OkWhenCountIsZero));
    public static string SkewXStart => IdOf(nameof(ContourMatchSettings.SkewXStart));
    public static string SkewXEnd => IdOf(nameof(ContourMatchSettings.SkewXEnd));
    public static string CountLimitEnabled => IdOf(nameof(ContourMatchSettings.CountLimitEnabled));
    public static string CountLimitLow => IdOf(nameof(ContourMatchSettings.CountLimitLow));
    public static string CountLimitHigh => IdOf(nameof(ContourMatchSettings.CountLimitHigh));
    public static string AngleLimitEnabled => IdOf(nameof(ContourMatchSettings.AngleLimitEnabled));
    public static string AngleLimitLow => IdOf(nameof(ContourMatchSettings.AngleLimitLow));
    public static string AngleLimitHigh => IdOf(nameof(ContourMatchSettings.AngleLimitHigh));
    public static string ScaleLimitEnabled => IdOf(nameof(ContourMatchSettings.ScaleLimitEnabled));
    public static string ScaleLimitLow => IdOf(nameof(ContourMatchSettings.ScaleLimitLow));
    public static string ScaleLimitHigh => IdOf(nameof(ContourMatchSettings.ScaleLimitHigh));
    public static string ScaleXLimitEnabled => IdOf(nameof(ContourMatchSettings.ScaleXLimitEnabled));
    public static string ScaleXLimitLow => IdOf(nameof(ContourMatchSettings.ScaleXLimitLow));
    public static string ScaleXLimitHigh => IdOf(nameof(ContourMatchSettings.ScaleXLimitHigh));
    public static string ScaleYLimitEnabled => IdOf(nameof(ContourMatchSettings.ScaleYLimitEnabled));
    public static string ScaleYLimitLow => IdOf(nameof(ContourMatchSettings.ScaleYLimitLow));
    public static string ScaleYLimitHigh => IdOf(nameof(ContourMatchSettings.ScaleYLimitHigh));
    public static string ScoreLimitEnabled => IdOf(nameof(ContourMatchSettings.ScoreLimitEnabled));
    public static string ScoreLimitLow => IdOf(nameof(ContourMatchSettings.ScoreLimitLow));
    public static string ScoreLimitHigh => IdOf(nameof(ContourMatchSettings.ScoreLimitHigh));
    public static string MatchPointXLimitLow => IdOf(nameof(ContourMatchSettings.MatchPointXLimitLow));
    public static string MatchPointXLimitHigh => IdOf(nameof(ContourMatchSettings.MatchPointXLimitHigh));
    public static string MatchPointYLimitLow => IdOf(nameof(ContourMatchSettings.MatchPointYLimitLow));
    public static string MatchPointYLimitHigh => IdOf(nameof(ContourMatchSettings.MatchPointYLimitHigh));
    public static string BoxPointXLimitLow => IdOf(nameof(ContourMatchSettings.BoxPointXLimitLow));
    public static string BoxPointXLimitHigh => IdOf(nameof(ContourMatchSettings.BoxPointXLimitHigh));
    public static string BoxPointYLimitLow => IdOf(nameof(ContourMatchSettings.BoxPointYLimitLow));
    public static string BoxPointYLimitHigh => IdOf(nameof(ContourMatchSettings.BoxPointYLimitHigh));

    public static string IdOf(string name)
        => ByName.TryGetValue(name, out var field) ? field.Id : throw new ArgumentException("未知的 ContourMatch 设置字段：" + name, nameof(name));

    public static ContourMatchFieldSpec Spec(string name)
        => ByName.TryGetValue(name, out var field) ? field : throw new ArgumentException("未知的 ContourMatch 设置字段：" + name, nameof(name));

    public static ContourMatchFieldSpec SpecByVmName(string virtualMachineName)
        => FieldList.FirstOrDefault(field => field.VirtualMachineName == virtualMachineName)
            ?? throw new ArgumentException("无关的原厂参数名：" + virtualMachineName, nameof(virtualMachineName));

    private static string StableId(string name)
        => "field:" + new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("SunEyeVision.Tool.ContourMatch:" + name)).AsSpan(0, 16)).ToString("N");

    private static ContourMatchFieldSpec B(string vm, string name, string label, string section, bool value,
        string? when = null, bool advanced = false, string? page = null)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(bool), Default = value,
            Label = label, Page = page ?? (section == JudgeSection ? ResultPage : RunPage), Section = section, VisibleWhen = when, Advanced = advanced
        };

    private static ContourMatchFieldSpec I(string vm, string name, string label, string section, int value, double minimum, double maximum,
        string? when = null, string? pair = null, string? pairLabel = null, bool advanced = false, bool reference = true,
        string? page = null)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(int), Default = value,
            Label = label, Page = page ?? (section == JudgeSection ? ResultPage : RunPage), Section = section, VisibleWhen = when, PairWith = pair, PairLabel = pairLabel,
            Advanced = advanced, AllowReference = reference, Minimum = minimum, Maximum = maximum,
            PairMinimum = pair is null ? null : minimum, PairMaximum = pair is null ? null : maximum, DecimalPlaces = 0, Increment = 1
        };

    private static ContourMatchFieldSpec N(string vm, string name, string label, string section, double value, double minimum, double maximum,
        string? when = null, string? pair = null, string? pairLabel = null, int decimals = 2, double increment = 0.01,
        bool advanced = false, bool reference = true, string? page = null)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(double), Default = value,
            Label = label, Page = page ?? (section == JudgeSection ? ResultPage : RunPage), Section = section, VisibleWhen = when, PairWith = pair, PairLabel = pairLabel,
            Advanced = advanced, AllowReference = reference, Minimum = minimum, Maximum = maximum,
            PairMinimum = pair is null ? null : minimum, PairMaximum = pair is null ? null : maximum,
            DecimalPlaces = decimals, Increment = increment
        };

    private static ContourMatchFieldSpec E(string vm, string name, string label, string section, Type enumType, int value,
        IEnumerable<string> keys, IEnumerable<string> labels, string? when = null, bool advanced = false, string? page = null)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = enumType,
            Default = Enum.ToObject(enumType, value), Label = label, Page = page ?? (section == JudgeSection ? ResultPage : RunPage), Section = section,
            VisibleWhen = when, Advanced = advanced,
            Choices = keys.ToArray(), ChoiceLabels = keys.Zip(labels).ToDictionary(item => item.First, item => item.Second, StringComparer.Ordinal)
        };

    private static ContourMatchFieldSpec S(string vm, string name, string label, string section, string value, string page)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(string), Default = value,
            Label = label, Page = page, Section = section, DecimalPlaces = 0
        };

    private static IReadOnlyList<ContourMatchFieldSpec> Build()
    {
        var fields = new List<ContourMatchFieldSpec>
        {
            // 模板参数（原件训练对话框「匹配参数」组；全部不走变量引用）
            S("ModelData", nameof(ContourMatchSettings.ModelData), "模板数据", ImageInputSection, "", BasicPage),
            E("PyramidScaleFlag", nameof(ContourMatchSettings.PyramidMode), "金字塔比例", TemplateSection,
                typeof(ContourMatchPyramidMode), 1, Enum.GetNames<ContourMatchPyramidMode>(), new[] { "手动", "自动" }, page: TemplatePage),
            I("PyramidScaleLevel", nameof(ContourMatchSettings.PyramidScaleLevel), "压缩比", TemplateSection, 5, 1, 10,
                when: nameof(ContourMatchSettings.PyramidMode), reference: false, page: TemplatePage),
            I("PyramidScaleRLevel", nameof(ContourMatchSettings.PyramidScaleRLevel), "特征尺度", TemplateSection, 1, 1, 10,
                when: nameof(ContourMatchSettings.PyramidMode), reference: false, page: TemplatePage),
            E("EdgeThresholdFlag", nameof(ContourMatchSettings.EdgeThresholdMode), "阈值模式", TemplateSection,
                typeof(ContourMatchThresholdMode), 1, Enum.GetNames<ContourMatchThresholdMode>(), new[] { "手动", "自动" }, page: TemplatePage),
            I("EdgeThreshold", nameof(ContourMatchSettings.EdgeThreshold), "边缘阈值", TemplateSection, 15, 1, 255,
                when: nameof(ContourMatchSettings.EdgeThresholdMode), reference: false, page: TemplatePage),
            E("ChainFlag", nameof(ContourMatchSettings.ChainMode), "链长模式", TemplateSection,
                typeof(ContourMatchChainMode), 1, Enum.GetNames<ContourMatchChainMode>(), new[] { "手动", "自动" }, page: TemplatePage),
            I("MinChainLen", nameof(ContourMatchSettings.MinChainLen), "最小链长", TemplateSection, 4, 2, 1000,
                when: nameof(ContourMatchSettings.ChainMode), reference: false, page: TemplatePage),
            E("MaskModelFlag", nameof(ContourMatchSettings.MaskModel), "建模类型", TemplateSection,
                typeof(ContourMatchMaskModel), 0, Enum.GetNames<ContourMatchMaskModel>(), new[] { "全轮廓", "外轮廓" }, page: TemplatePage),

            // 运行参数（原件 56 项 ParamItem）
            N("MinScore", nameof(ContourMatchSettings.MinScore), "最小分数", RunSection, 0.5, 0, 1),
            I("MaxMatchNum", nameof(ContourMatchSettings.MaxMatchCount), "最大匹配个数", RunSection, 1, 1, 50_000),
            I("Polarity", nameof(ContourMatchSettings.Polarity), "极性", RunSection, 1, 0, 1),
            I("AngleStart", nameof(ContourMatchSettings.AngleStart), "角度下限", RunSection, -45, -180, 180,
                pair: nameof(ContourMatchSettings.AngleEnd), pairLabel: "角度范围"),
            I("AngleEnd", nameof(ContourMatchSettings.AngleEnd), "角度上限", RunSection, 45, -180, 180),
            N("ScaleStart", nameof(ContourMatchSettings.ScaleStart), "尺度下限", RunSection, 1, 0.1, 10,
                pair: nameof(ContourMatchSettings.ScaleEnd), pairLabel: "尺度范围"),
            N("ScaleEnd", nameof(ContourMatchSettings.ScaleEnd), "尺度上限", RunSection, 1, 0.1, 10),
            N("ScaleXStart", nameof(ContourMatchSettings.ScaleXStart), "X尺度下限", RunSection, 1, 0.1, 10,
                pair: nameof(ContourMatchSettings.ScaleXEnd), pairLabel: "X尺度范围", advanced: true),
            N("ScaleXEnd", nameof(ContourMatchSettings.ScaleXEnd), "X尺度上限", RunSection, 1, 0.1, 10, advanced: true),
            N("ScaleYStart", nameof(ContourMatchSettings.ScaleYStart), "Y尺度下限", RunSection, 1, 0.1, 10,
                pair: nameof(ContourMatchSettings.ScaleYEnd), pairLabel: "Y尺度范围", advanced: true),
            N("ScaleYEnd", nameof(ContourMatchSettings.ScaleYEnd), "Y尺度上限", RunSection, 1, 0.1, 10, advanced: true),
            I("MaxOverlap", nameof(ContourMatchSettings.MaxOverlap), "最大重叠率", RunSection, 50, 0, 100),
            I("MultiModelMaxOverlap", nameof(ContourMatchSettings.MultiModelMaxOverlap), "多模板重叠率", RunSection, 50, 0, 100, advanced: true),
            E("SortType", nameof(ContourMatchSettings.SortType), "排序方式", RunSection, typeof(ContourMatchSortType), 2,
                Enum.GetNames<ContourMatchSortType>(),
                new[] { "分数降序", "角度降序", "X升序", "Y升序", "X升序·Y升序", "Y升序·X升序" }),
            I("MatchThresholdFlag", nameof(ContourMatchSettings.MatchThresholdMode), "阈值类型", RunSection, 0, 0, 2, advanced: true),
            I("MatchThresholdHigh", nameof(ContourMatchSettings.MatchThreshold), "匹配边缘阈值", RunSection, 40, 1, 255, advanced: true),
            B("SpotterFlag", nameof(ContourMatchSettings.SpotterEnabled), "考虑噪点", RunSection, false, advanced: true),
            I("MatchExtentRate", nameof(ContourMatchSettings.MatchExtentRate), "延拓阈值", RunSection, 0, 0, 90, advanced: true),
            I("TimeOut", nameof(ContourMatchSettings.TimeOut), "超时控制", RunSection, 0, 0, 10_000, advanced: true),
            B("OutLineEnable", nameof(ContourMatchSettings.OutlineEnabled), "轮廓使能", RunSection, true),
            B("OutLinePointEnable", nameof(ContourMatchSettings.OutlinePointsEnabled), "轮廓点使能", RunSection, false, advanced: true),
            B("UseMatchAllMode", nameof(ContourMatchSettings.UseMatchAllMode), "全部搜索模式", RunSection, false, advanced: true),
            I("RangeScaleType", nameof(ContourMatchSettings.RangeScaleType), "尺度范围类型", RunSection, 0, 0, 10, advanced: true),
            B("OKWhenNumIsZero", nameof(ContourMatchSettings.OkWhenCountIsZero), "个数为0时判定OK", RunSection, false),
            I("SkewXStart", nameof(ContourMatchSettings.SkewXStart), "起始横切角度", RunSection, 0, -180, 180, advanced: true),
            I("SkewXEnd", nameof(ContourMatchSettings.SkewXEnd), "终止横切角度", RunSection, 0, -180, 180, advanced: true),

            // 结果判断：数量限位
            B("NumLimitEnable", nameof(ContourMatchSettings.CountLimitEnabled), "匹配个数判断", JudgeSection, false, page: ResultPage),
            I("NumLimitLow", nameof(ContourMatchSettings.CountLimitLow), "匹配个数下限", JudgeSection, 0, 0, 99_999,
                when: nameof(ContourMatchSettings.CountLimitEnabled), pair: nameof(ContourMatchSettings.CountLimitHigh), pairLabel: "匹配个数范围", page: ResultPage),
            I("NumLimitHigh", nameof(ContourMatchSettings.CountLimitHigh), "匹配个数上限", JudgeSection, 99_999, 0, 99_999,
                when: nameof(ContourMatchSettings.CountLimitEnabled), page: ResultPage),

            // 结果判断：角度/分数限位
            B("AngleLimitEnable", nameof(ContourMatchSettings.AngleLimitEnabled), "角度判断", JudgeSection, false, advanced: true),
            N("AngleLimitLow", nameof(ContourMatchSettings.AngleLimitLow), "角度下限", JudgeSection, -180, -180, 180,
                when: nameof(ContourMatchSettings.AngleLimitEnabled), pair: nameof(ContourMatchSettings.AngleLimitHigh), pairLabel: "角度范围", advanced: true),
            N("AngleLimitHigh", nameof(ContourMatchSettings.AngleLimitHigh), "角度上限", JudgeSection, 180, -180, 180,
                when: nameof(ContourMatchSettings.AngleLimitEnabled), advanced: true),
            B("ScoreLimitEnable", nameof(ContourMatchSettings.ScoreLimitEnabled), "分数判断", JudgeSection, false),
            N("ScoreLimitLow", nameof(ContourMatchSettings.ScoreLimitLow), "分数下限", JudgeSection, 0, 0, 1,
                when: nameof(ContourMatchSettings.ScoreLimitEnabled), pair: nameof(ContourMatchSettings.ScoreLimitHigh), pairLabel: "分数范围"),
            N("ScoreLimitHigh", nameof(ContourMatchSettings.ScoreLimitHigh), "分数上限", JudgeSection, 1, 0, 1,
                when: nameof(ContourMatchSettings.ScoreLimitEnabled)),

            // 结果判断：尺度限位（等比 / X / Y）
            B("ScaleLimitEnable", nameof(ContourMatchSettings.ScaleLimitEnabled), "尺度判断", JudgeSection, false, advanced: true),
            N("ScaleLimitLow", nameof(ContourMatchSettings.ScaleLimitLow), "尺度下限", JudgeSection, 0.1, 0.1, 10,
                when: nameof(ContourMatchSettings.ScaleLimitEnabled), pair: nameof(ContourMatchSettings.ScaleLimitHigh), pairLabel: "尺度范围", advanced: true),
            N("ScaleLimitHigh", nameof(ContourMatchSettings.ScaleLimitHigh), "尺度上限", JudgeSection, 10, 0.1, 10,
                when: nameof(ContourMatchSettings.ScaleLimitEnabled), advanced: true),
            B("ScaleXLimitEnable", nameof(ContourMatchSettings.ScaleXLimitEnabled), "X尺度判断", JudgeSection, false, advanced: true),
            N("ScaleXLimitLow", nameof(ContourMatchSettings.ScaleXLimitLow), "X尺度下限", JudgeSection, 0.1, 0.1, 10,
                when: nameof(ContourMatchSettings.ScaleXLimitEnabled), pair: nameof(ContourMatchSettings.ScaleXLimitHigh), pairLabel: "X尺度范围", advanced: true),
            N("ScaleXLimitHigh", nameof(ContourMatchSettings.ScaleXLimitHigh), "X尺度上限", JudgeSection, 10, 0.1, 10,
                when: nameof(ContourMatchSettings.ScaleXLimitEnabled), advanced: true),
            B("ScaleYLimitEnable", nameof(ContourMatchSettings.ScaleYLimitEnabled), "Y尺度判断", JudgeSection, false, advanced: true),
            N("ScaleYLimitLow", nameof(ContourMatchSettings.ScaleYLimitLow), "Y尺度下限", JudgeSection, 0.1, 0.1, 10,
                when: nameof(ContourMatchSettings.ScaleYLimitEnabled), pair: nameof(ContourMatchSettings.ScaleYLimitHigh), pairLabel: "Y尺度范围", advanced: true),
            N("ScaleYLimitHigh", nameof(ContourMatchSettings.ScaleYLimitHigh), "Y尺度上限", JudgeSection, 10, 0.1, 10,
                when: nameof(ContourMatchSettings.ScaleYLimitEnabled), advanced: true),

            // 结果判断：匹配点 / Box 点限位
            B("MatchPointXLimitEnable", nameof(ContourMatchSettings.MatchPointXLimitEnabled), "匹配点X判断", JudgeSection, false, advanced: true),
            N("MatchPointXLimitLow", nameof(ContourMatchSettings.MatchPointXLimitLow), "匹配点X下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(ContourMatchSettings.MatchPointXLimitEnabled), pair: nameof(ContourMatchSettings.MatchPointXLimitHigh), pairLabel: "匹配点X范围", advanced: true),
            N("MatchPointXLimitHigh", nameof(ContourMatchSettings.MatchPointXLimitHigh), "匹配点X上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(ContourMatchSettings.MatchPointXLimitEnabled), advanced: true),
            B("MatchPointYLimitEnable", nameof(ContourMatchSettings.MatchPointYLimitEnabled), "匹配点Y判断", JudgeSection, false, advanced: true),
            N("MatchPointYLimitLow", nameof(ContourMatchSettings.MatchPointYLimitLow), "匹配点Y下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(ContourMatchSettings.MatchPointYLimitEnabled), pair: nameof(ContourMatchSettings.MatchPointYLimitHigh), pairLabel: "匹配点Y范围", advanced: true),
            N("MatchPointYLimitHigh", nameof(ContourMatchSettings.MatchPointYLimitHigh), "匹配点Y上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(ContourMatchSettings.MatchPointYLimitEnabled), advanced: true),
            B("BoxPointXLimitEnable", nameof(ContourMatchSettings.BoxPointXLimitEnabled), "Box点X判断", JudgeSection, false, advanced: true),
            N("BoxPointXLimitLow", nameof(ContourMatchSettings.BoxPointXLimitLow), "Box点X下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(ContourMatchSettings.BoxPointXLimitEnabled), pair: nameof(ContourMatchSettings.BoxPointXLimitHigh), pairLabel: "Box点X范围", advanced: true),
            N("BoxPointXLimitHigh", nameof(ContourMatchSettings.BoxPointXLimitHigh), "Box点X上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(ContourMatchSettings.BoxPointXLimitEnabled), advanced: true),
            B("BoxPointYLimitEnable", nameof(ContourMatchSettings.BoxPointYLimitEnabled), "Box点Y判断", JudgeSection, false, advanced: true),
            N("BoxPointYLimitLow", nameof(ContourMatchSettings.BoxPointYLimitLow), "Box点Y下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(ContourMatchSettings.BoxPointYLimitEnabled), pair: nameof(ContourMatchSettings.BoxPointYLimitHigh), pairLabel: "Box点Y范围", advanced: true),
            N("BoxPointYLimitHigh", nameof(ContourMatchSettings.BoxPointYLimitHigh), "Box点Y上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(ContourMatchSettings.BoxPointYLimitEnabled), advanced: true)
        };
        var names = fields.Select(field => field.Name).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new InvalidOperationException("ContourMatch 参数字段名重复。");
        if (fields.Select(field => field.Id).Distinct(StringComparer.Ordinal).Count() != fields.Count)
            throw new InvalidOperationException("ContourMatch 参数稳定 ID 重复。");
        if (!Properties.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(names))
            throw new InvalidOperationException("ContourMatch 参数表与设置模型不一致。");
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
            var presentationOrder = field.Page == ResultPage ? 10_000 + index : index;
            presentations.Add(new FieldPresentation(field.Id, field.Label, field.Section, presentationOrder,
                field.Choices.Count == 0 ? FieldEditorKind.Scalar : FieldEditorKind.Choice,
                field.Choices, field.ChoiceLabels) { Layout = layout });
        }
        return new ToolSchema(ports, Array.Empty<Port>(), new ToolPresentation(presentations));
    }

    /// <summary>
    /// 可见性条件的取值：三个模式下拉都是“选手动才显示跟随数值”，枚举控制器取 <c>Manual</c>。
    /// </summary>
    private static object VisibilityValue(ContourMatchFieldSpec controller)
        => controller.Type.IsEnum ? "Manual" : true;

    private static TypeRef PortType(ContourMatchFieldSpec field)
        => field.Type == typeof(bool) ? DataType.Boolean
            : field.Type == typeof(int) ? DataType.Integer
            : field.Type == typeof(double) ? DataType.Number
            : field.Type.IsEnum ? TypeRef.ScalarOf(ScalarKind.Text)
            : DataType.Text;

    /// <summary>按冻结设置生成规范值；越界或类型不符直接报错。</summary>
    public static InputValues Read(ContourMatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var values = new Dictionary<string, object?>(FieldList.Count, StringComparer.Ordinal);
        foreach (var field in FieldList) values[field.Id] = ReadField(field, settings);
        return new InputValues(Schema.Inputs, values);
    }

    private static object? ReadField(ContourMatchFieldSpec field, ContourMatchSettings settings)
    {
        var raw = Properties[field.Name].GetValue(settings);
        object? value = field.Type == typeof(int) ? (long)(int)raw!
            : field.Type.IsEnum ? raw!.ToString()
            : raw;
        return ValueRules.Freeze(Schema.Inputs.Single(port => port.Id == field.Id), value);
    }

    /// <summary>从完整解析值物化设置；不接受缺项，也不从旧设置补默认。</summary>
    public static ContourMatchSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var settings = new ContourMatchSettings();
        foreach (var field in FieldList) settings = Write(settings, field, values.Get<object?>(field.Id));
        var errors = settings.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return settings.Freeze();
    }

    internal static ContourMatchSettings Write(ContourMatchSettings settings, ContourMatchFieldSpec field, object? value)
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

    public static string DefaultText(ContourMatchFieldSpec field)
        => field.Default switch
        {
            bool flag => flag ? "True" : "False",
            int number => number.ToString(CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(field.Default, CultureInfo.InvariantCulture) ?? ""
        };
}


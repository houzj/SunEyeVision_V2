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
    public FieldDefaultPresence DefaultPresence { get; init; } = FieldDefaultPresence.Unset;
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
    /// <summary>结果页分组：原厂键 Tab_Check Result → 中文"结果判断"（zh-cn.xaml），不是页签名"结果显示"。</summary>
    public const string JudgeSection = "Tab_Check Result";

    private static readonly IReadOnlyDictionary<string, PropertyInfo> Properties =
        typeof(ContourMatchSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, property => property, StringComparer.Ordinal);
    private static readonly IReadOnlyList<ContourMatchFieldSpec> FieldList = Build();
    private static readonly IReadOnlyDictionary<string, ContourMatchFieldSpec> ByName =
        new Dictionary<string, ContourMatchFieldSpec>(FieldList.ToDictionary(item => item.Name, StringComparer.Ordinal), StringComparer.Ordinal);

    public static IReadOnlyList<ContourMatchFieldSpec> All => FieldList;

    public static string ModelData => IdOf(nameof(ContourMatchSettings.ModelData));
    public static string TrainingRois => IdOf(nameof(ContourMatchSettings.TrainingRois));
    public static string PyramidMode => IdOf(nameof(ContourMatchSettings.PyramidMode));
    public static string PyramidScaleLevel => IdOf(nameof(ContourMatchSettings.PyramidScaleLevel));
    public static string PyramidScaleRLevel => IdOf(nameof(ContourMatchSettings.PyramidScaleRLevel));
    public static string EdgeThresholdMode => IdOf(nameof(ContourMatchSettings.EdgeThresholdMode));
    public static string EdgeThreshold => IdOf(nameof(ContourMatchSettings.EdgeThreshold));
    public static string MinContrast => IdOf(nameof(ContourMatchSettings.MinContrast));
    public static string Optimization => IdOf(nameof(ContourMatchSettings.Optimization));
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
    public static string Greediness => IdOf(nameof(ContourMatchSettings.Greediness));
    public static string SubPixel => IdOf(nameof(ContourMatchSettings.SubPixel));
    public static string MatchExtentRate => IdOf(nameof(ContourMatchSettings.MatchExtentRate));
    public static string TimeOut => IdOf(nameof(ContourMatchSettings.TimeOut));
    public static string OutlineEnabled => IdOf(nameof(ContourMatchSettings.OutlineEnabled));
    public static string OutlinePointsEnabled => IdOf(nameof(ContourMatchSettings.OutlinePointsEnabled));
    public static string UseMatchAllMode => IdOf(nameof(ContourMatchSettings.UseMatchAllMode));
    public static string RangeScaleType => IdOf(nameof(ContourMatchSettings.RangeScaleType));
    public static string OkWhenCountIsZero => IdOf(nameof(ContourMatchSettings.OkWhenCountIsZero));
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

    /// <summary>按字段标识（field:GUID）查声明；界面侧拿到的是 Id 而不是属性名。</summary>
    public static ContourMatchFieldSpec SpecById(string id)
        => FieldList.FirstOrDefault(field => field.Id == id)
            ?? throw new ArgumentException("未知的 ContourMatch 字段标识：" + id, nameof(id));

    private static string StableId(string name)
        => "field:" + new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("SunEyeVision.Tool.ContourMatch:" + name)).AsSpan(0, 16)).ToString("N");

    private static ContourMatchFieldSpec B(string vm, string name, string label, string section, bool value,
        string? when = null, bool advanced = false, string? page = null)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(bool), Default = value,
            Label = label, Page = page ?? (section == JudgeSection ? ResultPage : RunPage), Section = section, VisibleWhen = when, Advanced = advanced,
            // 原厂 parameters.json 中这些开关均声明 DefaultValue（False/True），默认界面呈现该值。
            // 本方缺项时须用声明默认值补齐，否则 UnsetSetting + Required 会让整行启动报红。
            // 证据：docs/07-工具逆向/tools/imvscontourmatchmodu/parameters.json:985-987 等 8 项。
            DefaultPresence = FieldDefaultPresence.UseDeclaredDefault
        };

    private static ContourMatchFieldSpec I(string vm, string name, string label, string section, int value, double minimum, double maximum,
        string? when = null, string? pair = null, string? pairLabel = null, bool advanced = false, bool reference = true,
        string? page = null)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(int), Default = value,
            Label = label, Page = page ?? (section == JudgeSection ? ResultPage : RunPage), Section = section, VisibleWhen = when, PairWith = pair, PairLabel = pairLabel,
            Advanced = advanced, AllowReference = reference, Minimum = minimum, Maximum = maximum,
            PairMinimum = pair is null ? null : minimum, PairMaximum = pair is null ? null : maximum, DecimalPlaces = 0, Increment = 1,
            DefaultPresence = FieldDefaultPresence.UseDeclaredDefault
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
            DecimalPlaces = decimals, Increment = increment,
            DefaultPresence = FieldDefaultPresence.UseDeclaredDefault
        };

    private static ContourMatchFieldSpec E(string vm, string name, string label, string section, Type enumType, int value,
        IEnumerable<string> keys, IEnumerable<string> labels, string? when = null, bool advanced = false, string? page = null)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = enumType,
            Default = Enum.ToObject(enumType, value), Label = label, Page = page ?? (section == JudgeSection ? ResultPage : RunPage), Section = section,
            VisibleWhen = when, Advanced = advanced,
            Choices = keys.ToArray(), ChoiceLabels = keys.Zip(labels).ToDictionary(item => item.First, item => item.Second, StringComparer.Ordinal),
            DefaultPresence = FieldDefaultPresence.UseDeclaredDefault
        };

    private static ContourMatchFieldSpec S(string vm, string name, string label, string section, string value, string page)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(string), Default = value,
            Label = label, Page = page, Section = section, DecimalPlaces = 0,
            // 新建节点时该字段尚无值，必须按声明默认值补齐；否则物化设置直接报
            // "Missing required port field:..."，表现为点“生成模板”无反应。
            DefaultPresence = FieldDefaultPresence.UseDeclaredDefault
        };

    private static ContourMatchFieldSpec D(string vm, string name, string label, string section, string page)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(RecordValue),
            Default = RoiDocumentValue.Empty, Label = label, Page = page, Section = section, DecimalPlaces = 0,
            DefaultPresence = FieldDefaultPresence.UseDeclaredDefault
        };

    private static IReadOnlyList<ContourMatchFieldSpec> Build()
    {
        var fields = new List<ContourMatchFieldSpec>
        {
            // 模板参数（原件训练对话框「匹配参数」组；全部不走变量引用）
            S("ModelData", nameof(ContourMatchSettings.ModelData), "模板数据", ImageInputSection, "", BasicPage),
            D("TrainingRois", nameof(ContourMatchSettings.TrainingRois), "训练区域", TemplateSection, TemplatePage),
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
            // HALCON 原生创建参数：库链长/建模类型在 HALCON 形状模板中不存在，已移除；
            // 改由 min_contrast（忽略低于此对比度的边缘）与 optimization（轮廓点约简）开放给用户。
            I("MinContrast", nameof(ContourMatchSettings.MinContrast), "最小对比度", TemplateSection, 5, 1, 255,
                reference: false, page: TemplatePage),
            E("Optimization", nameof(ContourMatchSettings.Optimization), "轮廓优化", TemplateSection,
                typeof(ContourMatchOptimization), 0, Enum.GetNames<ContourMatchOptimization>(),
                new[] { "自动", "不优化", "低", "中", "高" }, page: TemplatePage),

            // 运行参数（原件 56 项 ParamItem）
            // 原厂首屏顺序：全部搜索模式 → 最小匹配分数 → 最大匹配个数 → 匹配极性 → 角度范围 → 尺度范围类型 → 尺度范围。
            B("UseMatchAllMode", nameof(ContourMatchSettings.UseMatchAllMode), "全部搜索模式", RunSection, false),
            // 原厂 AlgorithmTab.xml：MultiModelMaxOverlap 紧跟全部搜索模式，默认 CustomVisible=False，
            // 由 UseMatchAllMode 的 Triggers(CurValue=True→Visible) 控制显示（NMS Overlap Threshold）。
            I("MultiModelMaxOverlap", nameof(ContourMatchSettings.MultiModelMaxOverlap), "多模板重叠率", RunSection, 50, 0, 100,
                when: nameof(ContourMatchSettings.UseMatchAllMode)),
            // 原厂 AlgorithmTab.xml MinScore/DefaultValue=0.8（Min Match Score），范围 0~1 步长 0.01。
            N("MinScore", nameof(ContourMatchSettings.MinScore), "最小匹配分数", RunSection, 0.8, 0, 1),
            I("MaxMatchNum", nameof(ContourMatchSettings.MaxMatchCount), "最大匹配个数", RunSection, 1, 1, 50_000),
            // 原厂标签「匹配极性」。算法层限制：极性在 HALCON 模板创建期固定，运行期只支持 1（考虑极性），
            // 故保持整数显示而非枚举（枚举会误导用户以为可切换）——属算法约束，非 UI 可修复差异。
            I("Polarity", nameof(ContourMatchSettings.Polarity), "匹配极性", RunSection, 1, 0, 1),
            I("AngleStart", nameof(ContourMatchSettings.AngleStart), "角度下限", RunSection, -45, -180, 180,
                pair: nameof(ContourMatchSettings.AngleEnd), pairLabel: "角度范围"),
            I("AngleEnd", nameof(ContourMatchSettings.AngleEnd), "角度上限", RunSection, 45, -180, 180),
            // 原厂 AlgorithmTab.xml RangeScaleType：EnumEntrys 尺度范围/尺度范围XY，=XY 时显示 X/Y 尺度范围。
            E("RangeScaleType", nameof(ContourMatchSettings.RangeScaleType), "尺度范围类型", RunSection,
                typeof(ContourMatchRangeScaleType), 0, Enum.GetNames<ContourMatchRangeScaleType>(), new[] { "尺度范围", "尺度范围XY" }),
            N("ScaleStart", nameof(ContourMatchSettings.ScaleStart), "尺度下限", RunSection, 1, 0.1, 10,
                pair: nameof(ContourMatchSettings.ScaleEnd), pairLabel: "尺度范围"),
            N("ScaleEnd", nameof(ContourMatchSettings.ScaleEnd), "尺度上限", RunSection, 1, 0.1, 10),
            // 原厂 AlgorithmTab.xml：RangScaleX/Y 默认 CustomVisible=False，由 RangeScaleType=XY 触发显示。
            N("ScaleXStart", nameof(ContourMatchSettings.ScaleXStart), "X尺度下限", RunSection, 1, 0.1, 10,
                when: nameof(ContourMatchSettings.RangeScaleType) + "=XY",
                pair: nameof(ContourMatchSettings.ScaleXEnd), pairLabel: "X尺度范围", advanced: true),
            N("ScaleXEnd", nameof(ContourMatchSettings.ScaleXEnd), "X尺度上限", RunSection, 1, 0.1, 10,
                when: nameof(ContourMatchSettings.RangeScaleType) + "=XY", advanced: true),
            N("ScaleYStart", nameof(ContourMatchSettings.ScaleYStart), "Y尺度下限", RunSection, 1, 0.1, 10,
                when: nameof(ContourMatchSettings.RangeScaleType) + "=XY",
                pair: nameof(ContourMatchSettings.ScaleYEnd), pairLabel: "Y尺度范围", advanced: true),
            N("ScaleYEnd", nameof(ContourMatchSettings.ScaleYEnd), "Y尺度上限", RunSection, 1, 0.1, 10,
                when: nameof(ContourMatchSettings.RangeScaleType) + "=XY", advanced: true),
            // 横切角度（SkewX）在 HALCON 形状模板中无对应语义，已移除。
            I("MaxOverlap", nameof(ContourMatchSettings.MaxOverlap), "最大重叠率", RunSection, 50, 0, 100, advanced: true),
            E("SortType", nameof(ContourMatchSettings.SortType), "排序类型", RunSection, typeof(ContourMatchSortType), 2,
                Enum.GetNames<ContourMatchSortType>(),
                new[] { "按分数降序排序", "按角度降序排序", "按X升序排序", "按Y升序排序", "按X升序·Y升序排序", "按Y升序·X升序排序" }, advanced: true),
            // 运行期阈值类型/边缘阈值在 HALCON 中不可切换（模型创建期固定），已移除；
            // 创建期阈值由模板页「边缘阈值」「最小对比度」承担。
            // 新增 HALCON 匹配参数：贪婪度与亚像素模式。
            N("Greediness", nameof(ContourMatchSettings.Greediness), "贪婪度", RunSection, 0.9, 0, 1, advanced: true),
            E("SubPixel", nameof(ContourMatchSettings.SubPixel), "亚像素模式", RunSection,
                typeof(ContourMatchSubPixel), 2, Enum.GetNames<ContourMatchSubPixel>(),
                new[] { "无", "插值", "最小二乘" }, advanced: true),
            I("MatchExtentRate", nameof(ContourMatchSettings.MatchExtentRate), "延拓阈值", RunSection, 0, 0, 90, advanced: true),
            I("TimeOut", nameof(ContourMatchSettings.TimeOut), "超时控制", RunSection, 0, 0, 10_000, advanced: true),
            B("OutLineEnable", nameof(ContourMatchSettings.OutlineEnabled), "是否获取轮廓", RunSection, true, advanced: true),
            // 原厂 AlgorithmTab.xml：OutLineEnable 的 Triggers(CurValue=False→OutLinePointEnable Hidden)，
            // 即关闭"是否获取轮廓"时隐藏"是否输出轮廓"。
            B("OutLinePointEnable", nameof(ContourMatchSettings.OutlinePointsEnabled), "是否输出轮廓", RunSection, false,
                when: nameof(ContourMatchSettings.OutlineEnabled), advanced: true),
            // 原厂 ResultShow 页首项「忽略匹配个数」（OKWhenNumIsZero，AlgorithmTab.xml:596-603），属结果判断。
            B("OKWhenNumIsZero", nameof(ContourMatchSettings.OkWhenCountIsZero), "忽略匹配个数", JudgeSection, false, page: ResultPage),

            // 结果判断：数量限位
            B("NumLimitEnable", nameof(ContourMatchSettings.CountLimitEnabled), "数量判断", JudgeSection, false, page: ResultPage),
            I("NumLimitLow", nameof(ContourMatchSettings.CountLimitLow), "匹配个数下限", JudgeSection, 0, 0, 99_999,
                when: nameof(ContourMatchSettings.CountLimitEnabled), pair: nameof(ContourMatchSettings.CountLimitHigh), pairLabel: "匹配个数范围", page: ResultPage),
            I("NumLimitHigh", nameof(ContourMatchSettings.CountLimitHigh), "匹配个数上限", JudgeSection, 99_999, 0, 99_999,
                when: nameof(ContourMatchSettings.CountLimitEnabled), page: ResultPage),

            // 结果判断：角度/分数限位（原厂 ResultShow 首屏四项判断开关之一，不折叠）
            B("AngleLimitEnable", nameof(ContourMatchSettings.AngleLimitEnabled), "角度判断", JudgeSection, false),
            // 原厂 AlgorithmTab.xml RangeAngleLimit/DefaultValue=0（范围仍为 -180~180）。
            N("AngleLimitLow", nameof(ContourMatchSettings.AngleLimitLow), "角度下限", JudgeSection, 0, -180, 180,
                when: nameof(ContourMatchSettings.AngleLimitEnabled), pair: nameof(ContourMatchSettings.AngleLimitHigh), pairLabel: "角度范围", advanced: true),
            N("AngleLimitHigh", nameof(ContourMatchSettings.AngleLimitHigh), "角度上限", JudgeSection, 180, -180, 180,
                when: nameof(ContourMatchSettings.AngleLimitEnabled), advanced: true),
            // 结果判断：尺度限位（等比 / X / Y），主开关原厂 ResultShow 首屏直接可见
            B("ScaleLimitEnable", nameof(ContourMatchSettings.ScaleLimitEnabled), "尺度判断", JudgeSection, false),
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

            // 原厂 ResultShow 顺序：ScaleY(918) → Score(931) → MatchPointX(994)。
            B("ScoreLimitEnable", nameof(ContourMatchSettings.ScoreLimitEnabled), "分数判断", JudgeSection, false, advanced: true),
            // 范围必须与判断同属高级分组（advanced），否则会被排到收起区之后、错误地显示在尺度判断下面。
            N("ScoreLimitLow", nameof(ContourMatchSettings.ScoreLimitLow), "分数下限", JudgeSection, 0, 0, 1,
                when: nameof(ContourMatchSettings.ScoreLimitEnabled), pair: nameof(ContourMatchSettings.ScoreLimitHigh), pairLabel: "分数范围", advanced: true),
            N("ScoreLimitHigh", nameof(ContourMatchSettings.ScoreLimitHigh), "分数上限", JudgeSection, 1, 0, 1,
                when: nameof(ContourMatchSettings.ScoreLimitEnabled), advanced: true),

            // 结果判断：匹配点 / 中心点限位。原厂 BoxPoint* 的界面标签是 "Central Point * Check/Range"，
            // 中文为"中心点X/Y判断、中心点X/Y范围"（decompiled/Lang/zh-cn.xaml:3379-3386）。
            // 原厂 AlgorithmTab.xml Range*Limit 的 DefaultValue 均为 0，MinValue -99999（范围下限仍可到 -99999）。
            B("MatchPointXLimitEnable", nameof(ContourMatchSettings.MatchPointXLimitEnabled), "匹配点X判断", JudgeSection, false, advanced: true),
            N("MatchPointXLimitLow", nameof(ContourMatchSettings.MatchPointXLimitLow), "匹配点X下限", JudgeSection, 0, -99_999, 99_999,
                when: nameof(ContourMatchSettings.MatchPointXLimitEnabled), pair: nameof(ContourMatchSettings.MatchPointXLimitHigh), pairLabel: "匹配点X范围", advanced: true),
            N("MatchPointXLimitHigh", nameof(ContourMatchSettings.MatchPointXLimitHigh), "匹配点X上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(ContourMatchSettings.MatchPointXLimitEnabled), advanced: true),
            B("MatchPointYLimitEnable", nameof(ContourMatchSettings.MatchPointYLimitEnabled), "匹配点Y判断", JudgeSection, false, advanced: true),
            N("MatchPointYLimitLow", nameof(ContourMatchSettings.MatchPointYLimitLow), "匹配点Y下限", JudgeSection, 0, -99_999, 99_999,
                when: nameof(ContourMatchSettings.MatchPointYLimitEnabled), pair: nameof(ContourMatchSettings.MatchPointYLimitHigh), pairLabel: "匹配点Y范围", advanced: true),
            N("MatchPointYLimitHigh", nameof(ContourMatchSettings.MatchPointYLimitHigh), "匹配点Y上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(ContourMatchSettings.MatchPointYLimitEnabled), advanced: true),
            B("BoxPointXLimitEnable", nameof(ContourMatchSettings.BoxPointXLimitEnabled), "中心点X判断", JudgeSection, false, advanced: true),
            N("BoxPointXLimitLow", nameof(ContourMatchSettings.BoxPointXLimitLow), "中心点X下限", JudgeSection, 0, -99_999, 99_999,
                when: nameof(ContourMatchSettings.BoxPointXLimitEnabled), pair: nameof(ContourMatchSettings.BoxPointXLimitHigh), pairLabel: "中心点X范围", advanced: true),
            N("BoxPointXLimitHigh", nameof(ContourMatchSettings.BoxPointXLimitHigh), "中心点X上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(ContourMatchSettings.BoxPointXLimitEnabled), advanced: true),
            B("BoxPointYLimitEnable", nameof(ContourMatchSettings.BoxPointYLimitEnabled), "中心点Y判断", JudgeSection, false, advanced: true),
            N("BoxPointYLimitLow", nameof(ContourMatchSettings.BoxPointYLimitLow), "中心点Y下限", JudgeSection, 0, -99_999, 99_999,
                when: nameof(ContourMatchSettings.BoxPointYLimitEnabled), pair: nameof(ContourMatchSettings.BoxPointYLimitHigh), pairLabel: "中心点Y范围", advanced: true),
            N("BoxPointYLimitHigh", nameof(ContourMatchSettings.BoxPointYLimitHigh), "中心点Y上限", JudgeSection, 99_999, -99_999, 99_999,
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
            ports.Add(new Port(field.Id, PortType(field), EditModes: FieldEditModes.Constant | (field.AllowReference ? FieldEditModes.Reference : FieldEditModes.None),
                DefaultPresence: field.DefaultPresence, Choices: choices));
            // 训练样本是 RecordValue 文档，由工具专用样本面板读写；通用字段行只渲染 Rgba，
            // 不给它生成 presentation（与 Blob/PixelCount 的 ROI 文档字段同一做法），避免建行时抛异常。
            if (field.Name == nameof(ContourMatchSettings.TrainingRois)) continue;
            // 原厂这些字段是条件触发才显示（RangeScaleType=XY / UseMatchAllMode / 阈值类型=手动），
            // 已按 AlgorithmTab.xml 的 Triggers 建行并用 VisibleWhen 控制显隐，不再整体跳过。
            var condition = BuildCondition(field.VisibleWhen);
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
            // 结果显示页的判定行排在图像显示之前（显示行固定 10000+，见 ContourMatchResultDisplay.Build），
            // 对齐原厂 ResultShow 先「结果判断」后「图像显示」的分组顺序。
            var presentationOrder = index;
            var editor = field.Type == typeof(RecordValue) ? FieldEditorKind.Record
                : field.Choices.Count == 0 ? FieldEditorKind.Scalar : FieldEditorKind.Choice;
            presentations.Add(new FieldPresentation(field.Id, field.Label, field.Section, presentationOrder,
                editor, field.Choices, field.ChoiceLabels) { Layout = layout });
        }
        return new ToolSchema(ports, Array.Empty<Port>(), new ToolPresentation(presentations));
    }

    /// <summary>
    /// 可见性条件的取值：三个模式下拉都是“选手动才显示跟随数值”，枚举控制器取 <c>Manual</c>。
    /// </summary>
    /// <summary>
    /// 解析 VisibleWhen：支持 "Field"（旧写法，枚举取 Manual、布尔取 true）与 "Field=Value"。
    /// 枚举字段的值以字符串存储（Port 类型为 Text），布尔为 bool，整数为 long。
    /// </summary>
    private static FieldCondition? BuildCondition(string? visibleWhen)
    {
        if (string.IsNullOrWhiteSpace(visibleWhen)) return null;
        var parts = visibleWhen.Split('=', 2);
        var controller = Spec(parts[0].Trim());
        if (parts.Length == 1)
            return new FieldCondition(IdOf(controller.Name), controller.Type.IsEnum ? "Manual" : true);
        var raw = parts[1].Trim();
        object value = controller.Type == typeof(bool) ? bool.Parse(raw)
            : controller.Type.IsEnum ? raw
            : controller.Type == typeof(int) ? long.Parse(raw, System.Globalization.CultureInfo.InvariantCulture)
            : throw new ArgumentException("VisibleWhen 只能引用布尔、整数或枚举字段：" + controller.Name);
        return new FieldCondition(IdOf(controller.Name), value);
    }

    private static TypeRef PortType(ContourMatchFieldSpec field)
        => field.Type == typeof(bool) ? DataType.Boolean
            : field.Type == typeof(int) ? DataType.Integer
            : field.Type == typeof(double) ? DataType.Number
            : field.Type == typeof(RecordValue) ? RoiDocumentValue.Declaration
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

    /// <summary>从完整解析值物化设置；字段缺项由 Port.DefaultPresence=UseDeclaredDefault 在加载时补齐，此处不接受缺项。</summary>
    public static ContourMatchSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var settings = new ContourMatchSettings();
        foreach (var field in FieldList)
        {
            var value = values.Get<object?>(field.Id);
            settings = Write(settings, field, value);
        }
        var errors = settings.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return settings.Freeze();
    }

    internal static ContourMatchSettings Write(ContourMatchSettings settings, ContourMatchFieldSpec field, object? value)
    {
        var port = Schema.Inputs.Single(item => item.Id == field.Id);
        ValueRules.Freeze(port, value);
        if (field.Type == typeof(RecordValue))
        {
            if (value is not RecordValue document || document.Declaration != RoiDocumentValue.Declaration)
                throw new ArgumentException("训练区域必须是有效的区域文档。");
            ContourMatchTrainingRois.Validate(document);
        }
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




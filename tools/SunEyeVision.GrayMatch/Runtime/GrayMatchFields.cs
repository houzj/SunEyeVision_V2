using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.GrayMatch.Runtime;

/// <summary>一个原厂参数在编辑/持久化上的完整声明，不包含执行语义。</summary>
public sealed class GrayMatchFieldSpec
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
/// 灰度匹配（原件 <c>IMVSGrayMatchModuVA</c>，工具箱显示名「灰度匹配」）的参数清单。数据来源：
/// <list type="bullet">
/// <item>模板参数：<c>ModuleControls/IMVSGrayMatchModuVAControl/Apps.XmlParser.ParamTab/GrayMatchVADialogData.cs:7-15</c>
/// 字段默认值；控件顺序/类型/范围见 <c>IMVSGrayMatchModuVAControl/graymatchvadialoguistyle.xaml:860-872</c>
/// （层数 1..8 仅手动可用、角度步长 1..50、起始/终止角度 -180..180 互相夹取），
/// 中文标签取自 <c>decompiled/Lang/zh-cn.xaml:6113-6114、:6276、:3173、:3206</c>。</item>
/// <item>运行/判断参数：<c>docs/07-工具逆向/tools/imvsgraymatchmoduva/parameters.json</c> 的 **33 项** ParamItem
/// （VA 版＝工具箱条目；旧版 <c>imvsgraymatchmodu</c> 的 31 项不作为默认值来源）。</item>
/// </list>
/// 字段 ID 由字段名确定性派生，保存方案时不随版本变化。
/// </summary>
public static class GrayMatchFields
{
    public const string TemplatePage = "模板参数";
    public const string RunPage = "运行参数";
    public const string TemplateSection = "模板参数";
    public const string RunSection = "运行参数";
    public const string JudgeSection = "结果判断";

    private static readonly IReadOnlyDictionary<string, PropertyInfo> Properties =
        typeof(GrayMatchSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, property => property, StringComparer.Ordinal);
    private static readonly IReadOnlyList<GrayMatchFieldSpec> FieldList = Build();
    private static readonly IReadOnlyDictionary<string, GrayMatchFieldSpec> ByName =
        new Dictionary<string, GrayMatchFieldSpec>(FieldList.ToDictionary(item => item.Name, StringComparer.Ordinal), StringComparer.Ordinal);

    public static IReadOnlyList<GrayMatchFieldSpec> All => FieldList;

    public static string ModelData => IdOf(nameof(GrayMatchSettings.ModelData));
    public static string PyramidMode => IdOf(nameof(GrayMatchSettings.PyramidMode));
    public static string PyramidLevel => IdOf(nameof(GrayMatchSettings.PyramidLevel));
    public static string ModelAngleStep => IdOf(nameof(GrayMatchSettings.ModelAngleStep));
    public static string ModelAngleStart => IdOf(nameof(GrayMatchSettings.ModelAngleStart));
    public static string ModelAngleEnd => IdOf(nameof(GrayMatchSettings.ModelAngleEnd));
    public static string MinScore => IdOf(nameof(GrayMatchSettings.MinScore));
    public static string MaxMatchCount => IdOf(nameof(GrayMatchSettings.MaxMatchCount));
    public static string MaxOverlap => IdOf(nameof(GrayMatchSettings.MaxOverlap));
    public static string Polarity => IdOf(nameof(GrayMatchSettings.Polarity));
    public static string MatchingPolarity => IdOf(nameof(GrayMatchSettings.MatchingPolarity));
    public static string AngleStart => IdOf(nameof(GrayMatchSettings.AngleStart));
    public static string AngleEnd => IdOf(nameof(GrayMatchSettings.AngleEnd));
    public static string SortType => IdOf(nameof(GrayMatchSettings.SortType));
    public static string TimeOut => IdOf(nameof(GrayMatchSettings.TimeOut));
    public static string EndPyramidLevel => IdOf(nameof(GrayMatchSettings.EndPyramidLevel));
    public static string UseMatchAllMode => IdOf(nameof(GrayMatchSettings.UseMatchAllMode));
    public static string OkWhenCountIsZero => IdOf(nameof(GrayMatchSettings.OkWhenCountIsZero));
    public static string CountLimitEnabled => IdOf(nameof(GrayMatchSettings.CountLimitEnabled));
    public static string CountLimitLow => IdOf(nameof(GrayMatchSettings.CountLimitLow));
    public static string CountLimitHigh => IdOf(nameof(GrayMatchSettings.CountLimitHigh));
    public static string AngleLimitEnabled => IdOf(nameof(GrayMatchSettings.AngleLimitEnabled));
    public static string AngleLimitLow => IdOf(nameof(GrayMatchSettings.AngleLimitLow));
    public static string AngleLimitHigh => IdOf(nameof(GrayMatchSettings.AngleLimitHigh));
    public static string ScoreLimitEnabled => IdOf(nameof(GrayMatchSettings.ScoreLimitEnabled));
    public static string ScoreLimitLow => IdOf(nameof(GrayMatchSettings.ScoreLimitLow));
    public static string ScoreLimitHigh => IdOf(nameof(GrayMatchSettings.ScoreLimitHigh));
    public static string MatchPointXLimitEnabled => IdOf(nameof(GrayMatchSettings.MatchPointXLimitEnabled));
    public static string MatchPointXLimitLow => IdOf(nameof(GrayMatchSettings.MatchPointXLimitLow));
    public static string MatchPointXLimitHigh => IdOf(nameof(GrayMatchSettings.MatchPointXLimitHigh));
    public static string MatchPointYLimitEnabled => IdOf(nameof(GrayMatchSettings.MatchPointYLimitEnabled));
    public static string MatchPointYLimitLow => IdOf(nameof(GrayMatchSettings.MatchPointYLimitLow));
    public static string MatchPointYLimitHigh => IdOf(nameof(GrayMatchSettings.MatchPointYLimitHigh));
    public static string BoxPointXLimitEnabled => IdOf(nameof(GrayMatchSettings.BoxPointXLimitEnabled));
    public static string BoxPointXLimitLow => IdOf(nameof(GrayMatchSettings.BoxPointXLimitLow));
    public static string BoxPointXLimitHigh => IdOf(nameof(GrayMatchSettings.BoxPointXLimitHigh));
    public static string BoxPointYLimitEnabled => IdOf(nameof(GrayMatchSettings.BoxPointYLimitEnabled));
    public static string BoxPointYLimitLow => IdOf(nameof(GrayMatchSettings.BoxPointYLimitLow));
    public static string BoxPointYLimitHigh => IdOf(nameof(GrayMatchSettings.BoxPointYLimitHigh));

    public static string IdOf(string name)
        => ByName.TryGetValue(name, out var field) ? field.Id : throw new ArgumentException("未知的 GrayMatch 设置字段：" + name, nameof(name));

    public static GrayMatchFieldSpec Spec(string name)
        => ByName.TryGetValue(name, out var field) ? field : throw new ArgumentException("未知的 GrayMatch 设置字段：" + name, nameof(name));

    public static GrayMatchFieldSpec SpecByVmName(string virtualMachineName)
        => FieldList.FirstOrDefault(field => field.VirtualMachineName == virtualMachineName)
            ?? throw new ArgumentException("无关的原厂参数名：" + virtualMachineName, nameof(virtualMachineName));

    private static string StableId(string name)
        => "field:" + new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("SunEyeVision.Tool.GrayMatch:" + name)).AsSpan(0, 16)).ToString("N");

    private static GrayMatchFieldSpec B(string vm, string name, string label, string section, bool value,
        string? when = null, bool advanced = false, string page = RunPage)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(bool), Default = value,
            Label = label, Page = page, Section = section, VisibleWhen = when, Advanced = advanced
        };

    private static GrayMatchFieldSpec I(string vm, string name, string label, string section, int value, double minimum, double maximum,
        string? when = null, string? pair = null, string? pairLabel = null, bool advanced = false, bool reference = true,
        string page = RunPage)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(int), Default = value,
            Label = label, Page = page, Section = section, VisibleWhen = when, PairWith = pair, PairLabel = pairLabel,
            Advanced = advanced, AllowReference = reference, Minimum = minimum, Maximum = maximum,
            PairMinimum = pair is null ? null : minimum, PairMaximum = pair is null ? null : maximum, DecimalPlaces = 0, Increment = 1
        };

    private static GrayMatchFieldSpec N(string vm, string name, string label, string section, double value, double minimum, double maximum,
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

    private static GrayMatchFieldSpec E(string vm, string name, string label, string section, Type enumType, int value,
        IEnumerable<string> keys, IEnumerable<string> labels, string? when = null, bool advanced = false, string page = RunPage)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = enumType,
            Default = Enum.ToObject(enumType, value), Label = label, Page = page, Section = section,
            VisibleWhen = when, Advanced = advanced,
            Choices = keys.ToArray(), ChoiceLabels = keys.Zip(labels).ToDictionary(item => item.First, item => item.Second, StringComparer.Ordinal)
        };

    private static GrayMatchFieldSpec S(string vm, string name, string label, string section, string value, string page)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(string), Default = value,
            Label = label, Page = page, Section = section, DecimalPlaces = 0
        };

    private static IReadOnlyList<GrayMatchFieldSpec> Build()
    {
        var fields = new List<GrayMatchFieldSpec>
        {
            // 模板参数（原件训练对话框「配置参数」组 5 行 + 模型数据）
            S("ModelData", nameof(GrayMatchSettings.ModelData), "模板数据", TemplateSection, "", TemplatePage),
            E("PyramidNumLevelFlag", nameof(GrayMatchSettings.PyramidMode), "金字塔层数标记", TemplateSection,
                typeof(GrayMatchPyramidMode), 1, Enum.GetNames<GrayMatchPyramidMode>(), new[] { "手动", "自动" }, page: TemplatePage),
            I("PyramidNumLevel", nameof(GrayMatchSettings.PyramidLevel), "金字塔层数", TemplateSection, 5, 1, 8,
                when: nameof(GrayMatchSettings.PyramidMode), reference: false, page: TemplatePage),
            I("ModelAngleStep", nameof(GrayMatchSettings.ModelAngleStep), "角度步长", TemplateSection, 2, 1, 50,
                reference: false, page: TemplatePage),
            I("ModelAngleStart", nameof(GrayMatchSettings.ModelAngleStart), "起始角度", TemplateSection, -45, -180, 180,
                pair: nameof(GrayMatchSettings.ModelAngleEnd), pairLabel: "模板角度范围", reference: false, page: TemplatePage),
            I("ModelAngleEnd", nameof(GrayMatchSettings.ModelAngleEnd), "终止角度", TemplateSection, 45, -180, 180,
                reference: false, page: TemplatePage),

            // 运行参数（原件 VA 版 33 项 ParamItem）
            N("MinScore", nameof(GrayMatchSettings.MinScore), "最小分数", RunSection, 0.5, 0, 1),
            I("MaxMatchNum", nameof(GrayMatchSettings.MaxMatchCount), "最大匹配个数", RunSection, 1, 1, 50_000),
            I("MaxOverlap", nameof(GrayMatchSettings.MaxOverlap), "最大重叠率", RunSection, 40, 0, 100),
            I("Polarity", nameof(GrayMatchSettings.Polarity), "极性", RunSection, 1, 0, 1),
            I("MatchingPolarity", nameof(GrayMatchSettings.MatchingPolarity), "匹配极性", RunSection, 1, 0, 1),
            I("AngleStart", nameof(GrayMatchSettings.AngleStart), "角度下限", RunSection, -45, -180, 180,
                pair: nameof(GrayMatchSettings.AngleEnd), pairLabel: "角度范围"),
            I("AngleEnd", nameof(GrayMatchSettings.AngleEnd), "角度上限", RunSection, 45, -180, 180),
            E("SortType", nameof(GrayMatchSettings.SortType), "排序方式", RunSection, typeof(GrayMatchSortType), 2,
                Enum.GetNames<GrayMatchSortType>(),
                new[] { "分数降序", "角度降序", "X升序", "Y升序", "X升序·Y升序", "Y升序·X升序" }),
            I("TimeOut", nameof(GrayMatchSettings.TimeOut), "超时时间(毫秒)", RunSection, 0, 0, 10_000, advanced: true),
            I("EndPyramidLevel", nameof(GrayMatchSettings.EndPyramidLevel), "匹配结束层", RunSection, 0, 0, 8),
            B("UseMatchAllMode", nameof(GrayMatchSettings.UseMatchAllMode), "全部搜索模式", RunSection, false, advanced: true),
            B("OKWhenNumIsZero", nameof(GrayMatchSettings.OkWhenCountIsZero), "个数为0时判定OK", RunSection, false),

            // 结果判断：数量限位
            B("NumLimitEnable", nameof(GrayMatchSettings.CountLimitEnabled), "匹配个数判断", JudgeSection, false),
            I("NumLimitLow", nameof(GrayMatchSettings.CountLimitLow), "匹配个数下限", JudgeSection, 0, 0, 99_999,
                when: nameof(GrayMatchSettings.CountLimitEnabled), pair: nameof(GrayMatchSettings.CountLimitHigh), pairLabel: "匹配个数范围"),
            I("NumLimitHigh", nameof(GrayMatchSettings.CountLimitHigh), "匹配个数上限", JudgeSection, 99_999, 0, 99_999,
                when: nameof(GrayMatchSettings.CountLimitEnabled)),

            // 结果判断：角度/分数限位
            B("AngleLimitEnable", nameof(GrayMatchSettings.AngleLimitEnabled), "角度判断", JudgeSection, false, advanced: true),
            N("AngleLimitLow", nameof(GrayMatchSettings.AngleLimitLow), "角度下限", JudgeSection, -180, -180, 180,
                when: nameof(GrayMatchSettings.AngleLimitEnabled), pair: nameof(GrayMatchSettings.AngleLimitHigh), pairLabel: "角度范围", advanced: true),
            N("AngleLimitHigh", nameof(GrayMatchSettings.AngleLimitHigh), "角度上限", JudgeSection, 180, -180, 180,
                when: nameof(GrayMatchSettings.AngleLimitEnabled), advanced: true),
            B("ScoreLimitEnable", nameof(GrayMatchSettings.ScoreLimitEnabled), "分数判断", JudgeSection, false),
            N("ScoreLimitLow", nameof(GrayMatchSettings.ScoreLimitLow), "分数下限", JudgeSection, 0, 0, 1,
                when: nameof(GrayMatchSettings.ScoreLimitEnabled), pair: nameof(GrayMatchSettings.ScoreLimitHigh), pairLabel: "分数范围"),
            N("ScoreLimitHigh", nameof(GrayMatchSettings.ScoreLimitHigh), "分数上限", JudgeSection, 1, 0, 1,
                when: nameof(GrayMatchSettings.ScoreLimitEnabled)),

            // 结果判断：匹配点 / Box 点限位
            B("MatchPointXLimitEnable", nameof(GrayMatchSettings.MatchPointXLimitEnabled), "匹配点X判断", JudgeSection, false, advanced: true),
            N("MatchPointXLimitLow", nameof(GrayMatchSettings.MatchPointXLimitLow), "匹配点X下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(GrayMatchSettings.MatchPointXLimitEnabled), pair: nameof(GrayMatchSettings.MatchPointXLimitHigh), pairLabel: "匹配点X范围", advanced: true),
            N("MatchPointXLimitHigh", nameof(GrayMatchSettings.MatchPointXLimitHigh), "匹配点X上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(GrayMatchSettings.MatchPointXLimitEnabled), advanced: true),
            B("MatchPointYLimitEnable", nameof(GrayMatchSettings.MatchPointYLimitEnabled), "匹配点Y判断", JudgeSection, false, advanced: true),
            N("MatchPointYLimitLow", nameof(GrayMatchSettings.MatchPointYLimitLow), "匹配点Y下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(GrayMatchSettings.MatchPointYLimitEnabled), pair: nameof(GrayMatchSettings.MatchPointYLimitHigh), pairLabel: "匹配点Y范围", advanced: true),
            N("MatchPointYLimitHigh", nameof(GrayMatchSettings.MatchPointYLimitHigh), "匹配点Y上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(GrayMatchSettings.MatchPointYLimitEnabled), advanced: true),
            B("BoxPointXLimitEnable", nameof(GrayMatchSettings.BoxPointXLimitEnabled), "Box点X判断", JudgeSection, false, advanced: true),
            N("BoxPointXLimitLow", nameof(GrayMatchSettings.BoxPointXLimitLow), "Box点X下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(GrayMatchSettings.BoxPointXLimitEnabled), pair: nameof(GrayMatchSettings.BoxPointXLimitHigh), pairLabel: "Box点X范围", advanced: true),
            N("BoxPointXLimitHigh", nameof(GrayMatchSettings.BoxPointXLimitHigh), "Box点X上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(GrayMatchSettings.BoxPointXLimitEnabled), advanced: true),
            B("BoxPointYLimitEnable", nameof(GrayMatchSettings.BoxPointYLimitEnabled), "Box点Y判断", JudgeSection, false, advanced: true),
            N("BoxPointYLimitLow", nameof(GrayMatchSettings.BoxPointYLimitLow), "Box点Y下限", JudgeSection, -99_999, -99_999, 99_999,
                when: nameof(GrayMatchSettings.BoxPointYLimitEnabled), pair: nameof(GrayMatchSettings.BoxPointYLimitHigh), pairLabel: "Box点Y范围", advanced: true),
            N("BoxPointYLimitHigh", nameof(GrayMatchSettings.BoxPointYLimitHigh), "Box点Y上限", JudgeSection, 99_999, -99_999, 99_999,
                when: nameof(GrayMatchSettings.BoxPointYLimitEnabled), advanced: true)
        };
        var names = fields.Select(field => field.Name).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new InvalidOperationException("GrayMatch 参数字段名重复。");
        if (fields.Select(field => field.Id).Distinct(StringComparer.Ordinal).Count() != fields.Count)
            throw new InvalidOperationException("GrayMatch 参数稳定 ID 重复。");
        if (!Properties.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(names))
            throw new InvalidOperationException("GrayMatch 参数表与设置模型不一致。");
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
            presentations.Add(new FieldPresentation(field.Id, field.Label, field.Section, index,
                field.Choices.Count == 0 ? FieldEditorKind.Scalar : FieldEditorKind.Choice,
                field.Choices, field.ChoiceLabels) { Layout = layout });
        }
        return new ToolSchema(ports, Array.Empty<Port>(), new ToolPresentation(presentations));
    }

    /// <summary>可见性条件的取值：原件「金字塔层数标记」选手动才显示层数，枚举控制器取 <c>Manual</c>。</summary>
    private static object VisibilityValue(GrayMatchFieldSpec controller)
        => controller.Type.IsEnum ? "Manual" : true;

    private static TypeRef PortType(GrayMatchFieldSpec field)
        => field.Type == typeof(bool) ? DataType.Boolean
            : field.Type == typeof(int) ? DataType.Integer
            : field.Type == typeof(double) ? DataType.Number
            : field.Type.IsEnum ? TypeRef.ScalarOf(ScalarKind.Text)
            : DataType.Text;

    /// <summary>按冻结设置生成规范值；越界或类型不符直接报错。</summary>
    public static InputValues Read(GrayMatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var values = new Dictionary<string, object?>(FieldList.Count, StringComparer.Ordinal);
        foreach (var field in FieldList) values[field.Id] = ReadField(field, settings);
        return new InputValues(Schema.Inputs, values);
    }

    private static object? ReadField(GrayMatchFieldSpec field, GrayMatchSettings settings)
    {
        var raw = Properties[field.Name].GetValue(settings);
        object? value = field.Type == typeof(int) ? (long)(int)raw!
            : field.Type.IsEnum ? raw!.ToString()
            : raw;
        return ValueRules.Freeze(Schema.Inputs.Single(port => port.Id == field.Id), value);
    }

    /// <summary>从完整解析值物化设置；不接受缺项，也不从旧设置补默认。</summary>
    public static GrayMatchSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var settings = new GrayMatchSettings();
        foreach (var field in FieldList) settings = Write(settings, field, values.Get<object?>(field.Id));
        var errors = settings.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return settings.Freeze();
    }

    internal static GrayMatchSettings Write(GrayMatchSettings settings, GrayMatchFieldSpec field, object? value)
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

    public static string DefaultText(GrayMatchFieldSpec field)
        => field.Default switch
        {
            bool flag => flag ? "True" : "False",
            int number => number.ToString(CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(field.Default, CultureInfo.InvariantCulture) ?? ""
        };
}

using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.GrayMatch.Runtime;

/// <summary>
/// 原件「金字塔层数标记」（<c>GrayMatchVADialogData.PyramidNumLevelFlag</c>，默认 1）。
/// 证据：<c>IMVSGrayMatchModuVAControl/graymatchvadialoguistyle.xaml:865-869</c> 下拉项顺序为
/// 0=自动、1=手动（index 与 flag 互为 0/1 由 <c>GrayMatchVADialogDataCvt.cs:15-30</c> 转换），
/// 且 <c>Enable</c> 转换给出「flag=0 时层数滑条可用」→ **1=自动、0=手动**。
/// </summary>
public enum GrayMatchPyramidMode
{
    Manual = 0,
    Auto = 1
}

/// <summary>
/// 原件排序方式（<c>SortType</c>，默认 2）。取值语义来自同族托管 SDK 源码
/// <c>decompiled/Managed/FastFeatureMatch/IMVSFastFeatureMatchModuCs/FastFeatureMatchParam.cs:28-54</c>
/// 的 <c>SortTypeEnum</c>：2=分数降序、3=角度降序、4=X 升序、5=Y 升序、6=X 升序再 Y 升序、7=Y 升序再 X 升序。
/// </summary>
public enum GrayMatchSortType
{
    Score = 2,
    Angle = 3,
    X = 4,
    Y = 5,
    XY = 6,
    YX = 7
}

/// <summary>
/// 冻结的算法快照。字段来源：
/// <list type="bullet">
/// <item>模板参数：<c>ModuleControls/IMVSGrayMatchModuVAControl/Apps.XmlParser.ParamTab/GrayMatchVADialogData.cs:7-15</c>
/// 的字段默认值（pyramidNumLevelFlag=1、pyramidNumLevel=5、modelAngleStep=2、modelAngleStart=-45、modelAngleEnd=45）
/// 与控件范围 <c>graymatchvadialoguistyle.xaml:865-872</c>（层数 1..8、角度步长 1..50、角度 -180..180 互相夹取）。</item>
/// <item>运行/判断参数：<c>docs/07-工具逆向/tools/imvsgraymatchmoduva/parameters.json</c> 的 **33 项** ParamItem
/// （VA 版：<c>MaxOverlap=40</c>、<c>MatchingPolarity=1</c>、<c>EndPyramidLevel=0</c>、<c>UseMatchAllMode=False</c>，
/// 且**不含** <c>AngleStep</c>；旧版 <c>imvsgraymatchmodu</c> 的 31 项不作为默认值来源）。</item>
/// </list>
/// 属性名按产品命名规范改写，与原厂名称的对应关系由 <see cref="GrayMatchFields"/> 统一给出。
/// </summary>
public sealed record GrayMatchSettings : IToolSettings<GrayMatchSettings>
{
    // 模板参数（原件训练对话框「配置参数」组 + 模型数据）
    /// <summary>原件 <c>ModelData</c>（AddSpareModel 字节数据）的中立承载：本工具模板数据（见 <see cref="GrayMatchTemplate"/>）。</summary>
    public string ModelData { get; init; } = "";
    public GrayMatchPyramidMode PyramidMode { get; init; } = GrayMatchPyramidMode.Auto;
    /// <summary>原件「金字塔层数」（PyramidNumLevel，滑条 1..8，默认 5）。</summary>
    public int PyramidLevel { get; init; } = 5;
    /// <summary>原件「角度步长」（ModelAngleStep，滑条 1..50，默认 2）。</summary>
    public int ModelAngleStep { get; init; } = 2;
    /// <summary>原件「起始角度」（ModelAngleStart，-180..180，默认 -45）。</summary>
    public int ModelAngleStart { get; init; } = -45;
    /// <summary>原件「终止角度」（ModelAngleEnd，-180..180，默认 45）。</summary>
    public int ModelAngleEnd { get; init; } = 45;

    // 运行参数（原件 VA 版 33 项 ParamItem）
    public double MinScore { get; init; } = 0.5;
    public int MaxMatchCount { get; init; } = 1;
    /// <summary>原件 MaxOverlap：默认 40（VA 版；旧版为 80，不作为来源）。</summary>
    public int MaxOverlap { get; init; } = 40;
    /// <summary>原件 Polarity：同族 SDK 枚举 0=不考虑极性、1=考虑极性；默认 1。</summary>
    public int Polarity { get; init; } = 1;
    /// <summary>原件 MatchingPolarity：默认 1。与 Polarity 的分工未取证，非默认即拒绝。</summary>
    public int MatchingPolarity { get; init; } = 1;
    public int AngleStart { get; init; } = -45;
    public int AngleEnd { get; init; } = 45;
    public GrayMatchSortType SortType { get; init; } = GrayMatchSortType.Score;
    /// <summary>原件 TimeOut：超时时间（毫秒），默认 0（不限）。原生匹配不可中断，非 0 即拒绝。</summary>
    public int TimeOut { get; init; }
    /// <summary>
    /// 原件「匹配结束层」（EndPyramidLevel，默认 0）。**适配映射结论**（非原厂算法语义已证实）：
    /// 语义与 HALCON <c>find_ncc_model</c> 的 <c>NumLevels</c>（0＝使用全部层）对齐。
    /// </summary>
    public int EndPyramidLevel { get; init; }
    /// <summary>原件 UseMatchAllMode：全部搜索模式，默认 False。语义未取证，启用即拒绝。</summary>
    public bool UseMatchAllMode { get; init; }
    /// <summary>原件 OKWhenNumIsZero：匹配个数为 0 时判定 OK，默认 False。</summary>
    public bool OkWhenCountIsZero { get; init; }

    // 结果判断（原件限位组）
    public bool CountLimitEnabled { get; init; }
    public int CountLimitLow { get; init; }
    public int CountLimitHigh { get; init; } = 99_999;
    public bool AngleLimitEnabled { get; init; }
    public double AngleLimitLow { get; init; } = -180;
    public double AngleLimitHigh { get; init; } = 180;
    public bool ScoreLimitEnabled { get; init; }
    public double ScoreLimitLow { get; init; }
    public double ScoreLimitHigh { get; init; } = 1;
    public bool MatchPointXLimitEnabled { get; init; }
    public double MatchPointXLimitLow { get; init; } = -99_999;
    public double MatchPointXLimitHigh { get; init; } = 99_999;
    public bool MatchPointYLimitEnabled { get; init; }
    public double MatchPointYLimitLow { get; init; } = -99_999;
    public double MatchPointYLimitHigh { get; init; } = 99_999;
    public bool BoxPointXLimitEnabled { get; init; }
    public double BoxPointXLimitLow { get; init; } = -99_999;
    public double BoxPointXLimitHigh { get; init; } = 99_999;
    public bool BoxPointYLimitEnabled { get; init; }
    public double BoxPointYLimitLow { get; init; } = -99_999;
    public double BoxPointYLimitHigh { get; init; } = 99_999;

    /// <summary>冻结只做文本规范化，不改数值；可执行性由 <see cref="Validate"/> 明确判定。</summary>
    public GrayMatchSettings Freeze() => this with { ModelData = ModelData?.Trim() ?? "" };

    /// <summary>参数组合校验；任何一项不成立都返回明确诊断，不做静默降级（rule-015）。</summary>
    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Fail(string code, string message, string? fieldId = null) => errors.Add(new(code, message, fieldId));
        void Range(bool enabled, double minimum, double maximum, string name, string fieldId)
        {
            if (!enabled) return;
            if (!double.IsFinite(minimum) || !double.IsFinite(maximum)) Fail("GRAYMATCH_SETTINGS_INVALID", name + "范围必须是有限数。", fieldId);
            else if (minimum > maximum) Fail("GRAYMATCH_SETTINGS_INVALID", name + "下限不能超过上限。", fieldId);
        }

        // 模板参数
        if (PyramidLevel is < 1 or > 8) Fail("GRAYMATCH_SETTINGS_INVALID", "金字塔层数必须在 1 到 8 之间。", GrayMatchFields.PyramidLevel);
        if (ModelAngleStep is < 1 or > 50) Fail("GRAYMATCH_SETTINGS_INVALID", "角度步长必须在 1 到 50 之间。", GrayMatchFields.ModelAngleStep);
        if (ModelAngleStart is < -180 or > 180 || ModelAngleEnd is < -180 or > 180)
            Fail("GRAYMATCH_SETTINGS_INVALID", "模板角度范围必须在 -180 到 180 度之间。", GrayMatchFields.ModelAngleStart);
        else if (ModelAngleStart > ModelAngleEnd)
            Fail("GRAYMATCH_SETTINGS_INVALID", "模板起始角度不能超过终止角度。", GrayMatchFields.ModelAngleStart);
        if (!Enum.IsDefined(PyramidMode)) Fail("GRAYMATCH_SETTINGS_INVALID", "金字塔层数标记取值非法。", GrayMatchFields.PyramidMode);
        if (!Enum.IsDefined(SortType)) Fail("GRAYMATCH_SETTINGS_INVALID", "排序方式取值非法。", GrayMatchFields.SortType);

        // 运行参数
        if (MinScore is < 0 or > 1) Fail("GRAYMATCH_SETTINGS_INVALID", "最小分数必须在 0 到 1 之间。", GrayMatchFields.MinScore);
        if (MaxMatchCount is < 1 or > 50_000) Fail("GRAYMATCH_SETTINGS_INVALID", "最大匹配个数必须在 1 到 50000 之间。", GrayMatchFields.MaxMatchCount);
        if (MaxOverlap is < 0 or > 100) Fail("GRAYMATCH_SETTINGS_INVALID", "最大重叠率必须在 0 到 100 之间。", GrayMatchFields.MaxOverlap);
        if (EndPyramidLevel is < 0 or > 8) Fail("GRAYMATCH_SETTINGS_INVALID", "匹配结束层必须在 0 到 8 之间（0 表示使用全部层）。", GrayMatchFields.EndPyramidLevel);
        if (AngleStart is < -180 or > 180 || AngleEnd is < -180 or > 180) Fail("GRAYMATCH_SETTINGS_INVALID", "角度范围必须在 -180 到 180 度之间。", GrayMatchFields.AngleStart);
        else if (AngleStart > AngleEnd) Fail("GRAYMATCH_SETTINGS_INVALID", "角度范围下限不能超过上限。", GrayMatchFields.AngleStart);

        // 未取证/未迁移语义的取值一律显式拒绝，不静默忽略。
        if (Polarity != 1)
            Fail("GRAYMATCH_POLARITY_NOT_READY", "极性是 HALCON 模板创建期度量（metric），运行期无法切换，暂只支持 1（考虑极性）。", GrayMatchFields.Polarity);
        if (MatchingPolarity != 1)
            Fail("GRAYMATCH_MATCHING_POLARITY_NOT_READY", "匹配极性与极性的分工尚未取证，暂只支持默认值 1。", GrayMatchFields.MatchingPolarity);
        if (TimeOut != 0)
            Fail("GRAYMATCH_TIMEOUT_NOT_READY", "原生匹配不可中断，时间限制未实现，暂只支持 0（不限）。", GrayMatchFields.TimeOut);
        if (UseMatchAllMode)
            Fail("GRAYMATCH_MATCH_ALL_MODE_NOT_READY", "全部搜索模式语义尚未取证，暂不支持启用。", GrayMatchFields.UseMatchAllMode);

        // 结果判断
        Range(CountLimitEnabled, CountLimitLow, CountLimitHigh, "匹配个数判断", GrayMatchFields.CountLimitLow);
        Range(AngleLimitEnabled, AngleLimitLow, AngleLimitHigh, "角度判断", GrayMatchFields.AngleLimitLow);
        Range(ScoreLimitEnabled, ScoreLimitLow, ScoreLimitHigh, "分数判断", GrayMatchFields.ScoreLimitLow);
        Range(MatchPointXLimitEnabled, MatchPointXLimitLow, MatchPointXLimitHigh, "匹配点X判断", GrayMatchFields.MatchPointXLimitLow);
        Range(MatchPointYLimitEnabled, MatchPointYLimitLow, MatchPointYLimitHigh, "匹配点Y判断", GrayMatchFields.MatchPointYLimitLow);
        Range(BoxPointXLimitEnabled, BoxPointXLimitLow, BoxPointXLimitHigh, "Box点X判断", GrayMatchFields.BoxPointXLimitLow);
        Range(BoxPointYLimitEnabled, BoxPointYLimitLow, BoxPointYLimitHigh, "Box点Y判断", GrayMatchFields.BoxPointYLimitLow);
        if (CountLimitEnabled && CountLimitLow < 0) Fail("GRAYMATCH_SETTINGS_INVALID", "匹配个数判断下限不能为负。", GrayMatchFields.CountLimitLow);
        if (ScoreLimitEnabled && (ScoreLimitLow < 0 || ScoreLimitHigh > 1))
            Fail("GRAYMATCH_SETTINGS_INVALID", "分数判断范围必须在 0 到 1 之间。", GrayMatchFields.ScoreLimitLow);
        if (AngleLimitEnabled && (AngleLimitLow < -180 || AngleLimitHigh > 180))
            Fail("GRAYMATCH_SETTINGS_INVALID", "角度判断范围必须在 -180 到 180 度之间。", GrayMatchFields.AngleLimitLow);
        return errors.AsReadOnly();
    }
}

using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 原件「金字塔比例」（<c>HPPyramidFeaturepatMatchDialogData.PyramidScaleFlag</c>，默认 1）。
/// 训练对话框 <c>hppyramidfeaturepatmatchdialoguistyle.xaml:887-890</c> 的下拉项顺序是 0=手动、1=自动；
/// 手动时压缩比/特征尺度才可用（同处 <c>IsEnabled</c> 绑定 <c>PyramidModeToEnableCvt</c>）。
/// </summary>
public enum ContourMatchPyramidMode
{
    Manual = 0,
    Auto = 1
}

/// <summary>
/// 原件「阈值模式」（<c>EdgeThresholdFlag</c>，默认 1）。下拉项顺序同为 0=手动、1=自动
/// （<c>hppyramidfeaturepatmatchdialoguistyle.xaml:893-897</c>），手动时边缘阈值可用。
/// </summary>
public enum ContourMatchThresholdMode
{
    Manual = 0,
    Auto = 1
}

/// <summary>
/// 原件「链式」（<c>ChainFlag</c>，默认 1）。下拉项 0=手动、1=自动
/// （<c>hppyramidfeaturepatmatchdialoguistyle.xaml:898-902</c>），自动时最小链长由原件自行决定、控件禁用。
/// </summary>
public enum ContourMatchChainMode
{
    Manual = 0,
    Auto = 1
}

/// <summary>
/// 原件「掩膜模型」（<c>MaskModelFlag</c>，默认 0）。下拉项 0=全轮廓、1=外轮廓
/// （<c>hppyramidfeaturepatmatchdialoguistyle.xaml:903-906</c>）。
/// </summary>
public enum ContourMatchMaskModel
{
    FullOutline = 0,
    OuterOutline = 1
}

/// <summary>
/// 原件排序方式（<c>SortType</c>，默认 2）。取值语义来自同族托管 SDK 源码
/// <c>decompiled/Managed/FastFeatureMatch/IMVSFastFeatureMatchModuCs/FastFeatureMatchParam.cs:28-54</c>
/// 的 <c>SortTypeEnum</c>：2=分数降序、3=角度降序、4=X 升序、5=Y 升序、6=X 升序再 Y 升序、7=Y 升序再 X 升序。
/// </summary>
public enum ContourMatchSortType
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
/// <item>模板参数：<c>ModuleControls/IMVSContourMatchModuControl/Apps.XmlParser.ParamTab/HPPyramidFeaturepatMatchDialogData.cs</c>
/// 的字段默认值（_IncludeImageFlag=0、_pyramidScaleFlag=1、_pyramidScaleLevel=5、_pyramidScaleRLevel=1、
/// _edgeThresholdFlag=1、_edgeThreshold=15、_maskModelFlag=0）与基类
/// <c>ModuleControls/IMVSHPFeatureMatchModuControl/Apps.XmlParser.ParamTab/HPFeaturepatMatchDialogData.cs</c>
/// 的 _chainFlag=1、_minChainLen=4；控件顺序与范围取
/// <c>IMVSContourMatchModuControl/hppyramidfeaturepatmatchdialoguistyle.xaml:879-906</c>。</item>
/// <item>运行/判断参数：<c>docs/07-工具逆向/tools/imvscontourmatchmodu/parameters.json</c> 的 56 项 ParamItem
/// （名称与默认值逐条对应）；取值语义参考同族托管 SDK 源码 <c>FastFeatureMatchParam.cs</c>。</item>
/// </list>
/// 属性名按产品命名规范改写，与原厂名称的对应关系由 <see cref="ContourMatchFields"/> 统一给出。
/// </summary>
public sealed record ContourMatchSettings : IToolSettings<ContourMatchSettings>
{
    // 模板参数（原件训练对话框「匹配参数」组 + 模型数据）
    /// <summary>原件 <c>ModelData</c>（AddSpareModel 字节数据）的中立承载：本工具模板数据（见 <see cref="ContourMatchTemplate"/>）。</summary>
    public string ModelData { get; init; } = "";
    /// <summary>
    /// 模板训练样本：复用平台区域文档，inspection 组=正样本、mask 组=背景样本
    /// （见 <see cref="ContourMatchTrainingRois"/> 与原厂 SignList 证据）。
    /// </summary>
    public RecordValue TrainingRois { get; init; } = RoiDocumentValue.Empty;
    public ContourMatchPyramidMode PyramidMode { get; init; } = ContourMatchPyramidMode.Auto;
    /// <summary>原件「压缩比」（PyramidScaleLevel，滑条 1..10，默认 5）。</summary>
    public int PyramidScaleLevel { get; init; } = 5;
    /// <summary>原件「特征尺度」（PyramidScaleRLevel，滑条 1..10，默认 1）。</summary>
    public int PyramidScaleRLevel { get; init; } = 1;
    public ContourMatchThresholdMode EdgeThresholdMode { get; init; } = ContourMatchThresholdMode.Auto;
    /// <summary>原件「边缘阈值」（EdgeThreshold，滑条 1..255，默认 15）。</summary>
    public int EdgeThreshold { get; init; } = 15;
    public ContourMatchChainMode ChainMode { get; init; } = ContourMatchChainMode.Auto;
    /// <summary>原件「最小链长」（MinChainLen，滑条 2..1000，默认 4）；链式为自动时原件禁用该控件。</summary>
    public int MinChainLen { get; init; } = 4;
    public ContourMatchMaskModel MaskModel { get; init; } = ContourMatchMaskModel.FullOutline;

    // 运行参数（原件 56 项 ParamItem）
    public double MinScore { get; init; } = 0.5;
    public int MaxMatchCount { get; init; } = 1;
    /// <summary>原件 Polarity：同族 SDK 枚举 0=不考虑极性、1=考虑极性；默认 1。</summary>
    public int Polarity { get; init; } = 1;
    public int AngleStart { get; init; } = -45;
    public int AngleEnd { get; init; } = 45;
    public double ScaleStart { get; init; } = 1;
    public double ScaleEnd { get; init; } = 1;
    public double ScaleXStart { get; init; } = 1;
    public double ScaleXEnd { get; init; } = 1;
    public double ScaleYStart { get; init; } = 1;
    public double ScaleYEnd { get; init; } = 1;
    public int MaxOverlap { get; init; } = 50;
    /// <summary>原件 MultiModelMaxOverlap：默认 50，只在多模板匹配时生效；本工具单模板，非默认值显式拒绝。</summary>
    public int MultiModelMaxOverlap { get; init; } = 50;
    public ContourMatchSortType SortType { get; init; } = ContourMatchSortType.Score;
    /// <summary>原件 MatchThresholdFlag：同族 SDK 枚举 0=自动、1=模板、2=手动；默认 0。</summary>
    public int MatchThresholdMode { get; init; }
    public int MatchThreshold { get; init; } = 40;
    /// <summary>原件 SpotterFlag：是否考虑噪点，默认 False。匹配期语义未取证，启用即拒绝。</summary>
    public bool SpotterEnabled { get; init; }
    /// <summary>原件 MatchExtentRate：延拓阈值 0..90，默认 0。语义未取证，非 0 即拒绝。</summary>
    public int MatchExtentRate { get; init; }
    /// <summary>原件 TimeOut：超时控制 0..10000，默认 0（不限）。原生匹配不可中断，非 0 即拒绝。</summary>
    public int TimeOut { get; init; }
    public bool OutlineEnabled { get; init; } = true;
    /// <summary>原件 OutLinePointEnable：轮廓点使能，默认 False。逐点明细语义未取证，启用即拒绝。</summary>
    public bool OutlinePointsEnabled { get; init; }
    /// <summary>原件 UseMatchAllMode：全部搜索模式，默认 False。语义未取证，启用即拒绝。</summary>
    public bool UseMatchAllMode { get; init; }
    /// <summary>原件 RangeScaleType：尺度范围类型，默认 0（等比）。非默认即拒绝。</summary>
    public int RangeScaleType { get; init; }
    /// <summary>原件 OKWhenNumIsZero：匹配个数为 0 时判定 OK，默认 False。</summary>
    public bool OkWhenCountIsZero { get; init; }
    /// <summary>原件 SkewXStart：起始横切角度，默认 0。HALCON 形状模板无横切语义，非 0 即拒绝。</summary>
    public int SkewXStart { get; init; }
    /// <summary>原件 SkewXEnd：终止横切角度，默认 0。</summary>
    public int SkewXEnd { get; init; }

    // 结果判断（原件限位组）
    public bool CountLimitEnabled { get; init; }
    public int CountLimitLow { get; init; }
    public int CountLimitHigh { get; init; } = 99_999;
    public bool AngleLimitEnabled { get; init; }
    public double AngleLimitLow { get; init; } = -180;
    public double AngleLimitHigh { get; init; } = 180;
    public bool ScaleLimitEnabled { get; init; }
    public double ScaleLimitLow { get; init; } = 0.1;
    public double ScaleLimitHigh { get; init; } = 10;
    public bool ScaleXLimitEnabled { get; init; }
    public double ScaleXLimitLow { get; init; } = 0.1;
    public double ScaleXLimitHigh { get; init; } = 10;
    public bool ScaleYLimitEnabled { get; init; }
    public double ScaleYLimitLow { get; init; } = 0.1;
    public double ScaleYLimitHigh { get; init; } = 10;
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
    public ContourMatchSettings Freeze() => this with { ModelData = ModelData?.Trim() ?? "" };

    /// <summary>参数组合校验；任何一项不成立都返回明确诊断，不做静默降级（rule-015）。</summary>
    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Fail(string code, string message, string? fieldId = null) => errors.Add(new(code, message, fieldId));
        void Range(bool enabled, double minimum, double maximum, string name, string minField, string maxField)
        {
            if (!enabled) return;
            if (!double.IsFinite(minimum) || !double.IsFinite(maximum)) Fail("CONTOURMATCH_SETTINGS_INVALID", name + "范围必须是有限数。", minField);
            else if (minimum > maximum) Fail("CONTOURMATCH_SETTINGS_INVALID", name + "下限不能超过上限。", minField);
            _ = maxField;
        }

        // 模板参数
        if (PyramidScaleLevel is < 1 or > 10) Fail("CONTOURMATCH_SETTINGS_INVALID", "压缩比必须在 1 到 10 之间。", ContourMatchFields.PyramidScaleLevel);
        if (PyramidScaleRLevel is < 1 or > 10) Fail("CONTOURMATCH_SETTINGS_INVALID", "特征尺度必须在 1 到 10 之间。", ContourMatchFields.PyramidScaleRLevel);
        if (PyramidMode == ContourMatchPyramidMode.Manual && PyramidScaleRLevel > PyramidScaleLevel)
            Fail("CONTOURMATCH_SETTINGS_INVALID", "特征尺度不能大于压缩比。", ContourMatchFields.PyramidScaleRLevel);
        if (EdgeThreshold is < 1 or > 255) Fail("CONTOURMATCH_SETTINGS_INVALID", "边缘阈值必须在 1 到 255 之间。", ContourMatchFields.EdgeThreshold);
        if (MinChainLen is < 2 or > 1000) Fail("CONTOURMATCH_SETTINGS_INVALID", "最小链长必须在 2 到 1000 之间。", ContourMatchFields.MinChainLen);
        if (!Enum.IsDefined(PyramidMode)) Fail("CONTOURMATCH_SETTINGS_INVALID", "金字塔比例取值非法。", ContourMatchFields.PyramidMode);
        if (!Enum.IsDefined(EdgeThresholdMode)) Fail("CONTOURMATCH_SETTINGS_INVALID", "阈值模式取值非法。", ContourMatchFields.EdgeThresholdMode);
        if (!Enum.IsDefined(ChainMode)) Fail("CONTOURMATCH_SETTINGS_INVALID", "链式取值非法。", ContourMatchFields.ChainMode);
        if (!Enum.IsDefined(MaskModel)) Fail("CONTOURMATCH_SETTINGS_INVALID", "掩膜模型取值非法。", ContourMatchFields.MaskModel);
        if (!Enum.IsDefined(SortType)) Fail("CONTOURMATCH_SETTINGS_INVALID", "排序方式取值非法。", ContourMatchFields.SortType);

        // 运行参数
        if (MinScore is < 0 or > 1) Fail("CONTOURMATCH_SETTINGS_INVALID", "最小分数必须在 0 到 1 之间。", ContourMatchFields.MinScore);
        if (MaxMatchCount is < 1 or > 50_000) Fail("CONTOURMATCH_SETTINGS_INVALID", "最大匹配个数必须在 1 到 50000 之间。", ContourMatchFields.MaxMatchCount);
        if (MaxOverlap is < 0 or > 100) Fail("CONTOURMATCH_SETTINGS_INVALID", "最大重叠率必须在 0 到 100 之间。", ContourMatchFields.MaxOverlap);
        if (AngleStart is < -180 or > 180 || AngleEnd is < -180 or > 180) Fail("CONTOURMATCH_SETTINGS_INVALID", "角度范围必须在 -180 到 180 度之间。", ContourMatchFields.AngleStart);
        else if (AngleStart > AngleEnd) Fail("CONTOURMATCH_SETTINGS_INVALID", "角度范围下限不能超过上限。", ContourMatchFields.AngleStart);
        if (Polarity is not (0 or 1))
            Fail("CONTOURMATCH_SETTINGS_INVALID", "匹配极性必须是 0（忽略）或 1（考虑）。", ContourMatchFields.Polarity);
        if (ScaleStart < 0.5 || ScaleEnd > 2.0 || ScaleStart > ScaleEnd)
            Fail("CONTOURMATCH_SETTINGS_INVALID", "尺度搜索范围必须在 0.5~2.0 且下限不超过上限。", ContourMatchFields.ScaleStart);
        if (TimeOut is < 0 or > 10_000)
            Fail("CONTOURMATCH_SETTINGS_INVALID", "超时控制必须在 0 到 10000 秒之间。", ContourMatchFields.TimeOut);

        // 结果判断
        Range(CountLimitEnabled, CountLimitLow, CountLimitHigh, "匹配个数判断", ContourMatchFields.CountLimitLow, ContourMatchFields.CountLimitHigh);
        Range(AngleLimitEnabled, AngleLimitLow, AngleLimitHigh, "角度判断", ContourMatchFields.AngleLimitLow, ContourMatchFields.AngleLimitHigh);
        Range(ScoreLimitEnabled, ScoreLimitLow, ScoreLimitHigh, "分数判断", ContourMatchFields.ScoreLimitLow, ContourMatchFields.ScoreLimitHigh);
        Range(MatchPointXLimitEnabled, MatchPointXLimitLow, MatchPointXLimitHigh, "匹配点X判断", ContourMatchFields.MatchPointXLimitLow, ContourMatchFields.MatchPointXLimitHigh);
        Range(MatchPointYLimitEnabled, MatchPointYLimitLow, MatchPointYLimitHigh, "匹配点Y判断", ContourMatchFields.MatchPointYLimitLow, ContourMatchFields.MatchPointYLimitHigh);
        Range(BoxPointXLimitEnabled, BoxPointXLimitLow, BoxPointXLimitHigh, "Box点X判断", ContourMatchFields.BoxPointXLimitLow, ContourMatchFields.BoxPointXLimitHigh);
        Range(BoxPointYLimitEnabled, BoxPointYLimitLow, BoxPointYLimitHigh, "Box点Y判断", ContourMatchFields.BoxPointYLimitLow, ContourMatchFields.BoxPointYLimitHigh);
        if (CountLimitEnabled && CountLimitLow < 0) Fail("CONTOURMATCH_SETTINGS_INVALID", "匹配个数判断下限不能为负。", ContourMatchFields.CountLimitLow);
        if (ScoreLimitEnabled && (ScoreLimitLow < 0 || ScoreLimitHigh > 1))
            Fail("CONTOURMATCH_SETTINGS_INVALID", "分数判断范围必须在 0 到 1 之间。", ContourMatchFields.ScoreLimitLow);
        if (AngleLimitEnabled && (AngleLimitLow < -180 || AngleLimitHigh > 180))
            Fail("CONTOURMATCH_SETTINGS_INVALID", "角度判断范围必须在 -180 到 180 度之间。", ContourMatchFields.AngleLimitLow);
        return errors.AsReadOnly();
    }

    /// <summary>
    /// 能力边界校验：未取证/未迁移语义的取值显式拒绝，不静默忽略（rule-015）。
    /// 原厂参数页允许切换这些开关（G3a/G4 证据：全部搜索模式=开、是否考虑噪点=开均为合法测试态），
    /// 故编辑期不拒绝；仅执行入口调用，把拒绝放在真实执行边界上。
    /// </summary>
    public IReadOnlyList<Diagnostic> ValidateCapability()
    {
        var errors = new List<Diagnostic>();
        void Fail(string code, string message, string? fieldId = null) => errors.Add(new(code, message, fieldId));
        if (ChainMode != ContourMatchChainMode.Auto)
            Fail("CONTOURMATCH_CHAIN_MODE_NOT_READY", "手动链长在 HALCON 形状模板中没有对应语义，暂只支持自动。", ContourMatchFields.ChainMode);
        if (MaskModel != ContourMatchMaskModel.FullOutline)
            Fail("CONTOURMATCH_MASK_MODEL_NOT_READY", "外轮廓掩膜模型依赖原件轮廓划分语义，暂只支持全轮廓。", ContourMatchFields.MaskModel);
        if (ScaleXStart != 1 || ScaleXEnd != 1 || ScaleYStart != 1 || ScaleYEnd != 1)
            Fail("CONTOURMATCH_SCALE_XY_NOT_READY", "HALCON 形状模板仅支持等比尺度，X/Y 独立尺度搜索暂不支持。", ContourMatchFields.ScaleXStart);
        if (ScaleXLimitEnabled || ScaleYLimitEnabled)
            Fail("CONTOURMATCH_SCALE_XY_NOT_READY", "X/Y 独立尺度未实现，对应判断暂不可用。", ContourMatchFields.ScaleXLimitEnabled);
        if (MatchThresholdMode != 0)
            Fail("CONTOURMATCH_MATCH_THRESHOLD_NOT_READY", "边缘阈值在 HALCON 形状模板创建期固定，运行期无法切换，暂只支持 0（自动）。", ContourMatchFields.MatchThresholdMode);
        if (SpotterEnabled)
            Fail("CONTOURMATCH_SPOTTER_NOT_READY", "噪点考虑语义尚未取证，暂不支持执行。", ContourMatchFields.SpotterEnabled);
        if (MatchExtentRate != 0)
            Fail("CONTOURMATCH_MATCH_EXTENT_NOT_READY", "延拓阈值语义尚未取证，暂只支持 0。", ContourMatchFields.MatchExtentRate);
        if (OutlinePointsEnabled)
            Fail("CONTOURMATCH_OUTLINE_POINTS_NOT_READY", "轮廓点逐点明细语义尚未取证，轮廓已由“轮廓使能”输出，暂不支持执行。", ContourMatchFields.OutlinePointsEnabled);
        if (UseMatchAllMode)
            Fail("CONTOURMATCH_MATCH_ALL_MODE_NOT_READY", "全部搜索模式语义尚未取证，暂不支持执行。", ContourMatchFields.UseMatchAllMode);
        if (MultiModelMaxOverlap != 50)
            Fail("CONTOURMATCH_MULTI_MODEL_NOT_READY", "多模板匹配未迁移，多模板重叠率不具备执行语义，暂只支持默认值 50。", ContourMatchFields.MultiModelMaxOverlap);
        if (RangeScaleType != 0)
            Fail("CONTOURMATCH_RANGE_SCALE_TYPE_NOT_READY", "尺度范围类型语义尚未取证，暂只支持默认值 0。", ContourMatchFields.RangeScaleType);
        if (SkewXStart != 0 || SkewXEnd != 0)
            Fail("CONTOURMATCH_SKEW_NOT_READY", "横切角度在 HALCON 形状模板中没有对应语义，暂只支持 0。", ContourMatchFields.SkewXStart);
        return errors.AsReadOnly();
    }
}

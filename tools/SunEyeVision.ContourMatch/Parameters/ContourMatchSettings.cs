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
/// 原件「尺度范围类型」（RangeScaleType，默认 0）。AlgorithmTab.xml 的 EnumEntrys：
/// 0=尺度范围（Scale Range）、1=尺度范围XY（Scale RangeXY）；=1 时显示 X/Y 尺度范围行。
/// </summary>
public enum ContourMatchRangeScaleType
{
    Range = 0,
    XY = 1
}

/// <summary>HALCON sub_pixel：匹配结果亚像素精度模式。</summary>
public enum ContourMatchSubPixel
{
    None = 0,
    Interpolation = 1,
    LeastSquares = 2
}

/// <summary>HALCON optimization：模型轮廓点约简方式。</summary>
public enum ContourMatchOptimization
{
    Auto = 0,
    None = 1,
    PointReductionLow = 2,
    PointReductionMedium = 3,
    PointReductionHigh = 4
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
    /// <summary>HALCON min_contrast（1..255，默认 5）：忽略低于此对比度的边缘，抑制噪点与弱边。</summary>
    public int MinContrast { get; init; } = 5;
    /// <summary>HALCON optimization：模型轮廓点约简方式，默认 auto。</summary>
    public ContourMatchOptimization Optimization { get; init; } = ContourMatchOptimization.Auto;

    // 运行参数（原件 56 项 ParamItem）
    public double MinScore { get; init; } = 0.8;
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
    /// <summary>原件 MultiModelMaxOverlap：默认 50，只在多模板匹配时用于跨模型候选去重。</summary>
    public int MultiModelMaxOverlap { get; init; } = 50;
    public ContourMatchSortType SortType { get; init; } = ContourMatchSortType.Score;
    /// <summary>HALCON greediness（0..1，默认 0.9）：越大越快、候选越少。</summary>
    public double Greediness { get; init; } = 0.9;
    /// <summary>HALCON sub_pixel：匹配结果亚像素精度模式，默认 least_squares。</summary>
    public ContourMatchSubPixel SubPixel { get; init; } = ContourMatchSubPixel.LeastSquares;
    /// <summary>搜索域延拓比例 0..90（按图像短边百分比）；0 表示不延拓。</summary>
    public int MatchExtentRate { get; init; }
    /// <summary>原件 TimeOut：超时控制 0..10000 秒，默认 0（不限）。执行入口用 linked CTS 实现超时取消。</summary>
    public int TimeOut { get; init; }
    public bool OutlineEnabled { get; init; } = true;
    /// <summary>原件 OutLinePointEnable：轮廓点使能，默认 False。与轮廓使能取或，共同驱动逐点明细输出。</summary>
    public bool OutlinePointsEnabled { get; init; }
    /// <summary>原件 UseMatchAllMode：全部搜索模式，默认 False；开启后参与搜索的模板集合扩展为全部模板。</summary>
    public bool UseMatchAllMode { get; init; }
    /// <summary>原件 RangeScaleType：尺度范围类型，0=等比、1=X/Y 独立（走 HALCON 各向异性模型）。</summary>
    public ContourMatchRangeScaleType RangeScaleType { get; init; }
    /// <summary>原件 OKWhenNumIsZero：匹配个数为 0 时判定 OK，默认 False。</summary>
    public bool OkWhenCountIsZero { get; init; }


    // 结果判断（原件限位组）
    public bool CountLimitEnabled { get; init; }
    public int CountLimitLow { get; init; }
    public int CountLimitHigh { get; init; } = 99_999;
    public bool AngleLimitEnabled { get; init; }
    public double AngleLimitLow { get; init; } = 0;
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
    public double MatchPointXLimitLow { get; init; } = 0;
    public double MatchPointXLimitHigh { get; init; } = 99_999;
    public bool MatchPointYLimitEnabled { get; init; }
    public double MatchPointYLimitLow { get; init; } = 0;
    public double MatchPointYLimitHigh { get; init; } = 99_999;
    public bool BoxPointXLimitEnabled { get; init; }
    public double BoxPointXLimitLow { get; init; } = 0;
    public double BoxPointXLimitHigh { get; init; } = 99_999;
    public bool BoxPointYLimitEnabled { get; init; }
    public double BoxPointYLimitLow { get; init; } = 0;
    public double BoxPointYLimitHigh { get; init; } = 99_999;

    /// <summary>冻结只做文本规范化，不改数值；可执行性由 <see cref="Validate"/> 明确判定。</summary>
    public ContourMatchSettings Freeze() => this with { ModelData = ModelData?.Trim() ?? "" };

    /// <summary>枚举 → HALCON optimization 字符串。</summary>
    public static string HalconOptimization(ContourMatchOptimization value) => value switch
    {
        ContourMatchOptimization.None => "none",
        ContourMatchOptimization.PointReductionLow => "point_reduction_low",
        ContourMatchOptimization.PointReductionMedium => "point_reduction_medium",
        ContourMatchOptimization.PointReductionHigh => "point_reduction_high",
        _ => "auto"
    };

    /// <summary>枚举 → HALCON sub_pixel 字符串。</summary>
    public static string HalconSubPixel(ContourMatchSubPixel value) => value switch
    {
        ContourMatchSubPixel.None => "none",
        ContourMatchSubPixel.Interpolation => "interpolation",
        _ => "least_squares"
    };

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
        if (MinContrast is < 1 or > 255) Fail("CONTOURMATCH_SETTINGS_INVALID", "最小对比度必须在 1 到 255 之间。", ContourMatchFields.MinContrast);
        if (!Enum.IsDefined(PyramidMode)) Fail("CONTOURMATCH_SETTINGS_INVALID", "金字塔比例取值非法。", ContourMatchFields.PyramidMode);
        if (!Enum.IsDefined(EdgeThresholdMode)) Fail("CONTOURMATCH_SETTINGS_INVALID", "阈值模式取值非法。", ContourMatchFields.EdgeThresholdMode);
        if (!Enum.IsDefined(Optimization)) Fail("CONTOURMATCH_SETTINGS_INVALID", "轮廓优化取值非法。", ContourMatchFields.Optimization);
        if (!Enum.IsDefined(SubPixel)) Fail("CONTOURMATCH_SETTINGS_INVALID", "亚像素模式取值非法。", ContourMatchFields.SubPixel);
        if (!Enum.IsDefined(SortType)) Fail("CONTOURMATCH_SETTINGS_INVALID", "排序方式取值非法。", ContourMatchFields.SortType);

        // 运行参数
        if (MinScore is < 0 or > 1) Fail("CONTOURMATCH_SETTINGS_INVALID", "最小分数必须在 0 到 1 之间。", ContourMatchFields.MinScore);
        if (MaxMatchCount is < 1 or > 50_000) Fail("CONTOURMATCH_SETTINGS_INVALID", "最大匹配个数必须在 1 到 50000 之间。", ContourMatchFields.MaxMatchCount);
        if (MaxOverlap is < 0 or > 100) Fail("CONTOURMATCH_SETTINGS_INVALID", "最大重叠率必须在 0 到 100 之间。", ContourMatchFields.MaxOverlap);
        if (AngleStart is < -180 or > 180 || AngleEnd is < -180 or > 180) Fail("CONTOURMATCH_SETTINGS_INVALID", "角度范围必须在 -180 到 180 度之间。", ContourMatchFields.AngleStart);
        else if (AngleStart > AngleEnd) Fail("CONTOURMATCH_SETTINGS_INVALID", "角度范围下限不能超过上限。", ContourMatchFields.AngleStart);
        if (Polarity is not (0 or 1))
            Fail("CONTOURMATCH_SETTINGS_INVALID", "匹配极性必须是 0（忽略）或 1（考虑）。", ContourMatchFields.Polarity);
        // 原厂 AlgorithmTab.xml RangScale：MinValue 0.1 / MaxValue 10 / Inc 0.01（本方曾误限 0.5~2.0）。
        if (ScaleStart < 0.1 || ScaleEnd > 10 || ScaleStart > ScaleEnd)
            Fail("CONTOURMATCH_SETTINGS_INVALID", "尺度搜索范围必须在 0.1~10 且下限不超过上限。", ContourMatchFields.ScaleStart);
        if (Greediness is < 0 or > 1) Fail("CONTOURMATCH_SETTINGS_INVALID", "贪婪度必须在 0 到 1 之间。", ContourMatchFields.Greediness);
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
}

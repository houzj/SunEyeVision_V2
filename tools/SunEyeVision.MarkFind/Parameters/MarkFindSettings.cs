using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.MarkFind.Runtime;

/// <summary>
/// 原件「金字塔比例」（MarkFindDialogData.PyramidscaleModeSel，0=自动、1=手动）。
/// 自动时不使用压缩比/特征尺度；手动时两者同时生效且特征尺度不得超过压缩比。
/// </summary>
public enum MarkFindPyramidMode
{
    Auto = 0,
    Manual = 1
}

/// <summary>原件「阈值模式」（MarkFindDialogData.ThresholdModeSel，0=自动、1=手动）。</summary>
public enum MarkFindThresholdMode
{
    Auto = 0,
    Manual = 1
}

/// <summary>
/// 原件「定位方式」（FeatureMatchType，0=IMVSFastFeatureMatchModu、1=IMVSHPFeatureMatchModu）。
/// 快速匹配不显示特征尺度（MarkFindDialogUI.xaml.cs:1367 把第 3 行高度置 0）。
/// </summary>
public enum MarkFindMatchMode
{
    Fast = 0,
    HighPrecision = 1
}

/// <summary>
/// 原件「Mark类型」（0=自动判断、1=空心十字、2=实心十字、3=方框）。
/// 标记类型为矩形时原件强制为方框（MarkFindDialogUI.xaml.cs:1328 SetMarkMode）。
/// </summary>
public enum MarkFindMarkType
{
    Auto = 0,
    HollowCross = 1,
    SolidCross = 2,
    Rectangle = 3
}

/// <summary>原件「标记类型」（MaskType，0=十字标记、1=矩形标记），决定模板 ROI 的形状。</summary>
public enum MarkFindMaskType
{
    Cross = 0,
    Rectangle = 1
}

/// <summary>
/// 原件排序方式（SortType，默认 2）。同族 Blob 工具已证 1 升序 / 2 降序 / 3 不排序，
/// 本工具按分数排序，取值沿用同一数字语义。
/// </summary>
public enum MarkFindSortType
{
    Ascend = 1,
    Descend = 2,
    Unsorted = 3
}

/// <summary>
/// 冻结的算法快照。字段顺序与原件参数一致：模板参数来自
/// <c>ModuleControls/IMVSMarkFindModuControl/Apps.XmlParser.ParamTab/MarkFindDialogData.cs</c>，
/// 运行/判断参数来自 <c>docs/07-工具逆向/tools/imvsmarkfindmodu/parameters.json</c> 的 47 项 ParamItem。
/// 属性名按产品命名规范改写，与原厂名称的对应关系由 <see cref="MarkFindFields"/> 统一给出。
/// </summary>
public sealed record MarkFindSettings : IToolSettings<MarkFindSettings>
{
    // 模板参数（原件模型配置窗口，MarkFindDialogData 默认值）
    /// <summary>原件 ModelData + MarkModelData 的中立承载：本工具模板数据（见 MarkFindTemplate）。</summary>
    public string ModelData { get; init; } = "";
    public MarkFindPyramidMode PyramidScaleMode { get; init; } = MarkFindPyramidMode.Auto;
    public int RoughPyramidScale { get; init; } = 3;
    public int FinePyramidScale { get; init; } = 3;
    public MarkFindThresholdMode ThresholdMode { get; init; } = MarkFindThresholdMode.Auto;
    public int HighThreshold { get; init; } = 20;
    public MarkFindMatchMode FeatureMatchType { get; init; } = MarkFindMatchMode.HighPrecision;
    public int RotationAngle { get; init; } = 10;
    public double RotationStep { get; init; } = 0.5;
    public int ProjectionInterval { get; init; } = 3;
    public MarkFindMarkType MarkType { get; init; } = MarkFindMarkType.SolidCross;
    public MarkFindMaskType MaskType { get; init; } = MarkFindMaskType.Cross;

    // 运行参数（原件 47 项 ParamItem）
    public double MinScore { get; init; } = 0.5;
    public int MaxMatchCount { get; init; } = 1;
    public int Polarity { get; init; } = 1;
    public int AngleStart { get; init; } = -45;
    public int AngleEnd { get; init; } = 45;
    public double ScaleStart { get; init; } = 1;
    public double ScaleEnd { get; init; } = 1;
    public double ScaleXStart { get; init; } = 1;
    public double ScaleXEnd { get; init; } = 1;
    public double ScaleYStart { get; init; } = 1;
    public double ScaleYEnd { get; init; } = 1;
    public int MaxOverlap { get; init; } = 80;
    public MarkFindSortType SortType { get; init; } = MarkFindSortType.Descend;
    /// <summary>原件 MatchThresholdFlag：默认 0（关闭）。取值语义未取证，非 0 明确拒绝。</summary>
    public int MatchThresholdMode { get; init; }
    public int MatchThreshold { get; init; } = 40;
    /// <summary>原件 SpotterFlag：默认 False。语义未取证，启用即拒绝。</summary>
    public bool SpotterEnabled { get; init; }
    /// <summary>原件 MatchExtentRate：默认 0。语义未取证，非 0 即拒绝。</summary>
    public int MatchExtentRate { get; init; }
    /// <summary>原件 TimeOut：默认 0（不限）。时间语义未取证，非 0 即拒绝。</summary>
    public int TimeOut { get; init; }
    /// <summary>原件 OutLineEnable：默认 True，输出匹配轮廓点。</summary>
    public bool OutlineEnabled { get; init; } = true;
    /// <summary>原件 MarkLocationType：默认 0。取值语义未取证，非 0 即拒绝。</summary>
    public int MarkLocationType { get; init; }

    // 结果判断（原件限位组）
    public bool CountLimitEnabled { get; init; }
    public int CountLimitLow { get; init; }
    public int CountLimitHigh { get; init; } = 99_999;
    public bool AngleLimitEnabled { get; init; }
    public double AngleLimitLow { get; init; } = -180;
    public double AngleLimitHigh { get; init; } = 180;
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
    public MarkFindSettings Freeze() => this with { ModelData = ModelData?.Trim() ?? "" };

    /// <summary>参数组合校验；任何一项不成立都返回明确诊断，不做静默降级。</summary>
    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Fail(string code, string message, string? fieldId = null) => errors.Add(new(code, message, fieldId));
        void Range(string enabledName, bool enabled, string minName, double minimum, string maxName, double maximum, string name)
        {
            if (!enabled) return;
            if (!double.IsFinite(minimum) || !double.IsFinite(maximum)) Fail("MARKFIND_SETTINGS_INVALID", name + "范围必须是有限数。", minName);
            else if (minimum > maximum) Fail("MARKFIND_SETTINGS_INVALID", name + "下限不能超过上限。", minName);
            _ = maxName;
        }

        if (RoughPyramidScale is < 1 or > 20) Fail("MARKFIND_SETTINGS_INVALID", "压缩比必须在 1 到 20 之间。", MarkFindFields.RoughPyramidScale);
        if (FinePyramidScale is < 1 or > 20) Fail("MARKFIND_SETTINGS_INVALID", "特征尺度必须在 1 到 20 之间。", MarkFindFields.FinePyramidScale);
        if (PyramidScaleMode == MarkFindPyramidMode.Manual && FinePyramidScale > RoughPyramidScale)
            Fail("MARKFIND_SETTINGS_INVALID", "特征尺度不能大于压缩比。", MarkFindFields.FinePyramidScale);
        if (HighThreshold is < 1 or > 255) Fail("MARKFIND_SETTINGS_INVALID", "对比度阈值必须在 1 到 255 之间。", MarkFindFields.HighThreshold);
        if (RotationAngle is < 5 or > 45) Fail("MARKFIND_SETTINGS_INVALID", "旋转角度必须在 5 到 45 度之间。", MarkFindFields.RotationAngle);
        if (RotationStep is < 0.1 or > 1) Fail("MARKFIND_SETTINGS_INVALID", "旋转步长必须在 0.1 到 1 度之间。", MarkFindFields.RotationStep);
        if (!Enum.IsDefined(PyramidScaleMode)) Fail("MARKFIND_SETTINGS_INVALID", "金字塔比例取值非法。", MarkFindFields.PyramidScaleMode);
        if (!Enum.IsDefined(ThresholdMode)) Fail("MARKFIND_SETTINGS_INVALID", "阈值模式取值非法。", MarkFindFields.ThresholdMode);
        if (!Enum.IsDefined(FeatureMatchType)) Fail("MARKFIND_SETTINGS_INVALID", "定位方式取值非法。", MarkFindFields.FeatureMatchType);
        if (!Enum.IsDefined(MarkType)) Fail("MARKFIND_SETTINGS_INVALID", "Mark类型取值非法。", MarkFindFields.MarkType);
        if (!Enum.IsDefined(MaskType)) Fail("MARKFIND_SETTINGS_INVALID", "标记类型取值非法。", MarkFindFields.MaskType);
        if (!Enum.IsDefined(SortType)) Fail("MARKFIND_SETTINGS_INVALID", "排序方式取值非法。", MarkFindFields.SortType);

        if (MinScore is < 0 or > 1) Fail("MARKFIND_SETTINGS_INVALID", "最小分数必须在 0 到 1 之间。", MarkFindFields.MinScore);
        if (MaxMatchCount is < 1 or > 1000) Fail("MARKFIND_SETTINGS_INVALID", "最大匹配数必须在 1 到 1000 之间。", MarkFindFields.MaxMatchCount);
        if (MaxOverlap is < 0 or > 100) Fail("MARKFIND_SETTINGS_INVALID", "最大重叠率必须在 0 到 100 之间。", MarkFindFields.MaxOverlap);
        if (AngleStart is < -180 or > 180 || AngleEnd is < -180 or > 180) Fail("MARKFIND_SETTINGS_INVALID", "角度范围必须在 -180 到 180 度之间。", MarkFindFields.AngleStart);
        else if (AngleStart > AngleEnd) Fail("MARKFIND_SETTINGS_INVALID", "角度范围下限不能超过上限。", MarkFindFields.AngleStart);

        // 未取证语义的取值一律显式拒绝，不静默忽略（rule-015）。
        if (Polarity != 1)
            Fail("MARKFIND_POLARITY_NOT_READY", "极性取值语义尚未取证，暂只支持默认值 1。", MarkFindFields.Polarity);
        if (ScaleStart != 1 || ScaleEnd != 1)
            Fail("MARKFIND_SCALE_NOT_READY", "缩放范围依赖原件缩放搜索语义，HALCON 形状模板不支持，暂只支持 1。", MarkFindFields.ScaleStart);
        if (ScaleXStart != 1 || ScaleXEnd != 1 || ScaleYStart != 1 || ScaleYEnd != 1)
            Fail("MARKFIND_SCALE_NOT_READY", "X/Y 缩放范围依赖原件缩放搜索语义，暂只支持 1。", MarkFindFields.ScaleXStart);
        if (MatchThresholdMode != 0)
            Fail("MARKFIND_MATCH_THRESHOLD_NOT_READY", "匹配阈值使能语义尚未取证，暂只支持关闭（0）。", MarkFindFields.MatchThresholdMode);
        if (SpotterEnabled)
            Fail("MARKFIND_SPOTTER_NOT_READY", "斑点定位语义尚未取证，暂不支持启用。", MarkFindFields.SpotterEnabled);
        if (MatchExtentRate != 0)
            Fail("MARKFIND_MATCH_EXTENT_NOT_READY", "匹配范围比例语义尚未取证，暂只支持 0。", MarkFindFields.MatchExtentRate);
        if (TimeOut != 0)
            Fail("MARKFIND_TIMEOUT_NOT_READY", "时间限制语义尚未取证，暂只支持 0（不限）。", MarkFindFields.TimeOut);
        if (MarkLocationType != 0)
            Fail("MARKFIND_LOCATION_TYPE_NOT_READY", "标记定位方式语义尚未取证，暂只支持 0。", MarkFindFields.MarkLocationType);
        if (ProjectionInterval != 3)
            Fail("MARKFIND_PROJECTION_NOT_READY", "投影区间是原件模板内部构建参数，HALCON 形状模板无对应语义，暂只支持默认值 3。", MarkFindFields.ProjectionInterval);
        if (MarkType != MarkFindMarkType.SolidCross)
            Fail("MARKFIND_MARK_TYPE_NOT_READY", "空心十字/方框依赖原件标记模板语义，暂只支持实心十字（默认）。", MarkFindFields.MarkType);
        if (MaskType != MarkFindMaskType.Cross)
            Fail("MARKFIND_MASK_TYPE_NOT_READY", "矩形标记依赖原件矩形模板语义，暂只支持十字标记（默认）。", MarkFindFields.MaskType);

        Range(nameof(CountLimitEnabled), CountLimitEnabled, nameof(CountLimitLow), CountLimitLow, nameof(CountLimitHigh), CountLimitHigh, "匹配个数判断");
        Range(nameof(AngleLimitEnabled), AngleLimitEnabled, nameof(AngleLimitLow), AngleLimitLow, nameof(AngleLimitHigh), AngleLimitHigh, "角度判断");
        Range(nameof(ScoreLimitEnabled), ScoreLimitEnabled, nameof(ScoreLimitLow), ScoreLimitLow, nameof(ScoreLimitHigh), ScoreLimitHigh, "分数判断");
        Range(nameof(ScaleXLimitEnabled), ScaleXLimitEnabled, nameof(ScaleXLimitLow), ScaleXLimitLow, nameof(ScaleXLimitHigh), ScaleXLimitHigh, "X缩放判断");
        Range(nameof(ScaleYLimitEnabled), ScaleYLimitEnabled, nameof(ScaleYLimitLow), ScaleYLimitLow, nameof(ScaleYLimitHigh), ScaleYLimitHigh, "Y缩放判断");
        Range(nameof(MatchPointXLimitEnabled), MatchPointXLimitEnabled, nameof(MatchPointXLimitLow), MatchPointXLimitLow, nameof(MatchPointXLimitHigh), MatchPointXLimitHigh, "匹配点X判断");
        Range(nameof(MatchPointYLimitEnabled), MatchPointYLimitEnabled, nameof(MatchPointYLimitLow), MatchPointYLimitLow, nameof(MatchPointYLimitHigh), MatchPointYLimitHigh, "匹配点Y判断");
        Range(nameof(BoxPointXLimitEnabled), BoxPointXLimitEnabled, nameof(BoxPointXLimitLow), BoxPointXLimitLow, nameof(BoxPointXLimitHigh), BoxPointXLimitHigh, "Box点X判断");
        Range(nameof(BoxPointYLimitEnabled), BoxPointYLimitEnabled, nameof(BoxPointYLimitLow), BoxPointYLimitLow, nameof(BoxPointYLimitHigh), BoxPointYLimitHigh, "Box点Y判断");
        if (CountLimitEnabled && CountLimitLow < 0) Fail("MARKFIND_SETTINGS_INVALID", "匹配个数判断下限不能为负。", MarkFindFields.CountLimitLow);
        if (ScoreLimitEnabled && (ScoreLimitLow < 0 || ScoreLimitHigh > 1))
            Fail("MARKFIND_SETTINGS_INVALID", "分数判断范围必须在 0 到 1 之间。", MarkFindFields.ScoreLimitLow);
        return errors.AsReadOnly();
    }
}

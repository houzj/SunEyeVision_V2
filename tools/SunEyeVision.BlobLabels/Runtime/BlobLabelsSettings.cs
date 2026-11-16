using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobLabels.Runtime;

/// <summary>
/// 排序特征。取值与原厂 ParamItem <c>SortFeature</c> 的数字一致：
/// 原厂默认 1；同族 Blob 工具已证语义顺序 1..13（面积…轴比），本工具沿用同一参数模型。
/// </summary>
public enum BlobLabelsSortFeature
{
    SortFeatureArea = 1,
    SortFeaturePerimeter = 2,
    SortFeatureCircularity = 3,
    SortFeatureRect = 4,
    SortFeatureCentroidX = 5,
    SortFeatureCentroidY = 6,
    SortFeatureBoxAngle = 7,
    SortFeatureBoxWidth = 8,
    SortFeatureBoxHeight = 9,
    SortFeatureRectX = 10,
    SortFeatureRectY = 11,
    SortFeatureAxisAngle = 12,
    SortFeatureAxisRatio = 13
}

/// <summary>排序方式。原厂默认 2 = 降序（同族 Blob 工具已证 1 升序 / 2 降序 / 3 不排序）。</summary>
public enum BlobLabelsSortMode
{
    SortModeAscend = 1,
    SortModeDecend = 2,
    SortModeNotSort = 3
}

/// <summary>连通性。原厂默认 8 = 8 连通；4 连通为 4。</summary>
public enum BlobLabelsConnectivity
{
    Connected_8 = 8,
    Connected_4 = 4
}

/// <summary>
/// 一条标签阈值项。编码格式来自原件
/// <c>ModuleControls/IMVSBlobFindLabelsModuControl/IMVSBlobFindLabelsModuControl/ThresholdAreaItem.cs:126</c>：
/// <c>序号#是否新增#灰度值#面积下限#面积上限#是否反选</c>。
/// </summary>
public sealed record BlobLabelsThresholdArea(int Index, bool IsAdd, int GrayValue, int MinimumArea, int MaximumArea, bool IsInverse)
{
    private const char Separator = '#';

    public string Encode()
        => string.Join(Separator, Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            IsAdd ? "1" : "0",
            GrayValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            MinimumArea.ToString(System.Globalization.CultureInfo.InvariantCulture),
            MaximumArea.ToString(System.Globalization.CultureInfo.InvariantCulture),
            IsInverse ? "1" : "0");

    public static BlobLabelsThresholdArea Decode(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parts = text.Split(Separator);
        if (parts.Length != 6)
            throw new ArgumentException("阈值区域编码必须为 6 段：序号#新增#灰度#下限#上限#反选。");
        int Parse(int index, string name, int minimum, int maximum)
        {
            if (!int.TryParse(parts[index], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                || value < minimum || value > maximum)
                throw new ArgumentException($"阈值区域{name}超出允许范围：{parts[index]}");
            return value;
        }
        bool Flag(int index, string name) => parts[index] switch
        {
            "0" => false,
            "1" => true,
            _ => throw new ArgumentException($"阈值区域{name}必须为 0 或 1：{parts[index]}")
        };
        var minimumArea = Parse(3, "面积下限", 1, 999_999_999);
        var maximumArea = Parse(4, "面积上限", 1, 999_999_999);
        if (minimumArea > maximumArea) throw new ArgumentException("阈值区域面积下限不能超过上限。");
        return new(Parse(0, "序号", 1, BlobLabelsThresholdAreas.MaximumCount), Flag(1, "新增标记"),
            Parse(2, "灰度值", 1, 255), minimumArea, maximumArea, Flag(5, "反选标记"));
    }
}

/// <summary>
/// 冻结的算法快照。字段顺序与原厂 ParamItem 顺序一致；
/// 属性名按产品命名规范改写，与原厂名称的对应关系由 <see cref="BlobLabelsFields"/> 的表统一给出。
/// </summary>
public sealed record BlobLabelsSettings : IToolSettings<BlobLabelsSettings>
{
    /// <summary>原厂 <c>UserStringData["ThresholdArea"]</c>：多行编码，换行分隔。</summary>
    public string ThresholdArea { get; init; } = "";
    /// <summary>原厂 <c>LabelWiseAreaSelectEnable</c>：标签级面积使能（默认值未取证，按未启用保存）。</summary>
    public bool LabelWiseAreaSelectEnabled { get; init; }
    /// <summary>原厂 <c>ClassName</c>：类别名称（ClassInfo 输入语义未取证，非空即拒绝执行）。</summary>
    public string ClassName { get; init; } = "";
    /// <summary>原厂 <c>GrayValue</c>：类别灰度值（同上，未接入）。</summary>
    public int ClassGrayValue { get; init; }

    public int FindCount { get; init; } = 100;

    public bool AreaFilterEnabled { get; init; }
    public int AreaFilterMin { get; init; } = 10;
    public int AreaFilterMax { get; init; } = 999_999_999;
    public bool ContourOutputEnabled { get; init; }
    public bool BlobImageOutputEnabled { get; init; } = true;

    public bool PerimeterFilterEnabled { get; init; }
    public int PerimeterFilterMin { get; init; } = 10;
    public int PerimeterFilterMax { get; init; } = 999_999_999;
    public bool ShortAxisFilterEnabled { get; init; }
    public int ShortAxisFilterMin { get; init; } = 1;
    public int ShortAxisFilterMax { get; init; } = 999_999_999;
    public bool LongAxisFilterEnabled { get; init; }
    public int LongAxisFilterMin { get; init; } = 10;
    public int LongAxisFilterMax { get; init; } = 999_999_999;
    public bool CircularityFilterEnabled { get; init; }
    public double CircularityFilterMin { get; init; } = 0.1;
    public double CircularityFilterMax { get; init; } = 1;
    public bool RectangularityFilterEnabled { get; init; }
    public double RectangularityFilterMin { get; init; } = 0.1;
    public double RectangularityFilterMax { get; init; } = 1;
    public bool CenterBiasFilterEnabled { get; init; }
    public double CenterBiasFilterMin { get; init; }
    public double CenterBiasFilterMax { get; init; } = 9_999_999.99;
    public bool AxisRatioFilterEnabled { get; init; }
    public double AxisRatioFilterMin { get; init; } = 0.01;
    public double AxisRatioFilterMax { get; init; } = 1;

    public BlobLabelsSortFeature SortFeature { get; init; } = BlobLabelsSortFeature.SortFeatureArea;
    public BlobLabelsSortMode SortMode { get; init; } = BlobLabelsSortMode.SortModeDecend;
    public BlobLabelsConnectivity Connectivity { get; init; } = BlobLabelsConnectivity.Connected_8;
    /// <summary>原厂 <c>OverlapRatio</c>：最小重叠率（执行语义未取证，非 0 即拒绝执行）。</summary>
    public int MinOverlapRatio { get; init; }
    /// <summary>原厂 <c>MaxOutPixelNumEnable</c>/<c>MaxOutPixelNum</c>：像素超界（语义未取证，启用即拒绝执行）。</summary>
    public bool MaxOutsidePixelEnabled { get; init; }
    public int MaxOutsidePixels { get; init; }

    public bool BlobCountJudgeEnabled { get; init; }
    public int BlobCountJudgeMin { get; init; } = 1;
    public int BlobCountJudgeMax { get; init; } = 99_999;
    public bool BlobAreaJudgeEnabled { get; init; }
    public double BlobAreaJudgeMin { get; init; } = 1;
    public double BlobAreaJudgeMax { get; init; } = 999_999_999;
    public bool TotalAreaJudgeEnabled { get; init; }
    public double TotalAreaJudgeMin { get; init; } = 1;
    public double TotalAreaJudgeMax { get; init; } = 999_999_999;

    public bool CentroidXJudgeEnabled { get; init; }
    public double CentroidXJudgeMin { get; init; } = -99_999;
    public double CentroidXJudgeMax { get; init; } = 99_999;
    public bool CentroidYJudgeEnabled { get; init; }
    public double CentroidYJudgeMin { get; init; } = -99_999;
    public double CentroidYJudgeMax { get; init; } = 99_999;
    public bool BoxAngleJudgeEnabled { get; init; }
    public double BoxAngleJudgeMin { get; init; } = -180;
    public double BoxAngleJudgeMax { get; init; } = 180;
    public bool CenterXJudgeEnabled { get; init; }
    public double CenterXJudgeMin { get; init; } = -99_999;
    public double CenterXJudgeMax { get; init; } = 99_999;
    public bool CenterYJudgeEnabled { get; init; }
    public double CenterYJudgeMin { get; init; } = -99_999;
    public double CenterYJudgeMax { get; init; } = 99_999;
    public bool BoxHeightJudgeEnabled { get; init; }
    public double BoxHeightJudgeMin { get; init; } = 1;
    public double BoxHeightJudgeMax { get; init; } = 99_999;
    public bool BoxWidthJudgeEnabled { get; init; }
    public double BoxWidthJudgeMin { get; init; } = 1;
    public double BoxWidthJudgeMax { get; init; } = 99_999;
    public bool PerimeterJudgeEnabled { get; init; }
    public double PerimeterJudgeMin { get; init; } = 1;
    public double PerimeterJudgeMax { get; init; } = 999_999_999;
    public bool LongAxisJudgeEnabled { get; init; }
    public double LongAxisJudgeMin { get; init; } = 1;
    public double LongAxisJudgeMax { get; init; } = 999_999_999;
    public bool ShortAxisJudgeEnabled { get; init; }
    public double ShortAxisJudgeMin { get; init; } = 1;
    public double ShortAxisJudgeMax { get; init; } = 999_999_999;
    public bool CircularityJudgeEnabled { get; init; }
    public double CircularityJudgeMin { get; init; }
    public double CircularityJudgeMax { get; init; } = 1;
    public bool RectangularityJudgeEnabled { get; init; }
    public double RectangularityJudgeMin { get; init; }
    public double RectangularityJudgeMax { get; init; } = 1;

    /// <summary>冻结：只做文本规范化，不改数值；可执行性由 <see cref="Validate"/> 明确判定。</summary>
    public BlobLabelsSettings Freeze()
        => this with
        {
            ThresholdArea = ThresholdArea?.Trim() ?? "",
            ClassName = ClassName?.Trim() ?? ""
        };

    public IReadOnlyList<BlobLabelsThresholdArea> ReadThresholdAreas()
        => BlobLabelsThresholdAreas.Parse(ThresholdArea);

    /// <summary>参数组合校验；任何一项不成立都返回明确诊断，不做静默降级。</summary>
    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Fail(string code, string message, string? fieldId = null) => errors.Add(new(code, message, fieldId));
        void Range(string enabledField, bool enabled, string minField, double minimum, string maxField, double maximum, string name)
        {
            if (!enabled) return;
            if (!double.IsFinite(minimum) || !double.IsFinite(maximum)) Fail("BLOBLABEL_SETTINGS_INVALID", name + "范围必须是有限数。", minField);
            else if (minimum > maximum) Fail("BLOBLABEL_SETTINGS_INVALID", name + "下限不能超过上限。", minField);
            _ = maxField;
        }

        if (FindCount < 1 || FindCount > 20_000) Fail("BLOBLABEL_SETTINGS_INVALID", "查找个数必须在 1 到 20000 之间。", BlobLabelsFields.FindCount);
        Range(nameof(AreaFilterEnabled), AreaFilterEnabled, nameof(AreaFilterMin), AreaFilterMin, nameof(AreaFilterMax), AreaFilterMax, "面积筛选");
        Range(nameof(PerimeterFilterEnabled), PerimeterFilterEnabled, nameof(PerimeterFilterMin), PerimeterFilterMin, nameof(PerimeterFilterMax), PerimeterFilterMax, "周长筛选");
        Range(nameof(ShortAxisFilterEnabled), ShortAxisFilterEnabled, nameof(ShortAxisFilterMin), ShortAxisFilterMin, nameof(ShortAxisFilterMax), ShortAxisFilterMax, "短轴筛选");
        Range(nameof(LongAxisFilterEnabled), LongAxisFilterEnabled, nameof(LongAxisFilterMin), LongAxisFilterMin, nameof(LongAxisFilterMax), LongAxisFilterMax, "长轴筛选");
        Range(nameof(CircularityFilterEnabled), CircularityFilterEnabled, nameof(CircularityFilterMin), CircularityFilterMin, nameof(CircularityFilterMax), CircularityFilterMax, "圆形度筛选");
        Range(nameof(RectangularityFilterEnabled), RectangularityFilterEnabled, nameof(RectangularityFilterMin), RectangularityFilterMin, nameof(RectangularityFilterMax), RectangularityFilterMax, "矩形度筛选");
        Range(nameof(CenterBiasFilterEnabled), CenterBiasFilterEnabled, nameof(CenterBiasFilterMin), CenterBiasFilterMin, nameof(CenterBiasFilterMax), CenterBiasFilterMax, "质心偏移筛选");
        Range(nameof(AxisRatioFilterEnabled), AxisRatioFilterEnabled, nameof(AxisRatioFilterMin), AxisRatioFilterMin, nameof(AxisRatioFilterMax), AxisRatioFilterMax, "轴比筛选");
        Range(nameof(BlobCountJudgeEnabled), BlobCountJudgeEnabled, nameof(BlobCountJudgeMin), BlobCountJudgeMin, nameof(BlobCountJudgeMax), BlobCountJudgeMax, "Blob个数判断");
        Range(nameof(BlobAreaJudgeEnabled), BlobAreaJudgeEnabled, nameof(BlobAreaJudgeMin), BlobAreaJudgeMin, nameof(BlobAreaJudgeMax), BlobAreaJudgeMax, "Blob面积判断");
        Range(nameof(TotalAreaJudgeEnabled), TotalAreaJudgeEnabled, nameof(TotalAreaJudgeMin), TotalAreaJudgeMin, nameof(TotalAreaJudgeMax), TotalAreaJudgeMax, "总面积判断");
        Range(nameof(CentroidXJudgeEnabled), CentroidXJudgeEnabled, nameof(CentroidXJudgeMin), CentroidXJudgeMin, nameof(CentroidXJudgeMax), CentroidXJudgeMax, "质心X判断");
        Range(nameof(CentroidYJudgeEnabled), CentroidYJudgeEnabled, nameof(CentroidYJudgeMin), CentroidYJudgeMin, nameof(CentroidYJudgeMax), CentroidYJudgeMax, "质心Y判断");
        Range(nameof(BoxAngleJudgeEnabled), BoxAngleJudgeEnabled, nameof(BoxAngleJudgeMin), BoxAngleJudgeMin, nameof(BoxAngleJudgeMax), BoxAngleJudgeMax, "Box角度判断");
        Range(nameof(CenterXJudgeEnabled), CenterXJudgeEnabled, nameof(CenterXJudgeMin), CenterXJudgeMin, nameof(CenterXJudgeMax), CenterXJudgeMax, "中心X判断");
        Range(nameof(CenterYJudgeEnabled), CenterYJudgeEnabled, nameof(CenterYJudgeMin), CenterYJudgeMin, nameof(CenterYJudgeMax), CenterYJudgeMax, "中心Y判断");
        Range(nameof(BoxHeightJudgeEnabled), BoxHeightJudgeEnabled, nameof(BoxHeightJudgeMin), BoxHeightJudgeMin, nameof(BoxHeightJudgeMax), BoxHeightJudgeMax, "Box高度判断");
        Range(nameof(BoxWidthJudgeEnabled), BoxWidthJudgeEnabled, nameof(BoxWidthJudgeMin), BoxWidthJudgeMin, nameof(BoxWidthJudgeMax), BoxWidthJudgeMax, "Box宽度判断");
        Range(nameof(PerimeterJudgeEnabled), PerimeterJudgeEnabled, nameof(PerimeterJudgeMin), PerimeterJudgeMin, nameof(PerimeterJudgeMax), PerimeterJudgeMax, "周长判断");
        Range(nameof(LongAxisJudgeEnabled), LongAxisJudgeEnabled, nameof(LongAxisJudgeMin), LongAxisJudgeMin, nameof(LongAxisJudgeMax), LongAxisJudgeMax, "长轴判断");
        Range(nameof(ShortAxisJudgeEnabled), ShortAxisJudgeEnabled, nameof(ShortAxisJudgeMin), ShortAxisJudgeMin, nameof(ShortAxisJudgeMax), ShortAxisJudgeMax, "短轴判断");
        Range(nameof(CircularityJudgeEnabled), CircularityJudgeEnabled, nameof(CircularityJudgeMin), CircularityJudgeMin, nameof(CircularityJudgeMax), CircularityJudgeMax, "圆形度判断");
        Range(nameof(RectangularityJudgeEnabled), RectangularityJudgeEnabled, nameof(RectangularityJudgeMin), RectangularityJudgeMin, nameof(RectangularityJudgeMax), RectangularityJudgeMax, "矩形度判断");
        if (!Enum.IsDefined(SortFeature)) Fail("BLOBLABEL_SETTINGS_INVALID", "排序特征取值非法。", BlobLabelsFields.SortFeature);
        if (!Enum.IsDefined(SortMode)) Fail("BLOBLABEL_SETTINGS_INVALID", "排序方式取值非法。", BlobLabelsFields.SortMode);
        if (!Enum.IsDefined(Connectivity)) Fail("BLOBLABEL_SETTINGS_INVALID", "连通性取值非法。", BlobLabelsFields.Connectivity);
        if (MinOverlapRatio != 0)
            Fail("BLOBLABEL_OVERLAP_NOT_READY", "最小重叠率语义尚未取证，暂不支持非 0 取值。", BlobLabelsFields.MinOverlapRatio);
        if (MaxOutsidePixelEnabled)
            Fail("BLOBLABEL_OUTSIDE_PIXEL_NOT_READY", "像素超界语义尚未取证，暂不支持启用。", BlobLabelsFields.MaxOutsidePixelEnabled);
        if (ClassName.Length != 0 || ClassGrayValue != 0)
            Fail("BLOBLABEL_CLASS_INFO_NOT_READY", "类别信息（ClassInfo）输入尚未接入，请清空类别名称与灰度值。", BlobLabelsFields.ClassName);
        try { _ = ReadThresholdAreas(); }
        catch (ArgumentException error) { Fail("BLOBLABEL_THRESHOLD_INVALID", error.Message, BlobLabelsFields.ThresholdArea); }
        return errors.AsReadOnly();
    }
}

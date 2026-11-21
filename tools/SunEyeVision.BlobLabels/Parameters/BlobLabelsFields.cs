using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobLabels.Runtime;

/// <summary>一个原厂参数在编辑/持久化上的完整声明，不包含执行语义。</summary>
public sealed class BlobLabelsFieldSpec
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
    public bool VisibleNegate { get; init; }
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
/// BlobLabels 的参数清单。数据来源：原件 <c>IMVSBlobFindLabelsModu.xml</c> 的 78 个
/// <c>ParamItem</c>（名称与默认值逐条对应）与 <c>IMVSBlobFindLabelsModuAlgorithmTab.xml</c>
/// 的页签/分类/控件顺序（见 docs/07-工具逆向/tools/imvsblobfindlabelsmodu/parameters.json、
/// docs/08-工具预览/ui/Location-IMVSBlobFindLabelsModu.html）。
/// 字段 ID 由字段名确定性派生，保存方案时不随版本变化。
/// </summary>
public static class BlobLabelsFields
{
    public const string BasicPage = "基本参数";
    public const string RunPage = "运行参数";
    public const string ClassSection = "类别信息";
    public const string RoiSection = "ROI区域";
    public const string RunSection = "运行参数";
    public const string JudgeSection = "结果判断";

    private static readonly IReadOnlyDictionary<string, PropertyInfo> Properties =
        typeof(BlobLabelsSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, property => property, StringComparer.Ordinal);
    private static readonly IReadOnlyList<BlobLabelsFieldSpec> FieldList = Build();
    private static readonly IReadOnlyDictionary<string, BlobLabelsFieldSpec> ByName =
        new Dictionary<string, BlobLabelsFieldSpec>(FieldList.ToDictionary(item => item.Name, StringComparer.Ordinal), StringComparer.Ordinal);

    public static IReadOnlyList<BlobLabelsFieldSpec> All => FieldList;

    public static string FindCount => IdOf(nameof(BlobLabelsSettings.FindCount));
    public static string PositionCorrectionEnabled => IdOf(nameof(BlobLabelsSettings.PositionCorrectionEnabled));
    public static string ThresholdArea => IdOf(nameof(BlobLabelsSettings.ThresholdArea));
    public static string LabelWiseAreaSelectEnabled => IdOf(nameof(BlobLabelsSettings.LabelWiseAreaSelectEnabled));
    public static string ClassName => IdOf(nameof(BlobLabelsSettings.ClassName));
    public static string ClassGrayValue => IdOf(nameof(BlobLabelsSettings.ClassGrayValue));
    public static string AreaFilterEnabled => IdOf(nameof(BlobLabelsSettings.AreaFilterEnabled));
    public static string AreaFilterMin => IdOf(nameof(BlobLabelsSettings.AreaFilterMin));
    public static string AreaFilterMax => IdOf(nameof(BlobLabelsSettings.AreaFilterMax));
    public static string ContourOutputEnabled => IdOf(nameof(BlobLabelsSettings.ContourOutputEnabled));
    public static string BlobImageOutputEnabled => IdOf(nameof(BlobLabelsSettings.BlobImageOutputEnabled));
    public static string SortFeature => IdOf(nameof(BlobLabelsSettings.SortFeature));
    public static string SortMode => IdOf(nameof(BlobLabelsSettings.SortMode));
    public static string Connectivity => IdOf(nameof(BlobLabelsSettings.Connectivity));
    public static string MinOverlapRatio => IdOf(nameof(BlobLabelsSettings.MinOverlapRatio));
    public static string MaxOutsidePixelEnabled => IdOf(nameof(BlobLabelsSettings.MaxOutsidePixelEnabled));
    public static string MaxOutsidePixels => IdOf(nameof(BlobLabelsSettings.MaxOutsidePixels));

    public static string IdOf(string name)
        => ByName.TryGetValue(name, out var field) ? field.Id : throw new ArgumentException("未知的 BlobLabels 设置字段：" + name, nameof(name));

    public static BlobLabelsFieldSpec Spec(string name)
        => ByName.TryGetValue(name, out var field) ? field : throw new ArgumentException("未知的 BlobLabels 设置字段：" + name, nameof(name));

    public static BlobLabelsFieldSpec SpecByVmName(string virtualMachineName)
        => FieldList.FirstOrDefault(field => field.VirtualMachineName == virtualMachineName)
            ?? throw new ArgumentException("无关的原厂参数名：" + virtualMachineName, nameof(virtualMachineName));

    private static string StableId(string name)
        => "field:" + new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("SunEyeVision.Tool.BlobLabels:" + name)).AsSpan(0, 16)).ToString("N");

    private static BlobLabelsFieldSpec B(string vm, string name, string label, string section, bool value,
        string? when = null, bool advanced = false, string page = BasicPage)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(bool), Default = value,
            Label = label, Page = page, Section = section, VisibleWhen = when, Advanced = advanced
        };

    private static BlobLabelsFieldSpec I(string vm, string name, string label, string section, int value, double minimum, double maximum,
        string? when = null, string? pair = null, string? pairLabel = null, bool advanced = false, bool reference = true, string page = RunPage)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(int), Default = value,
            Label = label, Page = page, Section = section, VisibleWhen = when, PairWith = pair, PairLabel = pairLabel,
            Advanced = advanced, AllowReference = reference, Minimum = minimum, Maximum = maximum,
            PairMinimum = pair is null ? null : minimum, PairMaximum = pair is null ? null : maximum, DecimalPlaces = 0, Increment = 1
        };

    private static BlobLabelsFieldSpec N(string vm, string name, string label, string section, double value, double minimum, double maximum,
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

    private static BlobLabelsFieldSpec E(string vm, string name, string label, string section, Type enumType, int value,
        IEnumerable<string> keys, IEnumerable<string> labels, string page = RunPage)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = enumType,
            Default = Enum.ToObject(enumType, value), Label = label, Page = page, Section = section,
            Choices = keys.ToArray(), ChoiceLabels = keys.Zip(labels).ToDictionary(item => item.First, item => item.Second, StringComparer.Ordinal)
        };

    private static BlobLabelsFieldSpec S(string vm, string name, string label, string section, string value, bool reference = false,
        double? minimum = null, double? maximum = null, string page = BasicPage)
        => new()
        {
            VirtualMachineName = vm, Name = name, Id = StableId(name), Type = typeof(string), Default = value,
            Label = label, Page = page, Section = section, AllowReference = reference, Minimum = minimum, Maximum = maximum
        };

    private static IReadOnlyList<BlobLabelsFieldSpec> Build()
    {
        var fields = new List<BlobLabelsFieldSpec>
        {
            // 基本参数 / ROI区域末尾的 AssistGroup 开关。原厂组装链把其
            // 后续来源行挂在该开关之后，关闭时整组折叠。
            B("PositionCorrectionEnable", nameof(BlobLabelsSettings.PositionCorrectionEnabled), "位置修正", RoiSection, false),
            // 基本参数 / 类别信息（原件 AlgorithmTab 控件：ClassName、GrayValue、LabelWiseAreaSelectEnable、ThresholdAreaList）
            S("ClassName", nameof(BlobLabelsSettings.ClassName), "类别名称", ClassSection, ""),
            I("GrayValue", nameof(BlobLabelsSettings.ClassGrayValue), "灰度值", ClassSection, 0, 0, 255, reference: false, page: BasicPage),
            B("LabelWiseAreaSelectEnable", nameof(BlobLabelsSettings.LabelWiseAreaSelectEnabled), "标签面积使能", ClassSection, false),
            S("ThresholdArea", nameof(BlobLabelsSettings.ThresholdArea), "阈值区域", ClassSection, ""),

            // 运行参数 / 运行参数（原厂 ParamItem 1..6）
            I("FindNum", nameof(BlobLabelsSettings.FindCount), "查找个数", RunSection, 100, 1, 20_000),
            B("SelectByArea", nameof(BlobLabelsSettings.AreaFilterEnabled), "全局面积使能", RunSection, false),
            I("MinArea", nameof(BlobLabelsSettings.AreaFilterMin), "面积下限", RunSection, 10, 1, 999_999_999, when: nameof(BlobLabelsSettings.AreaFilterEnabled), pair: nameof(BlobLabelsSettings.AreaFilterMax), pairLabel: "全局面积范围"),
            I("MaxArea", nameof(BlobLabelsSettings.AreaFilterMax), "面积上限", RunSection, 999_999_999, 1, 999_999_999, when: nameof(BlobLabelsSettings.AreaFilterEnabled)),
            B("BolbOutLineEnable", nameof(BlobLabelsSettings.ContourOutputEnabled), "轮廓输出使能", RunSection, false),
            B("BolbImageEnable", nameof(BlobLabelsSettings.BlobImageOutputEnabled), "Blob图像输出", RunSection, true),

            // 运行参数 / 高级参数（原厂 ParamItem 7..27）
            B("SelectByPerimeter", nameof(BlobLabelsSettings.PerimeterFilterEnabled), "周长使能", RunSection, false, advanced: true),
            I("MinPerimeter", nameof(BlobLabelsSettings.PerimeterFilterMin), "周长下限", RunSection, 10, 1, 999_999_999, when: nameof(BlobLabelsSettings.PerimeterFilterEnabled), pair: nameof(BlobLabelsSettings.PerimeterFilterMax), pairLabel: "周长范围", advanced: true),
            I("MaxPerimeter", nameof(BlobLabelsSettings.PerimeterFilterMax), "周长上限", RunSection, 999_999_999, 1, 999_999_999, when: nameof(BlobLabelsSettings.PerimeterFilterEnabled), advanced: true),
            B("SelectByShortAxis", nameof(BlobLabelsSettings.ShortAxisFilterEnabled), "短轴使能", RunSection, false, advanced: true),
            I("MinShortAxis", nameof(BlobLabelsSettings.ShortAxisFilterMin), "短轴下限", RunSection, 1, 1, 999_999_999, when: nameof(BlobLabelsSettings.ShortAxisFilterEnabled), pair: nameof(BlobLabelsSettings.ShortAxisFilterMax), pairLabel: "短轴范围", advanced: true),
            I("MaxShortAxis", nameof(BlobLabelsSettings.ShortAxisFilterMax), "短轴上限", RunSection, 999_999_999, 1, 999_999_999, when: nameof(BlobLabelsSettings.ShortAxisFilterEnabled), advanced: true),
            B("SelectByLongAxis", nameof(BlobLabelsSettings.LongAxisFilterEnabled), "长轴使能", RunSection, false, advanced: true),
            I("MinLongAxis", nameof(BlobLabelsSettings.LongAxisFilterMin), "长轴下限", RunSection, 10, 1, 999_999_999, when: nameof(BlobLabelsSettings.LongAxisFilterEnabled), pair: nameof(BlobLabelsSettings.LongAxisFilterMax), pairLabel: "长轴范围", advanced: true),
            I("MaxLongAxis", nameof(BlobLabelsSettings.LongAxisFilterMax), "长轴上限", RunSection, 999_999_999, 1, 999_999_999, when: nameof(BlobLabelsSettings.LongAxisFilterEnabled), advanced: true),
            B("SelectByCircularuty", nameof(BlobLabelsSettings.CircularityFilterEnabled), "圆形度使能", RunSection, false, advanced: true),
            N("MinCircularity", nameof(BlobLabelsSettings.CircularityFilterMin), "圆形度下限", RunSection, 0.1, 0, 1, when: nameof(BlobLabelsSettings.CircularityFilterEnabled), pair: nameof(BlobLabelsSettings.CircularityFilterMax), pairLabel: "圆形度范围", advanced: true),
            N("MaxCircularity", nameof(BlobLabelsSettings.CircularityFilterMax), "圆形度上限", RunSection, 1, 0, 1, when: nameof(BlobLabelsSettings.CircularityFilterEnabled), advanced: true),
            B("SelectByRectangularity", nameof(BlobLabelsSettings.RectangularityFilterEnabled), "矩形度使能", RunSection, false, advanced: true),
            N("MinRectangularity", nameof(BlobLabelsSettings.RectangularityFilterMin), "矩形度下限", RunSection, 0.1, 0, 1, when: nameof(BlobLabelsSettings.RectangularityFilterEnabled), pair: nameof(BlobLabelsSettings.RectangularityFilterMax), pairLabel: "矩形度范围", advanced: true),
            N("MaxRectangularity", nameof(BlobLabelsSettings.RectangularityFilterMax), "矩形度上限", RunSection, 1, 0, 1, when: nameof(BlobLabelsSettings.RectangularityFilterEnabled), advanced: true),
            B("SelectByCentraBias", nameof(BlobLabelsSettings.CenterBiasFilterEnabled), "质心偏移使能", RunSection, false, advanced: true),
            N("MinCenterBias", nameof(BlobLabelsSettings.CenterBiasFilterMin), "质心偏移下限", RunSection, 0, 0, 9_999_999.99, when: nameof(BlobLabelsSettings.CenterBiasFilterEnabled), pair: nameof(BlobLabelsSettings.CenterBiasFilterMax), pairLabel: "质心偏移范围", advanced: true),
            N("MaxCenterBias", nameof(BlobLabelsSettings.CenterBiasFilterMax), "质心偏移上限", RunSection, 9_999_999.99, 0, 9_999_999.99, when: nameof(BlobLabelsSettings.CenterBiasFilterEnabled), advanced: true),
            B("AxisRatioEnable", nameof(BlobLabelsSettings.AxisRatioFilterEnabled), "轴比使能", RunSection, false, advanced: true),
            N("MinAxisRatio", nameof(BlobLabelsSettings.AxisRatioFilterMin), "轴比下限", RunSection, 0.01, 0.01, 1, when: nameof(BlobLabelsSettings.AxisRatioFilterEnabled), pair: nameof(BlobLabelsSettings.AxisRatioFilterMax), pairLabel: "轴比范围", advanced: true),
            N("MaxAxisRatio", nameof(BlobLabelsSettings.AxisRatioFilterMax), "轴比上限", RunSection, 1, 0, 1, when: nameof(BlobLabelsSettings.AxisRatioFilterEnabled), advanced: true),

            // 排序与连通性（原厂 ParamItem 28..33）
            E("SortFeature", nameof(BlobLabelsSettings.SortFeature), "排序特征", RunSection, typeof(BlobLabelsSortFeature), 1,
                Enum.GetNames<BlobLabelsSortFeature>(),
                new[] { "面积", "周长", "圆形度", "矩形度", "连通域中心x", "连通域中心y", "box角度", "box宽", "box高", "矩形左上顶点x", "矩形左上顶点y", "二阶中心距主轴角度", "轴比(box短轴/box长轴)" }),
            E("SortMode", nameof(BlobLabelsSettings.SortMode), "排序方式", RunSection, typeof(BlobLabelsSortMode), 2,
                Enum.GetNames<BlobLabelsSortMode>(), new[] { "升序", "降序", "不排序" }),
            E("connectivity", nameof(BlobLabelsSettings.Connectivity), "连通性", RunSection, typeof(BlobLabelsConnectivity), 8,
                Enum.GetNames<BlobLabelsConnectivity>(), new[] { "8连通", "4连通" }),
            I("OverlapRatio", nameof(BlobLabelsSettings.MinOverlapRatio), "最小重叠率", RunSection, 0, 0, 100, advanced: true),
            B("MaxOutPixelNumEnable", nameof(BlobLabelsSettings.MaxOutsidePixelEnabled), "像素超界使能", RunSection, false, advanced: true),
            I("MaxOutPixelNum", nameof(BlobLabelsSettings.MaxOutsidePixels), "最大超界像素", RunSection, 0, 0, int.MaxValue, when: nameof(BlobLabelsSettings.MaxOutsidePixelEnabled), advanced: true),

            // 结果判断（原厂 ParamItem 34..78）
            B("BlobNumLimitEnable", nameof(BlobLabelsSettings.BlobCountJudgeEnabled), "Blob个数判断", JudgeSection, false),
            I("BlobNumLimitLow", nameof(BlobLabelsSettings.BlobCountJudgeMin), "Blob个数下限", JudgeSection, 1, 0, 99_999, when: nameof(BlobLabelsSettings.BlobCountJudgeEnabled), pair: nameof(BlobLabelsSettings.BlobCountJudgeMax), pairLabel: "Blob个数范围"),
            I("BlobNumLimitHigh", nameof(BlobLabelsSettings.BlobCountJudgeMax), "Blob个数上限", JudgeSection, 99_999, 0, 99_999, when: nameof(BlobLabelsSettings.BlobCountJudgeEnabled)),
            B("BlobAreaLimitEnable", nameof(BlobLabelsSettings.BlobAreaJudgeEnabled), "Blob面积判断", JudgeSection, false),
            N("BlobAreaLimitLow", nameof(BlobLabelsSettings.BlobAreaJudgeMin), "Blob面积下限", JudgeSection, 1, 0, 999_999_999, when: nameof(BlobLabelsSettings.BlobAreaJudgeEnabled), pair: nameof(BlobLabelsSettings.BlobAreaJudgeMax), pairLabel: "Blob面积范围"),
            N("BlobAreaLimitHigh", nameof(BlobLabelsSettings.BlobAreaJudgeMax), "Blob面积上限", JudgeSection, 999_999_999, 0, 999_999_999, when: nameof(BlobLabelsSettings.BlobAreaJudgeEnabled)),
            B("BlobTotalAreaLimitEnable", nameof(BlobLabelsSettings.TotalAreaJudgeEnabled), "Blob总面积判断", JudgeSection, false),
            N("BlobTotalAreaLimitLow", nameof(BlobLabelsSettings.TotalAreaJudgeMin), "总面积下限", JudgeSection, 1, 0, 999_999_999, when: nameof(BlobLabelsSettings.TotalAreaJudgeEnabled), pair: nameof(BlobLabelsSettings.TotalAreaJudgeMax), pairLabel: "总面积范围"),
            N("BlobTotalAreaLimitHigh", nameof(BlobLabelsSettings.TotalAreaJudgeMax), "总面积上限", JudgeSection, 999_999_999, 0, 999_999_999, when: nameof(BlobLabelsSettings.TotalAreaJudgeEnabled)),

            B("CentroidXLimitEnable", nameof(BlobLabelsSettings.CentroidXJudgeEnabled), "质心X判断", JudgeSection, false, advanced: true),
            N("CentroidXLimitLow", nameof(BlobLabelsSettings.CentroidXJudgeMin), "质心X下限", JudgeSection, -99_999, -99_999, 99_999, when: nameof(BlobLabelsSettings.CentroidXJudgeEnabled), pair: nameof(BlobLabelsSettings.CentroidXJudgeMax), pairLabel: "质心X范围", advanced: true),
            N("CentroidXLimitHigh", nameof(BlobLabelsSettings.CentroidXJudgeMax), "质心X上限", JudgeSection, 99_999, -99_999, 99_999, when: nameof(BlobLabelsSettings.CentroidXJudgeEnabled), advanced: true),
            B("CentroidYLimitEnable", nameof(BlobLabelsSettings.CentroidYJudgeEnabled), "质心Y判断", JudgeSection, false, advanced: true),
            N("CentroidYLimitLow", nameof(BlobLabelsSettings.CentroidYJudgeMin), "质心Y下限", JudgeSection, -99_999, -99_999, 99_999, when: nameof(BlobLabelsSettings.CentroidYJudgeEnabled), pair: nameof(BlobLabelsSettings.CentroidYJudgeMax), pairLabel: "质心Y范围", advanced: true),
            N("CentroidYLimitHigh", nameof(BlobLabelsSettings.CentroidYJudgeMax), "质心Y上限", JudgeSection, 99_999, -99_999, 99_999, when: nameof(BlobLabelsSettings.CentroidYJudgeEnabled), advanced: true),
            B("AngleLimitEnable", nameof(BlobLabelsSettings.BoxAngleJudgeEnabled), "Box角度判断", JudgeSection, false, advanced: true),
            N("AngleLimitLow", nameof(BlobLabelsSettings.BoxAngleJudgeMin), "Box角度下限", JudgeSection, -180, -180, 180, when: nameof(BlobLabelsSettings.BoxAngleJudgeEnabled), pair: nameof(BlobLabelsSettings.BoxAngleJudgeMax), pairLabel: "Box角度范围", advanced: true),
            N("AngleLimitHigh", nameof(BlobLabelsSettings.BoxAngleJudgeMax), "Box角度上限", JudgeSection, 180, -180, 180, when: nameof(BlobLabelsSettings.BoxAngleJudgeEnabled), advanced: true),
            B("CenterXLimitEnable", nameof(BlobLabelsSettings.CenterXJudgeEnabled), "中心X判断", JudgeSection, false, advanced: true),
            N("CenterXLimitLow", nameof(BlobLabelsSettings.CenterXJudgeMin), "中心X下限", JudgeSection, -99_999, -99_999, 99_999, when: nameof(BlobLabelsSettings.CenterXJudgeEnabled), pair: nameof(BlobLabelsSettings.CenterXJudgeMax), pairLabel: "中心X范围", advanced: true),
            N("CenterXLimitHigh", nameof(BlobLabelsSettings.CenterXJudgeMax), "中心X上限", JudgeSection, 99_999, -99_999, 99_999, when: nameof(BlobLabelsSettings.CenterXJudgeEnabled), advanced: true),
            B("CenterYLimitEnable", nameof(BlobLabelsSettings.CenterYJudgeEnabled), "中心Y判断", JudgeSection, false, advanced: true),
            N("CenterYLimitLow", nameof(BlobLabelsSettings.CenterYJudgeMin), "中心Y下限", JudgeSection, -99_999, -99_999, 99_999, when: nameof(BlobLabelsSettings.CenterYJudgeEnabled), pair: nameof(BlobLabelsSettings.CenterYJudgeMax), pairLabel: "中心Y范围", advanced: true),
            N("CenterYLimitHigh", nameof(BlobLabelsSettings.CenterYJudgeMax), "中心Y上限", JudgeSection, 99_999, -99_999, 99_999, when: nameof(BlobLabelsSettings.CenterYJudgeEnabled), advanced: true),
            B("BoxHeightLimitEnable", nameof(BlobLabelsSettings.BoxHeightJudgeEnabled), "Box高度判断", JudgeSection, false, advanced: true),
            N("BoxHeightLimitLow", nameof(BlobLabelsSettings.BoxHeightJudgeMin), "Box高度下限", JudgeSection, 1, 0, 99_999, when: nameof(BlobLabelsSettings.BoxHeightJudgeEnabled), pair: nameof(BlobLabelsSettings.BoxHeightJudgeMax), pairLabel: "Box高度范围", advanced: true),
            N("BoxHeightLimitHigh", nameof(BlobLabelsSettings.BoxHeightJudgeMax), "Box高度上限", JudgeSection, 99_999, 0, 99_999, when: nameof(BlobLabelsSettings.BoxHeightJudgeEnabled), advanced: true),
            B("BoxWidthLimitEnable", nameof(BlobLabelsSettings.BoxWidthJudgeEnabled), "Box宽度判断", JudgeSection, false, advanced: true),
            N("BoxWidthLimitLow", nameof(BlobLabelsSettings.BoxWidthJudgeMin), "Box宽度下限", JudgeSection, 1, 0, 99_999, when: nameof(BlobLabelsSettings.BoxWidthJudgeEnabled), pair: nameof(BlobLabelsSettings.BoxWidthJudgeMax), pairLabel: "Box宽度范围", advanced: true),
            N("BoxWidthLimitHigh", nameof(BlobLabelsSettings.BoxWidthJudgeMax), "Box宽度上限", JudgeSection, 99_999, 0, 99_999, when: nameof(BlobLabelsSettings.BoxWidthJudgeEnabled), advanced: true),
            B("PerimeterLimitEnable", nameof(BlobLabelsSettings.PerimeterJudgeEnabled), "周长判断", JudgeSection, false, advanced: true),
            N("PerimeterLimitLow", nameof(BlobLabelsSettings.PerimeterJudgeMin), "周长判断下限", JudgeSection, 1, 0, 999_999_999, when: nameof(BlobLabelsSettings.PerimeterJudgeEnabled), pair: nameof(BlobLabelsSettings.PerimeterJudgeMax), pairLabel: "周长判断范围", advanced: true),
            N("PerimeterLimitHigh", nameof(BlobLabelsSettings.PerimeterJudgeMax), "周长判断上限", JudgeSection, 999_999_999, 0, 999_999_999, when: nameof(BlobLabelsSettings.PerimeterJudgeEnabled), advanced: true),
            B("LongAxisLimitEnable", nameof(BlobLabelsSettings.LongAxisJudgeEnabled), "长轴判断", JudgeSection, false, advanced: true),
            N("LongAxisLimitLow", nameof(BlobLabelsSettings.LongAxisJudgeMin), "长轴判断下限", JudgeSection, 1, 0, 999_999_999, when: nameof(BlobLabelsSettings.LongAxisJudgeEnabled), pair: nameof(BlobLabelsSettings.LongAxisJudgeMax), pairLabel: "长轴判断范围", advanced: true),
            N("LongAxisLimitHigh", nameof(BlobLabelsSettings.LongAxisJudgeMax), "长轴判断上限", JudgeSection, 999_999_999, 0, 999_999_999, when: nameof(BlobLabelsSettings.LongAxisJudgeEnabled), advanced: true),
            B("ShortAxisLimitEnable", nameof(BlobLabelsSettings.ShortAxisJudgeEnabled), "短轴判断", JudgeSection, false, advanced: true),
            N("ShortAxisLimitLow", nameof(BlobLabelsSettings.ShortAxisJudgeMin), "短轴判断下限", JudgeSection, 1, 0, 999_999_999, when: nameof(BlobLabelsSettings.ShortAxisJudgeEnabled), pair: nameof(BlobLabelsSettings.ShortAxisJudgeMax), pairLabel: "短轴判断范围", advanced: true),
            N("ShortAxisLimitHigh", nameof(BlobLabelsSettings.ShortAxisJudgeMax), "短轴判断上限", JudgeSection, 999_999_999, 0, 999_999_999, when: nameof(BlobLabelsSettings.ShortAxisJudgeEnabled), advanced: true),
            B("CircularityLimitEnable", nameof(BlobLabelsSettings.CircularityJudgeEnabled), "圆形度判断", JudgeSection, false, advanced: true),
            N("CircularityLimitLow", nameof(BlobLabelsSettings.CircularityJudgeMin), "圆形度判断下限", JudgeSection, 0, 0, 1, when: nameof(BlobLabelsSettings.CircularityJudgeEnabled), pair: nameof(BlobLabelsSettings.CircularityJudgeMax), pairLabel: "圆形度判断范围", advanced: true),
            N("CircularityLimitHigh", nameof(BlobLabelsSettings.CircularityJudgeMax), "圆形度判断上限", JudgeSection, 1, 0, 1, when: nameof(BlobLabelsSettings.CircularityJudgeEnabled), advanced: true),
            B("RectangularityLimitEnable", nameof(BlobLabelsSettings.RectangularityJudgeEnabled), "矩形度判断", JudgeSection, false, advanced: true),
            N("RectangularityLimitLow", nameof(BlobLabelsSettings.RectangularityJudgeMin), "矩形度判断下限", JudgeSection, 0, 0, 1, when: nameof(BlobLabelsSettings.RectangularityJudgeEnabled), pair: nameof(BlobLabelsSettings.RectangularityJudgeMax), pairLabel: "矩形度判断范围", advanced: true),
            N("RectangularityLimitHigh", nameof(BlobLabelsSettings.RectangularityJudgeMax), "矩形度判断上限", JudgeSection, 1, 0, 1, when: nameof(BlobLabelsSettings.RectangularityJudgeEnabled), advanced: true)
        };
        var names = fields.Select(field => field.Name).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new InvalidOperationException("BlobLabels 参数字段名重复。");
        if (fields.Select(field => field.Id).Distinct(StringComparer.Ordinal).Count() != fields.Count)
            throw new InvalidOperationException("BlobLabels 参数稳定 ID 重复。");
        if (!Properties.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(names))
            throw new InvalidOperationException("BlobLabels 参数表与设置模型不一致。");
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
            var condition = field.VisibleWhen is { } controller ? new FieldCondition(IdOf(controller), true, field.VisibleNegate) : null;
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

    private static TypeRef PortType(BlobLabelsFieldSpec field)
        => field.Type == typeof(bool) ? DataType.Boolean
            : field.Type == typeof(int) ? DataType.Integer
            : field.Type == typeof(double) ? DataType.Number
            : field.Type.IsEnum ? TypeRef.ScalarOf(ScalarKind.Text)
            : DataType.Text;

    private static NumericStorage? Storage(BlobLabelsFieldSpec field)
        => field.Type == typeof(int) ? NumericStorage.Int32 : field.Type == typeof(double) ? NumericStorage.Float64 : null;

    /// <summary>按冻结设置生成规范值；越界或类型不符直接报错。</summary>
    public static InputValues Read(BlobLabelsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var values = new Dictionary<string, object?>(FieldList.Count, StringComparer.Ordinal);
        foreach (var field in FieldList) values[field.Id] = ReadField(field, settings);
        return new InputValues(Schema.Inputs, values);
    }

    private static object? ReadField(BlobLabelsFieldSpec field, BlobLabelsSettings settings)
    {
        var raw = Properties[field.Name].GetValue(settings);
        object? value = field.Type == typeof(int) ? (long)(int)raw!
            : field.Type.IsEnum ? raw!.ToString()
            : raw;
        return ValueRules.Freeze(Schema.Inputs.Single(port => port.Id == field.Id), value);
    }

    /// <summary>从完整解析值物化设置；不接受缺项，也不从旧设置补默认。</summary>
    public static BlobLabelsSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var settings = new BlobLabelsSettings();
        foreach (var field in FieldList) settings = Write(settings, field, values.Get<object?>(field.Id));
        var errors = settings.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return settings.Freeze();
    }

    internal static BlobLabelsSettings Write(BlobLabelsSettings settings, BlobLabelsFieldSpec field, object? value)
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

    internal static BlobLabelsSettings Write(BlobLabelsSettings settings, string name, object? value)
        => Write(settings, Spec(name), value);

    /// <summary>引用字段的绑定记录排序键：使用原厂参数名，保证保存记录的稳定顺序。</summary>
    public static IReadOnlyDictionary<string, string> BindingNames { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
        FieldList.Where(field => field.AllowReference).ToDictionary(field => field.Id, field => field.VirtualMachineName, StringComparer.Ordinal));

    public static string DefaultText(BlobLabelsFieldSpec field)
        => field.Default switch
        {
            bool flag => flag ? "True" : "False",
            int number => number.ToString(CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(field.Default, CultureInfo.InvariantCulture) ?? ""
        };
}

using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>色彩提取（原件 IMVSColorExtractModu）Runtime 入口；一次调用只处理一帧。</summary>
public sealed class ColorExtractTool : IToolDefinition<ColorExtractSettings, ColorExtractOutput>, IToolSettingsFields,
    IToolCatalogProvider, IToolResultPresentationProvider, IToolRuntimeInitialization, IToolKnownSettingsValidation
{
    public const string Id = "ColorExtract";
    public const string ImageInput = "input.image";

    public string ToolId => Id;

    // 分类必须与工具箱目录里已核实映射的分类一致（原件位于「颜色处理」）。
    public ToolCatalogDescriptor Catalog { get; } = new("颜色抽取", "按颜色区间提取符合范围的像素并输出提取面积与外接矩形", "color-extract", "颜色处理", 10, "DEFAULT");

    public ToolSchema SettingsSchema => ColorExtractFields.Schema;

    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), ColorExtractOutput.Ports, outputDisplay: ColorExtractOutput.Display);

    public ResultProjectionDescriptor ResultProjection { get; } = new(ResultProjectionKind.PropertyList, ResultCardinality.One, new[]
    {
        new ResultColumnDescriptor("DEFAULT", "提取图像", new(ColorExtractOutput.Image)),
        new ResultColumnDescriptor("area", "提取总面积", new(ColorExtractOutput.TotalArea), DecimalPlaces: 0),
        new ResultColumnDescriptor("roi", "提取区域外接矩形", new(ColorExtractOutput.Roi)),
        new ResultColumnDescriptor("time", "模块耗时", new(ColorExtractOutput.ModuleRunTime), "ms", DecimalPlaces: 3)
    });

    public InputValues ReadSettings(object settings) => ColorExtractFields.Read(RequireSettings(settings));

    public object MaterializeSettings(InputValues values) => ColorExtractFields.Materialize(values);

    private static ColorExtractSettings RequireSettings(object settings) => settings as ColorExtractSettings
        ?? throw new ArgumentException("色彩提取设置类型不正确。");

    public ToolSchema Describe() => Schema;

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference)
    }, ColorExtractOutput.Ports, new ToolPresentation(Array.Empty<FieldPresentation>()),
        workflowImageInputId: ImageInput, outputDisplay: ColorExtractOutput.Display);

    public IReadOnlyList<Diagnostic> Validate(ColorExtractSettings settings) => settings.Validate();

    /// <summary>
    /// 编辑期显式校验：只判定已解析为常量的字段，引用/变量值留到执行期解析，
    /// 让参数页对非法区间文本与颠倒的面积上下限给出明确提示而不是抛出异常。
    /// </summary>
    public IReadOnlyList<Diagnostic> ValidateKnownSettings(FieldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<Diagnostic>();
        if (settings.Read(ColorExtractFields.ColorRange) is ConstantSetting { Value: string text })
        {
            try { _ = ColorExtractRanges.Parse(text); }
            catch (ArgumentException error)
            {
                errors.Add(new("COLOR_EXTRACT_RANGE_INVALID", error.Message, ColorExtractFields.ColorRange));
            }
        }
        if (settings.Read(ColorExtractFields.AreaLimitEnabled) is ConstantSetting { Value: true })
        {
            var low = Constant(settings, ColorExtractFields.AreaLimitLow);
            var high = Constant(settings, ColorExtractFields.AreaLimitHigh);
            if (low is { } minimum && (minimum < ColorExtractFields.MinimumAreaLimit || minimum > ColorExtractFields.MaximumAreaLimit))
                errors.Add(new("COLOR_EXTRACT_AREA_LIMIT",
                    $"面积下限必须在 {ColorExtractFields.MinimumAreaLimit}..{ColorExtractFields.MaximumAreaLimit}，实际 {minimum}。",
                    ColorExtractFields.AreaLimitLow));
            if (low is { } lower && high is { } upper && (upper < lower || upper > ColorExtractFields.MaximumAreaLimit))
                errors.Add(new("COLOR_EXTRACT_AREA_LIMIT",
                    $"面积上限必须在 {lower}..{ColorExtractFields.MaximumAreaLimit}，实际 {upper}。",
                    ColorExtractFields.AreaLimitHigh));
        }
        return errors.AsReadOnly();
    }

    private static long? Constant(FieldSettings settings, string fieldId)
        => settings.Read(fieldId) is ConstantSetting { Value: long value } ? value : null;

    public ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    public IToolSession<ColorExtractSettings, ColorExtractOutput> CreateSession() => new ColorExtractSession();
}

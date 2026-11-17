using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 亮度测量（原件 <c>IMVSIntensityMeasureModu</c>）的 Runtime 入口。
/// 一次调用只做一次同步统计；调度、输入准备与资源生命周期属于宿主。
/// </summary>
public sealed class IntensityMeasureTool : IToolDefinition<IntensityMeasureSettings, IntensityMeasureOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider, IToolResultDisplayProvider,
    IToolRuntimeInitialization, IToolManualReferenceSources
{
    public const string Id = "IntensityMeasure";
    public const string ImageInput = "input.image";
    /// <summary>原厂 InputROI（ROIBOX）/InputROIAnnulus（ROIANNULUS）在本方合并为一个区域输入；缺省即整幅图像。</summary>
    public const string RoiInput = "input.roi";
    /// <summary>原厂 InputMask（IMAGE）。</summary>
    public const string MaskInput = "input.mask";

    private static readonly ResultProjectionDescriptor Projection = BuildProjection();

    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog { get; } =
        new("亮度测量", "统计检测区域内灰度的最小值、最大值、均值、标准差与直方图对比度，并按上下限判定", "axes", "测量", 9, "DEFAULT");
    public ToolSchema SettingsSchema => IntensityMeasureFields.Schema;
    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), IntensityMeasureOutput.Ports,
        outputDisplay: IntensityMeasureOutput.Display);
    public ToolSchema DisplaySettingsSchema => IntensityMeasureResultDisplay.Schema;
    public ResultProjectionDescriptor ResultProjection => Projection;
    public ResultDisplayDescriptor ResultDisplay => IntensityMeasureResultDisplay.Descriptor;
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; } = Array.AsReadOnly(new[]
    {
        new ManualReferenceSource(IntensityMeasureOutput.Status),
        new ManualReferenceSource(IntensityMeasureOutput.RunTime),
        new ManualReferenceSource(IntensityMeasureOutput.LumMin),
        new ManualReferenceSource(IntensityMeasureOutput.LumMax),
        new ManualReferenceSource(IntensityMeasureOutput.LumMean),
        new ManualReferenceSource(IntensityMeasureOutput.LumStd),
        new ManualReferenceSource(IntensityMeasureOutput.HistContrast)
    });

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
        new Port(RoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
            EditModes: FieldEditModes.Reference),
        new Port(MaskInput, TypeRef.ResourceOf(ResourceKind.Image), Required: false, Nullable: true,
            EditModes: FieldEditModes.Reference)
    }, IntensityMeasureOutput.Ports, new ToolPresentation(Array.Empty<FieldPresentation>()),
        workflowImageInputId: ImageInput, outputDisplay: IntensityMeasureOutput.Display);

    public ToolSchema Describe() => Schema;

    private static ResultProjectionDescriptor BuildProjection()
    {
        var columns = new[]
        {
            new ResultColumnDescriptor("module-status", "模块状态", new(IntensityMeasureOutput.Status)),
            new ResultColumnDescriptor("module-time", "模块耗时", new(IntensityMeasureOutput.RunTime), "ms", DecimalPlaces: 3),
            new ResultColumnDescriptor("lum-min", "亮度最小值", new(IntensityMeasureOutput.LumMin), DecimalPlaces: 0),
            new ResultColumnDescriptor("lum-max", "亮度最大值", new(IntensityMeasureOutput.LumMax), DecimalPlaces: 0),
            new ResultColumnDescriptor("lum-mean", "亮度均值", new(IntensityMeasureOutput.LumMean), DecimalPlaces: 3),
            new ResultColumnDescriptor("lum-std", "亮度标准差", new(IntensityMeasureOutput.LumStd), DecimalPlaces: 3),
            new ResultColumnDescriptor("hist-contrast", "直方图对比度", new(IntensityMeasureOutput.HistContrast), DecimalPlaces: 3),
            // 原件 LumHistogram 是 int 数组：属性列表按声明顺序展开 256 级计数。
            new ResultColumnDescriptor("lum-histogram", "灰度直方图", new(IntensityMeasureOutput.Histogram)),
            new ResultColumnDescriptor("roi", "检测区域", new(IntensityMeasureOutput.Roi)),
            new ResultColumnDescriptor("mask", "输出掩膜", new(IntensityMeasureOutput.Mask))
        };
        var projection = new ResultProjectionDescriptor(ResultProjectionKind.PropertyList, ResultCardinality.One, columns);
        var errors = projection.Validate(new IntensityMeasureTool().ResultSchema);
        if (errors.Count != 0)
            throw new InvalidOperationException("IntensityMeasure 结果投影声明无效：" + string.Join("；", errors.Select(error => error.Message)));
        IntensityMeasureResultDisplay.Descriptor.Validate(new IntensityMeasureTool().ResultSchema);
        return projection;
    }

    public IReadOnlyList<Diagnostic> Validate(IntensityMeasureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    public IToolSession<IntensityMeasureSettings, IntensityMeasureOutput> CreateSession() => new IntensityMeasureSession();

    public InputValues ReadSettings(object settings) => IntensityMeasureFields.Read(RequireSettings(settings));

    public object MaterializeSettings(InputValues values) => IntensityMeasureFields.Materialize(values);

    public InputValues CreateDisplaySettings() => IntensityMeasureResultDisplay.Defaults();

    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields)
    {
        ArgumentNullException.ThrowIfNull(displayFields);
        return IntensityMeasureResultDisplay.Capture(displayFields);
    }

    private static IntensityMeasureSettings RequireSettings(object settings) => settings as IntensityMeasureSettings
        ?? throw new ArgumentException("亮度测量设置类型不正确。");
}

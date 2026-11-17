using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// 像素统计（原件 <c>IMVSPixelCountModuVA</c>）的 Runtime 入口。
/// 一次调用只做一次同步统计；调度、输入准备与资源生命周期属于宿主。
/// </summary>
public sealed class PixelCountTool : IToolDefinition<PixelCountSettings, PixelCountOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider, IToolResultDisplayProvider,
    IToolRuntimeInitialization, IToolManualReferenceSources
{
    public const string Id = "PixelCount";
    public const string ImageInput = "input.image";
    /// <summary>原厂 InputROI（ROIBOX）/InputROIAnnulus（ROIANNULUS）在本方合并为一个区域输入；缺省即整幅图像。</summary>
    public const string RoiInput = "input.roi";
    /// <summary>原厂 InputMask（IMAGE）。</summary>
    public const string MaskInput = "input.mask";

    private static readonly ResultProjectionDescriptor Projection = BuildProjection();

    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog { get; } =
        new("像素统计", "统计图像中落在各灰度区间的像素数与占比，并按数量/比率判定", "pixel-count", "测量", 9, "DEFAULT");
    public ToolSchema SettingsSchema => PixelCountFields.Schema;
    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), PixelCountOutput.Ports,
        outputDisplay: PixelCountOutput.Display);
    public ToolSchema DisplaySettingsSchema => PixelCountResultDisplay.Schema;
    public ResultProjectionDescriptor ResultProjection => Projection;
    public ResultDisplayDescriptor ResultDisplay => PixelCountResultDisplay.Descriptor;
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; } = Array.AsReadOnly(new[]
    {
        new ManualReferenceSource(PixelCountOutput.Status),
        new ManualReferenceSource(PixelCountOutput.RunTime),
        new ManualReferenceSource(PixelCountOutput.Count),
        new ManualReferenceSource(PixelCountOutput.Ratio)
    });

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
        new Port(RoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
            EditModes: FieldEditModes.Reference),
        new Port(MaskInput, TypeRef.ResourceOf(ResourceKind.Image), Required: false, Nullable: true,
            EditModes: FieldEditModes.Reference)
    }, PixelCountOutput.Ports, new ToolPresentation(Array.Empty<FieldPresentation>()),
        workflowImageInputId: ImageInput, outputDisplay: PixelCountOutput.Display);

    public ToolSchema Describe() => Schema;

    private static ResultProjectionDescriptor BuildProjection()
    {
        ResultFieldPath Cell(params string[] member) => new(PixelCountOutput.Rows, member);
        var columns = new[]
        {
            new ResultColumnDescriptor("range-index", "序号", Cell(PixelCountOutput.RangeIndex), DecimalPlaces: 0),
            new ResultColumnDescriptor("threshold-min", "灰度下限", Cell(PixelCountOutput.ThresholdMin), DecimalPlaces: 0),
            new ResultColumnDescriptor("threshold-max", "灰度上限", Cell(PixelCountOutput.ThresholdMax), DecimalPlaces: 0),
            new ResultColumnDescriptor("count", "像素数", Cell(PixelCountOutput.RangeCount), DecimalPlaces: 0),
            new ResultColumnDescriptor("ratio", "比率", Cell(PixelCountOutput.RangeRatio), DecimalPlaces: 4),
            new ResultColumnDescriptor("judgment", "判定", Cell(PixelCountOutput.JudgmentPassed))
        };
        var projection = new ResultProjectionDescriptor(ResultProjectionKind.StandardTable, ResultCardinality.Many,
            columns, new ResultFieldPath(PixelCountOutput.Rows), PixelCountOutput.RangeIndex);
        var errors = projection.Validate(new PixelCountTool().ResultSchema);
        if (errors.Count != 0)
            throw new InvalidOperationException("PixelCount 结果投影声明无效：" + string.Join("；", errors.Select(error => error.Message)));
        PixelCountResultDisplay.Descriptor.Validate(new PixelCountTool().ResultSchema);
        return projection;
    }

    public IReadOnlyList<Diagnostic> Validate(PixelCountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    public IToolSession<PixelCountSettings, PixelCountOutput> CreateSession() => new PixelCountSession();

    public InputValues ReadSettings(object settings) => PixelCountFields.Read(RequireSettings(settings));

    public object MaterializeSettings(InputValues values) => PixelCountFields.Materialize(values);

    public InputValues CreateDisplaySettings() => PixelCountResultDisplay.Defaults();

    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields)
    {
        ArgumentNullException.ThrowIfNull(displayFields);
        return PixelCountResultDisplay.Capture(displayFields);
    }

    private static PixelCountSettings RequireSettings(object settings) => settings as PixelCountSettings
        ?? throw new ArgumentException("像素统计设置类型不正确。");
}

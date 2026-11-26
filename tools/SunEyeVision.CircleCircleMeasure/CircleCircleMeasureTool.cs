using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.CircleCircleMeasure.Runtime;

/// <summary>
/// 圆圆测量（原件 <c>IMVSC2CMeasureModu</c>）的 Runtime 入口。
/// 本批交付 UI/端口/字段/结果显示对齐；算法数值取证为 not-established，执行明确返回"待补证"诊断，不产出测量值。
/// </summary>
public sealed class CircleCircleMeasureTool : IToolDefinition<CircleCircleMeasureSettings, CircleCircleMeasureOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider, IToolResultDisplayProvider,
    IToolRuntimeInitialization, IToolManualReferenceSources
{
    public const string Id = "CircleCircleMeasure";
    public const string ImageInput = "input.image";
    public const string CircleAnnulus1Input = "input.circle-annulus-1";
    public const string CircleAnnulus2Input = "input.circle-annulus-2";
    public const string FixtureInput = "input.position-correction";
    public const string CalibMatrixInput = "input.calib-matrix";

    private static readonly ResultProjectionDescriptor Projection = BuildProjection();

    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog { get; } =
        new("圆圆测量", "测量两个圆的交点、角度与距离并按上下限判定", "circle-circle-measure", "测量", 9, "DEFAULT");
    public ToolSchema SettingsSchema => CircleCircleMeasureFields.Schema;
    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), CircleCircleMeasureOutput.Ports,
        outputDisplay: CircleCircleMeasureOutput.Display);
    public ToolSchema DisplaySettingsSchema => CircleCircleMeasureResultDisplay.Schema;
    public ResultProjectionDescriptor ResultProjection => Projection;
    public ResultDisplayDescriptor ResultDisplay => CircleCircleMeasureResultDisplay.Descriptor;
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; } = Array.AsReadOnly(new[]
    {
        new ManualReferenceSource(CircleCircleMeasureOutput.Status),
        new ManualReferenceSource(CircleCircleMeasureOutput.RunTime),
        new ManualReferenceSource(CircleCircleMeasureOutput.Angle),
        new ManualReferenceSource(CircleCircleMeasureOutput.Dist),
        new ManualReferenceSource(CircleCircleMeasureOutput.C2cType)
    });

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
        new Port(CircleAnnulus1Input, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
            EditModes: FieldEditModes.Reference),
        new Port(CircleAnnulus2Input, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
            EditModes: FieldEditModes.Reference),
        new Port(FixtureInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
            EditModes: FieldEditModes.Reference),
        new Port(CalibMatrixInput, TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64)), Required: false, Nullable: true,
            EditModes: FieldEditModes.Reference)
    }, CircleCircleMeasureOutput.Ports, new ToolPresentation(Array.Empty<FieldPresentation>()),
        workflowImageInputId: ImageInput, outputDisplay: CircleCircleMeasureOutput.Display);

    public ToolSchema Describe() => Schema;

    private static ResultProjectionDescriptor BuildProjection()
    {
        var columns = new[]
        {
            new ResultColumnDescriptor("module-status", "模块状态", new(CircleCircleMeasureOutput.Status)),
            new ResultColumnDescriptor("module-time", "模块耗时", new(CircleCircleMeasureOutput.RunTime), "ms", DecimalPlaces: 3),
            new ResultColumnDescriptor("angle", "角度", new(CircleCircleMeasureOutput.Angle), "deg", DecimalPlaces: 3),
            new ResultColumnDescriptor("dist", "距离", new(CircleCircleMeasureOutput.Dist), DecimalPlaces: 3),
            new ResultColumnDescriptor("c2c-type", "圆圆类型", new(CircleCircleMeasureOutput.C2cType)),
            new ResultColumnDescriptor("circle-1", "测量圆1", new(CircleCircleMeasureOutput.Rows, new[] { CircleCircleMeasureOutput.Circle1 })),
            new ResultColumnDescriptor("circle-2", "测量圆2", new(CircleCircleMeasureOutput.Rows, new[] { CircleCircleMeasureOutput.Circle2 })),
            new ResultColumnDescriptor("intersection-1", "交点1", new(CircleCircleMeasureOutput.Rows, new[] { CircleCircleMeasureOutput.Intersection1 })),
            new ResultColumnDescriptor("intersection-2", "交点2", new(CircleCircleMeasureOutput.Rows, new[] { CircleCircleMeasureOutput.Intersection2 }))
        };
        var projection = new ResultProjectionDescriptor(ResultProjectionKind.PropertyList, ResultCardinality.One, columns);
        var errors = projection.Validate(new CircleCircleMeasureTool().ResultSchema);
        if (errors.Count != 0)
            throw new InvalidOperationException("圆圆测量结果投影声明无效：" + string.Join("；", errors.Select(error => error.Message)));
        CircleCircleMeasureResultDisplay.Descriptor.Validate(new CircleCircleMeasureTool().ResultSchema);
        return projection;
    }

    public IReadOnlyList<Diagnostic> Validate(CircleCircleMeasureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    public IToolSession<CircleCircleMeasureSettings, CircleCircleMeasureOutput> CreateSession() => new CircleCircleMeasureSession();

    public InputValues ReadSettings(object settings) => CircleCircleMeasureFields.Read(RequireSettings(settings));

    public object MaterializeSettings(InputValues values) => CircleCircleMeasureFields.Materialize(values);

    public InputValues CreateDisplaySettings() => CircleCircleMeasureResultDisplay.Defaults();

    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields)
    {
        ArgumentNullException.ThrowIfNull(displayFields);
        return CircleCircleMeasureResultDisplay.Capture(displayFields);
    }

    private static CircleCircleMeasureSettings RequireSettings(object settings) => settings as CircleCircleMeasureSettings
        ?? throw new ArgumentException("圆圆测量设置类型不正确。");
}

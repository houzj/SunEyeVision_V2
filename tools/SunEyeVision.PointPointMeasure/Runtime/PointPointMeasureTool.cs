using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.PointPointMeasure.Runtime;

/// <summary>
/// 点点测量（原件 <c>IMVSP2PMeasureModu</c>）的 Runtime 入口。
/// 本批交付 UI/端口/字段/结果显示对齐；算法数值取证为 not-established，执行明确返回"待补证"诊断。
/// </summary>
public sealed class PointPointMeasureTool : IToolDefinition<PointPointMeasureSettings, PointPointMeasureOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider, IToolResultDisplayProvider,
    IToolRuntimeInitialization, IToolManualReferenceSources
{
    public const string Id = "PointPointMeasure";
    public const string ImageInput = "input.image";
    public const string Point1Input = "input.point-1";
    public const string Point2Input = "input.point-2";
    public const string CalibMatrixInput = "input.calib-matrix";

    private static readonly ResultProjectionDescriptor Projection = BuildProjection();

    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog { get; } =
        new("点点测量", "测量输入两点间的距离、角度与中点并按上下限判定", "point-point-measure", "测量", 9, "DEFAULT");
    public ToolSchema SettingsSchema => PointPointMeasureFields.Schema;
    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), PointPointMeasureOutput.Ports,
        outputDisplay: PointPointMeasureOutput.Display);
    public ToolSchema DisplaySettingsSchema => PointPointMeasureResultDisplay.Schema;
    public ResultProjectionDescriptor ResultProjection => Projection;
    public ResultDisplayDescriptor ResultDisplay => PointPointMeasureResultDisplay.Descriptor;
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; } = Array.AsReadOnly(new[]
    {
        new ManualReferenceSource(PointPointMeasureOutput.Status),
        new ManualReferenceSource(PointPointMeasureOutput.RunTime),
        new ManualReferenceSource(PointPointMeasureOutput.Angle),
        new ManualReferenceSource(PointPointMeasureOutput.Dist)
    });

    private static ToolSchema PointPort(string id) => new(new[]
    {
        new Port("x", DataType.Number, Storage: NumericStorage.Float32),
        new Port("y", DataType.Number, Storage: NumericStorage.Float32)
    }, Array.Empty<Port>(), new ToolPresentation(Array.Empty<FieldPresentation>()));

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
        new Port(Point1Input, TypeRef.RecordOf("point-point-measure.point-input", new[]
        {
            new Port("x", DataType.Number, Storage: NumericStorage.Float32),
            new Port("y", DataType.Number, Storage: NumericStorage.Float32)
        }), EditModes: FieldEditModes.Reference),
        new Port(Point2Input, TypeRef.RecordOf("point-point-measure.point-input", new[]
        {
            new Port("x", DataType.Number, Storage: NumericStorage.Float32),
            new Port("y", DataType.Number, Storage: NumericStorage.Float32)
        }), EditModes: FieldEditModes.Reference),
        new Port(CalibMatrixInput, TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64)), Required: false, Nullable: true,
            EditModes: FieldEditModes.Reference)
    }, PointPointMeasureOutput.Ports, new ToolPresentation(Array.Empty<FieldPresentation>()),
        workflowImageInputId: ImageInput, outputDisplay: PointPointMeasureOutput.Display);

    public ToolSchema Describe() => Schema;

    private static ResultProjectionDescriptor BuildProjection()
    {
        var columns = new[]
        {
            new ResultColumnDescriptor("module-status", "模块状态", new(PointPointMeasureOutput.Status)),
            new ResultColumnDescriptor("module-time", "模块耗时", new(PointPointMeasureOutput.RunTime), "ms", DecimalPlaces: 3),
            new ResultColumnDescriptor("point-1", "测量点1", new(PointPointMeasureOutput.Rows, new[] { PointPointMeasureOutput.MeasurePoint1 })),
            new ResultColumnDescriptor("point-2", "测量点2", new(PointPointMeasureOutput.Rows, new[] { PointPointMeasureOutput.MeasurePoint2 })),
            new ResultColumnDescriptor("midpoint", "中点", new(PointPointMeasureOutput.Rows, new[] { PointPointMeasureOutput.Midpoint })),
            new ResultColumnDescriptor("angle", "角度", new(PointPointMeasureOutput.Angle), "deg", DecimalPlaces: 3),
            new ResultColumnDescriptor("dist", "距离", new(PointPointMeasureOutput.Dist), DecimalPlaces: 3)
        };
        var projection = new ResultProjectionDescriptor(ResultProjectionKind.PropertyList, ResultCardinality.One, columns);
        var errors = projection.Validate(new PointPointMeasureTool().ResultSchema);
        if (errors.Count != 0)
            throw new InvalidOperationException("点点测量结果投影声明无效：" + string.Join("；", errors.Select(error => error.Message)));
        PointPointMeasureResultDisplay.Descriptor.Validate(new PointPointMeasureTool().ResultSchema);
        return projection;
    }

    public IReadOnlyList<Diagnostic> Validate(PointPointMeasureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    public IToolSession<PointPointMeasureSettings, PointPointMeasureOutput> CreateSession() => new PointPointMeasureSession();

    public InputValues ReadSettings(object settings) => PointPointMeasureFields.Read(RequireSettings(settings));

    public object MaterializeSettings(InputValues values) => PointPointMeasureFields.Materialize(values);

    public InputValues CreateDisplaySettings() => PointPointMeasureResultDisplay.Defaults();

    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields)
    {
        ArgumentNullException.ThrowIfNull(displayFields);
        return PointPointMeasureResultDisplay.Capture(displayFields);
    }

    private static PointPointMeasureSettings RequireSettings(object settings) => settings as PointPointMeasureSettings
        ?? throw new ArgumentException("点点测量设置类型不正确。");
}

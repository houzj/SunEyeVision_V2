using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.LineFind.Runtime;

/// <summary>
/// 直线查找（原件 <c>IMVSLineFindModu</c>）的 Runtime 入口。UI/契约按原厂证据对齐；
/// 原生算法未取证，执行返回「待补证」诊断（见 LineFindSession）。
/// </summary>
public sealed class LineFindTool : IToolDefinition<LineFindSettings, LineFindOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider, IToolResultDisplayProvider,
    IToolRuntimeInitialization, IToolManualReferenceSources
{
    public const string Id = "LineFind";

    private static readonly ToolSchema ExecutionSchema = BuildExecutionSchema();
    private static readonly ResultProjectionDescriptor Projection = BuildProjection();

    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog { get; } =
        new("直线查找", "在检测区域内按边缘查找直线，输出端点、中点、角度与拟合误差", "line-find", "定位", 32, "DEFAULT");
    public ToolSchema SettingsSchema => LineFindFields.Schema;
    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), LineFindSchema.Outputs,
        outputDisplay: LineFindSchema.OutputDisplay);
    public ToolSchema DisplaySettingsSchema => LineFindResultDisplay.Schema;
    public ResultProjectionDescriptor ResultProjection => Projection;
    public ResultDisplayDescriptor ResultDisplay => LineFindResultDisplay.Descriptor;
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; } = Array.AsReadOnly(new[]
    {
        new ManualReferenceSource(LineFindSchema.Status),
        new ManualReferenceSource(LineFindSchema.RunTime),
        new ManualReferenceSource(LineFindSchema.LineAngle),
        new ManualReferenceSource(LineFindSchema.LineFitError)
    });

    public ToolSchema Describe() => ExecutionSchema;

    private static ToolSchema BuildExecutionSchema()
        => new(new[]
        {
            new Port(LineFindSchema.ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
            new Port(LineFindSchema.RoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
                EditModes: FieldEditModes.Reference),
            new Port(LineFindSchema.FixtureInput, FixtureDeclarations.Info, Required: false, Nullable: true,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference)
        }, LineFindSchema.Outputs, new ToolPresentation(Array.Empty<FieldPresentation>()),
            workflowImageInputId: LineFindSchema.ImageInput, outputDisplay: LineFindSchema.OutputDisplay);

    private static ResultProjectionDescriptor BuildProjection()
    {
        ResultFieldPath Cell(params string[] member) => new(LineFindSchema.Rows, member);
        var columns = new[]
        {
            new ResultColumnDescriptor("element-id", "序号", Cell("element-id"), DecimalPlaces: 0,
                Geometry: new(LineFindSchema.Rows, new[] { "line" }),
                GeometrySource: new(LineFindSchema.Rows, new[] { "region" })),
            new ResultColumnDescriptor("start-x", "起点X", Cell("start-x"), DecimalPlaces: 2),
            new ResultColumnDescriptor("start-y", "起点Y", Cell("start-y"), DecimalPlaces: 2),
            new ResultColumnDescriptor("end-x", "终点X", Cell("end-x"), DecimalPlaces: 2),
            new ResultColumnDescriptor("end-y", "终点Y", Cell("end-y"), DecimalPlaces: 2),
            new ResultColumnDescriptor("angle", "角度", Cell("angle-degrees"), DecimalPlaces: 2),
            new ResultColumnDescriptor("fit-error", "拟合误差", Cell("fit-error"), DecimalPlaces: 2),
            new ResultColumnDescriptor("judgment", "判定", Cell("judgment-passed"))
        };
        var projection = new ResultProjectionDescriptor(ResultProjectionKind.StandardTable, ResultCardinality.Many,
            columns, new ResultFieldPath(LineFindSchema.Rows), "element-id");
        var tool = new LineFindTool();
        var errors = projection.Validate(tool.ResultSchema);
        if (errors.Count != 0)
            throw new InvalidOperationException("LineFind 结果投影声明无效：" + string.Join("；", errors.Select(error => error.Message)));
        LineFindResultDisplay.Descriptor.Validate(tool.ResultSchema);
        return projection;
    }

    public IReadOnlyList<Diagnostic> Validate(LineFindSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    public IToolSession<LineFindSettings, LineFindOutput> CreateSession() => new LineFindSession();

    public InputValues ReadSettings(object settings)
        => LineFindFields.Read(settings as LineFindSettings
            ?? throw new ArgumentException("LineFindSettings is required.", nameof(settings)));

    public object MaterializeSettings(InputValues values) => LineFindFields.Materialize(values);

    public InputValues CreateDisplaySettings() => LineFindResultDisplay.Defaults();

    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields)
    {
        ArgumentNullException.ThrowIfNull(displayFields);
        return LineFindResultDisplay.Capture(displayFields);
    }
}

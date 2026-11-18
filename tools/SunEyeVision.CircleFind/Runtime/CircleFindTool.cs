using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.CircleFind.Runtime;

/// <summary>
/// 圆查找（原件 <c>IMVSCircleFindModu</c>）的 Runtime 入口。
/// 一次调用只做一次同步圆查找；调度、输入准备与资源生命周期属于宿主。
/// 本阶段 UI/契约按原厂证据对齐；原生算法未取证，执行返回「待补证」诊断（见 CircleFindSession）。
/// </summary>
public sealed class CircleFindTool : IToolDefinition<CircleFindSettings, CircleFindOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider, IToolResultDisplayProvider,
    IToolRuntimeInitialization, IToolManualReferenceSources
{
    public const string Id = "CircleFind";

    private static readonly ToolSchema ExecutionSchema = BuildExecutionSchema();
    private static readonly ResultProjectionDescriptor Projection = BuildProjection();

    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog { get; } =
        new("圆查找", "在检测区域内查找满足边缘条件的圆，输出圆心、半径与拟合误差", "circle-find", "定位", 31, "DEFAULT");
    public ToolSchema SettingsSchema => CircleFindFields.Schema;
    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), CircleFindSchema.Outputs,
        outputDisplay: CircleFindSchema.OutputDisplay);
    public ToolSchema DisplaySettingsSchema => CircleFindResultDisplay.Schema;
    public ResultProjectionDescriptor ResultProjection => Projection;
    public ResultDisplayDescriptor ResultDisplay => CircleFindResultDisplay.Descriptor;
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; } = Array.AsReadOnly(new[]
    {
        new ManualReferenceSource(CircleFindSchema.Status),
        new ManualReferenceSource(CircleFindSchema.RunTime),
        new ManualReferenceSource(CircleFindSchema.CenterX),
        new ManualReferenceSource(CircleFindSchema.CenterY),
        new ManualReferenceSource(CircleFindSchema.Radius),
        new ManualReferenceSource(CircleFindSchema.FitError)
    });

    public ToolSchema Describe() => ExecutionSchema;

    private static ToolSchema BuildExecutionSchema()
        => new(new[]
        {
            new Port(CircleFindSchema.ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
            new Port(CircleFindSchema.RoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
                EditModes: FieldEditModes.Reference),
            new Port(CircleFindSchema.FixtureInput, FixtureDeclarations.Info, Required: false, Nullable: true,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference)
        }, CircleFindSchema.Outputs, new ToolPresentation(Array.Empty<FieldPresentation>()),
            workflowImageInputId: CircleFindSchema.ImageInput, outputDisplay: CircleFindSchema.OutputDisplay);

    private static ResultProjectionDescriptor BuildProjection()
    {
        ResultFieldPath Cell(params string[] member) => new(CircleFindSchema.Rows, member);
        var columns = new[]
        {
            new ResultColumnDescriptor("element-id", "序号", Cell("element-id"), DecimalPlaces: 0,
                Geometry: new(CircleFindSchema.Rows, new[] { "circle" }),
                GeometrySource: new(CircleFindSchema.Rows, new[] { "region" })),
            new ResultColumnDescriptor("center-x", "圆心X", Cell("center-x"), DecimalPlaces: 2),
            new ResultColumnDescriptor("center-y", "圆心Y", Cell("center-y"), DecimalPlaces: 2),
            new ResultColumnDescriptor("radius", "半径", Cell("radius"), DecimalPlaces: 2),
            new ResultColumnDescriptor("fit-error", "拟合误差", Cell("fit-error"), DecimalPlaces: 2),
            new ResultColumnDescriptor("judgment", "判定", Cell("judgment-passed"))
        };
        var projection = new ResultProjectionDescriptor(ResultProjectionKind.StandardTable, ResultCardinality.Many,
            columns, new ResultFieldPath(CircleFindSchema.Rows), "element-id");
        var tool = new CircleFindTool();
        var errors = projection.Validate(tool.ResultSchema);
        if (errors.Count != 0)
            throw new InvalidOperationException("CircleFind 结果投影声明无效：" + string.Join("；", errors.Select(error => error.Message)));
        CircleFindResultDisplay.Descriptor.Validate(tool.ResultSchema);
        return projection;
    }

    public IReadOnlyList<Diagnostic> Validate(CircleFindSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    public IToolSession<CircleFindSettings, CircleFindOutput> CreateSession() => new CircleFindSession();

    public InputValues ReadSettings(object settings)
        => CircleFindFields.Read(settings as CircleFindSettings
            ?? throw new ArgumentException("CircleFindSettings is required.", nameof(settings)));

    public object MaterializeSettings(InputValues values) => CircleFindFields.Materialize(values);

    public InputValues CreateDisplaySettings() => CircleFindResultDisplay.Defaults();

    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields)
    {
        ArgumentNullException.ThrowIfNull(displayFields);
        return CircleFindResultDisplay.Capture(displayFields);
    }
}

using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 轮廓匹配（原件 <c>IMVSContourMatchModu</c>）的 Runtime 入口。一次调用只做一次同步形状模板匹配；
/// 调度、输入准备与资源生命周期属于宿主。
/// </summary>
public sealed class ContourMatchTool : IToolDefinition<ContourMatchSettings, ContourMatchOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider, IToolResultDisplayProvider,
    IToolParameterBindingNames, IToolResourceFieldDeclarations, IToolKnownSettingsValidation, IToolRuntimeInitialization
{
    public const string Id = "ContourMatch";

    private static readonly ToolSchema ExecutionSchema = BuildExecutionSchema();
    private static readonly ResultProjectionDescriptor Projection = BuildProjection();

    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog { get; } = new("轮廓匹配", "按学习轮廓模板在搜索区域内匹配形状相似的目标，输出匹配点、角度、分数与匹配框", "contour-match", "定位", 25, "DEFAULT");
    public ToolSchema SettingsSchema => ContourMatchFields.Schema;
    public IReadOnlyDictionary<string, string> ParameterBindingNames => ContourMatchFields.BindingNames;
    /// <summary>本工具没有图像/文件路径类设置：模板数据随设置内联，搜索区域以输入端口引用承载。</summary>
    public IReadOnlyList<ResourceFieldDeclaration> ResourceFields { get; } = Array.Empty<ResourceFieldDeclaration>();
    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), ContourMatchSchema.Outputs, outputDisplay: ContourMatchSchema.OutputDisplay);
    public ToolSchema DisplaySettingsSchema => ContourMatchResultDisplay.Schema;
    public ResultProjectionDescriptor ResultProjection => Projection;
    public ResultDisplayDescriptor ResultDisplay => ContourMatchResultDisplay.Descriptor;

    public ToolSchema Describe() => ExecutionSchema;

    private static ToolSchema BuildExecutionSchema()
        => new(new[]
        {
            new Port(ContourMatchSchema.ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
            new Port(ContourMatchSchema.RoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
                EditModes: FieldEditModes.Reference),
            new Port(ContourMatchSchema.FixtureInput, FixtureDeclarations.Info, Required: false, Nullable: true,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference)
        }, ContourMatchSchema.Outputs, new ToolPresentation(ExecutionInputPresentations()),
            workflowImageInputId: ContourMatchSchema.ImageInput, outputDisplay: ContourMatchSchema.OutputDisplay);

    private static IReadOnlyList<FieldPresentation> ExecutionInputPresentations()
        => new FieldPresentation[]
        {
            new(ContourMatchSchema.ImageInput, "输入图像", ContourMatchFields.ImageInputSection, 0, FieldEditorKind.Image)
            {
                Layout = new FieldLayout(ContourMatchFields.BasicPage, ContourMatchFields.ImageInputSection,
                    AllowReference: true)
            },
            new(ContourMatchSchema.RoiInput, "输入ROI区域", ContourMatchFields.RoiInputSection, 1, FieldEditorKind.Region)
            {
                Layout = new FieldLayout(ContourMatchFields.BasicPage, ContourMatchFields.RoiInputSection,
                    AllowReference: true)
            },
            new(ContourMatchSchema.FixtureInput, "位置修正", ContourMatchFields.RoiInputSection, 2, FieldEditorKind.Record)
            {
                Layout = new FieldLayout(ContourMatchFields.BasicPage, ContourMatchFields.RoiInputSection,
                    AllowReference: true)
            }
        };
    private static ResultProjectionDescriptor BuildProjection()
    {
        ResultColumnDescriptor Column(string key, string label, params string[] member)
            => new(key, label, new ResultFieldPath(ContourMatchSchema.Rows, member), DecimalPlaces: member[0] == "element-id" ? 0 : 2,
                Geometry: new(ContourMatchSchema.Rows, new[] { "match-point" }),
                GeometrySource: new(ContourMatchSchema.Rows, new[] { "region" }));
        var columns = new[]
        {
            Column("element-id", "序号", "element-id"),
            Column("center-x", "匹配点X", "center-x"),
            Column("center-y", "匹配点Y", "center-y"),
            Column("angle", "角度", "angle-degrees"),
            Column("score", "匹配分数", "score"),
            Column("judgment", "判定", "judgment-passed")
        };
        var projection = new ResultProjectionDescriptor(ResultProjectionKind.StandardTable, ResultCardinality.Many,
            columns, new ResultFieldPath(ContourMatchSchema.Rows), "element-id");
        var errors = projection.Validate(new ContourMatchTool().ResultSchema);
        if (errors.Count != 0)
            throw new InvalidOperationException("ContourMatch 结果投影声明无效：" + string.Join("；", errors.Select(error => error.Message)));
        ContourMatchResultDisplay.Descriptor.Validate(new ContourMatchTool().ResultSchema);
        return projection;
    }

    public IReadOnlyList<Diagnostic> Validate(ContourMatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    public IToolSession<ContourMatchSettings, ContourMatchOutput> CreateSession() => new ContourMatchSession();

    public InputValues ReadSettings(object settings)
        => ContourMatchFields.Read(settings as ContourMatchSettings ?? throw new ArgumentException("ContourMatchSettings is required.", nameof(settings)));

    public object MaterializeSettings(InputValues values) => ContourMatchFields.Materialize(values);

    public InputValues CreateDisplaySettings() => ContourMatchResultDisplay.Defaults();

    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields)
    {
        ArgumentNullException.ThrowIfNull(displayFields);
        return ContourMatchResultDisplay.Capture(displayFields);
    }

    /// <summary>已知设置的显式校验：范围越界在准入时给出明确诊断。</summary>
    public IReadOnlyList<Diagnostic> ValidateKnownSettings(FieldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<Diagnostic>();
        foreach (var spec in ContourMatchFields.All)
        {
            var id = spec.Id;
            if (settings.Read(id) is not ConstantSetting constant) continue;
            if (constant.Value is long integer)
            {
                if (spec.Minimum is { } minimum && integer < minimum)
                    errors.Add(new("CONTOURMATCH_SETTINGS_INVALID", spec.Label + "低于允许范围。", id));
                if (spec.Maximum is { } maximum && integer > maximum)
                    errors.Add(new("CONTOURMATCH_SETTINGS_INVALID", spec.Label + "超出允许范围。", id));
            }
            else if (constant.Value is double number)
            {
                if (!double.IsFinite(number) || spec.Minimum is { } minimum && number < minimum || spec.Maximum is { } maximum && number > maximum)
                    errors.Add(new("CONTOURMATCH_SETTINGS_INVALID", spec.Label + "超出允许范围。", id));
            }
        }
        return errors.AsReadOnly();
    }
}


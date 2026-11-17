using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.GrayMatch.Runtime;

/// <summary>
/// 灰度匹配（原件 <c>IMVSGrayMatchModuVA</c>）的 Runtime 入口。一次调用只做一次同步灰度模板匹配；
/// 调度、输入准备与资源生命周期属于宿主。
/// </summary>
public sealed class GrayMatchTool : IToolDefinition<GrayMatchSettings, GrayMatchOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider, IToolResultDisplayProvider,
    IToolParameterBindingNames, IToolResourceFieldDeclarations, IToolKnownSettingsValidation, IToolRuntimeInitialization
{
    public const string Id = "GrayMatch";

    private static readonly ToolSchema ExecutionSchema = BuildExecutionSchema();
    private static readonly ResultProjectionDescriptor Projection = BuildProjection();

    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog { get; } = new("灰度匹配", "按学习灰度模板在搜索区域内查找灰度相似的目标，输出匹配点、角度、分数与匹配框", "gray-match", "定位", 35, "DEFAULT");
    public ToolSchema SettingsSchema => GrayMatchFields.Schema;
    public IReadOnlyDictionary<string, string> ParameterBindingNames => GrayMatchFields.BindingNames;
    /// <summary>本工具没有图像/文件路径类设置：模板数据随设置内联，搜索区域以输入端口引用承载。</summary>
    public IReadOnlyList<ResourceFieldDeclaration> ResourceFields { get; } = Array.Empty<ResourceFieldDeclaration>();
    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), GrayMatchSchema.Outputs, outputDisplay: GrayMatchSchema.OutputDisplay);
    public ToolSchema DisplaySettingsSchema => GrayMatchResultDisplay.Schema;
    public ResultProjectionDescriptor ResultProjection => Projection;
    public ResultDisplayDescriptor ResultDisplay => GrayMatchResultDisplay.Descriptor;

    public ToolSchema Describe() => ExecutionSchema;

    private static ToolSchema BuildExecutionSchema()
        => new(new[]
        {
            new Port(GrayMatchSchema.ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
            new Port(GrayMatchSchema.RoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
                EditModes: FieldEditModes.Reference),
            new Port(GrayMatchSchema.FixtureInput, FixtureDeclarations.Info, Required: false, Nullable: true,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference)
        }, GrayMatchSchema.Outputs, new ToolPresentation(Array.Empty<FieldPresentation>()),
            workflowImageInputId: GrayMatchSchema.ImageInput, outputDisplay: GrayMatchSchema.OutputDisplay);

    private static ResultProjectionDescriptor BuildProjection()
    {
        ResultColumnDescriptor Column(string key, string label, params string[] member)
            => new(key, label, new ResultFieldPath(GrayMatchSchema.Rows, member), DecimalPlaces: member[0] == "element-id" ? 0 : 2,
                Geometry: new(GrayMatchSchema.Rows, new[] { "match-point" }),
                GeometrySource: new(GrayMatchSchema.Rows, new[] { "region" }));
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
            columns, new ResultFieldPath(GrayMatchSchema.Rows), "element-id");
        var errors = projection.Validate(new GrayMatchTool().ResultSchema);
        if (errors.Count != 0)
            throw new InvalidOperationException("GrayMatch 结果投影声明无效：" + string.Join("；", errors.Select(error => error.Message)));
        GrayMatchResultDisplay.Descriptor.Validate(new GrayMatchTool().ResultSchema);
        return projection;
    }

    public IReadOnlyList<Diagnostic> Validate(GrayMatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    public IToolSession<GrayMatchSettings, GrayMatchOutput> CreateSession() => new GrayMatchSession();

    public InputValues ReadSettings(object settings)
        => GrayMatchFields.Read(settings as GrayMatchSettings ?? throw new ArgumentException("GrayMatchSettings is required.", nameof(settings)));

    public object MaterializeSettings(InputValues values) => GrayMatchFields.Materialize(values);

    public InputValues CreateDisplaySettings() => GrayMatchResultDisplay.Defaults();

    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields)
    {
        ArgumentNullException.ThrowIfNull(displayFields);
        return GrayMatchResultDisplay.Capture(displayFields);
    }

    /// <summary>已知设置的显式校验：范围越界在准入时给出明确诊断。</summary>
    public IReadOnlyList<Diagnostic> ValidateKnownSettings(FieldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<Diagnostic>();
        foreach (var spec in GrayMatchFields.All)
        {
            var id = spec.Id;
            if (settings.Read(id) is not ConstantSetting constant) continue;
            if (constant.Value is long integer)
            {
                if (spec.Minimum is { } minimum && integer < minimum)
                    errors.Add(new("GRAYMATCH_SETTINGS_INVALID", spec.Label + "低于允许范围。", id));
                if (spec.Maximum is { } maximum && integer > maximum)
                    errors.Add(new("GRAYMATCH_SETTINGS_INVALID", spec.Label + "超出允许范围。", id));
            }
            else if (constant.Value is double number)
            {
                if (!double.IsFinite(number) || spec.Minimum is { } minimum && number < minimum || spec.Maximum is { } maximum && number > maximum)
                    errors.Add(new("GRAYMATCH_SETTINGS_INVALID", spec.Label + "超出允许范围。", id));
            }
        }
        return errors.AsReadOnly();
    }
}

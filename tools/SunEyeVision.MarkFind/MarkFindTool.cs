using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.MarkFind.Runtime;

/// <summary>
/// 图形定位（原件 <c>IMVSMarkFindModu</c>）的 Runtime 入口。一次调用只做一次同步形状模板匹配；
/// 调度、输入准备与资源生命周期属于宿主。
/// </summary>
public sealed class MarkFindTool : IToolDefinition<MarkFindSettings, MarkFindOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider, IToolResultDisplayProvider,
    IToolParameterBindingNames, IToolResourceFieldDeclarations, IToolKnownSettingsValidation, IToolRuntimeInitialization
{
    public const string Id = "MarkFind";

    private static readonly ToolSchema ExecutionSchema = BuildExecutionSchema();
    private static readonly ResultProjectionDescriptor Projection = BuildProjection();

    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog { get; } = new("图形定位", "按学习模板在搜索区域内定位标记，输出匹配点、角度、分数与匹配框", "graph-locate", "定位", 30, "DEFAULT");
    public ToolSchema SettingsSchema => MarkFindFields.Schema;
    public IReadOnlyDictionary<string, string> ParameterBindingNames => MarkFindFields.BindingNames;
    /// <summary>本工具没有图像/文件路径类设置：模板数据随设置内联，搜索区域以输入端口引用承载。</summary>
    public IReadOnlyList<ResourceFieldDeclaration> ResourceFields { get; } = Array.Empty<ResourceFieldDeclaration>();
    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), MarkFindSchema.Outputs, outputDisplay: MarkFindSchema.OutputDisplay);
    public ToolSchema DisplaySettingsSchema => MarkFindResultDisplay.Schema;
    public ResultProjectionDescriptor ResultProjection => Projection;
    public ResultDisplayDescriptor ResultDisplay => MarkFindResultDisplay.Descriptor;

    public ToolSchema Describe() => ExecutionSchema;

    private static ToolSchema BuildExecutionSchema()
        => new(new[]
        {
            new Port(MarkFindSchema.ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
            new Port(MarkFindSchema.RoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
                EditModes: FieldEditModes.Reference),
            new Port(MarkFindSchema.FixtureInput, FixtureDeclarations.Info, Required: false, Nullable: true,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference)
        }, MarkFindSchema.Outputs, new ToolPresentation(Array.Empty<FieldPresentation>()),
            workflowImageInputId: MarkFindSchema.ImageInput, outputDisplay: MarkFindSchema.OutputDisplay);

    private static ResultProjectionDescriptor BuildProjection()
    {
        ResultColumnDescriptor Column(string key, string label, params string[] member)
            => new(key, label, new ResultFieldPath(MarkFindSchema.Rows, member), DecimalPlaces: member[0] == "element-id" ? 0 : 2,
                Geometry: new(MarkFindSchema.Rows, new[] { "match-point" }),
                GeometrySource: new(MarkFindSchema.Rows, new[] { "region" }));
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
            columns, new ResultFieldPath(MarkFindSchema.Rows), "element-id");
        var errors = projection.Validate(new MarkFindTool().ResultSchema);
        if (errors.Count != 0)
            throw new InvalidOperationException("MarkFind 结果投影声明无效：" + string.Join("；", errors.Select(error => error.Message)));
        MarkFindResultDisplay.Descriptor.Validate(new MarkFindTool().ResultSchema);
        return projection;
    }

    public IReadOnlyList<Diagnostic> Validate(MarkFindSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    public IToolSession<MarkFindSettings, MarkFindOutput> CreateSession() => new MarkFindSession();

    public InputValues ReadSettings(object settings)
        => MarkFindFields.Read(settings as MarkFindSettings ?? throw new ArgumentException("MarkFindSettings is required.", nameof(settings)));

    public object MaterializeSettings(InputValues values) => MarkFindFields.Materialize(values);

    public InputValues CreateDisplaySettings() => MarkFindResultDisplay.Defaults();

    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields)
    {
        ArgumentNullException.ThrowIfNull(displayFields);
        return MarkFindResultDisplay.Capture(displayFields);
    }

    /// <summary>已知设置的显式校验：范围越界在准入时给出明确诊断。</summary>
    public IReadOnlyList<Diagnostic> ValidateKnownSettings(FieldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<Diagnostic>();
        foreach (var spec in MarkFindFields.All)
        {
            var id = spec.Id;
            if (settings.Read(id) is not ConstantSetting constant) continue;
            if (constant.Value is long integer)
            {
                if (spec.Minimum is { } minimum && integer < minimum)
                    errors.Add(new("MARKFIND_SETTINGS_INVALID", spec.Label + "低于允许范围。", id));
                if (spec.Maximum is { } maximum && integer > maximum)
                    errors.Add(new("MARKFIND_SETTINGS_INVALID", spec.Label + "超出允许范围。", id));
            }
            else if (constant.Value is double number)
            {
                if (!double.IsFinite(number) || spec.Minimum is { } minimum && number < minimum || spec.Maximum is { } maximum && number > maximum)
                    errors.Add(new("MARKFIND_SETTINGS_INVALID", spec.Label + "超出允许范围。", id));
            }
        }
        return errors.AsReadOnly();
    }
}

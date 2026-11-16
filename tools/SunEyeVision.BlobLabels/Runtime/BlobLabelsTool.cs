using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobLabels.Runtime;

/// <summary>
/// Blob 标签分析（原件 <c>IMVSBlobFindLabelsModu</c>）的 Runtime 入口。
/// 一次调用只做一次同步算法；调度、输入准备与资源生命周期属于宿主。
/// </summary>
public sealed class BlobLabelsTool : IToolDefinition<BlobLabelsSettings, BlobLabelsOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider, IToolResultDisplayProvider,
    IToolParameterBindingNames, IToolResourceFieldDeclarations, IToolKnownSettingsValidation, IToolRuntimeInitialization
{
    public const string Id = "BlobLabels";

    private static readonly ToolSchema ExecutionSchema = BuildExecutionSchema();

    public string ToolId => Id;
    public ToolCatalogDescriptor Catalog { get; } = new("Blob标签分析", "按灰度标签分割并输出连通域特征与判定", "blob-label", "定位", 20, "DEFAULT");
    public ToolSchema SettingsSchema => BlobLabelsFields.Schema;
    public IReadOnlyDictionary<string, string> ParameterBindingNames => BlobLabelsFields.BindingNames;
    /// <summary>本工具没有图像/文件路径类设置：掩膜与 ROI 都以输入端口引用承载。</summary>
    public IReadOnlyList<ResourceFieldDeclaration> ResourceFields { get; } = Array.Empty<ResourceFieldDeclaration>();
    public ToolSchema ResultSchema => new(Array.Empty<Port>(), BlobLabelsSchema.Outputs, outputDisplay: BlobLabelsSchema.OutputDisplay);
    public ToolSchema DisplaySettingsSchema => BlobLabelsResultDisplay.Schema;
    public ResultProjectionDescriptor ResultProjection => Projection;
    public ResultDisplayDescriptor ResultDisplay => BlobLabelsResultDisplay.Descriptor;

    private static readonly ResultProjectionDescriptor Projection = BuildProjection();

    public ToolSchema Describe() => ExecutionSchema;

    private static ToolSchema BuildExecutionSchema()
        => new(new[]
        {
            new Port(BlobLabelsSchema.ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
            new Port(BlobLabelsSchema.MaskInput, TypeRef.ResourceOf(ResourceKind.Image), Required: false, Nullable: true,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference),
            new Port(BlobLabelsSchema.InspectionRoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
                EditModes: FieldEditModes.Reference),
            new Port(BlobLabelsSchema.AnnulusRoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
                EditModes: FieldEditModes.Reference),
            new Port(BlobLabelsSchema.MaskRegionInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
                EditModes: FieldEditModes.Reference),
            new Port(BlobLabelsSchema.FixtureInput, FixtureDeclarations.Info, Required: false, Nullable: true,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference)
        }, BlobLabelsSchema.Outputs, new ToolPresentation(Array.Empty<FieldPresentation>()),
            workflowImageInputId: BlobLabelsSchema.ImageInput, outputDisplay: BlobLabelsSchema.OutputDisplay);

    private static ResultProjectionDescriptor BuildProjection()
    {
        ResultColumnDescriptor Column(string key, string label, params string[] member)
            => new(key, label, new ResultFieldPath(BlobLabelsSchema.Rows, member), DecimalPlaces: member[0] is "element-id" ? 0 : 2,
                Geometry: new(BlobLabelsSchema.Rows, new[] { "centroid" }),
                GeometrySource: new(BlobLabelsSchema.Rows, new[] { "region" }));
        var columns = new[]
        {
            Column("element-id", "序号", "element-id"),
            Column("area", "面积", "area"),
            Column("perimeter", "周长", "perimeter"),
            Column("centroid-x", "质心X", "centroid-x"),
            Column("centroid-y", "质心Y", "centroid-y"),
            Column("circularity", "圆形度", "circularity"),
            Column("rectangularity", "矩形度", "rectangularity"),
            Column("long-axis", "长轴", "long-axis"),
            Column("short-axis", "短轴", "short-axis"),
            Column("angle", "Box角度", "oriented-bounds", "angle-degrees"),
            Column("judgment", "判定", "judgment-passed")
        };
        var projection = new ResultProjectionDescriptor(ResultProjectionKind.StandardTable, ResultCardinality.Many,
            columns, new ResultFieldPath(BlobLabelsSchema.Rows), "element-id");
        var errors = projection.Validate(new BlobLabelsTool().ResultSchema);
        if (errors.Count != 0)
            throw new InvalidOperationException("BlobLabels 结果投影声明无效：" + string.Join("；", errors.Select(error => error.Message)));
        BlobLabelsResultDisplay.Descriptor.Validate(new BlobLabelsTool().ResultSchema);
        return projection;
    }

    public IReadOnlyList<Diagnostic> Validate(BlobLabelsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    public IToolSession<BlobLabelsSettings, BlobLabelsOutput> CreateSession() => new BlobLabelsSession();

    public InputValues ReadSettings(object settings)
        => BlobLabelsFields.Read(settings as BlobLabelsSettings ?? throw new ArgumentException("BlobLabelsSettings is required.", nameof(settings)));

    public object MaterializeSettings(InputValues values) => BlobLabelsFields.Materialize(values);

    public InputValues CreateDisplaySettings() => BlobLabelsResultDisplay.Defaults();

    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings displayFields)
    {
        ArgumentNullException.ThrowIfNull(displayFields);
        return BlobLabelsResultDisplay.Capture(displayFields);
    }

    /// <summary>已知设置的显式校验：字段联动不出现在执行期，只在准入时给出明确诊断。</summary>
    public IReadOnlyList<Diagnostic> ValidateKnownSettings(FieldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<Diagnostic>();
        foreach (var spec in BlobLabelsFields.All)
        {
            var id = spec.Id;
            if (settings.Read(id) is not ConstantSetting constant) continue;
            if (constant.Value is long integer)
            {
                if (spec.Minimum is { } minimum && integer < minimum)
                    errors.Add(new("BLOBLABEL_SETTINGS_INVALID", spec.Label + "低于允许范围。", id));
                if (spec.Maximum is { } maximum && integer > maximum)
                    errors.Add(new("BLOBLABEL_SETTINGS_INVALID", spec.Label + "超出允许范围。", id));
            }
            else if (constant.Value is double number)
            {
                if (!double.IsFinite(number) || spec.Minimum is { } minimum && number < minimum || spec.Maximum is { } maximum && number > maximum)
                    errors.Add(new("BLOBLABEL_SETTINGS_INVALID", spec.Label + "超出允许范围。", id));
            }
        }
        if (settings.Read(BlobLabelsFields.ThresholdArea) is ConstantSetting { Value: string text })
        {
            try { _ = BlobLabelsThresholdAreas.Parse(text); }
            catch (ArgumentException error) { errors.Add(new("BLOBLABEL_THRESHOLD_INVALID", error.Message, BlobLabelsFields.ThresholdArea)); }
        }
        return errors.AsReadOnly();
    }
}

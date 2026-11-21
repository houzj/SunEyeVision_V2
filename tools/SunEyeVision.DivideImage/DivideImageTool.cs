using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.DivideImage.Runtime;

/// <summary>
/// 划片拆分（原件 <c>IMVSDivideImageModu</c>）：按 X/Y 方向划片数量与重叠率计算分片矩形；
/// 原件声明面只输出分片矩形集合与分片个数，不输出子图。
/// </summary>
public sealed class DivideImageTool : IToolDefinition<DivideImageSettings, DivideImageOutput>, IToolSettingsFields,
    IToolCatalogProvider, IToolResultPresentationProvider, IToolKnownSettingsValidation
{
    public const string Id = "DivideImage";
    public const string ImageInput = "input.image";
    /// <summary>原厂 InputROI/ROIBOX 输入，运行期边界取整语义仍待动态证据确认。</summary>
    public const string RoiInput = "input.roi";

    public string ToolId => Id;

    // 工具箱目录中 IMVSDivideImageModu 位于「拆分组合」组；运行时分类必须与之一致才能准入。
    public ToolCatalogDescriptor Catalog { get; } = new("划片拆分",
        "按X/Y方向划片数量与重叠率计算分片矩形，输出分片矩形集合与分片个数", "divide-image", "拆分组合", 10, "IMAGE");

    public ToolSchema SettingsSchema => DivideImageFields.Schema;

    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), DivideImageOutput.Ports,
        outputDisplay: DivideImageOutput.Display);

    public ResultProjectionDescriptor ResultProjection { get; } = new(ResultProjectionKind.PropertyList,
        ResultCardinality.One, new[]
        {
            new ResultColumnDescriptor("roi", "检测框", new(DivideImageOutput.Roi)),
            new ResultColumnDescriptor("rects", "分片矩形", new(DivideImageOutput.Rects)),
            new ResultColumnDescriptor("count", "分片个数", new(DivideImageOutput.Count), DecimalPlaces: 0),
            new ResultColumnDescriptor("time", "模块耗时", new(DivideImageOutput.ModuleRunTime), "ms", DecimalPlaces: 3)
        });

    public ToolSchema Describe() => Schema;

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
        new Port(RoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true,
            EditModes: FieldEditModes.Reference)
    }
        .Concat(new[] { new Port(DivideImageRoi.LocalDocumentId, RoiDocumentValue.Declaration,
            Required: false, EditModes: FieldEditModes.Constant) })
        .Concat(DivideImageRoi.Ports)
        .Concat(DivideImageAssist.Ports)
        .ToArray(), DivideImageOutput.Ports, new ToolPresentation(new[]
    {
        // 原厂 AlgorithmTab 的 ROI 来源由 ROISelectGroup 组装，不单独渲染为第二行。
        // 划片数量和重叠率属于 Tab_Run Params，由 DivideImageFields.Schema 声明。
        new FieldPresentation(ImageInput, "输入源", "图像输入", 0, FieldEditorKind.Image)
        {
            Layout = new("基本参数", "图像输入", AllowReference: true)
        }
    }),
        workflowImageInputId: ImageInput, outputDisplay: DivideImageOutput.Display);

    public InputValues ReadSettings(object settings) => DivideImageFields.Read(RequireSettings(settings));

    public object MaterializeSettings(InputValues values) => DivideImageFields.Materialize(values);

    private static DivideImageSettings RequireSettings(object settings) => settings as DivideImageSettings
        ?? throw new ArgumentException("划片拆分设置类型不正确。");

    public IReadOnlyList<Diagnostic> Validate(DivideImageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    /// <summary>编辑期显式校验：常量字段立即给出诊断，引用/变量值留到执行期解析。</summary>
    public IReadOnlyList<Diagnostic> ValidateKnownSettings(FieldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<Diagnostic>();
        CheckInteger(settings, DivideImageFields.Columns, "X方向划片数量", 1, DivideImageFields.MaximumDivisionsPerAxis, "DIVIDE_IMAGE_COLUMNS", errors);
        CheckInteger(settings, DivideImageFields.Rows, "Y方向划片数量", 1, DivideImageFields.MaximumDivisionsPerAxis, "DIVIDE_IMAGE_ROWS", errors);
        CheckInteger(settings, DivideImageFields.OverlapX, "X方向重叠率",
            DivideImageFields.MinimumOverlapPercent, DivideImageFields.MaximumOverlapPercent, "DIVIDE_IMAGE_OVERLAP_X", errors);
        CheckInteger(settings, DivideImageFields.OverlapY, "Y方向重叠率",
            DivideImageFields.MinimumOverlapPercent, DivideImageFields.MaximumOverlapPercent, "DIVIDE_IMAGE_OVERLAP_Y", errors);
        return errors.AsReadOnly();
    }

    private static void CheckInteger(FieldSettings settings, string fieldId, string label, long minimum, long? maximum,
        string code, List<Diagnostic> errors)
    {
        if (settings.Read(fieldId) is not ConstantSetting { Value: long value }) return;
        if (value < minimum)
            errors.Add(new(code, $"{label}必须≥{minimum}，实际 {value}。", fieldId));
        else if (maximum is { } upper && value > upper)
            errors.Add(new(code, $"{label}必须≤{upper}，实际 {value}。", fieldId));
    }

    public ToolCapabilities Capabilities() => ToolCapabilities.None;

    public IToolSession<DivideImageSettings, DivideImageOutput> CreateSession() => new DivideImageSession();
}

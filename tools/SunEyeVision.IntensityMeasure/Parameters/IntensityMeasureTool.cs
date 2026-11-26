using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 亮度测量（原件 <c>IMVSIntensityMeasureModu</c>）的 Runtime 入口。
/// 一次调用只做一次同步统计；调度、输入准备与资源生命周期属于宿主。
/// 声明与契约生成由 <see cref="ToolDefinition{TSettings,TOutput}"/> 承担。
/// </summary>
public sealed class IntensityMeasureTool : ToolDefinition<IntensityMeasureSettings, IntensityMeasureOutput>,
    IToolRuntimeInitialization
{
    public const string Id = "IntensityMeasure";
    public const string ImageInput = "input.image";
    /// <summary>原厂 InputROI（ROIBOX）/InputROIAnnulus（ROIANNULUS）在本方合并为一个区域输入；缺省即整幅图像。</summary>
    public const string RoiInput = "input.roi";
    /// <summary>原厂 InputMask（IMAGE）。</summary>
    public const string MaskInput = "input.mask";

    protected override ToolManifest Manifest => IntensityMeasureManifest.Value;

    /// <summary>
    /// 原厂准入语义：区间上下限颠倒属于配置错误，物化时直接拒绝（不夹取、不静默修正）。
    /// </summary>
    public override object MaterializeSettings(InputValues values)
    {
        var settings = (IntensityMeasureSettings)base.MaterializeSettings(values);
        var errors = settings.Validate();
        if (errors.Count != 0)
            throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return settings;
    }

    /// <summary>
    /// 原厂「ROI区域」分组锚点：纯界面占位，没有对应 C# 属性，
    /// 因此不写进参数表（避免反射往返），只作为可缺省端口与呈现追加。
    /// </summary>
    protected override ToolSchema ExtendSettings(ToolSchema schema)
    {
        var ports = new List<Port>(schema.Inputs)
        {
            new Port(IntensityMeasureFields.RoiSectionAnchor, DataType.Boolean,
                Required: false, EditModes: FieldEditModes.Constant)
        };
        var fields = new List<FieldPresentation>(
            schema.Presentation?.Fields ?? Array.Empty<FieldPresentation>())
        {
            new FieldPresentation(IntensityMeasureFields.RoiSectionAnchor, "ROI区域", "ROI区域", 60,
                FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Boolean)
            {
                Layout = new FieldLayout("基本参数", "ROI区域", AllowReference: false)
            }
        };
        return new ToolSchema(ports, schema.Outputs, new ToolPresentation(fields),
            schema.WorkflowImageInputId, schema.OutputDisplay, schema.ControlOutputs,
            schema.RoiGroups, schema.AssistGroups, schema.ExternalInputs);
    }

    /// <summary>执行 Schema：描述 = 输入 + 参数，供宿主装配。</summary>
    public static ToolSchema Schema => new IntensityMeasureTool().Describe();

    public override IToolSession<IntensityMeasureSettings, IntensityMeasureOutput> CreateSession()
        => new IntensityMeasureSession();

    public override IReadOnlyList<Diagnostic> Validate(IntensityMeasureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public override ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();
}

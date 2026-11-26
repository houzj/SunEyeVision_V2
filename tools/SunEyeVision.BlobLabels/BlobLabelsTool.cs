using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobLabels.Runtime;

/// <summary>
/// Blob 标签分析（原件 <c>IMVSBlobFindLabelsModu</c>）。
/// 声明与契约生成由 <see cref="ToolDefinition{TSettings,TOutput}"/> 承担；
/// 绑定名、ROI 编辑声明、轮廓点出口与编辑期校验由工具侧实现。
/// </summary>
public sealed class BlobLabelsTool : ToolDefinition<BlobLabelsSettings, BlobLabelsOutput>,
    IToolParameterBindingNames, IToolResourceFieldDeclarations, IToolKnownSettingsValidation,
    IToolRuntimeInitialization, IToolResultOutlineDeclaration
{
    public const string Id = "BlobLabels";

    /// <summary>轮廓点出口（系统类型 IToolResultOutlineDeclaration）。</summary>
    public string OutlinePortId => BlobLabelsSchema.ContourPoints;

    /// <summary>结果视图与参数页共用的 ROI 编辑声明。</summary>
    public RoiEditDeclaration RoiEdit => BlobLabelsRoi.RoiEdit;

    protected override ToolManifest Manifest => BlobLabelsManifest.Value;

    /// <summary>执行 Schema：描述 = 输入 + 参数 + 输出，供宿主装配。</summary>
    public static ToolSchema Schema => new BlobLabelsTool().Describe();

    /// <summary>原厂 VM 参数名 → 本工具字段身份（算法按名字取参数）。</summary>
    public IReadOnlyDictionary<string, string> ParameterBindingNames => BlobLabelsFields.BindingNames;

    /// <summary>本工具没有图像/文件路径类设置：掩膜与 ROI 都以输入端口引用承载。</summary>
    public IReadOnlyList<ResourceFieldDeclaration> ResourceFields { get; } = Array.Empty<ResourceFieldDeclaration>();

    public override IToolSession<BlobLabelsSettings, BlobLabelsOutput> CreateSession() => new BlobLabelsSession();

    public override IReadOnlyList<Diagnostic> Validate(BlobLabelsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
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

    public override ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();
}

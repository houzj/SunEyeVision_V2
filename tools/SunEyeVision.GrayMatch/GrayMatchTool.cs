using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.GrayMatch.Runtime;

/// <summary>
/// 灰度匹配（原件 <c>IMVSGrayMatchModu</c>）。
/// 声明与契约生成由 <see cref="ToolDefinition{TSettings,TOutput}"/> 承担；
/// 参数绑定名、资源字段与编辑期校验由工具侧实现。
/// </summary>
public sealed class GrayMatchTool : ToolDefinition<GrayMatchSettings, GrayMatchOutput>,
    IToolParameterBindingNames, IToolResourceFieldDeclarations, IToolKnownSettingsValidation,
    IToolRuntimeInitialization
{
    public const string Id = "GrayMatch";

    protected override ToolManifest Manifest => GrayMatchManifest.Value;

    /// <summary>执行 Schema：描述 = 输入 + 参数 + 输出，供宿主装配。</summary>
    public static ToolSchema Schema => new GrayMatchTool().Describe();

    /// <summary>原厂 VM 参数名 → 本工具字段身份（算法按名字取参数）。</summary>
    public IReadOnlyDictionary<string, string> ParameterBindingNames => GrayMatchFields.BindingNames;

    /// <summary>本工具没有图像/文件路径类设置：模板数据随设置内联，搜索区域以输入端口引用承载。</summary>
    public IReadOnlyList<ResourceFieldDeclaration> ResourceFields { get; } = Array.Empty<ResourceFieldDeclaration>();

    public override IToolSession<GrayMatchSettings, GrayMatchOutput> CreateSession() => new GrayMatchSession();

    public override IReadOnlyList<Diagnostic> Validate(GrayMatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
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

    public override ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();
}

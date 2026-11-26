using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;

namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>
/// 色彩提取（原件 IMVSColorExtractModu）Runtime 入口；一次调用只处理一帧。
/// 声明与契约生成由 <see cref="ToolDefinition{TSettings,TOutput}"/> 承担；
/// 编辑期的区间文本与面积上下限准入保留在工具侧。
/// </summary>
public sealed class ColorExtractTool : ToolDefinition<ColorExtractSettings, ColorExtractOutput>,
    IToolRuntimeInitialization, IToolKnownSettingsValidation
{
    public const string Id = "ColorExtract";
    public const string ImageInput = "input.image";

    protected override ToolManifest Manifest => ColorExtractManifest.Value;

    /// <summary>执行 Schema：描述 = 输入 + 参数，供宿主装配。</summary>
    public static ToolSchema Schema => new ColorExtractTool().Describe();

    public override IToolSession<ColorExtractSettings, ColorExtractOutput> CreateSession()
        => new ColorExtractSession();

    public override IReadOnlyList<Diagnostic> Validate(ColorExtractSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    /// <summary>
    /// 编辑期显式校验：只判定已解析为常量的字段，引用/变量值留到执行期解析，
    /// 让参数页对非法区间文本与颠倒的面积上下限给出明确提示而不是抛出异常。
    /// </summary>
    public IReadOnlyList<Diagnostic> ValidateKnownSettings(FieldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<Diagnostic>();
        if (settings.Read(ColorExtractFields.ColorRange) is ConstantSetting { Value: string text })
        {
            try { _ = ColorExtractRanges.Parse(text); }
            catch (ArgumentException error)
            {
                errors.Add(new("COLOR_EXTRACT_RANGE_INVALID", error.Message, ColorExtractFields.ColorRange));
            }
        }
        if (settings.Read(ColorExtractFields.AreaLimitEnabled) is ConstantSetting { Value: true })
        {
            var low = Constant(settings, ColorExtractFields.AreaLimitLow);
            var high = Constant(settings, ColorExtractFields.AreaLimitHigh);
            if (low is { } minimum && (minimum < ColorExtractFields.MinimumAreaLimit || minimum > ColorExtractFields.MaximumAreaLimit))
                errors.Add(new("COLOR_EXTRACT_AREA_LIMIT",
                    $"面积下限必须在 {ColorExtractFields.MinimumAreaLimit}..{ColorExtractFields.MaximumAreaLimit}，实际 {minimum}。",
                    ColorExtractFields.AreaLimitLow));
            if (low is { } lower && high is { } upper && (upper < lower || upper > ColorExtractFields.MaximumAreaLimit))
                errors.Add(new("COLOR_EXTRACT_AREA_LIMIT",
                    $"面积上限必须在 {lower}..{ColorExtractFields.MaximumAreaLimit}，实际 {upper}。",
                    ColorExtractFields.AreaLimitHigh));
        }
        return errors.AsReadOnly();
    }

    private static long? Constant(FieldSettings settings, string fieldId)
        => settings.Read(fieldId) is ConstantSetting { Value: long value } ? value : null;

    public override ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();
}

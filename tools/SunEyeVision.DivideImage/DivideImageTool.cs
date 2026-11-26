using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;

namespace SunEyeVision.Tool.DivideImage.Runtime;

/// <summary>
/// 划片拆分（原件 <c>IMVSDivideImageModu</c>）：按 X/Y 方向划片数量与重叠率计算分片矩形；
/// 原件声明面只输出分片矩形集合与分片个数，不输出子图。
/// 声明与契约生成由 <see cref="ToolDefinition{TSettings,TOutput}"/> 承担；
/// 编辑期的常量准入保留在工具侧（<see cref="IToolKnownSettingsValidation"/>）。
/// </summary>
public sealed class DivideImageTool : ToolDefinition<DivideImageSettings, DivideImageOutput>,
    IToolKnownSettingsValidation
{
    public const string Id = "DivideImage";
    public const string ImageInput = "input.image";
    /// <summary>原厂 InputROI/ROIBOX 输入，运行期边界取整语义仍待动态证据确认。</summary>
    public const string RoiInput = "input.roi";

    protected override ToolManifest Manifest => DivideImageManifest.Value;

    /// <summary>执行 Schema：描述 = 输入 + 参数，供宿主装配。</summary>
    public static ToolSchema Schema => new DivideImageTool().Describe();

    public override IToolSession<DivideImageSettings, DivideImageOutput> CreateSession()
        => new DivideImageSession();

    public override IReadOnlyList<Diagnostic> Validate(DivideImageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    /// <summary>编辑期显式校验：常量字段立即给出诊断，引用/变量值留到执行期解析。</summary>
    public IReadOnlyList<Diagnostic> ValidateKnownSettings(FieldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<Diagnostic>();
        CheckInteger(settings, DivideImageFields.Columns, "X方向划片数量", 1,
            DivideImageFields.MaximumDivisionsPerAxis, "DIVIDE_IMAGE_COLUMNS", errors);
        CheckInteger(settings, DivideImageFields.Rows, "Y方向划片数量", 1,
            DivideImageFields.MaximumDivisionsPerAxis, "DIVIDE_IMAGE_ROWS", errors);
        CheckInteger(settings, DivideImageFields.OverlapX, "X方向重叠率",
            DivideImageFields.MinimumOverlapPercent, DivideImageFields.MaximumOverlapPercent,
            "DIVIDE_IMAGE_OVERLAP_X", errors);
        CheckInteger(settings, DivideImageFields.OverlapY, "Y方向重叠率",
            DivideImageFields.MinimumOverlapPercent, DivideImageFields.MaximumOverlapPercent,
            "DIVIDE_IMAGE_OVERLAP_Y", errors);
        return errors.AsReadOnly();
    }

    private static void CheckInteger(FieldSettings settings, string fieldId, string label, long minimum,
        long? maximum, string code, List<Diagnostic> errors)
    {
        if (settings.Read(fieldId) is not ConstantSetting { Value: long value }) return;
        if (value < minimum)
            errors.Add(new(code, $"{label}必须≥{minimum}，实际 {value}。", fieldId));
        else if (maximum is { } upper && value > upper)
            errors.Add(new(code, $"{label}必须≤{upper}，实际 {value}。", fieldId));
    }
}

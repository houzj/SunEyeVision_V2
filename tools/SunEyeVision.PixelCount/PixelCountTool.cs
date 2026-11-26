using System.Globalization;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// 像素统计（原件 <c>IMVSPixelCountModuVA</c>）的 Runtime 入口。
/// 声明与契约生成全部由 <see cref="ToolDefinition{TSettings,TOutput}"/> 承担，
/// 本类只保留：槽位扩展、区间列表读写与算法会话。
/// </summary>
public sealed class PixelCountTool : ToolDefinition<PixelCountSettings, PixelCountOutput>, IToolRuntimeInitialization
{
    public const string Id = "PixelCount";
    public const string ImageInput = "input.image";
    /// <summary>原厂 InputROI（ROIBOX）/InputROIAnnulus（ROIANNULUS）在本方合并为一个区域输入；缺省即整幅图像。</summary>
    public const string RoiInput = "input.roi";
    /// <summary>原厂 InputMask（IMAGE）。</summary>
    public const string MaskInput = "input.mask";

    protected override ToolManifest Manifest => PixelCountManifest.Value;

    /// <summary>执行 Schema：描述 = 输入 + 参数，供宿主装配。</summary>
    public static ToolSchema Schema => new PixelCountTool().Describe();

    public override IToolSession<PixelCountSettings, PixelCountOutput> CreateSession() => new PixelCountSession();

    public override IReadOnlyList<Diagnostic> Validate(PixelCountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public override ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();

    // ---------------- 32 组区间槽位（原厂 IDynamicChild 的静态等价） ----------------

    protected override ToolSchema ExtendSettings(ToolSchema schema) => WithRangeSlots(schema);

    protected override ToolSchema ExtendExecution(ToolSchema schema) => WithRangeSlots(schema);

    private static ToolSchema WithRangeSlots(ToolSchema schema)
    {
        var ports = new List<Port>(schema.Inputs);
        var presentations = new List<FieldPresentation>(
            schema.Presentation?.Fields ?? Array.Empty<FieldPresentation>());
        var order = presentations.Count == 0 ? 0 : presentations.Max(field => field.Order) + 1;
        for (var index = 0; index < PixelCountRanges.MaximumCount; index++)
        {
            var label = index.ToString(CultureInfo.InvariantCulture);
            // 槽位端口可缺省：基类反射只覆盖标量参数，槽位值由本类的读写补进来。
            var minimum = new Port(PixelCountFields.RangeMinimum(index), DataType.Integer,
                Required: false, EditModes: FieldEditModes.Constant | FieldEditModes.Reference);
            var maximum = new Port(PixelCountFields.RangeMaximum(index), DataType.Integer,
                Required: false, EditModes: FieldEditModes.Constant | FieldEditModes.Reference);
            ports.Add(minimum);
            ports.Add(maximum);
            presentations.Add(new FieldPresentation(minimum.Id, label, "阈值范围", order++,
                FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.IntegerBettween)
            {
                Layout = new FieldLayout("运行参数", "阈值范围",
                    Minimum: PixelCountRanges.MinimumGray, Maximum: PixelCountRanges.MaximumGray,
                    DecimalPlaces: 0, AllowReference: true, Increment: 1,
                    PairWith: maximum.Id, PairMinimum: PixelCountRanges.MinimumGray,
                    PairMaximum: PixelCountRanges.MaximumGray, PairDisplayName: label)
            });
            presentations.Add(new FieldPresentation(maximum.Id, "上限", "阈值范围", order++,
                FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.Integer)
            {
                Layout = new FieldLayout("运行参数", "阈值范围",
                    Minimum: PixelCountRanges.MinimumGray, Maximum: PixelCountRanges.MaximumGray,
                    DecimalPlaces: 0, AllowReference: true, Increment: 1)
            });
        }
        return new ToolSchema(ports, schema.Outputs, new ToolPresentation(presentations),
            schema.WorkflowImageInputId, schema.OutputDisplay, schema.ControlOutputs,
            schema.RoiGroups, schema.AssistGroups, schema.ExternalInputs);
    }

    public override InputValues ReadSettings(object settings)
    {
        var typed = RequireSettings(settings);
        // 基类只读标量参数；槽位值在这里补齐，保证 InputValues 覆盖完整端口集。
        var values = base.ReadSettings(typed).CopyValues()
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        for (var index = 0; index < PixelCountRanges.MaximumCount; index++)
        {
            var range = index < typed.Ranges.Count ? typed.Ranges[index] : PixelCountRanges.Default;
            values[PixelCountFields.RangeMinimum(index)] = (long)range.Minimum;
            values[PixelCountFields.RangeMaximum(index)] = (long)range.Maximum;
        }
        return new InputValues(SettingsSchema.Inputs, values);
    }

    public override object MaterializeSettings(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var scalars = (PixelCountSettings)base.MaterializeSettings(values);
        var ranges = new List<PixelCountRange>(PixelCountRanges.MaximumCount);
        for (var index = 0; index < PixelCountRanges.MaximumCount; index++)
        {
            var minimum = values.Contains(PixelCountFields.RangeMinimum(index))
                ? Convert.ToInt64(values.Get<object?>(PixelCountFields.RangeMinimum(index)),
                    CultureInfo.InvariantCulture)
                : PixelCountRanges.Default.Minimum;
            var maximum = values.Contains(PixelCountFields.RangeMaximum(index))
                ? Convert.ToInt64(values.Get<object?>(PixelCountFields.RangeMaximum(index)),
                    CultureInfo.InvariantCulture)
                : PixelCountRanges.Default.Maximum;
            ranges.Add(new(checked((int)minimum), checked((int)maximum)));
        }
        var result = (scalars with { Ranges = ranges.AsReadOnly() }).Freeze();
        // 区间参与准入校验（上下限、条数），必须在校验前补齐。
        var errors = result.Validate();
        if (errors.Count != 0)
            throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return result;
    }

    private static PixelCountSettings RequireSettings(object settings) => settings as PixelCountSettings
        ?? throw new ArgumentException("像素统计设置类型不正确。");
}

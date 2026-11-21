using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// PixelCount 的原厂声明映射。静态参数页由公共 DeclaredParameterSchema 生成；
/// 阈值区间按原厂 <c>PixelModuRangeConfigList</c> 的语义提供 32 组固定端点槽位
/// （<c>range.{i}.min/max</c>），有效条数由 <c>range.count</c> 控制。
/// 槽位是普通声明字段，因此区间端点可以直接使用公共的成对编辑与引用会话。
/// </summary>
public sealed class PixelCountParameterDefinition
{
    private readonly DeclaredParameterSchema _schema;
    private readonly ToolSchema _settingsSchema;
    private readonly IReadOnlyList<SettingBinding> _bindings;

    public static PixelCountParameterDefinition Instance { get; } = Load();

    private PixelCountParameterDefinition(DeclaredParameterSchema schema)
    {
        _schema = schema;
        if (schema.WorkflowImageInputId != PixelCountTool.ImageInput)
            throw new InvalidDataException("PixelCount workflow image input differs from the original mapping.");
        if (!schema.InputIds.ToHashSet(StringComparer.Ordinal)
            .SetEquals(new[] { PixelCountTool.ImageInput, PixelCountTool.RoiInput, PixelCountTool.MaskInput }))
            throw new InvalidDataException("PixelCount input mapping differs from the original mapping.");
        _settingsSchema = Extend(schema.Schema);
        _bindings = BuildBindings();
    }

    private static PixelCountParameterDefinition Load()
    {
        using var stream = typeof(PixelCountParameterDefinition).Assembly
            .GetManifestResourceStream("PixelCount.PixelCountParameters.xml")
            ?? throw new InvalidDataException("PixelCountParameters.xml is missing from the single project.");
        var ports = new[]
        {
            new Port(PixelCountTool.ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
            new Port(PixelCountTool.RoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
            new Port(PixelCountTool.MaskInput, TypeRef.ResourceOf(ResourceKind.Image), Required: false, Nullable: true, EditModes: FieldEditModes.Reference)
        };
        var schema = DeclaredParameterSchema.LoadWithTypes(
            stream, typeof(PixelCountSettings), null, null, ports);
        return new PixelCountParameterDefinition(schema);
    }

    public ToolSchema SettingsSchema => _settingsSchema;
    public string WorkflowImageInputId => _schema.WorkflowImageInputId;
    public string SettingsFieldId(string property) => _schema.SettingsFieldId(property);
    public FieldPresentation InputPresentation(Port port, int order) => _schema.InputPresentation(port, order);

    private sealed record SettingBinding(string FieldId,
        Func<PixelCountSettings, object?> Read, Func<PixelCountSettings, object?, PixelCountSettings> Write);

    private IReadOnlyList<SettingBinding> BuildBindings()
    {
        var bindings = new List<SettingBinding>();
        foreach (var property in typeof(PixelCountSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name == nameof(PixelCountSettings.Ranges)) continue;
            if (!_schema.Contains(property.Name))
                throw new InvalidDataException($"PixelCount setting is missing from XML: {property.Name}");
            var fieldId = _schema.SettingsFieldId(property.Name);
            bindings.Add(new(
                fieldId,
                settings => property.GetValue(settings),
                (settings, value) => Write(settings, property, value)));
        }
        return bindings.AsReadOnly();
    }

    private static PixelCountSettings Write(PixelCountSettings settings, PropertyInfo property, object? value)
    {
        var target = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        object converted = target == typeof(long)
            ? value switch { long l => l, int i => (long)i, _ => throw new ArgumentException($"字段 {property.Name} 必须是整数。") }
            : target == typeof(double)
                ? value switch { double d => d, float f => (double)f, long l => l, int i => i, _ => throw new ArgumentException($"字段 {property.Name} 必须是数值。") }
                : target == typeof(bool)
                    ? value is bool b ? b : throw new ArgumentException($"字段 {property.Name} 必须是布尔值。")
                    : target == typeof(string)
                        ? value as string ?? throw new ArgumentException($"字段 {property.Name} 必须是文本。")
                        : throw new InvalidDataException($"Unsupported PixelCount setting type: {target.Name}");
        return WithProperty(settings, property, converted);
    }

    private static PixelCountSettings WithProperty(PixelCountSettings settings, PropertyInfo property, object value)
    {
        return property.Name switch
        {
            nameof(PixelCountSettings.RoiAreaType) => settings with { RoiAreaType = (long)value },
            nameof(PixelCountSettings.OutputMaskEnabled) => settings with { OutputMaskEnabled = (bool)value },
            nameof(PixelCountSettings.PositionCorrectionEnabled) => settings with { PositionCorrectionEnabled = (bool)value },
            nameof(PixelCountSettings.RangeCount) => settings with { RangeCount = (long)value },
            nameof(PixelCountSettings.CountLimitEnabled) => settings with { CountLimitEnabled = (bool)value },
            nameof(PixelCountSettings.CountLimitLow) => settings with { CountLimitLow = (long)value },
            nameof(PixelCountSettings.CountLimitHigh) => settings with { CountLimitHigh = (long)value },
            nameof(PixelCountSettings.RatioLimitEnabled) => settings with { RatioLimitEnabled = (bool)value },
            nameof(PixelCountSettings.RatioLimitLow) => settings with { RatioLimitLow = (double)value },
            nameof(PixelCountSettings.RatioLimitHigh) => settings with { RatioLimitHigh = (double)value },
            _ => throw new InvalidDataException($"Unknown PixelCount setting: {property.Name}")
        };
    }

    public InputValues Read(PixelCountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var values = _bindings.ToDictionary(binding => binding.FieldId, binding => binding.Read(settings), StringComparer.Ordinal);
        for (var i = 0; i < PixelCountRanges.MaximumCount; i++)
        {
            var range = i < settings.Ranges.Count ? settings.Ranges[i] : PixelCountRanges.Default;
            values[PixelCountFields.RangeMinimum(i)] = (long)range.Minimum;
            values[PixelCountFields.RangeMaximum(i)] = (long)range.Maximum;
        }
        return new InputValues(SettingsSchema.Inputs, values);
    }

    public PixelCountSettings Materialize(InputValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var settings = new PixelCountSettings();
        foreach (var binding in _bindings)
            settings = binding.Write(settings, values.Get<object?>(binding.FieldId));
        var ranges = new List<PixelCountRange>(PixelCountRanges.MaximumCount);
        for (var i = 0; i < PixelCountRanges.MaximumCount; i++)
        {
            var minId = PixelCountFields.RangeMinimum(i);
            var maxId = PixelCountFields.RangeMaximum(i);
            var minimum = values.Contains(minId)
                ? Convert.ToInt64(values.Get<object?>(minId), CultureInfo.InvariantCulture)
                : PixelCountRanges.Default.Minimum;
            var maximum = values.Contains(maxId)
                ? Convert.ToInt64(values.Get<object?>(maxId), CultureInfo.InvariantCulture)
                : PixelCountRanges.Default.Maximum;
            ranges.Add(new(checked((int)minimum), checked((int)maximum)));
        }
        settings = settings with { Ranges = ranges.AsReadOnly() };
        var errors = settings.Validate();
        if (errors.Count != 0)
            throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return settings.Freeze();
    }

    /// <summary>在 XML 声明之后追加原厂 32 组区间端点槽位。</summary>
    private static ToolSchema Extend(ToolSchema declared)
    {
        var ports = new List<Port>(declared.Inputs);
        var presentations = new List<FieldPresentation>();
        if (declared.Presentation is { } existing) presentations.AddRange(existing.Fields);
        var order = presentations.Count == 0 ? 0 : presentations.Max(field => field.Order) + 1;
        for (var i = 0; i < PixelCountRanges.MaximumCount; i++)
        {
            var minimum = new Port(PixelCountFields.RangeMinimum(i), DataType.Integer,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference);
            var maximum = new Port(PixelCountFields.RangeMaximum(i), DataType.Integer,
                EditModes: FieldEditModes.Constant | FieldEditModes.Reference);
            ports.Add(minimum); ports.Add(maximum);
            var label = i.ToString(CultureInfo.InvariantCulture);
            presentations.Add(new FieldPresentation(minimum.Id, label, "阈值范围", order++, FieldEditorKind.Scalar)
            {
                Layout = new FieldLayout("运行参数", "阈值范围",
                    Minimum: PixelCountRanges.MinimumGray, Maximum: PixelCountRanges.MaximumGray,
                    DecimalPlaces: 0, AllowReference: true, Increment: 1,
                    PairWith: maximum.Id, PairMinimum: PixelCountRanges.MinimumGray,
                    PairMaximum: PixelCountRanges.MaximumGray, PairDisplayName: label)
            });
            presentations.Add(new FieldPresentation(maximum.Id, "上限", "阈值范围", order++, FieldEditorKind.Scalar)
            {
                Layout = new FieldLayout("运行参数", "阈值范围",
                    Minimum: PixelCountRanges.MinimumGray, Maximum: PixelCountRanges.MaximumGray,
                    DecimalPlaces: 0, AllowReference: true, Increment: 1)
            });
        }
        return new ToolSchema(ports, declared.Outputs, new ToolPresentation(presentations), declared.WorkflowImageInputId,
            declared.OutputDisplay, declared.ControlOutputs, declared.RoiGroups, declared.AssistGroups, declared.ExternalInputs);
    }
}

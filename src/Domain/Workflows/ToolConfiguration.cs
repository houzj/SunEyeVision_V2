using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Domain.Workflows;

/// <summary>Immutable version of one tool's persisted field state and in-session current values.</summary>
public sealed class ToolConfiguration
{
    private readonly IReadOnlyDictionary<string, ConstantSetting> _currentValues;
    private readonly IReadOnlyDictionary<string, long> _revisions;
    private readonly IReadOnlyList<string> _bindingOrder;

    public string ToolId { get; }
    public FieldSettings Fields { get; }
    public IReadOnlyList<string> ParameterBindingOrder => _bindingOrder;

    public ToolConfiguration(string toolId, FieldSettings fields,
        IReadOnlyDictionary<string, ConstantSetting>? currentValues = null,
        IReadOnlyDictionary<string, long>? revisions = null,
        IReadOnlyList<string>? parameterBindingOrder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(fields);
        ToolId = toolId;
        Fields = fields;
        var current = new Dictionary<string, ConstantSetting>(StringComparer.Ordinal);
        foreach (var (id, value) in currentValues ?? new Dictionary<string, ConstantSetting>())
        {
            if (!fields.Declarations.TryGetValue(id, out var port) ||
                fields.Read(id) is not (ReferenceSetting or VariableSetting))
                throw new ArgumentException("Current values require a declared referenced field: " + id);
            if (ContainsResource(port.Type))
                throw new ArgumentException("Current values cannot own invocation resources: " + id);
            current.Add(id, new ConstantSetting(ValueRules.Freeze(port, value.Value)));
        }
        if (revisions?.Any(pair => !current.ContainsKey(pair.Key) || pair.Value < 0) == true)
            throw new ArgumentException("Current value revisions require present values.", nameof(revisions));
        _currentValues = new ReadOnlyDictionary<string, ConstantSetting>(current);
        _revisions = new ReadOnlyDictionary<string, long>(current.Keys.ToDictionary(
            id => id, id => revisions?.GetValueOrDefault(id) ?? 0, StringComparer.Ordinal));
        var order = (parameterBindingOrder ?? Array.Empty<string>()).ToArray();
        if (order.Any(string.IsNullOrWhiteSpace) || order.Distinct(StringComparer.Ordinal).Count() != order.Length)
            throw new ArgumentException("Parameter binding order must contain unique field identities.", nameof(parameterBindingOrder));
        _bindingOrder = Array.AsReadOnly(order);
    }

    public ConstantSetting? ReadCurrentParameter(string fieldId)
    {
        var value = Fields.Read(fieldId);
        if (value is ConstantSetting constant) return constant;
        return _currentValues.TryGetValue(fieldId, out var current)
            ? new ConstantSetting(ValueRules.Freeze(Fields.Declarations[fieldId], current.Value)) : null;
    }

    public IReadOnlyDictionary<string, ConstantSetting> CaptureCurrentValues() =>
        new ReadOnlyDictionary<string, ConstantSetting>(_currentValues.ToDictionary(
            pair => pair.Key,
            pair => new ConstantSetting(ValueRules.Freeze(Fields.Declarations[pair.Key], pair.Value.Value)),
            StringComparer.Ordinal));

    internal IReadOnlyDictionary<string, long> CaptureRevisions() =>
        new ReadOnlyDictionary<string, long>(_revisions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

    public ToolConfiguration Copy() => new(ToolId, Fields, CaptureCurrentValues(), CaptureRevisions(), _bindingOrder);

    /// <summary>Undo/redo retains live runtime values for remaining referenced fields.</summary>
    internal ToolConfiguration PreserveCurrentValuesFrom(ToolConfiguration live, IReadOnlySet<string> parameterIds)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(parameterIds);
        if (ToolId != live.ToolId) throw new ArgumentException("当前值属于其他工具声明。", nameof(live));
        var values = new Dictionary<string, ConstantSetting>(StringComparer.Ordinal);
        var revisions = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var id in parameterIds)
        {
            if (Fields.Read(id) is not (ReferenceSetting or VariableSetting)) continue;
            if (live.ReadCurrentParameter(id) is not { } current) continue;
            values.Add(id, current);
            revisions.Add(id, live.CurrentRevision(id));
        }
        return new ToolConfiguration(ToolId, Fields, values, revisions, _bindingOrder);
    }
    public long CurrentRevision(string fieldId) => _revisions.GetValueOrDefault(fieldId);

    internal ToolConfiguration WithAcceptedParameterValue(string fieldId, object? value)
    {
        if (!Fields.Declarations.TryGetValue(fieldId, out var port) ||
            Fields.Read(fieldId) is not (ReferenceSetting or VariableSetting))
            throw new ArgumentException("Only a declared referenced parameter may accept a current value.", nameof(fieldId));
        if (ContainsResource(port.Type)) throw new ArgumentException("Current values cannot own invocation resources.");
        var current = CaptureCurrentValues().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var revisions = _revisions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        current[fieldId] = new ConstantSetting(ValueRules.Freeze(port, value));
        revisions[fieldId] = checked(CurrentRevision(fieldId) + 1);
        return new ToolConfiguration(ToolId, Fields, current, revisions, _bindingOrder);
    }

    public bool CurrentValueChangedFrom(string fieldId, ToolConfiguration opening)
    {
        ArgumentNullException.ThrowIfNull(opening);
        return CurrentRevision(fieldId) != opening.CurrentRevision(fieldId) ||
            CurrentParameterIdentity(fieldId) != opening.CurrentParameterIdentity(fieldId);
    }

    /// <summary>Merge a private draft against the live version; same-value concurrent writes agree.</summary>
    internal ToolConfiguration MergeCurrentValues(ToolConfiguration live, ToolConfiguration? opening, IReadOnlySet<string> parameterIds)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(parameterIds);
        if (opening is null) return PreserveCurrentValuesFrom(live, parameterIds);
        if (ToolId != live.ToolId || ToolId != opening.ToolId)
            throw new ArgumentException("Current parameter values belong to another tool declaration.");
        var values = new Dictionary<string, ConstantSetting>(StringComparer.Ordinal);
        var revisions = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var id in parameterIds)
        {
            if (Fields.Read(id) is not (ReferenceSetting or VariableSetting)) continue;
            var changed = CurrentValueChangedFrom(id, opening);
            if (changed && live.CurrentValueChangedFrom(id, opening) &&
                CurrentParameterIdentity(id) != live.CurrentParameterIdentity(id))
                throw new InvalidOperationException("参数当前值已被正式运行和私有调试分别改变，请重新确认：" + id);
            var selected = changed ? ReadCurrentParameter(id) : live.ReadCurrentParameter(id);
            if (selected is null) continue;
            values.Add(id, selected);
            revisions.Add(id, changed ? checked(live.CurrentRevision(id) + 1) : live.CurrentRevision(id));
        }
        return new ToolConfiguration(ToolId, Fields, values, revisions, _bindingOrder);
    }

    private string CurrentParameterIdentity(string fieldId)
    {
        using var stream = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
        {
            var value = ReadCurrentParameter(fieldId);
            ParameterSettingCanonical.Write(writer, value is null
                ? new Dictionary<string, ParameterSetting>()
                : new Dictionary<string, ParameterSetting> { [fieldId] = value });
        }
        return Convert.ToBase64String(stream.ToArray());
    }
    private static bool ContainsResource(TypeRef type) => type.Shape == ValueShape.Resource ||
        type.Element is not null && ContainsResource(type.Element) ||
        type.Fields.Any(field => ContainsResource(field.Type));
}






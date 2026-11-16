using System.Collections.ObjectModel;
using System.Buffers;
using System.Text.Json;

namespace SunEyeVision.Plugin.Contracts;

/// <summary>One immutable, declaration-owned configuration. Missing keys are Unset;
/// resources and resolved invocation values never belong to this snapshot.</summary>
public sealed class FieldSettings
{
    private readonly IReadOnlyDictionary<string, Port> _ports;
    private readonly IReadOnlyDictionary<string, ParameterSetting> _values;
    public FieldSettings(IEnumerable<Port> ports, IReadOnlyDictionary<string, ParameterSetting> values)
    {
        ArgumentNullException.ThrowIfNull(ports); ArgumentNullException.ThrowIfNull(values);
        _ports = new ReadOnlyDictionary<string, Port>(ports.ToDictionary(port => port.Id, StringComparer.Ordinal));
        var frozen = new Dictionary<string, ParameterSetting>(StringComparer.Ordinal);
        foreach (var (id, setting) in values)
        {
            if (!_ports.TryGetValue(id, out var port)) throw new ArgumentException("Unknown configuration field: " + id);
            if (setting is UnsetSetting) throw new ArgumentException("Persistent Unset fields must be absent: " + id);
            frozen.Add(id, Freeze(port, setting));
        }
        _values = new ReadOnlyDictionary<string, ParameterSetting>(frozen);
    }
    private FieldSettings(IReadOnlyDictionary<string,Port> ports,Dictionary<string,ParameterSetting> frozen)
    {
        _ports=ports;
        _values=new ReadOnlyDictionary<string,ParameterSetting>(frozen);
    }
    public IReadOnlyDictionary<string, Port> Declarations => _ports;
    public IReadOnlyDictionary<string, ParameterSetting> Capture() => new ReadOnlyDictionary<string, ParameterSetting>(
        _values.ToDictionary(pair => pair.Key, pair => Freeze(_ports[pair.Key], pair.Value), StringComparer.Ordinal));
    public ParameterSetting Read(string id)
    {
        if (!_ports.ContainsKey(id)) throw new ArgumentException("Unknown configuration field: " + id);
        return _values.TryGetValue(id, out var value) ? Freeze(_ports[id], value) : new UnsetSetting();
    }
    public T Get<T>(string id) => Read(id) is ConstantSetting constant && (constant.Value is T || constant.Value is null && default(T) is null)
        ? (T)constant.Value! : throw new InvalidOperationException("Field is not a present constant of the requested type: " + id);
    /// <summary>Compare a field by its persisted value, without copying shared frozen entries.</summary>
    public bool HasSameSetting(FieldSettings other,string id)
    {
        ArgumentNullException.ThrowIfNull(other);
        if(!_ports.ContainsKey(id)||!other._ports.ContainsKey(id))return false;
        _values.TryGetValue(id,out var left);
        other._values.TryGetValue(id,out var right);
        if(ReferenceEquals(left,right))return true;
        if(left is null||right is null)return false;
        ArrayBufferWriter<byte> Encode(ParameterSetting setting)
        {
            var bytes=new ArrayBufferWriter<byte>();
            using(var writer=new Utf8JsonWriter(bytes))
                ParameterSettingCanonical.Write(writer,new Dictionary<string,ParameterSetting>{{id,setting}});
            return bytes;
        }
        return Encode(left).WrittenSpan.SequenceEqual(Encode(right).WrittenSpan);
    }
    public FieldSettings Apply(IReadOnlyDictionary<string, ParameterSetting> proposals)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        if(proposals.Count==0)return this;
        var values = new Dictionary<string,ParameterSetting>(_values,StringComparer.Ordinal);
        foreach (var (id, setting) in proposals)
        {
            if (!_ports.TryGetValue(id, out var port)) throw new ArgumentException("Unknown configuration field: " + id);
            if (setting is UnsetSetting)
            {
                if (port.Required) throw new ArgumentException("A required field cannot be Unset: " + id);
                values.Remove(id);
            }
            else values[id] = Freeze(port, setting);
        }
        return new(_ports, values);
    }
    public FieldSettings Select(IEnumerable<Port> ports)
    {
        var selected = ports.ToArray(); var ids = selected.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        return new(selected, _values.Where(pair => ids.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }
    public InputValues Materialize(IReadOnlyDictionary<string, object?> resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (id, setting) in _values)
        {
        if (setting is ConstantSetting constant) values.Add(id, constant.Value);
            else if (resolved.TryGetValue(id, out var value)) values.Add(id, value);
            else throw new ArgumentException("A configured reference has not been resolved: " + id);
        }
        if (resolved.Keys.Any(id => !_values.TryGetValue(id, out var setting) || setting is not (ReferenceSetting or VariableSetting)))
            throw new ArgumentException("Resolved values must name configured references only.");
        return new(_ports.Values, values);
    }
    public FieldSettings MapReferences(Func<OutputReference, OutputReference> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return new(_ports.Values, _values.ToDictionary(pair => pair.Key,
            pair => pair.Value switch {
                DisplayExpressionSetting expression=>expression.MapReferences(map),
                ReferenceSetting reference=>reference with {Reference=map(reference.Reference)},
                VariableSetting variable when variable.Reference.Selection is BindingSelection.AtField index=>variable with {
                    Reference=new VariableReference(variable.Reference.SolutionId,variable.Reference.VariableId,variable.Reference.Type,new BindingSelection.AtField(map(index.Reference),index.ConstructionBehavior),variable.Reference.Storage)},
                _=>pair.Value},
            StringComparer.Ordinal));
    }
    public static ParameterSetting Freeze(Port port, ParameterSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        if(setting is DisplayExpressionSetting expression)
        {
            if(port.Type!=DataType.Text||(port.EditModes&FieldEditModes.DisplayExpression)==0)
                throw new ArgumentException("This field does not allow display expressions: "+port.Id);
            return expression.Copy();
        }
        if (setting is ConstantSetting constant)
        {
            if (port.EditModes != FieldEditModes.None && (port.EditModes & FieldEditModes.Constant) == 0)
                throw new ArgumentException("This field does not allow constants: " + port.Id);
            if (ContainsResource(port.Type)) throw new ArgumentException("Configuration cannot own invocation resources: " + port.Id);
            return new ConstantSetting(ValueRules.Freeze(port, constant.Value));
        }
        if (setting is ReferenceSetting reference)
        {
            if (port.EditModes != FieldEditModes.None && (port.EditModes & FieldEditModes.Reference) == 0)
                throw new ArgumentException("This field does not allow references: " + port.Id);
            if (!Enum.IsDefined(reference.Conversion)) throw new ArgumentException("Unknown binding conversion.");
            ArgumentNullException.ThrowIfNull(reference.Reference);
            return reference;
        }
        if(setting is VariableSetting variable)
        {
            if(port.EditModes!=FieldEditModes.None&&(port.EditModes&FieldEditModes.Reference)==0)
                throw new ArgumentException("This field does not allow references: "+port.Id);
            ArgumentNullException.ThrowIfNull(variable.Reference);
            if(!Enum.IsDefined(variable.Conversion))throw new ArgumentException("Unknown binding conversion.");
            return variable;
        }
        throw new ArgumentException("Configuration requires Constant or Reference; Unset is an edit proposal.");
    }
    private static bool ContainsResource(TypeRef type) => type.Shape == ValueShape.Resource
        || type.Element is not null && ContainsResource(type.Element) || type.Fields.Any(field => ContainsResource(field.Type));
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Workflow;

public sealed partial class RuntimeToolConfiguration
{
    private readonly IReadOnlyDictionary<string,string> _bindingNames;
    private readonly IReadOnlyList<string> _bindingOrder;
    public IReadOnlyList<string> ParameterBindingOrder=>_bindingOrder;
    private readonly IReadOnlySet<string> _executionInputs;
    private IReadOnlySet<string> FreezeExecutionInputs()
    {
        var inputs=Schema.Inputs.ToDictionary(port=>port.Id,StringComparer.Ordinal);
        var selected=(_definition as IToolExecutionInputs)?.SelectExecutionInputs(Fields)?.ToArray()
            ??(_definition is IToolExecutionInputs?throw new ArgumentException("Execution input selection cannot be null."):inputs.Keys.ToArray());
        var ids=selected.ToHashSet(StringComparer.Ordinal);
        if(ids.Count!=selected.Length||selected.Any(id=>id is null||!inputs.ContainsKey(id))
            ||inputs.Values.Any(port=>port.Required&&!ids.Contains(port.Id)))
            throw new ArgumentException("Execution input selection must contain unique declared inputs and every required input.");
        return ids;
    }
    public bool IsExecutionFieldActive(string id)=>!IsDisplayField(id)&&BindingTargets.Any(port=>port.Id==id)
        &&(!Schema.Inputs.Any(port=>port.Id==id)||_executionInputs.Contains(id));
    private IReadOnlyDictionary<string,string> FreezeBindingNames()
    {
        var names=(_definition as IToolParameterBindingNames)?.ParameterBindingNames??new Dictionary<string,string>();
        var declared=SettingsSchema.Inputs.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
        var copy=new Dictionary<string,string>(StringComparer.Ordinal);var unique=new HashSet<string>(StringComparer.Ordinal);
        foreach(var pair in names)
        {
            if(!declared.Contains(pair.Key)||string.IsNullOrWhiteSpace(pair.Value)||pair.Value.Contains('\0')||!unique.Add(pair.Value))
                throw new ArgumentException("Parameter binding names require unique declared parameter keys.");
            _=new UTF8Encoding(false,true).GetByteCount(pair.Value);
            copy.Add(pair.Key,pair.Value);
        }
        return new ReadOnlyDictionary<string,string>(copy);
    }
    private IReadOnlyList<string> FreezeBindingOrder(IReadOnlyList<string>? order)
    {
        var parameters=SettingsSchema.Inputs.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
        var references=Fields.Capture().Where(pair=>parameters.Contains(pair.Key)&&(pair.Value is ReferenceSetting or VariableSetting))
            .Select(pair=>pair.Key).ToArray();
        if(references.Any(id=>!_bindingNames.ContainsKey(id)))throw new ArgumentException("Referenced settings require explicit parameter binding names.");
        var selected=order?.ToArray()??references;
        if(selected.Length!=references.Length||selected.Distinct(StringComparer.Ordinal).Count()!=selected.Length
            ||!selected.ToHashSet(StringComparer.Ordinal).SetEquals(references))
            throw new ArgumentException("Parameter binding order must contain every referenced setting exactly once.");
        return Array.AsReadOnly(selected);
    }
    private static string SettingIdentity(string id,ParameterSetting setting)
    {
        using var buffer=new System.IO.MemoryStream();
        using(var writer=new Utf8JsonWriter(buffer))RuntimeFieldJson.Write(writer,new Dictionary<string,ParameterSetting>{{id,setting}});
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
    private IReadOnlyList<string> ReconfigureBindingOrder(FieldSettings fields,IEnumerable<string>? proposals)
    {
        var parameters=SettingsSchema.Inputs.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
        bool Referenced(string id)=>parameters.Contains(id)&&(fields.Read(id) is ReferenceSetting or VariableSetting);
        var retained=_bindingOrder.Where(id=>Referenced(id)&&SettingIdentity(id,Fields.Read(id))==SettingIdentity(id,fields.Read(id))).ToList();
        foreach(var id in (proposals??Array.Empty<string>()).Concat(fields.Capture().Keys).Distinct(StringComparer.Ordinal))
            if(Referenced(id)&&!retained.Contains(id,StringComparer.Ordinal))retained.Add(id);
        return retained;
    }
    /// <summary>Parameter writes precede tool input processing. Resource readers are retained
    /// by the caller before this loop; dictionary/layout order is never parameter execution order.</summary>
    internal IReadOnlyList<KeyValuePair<string,ParameterSetting>> CaptureExecutionFields()
    {
        var fields=Fields.Capture().Where(pair=>IsExecutionFieldActive(pair.Key)).ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        var references=_bindingOrder.ToHashSet(StringComparer.Ordinal);
        return Array.AsReadOnly(_bindingOrder.Where(fields.ContainsKey).Select(id=>new KeyValuePair<string,ParameterSetting>(id,fields[id]))
            .Concat(fields.Where(pair=>!references.Contains(pair.Key))).ToArray());
    }
    internal void WriteBindingOrder(Utf8JsonWriter writer,bool export)
    {
        writer.WriteStartArray();
        var order=export?_bindingOrder.OrderBy(id=>_bindingNames[id],Utf8Order):_bindingOrder.AsEnumerable();
        foreach(var id in order)writer.WriteStringValue(id);
        writer.WriteEndArray();
    }
    private IReadOnlyList<string> RestoreBindingPosition(string fieldId,RuntimeToolConfiguration opening)
    {
        if(!_bindingOrder.Contains(fieldId,StringComparer.Ordinal))return _bindingOrder;
        var order=_bindingOrder.Where(id=>id!=fieldId).ToList();
        var next=opening._bindingOrder.SkipWhile(id=>id!=fieldId).Skip(1).FirstOrDefault(id=>order.Contains(id,StringComparer.Ordinal));
        if(next is null)order.Add(fieldId);else order.Insert(order.IndexOf(next),fieldId);
        return order;
    }
    private static readonly IComparer<string> Utf8Order=Comparer<string>.Create((left,right)=>
        Encoding.UTF8.GetBytes(left).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(right)));
}

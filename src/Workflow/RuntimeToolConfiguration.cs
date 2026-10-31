using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

namespace SunEyeVision.Workflow;

/// <summary>One persistent map of stable field settings, including inputs and references.
/// Typed settings are complete execution/edit-boundary projections, never a second authority.</summary>
[JsonConverter(typeof(RuntimeToolConfigurationJsonConverter))]
public sealed partial class RuntimeToolConfiguration
{
    private readonly IToolDefinition _definition;
    private readonly IToolSettingsFields _settingsFields;
    private readonly IReadOnlyDictionary<string,ConstantSetting> _currentValues;
    private readonly IReadOnlyDictionary<string,long> _currentRevisions;
    public FieldSettings Fields { get; }

    private RuntimeToolConfiguration(IToolDefinition definition, IReadOnlyDictionary<string,ParameterSetting> settings,
        IReadOnlyDictionary<string,ParameterSetting>? currentValues=null,IReadOnlyDictionary<string,long>? currentRevisions=null,
        IReadOnlyList<string>? bindingOrder=null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.ToolId)) throw new ArgumentException("Runtime ToolId is required.");
        _settingsFields=definition as IToolSettingsFields??throw new ArgumentException("Runtime requires declared settings and a materializer.");
        if(definition.Describe().Inputs.Select(p=>p.Id)
            .Intersect(_settingsFields.SettingsSchema.Inputs.Select(p=>p.Id),StringComparer.Ordinal).Any())
            throw new ArgumentException("Runtime input and settings field identities must be distinct.");
        Fields=new(BindingTargets,settings);
        _bindingNames=FreezeBindingNames();_bindingOrder=FreezeBindingOrder(bindingOrder);
        var parameters=SettingsSchema.Inputs.ToDictionary(port=>port.Id,StringComparer.Ordinal);
        var current=new Dictionary<string,ConstantSetting>(StringComparer.Ordinal);
        foreach(var (id,value) in currentValues??new Dictionary<string,ParameterSetting>())
        {
            if(!parameters.TryGetValue(id,out var port)||Fields.Read(id) is not (ReferenceSetting or VariableSetting)
                ||value is not ConstantSetting constant)
                throw new ArgumentException("Current parameter values require a declared referenced setting: "+id);
            current.Add(id,FreezeCurrent(port,constant.Value));
        }
        _currentValues=new System.Collections.ObjectModel.ReadOnlyDictionary<string,ConstantSetting>(current);
        if(currentRevisions?.Any(pair=>!current.ContainsKey(pair.Key)||pair.Value<0)==true)
            throw new ArgumentException("Current value revisions require present parameter values.",nameof(currentRevisions));
        _currentRevisions=new System.Collections.ObjectModel.ReadOnlyDictionary<string,long>(current.Keys.ToDictionary(
            id=>id,id=>currentRevisions?.GetValueOrDefault(id)??0,StringComparer.Ordinal));
        foreach(var port in SettingsSchema.Inputs.Where(port=>port.Required))
            if(Fields.Read(port.Id) is UnsetSetting)throw new ArgumentException("Missing required Runtime setting: "+port.Id);
        if(!Fields.Select(SettingsSchema.Inputs).Capture().Values.Any(value=>value is ReferenceSetting or VariableSetting))
        {var errors=definition.Validate(CaptureSettings());if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors.Select(error=>error.Message)));}
    }

    internal IReadOnlyList<ManualReferenceSource> CaptureManualReferenceSources()
        => (_definition as IToolManualReferenceSources)?.ManualReferenceSources
            .Select(source => new ManualReferenceSource(source.PortId, source.MemberPath)).ToArray()
            ?? Array.Empty<ManualReferenceSource>();
    public string ToolId => _definition.ToolId;
    public Type SettingsType => _definition.SettingsType;
    /// <summary>Editing and dependency identity deliberately excludes live parameter values.</summary>
    internal string ConfigurationIdentity
    {
        get
        {
            using var buffer=new System.IO.MemoryStream();
            using(var writer=new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();writer.WriteString("ToolId",ToolId);
                writer.WriteString("InputSchema",Schema.Fingerprint);writer.WriteString("SettingsSchema",SettingsSchema.Fingerprint);
                writer.WritePropertyName("Fields");RuntimeFieldJson.Write(writer,Fields.Capture());
                writer.WritePropertyName("ParameterBindingOrder");WriteBindingOrder(writer,export:false);
                writer.WritePropertyName("ParameterBindingNames");writer.WriteStartObject();
                foreach(var pair in _bindingNames.OrderBy(pair=>pair.Key,StringComparer.Ordinal))writer.WriteString(pair.Key,pair.Value);
                writer.WriteEndObject();writer.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
    internal string CurrentValuesIdentity
    {
        get
        {
            using var buffer=new System.IO.MemoryStream();
            using(var writer=new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();writer.WritePropertyName("Values");RuntimeFieldJson.Write(writer,CaptureCurrentValues());
                writer.WritePropertyName("Revisions");writer.WriteStartObject();
                foreach(var pair in _currentRevisions.OrderBy(pair=>pair.Key,StringComparer.Ordinal))writer.WriteNumber(pair.Key,pair.Value);
                writer.WriteEndObject();writer.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
    public object CaptureSettings() => _definition.FreezeSettings(_settingsFields.MaterializeSettings(ReadFieldValues()));
    public object MaterializeSettings(IReadOnlyDictionary<string,object?> resolved)
        =>_definition.FreezeSettings(_settingsFields.MaterializeSettings(Fields.Select(SettingsSchema.Inputs).Materialize(resolved)));
    public RuntimeToolConfiguration WithSettings(object settings) => WithFieldValues(_settingsFields.ReadSettings(_definition.FreezeSettings(settings)).CopyValues());
    public RuntimeToolConfiguration Copy() => new(_definition, Fields.Capture(),CaptureCurrentValues(),_currentRevisions,_bindingOrder);
    /// <summary>Pure parameter state, separate from binding configuration and invocation resources.
    /// Constants have only one authority, Fields; absent referenced values remain absent.</summary>
    public ConstantSetting? ReadCurrentParameter(string fieldId)
    {
        var port=SettingsSchema.Inputs.SingleOrDefault(port=>port.Id==fieldId)
            ??throw new ArgumentException("Unknown parameter field: "+fieldId,nameof(fieldId));
        if(Fields.Read(fieldId) is ConstantSetting constant)return FreezeCurrent(port,constant.Value);
        return _currentValues.TryGetValue(fieldId,out var current)?FreezeCurrent(port,current.Value):null;
    }
    internal IReadOnlyDictionary<string,ParameterSetting> CaptureCurrentValues()
        =>new System.Collections.ObjectModel.ReadOnlyDictionary<string,ParameterSetting>(_currentValues.ToDictionary(
            pair=>pair.Key,pair=>(ParameterSetting)FreezeCurrent(SettingsSchema.Inputs.Single(port=>port.Id==pair.Key),pair.Value.Value),StringComparer.Ordinal));
    /// <summary>One already accepted parameter write. The caller owns execution admission;
    /// this immutable operation neither resolves a source nor declares algorithm success.</summary>
    internal RuntimeToolConfiguration WithAcceptedParameterValue(string fieldId,object? value)
    {
        var port=SettingsSchema.Inputs.SingleOrDefault(port=>port.Id==fieldId)
            ??throw new ArgumentException("Unknown parameter field: "+fieldId,nameof(fieldId));
        if(Fields.Read(fieldId) is not (ReferenceSetting or VariableSetting))
            throw new ArgumentException("Only a referenced parameter can accept a resolved current value: "+fieldId);
        var current=CaptureCurrentValues().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        current[fieldId]=FreezeCurrent(port,value);
        var revisions=_currentRevisions.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        revisions[fieldId]=checked(CurrentRevision(fieldId)+1);
        return new(_definition,Fields.Capture(),current,revisions,_bindingOrder);
    }
    /// <summary>Configuration replay must not replay execution. Keep the live value for
    /// retained referenced parameter identities; explicit constant edits remain authoritative.</summary>
    internal RuntimeToolConfiguration PreserveCurrentValuesFrom(RuntimeToolConfiguration live)
    {
        ArgumentNullException.ThrowIfNull(live);
        if(ToolId!=live.ToolId||Schema.Fingerprint!=live.Schema.Fingerprint||SettingsSchema.Fingerprint!=live.SettingsSchema.Fingerprint)
            throw new ArgumentException("Current parameter values belong to another declaration.",nameof(live));
        var values=CaptureCurrentValues().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        foreach(var port in SettingsSchema.Inputs)
            if(Fields.Read(port.Id) is ReferenceSetting or VariableSetting)
            {
                values.Remove(port.Id);
                if(live.ReadCurrentParameter(port.Id) is {} current)values.Add(port.Id,current);
            }
        return new(_definition,Fields.Capture(),values,values.Keys.ToDictionary(id=>id,id=>live.CurrentRevision(id),StringComparer.Ordinal),_bindingOrder);
    }
    private long CurrentRevision(string fieldId)=>_currentRevisions.GetValueOrDefault(fieldId);
    internal bool CurrentValueChangedFrom(string fieldId,RuntimeToolConfiguration opening)
        =>CurrentRevision(fieldId)!=opening.CurrentRevision(fieldId)||CurrentParameterIdentity(fieldId)!=opening.CurrentParameterIdentity(fieldId);
    internal string CurrentParameterIdentity(string fieldId)
    {
        using var buffer=new System.IO.MemoryStream();
        using(var writer=new Utf8JsonWriter(buffer))
        {
            var value=ReadCurrentParameter(fieldId);
            RuntimeFieldJson.Write(writer,value is null?new Dictionary<string,ParameterSetting>()
                :new Dictionary<string,ParameterSetting>{{fieldId,value}});
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
    internal bool HasCurrentValueChangesFrom(RuntimeToolConfiguration? opening)
        =>opening is not null&&SettingsSchema.Inputs.Any(port=>(Fields.Read(port.Id) is ReferenceSetting or VariableSetting)
            &&CurrentValueChangedFrom(port.Id,opening));
    internal RuntimeToolConfiguration MergeCurrentValues(RuntimeToolConfiguration live,RuntimeToolConfiguration? opening)
    {
        if(opening is null)return PreserveCurrentValuesFrom(live);
        var values=new Dictionary<string,ParameterSetting>(StringComparer.Ordinal);
        var revisions=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var port in SettingsSchema.Inputs)
            if(Fields.Read(port.Id) is ReferenceSetting or VariableSetting)
            {
                var proposed=CurrentParameterIdentity(port.Id);
                var formal=live.CurrentParameterIdentity(port.Id);
                var changed=CurrentValueChangedFrom(port.Id,opening);
                if(changed&&live.CurrentValueChangedFrom(port.Id,opening)&&proposed!=formal)
                    throw new InvalidOperationException("参数当前值已被正式运行和私有调试分别改变，请重新确认："+port.Id);
                var value=changed?ReadCurrentParameter(port.Id):live.ReadCurrentParameter(port.Id);
                if(value is not null)
                {
                    values.Add(port.Id,value);
                    revisions.Add(port.Id,changed?checked(live.CurrentRevision(port.Id)+1):live.CurrentRevision(port.Id));
                }
            }
        return new(_definition,Fields.Capture(),values,revisions,_bindingOrder);
    }
    private static ConstantSetting FreezeCurrent(Port port,object? value)
    {
        static bool ContainsResource(TypeRef type)=>type.Shape==ValueShape.Resource
            ||type.Element is not null&&ContainsResource(type.Element)||type.Fields.Any(field=>ContainsResource(field.Type));
        if(ContainsResource(port.Type))throw new ArgumentException("Parameter current values cannot own invocation resources: "+port.Id);
        return new(ValueRules.Freeze(port,value));
    }
    public IReadOnlyList<Diagnostic> Validate()
    {
        var settings=Fields.Select(SettingsSchema.Inputs);
        var known=(_definition as IToolKnownSettingsValidation)?.ValidateKnownSettings(settings)??Array.Empty<Diagnostic>();
        if(known.Count!=0)return known;
        return settings.Capture().Values.Any(value=>value is ReferenceSetting or VariableSetting)
            ?Array.Empty<Diagnostic>():_definition.Validate(CaptureSettings());
    }
    public ToolSchema Schema => _definition.Describe();
    public ToolCapabilities Capabilities => _definition.Capabilities();
    public ToolSchema SettingsSchema => _settingsFields.SettingsSchema;
    public InputValues ReadFieldValues()
    {
        return Fields.Select(SettingsSchema.Inputs).Materialize(new Dictionary<string,object?>());
    }
    public IReadOnlyList<Port> BindingTargets => Schema.Inputs.Concat(SettingsSchema?.Inputs??Array.Empty<Port>()).ToArray();
    public RuntimeToolConfiguration WithFieldValues(IReadOnlyDictionary<string,object?> changes)
    {
        return WithFieldSettings(changes.ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value),StringComparer.Ordinal));
    }
    public RuntimeToolConfiguration WithFieldSettings(IReadOnlyDictionary<string,ParameterSetting> changes)=>Reconfigure(Fields.Apply(changes),changes.Keys);
    private RuntimeToolConfiguration Reconfigure(FieldSettings fields,IEnumerable<string>? proposals=null)
    {
        var current=new Dictionary<string,ParameterSetting>(StringComparer.Ordinal);
        foreach(var port in SettingsSchema.Inputs)
            if(fields.Read(port.Id) is ReferenceSetting or VariableSetting)
            {
                var value=ReadCurrentParameter(port.Id);
                if(value is not null)current.Add(port.Id,value);
            }
        return new(_definition,fields.Capture(),current,current.Keys.ToDictionary(id=>id,CurrentRevision,StringComparer.Ordinal),ReconfigureBindingOrder(fields,proposals));
    }
    /// <summary>Restore one exact field from an admitted opening snapshot, preserving all other edits.
    /// Missing invocation inputs can be restored to their original absence; user proposals still use Apply.</summary>
    public RuntimeToolConfiguration RestoreFieldFrom(string fieldId,RuntimeToolConfiguration opening)
    {
        ArgumentNullException.ThrowIfNull(opening);
        if(ToolId!=opening.ToolId||Schema.Fingerprint!=opening.Schema.Fingerprint||SettingsSchema.Fingerprint!=opening.SettingsSchema.Fingerprint)
            throw new ArgumentException("Opening configuration belongs to a different tool declaration.");
        _=Fields.Read(fieldId);var original=opening.Fields.Read(fieldId);
        var values=Fields.Capture().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        if(original is UnsetSetting)values.Remove(fieldId);else values[fieldId]=original;
        var restored=Reconfigure(new FieldSettings(BindingTargets,values));
        if(original is not (ReferenceSetting or VariableSetting)||!SettingsSchema.Inputs.Any(port=>port.Id==fieldId))return restored;
        var current=restored.CaptureCurrentValues().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        current.Remove(fieldId);
        if(opening.ReadCurrentParameter(fieldId) is {} openingValue)current.Add(fieldId,openingValue);
        var revisions=current.Keys.ToDictionary(id=>id,id=>id==fieldId?opening.CurrentRevision(id):restored.CurrentRevision(id),StringComparer.Ordinal);
        return new(_definition,values,current,revisions,restored.RestoreBindingPosition(fieldId,opening));
    }
    public RuntimeToolConfiguration WithSettingsFields(FieldSettings settings,IReadOnlyList<string>? proposalOrder=null)
    {
        var ids=SettingsSchema.Inputs.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
        if(settings.Declarations.Keys.Any(id=>!ids.Contains(id)))throw new ArgumentException("Settings snapshot contains invocation inputs.");
        if(proposalOrder is not null&&(proposalOrder.Any(id=>!ids.Contains(id))||proposalOrder.Distinct(StringComparer.Ordinal).Count()!=proposalOrder.Count))
            throw new ArgumentException("Settings proposal order contains unknown or duplicate fields.");
        var values=Fields.Capture().Where(pair=>!ids.Contains(pair.Key)).ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        foreach(var pair in settings.Capture())values.Add(pair.Key,pair.Value);
        return Reconfigure(new FieldSettings(BindingTargets,values),proposalOrder);
    }
    public RuntimeToolConfiguration MapReferences(Func<OutputReference,OutputReference> map,Func<string,string?>? mapOrigin=null)
    {
        var fields=Fields.MapReferences(map).Capture();
        return new(_definition,fields.ToDictionary(pair=>pair.Key,pair=>pair.Value is ReferenceSetting reference&&reference.OriginId is {} origin&&mapOrigin is not null
            ?(ParameterSetting)(reference with{OriginId=mapOrigin(origin)}):pair.Value,StringComparer.Ordinal),CaptureCurrentValues(),_currentRevisions,_bindingOrder);
    }

    public static RuntimeToolConfiguration Create(string toolId)
    {
        var definition = ToolRegistry.CreateRuntime(toolId);
        var fields=definition as IToolSettingsFields??throw new ArgumentException("Runtime requires a settings declaration.");
        return new(definition,fields.ReadSettings(definition.CreateSettings()).CopyValues()
            .ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value),StringComparer.Ordinal));
    }

    internal static RuntimeToolConfiguration Restore(string toolId, IReadOnlyDictionary<string,ParameterSetting> settings,
        IReadOnlyDictionary<string,ParameterSetting> currentValues,IReadOnlyList<string> bindingOrder)
    {
        var definition = ToolRegistry.CreateRuntime(toolId);
        return new(definition, settings,currentValues,bindingOrder:bindingOrder);
    }
}

public sealed class RuntimeToolConfigurationJsonConverter : JsonConverter<RuntimeToolConfiguration>
{
    public override RuntimeToolConfiguration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.GetRawText().Length > 1024 * 1024) throw new JsonException("Runtime settings payload is too large.");
        CheckUniqueMembers(root);
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4
            || !root.TryGetProperty("ToolId", out var id) || id.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(id.GetString()) || !root.TryGetProperty("Fields", out var settings)
            || settings.ValueKind != JsonValueKind.Object || !root.TryGetProperty("CurrentValues",out var currentValues)
            || currentValues.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("ParameterBindingOrder",out var bindingOrder)
            ||bindingOrder.ValueKind!=JsonValueKind.Array)
            throw new JsonException("Runtime configuration requires exactly ToolId, Fields, CurrentValues and ParameterBindingOrder.");
        try { return RuntimeToolConfiguration.Restore(id.GetString()!, RuntimeFieldJson.Read(id.GetString()!,settings),
            RuntimeFieldJson.Read(id.GetString()!,currentValues,currentValues:true),bindingOrder.EnumerateArray().Select(item=>
                item.ValueKind==JsonValueKind.String?item.GetString()!:throw new JsonException("Parameter binding order requires field IDs.")).ToArray()); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        { throw new JsonException("Runtime configuration rejected: " + error.Message, error); }
    }

    private static void CheckUniqueMembers(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate Runtime setting: " + property.Name);
                CheckUniqueMembers(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckUniqueMembers(item);
    }

    public override void Write(Utf8JsonWriter writer, RuntimeToolConfiguration value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("ToolId", value.ToolId);
        writer.WritePropertyName("Fields");
        RuntimeFieldJson.Write(writer,value.Fields.Capture());
        writer.WritePropertyName("CurrentValues");
        RuntimeFieldJson.Write(writer,value.CaptureCurrentValues());
        writer.WritePropertyName("ParameterBindingOrder");value.WriteBindingOrder(writer,export:true);
        writer.WriteEndObject();
    }
}

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
public sealed class RuntimeToolConfiguration
{
    private readonly IToolDefinition _definition;
    private readonly IToolSettingsFields _settingsFields;
    public FieldSettings Fields { get; }

    private RuntimeToolConfiguration(IToolDefinition definition, IReadOnlyDictionary<string,ParameterSetting> settings)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.ToolId)) throw new ArgumentException("Runtime ToolId is required.");
        _settingsFields=definition as IToolSettingsFields??throw new ArgumentException("Runtime requires declared settings and a materializer.");
        if(definition.Describe().Inputs.Select(p=>p.Id)
            .Intersect(_settingsFields.SettingsSchema.Inputs.Select(p=>p.Id),StringComparer.Ordinal).Any())
            throw new ArgumentException("Runtime input and settings field identities must be distinct.");
        Fields=new(BindingTargets,settings);
        foreach(var port in SettingsSchema.Inputs.Where(port=>port.Required))
            if(Fields.Read(port.Id) is UnsetSetting)throw new ArgumentException("Missing required Runtime setting: "+port.Id);
        if(!Fields.Select(SettingsSchema.Inputs).Capture().Values.Any(value=>value is ReferenceSetting))
        {var errors=definition.Validate(CaptureSettings());if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors.Select(error=>error.Message)));}
    }

    public string ToolId => _definition.ToolId;
    public Type SettingsType => _definition.SettingsType;
    public object CaptureSettings() => _definition.FreezeSettings(_settingsFields.MaterializeSettings(ReadFieldValues()));
    public object MaterializeSettings(IReadOnlyDictionary<string,object?> resolved)
        =>_definition.FreezeSettings(_settingsFields.MaterializeSettings(Fields.Select(SettingsSchema.Inputs).Materialize(resolved)));
    public RuntimeToolConfiguration WithSettings(object settings) => WithFieldValues(_settingsFields.ReadSettings(_definition.FreezeSettings(settings)).CopyValues());
    public RuntimeToolConfiguration Copy() => new(_definition, Fields.Capture());
    public IReadOnlyList<Diagnostic> Validate() => Fields.Select(SettingsSchema.Inputs).Capture().Values.Any(value=>value is ReferenceSetting)
        ?Array.Empty<Diagnostic>():_definition.Validate(CaptureSettings());
    public ToolSchema Schema => _definition.Describe();
    /// <summary>Existing tool-owned display declaration; never used as a binding or persisted identity.</summary>
    public SunEyeVision.Plugin.Contracts.Results.Presentation.ResultProjectionDescriptor? ResultProjection
        => (_definition as SunEyeVision.Plugin.Contracts.Results.Presentation.IToolResultPresentationProvider)?.ResultProjection;
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
    public RuntimeToolConfiguration WithFieldSettings(IReadOnlyDictionary<string,ParameterSetting> changes)=>new(_definition,Fields.Apply(changes).Capture());
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
        return new(_definition,values);
    }
    public RuntimeToolConfiguration WithSettingsFields(FieldSettings settings)
    {
        var ids=SettingsSchema.Inputs.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
        if(settings.Declarations.Keys.Any(id=>!ids.Contains(id)))throw new ArgumentException("Settings snapshot contains invocation inputs.");
        var values=Fields.Capture().Where(pair=>!ids.Contains(pair.Key)).ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        foreach(var pair in settings.Capture())values.Add(pair.Key,pair.Value);
        return new(_definition,values);
    }
    public RuntimeToolConfiguration MapReferences(Func<OutputReference,OutputReference> map,Func<string,string?>? mapOrigin=null)
    {
        var fields=Fields.MapReferences(map).Capture();
        return new(_definition,fields.ToDictionary(pair=>pair.Key,pair=>pair.Value is ReferenceSetting reference&&reference.OriginId is {} origin&&mapOrigin is not null
            ?(ParameterSetting)(reference with{OriginId=mapOrigin(origin)}):pair.Value,StringComparer.Ordinal));
    }

    public static RuntimeToolConfiguration Create(string toolId)
    {
        var definition = ToolRegistry.CreateRuntime(toolId);
        var fields=definition as IToolSettingsFields??throw new ArgumentException("Runtime requires a settings declaration.");
        return new(definition,fields.ReadSettings(definition.CreateSettings()).CopyValues()
            .ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value),StringComparer.Ordinal));
    }

    internal static RuntimeToolConfiguration Restore(string toolId, IReadOnlyDictionary<string,ParameterSetting> settings)
    {
        var definition = ToolRegistry.CreateRuntime(toolId);
        return new(definition, settings);
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
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2
            || !root.TryGetProperty("ToolId", out var id) || id.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(id.GetString()) || !root.TryGetProperty("Fields", out var settings)
            || settings.ValueKind != JsonValueKind.Object)
            throw new JsonException("Runtime configuration requires exactly ToolId and Fields.");
        try { return RuntimeToolConfiguration.Restore(id.GetString()!, RuntimeFieldJson.Read(id.GetString()!,settings)); }
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
        writer.WriteEndObject();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

namespace SunEyeVision.Workflow;

/// <summary>Host-owned persistent settings for a neutral Runtime. The payload is the
/// actual Runtime settings type; bindings and runtime input resources live elsewhere.</summary>
[JsonConverter(typeof(RuntimeToolConfigurationJsonConverter))]
public sealed class RuntimeToolConfiguration
{
    private readonly IToolDefinition _definition;
    private readonly object _settings;

    private RuntimeToolConfiguration(IToolDefinition definition, object settings)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.ToolId)) throw new ArgumentException("Runtime ToolId is required.");
        _settings = definition.FreezeSettings(settings);
        if (_settings.GetType() != definition.SettingsType) throw new ArgumentException("Runtime settings identity differs.");
        var errors = definition.Validate(_settings);
        if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors.Select(x => x.Message)), nameof(settings));
    }

    public string ToolId => _definition.ToolId;
    public Type SettingsType => _definition.SettingsType;
    public object CaptureSettings() => _definition.FreezeSettings(_settings);
    public RuntimeToolConfiguration WithSettings(object settings) => new(_definition, settings);
    public RuntimeToolConfiguration Copy() => new(_definition, _settings);
    public IReadOnlyList<Diagnostic> Validate() => _definition.Validate(CaptureSettings());
    public ToolSchema Schema => _definition.Describe(CaptureSettings());
    public ToolCapabilities Capabilities => _definition.Capabilities(CaptureSettings());
    public RuntimeToolConfiguration WithOverrides(IReadOnlyDictionary<string,object?> overrides)
    {
        var value=JsonSerializer.SerializeToNode(CaptureSettings(),SettingsType,RuntimeToolConfigurationJsonConverter.SettingsOptions)!.AsObject();
        foreach(var item in overrides)
        {
            if(!value.ContainsKey(item.Key))throw new ArgumentException("Unknown Runtime setting override: "+item.Key);
            value[item.Key]=JsonSerializer.SerializeToNode(item.Value,RuntimeToolConfigurationJsonConverter.SettingsOptions);
        }
        return Restore(ToolId,JsonSerializer.SerializeToElement(value));
    }

    public static RuntimeToolConfiguration Create(string toolId)
    {
        var definition = ToolRegistry.CreateRuntime(toolId);
        return new(definition, definition.CreateSettings());
    }

    internal static RuntimeToolConfiguration Restore(string toolId, JsonElement settings)
    {
        var definition = ToolRegistry.CreateRuntime(toolId);
        var value = settings.Deserialize(definition.SettingsType, RuntimeToolConfigurationJsonConverter.SettingsOptions)
            ?? throw new JsonException("Runtime settings cannot be null.");
        return new(definition, value);
    }
}

public sealed class RuntimeToolConfigurationJsonConverter : JsonConverter<RuntimeToolConfiguration>
{
    internal static JsonSerializerOptions SettingsOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 64
    };

    public override RuntimeToolConfiguration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.GetRawText().Length > 1024 * 1024) throw new JsonException("Runtime settings payload is too large.");
        CheckUniqueMembers(root);
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2
            || !root.TryGetProperty("ToolId", out var id) || id.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(id.GetString()) || !root.TryGetProperty("Settings", out var settings)
            || settings.ValueKind != JsonValueKind.Object)
            throw new JsonException("Runtime configuration requires exactly ToolId and Settings.");
        try { return RuntimeToolConfiguration.Restore(id.GetString()!, settings); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
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
        writer.WritePropertyName("Settings");
        JsonSerializer.Serialize(writer, value.CaptureSettings(), value.SettingsType, SettingsOptions);
        writer.WriteEndObject();
    }
}

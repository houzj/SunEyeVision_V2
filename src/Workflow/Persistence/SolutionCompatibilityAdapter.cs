using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Workflow.Persistence;

/// <summary>Validates current documents and applies documented V1 conversions in memory; never writes the input file.</summary>
internal static class SolutionCompatibilityAdapter
{
    public static void Prepare(JsonNode root, List<PersistenceDiagnostic> diagnostics)
    {
        CheckDuplicateKeys(root.ToJsonString());
        RequireId(root, "$");
        var legacy = SolutionFormatPolicy.ReadVersion(root) == SolutionFormatPolicy.Legacy;
        var knownRootFields = typeof(Solution).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetCustomAttribute<JsonExtensionDataAttribute>() is null
                && property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always)
            .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var property in (JsonObject)root)
            if (!knownRootFields.Contains(property.Key))
                throw new JsonException("Unknown solution field; no approved migration exists.", "$." + property.Key, null, null);
        if (root["GlobalVariables"] is JsonArray variables)
        for (int i = 0; i < variables.Count; i++)
        {
            var variable = variables[i] ?? throw new JsonException("Null variable.");
            var path = "$.GlobalVariables[" + i + "]";
            RequireId(variable, path);
            SolutionVariableValuePolicy.Prepare(variable, path, legacy, CanonicalType,
                (code, diagnosticPath, message) => diagnostics.Add(new(code, "Compatibility", "Warning", diagnosticPath, message)));
        }
        if (root["Devices"] is JsonArray devices)
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i]!;
            var path = "$.Devices[" + i + "]";
            foreach (var field in new[] { "Name", "Manufacturer", "Model", "SerialNumber", "IpAddress", "Description", "Port", "IsEnabled", "Settings", "CreatedTime", "ModifiedTime", "Type", "Status" })
                if (device is JsonObject deviceObject && deviceObject.ContainsKey(field) && device[field] == null)
                    throw new JsonException("Explicit null is not valid for this device field.", path + "." + field, null, null);
            var missing = new List<string>();
            foreach (var field in new[] { "Name", "Type", "Port", "Status", "IsEnabled", "Settings", "CreatedTime", "ModifiedTime" })
                if (device is JsonObject deviceObject && !deviceObject.ContainsKey(field)) missing.Add(field);
            if (missing.Count > 0)
                diagnostics.Add(new("COMPAT_DEVICE_DEFAULTS", "Compatibility", "Warning", path, "Legacy optional defaults applied: " + string.Join(", ", missing)));
            if (device["WorkflowRef"] != null && (device["WorkflowRef"] is not JsonValue workflowRef || !workflowRef.TryGetValue<string>(out _)))
                throw new JsonException("WorkflowRef must be a string or null.", path + ".WorkflowRef", null, null);
            foreach (var field in new[] { "Name", "Manufacturer", "Model", "SerialNumber", "IpAddress", "Description" })
                if (device[field] != null && (device[field] is not JsonValue value || !value.TryGetValue<string>(out _)))
                    throw new JsonException("Expected string.", path + "." + field, null, null);
            if (device["Port"] != null && (device["Port"] is not JsonValue port || !port.TryGetValue<int>(out _)))
                throw new JsonException("Expected integer port.", path + ".Port", null, null);
            if (device["IsEnabled"] != null && (device["IsEnabled"] is not JsonValue enabled || !enabled.TryGetValue<bool>(out _)))
                throw new JsonException("Expected boolean.", path + ".IsEnabled", null, null);
            if (device["Settings"] != null && device["Settings"] is not JsonObject)
                throw new JsonException("Settings must be an object.", path + ".Settings", null, null);
            foreach (var field in new[] { "CreatedTime", "ModifiedTime" })
                if (device[field] != null && !DateTime.TryParse(device[field]!.GetValue<string>(), out _))
                    throw new JsonException("Invalid date.", path + "." + field, null, null);
            foreach (var field in new[] { "Type", "Status" })
            {
                if (device[field] == null) continue;
                var enumType = field == "Type" ? typeof(DeviceType) : typeof(DeviceStatus);
                var element = JsonSerializer.SerializeToElement(device[field]);
                var valid = element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number) && Enum.IsDefined(enumType, number);
                valid |= element.ValueKind == JsonValueKind.String && Enum.TryParse(enumType, element.GetString(), true, out var parsed) && parsed != null && Enum.IsDefined(enumType, parsed);
                if (!valid) throw new JsonException("Invalid device enum.", path + "." + field, null, null);
            }
        }
        foreach (var name in new[] { "Workflows", "GlobalVariables", "Devices", "Communications" })
        {
            if (root is JsonObject obj && !obj.ContainsKey(name)) continue;
            if (root[name] is not JsonArray array) throw new JsonException("Expected array.", "$." + name, null, null);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < array.Count; i++)
            {
                var item = array[i] ?? throw new JsonException("Null collection item.");
                RequireId(item, "$." + name + "[" + i + "]");
                if (!ids.Add(item["Id"]!.GetValue<string>())) throw new JsonException("Duplicate identity.", "$." + name + "[" + i + "].Id", null, null);
            }
        }
        if (root["Workflows"] is JsonArray workflows)
        for (int w = 0; w < workflows.Count; w++)
        {
            var workflow = workflows[w]!;
            var path = "$.Workflows[" + w + "]";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (workflow["Nodes"] is JsonArray nodes)
            for (int n = 0; n < nodes.Count; n++)
            {
                var node = nodes[n] ?? throw new JsonException("Null node.", path + ".Nodes[" + n + "]", null, null);
                RequireId(node, path + ".Nodes[" + n + "]");
                if (!ids.Add(node["Id"]!.GetValue<string>())) throw new JsonException("Duplicate node identity.", path + ".Nodes[" + n + "].Id", null, null);
                SolutionBindingExpressionPolicy.Validate(node["ParamSettings"], path + ".Nodes[" + n + "].ParamSettings");
            }
            else if (workflow["Nodes"] != null) throw new JsonException("Nodes must be an array.", path + ".Nodes", null, null);
            if (workflow["Connections"] is JsonArray edges)
            {
                var edgeIds = new HashSet<string>(StringComparer.Ordinal);
                for (int e = 0; e < edges.Count; e++)
                {
                    var edge = edges[e] ?? throw new JsonException("Null connection.");
                    var edgePath = path + ".Connections[" + e + "]";
                    RequireId(edge, edgePath);
                    if (!edgeIds.Add(edge["Id"]!.GetValue<string>())) throw new JsonException("Duplicate connection.", edgePath + ".Id", null, null);
                    foreach (var endpoint in new[] { "SourceNodeId", "TargetNodeId" })
                        if (edge[endpoint] is not JsonValue value || !value.TryGetValue<string>(out var id) || !ids.Contains(id))
                            throw new JsonException("Connection endpoint does not exist.", edgePath + "." + endpoint, null, null);
                }
            }
            else if (workflow["Connections"] != null) throw new JsonException("Connections must be an array.", path + ".Connections", null, null);
        }
        if (legacy)
        {
            SolutionFormatPolicy.PromoteCandidate(root);
            diagnostics.Add(new("COMPAT_FORMAT_1_TO_2", "Compatibility", "Warning", "$.SchemaVersion",
                "Format 1 was adapted to format 2 in memory; the source file is unchanged."));
        }
    }

    private static string CanonicalType(string name)
    {
        var type = SdkTypeVocabulary.ToSystemType(name);
        if (type?.IsArray == true) type = type.GetElementType();
        if (type == typeof(int)) type = typeof(long);
        if (type == typeof(float)) type = typeof(double);
        return type?.FullName ?? throw new JsonException("Unknown variable type: " + name);
    }

    private static void RequireId(JsonNode node, string path)
    {
        if (node is not JsonObject || string.IsNullOrWhiteSpace(node["Id"]?.GetValue<string>()))
            throw new JsonException("Identity is required.", path + ".Id", null, null);
    }

    internal static void CheckDuplicateKeys(string json)
    {
        using var doc = JsonDocument.Parse(json);
        void Visit(JsonElement item, string path)
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in item.EnumerateObject())
                {
                    if (!keys.Add(p.Name)) throw new JsonException("Ambiguous duplicate property.", path + "." + p.Name, null, null);
                    Visit(p.Value, path + "." + p.Name);
                }
            }
            else if (item.ValueKind == JsonValueKind.Array)
            { int i = 0; foreach (var child in item.EnumerateArray()) Visit(child, path + "[" + i++ + "]"); }
        }
        Visit(doc.RootElement, "$");
    }
}

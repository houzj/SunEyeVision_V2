using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Workflow.Persistence;

/// <summary>Only documented V1 conversions; never writes the input file.</summary>
internal static class SolutionCompatibilityAdapter
{
    public static void Prepare(JsonNode root, List<PersistenceDiagnostic> diagnostics)
    {
        CheckDuplicateKeys(root.ToJsonString());
        RequireId(root, "$");
        if (root["SchemaVersion"] != null && root["SchemaVersion"]!.GetValue<int>() != 1)
            throw new JsonException("Unsupported SchemaVersion.", "$.SchemaVersion", null, null);
        if (root["GlobalVariables"] is JsonArray variables)
        for (int i = 0; i < variables.Count; i++)
        {
            var variable = variables[i] ?? throw new JsonException("Null variable.");
            var path = "$.GlobalVariables[" + i + "]";
            RequireId(variable, path);
            if (variable["Value"] == null) continue;
            var wrapper = variable["Value"] as JsonObject ?? throw new JsonException("Value wrapper must be an object.", path + ".Value", null, null);
            var type = wrapper["Type"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(type) || !wrapper.ContainsKey("Value") || SdkTypeVocabulary.ToSystemType(type) == null)
                throw new JsonException("Invalid variable wrapper.", path + ".Value", null, null);
            var declared = variable["Type"]?.GetValue<string>() ?? throw new JsonException("Variable Type is required.", path + ".Type", null, null);
            if (CanonicalType(declared) != CanonicalType(type))
                throw new JsonException("Variable Type disagrees with value Type.", path + ".Value.Type", null, null);
            var normalizedType = CanonicalType(type) switch
            {
                "System.Int64" => "Integer", "System.Double" => "Double",
                "System.String" => "String", "System.Boolean" => "Boolean", _ => type
            };
            if (normalizedType != type)
            {
                wrapper["Type"] = normalizedType;
                variable["Type"] = normalizedType;
                diagnostics.Add(new("COMPAT_TYPE_ALIAS", "Compatibility", "Warning", path + ".Type", "Known legacy type alias normalized in candidate only."));
                type = normalizedType;
            }
            if (wrapper["Value"] == null)
                diagnostics.Add(new("COMPAT_TYPED_NULL", "Compatibility", "Warning", path + ".Value", "Typed null retained as an empty value."));
            else if (type is "Integer" or "Double" or "String" or "Boolean" && wrapper["Value"] is not JsonArray)
            {
                wrapper["Value"] = new JsonArray(wrapper["Value"]!.DeepClone());
                diagnostics.Add(new("COMPAT_SCALAR", "Compatibility", "Warning", path + ".Value.Value", "V1 scalar promoted to a single-element tuple in candidate only."));
            }
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

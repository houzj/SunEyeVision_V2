using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using System.Text.Json.Serialization;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Domain.Persistence;

namespace SunEyeVision.Persistence;

/// <summary>Strict validation of the current document format; never changes source bytes.</summary>
public static class SolutionDocumentValidator
{
    public static void Prepare(JsonNode root, List<PersistenceDiagnostic> diagnostics, FrozenVariableTypeCatalog types, JsonSerializerOptions options)
    {
        CheckDuplicateKeys(root.ToJsonString());
        RequireId(root, "$");
        _ = SolutionFormatPolicy.ReadVersion(root);
        RejectUnknownMembers(root, "$", SerializableFields(typeof(SolutionDefinition)));
        if (root["GlobalVariables"] is JsonArray variables)
        for (int i = 0; i < variables.Count; i++)
        {
            var variable = variables[i] ?? throw new JsonException("Null variable.");
            var path = "$.GlobalVariables[" + i + "]";
            ValidateVariable(variable,path,types,diagnostics);
        }
        if (root["Devices"] is JsonArray devices)
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i]!;
            var path = "$.Devices[" + i + "]";
            RejectUnknownMembers(device, path, SerializableFields(typeof(Device)));
            foreach (var field in new[] { "Name", "Manufacturer", "Model", "SerialNumber", "IpAddress", "Description", "Port", "IsEnabled", "Settings", "CreatedTime", "ModifiedTime", "Type", "Status" })
                if (device is JsonObject deviceObject && deviceObject.ContainsKey(field) && device[field] == null)
                    throw new JsonException("Explicit null is not valid for this device field.", path + "." + field, null, null);
            var missing = new List<string>();
            foreach (var field in new[] { "Name", "Type", "Port", "Status", "IsEnabled", "Settings", "CreatedTime", "ModifiedTime" })
                if (device is JsonObject deviceObject && !deviceObject.ContainsKey(field)) missing.Add(field);
            if (missing.Count > 0)
                throw new JsonException("Required device fields are missing: " + string.Join(", ", missing), path, null, null);
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
            // WorkflowJsonConverter writes exactly these structural fields; extension data is not an approved schema.
            RejectUnknownMembers(workflow, path, new[] { "Id", "Name", "IsSubroutine", "WorkspaceSettings", "RootConfiguration", "Nodes", "Connections", "AdditionalData" });
            if(workflow is JsonObject flowObject && flowObject.ContainsKey("WorkspaceSettings"))
            {
                var settingsPath=path+".WorkspaceSettings";
                if(workflow["IsSubroutine"]?.GetValue<bool>()==true)throw new JsonException("Group不能声明根工作区设置。",settingsPath,null,null);
                if(workflow["WorkspaceSettings"] is not JsonObject workspace)throw new JsonException("根工作区设置必须是对象。",settingsPath,null,null);
                RejectUnknownMembers(workspace,settingsPath,SerializableFields(typeof(RootWorkspaceSettings)));
                try
                {
                    var value=JsonSerializer.Deserialize<RootWorkspaceSettings>(workspace.ToJsonString(),options)??throw new JsonException("根工作区设置缺失。");
                    value.Validate();
                }
                catch(Exception error){throw new JsonException("根工作区设置无效："+error.Message,settingsPath,null,null);}
            }
            if(workflow is JsonObject rootObject&&rootObject.ContainsKey("RootConfiguration"))
            {
                var configurationPath=path+".RootConfiguration";
                if(workflow["IsSubroutine"]?.GetValue<bool>()==true||workflow["RootConfiguration"] is not JsonObject rootConfiguration)throw new JsonException("仅根流程可声明根配置对象。",configurationPath,null,null);
                RejectUnknownMembers(rootConfiguration,configurationPath,SerializableFields(typeof(RootWorkflowConfiguration)));
                if(rootConfiguration["RunParameters"] is JsonArray proxies)for(int i=0;i<proxies.Count;i++)RejectUnknownMembers(proxies[i]??throw new JsonException("参数代理不能为null。"),configurationPath+".RunParameters["+i+"]",SerializableFields(typeof(RootParameterProxy)));
                try{(JsonSerializer.Deserialize<RootWorkflowConfiguration>(rootConfiguration.ToJsonString(),options)??throw new JsonException("根配置缺失。")).Validate();}
                catch(Exception error){throw new JsonException("根配置无效："+error.Message,configurationPath,null,null);}
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (workflow["Nodes"] is JsonArray nodes)
            for (int n = 0; n < nodes.Count; n++)
            {
                var node = nodes[n] ?? throw new JsonException("Null node.", path + ".Nodes[" + n + "]", null, null);
                RequireId(node, path + ".Nodes[" + n + "]");
                RejectUnknownMembers(node, path + ".Nodes[" + n + "]", new[] { "Id", "Name", "DispName", "ToolType", "GlobalIndex", "LocalIndex", "IsEnabled", "SubroutineId", "SelectedDisplayReference", "PositionX", "PositionY", "Width", "Height", "Configuration", "AdditionalData" });
                if (!ids.Add(node["Id"]!.GetValue<string>())) throw new JsonException("Duplicate node identity.", path + ".Nodes[" + n + "].Id", null, null);
                if (node is JsonObject nodeObject && (nodeObject.ContainsKey("Parameters") || nodeObject.ContainsKey("ParamSettings")))
                    throw new JsonException("Current Runtime nodes cannot contain SDK parameter models.", path + ".Nodes[" + n + "]", null, null);
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
                    RejectUnknownMembers(edge, edgePath, SerializableFields(typeof(ConnectionDefinition)));
                    if (!edgeIds.Add(edge["Id"]!.GetValue<string>())) throw new JsonException("Duplicate connection.", edgePath + ".Id", null, null);
                    foreach (var endpoint in new[] { "SourceNodeId", "TargetNodeId" })
                        if (edge[endpoint] is not JsonValue value || !value.TryGetValue<string>(out var id) || !ids.Contains(id))
                            throw new JsonException("Connection endpoint does not exist.", edgePath + "." + endpoint, null, null);
                }
            }
            else if (workflow["Connections"] != null) throw new JsonException("Connections must be an array.", path + ".Connections", null, null);
        }
    }

    internal static void ValidateVariable(JsonNode variable,string path,FrozenVariableTypeCatalog types,List<PersistenceDiagnostic> diagnostics)
    {
        RequireId(variable,path);
        RejectUnknownMembers(variable,path,SerializableFields(typeof(GlobalVariable)));
        if (variable["Type"] is JsonValue type && type.TryGetValue<string>(out var name) && !string.IsNullOrWhiteSpace(name))
            _ = types.CanonicalType(name);
        else
            throw new JsonException("Variable Type is required.", path + ".Type", null, null);
        SolutionVariableValuePolicy.Prepare(variable,path,types.CanonicalType,
            (code,diagnosticPath,message)=>diagnostics.Add(new(code,"Validation","Warning",diagnosticPath,message)));
    }
    private static IEnumerable<string> SerializableFields(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.GetIndexParameters().Length == 0
            && property.GetCustomAttribute<JsonExtensionDataAttribute>() is null
            && property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always)
        .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name);

    private static void RejectUnknownMembers(JsonNode node, string path, IEnumerable<string> names)
    {
        if (node is not JsonObject value) throw new JsonException("Expected a declared object.", path, null, null);
        var known = names.ToHashSet(StringComparer.Ordinal);
        foreach (var property in value)
            if (!known.Contains(property.Key))
                throw new JsonException("Unknown field; no approved migration exists.", path + "." + property.Key, null, null);
    }

    private static void RequireId(JsonNode node, string path)
    {
        if (node is not JsonObject || string.IsNullOrWhiteSpace(node["Id"]?.GetValue<string>()))
            throw new JsonException("Identity is required.", path + ".Id", null, null);
    }

    public static void CheckDuplicateKeys(string json)
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

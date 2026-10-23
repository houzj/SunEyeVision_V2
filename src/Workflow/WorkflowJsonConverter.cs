using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace SunEyeVision.Workflow;

/// <summary>Reads a pure candidate document. UI conversion belongs to workspace activation.</summary>
public class WorkflowJsonConverter : JsonConverter<Workflow>
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal) { "Id", "Name", "IsSubroutine", "Nodes", "Connections" };
    public override Workflow Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var id = root.GetProperty("Id").GetString();
        if (string.IsNullOrWhiteSpace(id)) throw new JsonException("Workflow Id is required.");
        var workflow = new Workflow(id, root.GetProperty("Name").GetString() ?? "");
        if (root.TryGetProperty("IsSubroutine", out var sub)) workflow.IsSubroutine = sub.GetBoolean();
        if (root.TryGetProperty("Nodes", out var nodes))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int nodeIndex = 0;
            foreach (var element in nodes.EnumerateArray())
            {
                WorkflowNodeBase node;
                try { node = JsonSerializer.Deserialize<WorkflowNodeBase>(element.GetRawText(), options) ?? throw new JsonException("Null workflow node."); }
                catch (Exception ex)
                {
                    var path = "$.Nodes[" + nodeIndex + "]" + ((ex as JsonException)?.Path is { } inner ? inner.TrimStart('$') : "");
                    var error = new JsonException(ex.Message, path, null, null);
                    error.Data["WorkflowId"] = id;
                    throw error;
                }
                if (string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id)) throw new JsonException("Empty or duplicate node Id.");
                workflow.Nodes.Add(node);
                nodeIndex++;
            }
        }
        if (root.TryGetProperty("Connections", out var connections))
        {
            var list = JsonSerializer.Deserialize<List<WorkflowConnection>>(connections.GetRawText(), options) ?? throw new JsonException("Null connection list.");
            var ids = workflow.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in list)
                if (edge == null || string.IsNullOrWhiteSpace(edge.Id) || !edgeIds.Add(edge.Id) || !ids.Contains(edge.SourceNodeId) || !ids.Contains(edge.TargetNodeId))
                    throw new JsonException("Invalid connection identity or missing endpoint.");
            workflow.Connections = new ObservableCollection<WorkflowConnection>(list);
        }
        workflow.AdditionalData = root.EnumerateObject().Where(p => !Known.Contains(p.Name)).ToDictionary(p => p.Name, p => p.Value.Clone());
        return workflow;
    }
    public override void Write(Utf8JsonWriter writer, Workflow value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Id", value.Id); writer.WriteString("Name", value.Name); writer.WriteBoolean("IsSubroutine", value.IsSubroutine);
        writer.WritePropertyName("Nodes"); JsonSerializer.Serialize(writer, value.Nodes, options);
        writer.WritePropertyName("Connections"); JsonSerializer.Serialize(writer, value.Connections, options);
        if (value.AdditionalData != null)
            foreach (var pair in value.AdditionalData.Where(p => !Known.Contains(p.Key))) { writer.WritePropertyName(pair.Key); pair.Value.WriteTo(writer); }
        writer.WriteEndObject();
    }
}

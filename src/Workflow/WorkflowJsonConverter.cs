using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.Runtime;
namespace SunEyeVision.Workflow;

/// <summary>Reads a pure candidate document. UI conversion belongs to workspace activation.</summary>
public class WorkflowJsonConverter : JsonConverter<Workflow>
{
    private static void ValidateNodeLayout(WorkflowNodeBase node)
    {
        if (!double.IsFinite(node.PositionX) || !double.IsFinite(node.PositionY) ||
            !double.IsFinite(node.Width) || !double.IsFinite(node.Height))
            throw new JsonException("Node layout position and size must be finite.");
    }
    private readonly RuntimeDirectorySnapshot? _directory;
    public WorkflowJsonConverter() { }
    public WorkflowJsonConverter(RuntimeDirectorySnapshot directory)=>_directory=directory??throw new ArgumentNullException(nameof(directory));
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal) { "Id", "Name", "IsSubroutine", "WorkspaceSettings", "RootConfiguration", "Nodes", "Connections" };
    public override Workflow Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var id = root.GetProperty("Id").GetString();
        if (string.IsNullOrWhiteSpace(id)) throw new JsonException("Workflow Id is required.");
        var workflow = new Workflow(id, root.GetProperty("Name").GetString() ?? "");
        if (root.TryGetProperty("IsSubroutine", out var sub)) workflow.IsSubroutine = sub.GetBoolean();
        if (!workflow.IsSubroutine && root.TryGetProperty("WorkspaceSettings", out var settings))
        {
            try { workflow.WorkspaceSettings = JsonSerializer.Deserialize<RootWorkspaceSettings>(settings.GetRawText(),options) ?? throw new JsonException("根流程工作区配置缺失。"); }
            catch(Exception error) { throw new JsonException("根流程工作区配置无效。",error); }
        }
        if(!workflow.IsSubroutine&&root.TryGetProperty("RootConfiguration",out var rootConfiguration))
        {try{workflow.RootConfiguration=JsonSerializer.Deserialize<RootWorkflowConfiguration>(rootConfiguration.GetRawText(),options)??throw new JsonException("根流程配置缺失。");}catch(Exception error){throw new JsonException("根流程配置无效。",error);}}
        if (root.TryGetProperty("Nodes", out var nodes))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int nodeIndex = 0;
            foreach (var element in nodes.EnumerateArray())
            {
                var runtimeMembers = element.EnumerateObject().Where(p => p.Name.Equals("RuntimeConfiguration", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (runtimeMembers.Length > 1) throw new JsonException("Duplicate RuntimeConfiguration.");
                if (runtimeMembers.Length != 0 && runtimeMembers[0].Value.ValueKind != JsonValueKind.Object)
                    throw new JsonException("RuntimeConfiguration must be an object when present.");
                if (element.EnumerateObject().Any(p => p.Name.Equals("Parameters", StringComparison.OrdinalIgnoreCase)||p.Name.Equals("ParamSettings", StringComparison.OrdinalIgnoreCase)))
                    throw new JsonException("Nodes cannot persist SDK Parameters or ParamSettings.");
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
                ValidateNodeLayout(node);
                if (node.RuntimeConfiguration is { } configuration && configuration.ToolId != node.ToolType)
                    throw new JsonException("Runtime configuration ToolId differs from the node identity.");
                if ((_directory??throw new JsonException("Workflow serialization requires an admitted runtime directory.")).Contains(node.ToolType) && node.RuntimeConfiguration is null)
                    throw new JsonException("Runtime tool requires its persisted RuntimeConfiguration.");
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
        foreach (var node in value.Nodes)
        {
            ValidateNodeLayout(node);
            RuntimeNodeAdmission.RequireShape(node);
            if (node.RuntimeConfiguration is { } configuration && configuration.ToolId != node.ToolType)
                throw new JsonException("Runtime configuration ToolId differs from the node identity.");
            if ((_directory??throw new JsonException("Workflow serialization requires an admitted runtime directory.")).Contains(node.ToolType) && node.RuntimeConfiguration is null)
                throw new JsonException("Runtime tool requires its persisted RuntimeConfiguration.");
        }
        writer.WriteStartObject();
        writer.WriteString("Id", value.Id); writer.WriteString("Name", value.Name); writer.WriteBoolean("IsSubroutine", value.IsSubroutine);
        if(!value.IsSubroutine){value.WorkspaceSettings.Validate();writer.WritePropertyName("WorkspaceSettings");JsonSerializer.Serialize(writer,value.WorkspaceSettings,options);}
        if(!value.IsSubroutine){value.RootConfiguration.Validate();writer.WritePropertyName("RootConfiguration");JsonSerializer.Serialize(writer,value.RootConfiguration,options);}
        writer.WritePropertyName("Nodes"); JsonSerializer.Serialize(writer, value.Nodes, options);
        writer.WritePropertyName("Connections"); JsonSerializer.Serialize(writer, value.Connections, options);
        if (value.AdditionalData != null)
            foreach (var pair in value.AdditionalData.Where(p => !Known.Contains(p.Key))) { writer.WritePropertyName(pair.Key); pair.Value.WriteTo(writer); }
        writer.WriteEndObject();
    }
}

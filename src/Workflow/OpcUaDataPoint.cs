using System;
using System.Text.Json.Serialization;

namespace SunEyeVision.Workflow;

/// <summary>Persisted OPC UA string NodeId configuration; values and quality live in the runtime.</summary>
public sealed class OpcUaDataPoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int NamespaceIndex { get; set; } = 2;
    public string TagName { get; set; } = string.Empty;
    public string MonitorMode { get; set; } = "变化订阅";
    public int MonitorIntervalMs { get; set; } = 100;
    [JsonIgnore] public string Name => (TagName ?? string.Empty)[((TagName ?? string.Empty).LastIndexOf('.') + 1)..];
    [JsonIgnore] public string NodeId => $"ns={NamespaceIndex};s={TagName ?? string.Empty}";

    public OpcUaDataPoint Clone() => new() { NamespaceIndex = NamespaceIndex, TagName = TagName,
        MonitorMode = MonitorMode, MonitorIntervalMs = MonitorIntervalMs };
}

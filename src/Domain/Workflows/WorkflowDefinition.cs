using System.Text.Json;

namespace SunEyeVision.Domain.Workflows;

/// <summary>One document-owned node. Editing replaces configuration versions, not node identity.</summary>
public sealed class NodeDefinition
{
    public NodeDefinition(string id, string name, string toolType, ToolConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id); ArgumentException.ThrowIfNullOrWhiteSpace(toolType);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.ToolId != toolType) throw new ArgumentException("节点与配置工具身份不一致。", nameof(configuration));
        Id = id; Name = name ?? throw new ArgumentNullException(nameof(name)); ToolType = toolType; Configuration = configuration;
    }
    public string Id { get; }
    public string Name { get; internal set; }
    public string DispName { get; internal set; } = string.Empty;
    public string ToolType { get; }
    public int GlobalIndex { get; internal set; } = -1;
    public int LocalIndex { get; internal set; } = -1;
    public bool IsEnabled { get; internal set; } = true;
    public string? SubroutineId { get; internal set; }
    public string? SelectedDisplayReference { get; internal set; }
    private IReadOnlyDictionary<string, JsonElement>? _additionalData;
    public IReadOnlyDictionary<string, JsonElement>? AdditionalData
    {
        get => _additionalData;
        internal set => _additionalData = value is null ? null :
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, JsonElement>(
                value.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal));
    }
    public double PositionX { get; internal set; }
    public double PositionY { get; internal set; }
    public double Width { get; internal set; } = 140;
    public double Height { get; internal set; } = 90;
    public CanvasPoint Position => new(PositionX, PositionY);
    public ToolConfiguration Configuration { get; internal set; }
}

public sealed class ConnectionDefinition
{
    public ConnectionDefinition(string id, string sourceNodeId, string targetNodeId, string sourcePort, string targetPort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id); ArgumentException.ThrowIfNullOrWhiteSpace(sourceNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeId); Id = id; SourceNodeId = sourceNodeId;
        TargetNodeId = targetNodeId; SourcePort = sourcePort; TargetPort = targetPort;
    }
    public string Id { get; }
    public string SourceNodeId { get; internal set; }
    public string TargetNodeId { get; internal set; }
    public string SourcePort { get; internal set; }
    public string TargetPort { get; internal set; }
    private IReadOnlyList<CanvasPoint>? _manualPathPoints;
    public IReadOnlyList<CanvasPoint>? ManualPathPoints
    {
        get => _manualPathPoints;
        internal set => _manualPathPoints = value is null ? null : Array.AsReadOnly(value.ToArray());
    }
}

public sealed class WorkflowDefinition
{
    private readonly List<NodeDefinition> _nodes = new();
    private readonly List<ConnectionDefinition> _connections = new();
    public WorkflowDefinition(string id, string name) { ArgumentException.ThrowIfNullOrWhiteSpace(id); Id = id; Name = name; }
    public string Id { get; }
    public string Name { get; internal set; }
    public bool IsSubroutine { get; internal set; }
    public RootWorkflowConfiguration RootConfiguration { get; internal set; } = new();
    public RootWorkspaceSettings WorkspaceSettings { get; internal set; } = new();
    private IReadOnlyDictionary<string, JsonElement>? _additionalData;
    public IReadOnlyDictionary<string, JsonElement>? AdditionalData
    {
        get => _additionalData;
        internal set => _additionalData = value is null ? null :
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, JsonElement>(
                value.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal));
    }
    public IReadOnlyList<NodeDefinition> Nodes => _nodes.AsReadOnly();
    public IReadOnlyList<ConnectionDefinition> Connections => _connections.AsReadOnly();
    internal List<NodeDefinition> MutableNodes => _nodes;
    internal List<ConnectionDefinition> MutableConnections => _connections;
}



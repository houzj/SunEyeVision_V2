using System.Text.Json;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Domain.Solutions;

/// <summary>Document-owned solution definition. Runtime lifetime belongs to the application session.</summary>
public sealed class SolutionDefinition
{
    private readonly List<WorkflowDefinition> _workflows = new();
    public SolutionDefinition(string id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id; Name = name ?? throw new ArgumentNullException(nameof(name));
    }
    public string Id { get; }
    public string Name { get; internal set; }
    public string Description { get; internal set; } = string.Empty;
    public int SchemaVersion { get; internal set; } = 5;
    public string Version { get; internal set; } = "1.0";
    private IReadOnlyDictionary<string, JsonElement>? _additionalData;
    public IReadOnlyDictionary<string, JsonElement>? AdditionalData
    {
        get => _additionalData;
        internal set => _additionalData = value is null ? null :
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, JsonElement>(
                value.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal));
    }
    public IReadOnlyList<WorkflowDefinition> Workflows => _workflows.AsReadOnly();
    internal List<WorkflowDefinition> MutableWorkflows => _workflows;
    private IReadOnlyList<GlobalVariable> _globalVariables = Array.Empty<GlobalVariable>();
    public IReadOnlyList<GlobalVariable> GlobalVariables { get => _globalVariables; internal set => _globalVariables = Array.AsReadOnly((value ?? throw new ArgumentNullException(nameof(value))).ToArray()); }
    private IReadOnlyList<Device> _devices = Array.Empty<Device>();
    public IReadOnlyList<Device> Devices { get => _devices; internal set => _devices = Array.AsReadOnly((value ?? throw new ArgumentNullException(nameof(value))).ToArray()); }
    private IReadOnlyList<Communication> _communications = Array.Empty<Communication>();
    public IReadOnlyList<Communication> Communications { get => _communications; internal set => _communications = Array.AsReadOnly((value ?? throw new ArgumentNullException(nameof(value))).ToArray()); }
    private IReadOnlyList<CommunicationEventDefinition> _communicationEvents = Array.Empty<CommunicationEventDefinition>();
    public IReadOnlyList<CommunicationEventDefinition> CommunicationEvents { get => _communicationEvents; internal set => _communicationEvents = Array.AsReadOnly((value ?? throw new ArgumentNullException(nameof(value))).ToArray()); }
    private IReadOnlyList<CommunicationSendEventDefinition> _communicationSendEvents = Array.Empty<CommunicationSendEventDefinition>();
    public IReadOnlyList<CommunicationSendEventDefinition> CommunicationSendEvents { get => _communicationSendEvents; internal set => _communicationSendEvents = Array.AsReadOnly((value ?? throw new ArgumentNullException(nameof(value))).ToArray()); }
    private IReadOnlyList<CommunicationHeartbeatDefinition> _communicationHeartbeats = Array.Empty<CommunicationHeartbeatDefinition>();
    public IReadOnlyList<CommunicationHeartbeatDefinition> CommunicationHeartbeats { get => _communicationHeartbeats; internal set => _communicationHeartbeats = Array.AsReadOnly((value ?? throw new ArgumentNullException(nameof(value))).ToArray()); }
    private IReadOnlyList<CommunicationTriggerDefinition> _communicationTriggers = Array.Empty<CommunicationTriggerDefinition>();
    public IReadOnlyList<CommunicationTriggerDefinition> CommunicationTriggers { get => _communicationTriggers; internal set => _communicationTriggers = Array.AsReadOnly((value ?? throw new ArgumentNullException(nameof(value))).ToArray()); }
    public CommunicationResponseConfiguration CommunicationResponses { get; internal set; } = new();
    public DatabaseConfiguration? DatabaseConfiguration { get; internal set; }
    public ExecutionStrategy? ExecutionStrategy { get; internal set; }
    private IReadOnlyList<SolutionVersion> _versionHistory = Array.Empty<SolutionVersion>();
    public IReadOnlyList<SolutionVersion> VersionHistory { get => _versionHistory; internal set => _versionHistory = Array.AsReadOnly((value ?? throw new ArgumentNullException(nameof(value))).ToArray()); }
}



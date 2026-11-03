using System.Text.Json;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Persistence;

/// <summary>Detached decoded data, not an active session. Mutable contained definitions are exclusively owned by this candidate until admission.</summary>
public sealed class SolutionReadCandidate : IDisposable
{
    private readonly DecodedResourcesScope _scope;
    private readonly byte[] _originalBytes;
    internal SolutionReadCandidate(DecodedResourcesScope scope, ReadOnlySpan<byte> originalBytes) { _scope=scope; _originalBytes=originalBytes.ToArray(); }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; }=string.Empty;
    public string Version { get; init; }="1.0";
    public IReadOnlyDictionary<string,JsonElement>? AdditionalData { get; init; }
    public IReadOnlyList<WorkflowReadCandidate> Workflows { get; init; }=Array.Empty<WorkflowReadCandidate>();
    public IReadOnlyList<GlobalVariable> GlobalVariables { get; init; }=Array.Empty<GlobalVariable>();
    public IReadOnlyList<Device> Devices { get; init; }=Array.Empty<Device>();
    public IReadOnlyList<Communication> Communications { get; init; }=Array.Empty<Communication>();
    public IReadOnlyList<CommunicationEventDefinition> CommunicationEvents { get; init; }=Array.Empty<CommunicationEventDefinition>();
    public IReadOnlyList<CommunicationSendEventDefinition> CommunicationSendEvents { get; init; }=Array.Empty<CommunicationSendEventDefinition>();
    public IReadOnlyList<CommunicationHeartbeatDefinition> CommunicationHeartbeats { get; init; }=Array.Empty<CommunicationHeartbeatDefinition>();
    public IReadOnlyList<CommunicationTriggerDefinition> CommunicationTriggers { get; init; }=Array.Empty<CommunicationTriggerDefinition>();
    public CommunicationResponseConfiguration CommunicationResponses { get; init; }=new();
    public DatabaseConfiguration? DatabaseConfiguration { get; init; }
    public ExecutionStrategy? ExecutionStrategy { get; init; }
    public IReadOnlyList<SolutionVersion> VersionHistory { get; init; }=Array.Empty<SolutionVersion>();
    public IReadOnlyList<FrozenResourceReference> ResourcePlan { get; init; }=Array.Empty<FrozenResourceReference>();
    public IReadOnlyList<PersistenceDiagnostic> Diagnostics { get; init; }=Array.Empty<PersistenceDiagnostic>();
    /// <summary>Only after full application admission; owner must be disposed on failed publish or after session drain.</summary>
    public IDisposable TransferResources() => _scope.Transfer();
    internal FrozenSolutionDocument PrepareSave(string targetPath,TargetExpectation? targetExpectation=null)
        => new(targetPath,_originalBytes,Diagnostics,ResourcePlan,targetExpectation);
    public void Dispose()=>_scope.Dispose();
}
public sealed record WorkflowReadCandidate(string Id,string Name,bool IsSubroutine,RootWorkflowConfiguration RootConfiguration,
    RootWorkspaceSettings WorkspaceSettings,IReadOnlyList<NodeReadCandidate> Nodes,IReadOnlyList<ConnectionReadCandidate> Connections);
public sealed record NodeReadCandidate(string Id,string Name,string DispName,string ToolType,int GlobalIndex,int LocalIndex,bool IsEnabled,
    string? SubroutineId,string? SelectedDisplayReference,double PositionX,double PositionY,double Width,double Height,ToolConfiguration Configuration);
public sealed record ConnectionReadCandidate(string Id,string SourceNodeId,string TargetNodeId,string SourcePort,string TargetPort,IReadOnlyList<CanvasPoint>? ManualPathPoints);

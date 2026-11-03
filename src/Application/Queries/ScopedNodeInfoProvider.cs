using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application.Queries;

/// <summary>All reads are confined to one validated document workflow scope.</summary>
public interface INodeInfoProvider
{
    string ScopeId { get; }
    string GetNodeName(string nodeId);
    string GetNodeType(string nodeId);
    string? GetNodeIcon(string nodeId);
    bool NodeExists(string nodeId);
    ToolSchema? GetSchema(string nodeId);
}

public interface IWorkflowConnectionProvider
{
    IReadOnlyList<string> GetParentNodeIds(string nodeId);
    IReadOnlyList<string> GetChildNodeIds(string nodeId);
    IReadOnlyList<string> GetAllNodeIds();
}

public sealed class ScopedNodeInfoProvider : INodeInfoProvider
{
    private readonly WorkflowScope _scope;
    public ScopedNodeInfoProvider(DocumentSession session, string workflowId)
    {
        ArgumentNullException.ThrowIfNull(session);
        _scope = session.OpenScope(workflowId);
    }
    public string ScopeId { get { _scope.Session.ValidateScope(_scope); return _scope.WorkflowId; } }
    public string GetNodeName(string nodeId) => _scope.Session.FindNode(_scope, nodeId)?.Node.Name ?? nodeId;
    public string GetNodeType(string nodeId) => _scope.Session.FindNode(_scope, nodeId)?.Node.ToolType ?? "Unknown";
    public string? GetNodeIcon(string nodeId)
    {
        var node = _scope.Session.FindNode(_scope, nodeId)?.Node;
        if (node is null) return null;
        var icon = _scope.Session.Directory.GetCatalog(node.ToolType)?.IconKey;
        _scope.Session.ValidateScope(_scope);
        return icon;
    }
    public bool NodeExists(string nodeId) => _scope.Session.FindNode(_scope, nodeId) is not null;
    public ToolSchema? GetSchema(string nodeId) => _scope.Session.FindNode(_scope, nodeId)?.Configuration.Schema;
}


/// <summary>Captures the real workflow graph under its session gate before traversing it.</summary>
public sealed class ScopedWorkflowConnectionProvider : IWorkflowConnectionProvider
{
    private readonly WorkflowScope _scope;
    public ScopedWorkflowConnectionProvider(DocumentSession session, string workflowId)
    {
        ArgumentNullException.ThrowIfNull(session);
        _scope = session.OpenScope(workflowId);
    }
    public IReadOnlyList<string> GetParentNodeIds(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var (_, edges) = _scope.Session.CaptureGraphQuery(_scope);
        var upstream = new List<string>();
        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { nodeId };
        pending.Enqueue(nodeId);
        while (pending.TryDequeue(out var current))
            foreach (var edge in edges)
                if (edge.Target == current && visited.Add(edge.Source)) { upstream.Add(edge.Source); pending.Enqueue(edge.Source); }
        _scope.Session.ValidateScope(_scope);
        return upstream.AsReadOnly();
    }
    public IReadOnlyList<string> GetChildNodeIds(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var (_, edges) = _scope.Session.CaptureGraphQuery(_scope);
        var children = edges.Where(edge => edge.Source == nodeId).Select(edge => edge.Target).Distinct(StringComparer.Ordinal).ToArray();
        _scope.Session.ValidateScope(_scope);
        return Array.AsReadOnly(children);
    }
    public IReadOnlyList<string> GetAllNodeIds() => Array.AsReadOnly(_scope.Session.CaptureGraphQuery(_scope).Nodes);
}


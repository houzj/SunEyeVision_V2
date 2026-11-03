using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

/// <summary>One actual solution document and its editing and execution lifetime.</summary>
public sealed partial class DocumentSession : IDisposable
{
    private readonly object _gate = new();
    private readonly string _documentId;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly RuntimeDirectorySnapshot _directory;
    private readonly ToolExecutionOwner _owner = new();
    private readonly CancellationTokenRegistration _directoryRetirement;
    private readonly Dictionary<string, Func<ToolConfiguration, AdmittedToolConfiguration>> _definitions = new(StringComparer.Ordinal);
    private bool _closed, _closing, _admitting;
    public Guid EditSessionId { get; } = Guid.NewGuid();
    public Guid ResultOwnerId => _owner.Id;
    public SolutionDefinition Document { get; }
    internal DocumentHistoryCoordinator History { get; }
    internal RuntimeDirectorySnapshot Directory => _directory;
    internal ToolExecutionOwner ExecutionOwner => _owner;

    internal DocumentSession(SolutionDefinition document, RuntimeDirectorySnapshot directory)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        if (!directory.IsActive) throw new InvalidOperationException("方案运行目录已退役。");
        _documentId = document.Id;
        History = new DocumentHistoryCoordinator(EditSessionId);
        _directoryRetirement = directory.RetirementToken.Register(() => { lock (_gate) _definitions.Clear(); });
    }

    internal WorkflowScope OpenScope(string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        lock (_gate)
        {
            EnsureOpen();
            var matches = Document.Workflows.Where(flow => flow.Id == workflowId).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("流程不属于当前方案或身份不唯一。");
            return new WorkflowScope(this, matches[0], workflowId);
        }
    }

    /// <summary>Read the actual nodes of one live workflow; returned identities remain owned by this session.</summary>
    public IReadOnlyList<NodeDefinition> GetNodes(string workflowId)
    {
        lock (_gate)
        {
            var scope = OpenScope(workflowId);
            ValidateScopeCore(scope);
            return Array.AsReadOnly(scope.Flow.Nodes.ToArray());
        }
    }

    public IReadOnlyList<ConnectionDefinition> GetConnections(string workflowId)
    {
        lock (_gate)
        {
            var scope = OpenScope(workflowId);
            ValidateScopeCore(scope);
            return Array.AsReadOnly(scope.Flow.Connections.ToArray());
        }
    }

    /// <summary>Resolve through this session's directory and owner; null means the scoped node is absent.</summary>
    public NodeDefinition? FindNode(string workflowId, string nodeId)
    {
        lock (_gate) return FindNode(OpenScope(workflowId), nodeId)?.Node;
    }

    /// <summary>Prepare a private proposal. It does not mutate the session or commit history.</summary>
    public GraphEditDraft PrepareGraphEdit(string workflowId)
    {
        lock (_gate) return CreateGraphDraft(OpenScope(workflowId));
    }

    internal GraphEditDraft CreateGraphDraft(WorkflowScope scope)
    {
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            ValidateScopeCore(scope);
            return new GraphEditDraft(scope.Flow);
        }
    }
    internal (string[] Nodes, (string Source, string Target)[] Edges) CaptureGraphQuery(WorkflowScope scope)
    {
        lock (_gate)
        {
            ValidateScopeCore(scope);
            var nodes = scope.Flow.Nodes.Select(node => node.Id).ToArray();
            if (nodes.Distinct(StringComparer.Ordinal).Count() != nodes.Length)
                throw new InvalidOperationException("流程节点身份不唯一。");
            var edges = scope.Flow.Connections.Select(edge => (edge.SourceNodeId, edge.TargetNodeId)).ToArray();
            return (nodes, edges);
        }
    }
    internal AdmittedNodeView? FindNode(WorkflowScope scope, string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        lock (_gate)
        {
            ValidateScope(scope);
            var nodes = scope.Flow.Nodes.Where(node => node.Id == nodeId).ToArray();
            if (nodes.Length > 1) throw new InvalidOperationException("节点身份不唯一。");
            if (nodes.Length == 0) return null;
            var node = nodes[0];
            if (node.Configuration.ToolId != node.ToolType)
                throw new InvalidOperationException("节点与配置工具身份不一致。");
            var capturedConfiguration = node.Configuration;
            var capturedToolId = node.ToolType;
            var admitted = AdmitConfiguration(node, capturedConfiguration);
            ValidateScopeCore(scope);
            var current = scope.Flow.Nodes.Where(candidate => candidate.Id == nodeId).ToArray();
            if (current.Length != 1 || !ReferenceEquals(current[0], node) ||
                node.ToolType != capturedToolId || !ReferenceEquals(node.Configuration, capturedConfiguration))
                throw new InvalidOperationException("准入期间节点身份、成员或配置已变化。");
            return new AdmittedNodeView(scope, node, admitted);
        }
    }

    private AdmittedToolConfiguration AdmitConfiguration(NodeDefinition node, ToolConfiguration configuration)
    {
        EnsureOpen();
        if (_admitting) throw new InvalidOperationException("工具声明准入期间不能重入。");
        if (configuration.ToolId != node.ToolType) throw new InvalidOperationException("节点与配置工具身份不一致。");
        _admitting = true;
        try
        {
            if (!_definitions.TryGetValue(node.ToolType, out var factory))
            {
                factory = AdmittedToolConfiguration.CreateFactory(_directory, node.ToolType);
                _definitions.Add(node.ToolType, factory);
            }
            return factory(configuration);
        }
        finally { _admitting = false; }
    }

    // Candidate-only preflight: detaches bindings owned by removed/retargeted graph edges.
    // The authoritative node is not changed here; full reconciliation and one atomic commit follow.
    private IReadOnlyDictionary<NodeDefinition, ToolConfiguration> PrepareRemovedConnectionBindings(GraphEditDraft draft)
    {
        var replacements = new Dictionary<NodeDefinition, ToolConfiguration>(ReferenceEqualityComparer.Instance);
        foreach (var edge in draft.Workflow.Connections)
        {
            var retained = draft.Edges.Any(item => item.Edge.Id == edge.Id &&
                item.Edge.SourceNodeId == edge.SourceNodeId && item.Edge.TargetNodeId == edge.TargetNodeId &&
                item.SourcePort == edge.SourcePort && item.TargetPort == edge.TargetPort);
            if (retained) continue;
            var target = draft.Nodes.SingleOrDefault(node => node.Id == edge.TargetNodeId);
            if (target is null) continue;
            var current = replacements.GetValueOrDefault(target) ?? target.Configuration;
            var admitted = AdmitConfiguration(target, current);
            if (_admitting) throw new InvalidOperationException("工具声明准入期间不能重入。");
            ToolConfiguration? detached;
            _admitting = true;
            try { detached = ConnectionBindingPolicy.RemoveAutomaticImageBinding(admitted, edge.Id); }
            finally { _admitting = false; }
            if (detached is not null) replacements[target] = detached;
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<NodeDefinition, ToolConfiguration>(replacements);
    }
    internal AdmittedNodeView RequireNode(WorkflowScope scope, string nodeId) =>
        FindNode(scope, nodeId) ?? throw new InvalidOperationException("节点不存在于当前流程。");

    internal AdmittedNodeView RequireCurrent(AdmittedNodeView captured, bool executionOnly = false)
    {
        ArgumentNullException.ThrowIfNull(captured);
        lock (_gate)
        {
            ValidateScopeCore(captured.Scope);
            var current = RequireNode(captured.Scope, captured.Node.Id);
            if (!ReferenceEquals(current.Node, captured.Node) ||
                (executionOnly ? current.Configuration.ExecutionConfigurationIdentity != captured.Configuration.ExecutionConfigurationIdentity
                               : current.Configuration.ConfigurationIdentity != captured.Configuration.ConfigurationIdentity))
                throw new InvalidOperationException("节点实例或声明配置已变化，不能使用过期视图。");
            return current;
        }
    }
    internal void ValidateScope(WorkflowScope scope)
    {
        lock (_gate) ValidateScopeCore(scope);
    }

    private void ValidateScopeCore(WorkflowScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        EnsureOpen();
        if (!ReferenceEquals(scope.Session, this) || Document.Id != _documentId || scope.Flow.Id != scope.WorkflowId ||
            Document.Workflows.Count(flow => ReferenceEquals(flow, scope.Flow)) != 1 ||
            Document.Workflows.Count(flow => flow.Id == scope.WorkflowId) != 1)
            throw new InvalidOperationException("方案或流程身份已变化，或流程已退出方案。");
    }

    private void EnsureOpen()
    {
        if (_closed || _closing || _owner.IsRetired || !_directory.IsActive)
            throw new InvalidOperationException("方案会话或运行目录已退役。");
    }

    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("方案会话必须在所属线程关闭。");
        lock (_gate)
        {
            if (_closed) return;
            if (_closing || _notifying || _admitting) throw new InvalidOperationException("方案正在关闭、通知或准入，不能重入关闭。");
            _closing = true;
        }
        Exception? retireFailure = null;
        try { _owner.Retire(); }
        catch (Exception error) { retireFailure = error; }
        if (!_owner.IsRetired)
        {
            lock (_gate) _closing = false;
            throw retireFailure ?? new InvalidOperationException("运行未排空，方案保持打开。");
        }
        lock (_gate)
        {
            _closed = true;
            _closing = false;
            _definitions.Clear();
        }
        _directoryRetirement.Dispose();
        Exception? historyFailure = null;
        try { History.Dispose(); } catch (Exception error) { historyFailure = error; }
        if (retireFailure is not null && historyFailure is not null) throw new AggregateException(retireFailure, historyFailure);
        if (retireFailure is not null) throw retireFailure;
        if (historyFailure is not null) throw historyFailure;
    }
}

internal sealed record WorkflowScope(DocumentSession Session, WorkflowDefinition Flow, string WorkflowId);
internal sealed record AdmittedNodeView(WorkflowScope Scope, NodeDefinition Node, AdmittedToolConfiguration Configuration);













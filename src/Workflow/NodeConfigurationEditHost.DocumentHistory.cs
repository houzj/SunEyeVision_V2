using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Workflow;

public sealed record ConfigurationHistoryReceipt(Guid CommitId, BindingConfigurationSnapshot Before,
    BindingConfigurationSnapshot After, IReadOnlyList<DocumentHistoryScope> Scopes);

public sealed partial class NodeConfigurationEditHost
{
    private readonly DocumentHistoryCoordinator? _documentHistory;
    public ConfigurationHistoryReceipt? LastHistoryReceipt { get; private set; }

    // Includes geometry and configuration, excludes live variable values and execution results.
    public static string DocumentHistorySignature(Solution document)
        => StructureSignature(document) + "\n" + ShapeSignature(document);

    private BindingConfigurationPublication CommitWithDocumentHistory(IReadOnlyList<NodeConfigurationEditDraft> nodes,
        GlobalVariableConfigurationEditDraft? variables, IReadOnlyList<RootWorkflowConfigurationEditDraft>? roots)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (_documentHistory is null || nodes.Any(n =>
            _design.Workflows.Any(w => w.Id == n.Identity.WorkflowId && w.IsSubroutine) || n.Node.ToolType == "Subroutine"))
            return CommitBatchCore(nodes, variables, roots, recordHistory: true);
        bool changed;
        try
        {
            lock (_gate)
            {
                var prepared = Compose(nodes, variables, roots);
                changed = ShapeSignature(prepared.Draft.Configuration) != ShapeSignature(_store.Capture().CopyConfiguration());
            }
        }
        finally { FlushStructureNotifications(); }
        // Value-only publications are explicit runtime commands, not editing-history entries.
        if (!changed) return CommitBatchCore(nodes, variables, roots, recordHistory: false);
        var scopes = nodes.Select(n => n.Identity.WorkflowId)
            .Concat((roots ?? Array.Empty<RootWorkflowConfigurationEditDraft>()).Select(r => r.WorkflowId))
            .Distinct().Select(id => new DocumentHistoryScope(id, id)).ToArray();
        var operation = new ConfigurationOperation(this, nodes, variables, roots, scopes);
        _documentHistory.Execute(operation);
        LastHistoryReceipt = operation.Receipt;
        return operation.Publication!;
    }

    private BindingConfigurationSnapshot RestoreDocumentHistory(bool undo)
    {
        if (_documentHistory is null) return RestoreHistory(undo);
        var restored = undo ? _documentHistory.Undo() : _documentHistory.Redo();
        if (!restored) throw new InvalidOperationException("没有可回放的编辑历史，或后续Group/外部修改尚未撤销。");
        return _store.Capture();
    }

    private BindingConfigurationSnapshot CaptureHistoryConfiguration()
    {
        var snapshot = _store.Capture();
        var configuration = snapshot.CopyConfiguration();
        foreach (var variable in configuration.GlobalVariables) variable.Value = null;
        return new(snapshot.Version, configuration,
            SolutionExecutionDependencyGraph.Compile(configuration, checkReadiness: false));
    }

    private BindingConfigurationPublication ReplayHistoryConfiguration(BindingConfigurationSnapshot target)
    {
        try
        {
            BindingConfigurationPublication accepted;
            List<(GlobalVariable Variable, bool ValueChanged)> notifications;
            lock (_gate)
            {
                EnsureBaseline();
                if (StructureSignature(target.CopyConfiguration()) != StructureSignature(_design))
                    throw new InvalidOperationException("配置回执的结构尚未恢复，不能越过后续结构编辑。");
                var projection = new Projection();
                try
                {
                    accepted = _store.ReplayAccepted(target, next => AcceptProjection(next, null, projection));
                    notifications = projection.Notifications;
                }
                catch { projection.Restore(this); throw; }
                finally { ReleaseProjection(projection); }
            }
            Emit(notifications);
            return accepted;
        }
        finally { FlushStructureNotifications(); }
    }

    /// <summary>Called only inside an authorized session-history operation after its graph algorithm
    /// has accepted a formal graph. This does not turn external/Group edits into root history.</summary>
    public IReadOnlyList<ScopedNodeIdentity> AcceptDocumentGraphHistory()
    {
        if (_documentHistory?.IsApplying != true)
            throw new InvalidOperationException("图历史接受必须处于当前方案的正式历史事务中。");
        lock (_gate)
        {
            var next = BindingConfigurationStore.Copy(_design);
            var candidate = new BindingConfigurationSnapshot(0, next,
                SolutionExecutionDependencyGraph.Compile(next, checkReadiness: false));
            var publication = _store.ReplayAccepted(candidate, _ => { });
            _nodeInstances.Clear();
            foreach (var pair in Nodes(_design)) _nodeInstances.Add(pair.Key, pair.Value);
            return publication.InvalidatedNodes;
        }
    }

    private sealed class ConfigurationOperation : IDocumentHistoryOperation, IDocumentHistoryReplayGuard, IDocumentHistoryReplayEffects
    {
        private readonly NodeConfigurationEditHost _host;
        private readonly IReadOnlyList<NodeConfigurationEditDraft> _nodes;
        private readonly GlobalVariableConfigurationEditDraft? _variables;
        private readonly IReadOnlyList<RootWorkflowConfigurationEditDraft>? _roots;
        private readonly Guid _commitId = Guid.NewGuid();
        private string _expected;
        private BindingConfigurationSnapshot? _before, _after;
        public string Description => _roots?.Count > 0 ? "修改流程配置" : _variables is not null ? "修改变量定义" : "修改节点参数";
        public IReadOnlyList<DocumentHistoryScope> Scopes { get; }
        public BindingConfigurationPublication? Publication { get; private set; }
        public IReadOnlyList<ScopedNodeIdentity> InvalidatedNodes { get; private set; } = Array.Empty<ScopedNodeIdentity>();
        public ConfigurationHistoryReceipt Receipt => new(_commitId, _before!, _after!, Array.AsReadOnly(Scopes.ToArray()));

        internal ConfigurationOperation(NodeConfigurationEditHost host, IReadOnlyList<NodeConfigurationEditDraft> nodes,
            GlobalVariableConfigurationEditDraft? variables, IReadOnlyList<RootWorkflowConfigurationEditDraft>? roots,
            IReadOnlyList<DocumentHistoryScope> scopes)
        { _host = host; _nodes = nodes; _variables = variables; _roots = roots; Scopes = scopes; _expected = DocumentHistorySignature(host._design); }

        public bool CanReplay(DocumentHistoryDirection direction) => _expected == DocumentHistorySignature(_host._design);

        public PreparedDocumentHistoryChange Prepare(DocumentHistoryDirection direction)
        {
            if (!CanReplay(direction)) throw new InvalidOperationException("配置历史之后有未纳入的Group或外部编辑。");
            var expected = _expected;
            var checkpoint = _host.CaptureHistoryConfiguration();
            var initial = _before is null;
            if (initial) _before = checkpoint;
            return new(() =>
            {
                if (initial)
                {
                    Publication = _host.CommitBatchCore(_nodes, _variables, _roots, recordHistory: false);
                    _after = _host.CaptureHistoryConfiguration();
                }
                else InvalidatedNodes = _host.ReplayHistoryConfiguration(direction == DocumentHistoryDirection.Backward ? _before! : _after!).InvalidatedNodes;
                _expected = DocumentHistorySignature(_host._design);
            }, () =>
            {
                if (DocumentHistorySignature(_host._design) != expected) _host.ReplayHistoryConfiguration(checkpoint);
                _expected = expected;
                InvalidatedNodes = Array.Empty<ScopedNodeIdentity>();
                if (initial)
                {
                    _before = null; _after = null;
                    foreach (var node in _nodes) node.Consumed = false;
                    if (_variables is not null) _variables.Consumed = false;
                    foreach (var root in _roots ?? Array.Empty<RootWorkflowConfigurationEditDraft>()) root.Consumed = false;
                }
            });
        }
        public void Dispose() { _before = null; _after = null; }
    }
}

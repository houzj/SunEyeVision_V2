using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    private readonly CancellationTokenSource _presentationRetirement = new();
    private readonly Dictionary<SunEyeVision.Domain.Workflows.NodeDefinition, CancellationTokenSource> _nodePresentations =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<CancellationTokenSource> _removedPresentations = new();
    public Exception? LastPresentationReleaseError { get; private set; }

    public ToolSchema? GetNodeSettingsSchema(string workflowId, string nodeId)
    {
        lock (_gate) return FindNode(OpenScope(workflowId), nodeId)?.Configuration.SettingsSchema;
    }

    public ToolSchema? GetNodeDisplaySchema(string workflowId, string nodeId)
    {
        lock (_gate) return FindNode(OpenScope(workflowId), nodeId)?.Configuration.DisplaySchema;
    }

    /// <summary>Admit the actual node, not a caller-supplied directory or tool-ID pair.
    /// A committed field edit does not replace that node's presentation identity.</summary>
    public ToolPresentationAdmission AdmitPresentation(string workflowId, string nodeId)
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("参数呈现必须由方案所属线程准入。");
        lock (_gate)
        {
            var scope = OpenScope(workflowId);
            var captured = RequireNode(scope, nodeId);
            if (!_nodePresentations.TryGetValue(captured.Node, out var lifetime))
            {
                lifetime = CancellationTokenSource.CreateLinkedTokenSource(_presentationRetirement.Token);
                _nodePresentations.Add(captured.Node, lifetime);
            }
            bool Current()
            {
                lock (_gate)
                {
                    if (_closed || _closing || _owner.IsRetired || !_directory.IsActive || Document.Id != _documentId ||
                        scope.Flow.Id != scope.WorkflowId || Document.Workflows.Count(flow => ReferenceEquals(flow, scope.Flow)) != 1 ||
                        Document.Workflows.Count(flow => flow.Id == scope.WorkflowId) != 1) return false;
                    var nodes = scope.Flow.Nodes.Where(node => node.Id == nodeId).ToArray();
                    return nodes.Length == 1 && ReferenceEquals(nodes[0], captured.Node) &&
                        nodes[0].ToolType == captured.Configuration.Configuration.ToolId;
                }
            }
            return new ToolPresentationAdmission(_directory, captured.Configuration.Definition, Current, lifetime.Token);
        }
    }

    // No cancellation callback executes under the transaction or owner lock.
    private void QueueRemovedPresentations(DocumentChangeSet change)
    {
        foreach (var removed in change.RemovedNodes)
            if (_nodePresentations.Remove(removed.Node, out var lifetime)) _removedPresentations.Add(lifetime);
    }

    private void CompleteChangePublication(DocumentChangeSet change)
    {
        _notifying = true;
        try
        {
            CancellationTokenSource[] removed;
            lock (_gate) { removed = _removedPresentations.ToArray(); _removedPresentations.Clear(); }
            var failures = new List<Exception>();
            foreach (var lifetime in removed)
            {
                try { lifetime.Cancel(throwOnFirstException: false); } catch (Exception error) { failures.Add(error); }
                finally { lifetime.Dispose(); }
            }
            LastPresentationReleaseError = failures.Count == 0 ? null : new AggregateException("节点已移除，部分呈现清理观察者失败。", failures);
            NotifyChanged(change);
        }
        finally { _notifying = false; }
    }

    private void RetirePresentations()
    {
        CancellationTokenSource[] sources;
        lock (_gate)
        {
            sources = _nodePresentations.Values.Concat(_removedPresentations).ToArray();
            _nodePresentations.Clear(); _removedPresentations.Clear();
        }
        try { _presentationRetirement.Cancel(throwOnFirstException: false); }
        finally
        {
            foreach (var source in sources) source.Dispose();
            _presentationRetirement.Dispose();
        }
    }
}

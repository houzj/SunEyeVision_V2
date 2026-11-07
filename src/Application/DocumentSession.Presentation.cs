using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{

    private readonly List<IDisposable> _invalidatedRuntimePublications = new();


    public ToolSchema? GetNodeSettingsSchema(string workflowId, string nodeId)
    {
        lock (_gate) return FindNode(OpenScope(workflowId), nodeId)?.Configuration.SettingsSchema;
    }

    public ToolSchema? GetNodeDisplaySchema(string workflowId, string nodeId)
    {
        lock (_gate) return FindNode(OpenScope(workflowId), nodeId)?.Configuration.DisplaySchema;
    }

    /// <summary>真实节点的输出声明，用于把已保存的引用还原成与选择时一致的显示文本。</summary>
    public ToolSchema? GetNodeOutputSchema(string workflowId, string nodeId)
    {
        lock (_gate) return FindNode(OpenScope(workflowId), nodeId)?.Configuration.Schema;
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
            var lifetime = CaptureNodeLifetime(scope, captured.Node);

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
            return new ToolPresentationAdmission(_directory, captured.Configuration.Definition, Current, lifetime.Retirement);
        }
    }

    private void CompleteChangePublication(DocumentChangeSet change)
    {
        _notifying = true;
        try
        {
            CancellationTokenSource[] removed;
            IDisposable[] invalidatedResults;
            lock (_gate)
            {
                removed = _retiredNodeLifetimes.ToArray(); _retiredNodeLifetimes.Clear();
                invalidatedResults = _invalidatedRuntimePublications.ToArray(); _invalidatedRuntimePublications.Clear();
            }
            var failures = new List<Exception>();
            foreach (var lifetime in removed)
            {
                try { lifetime.Cancel(throwOnFirstException: false); } catch (Exception error) { failures.Add(error); }
                finally { lifetime.Dispose(); }
            }
            LastNodeRetirementError = failures.Count == 0 ? null : new AggregateException("节点已移除，部分生命周期清理观察者失败。", failures);
            var resultFailures = new List<Exception>();
            foreach (var publication in invalidatedResults)
                try { publication.Dispose(); } catch (Exception error) { resultFailures.Add(error); }
            LastRuntimeReleaseError = resultFailures.Count == 0 ? null :
                new AggregateException("编辑已提交，部分失效结果 reader 释放失败。", resultFailures);
            NotifyChanged(change);
        }
        finally { _notifying = false; }
    }

}

using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    internal sealed record RunNodeCapture(WorkflowDefinition Workflow, NodeDefinition Node,
        ToolConfiguration Configuration, string ConfigurationIdentity);

    private RuntimeParameterValueAcceptance CreateRunParameterAcceptance(
        IReadOnlyDictionary<ScopedNodeIdentity, RunNodeCapture> captured)
    {
        return new RuntimeParameterValueAcceptance((snapshot, identity, fieldId, value) =>
        {
            var key = new ScopedNodeIdentity(snapshot.WorkflowId, snapshot.NodeId);
            if (!captured.TryGetValue(key, out var opening) || !ReferenceEquals(snapshot.Configuration, opening.Configuration) ||
                !identity.HasStartedExecution || !ReferenceEquals(identity.Owner, _owner) || identity.NodeId != snapshot.OwnerDomainId)
                throw new InvalidOperationException("参数值不属于实际运行节点、配置或 owner。 ");
            ToolConfiguration prior; ToolConfiguration prepared;
            lock (_gate)
            {
                EnsureOpen();
                if (_admitting || _preparing || !RunNodeIsCurrent(key, opening))
                    throw new InvalidOperationException("参数值接纳期间方案或节点已改变。 ");
                prior = opening.Node.Configuration;
                _preparing = true;
                try
                {
                    var current = AdmitConfiguration(opening.Node, prior);
                    if (current.ConfigurationIdentity != opening.ConfigurationIdentity)
                        throw new InvalidOperationException("参数声明或绑定已改变，拒绝旧运行值。 ");
                    prepared = current.WithAcceptedParameterValue(fieldId, value).Configuration;
                    if (!RunNodeIsCurrent(key, opening) || !ReferenceEquals(opening.Node.Configuration, prior))
                        throw new InvalidOperationException("参数候选准备期间正式节点已改变。 ");
                }
                finally { _preparing = false; }
            }
            var post = false;
            if (!identity.TryInvokeCallback(() =>
            {
                lock (_gate)
                {
                    EnsureOpen();
                    if (_admitting || _preparing || !RunNodeIsCurrent(key, opening) ||
                        !ReferenceEquals(opening.Node.Configuration, prior))
                        throw new InvalidOperationException("参数值安装前正式节点或配置版本已改变。 ");
                    opening.Node.Configuration = prepared;
                    var change = CreateCommittedChange(DocumentChangeOrigin.RuntimeCurrentValues,
                        new[] { new NodeChange(key, opening.Node, NodeChangedAspect.CurrentValues) },
                        Array.Empty<RemovedNode>(), Array.Empty<ConnectionChange>(), Array.Empty<VariableChange>(),
                        Array.Empty<ScopedNodeIdentity>());
                    post = EnqueueRuntimeChange(change);
                }
            })) throw new InvalidOperationException("运行身份已退役，不能安装参数当前值。 ");
            if (post) PostRuntimeChanges();
        });
    }

    private bool RunNodeIsCurrent(ScopedNodeIdentity key, RunNodeCapture captured) =>
        Document.Id == _documentId && captured.Workflow.Id == key.WorkflowId && captured.Node.Id == key.NodeId &&
        Document.Workflows.Count(flow => flow.Id == key.WorkflowId) == 1 &&
        Document.Workflows.Count(flow => ReferenceEquals(flow, captured.Workflow)) == 1 &&
        captured.Workflow.Nodes.Count(node => node.Id == key.NodeId) == 1 &&
        captured.Workflow.Nodes.Count(node => ReferenceEquals(node, captured.Node)) == 1 &&
        captured.Node.ToolType == captured.Configuration.ToolId;
}

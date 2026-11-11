using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    internal sealed record RunVariableCapture(GlobalVariable Actual, GlobalVariable Opening);

    private void AcceptRunNodeResult(ScopedNodeIdentity source, RunNodeCapture sourceNode,
        DeclaredOutputSnapshot result, IReadOnlyList<ExecutionGlobalProducerOperation> operations,
        ExecutionVariableValues values, IReadOnlyDictionary<string, RunVariableCapture> captured)
    {
        var relevant = operations.Where(operation => operation.Source.ScopeId == source.WorkflowId && operation.Source.NodeId == source.NodeId).ToArray();
        var identity = result.ExecutionIdentity ?? throw new InvalidOperationException("生产结果缺少原始运行身份。 ");
        if (!identity.HasStartedExecution || !ReferenceEquals(identity.Owner, _owner) || identity.NodeId != ExecutionNodeSnapshot.MakeOwnerDomainId(source.WorkflowId, source.NodeId) ||
            result.Identity != identity.ToInvocationIdentity(source.NodeId)) throw new InvalidOperationException("全局生产来源身份不一致。 ");
        var available = values.CaptureValues(); var diagnostics = values.CaptureDiagnostics();
        var writes = new List<(RunVariableCapture Capture, GlobalVariable.ValueState Prior, object? Value)>();
        lock (_gate)
        {
            EnsureOpen();
            if (!RunNodeIsCurrent(source, sourceNode) || !GraphState.SameConfiguration(sourceNode.Node.Configuration, sourceNode.Configuration))
                throw new InvalidOperationException("全局生产节点或配置已变化。 ");
            lock (GlobalVariable.ValueGate)
            {
                foreach (var operation in relevant)
                {
                    if (!result.IsSuccess) continue;
                    if (diagnostics.ContainsKey(operation.VariableId) || !available.TryGetValue(operation.VariableName, out var value)) continue;
                    if (!captured.TryGetValue(operation.VariableId, out var variable) || !RunVariableIsCurrent(variable) ||
                        variable.Actual.ValueRevision != variable.Opening.ValueRevision)
                        throw new InvalidOperationException("生产变量实例、声明或当前值已改变。 ");
                    Plugin.Contracts.VariableValueDeclarations.ValidateEditableValue(variable.Opening.Type, value);
                    writes.Add((variable, variable.Actual.CaptureValueRaw(), GlobalVariable.CopyEditableValue(value)));
                }
            }
        }
        var post = false;
        DeclaredOutputReader? previous = null;
        var next = result.AcquireReader();
        var installed = false;
        try
        {
            if (!identity.TryInvokeCallback(() =>
            {
                lock (_gate)
                {
                    EnsureOpen();
                    if (!RunNodeIsCurrent(source, sourceNode) || !GraphState.SameConfiguration(sourceNode.Node.Configuration, sourceNode.Configuration))
                        throw new InvalidOperationException("生产结果安装前节点已变化。 ");
                    lock (GlobalVariable.ValueGate)
                    {
                        if (writes.Count > 0) _ = checked(ChangeRevision + 1);
                        foreach (var write in writes)
                            if (!RunVariableIsCurrent(write.Capture) || write.Capture.Actual.ValueRevision != write.Prior.Revision)
                                throw new InvalidOperationException("生产值安装前变量或当前值已变化。 ");
                        _publishedRuntimeResults.TryGetValue(source, out previous);
                        var priorRevision = ChangeRevision;
                        try
                        {
                            foreach (var write in writes)
                                write.Capture.Actual.ApplyValueRaw(new GlobalVariable.ValueState(write.Value, checked(write.Prior.Revision + 1)));
                            _publishedRuntimeResults[source] = next;
                            if (writes.Count > 0)
                            {
                                var changes = writes.Select(write => new VariableChange(write.Capture.Actual, VariableChangeKind.Updated,
                                    write.Capture.Actual.Id, null, true)).ToArray();
                                var change = CreateCommittedChange(DocumentChangeOrigin.RuntimeCurrentValues,
                                    Array.Empty<NodeChange>(), Array.Empty<RemovedNode>(), Array.Empty<ConnectionChange>(), changes,
                                    Array.Empty<ScopedNodeIdentity>());
                                post = EnqueueRuntimeChange(change);
                            }
                            if (result.ResultRecords.Count != 0)
                                _resultHistory.GetOrCreateValue(sourceNode.Node).Append(result.ResultRecords, result.CompletedAt);
                            installed = true;
                        }
                        catch
                        {
                            foreach (var write in writes) write.Capture.Actual.ApplyValueRaw(write.Prior);
                            ChangeRevision = priorRevision;
                            if (previous is null) _publishedRuntimeResults.Remove(source); else _publishedRuntimeResults[source] = previous;
                            throw;
                        }
                    }
                }
            })) throw new InvalidOperationException("生产来源身份已退役，不能安装变量值。 ");
        }
        finally { if (!installed) next.Dispose(); }
        try { previous?.Dispose(); } catch (Exception error) { LastRuntimeReleaseError = error; }
        if (post) PostRuntimeChanges();
        PostResultAvailable(source, result.Identity);
    }

    private bool RunVariableIsCurrent(RunVariableCapture captured) =>
        Document.GlobalVariables.Count(variable => variable.Id == captured.Opening.Id) == 1 &&
        Document.GlobalVariables.Count(variable => ReferenceEquals(variable, captured.Actual)) == 1 &&
        VariableConfigurationSignature(captured.Actual) == VariableConfigurationSignature(captured.Opening);
}

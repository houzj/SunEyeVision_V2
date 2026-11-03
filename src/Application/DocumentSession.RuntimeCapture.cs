using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public sealed record WorkflowImageInput(IReadOnlyImage Image, IDisposable Lease);

public sealed partial class DocumentSession
{
    /// <summary>Reserve one real root and freeze all selected workflows, values and input ownership together.</summary>
    public DocumentRunBatch CaptureRunBatch(IEnumerable<string> workflowIds,
        IReadOnlyDictionary<ScopedNodeIdentity, IHostInputPreparer>? hostInputs = null,
        IReadOnlyDictionary<string, WorkflowImageInput>? workflowImages = null, IHostInputFactory? hostFactory = null)
    {
        ArgumentNullException.ThrowIfNull(workflowIds);
        EnsureOwnerCanEdit();
        var requested = workflowIds.ToArray();
        if (requested.Length == 0 || requested.Any(string.IsNullOrWhiteSpace) || requested.Distinct(StringComparer.Ordinal).Count() != requested.Length)
            throw new ArgumentException("运行流程必须非空且身份唯一。", nameof(workflowIds));
        var hosts = (hostInputs ?? new Dictionary<ScopedNodeIdentity, IHostInputPreparer>()).ToList();
        var createdHosts = new List<IHostInputPreparer>();
        var images = (workflowImages ?? new Dictionary<string, WorkflowImageInput>()).ToArray();
        if (hosts.Any(pair => pair.Key is null || pair.Value is null) || images.Any(pair => pair.Value is null || pair.Value.Image is null || pair.Value.Lease is null))
            throw new ArgumentException("运行输入缺少实际身份或资源租约。 ");
        if (hosts.Select(pair => pair.Key.WorkflowId).Distinct(StringComparer.Ordinal).Count() > 1 &&
            hosts.GroupBy(pair => pair.Value, ReferenceEqualityComparer.Instance).Any(group => group.Select(pair => pair.Key.WorkflowId).Distinct(StringComparer.Ordinal).Count() > 1))
            throw new ArgumentException("不同流程不能共同拥有同一个宿主输入准备器。", nameof(hostInputs));
        if (images.Select(pair => pair.Value.Lease).Distinct(ReferenceEqualityComparer.Instance).Count() != images.Length ||
            images.Any(pair => hosts.Any(host => ReferenceEquals(host.Value, pair.Value.Lease))))
            throw new ArgumentException("每个流程图像租约必须具有唯一所有者。", nameof(workflowImages));
        var reservation = new ExecutionRunBatch(_owner, _directory);
        ExecutionVariableValues? variableValues = null;
        try
        {
            lock (_gate)
            {
                EnsureOwnerCanEdit(); InitializeRuntimeNotifications(); _preparing = true;
                try
                {
                    var flows = Document.Workflows.ToArray();
                    if (flows.Select(flow => flow.Id).Distinct(StringComparer.Ordinal).Count() != flows.Length)
                        throw new InvalidOperationException("方案流程身份不唯一。 ");
                    var selected = requested.Select(id => flows.SingleOrDefault(flow => flow.Id == id && !flow.IsSubroutine)
                        ?? throw new InvalidOperationException("运行流程不存在或不属于可执行根流程：" + id)).ToArray();
                    if (hosts.Any(pair => !requested.Contains(pair.Key.WorkflowId, StringComparer.Ordinal)) ||
                        images.Any(pair => !requested.Contains(pair.Key, StringComparer.Ordinal)))
                        throw new ArgumentException("运行输入属于未选择的流程。 ");
                    var states = flows.ToDictionary(flow => flow, GraphState.Capture,
                        (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance);
                    var actualVariables = Document.GlobalVariables.ToArray();
                    GlobalVariable[] definitions;
                    lock (GlobalVariable.ValueGate) definitions = GlobalVariable.CaptureDefinitions(actualVariables).ToArray();
                    var dependencies = GraphDependencySnapshot.Create(Document, states, definitions, AdmitConfiguration);
                    var nodeCaptures = new Dictionary<ScopedNodeIdentity, RunNodeCapture>();
                    var snapshots = new Dictionary<string, ExecutionWorkflowSnapshot>(StringComparer.Ordinal);
                    foreach (var flow in selected)
                    {
                        var scope = OpenScope(flow.Id);
                        var nodes = new List<ExecutionWorkflowNode>();
                        foreach (var item in states[flow].Nodes)
                        {
                            var view = RequireNode(scope, item.Node.Id);
                            if (!ReferenceEquals(view.Node, item.Node) || !ReferenceEquals(item.Configuration, view.Node.Configuration))
                                throw new InvalidOperationException("运行捕获期间节点实例或配置已改变。 ");
                            var hostKey = new ScopedNodeIdentity(flow.Id, item.Node.Id);
                            if (item.Enabled && hostFactory is not null && view.Configuration.HostDeclarations.HostInputs is not null && !hosts.Any(pair => pair.Key == hostKey))
                            {
                                var context = CaptureHostInputContextCore(scope, view) ?? throw new InvalidOperationException("宿主声明已变化。");
                                var prepared = hostFactory.Create(context);
                                if (prepared is not null)
                                {
                                    if (hosts.Any(pair => ReferenceEquals(pair.Value, prepared)))
                                        throw new InvalidOperationException("宿主输入准备器不能由多个节点共享。");
                                    createdHosts.Add(prepared);
                                    hosts.Add(new(hostKey, prepared));
                                }
                            }
                            var snapshot = IssueExecutionNode(view);
                            nodeCaptures.Add(new(flow.Id, item.Node.Id), new(flow, item.Node, view.Configuration.Configuration, view.Configuration.ConfigurationIdentity));
                            nodes.Add(new(snapshot, item.Enabled));
                        }
                        var predecessors = states[flow].Nodes.ToDictionary(item => item.Node.Id,
                            item => (IReadOnlyCollection<string>)dependencies.GetPredecessors(new(flow.Id, item.Node.Id))
                                .Where(source => source.WorkflowId == flow.Id).Select(source => source.NodeId).ToArray(), StringComparer.Ordinal);
                        var control = states[flow].Nodes.ToDictionary(item => item.Node.Id,
                            item => (IReadOnlyCollection<string>)states[flow].Edges.Where(edge => edge.TargetNodeId == item.Node.Id)
                                .Select(edge => edge.SourceNodeId).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
                        var edges = states[flow].Edges.Select(edge => new ExecutionControlEdge(edge.Edge.Id, edge.SourceNodeId, edge.TargetNodeId)).ToArray();
                        snapshots.Add(flow.Id, new ExecutionWorkflowSnapshot(flow.Id, ChangeRevision, _directory, _owner, nodes, predecessors, control, edges));
                    }
                    if (hosts.Any(pair => !nodeCaptures.ContainsKey(pair.Key))) throw new ArgumentException("宿主输入不属于所选流程的实际节点。", nameof(hostInputs));
                    var capturedVariables = CaptureRuntimeVariables(flows, definitions);
                    variableValues = capturedVariables.Values;
                    var producers = capturedVariables.Producers;
                    var acceptance = CreateRunParameterAcceptance(nodeCaptures);
                    var variableCaptures = actualVariables.Select((variable, index) => new RunVariableCapture(variable, definitions[index]))
                        .ToDictionary(item => item.Opening.Id, StringComparer.Ordinal);
                    var batchVariables = variableValues;
                    var producerSnapshot = Array.AsReadOnly(producers.ToArray());
                    var requests = selected.ToDictionary(flow => flow.Id, flow =>
                    {
                        var nodeInputs = hosts.Where(pair => pair.Key.WorkflowId == flow.Id).ToDictionary(pair => pair.Key.NodeId, pair => pair.Value, StringComparer.Ordinal);
                        var image = images.SingleOrDefault(pair => pair.Key == flow.Id).Value;
                        return new ExecutionRunRequest(snapshots[flow.Id], nodeInputs, acceptance, batchVariables, producerSnapshot, image?.Image, image?.Lease,
                            (node, result) => { var key = new ScopedNodeIdentity(node.WorkflowId, node.NodeId);
                                AcceptRunNodeResult(key, nodeCaptures[key], result, producerSnapshot, batchVariables, variableCaptures); });
                    }, StringComparer.Ordinal);
                    var order = OrderRunWorkflows(requested, dependencies.GetEdges());
                    EnsureOpen();
                    if (Document.Id != _documentId || flows.Length != Document.Workflows.Count ||
                        flows.Where((flow, index) => !ReferenceEquals(flow, Document.Workflows[index])).Any() ||
                        states.Any(pair => !pair.Value.MatchesCurrent(pair.Key)) ||
                        actualVariables.Length != Document.GlobalVariables.Count ||
                        actualVariables.Where((variable, index) => !ReferenceEquals(variable, Document.GlobalVariables[index])).Any())
                        throw new InvalidOperationException("运行冻结期间正式方案已改变。 ");
                    lock (GlobalVariable.ValueGate)
                    {
                        var latest = GlobalVariable.CaptureDefinitions(actualVariables).ToArray();
                        if (!latest.Select(VariableConfigurationSignature).SequenceEqual(definitions.Select(VariableConfigurationSignature)) ||
                            latest.Where((variable, index) => variable.ValueRevision != definitions[index].ValueRevision).Any())
                            throw new InvalidOperationException("运行冻结期间变量配置或当前值已改变。 ");
                    }
                    return new DocumentRunBatch(this, reservation, variableValues, requests, order);
                }
                finally { _preparing = false; }
            }
        }
        catch (Exception failure)
        {
            var errors = new List<Exception> { failure };
            try { variableValues?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { reservation.Dispose(); } catch (Exception error) { errors.Add(error); }
            foreach (var host in createdHosts) try { host.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 1) throw new AggregateException("运行捕获和内部资源释放均失败。", errors);
            throw;
        }
    }

    private static IReadOnlyList<string> OrderRunWorkflows(IReadOnlyList<string> requested,
        IReadOnlyList<(ScopedNodeIdentity Source, ScopedNodeIdentity Target)> edges)
    {
        var selected = requested.ToHashSet(StringComparer.Ordinal);
        var predecessors = requested.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var (source, target) in edges)
            if (source.WorkflowId != target.WorkflowId && selected.Contains(source.WorkflowId) && selected.Contains(target.WorkflowId))
                predecessors[target.WorkflowId].Add(source.WorkflowId);
        var order = new List<string>();
        while (predecessors.Count > 0)
        {
            var next = requested.FirstOrDefault(id => predecessors.TryGetValue(id, out var previous) && previous.Count == 0)
                ?? throw new InvalidOperationException("所选流程之间存在不可排序的跨流程依赖。 ");
            order.Add(next); predecessors.Remove(next);
            foreach (var remaining in predecessors.Values) remaining.Remove(next);
        }
        return Array.AsReadOnly(order.ToArray());
    }
}

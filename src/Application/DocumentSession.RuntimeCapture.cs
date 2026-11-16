using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

public sealed record WorkflowImageInput(IReadOnlyImage Image, IDisposable Lease);

public sealed partial class DocumentSession
{
    /// <summary>Reserve one real root and freeze all selected workflows, values and input ownership together.</summary>
    public DocumentRunBatch CaptureRunBatch(IEnumerable<string> workflowIds,
        IReadOnlyDictionary<ScopedNodeIdentity, IHostInputPreparer>? hostInputs = null,
        IReadOnlyDictionary<string, WorkflowImageInput>? workflowImages = null, IHostInputFactory? hostFactory = null)
        => CaptureRunBatchCore(workflowIds, hostInputs, workflowImages, hostFactory, null);

    private DocumentRunBatch CaptureRunBatchCore(IEnumerable<string> workflowIds,
        IReadOnlyDictionary<ScopedNodeIdentity, IHostInputPreparer>? hostInputs,
        IReadOnlyDictionary<string, WorkflowImageInput>? workflowImages, IHostInputFactory? hostFactory,
        ExecutionRunBatch? existingReservation)
    {
        ArgumentNullException.ThrowIfNull(workflowIds);
        EnsureOwnerCanEdit();
        var requested = workflowIds.ToArray();
        if (requested.Length == 0 || requested.Any(string.IsNullOrWhiteSpace) || requested.Distinct(StringComparer.Ordinal).Count() != requested.Length)
            throw new ArgumentException("运行流程必须非空且身份唯一。", nameof(workflowIds));
        var hosts = (hostInputs ?? new Dictionary<ScopedNodeIdentity, IHostInputPreparer>()).ToList();
        var createdHosts = new List<IHostInputPreparer>();
        var hostGenerations = new Dictionary<ScopedNodeIdentity, Guid>();
        var images = (workflowImages ?? new Dictionary<string, WorkflowImageInput>()).ToArray();
        if (hosts.Any(pair => pair.Key is null || pair.Value is null) || images.Any(pair => pair.Value is null || pair.Value.Image is null || pair.Value.Lease is null))
            throw new ArgumentException("运行输入缺少实际身份或资源租约。 ");
        if (hosts.Select(pair => pair.Key.WorkflowId).Distinct(StringComparer.Ordinal).Count() > 1 &&
            hosts.GroupBy(pair => pair.Value, ReferenceEqualityComparer.Instance).Any(group => group.Select(pair => pair.Key.WorkflowId).Distinct(StringComparer.Ordinal).Count() > 1))
            throw new ArgumentException("不同流程不能共同拥有同一个宿主输入准备器。", nameof(hostInputs));
        if (images.Select(pair => pair.Value.Lease).Distinct(ReferenceEqualityComparer.Instance).Count() != images.Length ||
            images.Any(pair => hosts.Any(host => ReferenceEquals(host.Value, pair.Value.Lease))))
            throw new ArgumentException("每个流程图像租约必须具有唯一所有者。", nameof(workflowImages));
        var reservation = existingReservation ?? new ExecutionRunBatch(_owner, _directory);
        var ownsReservation = existingReservation is null;
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
                    var actualVariables = Document.GlobalVariables.ToArray();
                    GlobalVariable[] definitions;
                    lock (GlobalVariable.ValueGate) definitions = GlobalVariable.CaptureDefinitions(actualVariables).ToArray();
                    var plan = GetOrBuildExecutionPlan(flows, definitions);
                    var workflowPlans = plan.Workflows.ToDictionary(item => item.Workflow,
                        (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance);
                    var nodeCaptures = new Dictionary<ScopedNodeIdentity, RunNodeCapture>();
                    var snapshots = new Dictionary<string, ExecutionWorkflowSnapshot>(StringComparer.Ordinal);
                    foreach (var flow in selected)
                    {
                        var workflowPlan = workflowPlans[flow];
                        var scope = OpenScope(flow.Id);
                        var views = new List<(ExecutionPlanNode Plan, AdmittedNodeView View)>();
                        foreach (var item in workflowPlan.Nodes)
                        {
                            var view = RequireNode(scope, item.Node.Id);
                            if (!ReferenceEquals(view.Node, item.Node) || view.Node.IsEnabled != item.Enabled)
                                throw new InvalidOperationException("运行捕获期间节点实例或图结构已改变。 ");
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
                                    hostGenerations.Add(hostKey, context.NodeGeneration);
                                    hosts.Add(new(hostKey, prepared));
                                }
                            }
                            views.Add((item, view));
                        }
                        var cached = workflowPlan.CachedSnapshot;
                        var canReuse = cached is not null && cached.Nodes.Count == views.Count &&
                            views.Select((pair, index) => (pair, index)).All(item =>
                                item.pair.View.Node.Id == cached!.Nodes[item.index].Node.NodeId &&
                                ReferenceEquals(item.pair.View.Configuration.Configuration, cached.Nodes[item.index].Node.Configuration) &&
                                item.pair.Plan.Enabled == cached.Nodes[item.index].IsEnabled);
                        ExecutionWorkflowSnapshot runSnapshot;
                        if (canReuse)
                        {
                            runSnapshot = cached!.WithDocumentRevision(ChangeRevision);
                        }
                        else
                        {
                            var nodes = views.Select(pair => new ExecutionWorkflowNode(IssueExecutionNode(pair.View), pair.Plan.Enabled)).ToArray();
                            // A control edge addresses a declared control output. An unknown control
                            // port must fail here with node and port named, instead of entering a
                            // branch that can never be taken; a data port set is not re-validated
                            // because it was already admitted with the configuration fingerprint.
                            var byNode = nodes.ToDictionary(item => item.Node.NodeId, StringComparer.Ordinal);
                            foreach (var edge in workflowPlan.Edges)
                            {
                                if (!edge.Edge.IsControlEdge) continue;
                                if (!byNode.TryGetValue(edge.Edge.SourceNodeId, out var source))
                                    throw new InvalidOperationException($"控制连线 {edge.Edge.Id} 的源节点不在本次执行图中。");
                                var outputId = edge.Edge.ControlOutputId!;
                                if (!source.Node.Schema.ControlOutputs.Any(output => output.Id == outputId))
                                    throw new InvalidOperationException(
                                        $"控制连线 {edge.Edge.Id} 引用了工具未声明的控制输出 {edge.Edge.SourcePort}（节点 {edge.Edge.SourceNodeId}）。");
                            }
                            var predecessors = workflowPlan.Nodes.ToDictionary(item => item.Node.Id,
                                item => (IReadOnlyCollection<string>)plan.Dependencies.GetPredecessors(new(flow.Id, item.Node.Id))
                                    .Where(source => source.WorkflowId == flow.Id).Select(source => source.NodeId).ToArray(), StringComparer.Ordinal);
                            var control = workflowPlan.Nodes.ToDictionary(item => item.Node.Id,
                                item => (IReadOnlyCollection<string>)workflowPlan.Edges.Where(edge => edge.Edge.TargetNodeId == item.Node.Id)
                                    .Select(edge => edge.Edge.SourceNodeId).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
                            var edges = workflowPlan.Edges.Select(edge => new ExecutionControlEdge(edge.Edge.Id, edge.Edge.SourceNodeId, edge.Edge.TargetNodeId, edge.Edge.SourcePort)).ToArray();
                            runSnapshot = new ExecutionWorkflowSnapshot(flow.Id, ChangeRevision, _directory, _owner, nodes, predecessors, control, edges);
                            workflowPlan.CachedSnapshot = runSnapshot;
                        }
                        foreach (var pair in views)
                            nodeCaptures.Add(new(flow.Id, pair.Plan.Node.Id),
                                new(flow, pair.Plan.Node, pair.View.Configuration.Configuration, pair.View.Configuration.ConfigurationIdentity));
                        snapshots.Add(flow.Id, runSnapshot);
                    }
                    if (hosts.Any(pair => !nodeCaptures.ContainsKey(pair.Key))) throw new ArgumentException("宿主输入不属于所选流程的实际节点。", nameof(hostInputs));
                    var capturedVariables = CaptureRuntimeVariables(flows, definitions,
                        ReferencedVariableImages(selected.SelectMany(flow => snapshots[flow.Id].Nodes)));
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
                    var order = OrderRunWorkflows(requested, plan.Dependencies.GetEdges());
                    EnsureOpen();
                    if (Document.Id != _documentId || flows.Length != Document.Workflows.Count ||
                        flows.Where((flow, index) => !ReferenceEquals(flow, Document.Workflows[index])).Any() ||
                        !plan.Matches(Document) ||
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
                    return new DocumentRunBatch(this, reservation, variableValues, requests, order, ownsReservation, hostGenerations, _executionEngine, _executionServer);
                }
                finally { _preparing = false; }
            }
        }
        catch (Exception failure)
        {
            var errors = new List<Exception> { failure };
            try { variableValues?.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (ownsReservation) try { reservation.Dispose(); } catch (Exception error) { errors.Add(error); }
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

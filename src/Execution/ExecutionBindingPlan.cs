using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>Compiles all node bindings and output demand for one actual execution scope.</summary>
internal sealed class ExecutionBindingPlan
{
    internal sealed record NodePlan(IReadOnlyDictionary<string, BindingPlan> References,
        IReadOnlyDictionary<string, VariableBindingPlan> Variables, IReadOnlyList<OutputValueRequest> Requests,
        IReadOnlyList<Diagnostic> Issues);
    private readonly IReadOnlyDictionary<string, NodePlan> _nodes;
    internal ExecutionBindingPlan(ExecutionWorkflowSnapshot workflow, Guid executionScope,
        ExecutionVariableValues? variables, IEnumerable<OutputReference> globalProducerSources, Guid? bindingOwnerId = null,
        IReadOnlyDictionary<BindingNodeId, BindingSource>? publishedSources = null)
    {
        ArgumentNullException.ThrowIfNull(workflow); ArgumentNullException.ThrowIfNull(globalProducerSources);
        if (executionScope == Guid.Empty) throw new ArgumentException("运行作用域身份为空。", nameof(executionScope));
        var byId = workflow.Nodes.ToDictionary(item => item.Node.NodeId, item => item.Node, StringComparer.Ordinal);
        var sources = publishedSources is null ? byId.Values.Select(node => new BindingSource(new(workflow.WorkflowId, node.NodeId), executionScope,
            new ToolSchema(Array.Empty<Port>(), node.Schema.Outputs, outputDisplay: node.Schema.OutputDisplay), 0)).ToArray()
            : publishedSources.Values.Where(source => source.Node.ScopeId == workflow.WorkflowId).ToArray();
        var visible = sources.Select(source => source.Node).ToArray();
        var predecessors = workflow.Predecessors.Select(pair => new KeyValuePair<BindingNodeId, IReadOnlyList<BindingNodeId>>(
            new(workflow.WorkflowId, pair.Key), pair.Value.Select(id => new BindingNodeId(workflow.WorkflowId, id)).ToArray())).ToArray();
        var variablesById = (variables?.Sources ?? Array.Empty<BindingVariableSource>()).ToDictionary(
            source => source.Reference.VariableId, source => new Port(source.Reference.VariableId, source.Reference.Type,
                Storage: source.Reference.Storage), StringComparer.Ordinal);
        var references = new Dictionary<string, Dictionary<string, BindingPlan>>(StringComparer.Ordinal);
        var variablePlans = new Dictionary<string, Dictionary<string, VariableBindingPlan>>(StringComparer.Ordinal);
        var issues = new Dictionary<string, List<Diagnostic>>(StringComparer.Ordinal);
        var service = new ParameterBindingService();
        foreach (var item in workflow.Nodes)
        {
            var node = item.Node;
            var compiled = new Dictionary<string, BindingPlan>(StringComparer.Ordinal);
            var variable = new Dictionary<string, VariableBindingPlan>(StringComparer.Ordinal);
            var errors = new List<Diagnostic>();
            var scope = new BindingScopeSnapshot(new(workflow.WorkflowId, node.NodeId), bindingOwnerId ?? workflow.Owner.Id,
                workflow.DocumentRevision, sources, visible, predecessors, variables?.Sources);
            var manual = scope.WithReferenceQualification(reference => ValidateManual(workflow, byId, node.NodeId, reference));
            var numeric = node.SettingsSchema.Inputs.Where(port => port.Type.Shape == ValueShape.Scalar &&
                port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64).Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var (id, setting) in node.CaptureExecutionFields())
            {
                try
                {
                    var target = node.BindingTargets.Single(port => port.Id == id);
                    var behavior = node.ReferenceBehaviors.TryGetValue(id, out var declared) ? declared : ParameterReferenceBehavior.StrictExactlyOne;
                    var actualScope = numeric.Contains(id) ? manual : scope;
                    if (setting is ReferenceSetting reference)
                    {
                        if (!string.IsNullOrEmpty(reference.RebindingReason)) throw new ArgumentException(reference.RebindingReason);
                        if (ReferenceOwnershipError(workflow, node, id, reference) is { } ownershipError)
                            throw new ArgumentException(ownershipError);
                        var result = service.CompileBinding(reference.Reference, target, actualScope, reference.Conversion, behavior);
                        compiled.Add(id, result.Plan ?? throw new ArgumentException(result.Diagnostic?.Message ?? "引用没有就绪。"));
                    }
                    else if (setting is VariableSetting variableSetting)
                    {
                        if (variables is null) throw new ArgumentException("本次运行没有变量快照。");
                        var plan = VariableBindingCompiler.Compile(variableSetting, target, variables.SolutionId, variablesById, actualScope, behavior);
                        variable.Add(id, plan);
                        if (plan.Index is { } index) compiled.Add(id, index);
                    }
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException)
                { errors.Add(new Diagnostic("execution-binding", error.Message, id)); }
            }
            references.Add(node.NodeId, compiled); variablePlans.Add(node.NodeId, variable); issues.Add(node.NodeId, errors);
        }
        var demand = byId.Keys.ToDictionary(id => id, _ => new List<OutputValueRequest>(), StringComparer.Ordinal);
        foreach (var item in workflow.Nodes.Where(item => item.IsEnabled))
            foreach (var plan in references[item.Node.NodeId].Values) AddDemand(plan.Source);
        foreach (var producer in globalProducerSources)
            if ((producer.ScopeId ?? workflow.WorkflowId) == workflow.WorkflowId && demand.TryGetValue(producer.NodeId, out var list))
                list.Add(new(producer.PortId, producer.MemberFieldPath));
        _nodes = new ReadOnlyDictionary<string, NodePlan>(byId.Keys.ToDictionary(id => id, id => new NodePlan(
            new ReadOnlyDictionary<string, BindingPlan>(references[id]),
            new ReadOnlyDictionary<string, VariableBindingPlan>(variablePlans[id]),
            Array.AsReadOnly(demand[id].DistinctBy(request => System.Text.Json.JsonSerializer.Serialize(new { request.PortId, request.FieldPath })).ToArray()),
            Array.AsReadOnly(issues[id].ToArray())), StringComparer.Ordinal));
        void AddDemand(BindingReferencePlan plan)
        {
            if (plan.Source.Node.ScopeId == workflow.WorkflowId && demand.TryGetValue(plan.Source.Node.NodeId, out var list))
                list.Add(new(plan.Reference.PortId, plan.Reference.MemberFieldPath));
            if (plan.Index is { } index) AddDemand(index);
        }
    }
    internal NodePlan ForNode(string nodeId) => _nodes[nodeId];

    private static Diagnostic? ValidateManual(ExecutionWorkflowSnapshot workflow,
        IReadOnlyDictionary<string, ExecutionNodeSnapshot> nodes, string consumer, OutputReference reference)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(); pending.Push(consumer);
        while (pending.TryPop(out var node))
            foreach (var source in workflow.ControlPredecessors[node]) if (visited.Add(source)) pending.Push(source);
        visited.Remove(consumer);
        if ((reference.ScopeId ?? workflow.WorkflowId) != workflow.WorkflowId || !visited.Contains(reference.NodeId))
            return new("manual-source-scope", "普通参数来源必须属于当前流程的控制前序。");
        if (!nodes.TryGetValue(reference.NodeId, out var sourceNode) || !sourceNode.ManualReferenceSources.Any(source =>
            source.PortId == reference.PortId && source.MemberPath.SequenceEqual(reference.MemberFieldPath, StringComparer.Ordinal)))
            return new("manual-source-declaration", "来源字段未声明为可手动引用。");
        return null;
    }

    private static string? ReferenceOwnershipError(ExecutionWorkflowSnapshot workflow,
        ExecutionNodeSnapshot node, string fieldId, ReferenceSetting setting)
    {
        if (setting.OriginId is null) return null;
        const string prefix = "connection:";
        if (!setting.OriginId.StartsWith(prefix, StringComparison.Ordinal) || setting.OriginId.Length == prefix.Length)
            return "自动来源归属不是有效的连线标识。";
        var reference = setting.Reference;
        TypeRef? selected = reference.Type;
        foreach (var member in reference.MemberFieldPath)
        {
            if (selected?.Shape != ValueShape.Record) { selected = null; break; }
            selected = selected.Fields.SingleOrDefault(field => field.Id == member)?.Type;
        }
        if (node.Schema.WorkflowImageInputId != fieldId || selected != TypeRef.ResourceOf(ResourceKind.Image)
            || reference.Selection is not BindingSelection.AllItems || setting.Conversion != BindingConversion.Automatic
            || (reference.ScopeId ?? workflow.WorkflowId) != workflow.WorkflowId)
            return "自动图像来源的字段、引用或工作流归属不一致。";
        var edges = workflow.ControlEdges.Where(edge => edge.Id == setting.OriginId[prefix.Length..]).ToArray();
        if (edges.Length != 1 || edges[0].TargetNodeId != node.NodeId || edges[0].SourceNodeId != reference.NodeId)
            return "自动图像来源对应的连线不存在或不属于当前来源与目标节点。";
        return null;
    }
}

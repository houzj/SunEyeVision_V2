using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Workflow;

public sealed record OutputBindingChange(string NodeName, string InputName, string Reason, string NodeId, string? WorkflowId = null);

/// <summary>Design-time formal reconciliation. No controls, no hardware, no scheduler.</summary>
public sealed class OutputBindingChangeService
{
    private Solution? _solution;
    private Dictionary<ScopedNodeIdentity, IReadOnlyList<OutputVariable>?> _contracts = new();
    private Dictionary<(ScopedNodeIdentity Node, string Input), (string Interpretation, string Expression)> _policies = new();
    private Dictionary<string, string> _variableTypes = new();
    public Func<WorkflowNodeBase, IReadOnlyList<OutputVariable>?>? StaticContractReader { get; set; }
    public IReadOnlyList<ScopedNodeIdentity> InvalidatedNodes { get; private set; } = Array.Empty<ScopedNodeIdentity>();
    public IReadOnlyList<string> InvalidatedNodeIds => InvalidatedNodes.Select(n => n.NodeId).Distinct().ToArray();

    public void Reset(Solution? solution)
    {
        StaticContractReader = null; _solution = solution; InvalidatedNodes = Array.Empty<ScopedNodeIdentity>();
        Capture();
    }

    public IReadOnlyList<OutputBindingChange> Commit(string sourceId)
    {
        var owners = (_solution?.Workflows.Where(w => w.Nodes.Any(n => n.Id == sourceId)).Select(w => w.Id)
            ?? Array.Empty<string>()).Concat(_contracts.Keys.Where(k => k.NodeId == sourceId).Select(k => k.WorkflowId)).Distinct().ToArray();
        if (owners.Length > 1) throw new InvalidOperationException("节点ID在多个工作流中重复，请提供工作流作用域。");
        return Reconcile(owners.Length == 1 ? new(owners[0], sourceId) : null, false);
    }
    public IReadOnlyList<OutputBindingChange> Commit(string workflowId, string nodeId) => Reconcile(new(workflowId, nodeId), false);
    public IReadOnlyList<OutputBindingChange> ReconcileAll() => Reconcile(null, true);

    // Candidate validation must compare with the published contracts, not capture the edited
    // candidate as its own baseline. Delegates remain attached only to candidate parameters.
    internal OutputBindingChangeService ForCandidate(Solution candidate) => new()
    {
        _solution = candidate,
        _contracts = new(_contracts),
        _policies = new(_policies),
        _variableTypes = new(_variableTypes),
        StaticContractReader = StaticContractReader
    };

    /// <summary>Read-only validation of the frozen local execution model; never modifies design configuration.</summary>
    public static void EnsureExecutable(Workflow workflow, string? sourceWorkflowId = null)
    {
        var reader = new OutputBindingChangeService();
        var nodes = workflow.Nodes.ToDictionary(n => n.Id);
        foreach (var node in workflow.Nodes)
            foreach (var input in BindingRelationCatalog.Describe(node).Where(i => i.Active && !i.InternalProjection))
            {
                if (node.Parameters.RebindingRequired.TryGetValue(input.Key, out var required)) throw new InvalidOperationException($"{node.Name}.{input.Name} 需要重新绑定：{required}");
                if (node.Parameters.BindingValidationPending.TryGetValue(input.Key, out var pending)) throw new InvalidOperationException($"{node.Name}.{input.Name} 待校验：{pending}");
                if (!BindingInputCatalog.TryReference(input.Read(), nodes.Keys, input.ReferenceOnly, out var id, out var path) || id.StartsWith("__", StringComparison.Ordinal)) continue;
                if (BindingRelationCatalog.SourceWorkflowId(node, input, sourceWorkflowId ?? workflow.Id) != (sourceWorkflowId ?? workflow.Id))
                    throw new InvalidOperationException("绑定来源跨出当前工作流作用域，请使用全局变量。");
                if (!workflow.GetParentNodeIds(node.Id).Contains(id))
                    throw new InvalidOperationException($"{node.Name}.{input.Name}：绑定来源不是画布连接的上游节点，请先建立执行连接或改用全局变量。");
                var validation = nodes.TryGetValue(id, out var upstream) ? BindingContractValidator.Validate(reader.ReadContract(upstream), path, input.AllowedTypes)
                    : new BindingValidation(BindingCompatibility.Incompatible, "来源节点不存在");
                if (validation.State != BindingCompatibility.Compatible) throw new InvalidOperationException($"{node.Name}.{input.Name}：{validation.Reason}");
            }
    }

    public IReadOnlyList<string> GetCandidateNodeIds(string workflowId, string targetId)
    {
        var flow = _solution?.Workflows.FirstOrDefault(w => w.Id == workflowId);
        if (flow == null) return Array.Empty<string>();
        return flow.GetParentNodeIds(targetId).Where(id => id != targetId).Distinct().ToArray();
    }

    public IReadOnlyList<string> GetExistingBindingSourceNodeIds(string workflowId, string targetId)
    {
        var flow = _solution?.Workflows.FirstOrDefault(w => w.Id == workflowId);
        var node = flow?.Nodes.FirstOrDefault(n => n.Id == targetId);
        if (flow == null || node == null) return Array.Empty<string>();
        var ids = flow.Nodes.Select(n => n.Id).ToArray();
        return BindingRelationCatalog.Describe(node).Where(i => i.Active && !i.InternalProjection &&
            BindingRelationCatalog.SourceWorkflowId(node, i, workflowId) == workflowId)
            .Select(i => BindingInputCatalog.TryReference(i.Read(), ids, i.ReferenceOnly, out var id, out _) && ids.Contains(id) ? id : null)
            .OfType<string>().Distinct().ToArray();
    }

    public IReadOnlyList<OutputVariable>? ReadContract(WorkflowNodeBase node)
    {
        var contract = node.Parameters is IOutputContractProvider provider ? provider.GetOutputContract() : StaticContractReader?.Invoke(node);
        if (contract == null && ToolRegistry.ToolExists(node.ToolType))
            contract = ToolRegistry.GetAllPropertyMetadata(node.ToolType).Select(m => new OutputVariable
            {
                Name = m.PropertyName, DisplayName = m.DisplayName, VariableType = SdkTypeVocabulary.ToTypeVocabulary(m.DeclaredType ?? m.PropertyType!) ?? "",
                DeclaredType = m.DeclaredType, IsCollection = m.IsCollection, FixedCount = m.FixedCount, FieldId = m.FieldId, Group = m.GroupName
            }).ToArray();
        return contract?.Select(o => new OutputVariable { Name = o.Name, DisplayName = o.DisplayName, VariableType = o.VariableType,
            Kind = o.Kind, FieldId = o.FieldId, DeclaredType = o.DeclaredType, IsCollection = o.IsCollection, FixedCount = o.FixedCount, Group = o.Group }).ToArray();
    }

    private IReadOnlyList<OutputBindingChange> Reconcile(ScopedNodeIdentity? committed, bool all)
    {
        InvalidatedNodes = Array.Empty<ScopedNodeIdentity>();
        if (_solution == null) return Array.Empty<OutputBindingChange>();
        var nodes = _solution.Workflows.SelectMany(w => w.Nodes.Select(n => (Id: new ScopedNodeIdentity(w.Id, n.Id), Node: n))).ToDictionary(p => p.Id, p => p.Node);
        var current = nodes.ToDictionary(p => p.Key, p => ReadContract(p.Value));
        var relations = BindingRelationCatalog.Describe(_solution);
        var plan = new List<(Action Apply, Action Undo)>(); var notices = new List<OutputBindingChange>();
        var invalidated = new HashSet<ScopedNodeIdentity>(); var invalidVariables = new HashSet<string>();
        var pendingVariables = new Dictionary<string, string>();

        foreach (var variable in _solution.GlobalVariables)
        {
            if (variable.ProducerBinding is not { } producer) continue;
            var owners = nodes.Keys.Where(k => k.NodeId == producer.NodeId && (string.IsNullOrEmpty(producer.WorkflowId) || k.WorkflowId == producer.WorkflowId)).ToArray();
            BindingValidation validation;
            if (owners.Length > 1) validation = new(BindingCompatibility.PendingValidation, "变量生产来源缺少工作流作用域，等待重新确认");
            else if (owners.Length == 0) validation = new(BindingCompatibility.Incompatible, "全局变量生产节点已删除，请重新绑定");
            else
            {
                var oldName = PropertyPathParser.ExtractPropertyName(producer.ResultName.Split('.')[0]);
                var field = producer.SourceFieldId.HasValue ? current[owners[0]]?.FirstOrDefault(o => o.FieldId == producer.SourceFieldId)
                    : current[owners[0]]?.FirstOrDefault(o => o.Name == oldName);
                var path = field == null ? producer.ResultName : field.Name + producer.ResultName[oldName.Length..];
                if (field != null && path != producer.ResultName)
                {
                    var originalPath = producer.ResultName; var originalLabel = producer.ResultDisplayName;
                    plan.Add((() => { producer.ResultName = path; producer.ResultDisplayName = field.DisplayName; },
                        () => { producer.ResultName = originalPath; producer.ResultDisplayName = originalLabel; }));
                }
                validation = producer.SourceFieldId.HasValue && field == null && current[owners[0]] != null
                    ? new(BindingCompatibility.Incompatible, "全局变量原输出身份已删除，请重新绑定")
                    : BindingContractValidator.Validate(current[owners[0]], path,
                        SdkTypeVocabulary.ToSystemType(variable.Type) is { } type ? new[] { type } : Array.Empty<Type>());
            }
            var priorPending = variable.BindingValidationPending;
            if (validation.State == BindingCompatibility.PendingValidation)
            {
                pendingVariables[variable.Id] = validation.Reason;
                plan.Add((() => variable.BindingValidationPending = validation.Reason, () => variable.BindingValidationPending = priorPending));
            }
            else if (validation.State == BindingCompatibility.Compatible)
                plan.Add((() => variable.BindingValidationPending = "", () => variable.BindingValidationPending = priorPending));
            else
            {
                invalidVariables.Add(variable.Id);
                var prior = variable.RebindingReason; var consumers = variable.ConsumerBindings.ToList();
                plan.Add((() => { variable.ProducerBinding = null; variable.RebindingReason = validation.Reason; variable.BindingValidationPending = ""; variable.ConsumerBindings.Clear(); },
                    () => { variable.ProducerBinding = producer; variable.RebindingReason = prior; variable.BindingValidationPending = priorPending; variable.ConsumerBindings = consumers; }));
                foreach (var consumer in consumers)
                {
                    var targets = nodes.Where(p => p.Key.NodeId == consumer.NodeId && (string.IsNullOrEmpty(consumer.WorkflowId) || p.Key.WorkflowId == consumer.WorkflowId)).ToArray();
                    if (targets.Length == 1) Mark(targets[0].Key, targets[0].Value, consumer.ParameterName, "全局变量输入来源已变化，请重新绑定", false);
                }
            }
        }

        foreach (var relation in relations)
        {
            var input = relation.Input; var expression = input.Read();
            var parameters = relation.Node.Parameters;
            // A cleared marker is not a new empty constant. Explicit ordinary constants have their own descriptor.
            if (expression.Length == 0 && input.ValidateLiteral == null)
            {
                if (parameters.BindingValidationPending.ContainsKey(input.Key)) UpdateMarker(parameters.BindingValidationPending, input.Key, null);
                continue;
            }
            if (expression.Length == 0 && parameters.RebindingRequired.ContainsKey(input.Key) &&
                !relation.Node.ParamSettings.Settings.Any(s => s.ParameterName == input.Key && s.BindingType == BindingType.Constant)) continue;
            // Recognition is solution-wide; resolution remains strictly in the consumer's scope.
            var localIds = nodes.Keys.Concat(_contracts.Keys).Select(k => k.NodeId);
            var bound = BindingInputCatalog.TryReference(expression, localIds, input.ReferenceOnly, out var sourceId, out var path);
            BindingValidation validation;
            OutputVariable? oldOutput = null; OutputVariable? nextOutput = null;
            if (bound && sourceId == "__SubroutineInputs__")
            {
                var parents = nodes.Values.Where(n => n.Parameters is SubroutineParameters p &&
                    (string.IsNullOrEmpty(n.SubroutineId) ? p.SubroutineId : n.SubroutineId) == relation.Target.WorkflowId).ToArray();
                var ports = parents.SelectMany(n => ((SubroutineParameters)n.Parameters).Inputs).Where(p => p.Name == PropertyPathParser.ExtractPropertyName(path)).ToArray();
                validation = ports.Length == 0 ? new(BindingCompatibility.PendingValidation, "子程序输入端口契约暂不可用，等待校验")
                    : BindingContractValidator.Validate(ports.Select(p => new OutputVariable { Name = p.Name, VariableType = p.TypeName, IsCollection = false }).ToArray(), path, input.AllowedTypes);
            }
            else if (bound && (sourceId == "__GlobalVars__" || sourceId == "__VariableId__"))
            {
                parameters.BindingSourceIdentities.TryGetValue(input.Key, out var savedVariable);
                var variable = _solution.GlobalVariables.FirstOrDefault(v => savedVariable?.Expression == expression && savedVariable.VariableId != null ? v.Id == savedVariable.VariableId
                    : sourceId == "__VariableId__" ? v.Id == path : v.Name == PropertyPathParser.ExtractPropertyName(path));
                if (variable != null && sourceId == "__GlobalVars__" && savedVariable?.Expression == expression && savedVariable.VariableId != null)
                {
                    var oldName = PropertyPathParser.ExtractPropertyName(path.Split('.')[0]);
                    var updated = sourceId + "." + variable.Name + path[oldName.Length..];
                    if (updated != expression) plan.Add((() => input.Write(updated), () => input.Write(expression)));
                    path = variable.Name + path[oldName.Length..];
                }
                validation = variable == null || invalidVariables.Contains(variable.Id) || !string.IsNullOrEmpty(variable.RebindingReason)
                    ? new(BindingCompatibility.Incompatible, "全局变量不存在或输入来源已失效，请重新绑定")
                    : pendingVariables.TryGetValue(variable.Id, out var pendingVariable) ? new(BindingCompatibility.PendingValidation, pendingVariable)
                    : BindingContractValidator.Validate(new[] { new OutputVariable { Name = "Value", VariableType = variable.Type, IsCollection = SdkTypeVocabulary.ToSystemType(variable.Type)?.IsArray } },
                        sourceId == "__VariableId__" ? "Value" : "Value" + path[variable.Name.Length..], input.AllowedTypes);
                if (validation.State == BindingCompatibility.Compatible && variable != null && !input.AllowsTypeChange &&
                    _variableTypes.TryGetValue(variable.Id, out var oldVariableType) && oldVariableType != variable.Type)
                    validation = new(BindingCompatibility.Incompatible, "全局变量类型已变化，请重新绑定");
            }
            else if (bound)
            {
                var source = new ScopedNodeIdentity(BindingRelationCatalog.SourceWorkflowId(relation.Node, input, relation.Target.WorkflowId), sourceId);
                if (source == relation.Target) throw new InvalidOperationException("输入绑定不能引用节点自身。");
                if (source.WorkflowId != relation.Target.WorkflowId && !input.InternalProjection || !nodes.ContainsKey(source))
                    validation = new(BindingCompatibility.Incompatible, "来源不在当前工作流中；跨流程请使用全局变量，请重新绑定");
                else if (!input.InternalProjection && !_solution.Workflows.First(w => w.Id == relation.Target.WorkflowId)
                    .GetParentNodeIds(relation.Target.NodeId).Contains(source.NodeId))
                    validation = new(BindingCompatibility.Incompatible, "来源不是画布连接的上游节点，请先建立执行连接或改用全局变量");
                else
                {
                    _contracts.TryGetValue(source, out var previous);
                    var root = PropertyPathParser.ExtractPropertyName(path.Split('.')[0]);
                    oldOutput = previous?.FirstOrDefault(o => o.Name == root);
                    nextOutput = current[source]?.FirstOrDefault(o => o.Name == root);
                    parameters.BindingSourceIdentities.TryGetValue(input.Key, out var savedSource);
                    var fieldId = input.ReadFieldId?.Invoke() ?? (savedSource?.Expression == expression && savedSource.WorkflowId == source.WorkflowId ? savedSource.FieldId : null) ?? oldOutput?.FieldId;
                    if (fieldId != null && nextOutput?.FieldId != fieldId)
                    {
                        nextOutput = current[source]?.FirstOrDefault(o => o.FieldId == fieldId);
                        if (nextOutput != null)
                        {
                            var newExpression = sourceId + "." + nextOutput.Name + path[root.Length..];
                            plan.Add((() => input.Write(newExpression), () => input.Write(expression)));
                            path = nextOutput.Name + path[root.Length..];
                        }
                    }
                    validation = fieldId != null && nextOutput == null && current[source] != null
                        ? new(BindingCompatibility.Incompatible, "原输出身份已删除，请重新绑定")
                        : BindingContractValidator.Validate(current[source], path, input.AllowedTypes);
                    if (validation.State == BindingCompatibility.Compatible && !input.AllowsTypeChange && oldOutput != null && nextOutput != null && oldOutput.VariableType != nextOutput.VariableType)
                        validation = new(BindingCompatibility.Incompatible, "上游输出类型已变化，请重新绑定");
                }
            }
            else if (input.ReferenceOnly) validation = new(BindingCompatibility.Incompatible, "绑定来源格式无效，请重新绑定");
            else if (input.ValidateLiteral != null) validation = input.ValidateLiteral(expression) is { } error
                ? new(BindingCompatibility.Incompatible, error) : new(BindingCompatibility.Compatible, "常量适配");
            else continue;

            if (validation.State == BindingCompatibility.Compatible && bound && _policies.TryGetValue((relation.Target, input.Key), out var policy) &&
                policy.Expression == expression && policy.Interpretation != input.InterpretationKey)
                validation = new(BindingCompatibility.Incompatible, "输入解释规则已变化，请重新绑定");
            if (validation.State == BindingCompatibility.PendingValidation)
                Mark(relation.Target, relation.Node, input.Key, validation.Reason, true);
            else if (validation.State == BindingCompatibility.Incompatible)
            {
                if (bound) plan.Add((() => input.Write(""), () => input.Write(expression)));
                foreach (var variable in _solution.GlobalVariables)
                    foreach (var consumer in variable.ConsumerBindings.Where(c => c.NodeId == relation.Target.NodeId &&
                        (string.IsNullOrEmpty(c.WorkflowId) || c.WorkflowId == relation.Target.WorkflowId) &&
                        (c.InputKey != null ? c.InputKey == input.Key : c.ParameterName == input.Key || c.ParameterName == input.Name)).ToArray())
                        plan.Add((() => variable.ConsumerBindings.Remove(consumer), () => variable.ConsumerBindings.Add(consumer)));
                Mark(relation.Target, relation.Node, input.Key, validation.Reason, false);
                UpdateMarker(parameters.BindingValidationPending, input.Key, null);
            }
            else
            {
                UpdateMarker(parameters.BindingValidationPending, input.Key, null);
                if (all || committed == relation.Target) UpdateMarker(parameters.RebindingRequired, input.Key, null);
            }
        }
        var applied = new Stack<Action>();
        try
        {
            foreach (var action in plan) { applied.Push(action.Undo); action.Apply(); }
            SolutionExecutionDependencyGraph.Compile(_solution, checkReadiness: false);
        }
        catch { while (applied.Count > 0) applied.Pop()(); throw; }
        foreach (var pair in current)
            if (_contracts.TryGetValue(pair.Key, out var old) && old != null && pair.Value != null &&
                !old.Select(Signature).SequenceEqual(pair.Value.Select(Signature))) invalidated.Add(pair.Key);
        foreach (var missing in _contracts.Keys.Except(current.Keys)) invalidated.Add(missing);
        bool added;
        do
        {
            added = false;
            foreach (var relation in relations.Where(r => r.Input.Active && !invalidated.Contains(r.Target)))
                if (BindingInputCatalog.TryReference(relation.Input.Read(), nodes.Keys.Where(k => k.WorkflowId == relation.Target.WorkflowId).Select(k => k.NodeId), relation.Input.ReferenceOnly, out var id, out _) &&
                    invalidated.Contains(new(relation.Input.ReadWorkflowId?.Invoke() ?? relation.Target.WorkflowId, id))) added |= invalidated.Add(relation.Target);
        } while (added);
        InvalidatedNodes = invalidated.ToArray();
        foreach (var id in invalidated) if (nodes.TryGetValue(id, out var node)) { node.LastResult = null; node.OutputCache = null; }
        Capture(); return notices.Distinct().ToArray();

        void UpdateMarker(Dictionary<string, string> markers, string key, string? reason)
        {
            var had = markers.TryGetValue(key, out var old);
            if (reason == old || reason == null && !had) return;
            plan.Add((() => { if (reason == null) markers.Remove(key); else markers[key] = reason; },
                () => { if (had) markers[key] = old!; else markers.Remove(key); }));
        }
        void Mark(ScopedNodeIdentity identity, WorkflowNodeBase node, string key, string reason, bool pending)
        {
            UpdateMarker(pending ? node.Parameters.BindingValidationPending : node.Parameters.RebindingRequired, key, reason);
            invalidated.Add(identity); notices.Add(new(node.Name, key, reason, node.Id, identity.WorkflowId));
        }
    }

    private void Capture()
    {
        _contracts = _solution == null ? new() : _solution.Workflows.SelectMany(w => w.Nodes.Select(n => (Id: new ScopedNodeIdentity(w.Id, n.Id), Node: n))).ToDictionary(p => p.Id, p => ReadContract(p.Node));
        _policies = _solution == null ? new() : BindingRelationCatalog.Describe(_solution).ToDictionary(r => (r.Target, r.Input.Key), r => (r.Input.InterpretationKey, r.Input.Read()));
        _variableTypes = _solution?.GlobalVariables.ToDictionary(v => v.Id, v => v.Type) ?? new();
        if (_solution == null) return;
        foreach (var variable in _solution.GlobalVariables)
        {
            if (variable.ProducerBinding is { SourceFieldId: null } producer)
            {
                var matches = _contracts.Where(p => p.Key.NodeId == producer.NodeId && (string.IsNullOrEmpty(producer.WorkflowId) || p.Key.WorkflowId == producer.WorkflowId)).ToArray();
                if (matches.Length == 1) producer.SourceFieldId = matches[0].Value?.FirstOrDefault(o => o.Name == PropertyPathParser.ExtractPropertyName(producer.ResultName.Split('.')[0]))?.FieldId;
            }
            foreach (var consumer in variable.ConsumerBindings)
            {
                var targets = _solution.Workflows.Where(w => string.IsNullOrEmpty(consumer.WorkflowId) || w.Id == consumer.WorkflowId)
                    .SelectMany(w => w.Nodes.Where(n => n.Id == consumer.NodeId).Select(n => (Workflow: w.Id, Node: n))).ToArray();
                if (targets.Length != 1) continue;
                var descriptor = BindingRelationCatalog.Describe(targets[0].Node).FirstOrDefault(i => consumer.InputKey != null ? i.Key == consumer.InputKey : i.Key == consumer.ParameterName || i.Name == consumer.ParameterName);
                if (descriptor == null) continue;
                consumer.WorkflowId = targets[0].Workflow; consumer.InputKey = descriptor.Key;
                if (descriptor.Key.StartsWith("Input:", StringComparison.Ordinal)) consumer.ParameterName = descriptor.Name;
            }
        }
        foreach (var relation in BindingRelationCatalog.Describe(_solution))
        {
            var expression = relation.Input.Read();
            var ids = _contracts.Keys.Where(k => k.WorkflowId == relation.Target.WorkflowId).Select(k => k.NodeId);
            var identities = relation.Node.Parameters.BindingSourceIdentities;
            if (!BindingInputCatalog.TryReference(expression, ids, relation.Input.ReferenceOnly, out var id, out var path)) { identities.Remove(relation.Input.Key); continue; }
            if (identities.TryGetValue(relation.Input.Key, out var prior) && prior.Expression == expression && (prior.FieldId != null || prior.VariableId != null)) continue;
            var scope = relation.Input.ReadWorkflowId?.Invoke() ?? relation.Target.WorkflowId;
            var field = _contracts.GetValueOrDefault(new(scope, id))?.FirstOrDefault(o => o.Name == PropertyPathParser.ExtractPropertyName(path.Split('.')[0]));
            var variable = _solution.GlobalVariables.FirstOrDefault(v => id == "__VariableId__" ? v.Id == path : id == "__GlobalVars__" && v.Name == PropertyPathParser.ExtractPropertyName(path.Split('.')[0]));
            identities[relation.Input.Key] = new(expression, scope, id, relation.Input.ReadFieldId?.Invoke() ?? field?.FieldId, variable?.Id);
        }
    }
    private static string Signature(OutputVariable o) => $"{o.FieldId}/{o.Name}/{o.VariableType}/{o.DeclaredType}/{o.IsCollection}/{o.FixedCount}/{o.Kind}/{o.DisplayName}";
}

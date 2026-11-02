using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Workflow;

/// <summary>
/// 解决方案级依赖快照。
/// 它把每个工作流内部图与“节点结果 → 全局变量 → 目标参数”连接起来，
/// 供未来的 RunAll/批次调度器自动推导跨工作流顺序；不要求用户额外编排。
/// </summary>
public sealed class SolutionExecutionDependencyGraph
{
    private readonly Dictionary<string, HashSet<string>> _predecessors;
    private readonly List<SolutionExecutionDependency> _dependencies;

    private SolutionExecutionDependencyGraph(
        Dictionary<string, HashSet<string>> predecessors,
        List<SolutionExecutionDependency> dependencies)
    {
        _predecessors = predecessors;
        _dependencies = dependencies;
    }

    public IReadOnlyList<SolutionExecutionDependency> Dependencies => _dependencies;

    public IReadOnlyCollection<string> GetPredecessors(string scopedNodeId) =>
        _predecessors.TryGetValue(scopedNodeId, out var predecessors)
            ? predecessors
            : Array.Empty<string>();

    /// <summary>验证整个解决方案（包括跨流程全局变量边）不存在循环。</summary>
    public IReadOnlyList<IReadOnlyList<string>> GetTopologicalLayers(IEnumerable<string> stableScopedNodeOrder)
    {
        var order = stableScopedNodeOrder.Distinct(StringComparer.Ordinal).ToList();
        var orderIndex = order.Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);
        var remaining = _predecessors.ToDictionary(
            pair => pair.Key,
            pair => new HashSet<string>(pair.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
        var layers = new List<IReadOnlyList<string>>();

        while (remaining.Count > 0)
        {
            var ready = remaining.Where(pair => pair.Value.Count == 0)
                .Select(pair => pair.Key)
                .OrderBy(id => orderIndex.TryGetValue(id, out var index) ? index : int.MaxValue)
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToList();
            if (ready.Count == 0)
                throw new InvalidOperationException("解决方案依赖图存在循环，无法开始运行。");

            layers.Add(ready);
            foreach (var id in ready)
                remaining.Remove(id);
            foreach (var predecessors in remaining.Values)
                predecessors.ExceptWith(ready);
        }

        return layers;
    }

    public static string GetScopedNodeId(string workflowId, string nodeId) => $"{workflowId}/{nodeId}";

    public static SolutionExecutionDependencyGraph Compile(Solution solution, bool checkReadiness = true, SunEyeVision.Plugin.Runtime.RuntimeDirectorySnapshot? expectedDirectory = null)
    {
        if (solution == null)
            throw new ArgumentNullException(nameof(solution));
        if(checkReadiness)
        {
            expectedDirectory ??= solution.RuntimeDirectory ?? throw new InvalidOperationException("方案缺少已准入目录。");
            if(!expectedDirectory.IsActive || (solution.RuntimeDirectory is {} actual && !ReferenceEquals(actual,expectedDirectory)))
                throw new InvalidOperationException("方案运行目录代次已变更或退役。");
        }

        var workflows = solution.Workflows ?? new List<Workflow>();
        var nodeLookup = new Dictionary<string, WorkflowNodeBase>(StringComparer.Ordinal);
        var workflowByNodeId = new Dictionary<string, List<Workflow>>(StringComparer.Ordinal);
        var predecessors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var dependencies = new List<SolutionExecutionDependency>();

        foreach (var workflow in workflows)
        {
            foreach (var node in workflow.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.Id))
                    throw new InvalidOperationException($"工作流 {workflow.Name} 存在空节点 ID。 ");

                var scopedId = GetScopedNodeId(workflow.Id, node.Id);
                if (!nodeLookup.TryAdd(scopedId, node))
                    throw new InvalidOperationException($"解决方案中存在重复的作用域节点 ID：{scopedId}。");
                predecessors.Add(scopedId, new HashSet<string>(StringComparer.Ordinal));

                if (!workflowByNodeId.TryGetValue(node.Id, out var owners))
                {
                    owners = new List<Workflow>();
                    workflowByNodeId.Add(node.Id, owners);
                }
                owners.Add(workflow);
            }

            // Local input faults are owned by their consumer, not by the whole solution.
            // Candidate reconciliation must retain unresolved-but-explicit binding edges for cycle checking.
            // Runtime readiness still omits failed inputs so unrelated nodes can execute.
            var localIssues = checkReadiness ? OutputBindingChangeService.GetExecutionInputIssues(workflow, expectedDirectory!, workflow.Id) : null;
            var local = WorkflowExecutionDependencyGraph.Compile(workflow, null, workflow.Id, localIssues,solution.Id);
            foreach (var dependency in local.Dependencies)
            {
                AddDependency(
                    GetScopedNodeId(workflow.Id, dependency.SourceNodeId),
                    GetScopedNodeId(workflow.Id, dependency.TargetNodeId),
                    SolutionDependencyKind.Internal,
                    null,
                    dependency.ParameterName);
            }
        }

        foreach (var variable in solution.GlobalVariables ?? Enumerable.Empty<GlobalVariable>())
        {
            // Invalid producer bindings remain unavailable at execution time; unrelated graphs still compile.
            if (!string.IsNullOrEmpty(variable.RebindingReason) || !string.IsNullOrEmpty(variable.BindingValidationPending))
                continue;
            if (variable.ProducerBinding == null)
                continue;

            var source = variable.ProducerBinding.SourceReference;
            var producer = ResolveNode(source.ScopeId!, source.NodeId,
                $"全局变量 {variable.Name} 的输入来源");
            if (producer == null) continue;

            foreach (var consumer in variable.ConsumerBindings ?? Enumerable.Empty<ConsumerBinding>())
            {
                var target = ResolveNode(consumer.WorkflowId, consumer.NodeId,
                    $"全局变量 {variable.Name} 的目标输出");
                if (target == null) continue;
                var consumerNode=nodeLookup[target];
                if(consumerNode?.RuntimeConfiguration is {} runtime)
                {
                    // These registrations remain in the document. They do not make a sleeping
                    // runtime input an execution dependency; runtime field identities are exact.
                    var key=consumer.InputKey??consumer.ParameterName;
                    // Unknown or ambiguous registration identities retain the existing conservative
                    // dependency. Only one positively identified sleeping field can be omitted.
                    if(runtime.Fields.Declarations.ContainsKey(key)&&!runtime.IsExecutionFieldActive(key))continue;
                }
                AddDependency(producer, target, SolutionDependencyKind.GlobalVariable,
                    variable.Name, consumer.ParameterName);
            }
        }

        foreach(var variable in solution.GlobalVariables.Where(variable=>variable.ProducerBinding is not null
            &&string.IsNullOrEmpty(variable.RebindingReason)&&string.IsNullOrEmpty(variable.BindingValidationPending)))
        {
            var source=variable.ProducerBinding!.SourceReference;
            var producer=ResolveNode(source.ScopeId!,source.NodeId,"变量生产来源");
            if(producer is null)continue;
            foreach(var workflow in workflows)
                foreach(var node in workflow.Nodes.Where(node=>node.RuntimeConfiguration is not null))
                    foreach(var (fieldId,reference) in BindingRelationCatalog.VariableReferences(node,executionOnly:true))
                        if(reference.SolutionId==solution.Id&&reference.VariableId==variable.Id)
                            AddDependency(producer,GetScopedNodeId(workflow.Id,node.Id),SolutionDependencyKind.GlobalVariable,variable.Name,fieldId);
        }
        var graph = new SolutionExecutionDependencyGraph(predecessors, dependencies);
        graph.GetTopologicalLayers(nodeLookup.Keys);
        return graph;

        string? ResolveNode(string workflowId, string nodeId, string role)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                return null;

            if (!string.IsNullOrWhiteSpace(workflowId))
            {
                var scoped = GetScopedNodeId(workflowId, nodeId);
                if (!nodeLookup.ContainsKey(scoped))
                    return null;
                return scoped;
            }

            // 兼容旧方案：旧 ConsumerBinding 未保存工作流 ID。节点 ID 全局唯一时可安全迁移；
            // 若不唯一则拒绝猜测，提示用户重新保存该绑定。
            if (!workflowByNodeId.TryGetValue(nodeId, out var owners) || owners.Count == 0)
                return null;
            if (owners.Count != 1)
                return null;
            return GetScopedNodeId(owners[0].Id, nodeId);
        }

        void AddDependency(string source, string target, SolutionDependencyKind kind, string? variableName, string? parameterName)
        {
            if (string.Equals(source, target, StringComparison.Ordinal))
                throw new InvalidOperationException($"解决方案依赖不能指向自身：{source}。");
            if (predecessors[target].Add(source))
                dependencies.Add(new SolutionExecutionDependency(source, target, kind, variableName, parameterName));
        }
    }
}

public enum SolutionDependencyKind
{
    Internal,
    GlobalVariable
}

public sealed record SolutionExecutionDependency(
    string SourceScopedNodeId,
    string TargetScopedNodeId,
    SolutionDependencyKind Kind,
    string? GlobalVariableName,
    string? ParameterName);

using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow;

/// <summary>
/// 由运行计划编译出的节点依赖图。
/// 除画布连接外，也包含参数绑定引用；后续并发调度只能消费此图，不能只看画布连线。
/// </summary>
public sealed class WorkflowExecutionDependencyGraph
{
    private readonly Dictionary<string, HashSet<string>> _predecessors;
    private readonly List<WorkflowExecutionDependency> _dependencies;

    private WorkflowExecutionDependencyGraph(
        Dictionary<string, HashSet<string>> predecessors,
        List<WorkflowExecutionDependency> dependencies)
    {
        _predecessors = predecessors;
        _dependencies = dependencies;
    }

    public IReadOnlyList<WorkflowExecutionDependency> Dependencies => _dependencies;

    public IReadOnlyCollection<string> GetPredecessors(string nodeId) =>
        _predecessors.TryGetValue(nodeId, out var predecessors)
            ? predecessors
            : Array.Empty<string>();

    /// <summary>以稳定节点顺序生成拓扑层，仅供诊断和后续调度器使用。</summary>
    public IReadOnlyList<IReadOnlyList<string>> GetTopologicalLayers(IEnumerable<string> stableNodeOrder)
    {
        var ordered = stableNodeOrder.Distinct().ToList();
        var orderIndex = ordered
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);
        var remaining = _predecessors.ToDictionary(
            pair => pair.Key,
            pair => new HashSet<string>(pair.Value));
        var layers = new List<IReadOnlyList<string>>();

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(pair => pair.Value.Count == 0)
                .Select(pair => pair.Key)
                .OrderBy(id => orderIndex.TryGetValue(id, out var index) ? index : int.MaxValue)
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToList();

            if (ready.Count == 0)
                throw new InvalidOperationException("执行依赖图存在循环，无法生成拓扑层。");

            layers.Add(ready);
            foreach (var nodeId in ready)
                remaining.Remove(nodeId);
            foreach (var predecessors in remaining.Values)
                predecessors.ExceptWith(ready);
        }

        return layers;
    }

    public static WorkflowExecutionDependencyGraph Compile(Workflow workflow,
        IReadOnlyList<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource>? variables = null, string? sourceWorkflowId = null)
    {
        if (workflow == null)
            throw new ArgumentNullException(nameof(workflow));

        var nodeIds = workflow.Nodes.Select(node => node.Id).ToList();
        if (nodeIds.Any(string.IsNullOrWhiteSpace) || nodeIds.Distinct(StringComparer.Ordinal).Count() != nodeIds.Count)
            throw new InvalidOperationException("工作流存在空节点 ID 或重复节点 ID，无法编译执行依赖图。");

        var knownNodes = new HashSet<string>(nodeIds, StringComparer.Ordinal);
        var predecessors = nodeIds.ToDictionary(
            nodeId => nodeId,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var dependencies = new List<WorkflowExecutionDependency>();

        void AddDependency(string sourceNodeId, string targetNodeId, WorkflowDependencyKind kind, string? parameterName = null)
        {
            if (!knownNodes.Contains(sourceNodeId) || !knownNodes.Contains(targetNodeId))
                throw new InvalidOperationException(
                    $"执行依赖引用了不存在的节点：{sourceNodeId} → {targetNodeId}。");
            if (string.Equals(sourceNodeId, targetNodeId, StringComparison.Ordinal))
                throw new InvalidOperationException($"节点 {targetNodeId} 不能依赖自身。");

            if (predecessors[targetNodeId].Add(sourceNodeId))
                dependencies.Add(new WorkflowExecutionDependency(sourceNodeId, targetNodeId, kind, parameterName));
        }

        foreach (var connection in workflow.Connections)
            AddDependency(connection.SourceNodeId, connection.TargetNodeId, WorkflowDependencyKind.Connection);

        foreach (var node in workflow.Nodes)
        {
            if (node.Parameters is SunEyeVision.Plugin.SDK.Models.Communication.IReceiveVariableInputProvider receive)
                foreach (var id in receive.GetReceiveVariableIds())
                {
                    var variable = variables?.FirstOrDefault(v => v.Id == id && v.HasProducer);
                    if (variable != null && knownNodes.Contains(variable.ProducerNodeId)
                        && (string.IsNullOrEmpty(variable.ProducerWorkflowId) || variable.ProducerWorkflowId == (sourceWorkflowId ?? workflow.Id)))
                        AddDependency(variable.ProducerNodeId, node.Id, WorkflowDependencyKind.ParameterBinding, "ReceiveVariable");
                }
            foreach (var setting in node.ParamSettings?.GetDynamicSettings() ?? Enumerable.Empty<ParamSetting>())
            {
                if (string.IsNullOrWhiteSpace(setting.SourceNodeId) || string.IsNullOrWhiteSpace(setting.SourceProperty))
                    throw new InvalidOperationException(
                        $"节点 {node.Name} 的参数 {setting.ParameterName} 存在不完整的动态绑定。");

                // 全局快照和子程序入口不是当前节点图中的生产节点；它们由对应运行边界提供。
                if (string.Equals(setting.SourceNodeId, "__GlobalVars__", StringComparison.Ordinal) ||
                    string.Equals(setting.SourceNodeId, "__SubroutineInputs__", StringComparison.Ordinal))
                    continue;

                AddDependency(setting.SourceNodeId, node.Id, WorkflowDependencyKind.ParameterBinding, setting.ParameterName);
            }
        }

        var graph = new WorkflowExecutionDependencyGraph(predecessors, dependencies);
        graph.GetTopologicalLayers(nodeIds);
        return graph;
    }
}

public enum WorkflowDependencyKind
{
    Connection,
    ParameterBinding
}

public sealed record WorkflowExecutionDependency(
    string SourceNodeId,
    string TargetNodeId,
    WorkflowDependencyKind Kind,
    string? ParameterName);

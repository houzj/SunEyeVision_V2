using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Models;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 子程序父节点 ID 在上下文变量中的键名
    /// </summary>
    internal static class SubroutineContextKeys
    {
        /// <summary>
        /// 用于在 contextVariables 中传递子程序节点在父工作流中的 NodeId
        /// </summary>
        public const string ParentNodeId = "__SubroutineParentNodeId__";
    }

    /// <summary>
    /// 工作流执行状态
    /// </summary>
    public enum WorkflowExecutionState
    {
        Idle,           // 空闲
        Running,        // 运行中
        Paused,         // 已暂停
        Stopped,        // 已停止
        Completed,      // 已完成
        Error,          // 错误
        Stopping        // 已请求停止，在途工作与清理仍未结束
    }

    /// <summary>
    /// 工作流执行引擎 - 扩展WorkflowEngine以支持工作流控制节点
    /// 简化版本：使用基于拓扑排序的并行组执行
    /// </summary>
    public class WorkflowExecutionEngine
    {
        private readonly WorkflowEngine _workflowEngine;
        private readonly IPluginManager _pluginManager;
        private readonly ILogger _logger;
        private readonly ToolExecutor _toolExecutor;

        // 根运行会话。当前执行器明确只承载一个根运行；并发请求会被拒绝，避免过去
        // `_currentContext` 与 `_cancellationTokenSource` 被后一次运行静默覆盖。
        private readonly object _sessionGate = new();
        private WorkflowRunSession? _activeSession;

        // ★ SSOT 已转移到 WorkflowContext，引擎不再维护重复缓存

        /// <summary>
        /// 当前执行状态
        /// </summary>
        public WorkflowExecutionState CurrentState => _activeSession?.State ?? WorkflowExecutionState.Idle;

        /// <summary>
        /// 当前执行上下文
        /// </summary>
        public WorkflowContext? CurrentContext => _activeSession?.RootContext;

        /// <summary>
        /// 节点执行状态变化事件
        /// </summary>
        public event EventHandler<NodeExecutionStatus>? NodeStatusChanged;

        /// <summary>
        /// 节点完成时发布其结果。订阅者只观察运行事实，不能参与调度或改变执行结果。
        /// </summary>
        public event Action<WorkflowNodeBase, NodeExecutionResult>? NodeExecutionFinished;

        /// <summary>
        /// 执行完成事件
        /// </summary>
        public event EventHandler<ExecutionResult>? ExecutionCompleted;

        /// <summary>
        /// 创建工作流执行引擎
        /// </summary>
        /// <param name="workflowEngine">工作流引擎</param>
        /// <param name="pluginManager">插件管理器</param>
        /// <param name="logger">日志器</param>
        /// <param name="toolExecutor">工具执行器（可选，不提供时自动创建）</param>
        public WorkflowExecutionEngine(
            WorkflowEngine workflowEngine,
            IPluginManager pluginManager,
            ILogger logger,
            ToolExecutor? toolExecutor = null)
        {
            _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _toolExecutor = toolExecutor ?? new ToolExecutor(new ParameterResolver());
            // ★ SSOT 统一由 WorkflowContext 管理，引擎不再维护独立 DSQS
        }

        #region 同步执行

        /// <summary>
        /// 使用基于拓扑排序的并行组执行工作流（简化方案）
        /// </summary>
        private async Task<ExecutionResult> ExecuteWorkflowWithParallelGroups(
            WorkflowRunSession session,
            Workflow workflow,
            WorkflowContext context,
            WorkflowExecutionDependencyGraph dependencyGraph,
            IReadOnlyDictionary<string, ToolExecutionProfile> nodeExecutionProfiles)
        {
            return await ExecuteWorkflowWithReadyQueue(
                session, workflow, context, dependencyGraph, nodeExecutionProfiles);

#pragma warning disable CS0162 // 保留旧分层实现，便于运行回归期间对照；实际入口已切到全局就绪队列。
            _logger.LogInfo($"[DEBUG ExecuteWorkflowWithParallelGroups] workflow.Id={workflow.Id}, workflow.Name={workflow.Name}, Nodes.Count={workflow.Nodes.Count}, Connections.Count={workflow.Connections.Count}");

            var result = new ExecutionResult();
            int totalExecutedNodes = 0;

            _logger.LogInfo($"========== 使用基于执行链的并行组执行模式 ==========");

            // 当前仍按组串行执行，但组必须来自完整依赖图：参数绑定也会形成前驱关系。
            // 不能只用画布连线，否则“未画线的结果参数绑定”会被错误放入同一候选组。
            var parallelGroups = dependencyGraph
                .GetTopologicalLayers(workflow.Nodes.Select(node => node.Id))
                .Select(layer => layer.ToList())
                .ToList();
            _logger.LogInfo($"获取到 {parallelGroups.Count} 个并行执行组");

            // 显示执行组信息
            for (int i = 0; i < parallelGroups.Count; i++)
            {
                var group = parallelGroups[i];
                var groupInfo = $"组{i + 1}: {string.Join(", ", group.Select(id => 
                {
                    var node = workflow.Nodes.FirstOrDefault(n => n.Id == id);
                    return node != null ? $"{node.Name}({id})" : id;
                }))}";
                _logger.LogInfo($"  {groupInfo}");
            }
            _logger.LogInfo($"=====================================================");

            // 按组顺序执行
            // ★ 剪枝集合：被跳过的节点（条件分支非命中分支 / 未命中整条分支）。
            //   分组仍用拓扑 BFS 分层（谁先谁后不变），条件分支只负责给不该跑的节点"贴标签"，
            //   执行时跳过贴标签节点。剪枝传播规则：一个节点被剪 ⟺ 其所有前驱都被剪。
            var prunedNodeIds = new HashSet<string>();
            // 条件分支的目标节点（命中时保留其整条上游链）
            HashSet<string> protectedNodeIds = new();

            for (int groupIndex = 0; groupIndex < parallelGroups.Count; groupIndex++)
            {
                await session.WaitIfPausedAsync();
                var group = parallelGroups[groupIndex];

                // ★ 本组内已被剪枝的节点：标记 Skipped，不执行。
                //   剪枝发生在条件分支评估后（后续组），这些节点尚未执行（Pending），直接标记 Skipped；
                //   UpdateNodeStatus 幂等，重复标记安全。
                var toSkip = group.Where(id => prunedNodeIds.Contains(id)).ToList();
                foreach (var skippedId in toSkip)
                {
                    context.UpdateNodeStatus(skippedId, NodeStatus.Skipped);
                    NodeStatusChanged?.Invoke(this, new NodeExecutionStatus { NodeId = skippedId, Status = NodeStatus.Skipped });
                }
                var executable = group.Where(id => !prunedNodeIds.Contains(id)).ToList();
                if (executable.Count == 0)
                {
                    _logger.LogInfo($"组 {groupIndex + 1} 全部被剪枝，跳过执行");
                    continue;
                }

                _logger.LogInfo($"");
                _logger.LogInfo($"========== 执行组 {groupIndex + 1}/{parallelGroups.Count} ({executable.Count}个节点) ==========");

                if (context.CancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                // 并行执行当前组的节点
                var groupStartTime = DateTime.Now;
                using var groupResult = await ExecuteNodesInParallel(
                    workflow,
                    executable,
                    context,
                    dependencyGraph,
                    nodeExecutionProfiles);

                // ★ 统计按实际成功节点数（失败/跳过的节点不计入）
                totalExecutedNodes += executable.Count(id => context.GetNodeResult(id)?.Success == true);

                var groupDuration = (DateTime.Now - groupStartTime).TotalMilliseconds;
                _logger.LogInfo($"组 {groupIndex + 1} 执行完成, 耗时: {groupDuration:F2}ms, 成功节点: {groupResult.Success}");

                if (!groupResult.Success)
                {
                    // ★ 失败隔离：本组存在失败节点，但不再终止整条流程。
                    //   失败节点结果已由 SetNodeResult 写入 SSOT，此处合并本组结果供 UI 使用，
                    //   随后继续执行后续组 —— 不相关分支照常跑完；
                    //   失败节点的下游由前驱可用性判定（AreParentsAvailable）标 Skipped，避免级联假错误。
                    foreach (var kvp in context.GetAllNodeResults())
                    {
                        groupResult.SetNodeResult(kvp.Key, kvp.Value);
                    }
                    _logger.LogInfo($"组 {groupIndex + 1} 存在失败节点，按隔离策略继续执行后续组");
                }

                // ★ 收集本组所有条件分支的控制信号（可能有多个同组并行）。
                //   必须逐一处理每个信号：一组多条件分支时，只取第一个会导致其余条件分支
                //   的剪枝（命中/未命中）被忽略，其下游不剪枝 → "无规则时下游全执行" bug。
                var controlSignals = TryExtractControlSignals(workflow, executable, context);
                foreach (var (controlNodeId, nextNodeIds) in controlSignals)
                {
                    // 命中：保留目标节点的整条上游链（防止剪掉目标必经的中间层，保证数据链不断）
                    protectedNodeIds = ComputeProtectedNodes(workflow, nextNodeIds);
                    // 计算剪枝：非目标分支（命中）或全部下游（未命中）
                    var newlyPruned = ComputePrunedNodes(workflow, controlNodeId, nextNodeIds, protectedNodeIds, prunedNodeIds);
                    foreach (var id in newlyPruned)
                    {
                        prunedNodeIds.Add(id);
                    }
                    _logger.LogInfo(nextNodeIds.Count > 0
                        ? $"条件分支控制: 命中 {nextNodeIds.Count} 个目标 [{string.Join(", ", nextNodeIds)}]，剪枝 {newlyPruned.Count} 个节点"
                        : $"条件分支控制: 未命中，剪枝 {newlyPruned.Count} 个节点（该分支终止）");
                }
            }

            // 从 SSOT (WorkflowContext) 获取所有节点结果
            var allResults = context.GetAllNodeResults().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 合并所有节点结果
            result.Success = allResults.Values.All(r => r.Success);
            result.Status = result.Success ? ExecutionStatus.Success : ExecutionStatus.Failed;
            result.ExecutionTime = DateTime.Now - context.ExecutionStartTime;
            result.NodeResults = allResults;

            // 收集错误
            foreach (var nodeResult in allResults.Values.Where(r => !r.Success))
            {
                if (nodeResult.ErrorMessages?.Any() == true)
                {
                    foreach (var error in nodeResult.ErrorMessages.Where(e => e != null))
                    {
                        result.AddError(error, nodeResult.NodeId);
                    }
                }
            }

            // 合并成功节点的输出
            if (result.Success && allResults.Any(r => r.Value.Result != null))
            {
                var lastSuccessful = allResults.Values
                    .LastOrDefault(r => r.Success && r.Result != null);
                if (lastSuccessful != null)
                {
                    var images = lastSuccessful.Result.GetDisplayImages();
                    var firstImage = images.Values.FirstOrDefault(m => m != null && !m.IsEmpty);
                    if (firstImage != null)
                    {
                        result.Outputs = new Dictionary<string, object>
                        {
                            { "FinalResult", firstImage }
                        };
                    }
                }
            }

            _logger.LogInfo($"");
            _logger.LogInfo($"========== 工作流执行总结 ==========");
            _logger.LogInfo($"  总执行节点: {totalExecutedNodes}");
            _logger.LogInfo($"  执行组数: {parallelGroups.Count}");
            _logger.LogInfo($"  成功节点: {allResults.Values.Count(r => r.Success)}");
            _logger.LogInfo($"  失败节点: {allResults.Values.Count(r => !r.Success)}");
            _logger.LogInfo($"  总耗时: {(DateTime.Now - context.ExecutionStartTime).TotalMilliseconds:F2} ms");
            _logger.LogInfo($"======================================");

            CaptureDeclaredRunOutputs(result,workflow);
            ExecutionCompleted?.Invoke(this, result);
            return result;
#pragma warning restore CS0162
        }

        /// <summary>
        /// 持续从整个依赖图选择已就绪节点，而不是等待一个拓扑层全部结束。
        /// 这样相机等待、外部信号等待或耗时节点不会阻塞与其无依赖关系的后续节点。
        /// </summary>
        private async Task<ExecutionResult> ExecuteWorkflowWithReadyQueue(
            WorkflowRunSession session,
            Workflow workflow,
            WorkflowContext context,
            WorkflowExecutionDependencyGraph dependencyGraph,
            IReadOnlyDictionary<string, ToolExecutionProfile> nodeExecutionProfiles)
        {
            var result = new ExecutionResult();
            var order = workflow.Nodes.Select(node => node.Id).ToList();
            var pending = new HashSet<string>(order, StringComparer.Ordinal);
            var running = new List<(WorkflowNodeBase Node, IDisposable Lease, Task<NodeExecutionResult> Task)>();
            var leases = new WorkflowResourceLeaseManager();
            var pruned = new HashSet<string>(StringComparer.Ordinal);
            var maxConcurrency = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1));

            try
            {
                while (pending.Count > 0 || running.Count > 0)
                {
                    await session.WaitIfPausedAsync();
                    context.CancellationToken.ThrowIfCancellationRequested();

                    // 条件分支已剪掉的节点、禁用节点、或失败前驱的消费者均不进入执行队列。
                    bool skippedAny;
                    do
                    {
                        skippedAny = false;
                        foreach (var nodeId in pending.Where(id =>
                                 pruned.Contains(id) ||
                                 !workflow.Nodes.First(node => node.Id == id).IsEnabled ||
                                 dependencyGraph.GetPredecessors(id).Any(parent =>
                                     context.GetNodeStatus(parent) is NodeStatus.Failed or NodeStatus.Skipped)).ToList())
                        {
                            pending.Remove(nodeId);
                            skippedAny = true;
                            context.UpdateNodeStatus(nodeId, NodeStatus.Skipped);
                            NodeStatusChanged?.Invoke(this, new NodeExecutionStatus { NodeId = nodeId, Status = NodeStatus.Skipped });
                        }
                    } while (skippedAny);

                    var madeProgress = true;
                    while (madeProgress)
                    {
                        madeProgress = false;
                        foreach (var nodeId in order.Where(pending.Contains).ToList())
                        {
                            if (!dependencyGraph.GetPredecessors(nodeId)
                                    .All(parent => context.GetNodeStatus(parent) == NodeStatus.Completed))
                                continue;

                            var node = workflow.Nodes.First(candidate => candidate.Id == nodeId);
                            var profile = nodeExecutionProfiles.TryGetValue(nodeId, out var declared)
                                ? declared : ToolExecutionProfile.LegacyExclusive;
                            var externalWait = profile.ConcurrencyKind == ToolConcurrencyKind.ExternalDataWait;
                            var executingCount = running.Count(item =>
                                !nodeExecutionProfiles.TryGetValue(item.Node.Id, out var activeProfile) ||
                                activeProfile.ConcurrencyKind != ToolConcurrencyKind.ExternalDataWait);
                            if (!externalWait && executingCount >= maxConcurrency)
                                continue;
                            var hasExclusive = running.Any(item =>
                                nodeExecutionProfiles.TryGetValue(item.Node.Id, out var active) &&
                                active.ConcurrencyKind == ToolConcurrencyKind.WorkflowExclusive);
                            if ((!externalWait && ((profile.ConcurrencyKind == ToolConcurrencyKind.WorkflowExclusive && executingCount > 0) ||
                                hasExclusive)) ||
                                !leases.TryAcquire(workflow.Id, node.Id, profile, out var lease))
                                continue;

                            pending.Remove(nodeId);
                            PrepareNodeExecution(nodeId, dependencyGraph.GetPredecessors(nodeId));
                            context.UpdateNodeStatus(nodeId, NodeStatus.Running);
                            NodeStatusChanged?.Invoke(this, new NodeExecutionStatus { NodeId = nodeId, Status = NodeStatus.Running });
                            dependencyGraph.InputIssues.TryGetValue(nodeId, out var inputIssues);
                            running.Add((node, lease, Task.Run(() => ExecuteNode(node, context, workflow, inputIssues))));
                            madeProgress = true;
                        }
                    }

                    if (running.Count == 0)
                    {
                        if (pending.Count == 0)
                            break;
                        throw new InvalidOperationException("全局就绪队列中不存在可派发节点；请检查依赖、资源声明或节点状态。");
                    }

                    await Task.WhenAny(running.Select(item => item.Task));
                    foreach (var completed in running.Where(item => item.Task.IsCompleted).ToList())
                    {
                        running.Remove(completed);
                        completed.Lease.Dispose();
                        var nodeResult = await completed.Task;
                        context.CancellationToken.ThrowIfCancellationRequested();
                        context.SetNodeResult(completed.Node.Id, nodeResult);
                        NodeExecutionFinished?.Invoke(completed.Node, nodeResult);
                        var status = nodeResult.Success ? NodeStatus.Completed : NodeStatus.Failed;
                        context.UpdateNodeStatus(completed.Node.Id, status);
                        NodeStatusChanged?.Invoke(this, new NodeExecutionStatus { NodeId = completed.Node.Id, Status = status });
                        if (!nodeResult.Success)
                            result.AddError(nodeResult.ErrorMessages?.FirstOrDefault() ?? "执行失败", completed.Node.Id);

                        foreach (var (controlNodeId, targets) in TryExtractControlSignals(
                                     workflow, new List<string> { completed.Node.Id }, context))
                        {
                            var protectedNodes = ComputeProtectedNodes(workflow, targets);
                            pruned.UnionWith(ComputePrunedNodes(workflow, controlNodeId, targets, protectedNodes, pruned));
                        }
                    }
                }
            }
            finally
            {
                foreach (var item in running)
                {
                    // 停止只撤销派发；实际用户工作和清理结束后才归还独占资源。
                    try { await item.Task.ConfigureAwait(false); }
                    catch { /* 主执行路径保留终态；这里仅观察并排空，不发布迟到结果。 */ }
                    finally { item.Lease.Dispose(); }
                }
            }

            var allResults = context.GetAllNodeResults().ToDictionary(pair => pair.Key, pair => pair.Value);
            result.NodeResults = allResults;
            result.Success = result.Errors.Count == 0 && allResults.Values.All(node => node.Success);
            result.Status = result.Success ? ExecutionStatus.Success : ExecutionStatus.Failed;
            result.ExecutionTime = DateTime.Now - context.ExecutionStartTime;
            CaptureDeclaredRunOutputs(result,workflow);
            ExecutionCompleted?.Invoke(this, result);
            return result;
        }

        /// <summary>
        /// 并行执行一组节点（同一组内节点无依赖关系）
        /// </summary>
        /// <remarks>
        /// 注意：为支持参数绑定解析，组内节点按依赖顺序执行。
        /// 只有当节点间无数据依赖关系时才真正并行执行。
        /// </remarks>
        private async Task<ExecutionResult> ExecuteNodesInParallel(
            Workflow workflow,
            List<string> nodeIds,
            WorkflowContext context,
            WorkflowExecutionDependencyGraph dependencyGraph,
            IReadOnlyDictionary<string, ToolExecutionProfile> nodeExecutionProfiles)
        {
            var result = new ExecutionResult();
            var pending = nodeIds
                .Select(id => workflow.Nodes.FirstOrDefault(node => node.Id == id))
                .Where(node => node != null)
                .Cast<WorkflowNodeBase>()
                .ToList();
            var running = new List<(WorkflowNodeBase Node, IDisposable Lease, Task<NodeExecutionResult> Task)>();
            var leases = new WorkflowResourceLeaseManager();
            var maxConcurrency = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1));

            try
            {
            while (pending.Count > 0 || running.Count > 0)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                for (var index = 0; index < pending.Count && running.Count < maxConcurrency;)
                {
                    var node = pending[index];
                    if (!node.IsEnabled)
                    {
                        pending.RemoveAt(index);
                        continue;
                    }

                    if (!AreParentsAvailable(node.Id, context, dependencyGraph))
                    {
                        pending.RemoveAt(index);
                        context.UpdateNodeStatus(node.Id, NodeStatus.Skipped);
                        NodeStatusChanged?.Invoke(this, new NodeExecutionStatus { NodeId = node.Id, Status = NodeStatus.Skipped });
                        continue;
                    }

                    var profile = nodeExecutionProfiles.TryGetValue(node.Id, out var declared)
                        ? declared
                        : ToolExecutionProfile.LegacyExclusive;
                    var exclusiveRunning = running.Any(item => nodeExecutionProfiles.TryGetValue(item.Node.Id, out var active) && active.ConcurrencyKind == ToolConcurrencyKind.WorkflowExclusive);
                    if ((profile.ConcurrencyKind == ToolConcurrencyKind.WorkflowExclusive && running.Count > 0) || exclusiveRunning ||
                        !leases.TryAcquire(workflow.Id, node.Id, profile, out var lease))
                    {
                        index++;
                        continue;
                    }

                    pending.RemoveAt(index);
                    PrepareNodeExecution(node.Id, dependencyGraph.GetPredecessors(node.Id));
                    context.UpdateNodeStatus(node.Id, NodeStatus.Running);
                    NodeStatusChanged?.Invoke(this, new NodeExecutionStatus { NodeId = node.Id, Status = NodeStatus.Running });
                    running.Add((node, lease, Task.Run(() => ExecuteNode(node, context, workflow))));
                }

                if (running.Count == 0)
                    throw new InvalidOperationException("调度器无法为就绪节点取得资源租约。");

                await Task.WhenAny(running.Select(item => item.Task));

                foreach (var completed in running.Where(item => item.Task.IsCompleted).ToList())
                {
                    running.Remove(completed);
                    completed.Lease.Dispose();
                    var nodeResult = await completed.Task;
                    context.CancellationToken.ThrowIfCancellationRequested();
                    context.SetNodeResult(completed.Node.Id, nodeResult);
                    NodeExecutionFinished?.Invoke(completed.Node, nodeResult);
                    var status = nodeResult.Success ? NodeStatus.Completed : NodeStatus.Failed;
                    context.UpdateNodeStatus(completed.Node.Id, status);
                    NodeStatusChanged?.Invoke(this, new NodeExecutionStatus { NodeId = completed.Node.Id, Status = status });
                    if (!nodeResult.Success)
                        result.AddError(nodeResult.ErrorMessages?.FirstOrDefault() ?? "执行失败", completed.Node.Id);
                }
            }

            }
            finally
            {
                foreach (var item in running)
                {
                    try { await item.Task.ConfigureAwait(false); }
                    catch { /* Observe and drain; do not publish after cancellation. */ }
                    finally { item.Lease.Dispose(); }
                }
            }

            result.Success = result.Errors.Count == 0;
            result.Status = result.Success ? ExecutionStatus.Success : ExecutionStatus.Failed;
            return result;
        }

        /// <summary>
        /// 判断节点的所有前驱是否可用（均可执行）。
        /// 任一前驱为 Failed 或 Skipped → 返回 false，本节点应跳过执行，
        /// 避免下游带着缺失的上游数据继续执行而产生级联假错误。
        /// </summary>
        private static bool AreParentsAvailable(
            string nodeId,
            WorkflowContext context,
            WorkflowExecutionDependencyGraph dependencyGraph)
        {
            foreach (var predecessorNodeId in dependencyGraph.GetPredecessors(nodeId))
            {
                var status = context.GetNodeStatus(predecessorNodeId);
                if (status == NodeStatus.Failed || status == NodeStatus.Skipped)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 按依赖关系对节点进行拓扑排序
        /// </summary>
        private List<string> SortNodesByDependencies(Workflow workflow, List<string> nodeIds)
        {
            if (nodeIds.Count <= 1)
                return nodeIds;

            var nodeSet = new HashSet<string>(nodeIds);
            var inDegree = new Dictionary<string, int>();
            var dependencies = new Dictionary<string, List<string>>();

            // 初始化
            foreach (var nodeId in nodeIds)
            {
                inDegree[nodeId] = 0;
                dependencies[nodeId] = new List<string>();
            }

            // 计算组内节点间的依赖关系
            foreach (var conn in workflow.Connections)
            {
                if (nodeSet.Contains(conn.SourceNodeId) && !string.IsNullOrEmpty(conn.TargetNodeId))
                {
                    if (nodeSet.Contains(conn.TargetNodeId))
                    {
                        dependencies[conn.SourceNodeId].Add(conn.TargetNodeId);
                        inDegree[conn.TargetNodeId]++;
                    }
                }
            }

            // 拓扑排序
            var result = new List<string>();
            var queue = new Queue<string>(nodeIds.Where(id => inDegree[id] == 0));

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);

                foreach (var dep in dependencies[current])
                {
                    inDegree[dep]--;
                    if (inDegree[dep] == 0)
                    {
                        queue.Enqueue(dep);
                    }
                }
            }

            // 如果存在循环依赖，返回原始顺序
            if (result.Count != nodeIds.Count)
            {
                _logger.LogWarning("组内存在循环依赖，使用原始顺序执行");
                return nodeIds;
            }

            return result;
        }

        /// <summary>
        /// 执行工作流（同步，支持工作流控制节点）
        /// 公开入口：管理执行状态和生命周期；支持递归调用（子程序场景）
        /// 主工作流和子程序递归共享同一节点类型转换逻辑
        /// </summary>
        public async Task<ExecutionResult> ExecuteWorkflow(string workflowId, IVisionImage? inputImage,
            IDictionary<string, object?>? globalVariables = null,
            Dictionary<string, object>? contextVariables = null)
            => await ExecuteWorkflowAsync(workflowId, inputImage, globalVariables, contextVariables);

        private async Task<ExecutionResult> ExecuteWorkflowCore(
            WorkflowRunSession session,
            WorkflowRunScope scope,
            string workflowId,
            IVisionImage? inputImage,
            IDictionary<string, object?>? globalVariables,
            Dictionary<string, object>? contextVariables,
            WorkflowRunRequest? request,
            bool isSubworkflow)
        {
            var sourceWorkflow = session.Configuration?.Find(workflowId);
            if (sourceWorkflow == null)
            {
                _logger.LogError($"工作流不存在: {workflowId}");
                return ExecutionResult.CreateFailure($"工作流不存在: {workflowId}");
            }

            // 在生成运行快照前一次性校验定义。执行器不接收不完整或分裂的工作流对象。
            var definitionErrors = WorkflowDefinitionValidator.Validate(
                sourceWorkflow,
                id => session.Configuration?.Find(id), validateBindings: false);
            if (definitionErrors.Count > 0)
            {
                var definitionError =
                    $"工作流定义校验失败: {string.Join("; ", definitionErrors)}";
                _logger.LogError(definitionError);
                return ExecutionResult.CreateFailure(definitionError);
            }

            // 先构建本次运行的冻结计划。后续执行只读取计划，不再直接消费 UI 编辑对象。
            if(!isSubworkflow)
            {
                lock(_sessionGate)
                {
                    if(!ReferenceEquals(_activeSession,session)||!session.IsActive)
                        throw new InvalidOperationException("Root session is no longer the host's active execution.");
                }
                // This root task owns the host slot until its outer await/finally completes.
                // Do not invoke owner callbacks while holding the host's session gate.
                // This is a frozen run copy borrowing the document's owner, not a document
                // ownership transfer. Retiring it here would retire the still-live source workflow.
                if(request?.ExecutionOwner!=null)sourceWorkflow.ExecutionOwner=request.ExecutionOwner;
                sourceWorkflow.ExecutionOwner.ActivateRoot(session.RunId);
                session.ExecutionOwner=sourceWorkflow.ExecutionOwner;
            }
            using var executionPlan = BuildExecutionPlan(session, scope, sourceWorkflow, request,inputImage);
            var execWorkflow = executionPlan.ExecutableWorkflow;
            _logger.LogInfo($"[DEBUG ExecuteWorkflow] execWorkflow 创建完成: Id={execWorkflow.Id}, Name={execWorkflow.Name}, Nodes.Count={execWorkflow.Nodes.Count}, Connections.Count={execWorkflow.Connections.Count}");

            // 递归路径：跳过状态管理，直接执行
            if (isSubworkflow)
            {
                // [DIAG] 记录子程序上下文
                var ctxVarKeys = contextVariables != null ? string.Join(", ", contextVariables.Keys) : "null";
                var ctxVarValues = contextVariables != null
                    ? string.Join(", ", contextVariables.Select(kvp =>
                    {
                        var valType = kvp.Value?.GetType().Name ?? "null";
                        var valStr = kvp.Value is IVisionImage img ? $"HImage(IsEmpty={img.IsEmpty})" : (kvp.Value?.ToString() is string s ? (s.Length > 60 ? s.Substring(0, 60) + "..." : s) : "null");
                        return $"{kvp.Key}({valType}={valStr})";
                    }))
                    : "null";
                _logger.LogInfo($"[DIAG] 子程序递归: workflowId={workflowId}, sourceName={sourceWorkflow.Name}, inputImage={(inputImage == null ? "null" : $"HImage(IsEmpty={inputImage.IsEmpty})")}, contextVariables keys=[{ctxVarKeys}], values=[{ctxVarValues}]",
                    "诊断.执行引擎");

                var subContext = new WorkflowContext
                {
                    CancellationToken = session.CancellationToken
                };
                scope.Context = subContext;
                scope.State = WorkflowExecutionState.Running;
                subContext.SetVariable("InputImage", inputImage);
                subContext.GlobalVariables = globalVariables == null
                    ? null
                    : new Dictionary<string, object?>(globalVariables);
                ApplyRuntimeParameters(subContext, request?.RuntimeParameters);

                // 合并子程序输入端口值到子上下文（同时注入为合成节点结果供绑定解析）
                if (contextVariables != null && contextVariables.Count > 0)
                {
                    // 提取 __SubroutineParentNodeId__ 并移出，不进入合成结果
                    string? parentNodeId = null;
                    if (contextVariables.TryGetValue(SubroutineContextKeys.ParentNodeId, out var pnId) && pnId is string pnIdStr)
                    {
                        parentNodeId = pnIdStr;
                    }

                    var syntheticResults = new GenericToolResults { Status = ExecutionStatus.Success };
                    foreach (var kvp in contextVariables)
                    {
                        // 排除 __SubroutineParentNodeId__ 元数据键
                        if (string.Equals(kvp.Key, SubroutineContextKeys.ParentNodeId, StringComparison.Ordinal)) continue;

                        subContext.SetVariable(kvp.Key, kvp.Value);
                        syntheticResults.AddResultItem(kvp.Key, kvp.Value);
                    }
                    subContext.SetNodeResult("__SubroutineInputs__", new NodeExecutionResult
                    {
                        NodeId = "__SubroutineInputs__",
                        Success = true,
                        Status = ExecutionStatus.Success,
                        Result = syntheticResults
                    });

                    // ★ 架构核心：同时以父节点 ID 为 key 注入合成结果
                    // 使子程序内部节点通过绑定 SourceNodeId=父节点ID 即可正确解析输入值
                    if (!string.IsNullOrEmpty(parentNodeId))
                    {
                        subContext.SetNodeResult(parentNodeId, new NodeExecutionResult
                        {
                            NodeId = parentNodeId,
                            Success = true,
                            Status = ExecutionStatus.Success,
                            Result = syntheticResults
                        });
                        _logger.LogInfo($"注入子程序输入合成结果到父节点ID: {parentNodeId}");
                    }
                }

                _logger.LogInfo($"递归执行子程序工作流: {sourceWorkflow.Name}");
                try
                {
                var subResult = await ExecuteWorkflowWithParallelGroups(
                    session,
                    execWorkflow,
                    subContext,
                    executionPlan.DependencyGraph,
                    executionPlan.NodeExecutionProfiles);
                    scope.State = subResult.Status == ExecutionStatus.Cancelled
                        ? WorkflowExecutionState.Stopped
                        : subResult.Success ? WorkflowExecutionState.Completed : WorkflowExecutionState.Error;

                    // 将子程序内部节点的执行结果回传到主上下文
                    // 确保子程序下钻模式下，选中内部节点也能正确显示该节点的结果
                    if (session.RootContext != null)
                    {
                        foreach (var kvp in subResult.NodeResults)
                        {
                            session.RootContext.SetNodeResult(kvp.Key, kvp.Value);
                        }
                    }

                    return subResult;
                }
                catch (OperationCanceledException)
                {
                    scope.State = WorkflowExecutionState.Stopped;
                    throw;
                }
                catch
                {
                    scope.State = WorkflowExecutionState.Error;
                    throw;
                }
                finally
                {
                    ReleaseExecutionResources(subContext, execWorkflow);
                    scope.Context = null;
                }
            }

            // 首次进入：全生命周期管理
            session.RootContext = new WorkflowContext
            {
                CancellationToken = session.CancellationToken
            };
            scope.Context = session.RootContext;
            session.RootContext.SetVariable("InputImage", inputImage);
            session.RootContext.GlobalVariables = globalVariables == null
                ? null
                : new Dictionary<string, object?>(globalVariables);
            ApplyRuntimeParameters(session.RootContext, request?.RuntimeParameters);

            session.State = WorkflowExecutionState.Running;

            try
            {
                _logger.LogInfo($"开始执行工作流: {sourceWorkflow.Name}");
                var result = await ExecuteWorkflowWithParallelGroups(
                    session,
                    execWorkflow,
                    session.RootContext,
                    executionPlan.DependencyGraph,
                    executionPlan.NodeExecutionProfiles);
                session.State = result.Success ? WorkflowExecutionState.Completed : WorkflowExecutionState.Error;
                scope.State = session.State;
                _logger.LogInfo($"工作流执行完成: {sourceWorkflow.Name}, 耗时: {(DateTimeOffset.UtcNow - session.StartedAt).TotalMilliseconds:F2}ms");
                return result;
            }
            catch (OperationCanceledException)
            {
                session.State = WorkflowExecutionState.Stopped;
                scope.State = WorkflowExecutionState.Stopped;
                _logger.LogWarning("工作流执行被取消");
                var cancelled=ExecutionResult.CreateCancelled();
                cancelled.NodeResults=session.RootContext.GetAllNodeResults();
                CaptureDeclaredRunOutputs(cancelled,execWorkflow);
                return cancelled;
            }
            catch (Exception ex)
            {
                session.State = WorkflowExecutionState.Error;
                scope.State = WorkflowExecutionState.Error;
                _logger.LogError($"工作流执行失败: {ex.Message}", ex);
                var failed=ExecutionResult.CreateFailure($"{ex.Message}\n{ex.StackTrace}");
                failed.NodeResults=session.RootContext.GetAllNodeResults();
                CaptureDeclaredRunOutputs(failed,execWorkflow);
                return failed;
            }
            finally
            {
                ReleaseExecutionResources(session.RootContext, execWorkflow);
                session.RootContext = null;
                scope.Context = null;
                if (session.State != WorkflowExecutionState.Paused)
                    session.State = WorkflowExecutionState.Idle;
            }
        }

        /// <summary>
        /// 从 UI 编辑模型生成一次运行专属的可执行计划。
        /// </summary>
        private static void CaptureDeclaredRunOutputs(ExecutionResult result,Workflow workflow)
        {
            result.CaptureDeclaredOutputs(workflow.Nodes.OfType<AlgorithmNode>()
                .Where(node=>node.AdmittedIdentity is not null&&node.BindingOutputSchema is not null)
                .Select(node=>node.LastToolResult?.DeclaredOutput??(result.NodeResults.TryGetValue(node.Id,out var outcome)
                    ?node.BindingContext!.WithoutResult(node,
                        outcome.Success?ExecutionStatus.NotExecuted:outcome.Status is ExecutionStatus.NotExecuted?ExecutionStatus.Failed:outcome.Status,
                        outcome.ErrorMessages.Select(message=>new SunEyeVision.Plugin.Contracts.Diagnostic("execution",message)))
                    :node.BindingContext!.WithoutResult(node,ExecutionStatus.NotExecuted,Array.Empty<SunEyeVision.Plugin.Contracts.Diagnostic>()))));
        }

        private void ReleaseExecutionResources(WorkflowContext? context, Workflow workflow)
        {
            try { context?.ClearNodeResults(); }
            catch (Exception ex) { _logger.LogError("工作流结果快照清理失败", ex); }
            foreach (var node in workflow.Nodes.OfType<AlgorithmNode>())
            {
                try { node.ReleaseLastResult(); }
                catch (Exception ex) { _logger.LogError("工作流节点结果清理失败", ex); }
            }
        }

        private WorkflowExecutionPlan BuildExecutionPlan(
            WorkflowRunSession session,
            WorkflowRunScope scope,
            Workflow sourceWorkflow,
            WorkflowRunRequest? request,IVisionImage? inputImage)
        {
            var executableWorkflow = new Workflow(Guid.NewGuid().ToString(), sourceWorkflow.Name);
            if(session.ExecutionOwner!=null)sourceWorkflow.ExecutionOwner=session.ExecutionOwner;
            ConvertNodesForExecution(session, scope, sourceWorkflow, executableWorkflow, request);
            foreach(var node in executableWorkflow.Nodes.OfType<AlgorithmNode>())node.WorkflowInputImage=inputImage;
            var inputIssues = OutputBindingChangeService.GetExecutionInputIssues(executableWorkflow, sourceWorkflow.Id);
            var variableDefinitions=request?.CommunicationInputs?.GetVariables().ToArray();
            var dependencyGraph = WorkflowExecutionDependencyGraph.Compile(executableWorkflow, variableDefinitions, sourceWorkflow.Id, inputIssues);
            var nodeExecutionProfiles = executableWorkflow.Nodes.ToDictionary(
                node => node.Id,
                node => inputIssues.ContainsKey(node.Id) ? ToolExecutionProfile.LegacyExclusive
                    : ResolveExecutionProfile(node, request?.CommunicationInputs),
                StringComparer.Ordinal);
            _logger.LogInfo(
                $"执行计划已编译: 节点={executableWorkflow.Nodes.Count}, 依赖={dependencyGraph.Dependencies.Count}, " +
                $"可并行声明={nodeExecutionProfiles.Values.Count(profile => profile.ConcurrencyKind == ToolConcurrencyKind.ParallelSafe)}");
            var bindings=new WorkflowBindingContext(session,scope,executableWorkflow,dependencyGraph,variableDefinitions);
            return new WorkflowExecutionPlan(scope, executableWorkflow, dependencyGraph, nodeExecutionProfiles,bindings);
        }

        private static ToolExecutionProfile ResolveExecutionProfile(WorkflowNodeBase node,
            SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? communicationInputs)
        {
            if (node is AlgorithmNode { Tool: IToolExecutionProfileProvider provider })
                return provider.DescribeExecution(node.Parameters, communicationInputs) ?? ToolExecutionProfile.LegacyExclusive;

            return ToolExecutionProfile.LegacyExclusive;
        }

        /// <summary>
        /// 将 UI 层原始节点（WorkflowNodeBase）转换为类型化执行节点
        /// 供 ExecuteWorkflow 内部统一调用，主工作流和子程序递归共享同一转换逻辑
        /// </summary>
        /// <remarks>
        /// - 所有节点（含 Condition、Subroutine）统一转为 AlgorithmNode，通过 ToolRegistry + IToolPlugin 执行
        /// - 参数绑定配置 ParamSettings 一并复制
        /// - 节点 ID 保持不变，连接关系直接复制
        /// - 转换失败的节点直接报错（遵循 fail-fast 原则）
        /// </remarks>
        private void ConvertNodesForExecution(
            WorkflowRunSession session,
            WorkflowRunScope scope,
            Workflow source,
            Workflow target,
            WorkflowRunRequest? request)
        {
            _logger.LogInfo($"[DEBUG ConvertNodesForExecution] 开始转换: source.Name={source.Name}, source.Id={source.Id}");
            _logger.LogInfo($"[DEBUG ConvertNodesForExecution] source.Nodes.Count={source.Nodes.Count}");
            _logger.LogInfo($"[DEBUG ConvertNodesForExecution] source.Connections.Count={source.Connections.Count}");

            for (int i = 0; i < source.Nodes.Count; i++)
            {
                var n = source.Nodes[i];
                _logger.LogInfo($"[DEBUG ConvertNodesForExecution] 节点[{i}]: Id={n.Id}, Name={n.Name}, ToolType={n.ToolType}, NodeType={n.NodeType}");
            }

            // Fail-Forward 防御：校验连接引用的节点确实存在于节点集合。
            // 若存在"连接引用了缺失节点"（历史上曾因 UI/引擎双数据源不同步导致节点静默消失），
            // 直接报错而非静默忽略，避免执行结果不完整且无任何提示。
            var nodeIds = new HashSet<string>(source.Nodes.Select(n => n.Id));
            foreach (var conn in source.Connections)
            {
                if (!nodeIds.Contains(conn.SourceNodeId) || !nodeIds.Contains(conn.TargetNodeId))
                {
                    throw new InvalidOperationException(
                        $"工作流数据不一致：连接 {conn.SourceNodeId} → {conn.TargetNodeId} 引用了不存在的节点。" +
                        "请重新保存/加载方案，或检查节点集合是否完整。");
                }
            }

            foreach (var node in source.Nodes)
            {
                // 运行开始时冻结参数。UI 后续编辑原对象只影响下一次运行。
                var executionParameters = ToolParameterSnapshot.Create(node.Parameters);
                ApplyNodeParameterOverrides(executionParameters, node.Id, request);

                // 所有节点（含 Condition、Subroutine）统一作为 AlgorithmNode + IToolPlugin 执行
                var executionNode = WorkflowNodeFactory.CreateAlgorithmNode(
                    node.ToolType,
                    node.Id,
                    node.Name,
                    node.DispName ?? node.Name,
                    executionParameters,
                    enableCaching: true,
                    enableRetry: false)
                    ?? throw new InvalidOperationException(
                        $"无法创建算法节点: ToolType={node.ToolType}, Name={node.Name}");

                // 复制参数绑定配置
                executionNode.ParamSettings = node.ParamSettings?.Clone() ?? new ParamSettingContainer();
                if (request != null && request.NodeBindingOverrides.TryGetValue(node.Id, out var overrides))
                    foreach (var value in overrides)
                        executionNode.ParamSettings.SetSetting(ParamSetting.CreateConstant(value.Key, value.Value));
                executionNode.CameraCaptureService = request?.CameraCaptureService;
                executionNode.ResourcePaths = request?.ResourcePaths;
                executionNode.CommunicationOutputs = request?.CommunicationOutputs;
                executionNode.ExecutionWorkflowId = source.Id;
                executionNode.ExecutionOwner = source.ExecutionOwner;
                executionNode.ExecutionRunId = scope.RunId;
                executionNode.ExecutionRootRunId = session.RunId;
                executionNode.CommunicationInputs = request?.CommunicationInputs == null ? null :
                    new SunEyeVision.Plugin.SDK.Models.Communication.RuntimeVariableReceiveService(request.CommunicationInputs, null, workflowId: source.Id) { ConsumerScope = scope.ConsumerScope };
                executionNode.CameraFrame = request != null && request.CameraFrames.TryGetValue(node.Id, out var cameraFrame)
                    ? cameraFrame
                    : null;

                // ★ 委托注入：Subroutine 节点通过委托调用 ExecuteWorkflowAsync 执行子工作流
                if (executionParameters is ISubWorkflowExecutor executor)
                {
                    // 运行时同步 SubroutineId（以 node.SubroutineId 为 SSOT）
                    executor.SubroutineId = node.SubroutineId ?? string.Empty;
                    // 闭包捕获 node.Id 作为父节点 ID，通过扩展字典传入子上下文
                    executor.ExecuteSubWorkflowAsync = (subId, img, ctx) =>
                    {
                        var extendedCtx = ctx != null
                            ? new Dictionary<string, object>(ctx)
                            : new Dictionary<string, object>();
                        extendedCtx[SubroutineContextKeys.ParentNodeId] = node.Id;
                        var childScope = session.CreateChildScope(subId, scope.RunId, node.Id);
                        return ExecuteWorkflowCore(
                            session,
                            childScope,
                            subId,
                            img,
                            null,
                            extendedCtx,
                            request,
                            isSubworkflow: true);
                    };
                }

                target.AddNode(executionNode);
            }

            // 复制连接关系（节点 ID 不变，直接复制）
            foreach (var conn in source.Connections)
            {
                try
                {
                    target.ConnectNodes(conn.SourceNodeId, conn.TargetNodeId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"复制工作流连接失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 收集刚执行的节点组中所有节点输出的流程控制信号（条件分支）。
        /// 返回 (控制节点ID, 命中目标节点列表) 列表。一组可能有多个条件分支并行，
        /// 必须全部收集（逐一处理），否则后面的条件分支被忽略，其剪枝不生效。
        /// HasControlSignal 区分"未命中终止"（true 且目标空）与"普通节点无信号"（false）。
        /// </summary>
        private List<(string controlNodeId, List<string> nextNodeIds)> TryExtractControlSignals(
            Workflow workflow,
            List<string> executedGroupNodeIds,
            WorkflowContext context)
        {
            var signals = new List<(string, List<string>)>();
            foreach (var nodeId in executedGroupNodeIds)
            {
                var nodeResult = context.GetNodeResult(nodeId);
                if (nodeResult?.Result == null) continue;

                // 强类型读取流程控制信号：HasControlSignal 为 true 才视为控制节点
                if (nodeResult.Result is IControlFlowResult cfr && cfr.HasControlSignal)
                {
                    var targets = cfr.NextNodeIds?.ToList() ?? new List<string>();
                    _logger.LogInfo(targets.Count > 0
                        ? $"节点 {nodeId} 输出控制信号，命中目标 [{string.Join(", ", targets)}]"
                        : $"节点 {nodeId} 输出控制信号，未命中（将剪枝该分支）", nodeId);
                    signals.Add((nodeId, targets));
                }
            }
            return signals;
        }

        /// <summary>
        /// 计算"受保护节点"：命中目标节点及其整条上游链（沿反向连接）。
        /// 这些节点即使前驱被剪也不剪——保证目标节点执行时上游数据链完整。
        /// </summary>
        private HashSet<string> ComputeProtectedNodes(Workflow workflow, List<string> nextNodeIds)
        {
            var protectedSet = new HashSet<string>();
            var queue = new Queue<string>(nextNodeIds);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!protectedSet.Add(id)) continue;
                // 找上游
                foreach (var conn in workflow.Connections.Where(c => c.TargetNodeId == id))
                {
                    if (!protectedSet.Contains(conn.SourceNodeId))
                        queue.Enqueue(conn.SourceNodeId);
                }
            }
            return protectedSet;
        }

        /// <summary>
        /// 计算条件分支应剪枝的节点集合。
        /// 规则（图不动点）：一个节点被剪 ⟺ 其所有前驱都被剪。
        /// - 未命中（nextNodeIds 空）：条件分支全部下游传播剪枝
        /// - 命中：条件分支下游中不属于命中目标（且不受保护）的传播剪枝
        /// 受保护节点（命中目标的上游链）永不剪，保证目标数据链完整。
        /// </summary>
        private HashSet<string> ComputePrunedNodes(
            Workflow workflow,
            string controlNodeId,
            List<string> nextNodeIds,
            HashSet<string> protectedNodeIds,
            HashSet<string> alreadyPruned)
        {
            var pruned = new HashSet<string>(alreadyPruned);
            pruned.Add(controlNodeId);  // 控制节点自身作为剪枝源

            // 从控制节点直接下游开始 BFS 传播
            var queue = new Queue<string>(
                workflow.Connections
                    .Where(c => c.SourceNodeId == controlNodeId)
                    .Select(c => c.TargetNodeId));

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (pruned.Contains(id)) continue;
                // 命中目标或其上游链受保护 → 保留
                if (protectedNodeIds.Contains(id)) continue;
                // 前驱全剪才剪
                var allPredsPruned = workflow.Connections
                    .Where(c => c.TargetNodeId == id)
                    .All(c => pruned.Contains(c.SourceNodeId));
                if (allPredsPruned)
                {
                    pruned.Add(id);
                    foreach (var conn in workflow.Connections.Where(c => c.SourceNodeId == id))
                    {
                        if (!pruned.Contains(conn.TargetNodeId))
                            queue.Enqueue(conn.TargetNodeId);
                    }
                }
            }

            return pruned;
        }

        /// <summary>
        /// 执行单个节点（统一为 AlgorithmNode 执行）
        /// </summary>
        /// <param name="node">工作流节点</param>
        /// <param name="context">工作流上下文</param>
        /// <param name="workflow">工作流实例</param>
        private async Task<NodeExecutionResult> ExecuteNode(
            WorkflowNodeBase node,
            WorkflowContext context,
            Workflow workflow, IReadOnlyList<ExecutionInputIssue>? inputIssues = null)
        {
            var nodeResult = new NodeExecutionResult
            {
                NodeId = node.Id,
                StartTime = DateTime.Now
            };

            try
            {
                if (inputIssues?.Count > 0)
                {
                    var message = string.Join("\n", inputIssues.Select(issue => issue.Message));
                    var diagnostics = new GenericToolResults { ToolId = node.ToolType, ToolName = node.Name };
                    diagnostics.SetError(message);
                    nodeResult.Result = diagnostics;
                    throw new InvalidOperationException(message);
                }
                // 所有节点统一为 AlgorithmNode 执行
                var algorithmNode = node as AlgorithmNode
                    ?? throw new InvalidOperationException($"节点 {node.Name} 不是 AlgorithmNode 类型，实际类型: {node.GetType().Name}");

                nodeResult = await ExecuteAlgorithmNode(algorithmNode, context);
                nodeResult.EndTime = DateTime.Now;
                return nodeResult;
            }
            catch (OperationCanceledException)
            {
                // 取消继续传播到工作流根入口，由根入口统一生成 Cancelled 结果。
                throw;
            }
            catch (Exception ex)
            {
                nodeResult.EndTime = DateTime.Now;
                nodeResult.Success = false;
                nodeResult.Status = ExecutionStatus.Failed;
                nodeResult.ErrorMessages = new List<string> { ex.Message };

                _logger.LogError($"节点 {node.Name} 执行失败: {ex.Message}", ex);
                return nodeResult;
            }
        }

        /// <summary>
        /// 执行算法节点
        /// </summary>
        /// <remarks>
        /// 使用 ToolExecutor 统一执行工具，确保与单工具调试使用相同的执行路径。
        /// 图像输入通过 ToolParameters.ResolvedInputImage 传递，不再作为独立参数。
        /// </remarks>
        /// <param name="node">算法节点</param>
        /// <param name="context">工作流上下文（SSOT，用于参数绑定解析）</param>
        private async Task<NodeExecutionResult> ExecuteAlgorithmNode(AlgorithmNode node, WorkflowContext context)
        {
            var result = new NodeExecutionResult { NodeId = node.Id, StartTime = DateTime.Now, EndTime = DateTime.Now };

            // Failed results are retained for diagnostics only, never as executable inputs.
            using var inputSnapshot = context.CaptureNodeResults();
            var toolResultsDict = inputSnapshot.Results
                .Where(kvp => kvp.Value.Success && kvp.Value.Result?.IsSuccess == true)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Result!);

            // [DIAG] 记录 toolResultsDict 的内容，便于排查参数绑定问题
            var toolResultKeys = string.Join(", ", toolResultsDict.Select(kvp =>
            {
                var res = kvp.Value;
                // 尝试获取 ToolResults 的 DisplayName 或 Image 相关内容
                var imageCount = res?.GetDisplayImages()?.Count ?? 0;
                var resultType = res?.GetType().Name ?? "null";
                return $"{kvp.Key}({resultType}, images={imageCount})";
            }));
            _logger.LogInfo($"[DIAG ExecuteAlgorithmNode] node={node.Name}({node.Id}), toolResultsDict keys=[{toolResultKeys}]",
                "诊断.执行引擎");

            // 注入全局变量作为合成节点结果（使 __GlobalVars__.变量名 绑定可解析）
            if (context.GlobalVariables != null && context.GlobalVariables.Count > 0)
            {
                var globalVarResults = new GenericToolResults { Status = ExecutionStatus.Success };
                foreach (var kvp in context.GlobalVariables)
                {
                    globalVarResults.AddResultItem(kvp.Key, kvp.Value);
                }
                toolResultsDict["__GlobalVars__"] = globalVarResults;
            }

            // 调用异步节点入口；旧同步工具由 ToolExecutor 兼容，新工具可真正异步执行。
            var algorithmResult = await node.ExecuteAsync(
                toolResultsDict,
                context.GlobalVariables,
                context.CancellationToken);

            if (algorithmResult.Status == ExecutionStatus.Cancelled)
            {
                throw new OperationCanceledException(
                    algorithmResult.ErrorMessage ?? "执行被取消",
                    context.CancellationToken);
            }

            if (algorithmResult.Success && algorithmResult.ToolResults != null)
            {
                result.Success = true;
                result.Status = ExecutionStatus.Success;
                result.Result = algorithmResult.ToolResults;
            }
            else if (!algorithmResult.Success)
            {
                result.ErrorMessages = new List<string> { algorithmResult.ErrorMessage };
                result.Success = false;
                result.Status = ExecutionStatus.Failed;
                // Diagnostics are visible on failed nodes; scheduling still follows Failed.
                result.Result = algorithmResult.ToolResults;
            }

            return result;
        }




        #endregion

        #region 异步执行

        /// <summary>
        /// 异步执行工作流
        /// </summary>
        public async Task<ExecutionResult> ExecuteWorkflowAsync(string workflowId, IVisionImage? inputImage,
            IDictionary<string, object?>? globalVariables = null,
            Dictionary<string, object>? contextVariables = null)
            => await ExecuteWorkflowAsync(
                new WorkflowRunRequest(workflowId, inputImage, globalVariables, contextVariables: contextVariables));

        /// <summary>
        /// 使用宿主已冻结的运行请求执行工作流。宿主取消令牌与本次根会话链接，
        /// 因此停止连续运行能传递到真正执行节点。
        /// </summary>
        public async Task<ExecutionResult> ExecuteWorkflowAsync(
            WorkflowRunRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            WorkflowRunSession session;
            lock (_sessionGate)
            {
                if (_activeSession?.IsActive == true)
                    return ExecutionResult.CreateFailure("已有工作流正在执行；请先停止或等待当前运行完成。");

                session = new WorkflowRunSession(request.WorkflowId, cancellationToken);
                // 先占用执行槽位，再排入线程池。否则两次紧邻的异步调用都可能在首个任务
                // 尚未开始、State 仍为 Idle 时通过检查，重新出现会话相互覆盖。
                session.TryStart();
                _activeSession = session;
            }

            try
            {
                session.Configuration = request.Configuration ?? WorkflowRunConfiguration.Capture(request.WorkflowId, _workflowEngine.GetWorkflow);
                // 不把取消令牌传给 Task.Run：若任务尚未排队即被取消，Task.Run 会在进入统一
                // 错误转换逻辑前直接抛 TaskCanceledException。执行链内部通过 WorkflowContext 的
                // 令牌检测取消并返回标准 ExecutionResult。
                return await Task.Run(
                    () => ExecuteWorkflowCore(
                        session,
                        session.RootScope,
                        request.WorkflowId,
                        request.InputImage,
                        new Dictionary<string, object?>(request.GlobalVariables),
                        new Dictionary<string, object>(request.ContextVariables),
                        request,
                        isSubworkflow: false));
            }
            finally
            {
                session.ExecutionOwner?.CompleteRoot(session.RunId);
                lock (_sessionGate)
                {
                    if (ReferenceEquals(_activeSession, session))
                        _activeSession = null;
                }
                session.Dispose();
            }
        }

        private static void ApplyRuntimeParameters(
            WorkflowContext context,
            IReadOnlyDictionary<string, object>? runtimeParameters)
        {
            if (runtimeParameters == null)
                return;

            foreach (var parameter in runtimeParameters)
                context.SetRuntimeParameter(parameter.Key, parameter.Value);
        }

        private static void ApplyNodeParameterOverrides(
            ToolParameters parameters,
            string nodeId,
            WorkflowRunRequest? request)
        {
            if (request == null || !request.NodeParameterOverrides.TryGetValue(nodeId, out var overrides))
                return;

            var parameterType = parameters.GetType();
            foreach (var pair in overrides)
            {
                var property = parameterType.GetProperty(pair.Key);
                if (property == null || !property.CanWrite)
                    throw new InvalidOperationException(
                        $"节点 {nodeId} 的运行时参数覆盖无效：{parameterType.Name}.{pair.Key} 不可写。");

                property.SetValue(parameters, pair.Value);
            }
        }

        /// <summary>
        /// 暂停执行
        /// </summary>
        public void PauseExecution()
        {
            if (_activeSession?.RequestPause() == true)
            {
                _logger.LogWarning("工作流执行已暂停");
            }
        }

        /// <summary>
        /// 恢复执行
        /// </summary>
        public void ResumeExecution()
        {
            if (_activeSession?.RequestResume() == true)
            {
                _logger.LogWarning("工作流执行已恢复");
            }
        }

        /// <summary>
        /// 停止执行
        /// </summary>
        public void StopExecution()
        {
            _activeSession?.RequestStop();
            _logger.LogWarning("工作流执行停止请求已发送");
        }

        #endregion

        #region 状态查询

        /// <summary>
        /// 获取执行统计信息
        /// </summary>
        public ExecutionStatistics GetExecutionStatistics()
        {
            if (_activeSession?.RootContext == null)
            {
                return new ExecutionStatistics();
            }

            return _activeSession.RootContext.GetStatistics();
        }

        #endregion

        #region 数据源集成（SSOT 已转移到 WorkflowContext）

        /// <summary>
        /// 执行节点前准备数据源（SSOT 模式下为空操作，数据已存储在 WorkflowContext 中）
        /// </summary>
        public void PrepareNodeExecution(string nodeId, IEnumerable<string> parentNodeIds)
        {
            // ★ SSOT: 数据已通过 context.SetNodeResult 存入 WorkflowContext
        }

        #endregion
    }

    /// <summary>
    /// 节点执行状态事件参数
    /// </summary>
    public class NodeExecutionStatus : EventArgs
    {
        public string NodeId { get; set; } = string.Empty;
        public NodeStatus Status { get; set; }
    }
}

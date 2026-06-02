using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Models;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

namespace SunEyeVision.Workflow
{
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
        Error           // 错误
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

        // 执行状态管理
        private WorkflowExecutionState _currentState;
        private CancellationTokenSource _cancellationTokenSource;
        private WorkflowContext? _currentContext;

        // 执行统计
        private DateTime _executionStartTime;

        // ★ SSOT 已转移到 WorkflowContext，引擎不再维护重复缓存

        /// <summary>
        /// 当前执行状态
        /// </summary>
        public WorkflowExecutionState CurrentState => _currentState;

        /// <summary>
        /// 当前执行上下文
        /// </summary>
        public WorkflowContext CurrentContext => _currentContext;

        /// <summary>
        /// 节点执行状态变化事件
        /// </summary>
        public event EventHandler<NodeExecutionStatus>? NodeStatusChanged;

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
            _currentState = WorkflowExecutionState.Idle;

            // ★ SSOT 统一由 WorkflowContext 管理，引擎不再维护独立 DSQS
        }

        #region 同步执行

        /// <summary>
        /// 使用基于拓扑排序的并行组执行工作流（简化方案）
        /// </summary>
        private async Task<ExecutionResult> ExecuteWorkflowWithParallelGroups(
            Workflow workflow,
            WorkflowContext context)
        {
            _logger.LogInfo($"[DEBUG ExecuteWorkflowWithParallelGroups] workflow.Id={workflow.Id}, workflow.Name={workflow.Name}, Nodes.Count={workflow.Nodes.Count}, Connections.Count={workflow.Connections.Count}");

            var result = new ExecutionResult();
            int totalExecutedNodes = 0;

            _logger.LogInfo($"========== 使用基于执行链的并行组执行模式 ==========");

            // 获取并行执行组（基于执行链识别）
            var parallelGroups = workflow.GetParallelExecutionGroupsByChains();
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
            for (int groupIndex = 0; groupIndex < parallelGroups.Count; groupIndex++)
            {
                var group = parallelGroups[groupIndex];
                _logger.LogInfo($"");
                _logger.LogInfo($"========== 执行组 {groupIndex + 1}/{parallelGroups.Count} ({group.Count}个节点) ==========");

                if (_cancellationTokenSource?.IsCancellationRequested == true)
                {
                    throw new OperationCanceledException();
                }

                // 并行执行当前组的节点
                var groupStartTime = DateTime.Now;
                var groupResult = await ExecuteNodesInParallel(
                    workflow,
                    group,
                    context);

                totalExecutedNodes += group.Count;

                var groupDuration = (DateTime.Now - groupStartTime).TotalMilliseconds;
                _logger.LogInfo($"组 {groupIndex + 1} 执行完成, 耗时: {groupDuration:F2}ms, 成功节点: {groupResult.Success}");

                if (!groupResult.Success)
                {
                    _currentState = WorkflowExecutionState.Error;
                    return groupResult;
                }
            }

            // 从 SSOT (WorkflowContext) 获取所有节点结果
            var allResults = context.GetAllNodeResults().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 合并所有节点结果
            result.Success = allResults.Values.All(r => r.Success);
            result.ExecutionTime = DateTime.Now - _executionStartTime;
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
            _logger.LogInfo($"  总耗时: {(DateTime.Now - _executionStartTime).TotalMilliseconds:F2} ms");
            _logger.LogInfo($"======================================");

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
            WorkflowContext context)
        {
            var result = new ExecutionResult();

            // 如果组内只有一个节点，直接顺序执行
            if (nodeIds.Count == 1)
            {
                var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeIds[0]);
                if (node != null && node.IsEnabled)
                {
                    _logger.Info($"开始执行", node.Name);

                    // 准备数据源
                    var parentIds = workflow.Connections
                        .Where(conn => conn.TargetNodeId == node.Id)
                        .Select(conn => conn.SourceNodeId)
                        .ToList();
                    PrepareNodeExecution(node.Id, parentIds);

                    context.UpdateNodeStatus(node.Id, NodeStatus.Running);
                    NodeStatusChanged?.Invoke(this, new NodeExecutionStatus { NodeId = node.Id, Status = NodeStatus.Running });

                    var nodeResult = await ExecuteNode(node, context, workflow);

                    var duration = nodeResult.Duration?.TotalMilliseconds ?? 0;
                    _logger.Info($"执行完成, 状态: {(nodeResult.Success ? "? 成功" : "? 失败")}, 耗时: {duration:F2}ms", node.Name);

                    if (nodeResult.Success && nodeResult.Result != null)
                    {
                        // ★ 结果已通过 context.SetNodeResult 存入 SSOT
                    }

                    context.SetNodeResult(node.Id, nodeResult);
                    context.UpdateNodeStatus(node.Id, nodeResult.Success ? NodeStatus.Completed : NodeStatus.Failed);
                    NodeStatusChanged?.Invoke(this, new NodeExecutionStatus { NodeId = node.Id, Status = nodeResult.Success ? NodeStatus.Completed : NodeStatus.Failed });

                    if (!nodeResult.Success)
                    {
                        var errorMessage = nodeResult.ErrorMessages?.FirstOrDefault() ?? "执行失败";
                        result.AddError(errorMessage, node.Id);
                    }
                }
                result.Success = result.Errors.Count == 0;
                return result;
            }

            // 按依赖顺序执行组内节点（确保参数绑定能正确解析上游节点结果）
            // 识别组内节点间的依赖关系，按拓扑顺序执行
            var sortedNodeIds = SortNodesByDependencies(workflow, nodeIds);
            
            foreach (var nodeId in sortedNodeIds)
            {
                var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
                if (node != null && node.IsEnabled)
                {
                    // 准备数据源
                    var parentIds = workflow.Connections
                        .Where(conn => conn.TargetNodeId == node.Id)
                        .Select(conn => conn.SourceNodeId)
                        .ToList();
                    PrepareNodeExecution(node.Id, parentIds);

                    context.UpdateNodeStatus(node.Id, NodeStatus.Running);
                    NodeStatusChanged?.Invoke(this, new NodeExecutionStatus { NodeId = node.Id, Status = NodeStatus.Running });

                    var nodeResult = await ExecuteNode(node, context, workflow);

                    var duration = nodeResult.Duration?.TotalMilliseconds ?? 0;
                    _logger.Info($"执行完成, 状态: {(nodeResult.Success ? "? 成功" : "? 失败")}, 耗时: {duration:F2}ms", node.Name);

                    if (nodeResult.Success && nodeResult.Result != null)
                    {
                        // ★ 结果已通过 context.SetNodeResult 存入 SSOT
                    }

                    context.SetNodeResult(nodeId, nodeResult);
                    context.UpdateNodeStatus(nodeId, nodeResult.Success ? NodeStatus.Completed : NodeStatus.Failed);
                    NodeStatusChanged?.Invoke(this, new NodeExecutionStatus { NodeId = nodeId, Status = nodeResult.Success ? NodeStatus.Completed : NodeStatus.Failed });

                    if (!nodeResult.Success)
                    {
                        var errorMessage = nodeResult.ErrorMessages?.FirstOrDefault() ?? "执行失败";
                        result.AddError(errorMessage, nodeId);
                        // 一个节点失败时停止当前组的执行
                        break;
                    }
                }
            }

            result.Success = result.Errors.Count == 0;
            return result;
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
            IDictionary<string, object?>? globalVariables = null)
        {
            var sourceWorkflow = _workflowEngine.GetWorkflow(workflowId);
            if (sourceWorkflow == null)
            {
                _logger.LogError($"工作流不存在: {workflowId}");
                return ExecutionResult.CreateFailure($"工作流不存在: {workflowId}");
            }

            // 首次进入时检测循环依赖（递归路径跳过此检查，由外层保证无循环）
            if (_currentState != WorkflowExecutionState.Running)
            {
                var cycles = sourceWorkflow.DetectCycles();
                if (cycles.Count > 0)
                {
                    var cycleMsg = $"检测到循环依赖: {string.Join("; ", cycles)}";
                    _logger.LogError(cycleMsg);
                    return ExecutionResult.CreateFailure(cycleMsg);
                }
            }

            // 统一创建临时执行工作流，将 UI 原始节点转换为类型化执行节点
            // 主工作流和子程序递归走同一段代码，消除两套转换逻辑
            var execWorkflow = new Workflow(Guid.NewGuid().ToString(), sourceWorkflow.Name);
            ConvertNodesForExecution(sourceWorkflow, execWorkflow);
            _logger.LogInfo($"[DEBUG ExecuteWorkflow] execWorkflow 创建完成: Id={execWorkflow.Id}, Name={execWorkflow.Name}, Nodes.Count={execWorkflow.Nodes.Count}, Connections.Count={execWorkflow.Connections.Count}");

            // 递归路径：跳过状态管理，直接执行
            if (_currentState == WorkflowExecutionState.Running)
            {
                var subContext = new WorkflowContext();
                subContext.SetVariable("InputImage", inputImage);
                subContext.GlobalVariables = globalVariables;
                _logger.LogInfo($"递归执行子程序工作流: {sourceWorkflow.Name}");
                return await ExecuteWorkflowWithParallelGroups(execWorkflow, subContext);
            }

            // 首次进入：全生命周期管理
            _currentContext = new WorkflowContext();
            _currentContext.SetVariable("InputImage", inputImage);
            _currentContext.GlobalVariables = globalVariables;

            _currentState = WorkflowExecutionState.Running;
            _executionStartTime = DateTime.Now;

            try
            {
                _logger.LogInfo($"开始执行工作流: {sourceWorkflow.Name}");
                var result = await ExecuteWorkflowWithParallelGroups(execWorkflow, _currentContext);
                _currentState = result.Success ? WorkflowExecutionState.Completed : WorkflowExecutionState.Error;
                _logger.LogInfo($"工作流执行完成: {sourceWorkflow.Name}, 耗时: {(DateTime.Now - _executionStartTime).TotalMilliseconds:F2}ms");
                return result;
            }
            catch (OperationCanceledException)
            {
                _currentState = WorkflowExecutionState.Stopped;
                _logger.LogWarning("工作流执行被取消");
                var result = ExecutionResult.CreateFailure("工作流执行被取消");
                result.IsStopped = true;
                return result;
            }
            catch (Exception ex)
            {
                _currentState = WorkflowExecutionState.Error;
                _logger.LogError($"工作流执行失败: {ex.Message}", ex);
                return ExecutionResult.CreateFailure($"{ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _currentContext = null;
                if (_currentState != WorkflowExecutionState.Paused)
                {
                    _currentState = WorkflowExecutionState.Idle;
                }
            }
        }

        /// <summary>
        /// 将 UI 层原始节点（WorkflowNodeBase）转换为类型化执行节点
        /// 供 ExecuteWorkflow 内部统一调用，主工作流和子程序递归共享同一转换逻辑
        /// </summary>
        /// <remarks>
        /// - Subroutine 节点 → SubroutineNode（复制 SubroutineId、SubroutineName）
        /// - Condition 节点 → ConditionNode
        /// - 普通算法节点 → AlgorithmNode（通过 WorkflowNodeFactory）
        /// - 参数绑定配置 ParamSettings 一并复制
        /// - 节点 ID 保持不变，连接关系直接复制
        /// - Start 节点跳过（UI 层面板节点，无执行逻辑）
        /// - 转换失败的节点直接报错（遵循 fail-fast 原则）
        /// </remarks>
        private void ConvertNodesForExecution(Workflow source, Workflow target)
        {
            _logger.LogInfo($"[DEBUG ConvertNodesForExecution] 开始转换: source.Name={source.Name}, source.Id={source.Id}");
            _logger.LogInfo($"[DEBUG ConvertNodesForExecution] source.Nodes.Count={source.Nodes.Count}");
            _logger.LogInfo($"[DEBUG ConvertNodesForExecution] source.Connections.Count={source.Connections.Count}");

            for (int i = 0; i < source.Nodes.Count; i++)
            {
                var n = source.Nodes[i];
                _logger.LogInfo($"[DEBUG ConvertNodesForExecution] 节点[{i}]: Id={n.Id}, Name={n.Name}, ToolType={n.ToolType}, NodeType={n.NodeType}");
            }

            foreach (var node in source.Nodes)
            {
                WorkflowNodeBase executionNode;

                if (ControlFlowRegistry.IsControlFlow(node.ToolType))
                {
                    executionNode = node.ToolType.ToLowerInvariant() switch
                    {
                        "subroutine" => ConvertSubroutineNode(node),
                        "condition" => new ConditionNode(node.Id, node.Name, node.DispName ?? node.Name)
                        {
                            Parameters = node.Parameters
                        },
                        _ => throw new NotSupportedException(
                            $"不支持的控制流节点类型: {node.ToolType} (节点: {node.Name})")
                    };
                }
                else
                {
                    executionNode = WorkflowNodeFactory.CreateAlgorithmNode(
                        node.ToolType,
                        node.Id,
                        node.Name,
                        node.DispName ?? node.Name,
                        node.Parameters,
                        enableCaching: true,
                        enableRetry: false)
                        ?? throw new InvalidOperationException(
                            $"无法创建算法节点: ToolType={node.ToolType}, Name={node.Name}");
                }

                // 复制参数绑定配置
                executionNode.ParamSettings = node.ParamSettings;
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
        /// 将 UI 节点转换为 SubroutineNode
        /// </summary>
        private SubroutineNode ConvertSubroutineNode(WorkflowNodeBase node)
        {
            var subNode = new SubroutineNode(node.Id, node.Name, node.DispName ?? node.Name)
            {
                Parameters = node.Parameters,
                SubroutineId = node.SubroutineId
            };

            if (!string.IsNullOrEmpty(subNode.SubroutineId))
            {
                var targetWorkflow = _workflowEngine.GetWorkflow(subNode.SubroutineId);
                if (targetWorkflow != null)
                    subNode.SubroutineName = targetWorkflow.Name;
            }

            return subNode;
        }

        /// <summary>
        /// 执行单个节点（支持多种节点类型）
        /// </summary>
        /// <param name="node">工作流节点</param>
        /// <param name="context">工作流上下文</param>
        /// <param name="workflow">工作流实例</param>
        private async Task<NodeExecutionResult> ExecuteNode(
            WorkflowNodeBase node,
            WorkflowContext context,
            Workflow workflow)
        {
            var nodeResult = new NodeExecutionResult
            {
                NodeId = node.Id,
                StartTime = DateTime.Now
            };

            try
            {
                // 根据节点类型执行
                switch (node)
                {
                    case AlgorithmNode algorithmNode:
                        nodeResult = ExecuteAlgorithmNode(algorithmNode, context);
                        break;

                    case SubroutineNode subroutineNode:
                        var savedStartTime = nodeResult.StartTime;
                        nodeResult = await ExecuteSubroutineNode(subroutineNode, context, workflow);
                        nodeResult.StartTime = savedStartTime;
                        break;

                    case ConditionNode conditionNode:
                        nodeResult = ExecuteConditionNode(conditionNode, context);
                        break;

                    default:
                        throw new NotSupportedException($"不支持的节点类型: {node.GetType().Name}");
                }

                nodeResult.EndTime = DateTime.Now;
                return nodeResult;
            }
            catch (Exception ex)
            {
                nodeResult.EndTime = DateTime.Now;
                nodeResult.Success = false;
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
        private NodeExecutionResult ExecuteAlgorithmNode(AlgorithmNode node, WorkflowContext context)
        {
            var result = new NodeExecutionResult { NodeId = node.Id, StartTime = DateTime.Now, EndTime = DateTime.Now };

            // 从 SSOT (WorkflowContext) 构建工具结果字典用于参数绑定解析（过滤空结果）
            var toolResultsDict = context.GetAllNodeResults()
                .Where(kvp => kvp.Value.Result != null)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Result!);

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

            // 调用 AlgorithmNode.Execute，图像通过 ImageSource 绑定解析
            var algorithmResult = node.Execute(toolResultsDict);

            if (algorithmResult.Success && algorithmResult.ToolResults != null)
            {
                result.Success = true;
                result.Result = algorithmResult.ToolResults;
            }
            else if (!algorithmResult.Success)
            {
                result.ErrorMessages = new List<string> { algorithmResult.ErrorMessage };
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// 执行子程序节点
        /// </summary>
        private async Task<NodeExecutionResult> ExecuteSubroutineNode(
            SubroutineNode node,
            WorkflowContext context,
            Workflow workflow)
        {
            // 查找工作流控制插件
            var plugins = _pluginManager.GetPlugins<IWorkflowControlPlugin>();
            var plugin = plugins.FirstOrDefault();

            if (plugin == null)
            {
                throw new InvalidOperationException("未找到工作流控制插件");
            }

            // 从已执行节点结果中查找上游图像
            IVisionImage? inputImage = null;
            foreach (var kvp in context.GetAllNodeResults())
            {
                if (kvp.Value?.Result != null)
                {
                    var images = kvp.Value.Result.GetDisplayImages();
                    var firstImage = images.Values.FirstOrDefault(m => m != null && !m.IsEmpty);
                    if (firstImage != null)
                    {
                        inputImage = firstImage;
                        break;
                    }
                }
            }

            // 设置上下文变量和执行引擎引用
            context.SetVariable("InputImage", inputImage);
            context.SetVariable("CurrentNode", node);
            context.ExecutionEngine = this;

            // 执行子程序
            var result = await plugin.ExecuteSubroutine(node, context);

            var nodeResult = new NodeExecutionResult { NodeId = node.Id };

            if (result.Success)
            {
                nodeResult.Success = true;
                if (result.Outputs?.Any() == true)
                {
                    var subroutineResults = new GenericToolResults
                    {
                        Status = ExecutionStatus.Success
                    };
                    foreach (var output in result.Outputs)
                    {
                        subroutineResults.AddResultItem(output.Key, output.Value);
                    }
                    nodeResult.Result = subroutineResults;
                }
            }
            else
            {
                nodeResult.ErrorMessages = new List<string> {
                    result.Errors?.FirstOrDefault() ?? "子程序执行失败"
                };
            }

            return nodeResult;
        }

        /// <summary>
        /// 执行条件节点
        /// </summary>
        private NodeExecutionResult ExecuteConditionNode(
            ConditionNode node,
            WorkflowContext context)
        {
            // 查找工作流控制插件
            var plugins = _pluginManager.GetPlugins<IWorkflowControlPlugin>();
            var plugin = plugins.FirstOrDefault();

            if (plugin == null)
            {
                throw new InvalidOperationException("未找到工作流控制插件");
            }

            // 从已执行节点结果中查找上游图像
            IVisionImage? inputImage = null;
            foreach (var kvp in context.GetAllNodeResults())
            {
                if (kvp.Value?.Result != null)
                {
                    var images = kvp.Value.Result.GetDisplayImages();
                    var firstImage = images.Values.FirstOrDefault(m => m != null && !m.IsEmpty);
                    if (firstImage != null)
                    {
                        inputImage = firstImage;
                        break;
                    }
                }
            }

            // 设置上下文变量
            context.SetVariable("InputImage", inputImage);
            context.SetVariable("CurrentNode", node);

            // 评估条件
            var conditionResult = plugin.EvaluateCondition(node, context);

            var nodeResult = new NodeExecutionResult
            {
                NodeId = node.Id,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now,
                Success = true
            };
            var conditionResults = new GenericToolResults
            {
                Status = ExecutionStatus.Success
            };
            conditionResults.AddResultItem("ConditionResult", conditionResult);
            conditionResults.AddResultItem("Output", inputImage);
            nodeResult.Result = conditionResults;

            return nodeResult;
        }

        #endregion

        #region 异步执行

        /// <summary>
        /// 异步执行工作流
        /// </summary>
        public async Task<ExecutionResult> ExecuteWorkflowAsync(string workflowId, IVisionImage? inputImage,
            IDictionary<string, object?>? globalVariables = null)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            return await Task.Run(async () => await ExecuteWorkflow(workflowId, inputImage, globalVariables), _cancellationTokenSource.Token);
        }

        /// <summary>
        /// 暂停执行
        /// </summary>
        public void PauseExecution()
        {
            if (_currentState == WorkflowExecutionState.Running)
            {
                _currentState = WorkflowExecutionState.Paused;
                _logger.LogWarning("工作流执行已暂停");
            }
        }

        /// <summary>
        /// 恢复执行
        /// </summary>
        public void ResumeExecution()
        {
            if (_currentState == WorkflowExecutionState.Paused)
            {
                _currentState = WorkflowExecutionState.Running;
                _logger.LogWarning("工作流执行已恢复");
            }
        }

        /// <summary>
        /// 停止执行
        /// </summary>
        public void StopExecution()
        {
            _cancellationTokenSource?.Cancel();
            _logger.LogWarning("工作流执行停止请求已发送");
        }

        #endregion

        #region 状态查询

        /// <summary>
        /// 获取执行统计信息
        /// </summary>
        public ExecutionStatistics GetExecutionStatistics()
        {
            if (_currentContext == null)
            {
                return new ExecutionStatistics();
            }

            return _currentContext.GetStatistics();
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

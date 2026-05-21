using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Models;
using SunEyeVision.UI.ViewModels;
using SunEyeVision.Workflow;
using SunEyeVision.UI.Infrastructure;
using SunEyeVision.UI.Services.Workflow;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 工作流执行管理器
    /// 管理工作流的同步和异步执行
    /// </summary>
    public class WorkflowExecutionManager
    {
        private readonly IInputProvider _inputProvider;
        private readonly Dictionary<string, CancellationTokenSource> _cancellationTokens;
        private readonly Dictionary<string, Task> _runningTasks;
        private readonly object _lock = new object();
        private SynchronizationContext? _synchronizationContext;

        /// <summary>
        /// 当前运行时参数（从 MainWindowViewModel 设置）
        /// </summary>
        private Dictionary<string, object> _runtimeParameters = new();

        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionStarted;
        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionCompleted;
        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionStopped;
        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionError;
        public event EventHandler<WorkflowExecutionProgressEventArgs>? WorkflowExecutionProgress;

        /// <summary>
        /// 节点执行完成事件
        /// </summary>
        public event EventHandler<NodeExecutionResultEventArgs>? NodeExecutionCompleted;

        public WorkflowExecutionManager(IInputProvider inputProvider)
        {
            _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
            _cancellationTokens = new Dictionary<string, CancellationTokenSource>();
            _runningTasks = new Dictionary<string, Task>();
            _synchronizationContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// 设置运行时参数
        /// </summary>
        /// <param name="parameters">运行时参数字典</param>
        public void SetRuntimeParameters(Dictionary<string, object> parameters)
        {
            _runtimeParameters = parameters ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// 获取运行时参数
        /// </summary>
        public Dictionary<string, object> GetRuntimeParameters()
        {
            return new Dictionary<string, object>(_runtimeParameters);
        }

        /// <summary>
        /// 执行指定工作流
        /// </summary>
        public async Task RunSingleAsync(WorkflowTabViewModel workflow, CancellationToken cancellationToken = default)
        {
            if (workflow == null)
                throw new ArgumentNullException(nameof(workflow));

            var workflowId = workflow.Name;
            OnWorkflowExecutionStarted(workflowId);

            try
            {
                await ExecuteWorkflowInternal(workflow, cancellationToken);
                OnWorkflowExecutionCompleted(workflowId);
            }
            catch (OperationCanceledException)
            {
                OnWorkflowExecutionStopped(workflowId);
            }
            catch (Exception ex)
            {
                OnWorkflowExecutionError(workflowId, ex.Message);
            }
        }

        /// <summary>
        /// 开始指定工作流的连续执行
        /// </summary>
        public void StartContinuousRun(WorkflowTabViewModel workflow)
        {
            if (workflow == null)
                throw new ArgumentNullException(nameof(workflow));

            var workflowId = workflow.Name;

            lock (_lock)
            {
                // 如果已经在运行，先停止
                if (_runningTasks.ContainsKey(workflowId))
                {
                    StopContinuousRun(workflow);
                }

                var cts = new CancellationTokenSource();
                _cancellationTokens[workflowId] = cts;

                var task = Task.Run(async () =>
                {
                    OnWorkflowExecutionStarted(workflowId);

                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            await ExecuteWorkflowInternal(workflow, cts.Token);
                            OnWorkflowExecutionCompleted(workflowId);

                            // 短暂休息避免CPU占用过高
                            await Task.Delay(10, cts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        OnWorkflowExecutionStopped(workflowId);
                    }
                    catch (Exception ex)
                    {
                        OnWorkflowExecutionError(workflowId, ex.Message);
                    }
                    finally
                    {
                        lock (_lock)
                        {
                            _cancellationTokens.Remove(workflowId);
                            _runningTasks.Remove(workflowId);
                        }
                    }
                }, cts.Token);

                _runningTasks[workflowId] = task;
            }
        }

        /// <summary>
        /// 停止指定工作流的连续执行
        /// </summary>
        public void StopContinuousRun(WorkflowTabViewModel workflow)
        {
            if (workflow == null)
                throw new ArgumentNullException(nameof(workflow));

            var workflowId = workflow.Name;

            lock (_lock)
            {
                if (_cancellationTokens.TryGetValue(workflowId, out var cts))
                {
                    cts.Cancel();
                    _cancellationTokens.Remove(workflowId);
                }

                _runningTasks.Remove(workflowId);
            }
        }

        /// <summary>
        /// 开始所有工作流执行(单次)
        /// </summary>
        public async Task StartAllWorkflowsRun(IEnumerable<WorkflowTabViewModel> workflows)
        {
            if (workflows == null)
                throw new ArgumentNullException(nameof(workflows));

            var tasks = workflows.Select(w => RunSingleAsync(w)).ToList();
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 开始所有工作流的连续执行
        /// </summary>
        public void StartAllContinuousRun(IEnumerable<WorkflowTabViewModel> workflows)
        {
            if (workflows == null)
                throw new ArgumentNullException(nameof(workflows));

            foreach (var workflow in workflows)
            {
                StartContinuousRun(workflow);
            }
        }

        /// <summary>
        /// 停止所有工作流的连续执行
        /// </summary>
        public void StopAllContinuousRun()
        {
            lock (_lock)
            {
                foreach (var cts in _cancellationTokens.Values)
                {
                    cts.Cancel();
                }
                _cancellationTokens.Clear();
                _runningTasks.Clear();
            }
        }

        /// <summary>
        /// 判断指定工作流是否正在运行
        /// </summary>
        public bool IsRunning(string workflowId)
        {
            lock (_lock)
            {
                return _runningTasks.ContainsKey(workflowId);
            }
        }

        /// <summary>
        /// 判断是否有任何工作流正在运行
        /// </summary>
        public bool IsAnyRunning()
        {
            lock (_lock)
            {
                return _runningTasks.Count > 0;
            }
        }

        /// <summary>
        /// 执行工作流内部逻辑
        /// </summary>
        private async Task ExecuteWorkflowInternal(WorkflowTabViewModel workflow, CancellationToken cancellationToken)
        {
            // 在开始时先发送一条开始日志
            InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.Name, "[EXEC] ====== 开始执行工作流 ======")));

            try
            {
                // 获取输入图像（可能为 null，工作流引擎会处理）
                var inputImage = await _inputProvider.GetInputImageAsync();
                cancellationToken.ThrowIfCancellationRequested();

                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.Name, "[EXEC] 正在创建日志器...")));
                // 使用全局日志器（自动转发到UILogWriter）
                var logger = VisionLogger.Instance.ForSource(workflow.Name);

                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.Name, "[EXEC] 正在创建工作流引擎...")));
                var (workflowEngine, executionEngine, pluginManager) = WorkflowEngineFactory.CreateEngineSuite(logger);

                // 创建工作流
                var suneyeWorkflow = workflowEngine.CreateWorkflow(workflow.Name, workflow.Name);
                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.Name, $"[EXEC] 工作流已创建, ID: {suneyeWorkflow.Id}")));

                // 记录运行时参数
                foreach (var param in _runtimeParameters)
                {
                    logger.LogInfo($"运行时参数: {param.Key} = {param.Value}");
                }

                // 从UI节点创建AlgorithmNode
                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.Name, $"[EXEC] 正在添加节点 (总数: {workflow.WorkflowNodes.Count})...")));
                foreach (var uiNode in workflow.WorkflowNodes)
                {
                    var algorithmNode = CreateAlgorithmNodeFromUiNode(uiNode, logger);
                    if (algorithmNode != null)
                    {
                        suneyeWorkflow.AddNode(algorithmNode);
                        logger.LogInfo($"添加节点: {uiNode.Name} (ID: {uiNode.Id})");
                    }
                }

                // 添加UI连接
                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.Name, $"[EXEC] 正在添加连接 (总数: {workflow.WorkflowConnections.Count})...")));
                foreach (var uiConnection in workflow.WorkflowConnections)
                {
                    try
                    {
                        suneyeWorkflow.ConnectNodes(uiConnection.SourceNodeId, uiConnection.TargetNodeId);
                        logger.LogInfo($"添加连接: {uiConnection.SourceNodeId} -> {uiConnection.TargetNodeId}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"添加连接失败: {ex.Message}");
                    }
                }

                // 执行
                logger.LogInfo($"开始执行工作流: {workflow.Name}");
                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.Name, "[EXEC] 开始执行工作流...")));

                var result = await executionEngine.ExecuteWorkflowAsync(suneyeWorkflow.Id, inputImage as IVisionImage);

                // 输出结果
                logger.LogInfo($"工作流执行完成: {workflow.Name}");
                logger.LogInfo($"执行结果: {(result.Success ? "成功" : "失败")}");
                logger.LogInfo($"执行时间: {result.ExecutionTime.TotalMilliseconds:F2} ms");

                // 触发节点执行结果事件
                if (result.NodeResults != null && result.NodeResults.Count > 0)
                {
                    foreach (var nodeResult in result.NodeResults)
                    {
                        var node = workflow.WorkflowNodes.FirstOrDefault(n => n.Id == nodeResult.Key);
                        if (node != null)
                        {
                            // 创建一个包含结果的 ToolResults
                            var toolResults = CreateToolResultsFromNodeResult(nodeResult.Value);
                            OnNodeExecutionCompleted(node, toolResults);
                        }
                    }
                }

                if (!result.Success && result.Errors.Any())
                {
                    InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.Name, $"[ERROR] 工作流执行失败，错误数量: {result.Errors.Count}")));
                    foreach (var error in result.Errors)
                    {
                        logger.LogError($"错误: {error}");
                    }
                }
                else
                {
                    InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.Name, "[EXEC] ====== 工作流执行结束 ======")));
                }
            }
            catch (Exception ex)
            {
                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.Name, $"[ERROR] ExecuteWorkflowInternal异常: {ex.Message}")));
                throw;
            }
        }

        /// <summary>
        /// 创建测试用的Mat — 已废弃，工作流不再提供默认输入图像
        /// </summary>
        // 已删除：工作流不再提供默认输入图像，缺少输入图像的节点将直接报错

        /// <summary>
        /// 从UI节点创建AlgorithmNode
        /// </summary>
        private SunEyeVision.Workflow.AlgorithmNode? CreateAlgorithmNodeFromUiNode(
            Models.WorkflowNode uiNode,
            SunEyeVision.Plugin.SDK.Logging.ILogger logger)
        {
            try
            {
                // 使用WorkflowNodeFactory创建节点(使用真实插件)
                var toolId = uiNode.ToolType ?? uiNode.Name;
                var node = SunEyeVision.Workflow.WorkflowNodeFactory.CreateAlgorithmNode(
                    toolId,
                    uiNode.Id,
                    uiNode.Name,
                    uiNode.DispName,
                    uiNode.Parameters,  // ✅ 修复：传递UI节点的参数
                    enableCaching: true,
                    enableRetry: false
                );

                if (node != null)
                {
                    logger.LogInfo($"通过WorkflowNodeFactory创建节点: {uiNode.Name} (ToolId: {toolId})");

                    // ★ 为图像源节点注入运行时参数
                    if (IsImageSourceNode(toolId))
                    {
                        // 获取运行时图像路径
                        if (_runtimeParameters.TryGetValue("CurrentImagePath", out var imagePath) && imagePath is string path && !string.IsNullOrEmpty(path))
                        {
                            // 使用反射设置属性值
                            var prop = node.Parameters.GetType().GetProperty("FilePath");
                            if (prop != null && prop.CanWrite)
                            {
                                prop.SetValue(node.Parameters, path);
                                logger.LogInfo($"★ 注入图像路径到节点 {uiNode.Name}: {path}");
                            }
                        }
                    }

                    return node;
                }
                else
                {
                    // 工具不存在
                    logger.LogError($"工具不存在: {toolId}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"创建AlgorithmNode失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 判断是否为图像源节点
        /// </summary>
        private bool IsImageSourceNode(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return false;
            
            var lowerToolId = toolId.ToLowerInvariant();
            return lowerToolId.Contains("imageload") ||
                   lowerToolId.Contains("image_load") ||
                   lowerToolId.Contains("imagecapture") ||
                   lowerToolId.Contains("image_capture") ||
                   lowerToolId.Contains("image_acquire") ||
                   lowerToolId == "image_source";
        }

        private void OnWorkflowExecutionStarted(string workflowId)
        {
            InvokeOnUI(() => WorkflowExecutionStarted?.Invoke(this, new WorkflowExecutionEventArgs(workflowId)));
        }

        private void OnWorkflowExecutionCompleted(string workflowId)
        {
            InvokeOnUI(() => WorkflowExecutionCompleted?.Invoke(this, new WorkflowExecutionEventArgs(workflowId)));
        }

        private void OnWorkflowExecutionStopped(string workflowId)
        {
            InvokeOnUI(() => WorkflowExecutionStopped?.Invoke(this, new WorkflowExecutionEventArgs(workflowId)));
        }

        private void OnWorkflowExecutionError(string workflowId, string errorMessage)
        {
            InvokeOnUI(() => WorkflowExecutionError?.Invoke(this, new WorkflowExecutionEventArgs(workflowId, errorMessage)));
        }

        /// <summary>
        /// 触发节点执行完成事件
        /// </summary>
        private void OnNodeExecutionCompleted(Models.WorkflowNode node, ToolResults result)
        {
            InvokeOnUI(() => NodeExecutionCompleted?.Invoke(this, new NodeExecutionResultEventArgs(node, result)));
        }

        /// <summary>
        /// 从节点执行结果创建 ToolResults
        /// </summary>
        private ToolResults CreateToolResultsFromNodeResult(NodeExecutionResult nodeResult)
        {
            // ★ 关键日志：节点执行结果状态
// 优先使用原始工具结果
            if (nodeResult.ToolResult != null)
            {
return nodeResult.ToolResult;
            }

            var results = new GenericToolResults
            {
                Status = nodeResult.Success ? ExecutionStatus.Success : ExecutionStatus.Failed,
                ExecutionTimeMs = (long)(nodeResult.Duration?.TotalMilliseconds ?? 0),
                Timestamp = DateTime.Now
            };

            // 使用输出数据
            if (nodeResult.Outputs != null)
            {
                foreach (var output in nodeResult.Outputs)
                {
                    results.AddResultItem(output.Key, output.Value, ResultItemType.Object);
                }
            }

            // 添加错误消息
            if (nodeResult.ErrorMessages != null)
            {
                foreach (var error in nodeResult.ErrorMessages)
                {
                    results.AddWarning(error);
                }
                if (nodeResult.ErrorMessages.Count > 0)
                {
                    results.ErrorMessage = string.Join("; ", nodeResult.ErrorMessages);
                }
            }

            return results;
        }

        private void InvokeOnUI(Action action)
        {
            if (_synchronizationContext != null)
            {
                _synchronizationContext.Post(_ => action(), null);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            StopAllContinuousRun();

            lock (_lock)
            {
                foreach (var cts in _cancellationTokens.Values)
                {
                    cts.Dispose();
                }
                _cancellationTokens.Clear();
            }
        }
    }

    /// <summary>
    /// 工作流执行事件参数
    /// </summary>
    public class WorkflowExecutionEventArgs : EventArgs
    {
        public string WorkflowId { get; }
        public string? ErrorMessage { get; }
        public DateTime Timestamp { get; }

        public WorkflowExecutionEventArgs(string workflowId, string? errorMessage = null)
        {
            WorkflowId = workflowId;
            ErrorMessage = errorMessage;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 工作流执行进度事件参数
    /// </summary>
    public class WorkflowExecutionProgressEventArgs : EventArgs
    {
        public string WorkflowId { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public WorkflowExecutionProgressEventArgs(string workflowId, string message)
        {
            WorkflowId = workflowId;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 节点执行结果事件参数
    /// </summary>
    public class NodeExecutionResultEventArgs : EventArgs
    {
        /// <summary>
        /// UI 节点模型
        /// </summary>
        public Models.WorkflowNode Node { get; }

        /// <summary>
        /// 执行结果
        /// </summary>
        public ToolResults Result { get; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public NodeExecutionResultEventArgs(Models.WorkflowNode node, ToolResults result)
        {
            Node = node;
            Result = result;
            Timestamp = DateTime.Now;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shell;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 工作流执行管理器
    /// 管理工作流的同步和异步执行
    /// </summary>
    public class WorkflowExecutionManager
    {
        private readonly IInputProvider _inputProvider;
        private readonly WorkflowEngine _workflowEngine;
        private readonly Dictionary<string, CancellationTokenSource> _cancellationTokens;
        private readonly Dictionary<string, Task> _runningTasks;
        private readonly object _lock = new object();
        private SynchronizationContext? _synchronizationContext;

        /// <summary>
        /// 当前运行时参数（从 MainWindowViewModel 设置）
        /// </summary>
        private Dictionary<string, object> _runtimeParameters = new();

        /// <summary>
        /// 当前全局变量（从 SolutionManager 获取，用于 __GlobalVars__ 绑定解析）
        /// </summary>
        private IDictionary<string, object?>? _globalVariables;

        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionStarted;
        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionCompleted;
        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionStopped;
        public event EventHandler<WorkflowExecutionEventArgs>? WorkflowExecutionError;
        public event EventHandler<WorkflowExecutionProgressEventArgs>? WorkflowExecutionProgress;

        /// <summary>
        /// 节点执行完成事件
        /// </summary>
        public event EventHandler<NodeExecutionResultEventArgs>? NodeExecutionCompleted;

        public WorkflowExecutionManager(IInputProvider inputProvider, WorkflowEngine workflowEngine)
        {
            _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
            _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
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
        /// 设置全局变量（用于 __GlobalVars__ 绑定解析）
        /// </summary>
        /// <param name="globalVariables">全局变量字典（变量名 -> 变量值）</param>
        public void SetGlobalVariables(IDictionary<string, object?>? globalVariables)
        {
            _globalVariables = globalVariables;
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

            var workflowId = workflow.RootWorkflowId;
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

            var workflowId = workflow.RootWorkflowId;

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

            var workflowId = workflow.RootWorkflowId;

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
        /// 执行工作流内部逻辑 — 始终从根工作流（RootWorkflowId）读取节点
        /// </summary>
        private async Task ExecuteWorkflowInternal(WorkflowTabViewModel workflow, CancellationToken cancellationToken)
        {
            // 在开始时先发送一条开始日志
            InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] ====== 开始执行工作流 ======")));

            try
            {
                // 获取输入图像（可能为 null，工作流引擎会处理）
                var inputImage = await _inputProvider.GetInputImageAsync();
                cancellationToken.ThrowIfCancellationRequested();

                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] 正在创建日志器...")));
                // 使用全局日志器（自动转发到UILogWriter）
                var logger = VisionLogger.Instance.ForSource(workflow.Name);

                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] 正在创建执行引擎...")));
                // ★ Phase 1: 使用共享的 _workflowEngine（子程序工作流已注册在其中），不再创建新引擎
                var pluginManager = new PluginManager(logger);
                var executionEngine = new WorkflowExecutionEngine(_workflowEngine, pluginManager, logger);

                // ★ 从引擎获取根工作流（始终读取 RootWorkflowId 对应的节点）
                var rootEngineWorkflow = _workflowEngine.GetWorkflow(workflow.RootWorkflowId);
                if (rootEngineWorkflow == null)
                {
                    logger.LogError($"根工作流未找到: {workflow.RootWorkflowId}");
                    return;
                }

                // ★ 订阅节点状态事件：引擎状态 → 画布节点（解决状态断点）
                //   引擎在 Running/Completed/Failed/Skipped 时发事件，此处映射到 UI 节点 Status。
                //   找不到节点记日志跳过（Fail-Forward），不中断流程。
                executionEngine.NodeStatusChanged += (_, e) =>
                {
                    var node = FindNodeRecursive(rootEngineWorkflow, e.NodeId);
                    if (node is SunEyeVision.UI.Shared.WorkflowNode uiNode)
                    {
                        InvokeOnUI(() =>
                        {
                            uiNode.Status = e.Status switch
                            {
                                NodeStatus.Running => "运行中",
                                NodeStatus.Completed => "成功",
                                NodeStatus.Failed => "失败",
                                NodeStatus.Skipped => "跳过",
                                _ => uiNode.Status
                            };
                        });
                    }
                    else
                    {
                        logger.LogWarning($"节点状态事件找不到画布节点: {e.NodeId}");
                    }
                };

                // 记录运行时参数
                foreach (var param in _runtimeParameters)
                {
                    logger.LogInfo($"运行时参数: {param.Key} = {param.Value}");
                }

                // ★ 从各节点的 InputSource 递归注入路径（每个 ImageLoad 节点独立控制）
                InjectFilePathFromInputSource(rootEngineWorkflow, logger, new HashSet<string>());

                // ★ 引擎内部统一完成节点类型转换，主/子工作流共享同一逻辑
                logger.LogInfo($"开始执行工作流: {rootEngineWorkflow.Name}");
                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] 开始执行工作流...")));

                var result = await executionEngine.ExecuteWorkflowAsync(workflow.RootWorkflowId, null, _globalVariables);

                // 输出结果
                logger.LogInfo($"工作流执行完成: {rootEngineWorkflow.Name}");
                logger.LogInfo($"执行结果: {(result.Success ? "成功" : "失败")}");
                logger.LogInfo($"执行时间: {result.ExecutionTime.TotalMilliseconds:F2} ms");

                // 触发节点执行结果事件
                if (result.NodeResults != null && result.NodeResults.Count > 0)
                {
                    foreach (var nodeResult in result.NodeResults)
                    {
                        var node = FindNodeRecursive(rootEngineWorkflow, nodeResult.Key);
                        if (node is SunEyeVision.UI.Shared.WorkflowNode uiNode)
                        {
                            // 创建一个包含结果的 ToolResults
                            var toolResults = CreateToolResultsFromNodeResult(nodeResult.Value);
                            // 方案 §6 画布三态：流程控制结果 → node.Status（OK=命中/NG=未命中）。
                            // 用 SDK 层 IControlFlowResult 接口判断（UI 不直接引用工具项目），
                            // NextNodeIds 非空 = 命中 → OK；空 = 未命中 → NG。
                            if (toolResults is IControlFlowResult flowResult)
                            {
                                uiNode.Status = flowResult.NextNodeIds != null && flowResult.NextNodeIds.Count > 0 ? "OK" : "NG";
                            }
                            OnNodeExecutionCompleted(uiNode, toolResults);
                        }
                    }
                }

                if (!result.Success && result.Errors.Any())
                {
                    InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, $"[ERROR] 工作流执行失败，错误数量: {result.Errors.Count}")));
                    foreach (var error in result.Errors)
                    {
                        logger.LogError($"错误: {error}");
                    }
                }
                else
                {
                    InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, "[EXEC] ====== 工作流执行结束 ======")));
                }
            }
            catch (Exception ex)
            {
                InvokeOnUI(() => WorkflowExecutionProgress?.Invoke(this, new WorkflowExecutionProgressEventArgs(workflow.RootWorkflowId, $"[ERROR] ExecuteWorkflowInternal异常: {ex.Message}")));
                throw;
            }
        }

        /// <summary>
        /// 创建测试用的Mat — 已废弃，工作流不再提供默认输入图像
        /// </summary>
        // 已删除：工作流不再提供默认输入图像，缺少输入图像的节点将直接报错

        /// <summary>
        /// 递归遍历工作流（含子工作流），从每个 ImageLoad 节点的 InputSource
        /// 读取当前选中的图像路径，注入到 Parameters.FilePath
        /// </summary>
        /// <remarks>
        /// 使用 WorkflowNodeBase 基类而非 SunEyeVision.UI.Shared.WorkflowNode，确保子程序工作流中的节点
        /// （类型为 WorkflowNodeBase）也能正确注入路径。
        /// IsImageLoadNode 通过 ToolType 字符串判断（在基类上可访问）。
        /// InputSource 在 WorkflowNodeBase 上为 object?，通过 as 转换访问。
        /// </remarks>
        private void InjectFilePathFromInputSource(
            SunEyeVision.Workflow.Workflow workflow, ILogger logger, HashSet<string> visitedIds)
        {
            if (workflow == null || !visitedIds.Add(workflow.Id)) return;

            foreach (var wn in workflow.Nodes)
            {
                if (wn == null) continue;

                // ★ 递归优先：子工作流遍历必须在 IsImageLoadToolType 判断之前，
                //   确保 Subroutine 节点（非 ImageLoad 类型）也能触发子工作流递归
                if (!string.IsNullOrEmpty(wn.SubroutineId))
                {
                    var subWorkflow = _workflowEngine.GetWorkflow(wn.SubroutineId);
                    if (subWorkflow != null)
                        InjectFilePathFromInputSource(subWorkflow, logger, visitedIds);
                }

                // 通过 ToolType 判断是否为图像载入节点（基类属性，无需强制转换为 SunEyeVision.UI.Shared.WorkflowNode）
                if (!IsImageLoadToolType(wn.ToolType))
                    continue;

                // 解析节点参数中的 FilePath 属性（ImageLoadParameters.FilePath）
                var prop = wn.Parameters?.GetType().GetProperty("FilePath");
                if (prop == null || !prop.CanWrite)
                    continue;

                var inputSource = wn.InputSource as ImageInputSource;
                var currentImage = inputSource?.CurrentImage;

                // InputSource 有当前图 → 注入其路径到 Parameters.FilePath
                if (currentImage != null && !string.IsNullOrEmpty(currentImage.FilePath))
                {
                    prop.SetValue(wn.Parameters, currentImage.FilePath);
                    logger.LogInfo($"从 InputSource 注入路径: '{wn.Name}' -> '{currentImage.FilePath}'");
                }
                else
                {
                    // ★ 治本（rule-015 报错优先）：InputSource 为空/被清空时，显式清空节点参数 FilePath，
                    // 防止运行时读到上次残留的旧路径，导致"清空图像源后运行仍读到旧图"。
                    // ImageLoadTool.Run 检测到 FilePath 为空会返回"文件路径为空"错误，用户明确感知图像源为空。
                    prop.SetValue(wn.Parameters, string.Empty);
                    logger.LogWarning($"InputSource 无有效图像，已清空 '{wn.Name}' 的 FilePath，运行时将报文件路径为空");
                }
            }
        }

        /// <summary>
        /// 判断工具类型是否为图像载入节点（基于 ToolType 字符串匹配）
        /// 用于 WorkflowNodeBase 基类，无需强制转换为 SunEyeVision.UI.Shared.WorkflowNode
        /// </summary>
        private static bool IsImageLoadToolType(string toolType)
        {
            return toolType != null && (
                toolType.Contains("ImageLoad", StringComparison.OrdinalIgnoreCase) ||
                toolType.Contains("图片载入", StringComparison.OrdinalIgnoreCase) ||
                toolType.Contains("ImageSource", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 递归在工作流树中查找指定 ID 的节点（含子工作流）
        /// </summary>
        private SunEyeVision.Workflow.WorkflowNodeBase? FindNodeRecursive(
            SunEyeVision.Workflow.Workflow workflow, string nodeId, HashSet<string>? visited = null)
        {
            visited ??= new HashSet<string>();
            if (!visited.Add(workflow.Id)) return null;

            var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null) return node;

            // 递归搜索子工作流
            foreach (var n in workflow.Nodes)
            {
                if (!string.IsNullOrEmpty(n.SubroutineId))
                {
                    var subWf = _workflowEngine.GetWorkflow(n.SubroutineId);
                    if (subWf != null)
                    {
                        var found = FindNodeRecursive(subWf, nodeId, visited);
                        if (found != null) return found;
                    }
                }
            }

            return null;
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
        private void OnNodeExecutionCompleted(SunEyeVision.UI.Shared.WorkflowNode node, ToolResults result)
        {
            InvokeOnUI(() => NodeExecutionCompleted?.Invoke(this, new NodeExecutionResultEventArgs(node, result)));
        }

        /// <summary>
        /// 从节点执行结果创建 ToolResults
        /// </summary>
        private ToolResults CreateToolResultsFromNodeResult(NodeExecutionResult nodeResult)
        {
            // 优先使用原始工具结果
            if (nodeResult.Result != null)
            {
                return nodeResult.Result;
            }

            var results = new GenericToolResults
            {
                Status = nodeResult.Success ? ExecutionStatus.Success : ExecutionStatus.Failed,
                ExecutionTimeMs = (long)(nodeResult.Duration?.TotalMilliseconds ?? 0),
                Timestamp = DateTime.Now
            };

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
        public SunEyeVision.UI.Shared.WorkflowNode Node { get; }

        /// <summary>
        /// 执行结果
        /// </summary>
        public ToolResults Result { get; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public NodeExecutionResultEventArgs(SunEyeVision.UI.Shared.WorkflowNode node, ToolResults result)
        {
            Node = node;
            Result = result;
            Timestamp = DateTime.Now;
        }
    }
}

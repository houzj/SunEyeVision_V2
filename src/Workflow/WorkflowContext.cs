using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using LogLevel = SunEyeVision.Plugin.SDK.Logging.LogLevel;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 子程序调用信息
    /// </summary>
    public class SubroutineCallInfo
    {
        /// <summary>
        /// 子程序ID
        /// </summary>
        public string SubroutineId { get; set; }

        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; }

        /// <summary>
        /// 调用深度
        /// </summary>
        public int CallDepth { get; set; }

        /// <summary>
        /// 调用时间
        /// </summary>
        public DateTime CallTime { get; set; }

        /// <summary>
        /// 输入参数
        /// </summary>
        public Dictionary<string, object> InputParameters { get; set; }

        /// <summary>
        /// 输出结果
        /// </summary>
        public Dictionary<string, object> OutputResults { get; set; }

        /// <summary>
        /// 调用状态
        /// </summary>
        public CallStatus Status { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string Error { get; set; }

        public SubroutineCallInfo()
        {
            InputParameters = new Dictionary<string, object>();
            OutputResults = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// 调用状态
    /// </summary>
    public enum CallStatus
    {
        /// <summary>
        /// 调用中
        /// </summary>
        Calling,

        /// <summary>
        /// 执行中
        /// </summary>
        Executing,

        /// <summary>
        /// 完成
        /// </summary>
        Completed,

        /// <summary>
        /// 失败
        /// </summary>
        Failed,
        
        /// <summary>
        /// 已停止
        /// </summary>
        Stopped
    }

    /// <summary>
    /// 节点执行状态（内部使用）
    /// </summary>
    public class NodeExecutionStatusInternal
    {
        /// <summary>
        /// 状态
        /// </summary>
        public NodeStatus Status { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 执行时长
        /// </summary>
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

        public NodeExecutionStatusInternal()
        {
        }
    }

    /// <summary>
    /// 节点状态
    /// </summary>
    public enum NodeStatus
    {
        /// <summary>
        /// 等待中
        /// </summary>
        Pending,

        /// <summary>
        /// 执行中
        /// </summary>
        Running,

        /// <summary>
        /// 已完成
        /// </summary>
        Completed,

        /// <summary>
        /// 失败
        /// </summary>
        Failed,

        /// <summary>
        /// 已跳过
        /// </summary>
        Skipped
    }

    /// <summary>
    /// 执行路径项
    /// </summary>
    public class ExecutionPathItem
    {
        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; }

        /// <summary>
        /// 节点类型
        /// </summary>
        public string NodeType { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 执行时长
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 输出数据摘要
        /// </summary>
        public string OutputSummary { get; set; }
    }

    /// <summary>
    /// 工作流执行上下文
    /// </summary>
    public class WorkflowContext : IRuntimeParameterProvider
    {
        /// <summary>
        /// 运行时参数存储
        /// </summary>
        private readonly Dictionary<string, object> _runtimeParameters = new();

        /// <summary>
        /// 节点执行结果存储（新增）
        /// </summary>
        private readonly Dictionary<string, NodeExecutionResult> _executionResults = new();
        private readonly object _runtimeParametersGate = new();
        private readonly object _nodeResultsGate = new();
        private readonly Dictionary<string, IDisposable?> _resultLeases = new();
        private readonly object _nodeStatesGate = new();
        private readonly object _logsGate = new();

        /// <summary>
        /// 工作流ID
        /// </summary>
        public string WorkflowId { get; set; }

        /// <summary>
        /// 工作流名称
        /// </summary>
        public string WorkflowName { get; set; }

        /// <summary>
        /// 执行ID（唯一标识一次执行）
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// 执行开始时间
        /// </summary>
        public DateTime ExecutionStartTime { get; set; }

        /// <summary>
        /// 全局变量
        /// </summary>
        public Dictionary<string, object> Variables { get; set; }

        /// <summary>
        /// 解决方案级别全局变量（用于 __GlobalVars__ 参数绑定解析）
        /// </summary>
        public IDictionary<string, object?>? GlobalVariables { get; set; }

        /// <summary>
        /// 子程序调用栈
        /// </summary>
        public Stack<SubroutineCallInfo> CallStack { get; set; }

        /// <summary>
        /// 执行路径
        /// </summary>
        public List<ExecutionPathItem> ExecutionPath { get; set; }

        /// <summary>
        /// 节点执行状态
        /// </summary>
        public Dictionary<string, NodeExecutionStatusInternal> NodeStates { get; set; }

        /// <summary>
        /// 执行引擎引用（由 WorkflowExecutionEngine 设置，供子程序插件使用）
        /// </summary>
        public WorkflowExecutionEngine ExecutionEngine { get; set; }

        /// <summary>
        /// 已访问的工作流 ID 集合（Phase 2：用于跨工作流循环依赖检测）
        /// </summary>
        public HashSet<string> VisitedWorkflowIds { get; set; } = new();

        /// <summary>
        /// 取消令牌
        /// </summary>
        public System.Threading.CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// 进度报告器
        /// </summary>
        public IProgress<ExecutionProgress> ProgressReporter { get; set; }

        /// <summary>
        /// 执行日志
        /// </summary>
        public List<ExecutionLog> Logs { get; set; }

        /// <summary>
        /// 执行元数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// 是否启用调试
        /// </summary>
        public bool IsDebugMode { get; set; }

        /// <summary>
        /// 是否启用性能分析
        /// </summary>
        public bool EnableProfiling { get; set; }

        public WorkflowContext()
        {
            ExecutionId = Guid.NewGuid().ToString();
            ExecutionStartTime = DateTime.Now;
            Variables = new Dictionary<string, object>();
            CallStack = new Stack<SubroutineCallInfo>();
            ExecutionPath = new List<ExecutionPathItem>();
            NodeStates = new Dictionary<string, NodeExecutionStatusInternal>();
            Logs = new List<ExecutionLog>();
            Metadata = new Dictionary<string, object>();
            IsDebugMode = false;
            EnableProfiling = false;
        }

        /// <summary>
        /// 设置变量值
        /// </summary>
        public void SetVariable(string key, object value)
        {
            Variables[key] = value;
        }

        /// <summary>
        /// 获取变量值
        /// </summary>
        public object GetVariable(string key)
        {
            return Variables.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// 获取变量值（指定类型）
        /// </summary>
        public T GetVariable<T>(string key)
        {
            var value = GetVariable(key);
            if (value is T typedValue)
            {
                return typedValue;
            }
            return default(T);
        }

        /// <summary>
        /// 检查变量是否存在
        /// </summary>
        public bool HasVariable(string key)
        {
            return Variables.ContainsKey(key);
        }

        /// <summary>
        /// 移除变量
        /// </summary>
        public bool RemoveVariable(string key)
        {
            return Variables.Remove(key);
        }

        /// <summary>
        /// 获取当前调用深度
        /// </summary>
        public int GetCurrentCallDepth()
        {
            return CallStack.Count;
        }

        /// <summary>
        /// 推送调用信息到栈
        /// </summary>
        public void PushCallInfo(SubroutineCallInfo callInfo)
        {
            CallStack.Push(callInfo);
        }

        /// <summary>
        /// 从栈弹出调用信息
        /// </summary>
        public SubroutineCallInfo PopCallInfo()
        {
            return CallStack.Count > 0 ? CallStack.Pop() : null;
        }

        /// <summary>
        /// 获取当前调用信息
        /// </summary>
        public SubroutineCallInfo GetCurrentCallInfo()
        {
            return CallStack.Count > 0 ? CallStack.Peek() : null;
        }

        /// <summary>
        /// 更新节点状态
        /// </summary>
        public void UpdateNodeStatus(string nodeId, NodeStatus status, ExecutionResult result = null)
        {
            lock (_nodeStatesGate)
            {
                if (!NodeStates.ContainsKey(nodeId))
                {
                    NodeStates[nodeId] = new NodeExecutionStatusInternal
                    {
                        StartTime = DateTime.Now,
                        Status = status
                    };
                }

                var nodeStatus = NodeStates[nodeId];
                nodeStatus.Status = status;

                if (status == NodeStatus.Completed || status == NodeStatus.Failed)
                    nodeStatus.EndTime = DateTime.Now;

                if (status == NodeStatus.Failed && result != null && result.Errors.Any())
                {
                    // 节点执行失败，错误信息已记录在result.Errors中
                }
            }
        }

        /// <summary>
        /// 获取节点执行状态（未记录时返回 Pending）
        /// </summary>
        public NodeStatus GetNodeStatus(string nodeId)
        {
            lock (_nodeStatesGate)
                return NodeStates.TryGetValue(nodeId, out var state) ? state.Status : NodeStatus.Pending;
        }

        /// <summary>
        /// 添加执行日志
        /// </summary>
        public void AddLog(string message, LogLevel level = LogLevel.Info)
        {
            // 构建层级来源
            var source = GetDefaultLogSource();

            // 同步到VisionLogger（全局日志系统）
            VisionLogger.Instance.Log(level, message, source);

            // 同时保留执行日志（用于工作流执行追踪）
            lock (_logsGate) Logs.Add(new ExecutionLog
            {
                Timestamp = DateTime.Now,
                Message = message,
                Level = level,
                ExecutionId = ExecutionId,
                Source = source
            });
        }

        /// <summary>
        /// 添加执行日志（带来源参数）
        /// </summary>
        public void AddLog(string message, LogLevel level, string? source)
        {
            var actualSource = source ?? GetDefaultLogSource();

            // 同步到VisionLogger
            VisionLogger.Instance.Log(level, message, actualSource);

            // 保留执行日志
            lock (_logsGate) Logs.Add(new ExecutionLog
            {
                Timestamp = DateTime.Now,
                Message = message,
                Level = level,
                ExecutionId = ExecutionId,
                Source = actualSource
            });
        }

        /// <summary>
        /// 获取默认日志来源
        /// </summary>
        private string GetDefaultLogSource()
        {
            var workflowName = WorkflowName ?? WorkflowId ?? "未知工作流";

            // 尝试获取当前节点名称
            var currentCall = GetCurrentCallInfo();
            if (currentCall != null && !string.IsNullOrEmpty(currentCall.NodeId))
            {
                return LogSource.Runtime(workflowName, currentCall.NodeId);
            }

            return LogSource.Runtime(workflowName);
        }

        /// <summary>
        /// 添加执行路径项
        /// </summary>
        public void AddExecutionPathItem(ExecutionPathItem item)
        {
            ExecutionPath.Add(item);
        }

        /// <summary>
        /// 报告进度
        /// </summary>
        public void ReportProgress(ExecutionProgress progress)
        {
            ProgressReporter?.Report(progress);
        }

        /// <summary>
        /// 检查是否已取消
        /// </summary>
        public bool IsCancellationRequested()
        {
            return CancellationToken.IsCancellationRequested;
        }

        /// <summary>
        /// 获取执行统计信息
        /// </summary>
        public ExecutionStatistics GetStatistics()
        {
            NodeExecutionStatusInternal[] nodeStates;
            int logCount;
            lock (_nodeStatesGate) nodeStates = NodeStates.Values.ToArray();
            lock (_logsGate) logCount = Logs.Count;
            var stats = new ExecutionStatistics
            {
                ExecutionId = ExecutionId,
                StartTime = ExecutionStartTime,
                EndTime = DateTime.Now,
                TotalNodes = nodeStates.Length,
                CompletedNodes = nodeStates.Count(n => n.Status == NodeStatus.Completed),
                FailedNodes = nodeStates.Count(n => n.Status == NodeStatus.Failed),
                SkippedNodes = nodeStates.Count(n => n.Status == NodeStatus.Skipped),
                TotalLogs = logCount
            };

            stats.Duration = stats.EndTime - stats.StartTime;
            stats.CallDepth = CallStack.Count;

            return stats;
        }

        #region IRuntimeParameterProvider 实现

        /// <summary>
        /// 获取运行时参数值
        /// </summary>
        public T? GetRuntimeParameter<T>(string key)
        {
            lock (_runtimeParametersGate)
                return _runtimeParameters.TryGetValue(key, out var value) && value is T typed ? typed : default;
        }

        /// <summary>
        /// 设置运行时参数值
        /// </summary>
        public void SetRuntimeParameter<T>(string key, T value)
        {
            lock (_runtimeParametersGate) _runtimeParameters[key] = value!;
        }

        /// <summary>
        /// 检查运行时参数是否存在
        /// </summary>
        public bool HasRuntimeParameter(string key)
        {
            lock (_runtimeParametersGate) return _runtimeParameters.ContainsKey(key);
        }

        /// <summary>
        /// 移除运行时参数
        /// </summary>
        public bool RemoveRuntimeParameter(string key)
        {
            lock (_runtimeParametersGate) return _runtimeParameters.Remove(key);
        }

        /// <summary>
        /// 清除所有运行时参数
        /// </summary>
        public void ClearRuntimeParameters()
        {
            lock (_runtimeParametersGate) _runtimeParameters.Clear();
        }

        #endregion

        #region 节点执行结果管理（新增）

        /// <summary>
        /// 存储节点执行结果
        /// </summary>
        public void SetNodeResult(string nodeId, NodeExecutionResult result)
        {
            IDisposable? previous;
            lock (_nodeResultsGate)
            {
                var lease = result.Result?.Resources?.Retain();
                _resultLeases.Remove(nodeId, out previous);
                _resultLeases[nodeId] = lease;
                _executionResults[nodeId] = result;
            }
            previous?.Dispose();
        }

        /// <summary>
        /// 获取节点执行结果
        /// </summary>
        public NodeExecutionResult? GetNodeResult(string nodeId)
        {
            lock (_nodeResultsGate)
                return _executionResults.TryGetValue(nodeId, out var result) ? result : null;
        }

        /// <summary>
        /// 获取节点执行结果（指定类型）
        /// </summary>
        public T? GetNodeResult<T>(string nodeId) where T : ToolResults
        {
            var result = GetNodeResult(nodeId);
            if (result?.Result is T typedResult)
            {
                return typedResult;
            }
            return null;
        }

        /// <summary>
        /// 检查节点是否有执行结果
        /// </summary>
        public bool HasNodeResult(string nodeId)
        {
            lock (_nodeResultsGate) return _executionResults.ContainsKey(nodeId);
        }

        /// <summary>
        /// 移除节点执行结果
        /// </summary>
        public bool RemoveNodeResult(string nodeId)
        {
            IDisposable? lease;
            bool removed;
            lock (_nodeResultsGate)
            {
                removed = _executionResults.Remove(nodeId);
                _resultLeases.Remove(nodeId, out lease);
            }
            lease?.Dispose();
            return removed;
        }

        /// <summary>
        /// 清空所有节点执行结果
        /// </summary>
        public void ClearNodeResults()
        {
            IDisposable?[] leases;
            lock (_nodeResultsGate)
            {
                leases = _resultLeases.Values.ToArray();
                _resultLeases.Clear(); _executionResults.Clear();
            }
            List<Exception>? errors = null;
            foreach (var lease in leases)
                try { lease?.Dispose(); } catch (Exception ex) { (errors ??= new()).Add(ex); }
            if (errors != null) throw new AggregateException("节点结果回收失败", errors);
        }

        public NodeResultSnapshot CaptureNodeResults()
        {
            lock (_nodeResultsGate) return new NodeResultSnapshot(_executionResults);
        }

        /// <summary>
        /// 获取所有节点执行结果
        /// </summary>
        public IReadOnlyDictionary<string, NodeExecutionResult> GetAllNodeResults()
        {
            lock (_nodeResultsGate)
                return new Dictionary<string, NodeExecutionResult>(_executionResults);
        }

        #endregion
    }

    /// <summary>
    /// 执行日志
    /// </summary>
    public class ExecutionLog
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 执行ID
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; }

        /// <summary>
        /// 日志来源（层级格式，如"运行.检测流程.边缘检测"）
        /// </summary>
        public string? Source { get; set; }
    }

    /// <summary>
    /// 执行统计信息
    /// </summary>
    public class ExecutionStatistics
    {
        /// <summary>
        /// 执行ID
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 执行时长
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 总节点数
        /// </summary>
        public int TotalNodes { get; set; }

        /// <summary>
        /// 已完成节点数
        /// </summary>
        public int CompletedNodes { get; set; }

        /// <summary>
        /// 失败节点数
        /// </summary>
        public int FailedNodes { get; set; }

        /// <summary>
        /// 跳过节点数
        /// </summary>
        public int SkippedNodes { get; set; }

        /// <summary>
        /// 总日志数
        /// </summary>
        public int TotalLogs { get; set; }

        /// <summary>
        /// 调用深度
        /// </summary>
        public int CallDepth { get; set; }

        /// <summary>
        /// 成功率
        /// </summary>
        public double SuccessRate => TotalNodes > 0 ? (double)CompletedNodes / TotalNodes * 100 : 0;
    }
}

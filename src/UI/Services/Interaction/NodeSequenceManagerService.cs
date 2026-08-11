using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Shell.Controls.Canvas;

namespace SunEyeVision.UI.Services.Interaction
{
    /// <summary>
    /// 工作流索引上下文
    /// </summary>
    internal class WorkflowIndexContext
    {
        /// <summary>
        /// 当前最大索引（递增计数器）
        /// </summary>
        public int MaxIndex { get; set; } = 0;

        /// <summary>
        /// 空洞池（SortedSet实现O(log n)性能）
        /// </summary>
        public SortedSet<int> HolePool { get; set; } = new SortedSet<int>();
    }

    /// <summary>
    /// 节点序号管理器实例
    /// </summary>
    public class NodeSequenceManager : INodeSequenceManager
    {
        /// <summary>
        /// 工作流索引上下文
        /// Key: {workflowId}#{algorithmType}, Value: WorkflowIndexContext
        /// </summary>
        private readonly Dictionary<string, WorkflowIndexContext> _workflowContexts = new Dictionary<string, WorkflowIndexContext>();

        /// <summary>
        /// 全局索引计数器（静态变量，确保跨所有实例共享）
        /// </summary>
        private static int _globalIndex = 0;

        /// <summary>
        /// 全局空洞池（静态变量，存储被删除节点的全局索引）
        /// </summary>
        private static readonly SortedSet<int> _globalHolePool = new SortedSet<int>();

        /// <summary>
        /// 线程安全锁对象
        /// </summary>
        private readonly object _lockObject = new object();

        public int GetNextGlobalIndex()
        {
            lock (_lockObject)
            {
                // 优先从空洞池获取最小索引（O(log n)）
                if (_globalHolePool.Count > 0)
                {
                    int minHole = _globalHolePool.Min;
                    _globalHolePool.Remove(minHole);

                    VisionLogger.Instance.Log(
                        LogLevel.Info,
                        $"从全局空洞池获取索引: {minHole}, 剩余空洞数: {_globalHolePool.Count}",
                        "NodeSequenceManager"
                    );

                    return minHole;
                }

                // 空洞池为空，直接递增（O(1)）
                int newIndex = Interlocked.Increment(ref _globalIndex);

                VisionLogger.Instance.Log(
                    LogLevel.Info,
                    $"递增获取全局索引: {newIndex}",
                    "NodeSequenceManager"
                );

                return newIndex;
            }
        }

        public int GetNextLocalIndex(string workflowId, string toolType)
        {
            lock (_lockObject)
            {
                var key = BuildContextKey(workflowId, toolType);

                if (!_workflowContexts.TryGetValue(key, out var context))
                {
                    context = new WorkflowIndexContext();
                    _workflowContexts[key] = context;
                }

                // 优先从空洞池获取最小索引（O(log n)）
                if (context.HolePool.Count > 0)
                {
                    int minHole = context.HolePool.Min;
                    context.HolePool.Remove(minHole);

                    VisionLogger.Instance.Log(
                        LogLevel.Info,
                        $"从空洞池获取索引: {minHole}, 剩余空洞数: {context.HolePool.Count}",
                        "NodeSequenceManager"
                    );

                    return minHole;
                }

                // 空洞池为空，直接递增（O(1)）
                int newIndex = ++context.MaxIndex;

                VisionLogger.Instance.Log(
                    LogLevel.Info,
                    $"递增获取索引: {newIndex}",
                    "NodeSequenceManager"
                );

                return newIndex;
            }
        }

        public void ReleaseLocalIndex(string workflowId, string toolType, int localIndex)
        {
            lock (_lockObject)
            {
                var key = BuildContextKey(workflowId, toolType);

                if (!_workflowContexts.TryGetValue(key, out var context))
                {
                    VisionLogger.Instance.Log(
                        LogLevel.Warning,
                        $"未找到工作流上下文: {key}, 索引 {localIndex} 释放失败",
                        "NodeSequenceManager"
                    );
                    return;
                }

                // 将索引添加到空洞池（O(log n)）
                context.HolePool.Add(localIndex);

                VisionLogger.Instance.Log(
                    LogLevel.Info,
                    $"释放索引到空洞池: {localIndex}, 当前空洞数: {context.HolePool.Count}",
                    "NodeSequenceManager"
                );
            }
        }

        public void ReleaseGlobalIndex(int globalIndex)
        {
            lock (_lockObject)
            {
                if (globalIndex < 0)
                {
                    VisionLogger.Instance.Log(
                        LogLevel.Warning,
                        $"无效的全局索引: {globalIndex}, 释放失败",
                        "NodeSequenceManager"
                    );
                    return;
                }

                // 将索引添加到空洞池（O(log n)）
                _globalHolePool.Add(globalIndex);

                VisionLogger.Instance.Log(
                    LogLevel.Info,
                    $"释放全局索引到空洞池: {globalIndex}, 当前空洞数: {_globalHolePool.Count}",
                    "NodeSequenceManager"
                );
            }
        }

        public string GenerateNodeName(string displayName, int localIndex)
        {
            // 节点名称格式：{DisplayName}{LocalIndex}（无分隔符）
            return $"{displayName}{localIndex}";
        }

        public void Reset()
        {
            lock (_lockObject)
            {
                _globalIndex = 0;
                _globalHolePool.Clear();
                _workflowContexts.Clear();

                VisionLogger.Instance.Log(
                    LogLevel.Info,
                    "重置所有序号（包括全局索引和局部索引）",
                    "NodeSequenceManager"
                );
            }
        }

        public void ResetWorkflow(string workflowId)
        {
            lock (_lockObject)
            {
                // 移除该工作流的所有上下文（以workflowId开头）
                var keysToRemove = _workflowContexts.Keys
                    .Where(key => key.StartsWith($"{workflowId}#"))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _workflowContexts.Remove(key);
                }

                VisionLogger.Instance.Log(
                    LogLevel.Info,
                    $"重置工作流序号: {workflowId}, 移除上下文数: {keysToRemove.Count}",
                    "NodeSequenceManager"
                );
            }
        }

        /// <summary>
        /// 构建上下文键
        /// </summary>
        private string BuildContextKey(string workflowId, string toolType)
        {
            return $"{workflowId}#{toolType}";
        }

        public void InitializeHolePoolsFromNodes(string workflowId, IEnumerable<WorkflowNode> nodes)
        {
            lock (_lockObject)
            {
                VisionLogger.Instance.Log(LogLevel.Info, "开始从现有节点初始化空洞池", "NodeSequenceManager");

                _globalHolePool.Clear();

                // 计算全局空洞
                var usedGlobalIndices = nodes.Select(n => n.GlobalIndex).Where(i => i >= 0).ToHashSet();
                if (usedGlobalIndices.Count > 0)
                {
                    int maxGlobalIndex = usedGlobalIndices.Max();
                    for (int i = 1; i <= maxGlobalIndex; i++)
                    {
                        if (!usedGlobalIndices.Contains(i))
                        {
                            _globalHolePool.Add(i);
                        }
                    }
                    _globalIndex = maxGlobalIndex;
                    VisionLogger.Instance.Log(LogLevel.Success, $"全局空洞池初始化完成: 最大索引={maxGlobalIndex}, 空洞数={_globalHolePool.Count}", "NodeSequenceManager");
                }
                else
                {
                    VisionLogger.Instance.Log(LogLevel.Info, "没有发现有效的全局索引", "NodeSequenceManager");
                }

                // 计算局部空洞
                var usedLocalIndices = new Dictionary<string, HashSet<int>>();
                foreach (var node in nodes)
                {
                    if (!string.IsNullOrEmpty(node.ToolType) && node.LocalIndex >= 0)
                    {
                        if (!usedLocalIndices.ContainsKey(node.ToolType))
                        {
                            usedLocalIndices[node.ToolType] = new HashSet<int>();
                        }
                        usedLocalIndices[node.ToolType].Add(node.LocalIndex);
                    }
                }

                _workflowContexts.Clear();
                foreach (var kvp in usedLocalIndices)
                {
                    int maxLocalIndex = kvp.Value.Max();
                    var context = new WorkflowIndexContext { MaxIndex = maxLocalIndex };
                    for (int i = 1; i <= maxLocalIndex; i++)
                    {
                        if (!kvp.Value.Contains(i))
                        {
                            context.HolePool.Add(i);
                        }
                    }
                    string key = BuildContextKey(workflowId, kvp.Key);
                    _workflowContexts[key] = context;
                    VisionLogger.Instance.Log(LogLevel.Success, $"局部空洞池初始化完成: 工具类型={kvp.Key}, 最大索引={maxLocalIndex}, 空洞数={context.HolePool.Count}", "NodeSequenceManager");
                }

                VisionLogger.Instance.Log(LogLevel.Success, "空洞池初始化完成", "NodeSequenceManager");
            }
        }
    }
}

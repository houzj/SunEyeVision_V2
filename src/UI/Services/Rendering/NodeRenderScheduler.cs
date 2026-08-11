using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Rendering
{
    /// <summary>
    /// 节点渲染调度器 - 异步批量渲染节点，避免UI卡顿
    /// 将节点创建分散到多个帧中执行
    /// </summary>
    public class NodeRenderScheduler : IDisposable
    {
        // 待渲染队列
        private readonly ConcurrentQueue<RenderTask> _pendingRenders = new();

        // 正在处理的任务
        private readonly ConcurrentDictionary<string, RenderTask> _processingTasks = new();

        // 调度器配置
        private const int BatchSize = 3; // 每批处理3个节点
        private const int BatchIntervalMs = 8; // 批次间隔8ms（约120fps）

        // 调度器状态
        private readonly DispatcherTimer _renderTimer;
        private readonly Dispatcher _dispatcher;
        private bool _disposed;
        private bool _isProcessing;

        // 节点UI池引用
        private NodeUIPool? _nodeUIPool;

        // 回调委托
        public Action<WorkflowNode, FrameworkElement>? OnNodeRendered;
        public Action<string>? OnNodeRemoved;

        /// <summary>
        /// 是否有待处理任务
        /// </summary>
        public bool HasPendingTasks => !_pendingRenders.IsEmpty;

        /// <summary>
        /// 待处理任务数量
        /// </summary>
        public int PendingCount => _pendingRenders.Count;

        public NodeRenderScheduler()
        {
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _renderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(BatchIntervalMs)
            };
            _renderTimer.Tick += OnRenderTimerTick;
        }

        /// <summary>
        /// 设置节点UI池
        /// </summary>
        public void SetNodeUIPool(NodeUIPool pool)
        {
            _nodeUIPool = pool;
        }

        /// <summary>
        /// 调度节点渲染
        /// </summary>
        /// <param name="node">要渲染的节点</param>
        /// <param name="priority">渲染优先级</param>
        public void ScheduleRender(WorkflowNode node, RenderPriority priority = RenderPriority.Normal)
        {
            if (_disposed || node == null)
                return;

            var task = new RenderTask
            {
                Node = node,
                Priority = priority,
                ScheduledTime = DateTime.Now
            };

            _pendingRenders.Enqueue(task);

            // 确保定时器运行
            StartTimerIfNeeded();
        }

        /// <summary>
        /// 批量调度节点渲染
        /// </summary>
        public void ScheduleRenderBatch(IEnumerable<WorkflowNode> nodes, RenderPriority priority = RenderPriority.Normal)
        {
            if (_disposed)
                return;

            foreach (var node in nodes)
            {
                ScheduleRender(node, priority);
            }
        }

        /// <summary>
        /// 立即渲染所有待处理节点（同步）
        /// </summary>
        public void RenderNow()
        {
            if (_disposed)
                return;

            ProcessBatch(int.MaxValue);
        }

        /// <summary>
        /// 移除待渲染节点
        /// </summary>
        public void RemoveRender(string nodeId)
        {
            _processingTasks.TryRemove(nodeId, out _);
            OnNodeRemoved?.Invoke(nodeId);
        }

        /// <summary>
        /// 启动定时器（如果需要）
        /// </summary>
        private void StartTimerIfNeeded()
        {
            if (!_renderTimer.IsEnabled && !_disposed)
            {
                _renderTimer.Start();
            }
        }

        /// <summary>
        /// 定时器处理
        /// </summary>
        private void OnRenderTimerTick(object? sender, EventArgs e)
        {
            if (_isProcessing || _disposed)
                return;

            try
            {
                _isProcessing = true;
                ProcessBatch(BatchSize);

                // 如果没有更多任务，停止定时器
                if (_pendingRenders.IsEmpty)
                {
                    _renderTimer.Stop();
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// 处理一批渲染任务
        /// </summary>
        private void ProcessBatch(int maxCount)
        {
            var processed = 0;

            while (processed < maxCount && _pendingRenders.TryDequeue(out var task))
            {
                try
                {
                    ProcessRenderTask(task);
                    processed++;
                }
                catch (Exception ex)
                {
}
            }
        }

        /// <summary>
        /// 处理单个渲染任务
        /// </summary>
        private void ProcessRenderTask(RenderTask task)
        {
            if (task.Node == null)
                return;

            var node = task.Node;

            // 添加到处理中集合
            _processingTasks[node.Id] = task;

            // 从池中获取UI元素（如果池可用）
            FrameworkElement? nodeUI = null;

            if (_nodeUIPool != null)
            {
                nodeUI = _nodeUIPool.GetNodeUI(node);
            }

            // 触发回调，让调用方处理UI插入
            if (nodeUI != null)
            {
                OnNodeRendered?.Invoke(node, nodeUI);
            }

            // 从处理中集合移除
            _processingTasks.TryRemove(node.Id, out _);
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public (int Pending, int Processing) GetStatistics()
        {
            return (_pendingRenders.Count, _processingTasks.Count);
        }

        /// <summary>
        /// 清空所有待处理任务
        /// </summary>
        public void Clear()
        {
            while (_pendingRenders.TryDequeue(out _)) { }
            _processingTasks.Clear();
            _renderTimer.Stop();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _renderTimer.Stop();
            _renderTimer.Tick -= OnRenderTimerTick;
            Clear();

            _disposed = true;
        }
    }

    /// <summary>
    /// 渲染任务
    /// </summary>
    internal struct RenderTask
    {
        public WorkflowNode Node { get; set; }
        public RenderPriority Priority { get; set; }
        public DateTime ScheduledTime { get; set; }
    }

    /// <summary>
    /// 渲染优先级
    /// </summary>
    public enum RenderPriority
    {
        Low,
        Normal,
        High,
        Immediate
    }
}

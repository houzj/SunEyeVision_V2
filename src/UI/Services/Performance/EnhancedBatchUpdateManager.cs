using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.UI.Models;
using SunEyeVision.UI.Services.Connection;
using SunEyeVision.UI.Services.Performance;

namespace SunEyeVision.UI.Services.Performance
{
    /// <summary>
    /// 增强的批量更新管理器
    /// 实现真正的批量路径计算和UI更新
    /// </summary>
    public class EnhancedBatchUpdateManager
    {
        private readonly ConnectionPathCache _pathCache;
        private readonly DispatcherTimer _updateTimer;

        // 待更新的节点和连接
        private readonly HashSet<string> _pendingNodeUpdates = new();
        private readonly HashSet<string> _pendingConnectionUpdates = new();

        // 批量大小控制
        private const int MaxBatchSize = 100;
        private const int UpdateDelayMs = 16; // 60FPS

        private bool _isUpdateScheduled = false;
        private ViewModels.WorkflowTabViewModel? _currentTab;

        // 性能统计
        public int TotalUpdatesProcessed { get; private set; }
        public int TotalBatchesProcessed { get; private set; }
        public double AverageBatchSize => TotalBatchesProcessed > 0
            ? (double)TotalUpdatesProcessed / TotalBatchesProcessed
            : 0;

        public EnhancedBatchUpdateManager(ConnectionPathCache pathCache)
        {
            _pathCache = pathCache ?? throw new ArgumentNullException(nameof(pathCache));
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(UpdateDelayMs)
            };
            _updateTimer.Tick += OnUpdateTimerTick;
        }

        /// <summary>
        /// 设置当前活动的WorkflowTab
        /// </summary>
        public void SetCurrentTab(ViewModels.WorkflowTabViewModel? tab)
        {
            _currentTab = tab;
        }

        /// <summary>
        /// 调度单个连接的更新
        /// </summary>
        public void ScheduleUpdate(WorkflowConnection connection)
        {
            if (connection == null) return;

            lock (_pendingConnectionUpdates)
            {
                _pendingConnectionUpdates.Add(connection.Id);
                if (!_isUpdateScheduled)
                {
                    _updateTimer.Start();
                    _isUpdateScheduled = true;
                }
            }
        }

        /// <summary>
        /// 调度节点相关的所有连接更新
        /// </summary>
        public void ScheduleUpdateForNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;

            lock (_pendingNodeUpdates)
            {
                _pendingNodeUpdates.Add(nodeId);
                if (!_isUpdateScheduled)
                {
                    _updateTimer.Start();
                    _isUpdateScheduled = true;
                }
            }
        }

        /// <summary>
        /// 调度多个节点相关的所有连接更新
        /// </summary>
        public void ScheduleUpdateForNodes(IEnumerable<string> nodeIds)
        {
            if (nodeIds == null) return;

            lock (_pendingNodeUpdates)
            {
                foreach (var nodeId in nodeIds)
                {
                    if (!string.IsNullOrEmpty(nodeId))
                    {
                        _pendingNodeUpdates.Add(nodeId);
                    }
                }

                if (!_isUpdateScheduled && _pendingNodeUpdates.Count > 0)
                {
                    _updateTimer.Start();
                    _isUpdateScheduled = true;
                }
            }
        }

        /// <summary>
        /// 立即执行所有待处理的更新
        /// </summary>
        public void ForceUpdateAll()
        {
            if (_isUpdateScheduled)
            {
                OnUpdateTimerTick(null, null);
            }
        }

        /// <summary>
        /// 清空所有待处理的更新
        /// </summary>
        public void ClearPendingUpdates()
        {
            lock (_pendingConnectionUpdates)
            {
                _pendingConnectionUpdates.Clear();
            }
            lock (_pendingNodeUpdates)
            {
                _pendingNodeUpdates.Clear();
            }

            if (_isUpdateScheduled)
            {
                _updateTimer.Stop();
                _isUpdateScheduled = false;
            }
        }

        /// <summary>
        /// 定时器触发 - 批量执行更新
        /// </summary>
        private void OnUpdateTimerTick(object? sender, EventArgs e)
        {
            _updateTimer.Stop();
            _isUpdateScheduled = false;

            try
            {
                // 获取需要更新的节点和连接列表
                HashSet<string> nodesToUpdate;
                HashSet<string> connectionsToUpdate;

                lock (_pendingNodeUpdates)
                {
                    nodesToUpdate = new HashSet<string>(_pendingNodeUpdates);
                    _pendingNodeUpdates.Clear();
                }

                lock (_pendingConnectionUpdates)
                {
                    connectionsToUpdate = new HashSet<string>(_pendingConnectionUpdates);
                    _pendingConnectionUpdates.Clear();
                }

                // 如果没有需要更新的，直接返回
                if (nodesToUpdate.Count == 0 && connectionsToUpdate.Count == 0)
                {
                    return;
                }

                if (_currentTab == null)
                {
                    return;
                }

                // 执行批量更新
                PerformBatchUpdate(nodesToUpdate, connectionsToUpdate);
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
            }
        }

        /// <summary>
        /// 执行批量更新（核心优化）
        /// </summary>
        private void PerformBatchUpdate(
            HashSet<string> nodesToUpdate,
            HashSet<string> connectionsToUpdate)
        {
            // 收集所有需要更新的连接（去重）
            var allConnectionIds = new HashSet<string>(connectionsToUpdate);

            // 根据节点ID查找相关连接
            foreach (var nodeId in nodesToUpdate)
            {
                var connections = GetConnectionsForNode(nodeId);
                foreach (var conn in connections)
                {
                    allConnectionIds.Add(conn.Id);
                }
            }

            if (allConnectionIds.Count == 0)
            {
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // === 优化点1: 批量标记缓存为脏 ===
            // 一次性标记所有连接的缓存为脏，避免重复检查
            var connectionsToRecalculate = new List<WorkflowConnection>();
            foreach (var connectionId in allConnectionIds)
            {
                var connection = FindConnection(connectionId);
                if (connection != null)
                {
                    _pathCache.MarkDirty(connection);
                    connectionsToRecalculate.Add(connection);
                }
            }

            // === 优化点2: 批量计算路径 ===
            // 批量预热缓存，利用缓存的批处理能力
            if (connectionsToRecalculate.Count > 0)
            {
                _pathCache.WarmUp(connectionsToRecalculate);
            }

            // === 优化点3: 批量触发UI更新 ===
            // 使用Dispatcher一次性触发所有UI更新
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                foreach (var connection in connectionsToRecalculate)
                {
                    connection.InvalidatePath();
                }
            }, DispatcherPriority.Background);

            stopwatch.Stop();

            // 更新统计
            TotalUpdatesProcessed += connectionsToRecalculate.Count;
            TotalBatchesProcessed++;

            // 性能监控
            if (stopwatch.ElapsedMilliseconds > 20) // 超过20ms的批量操作才记录
            {
                //     $"[EnhancedBatchUpdateManager] 批量更新: " +
                //     $"{connectionsToRecalculate.Count}条连线, " +
                //     $"{stopwatch.ElapsedMilliseconds:F2}ms " +
                //     $"({stopwatch.ElapsedMilliseconds / connectionsToRecalculate.Count:F3}ms/条)");
            }
        }

        /// <summary>
        /// 根据节点ID获取相关连接
        /// </summary>
        private IEnumerable<WorkflowConnection> GetConnectionsForNode(string nodeId)
        {
            if (_currentTab?.WorkflowConnections == null)
            {
                return Enumerable.Empty<WorkflowConnection>();
            }

            try
            {
                return _currentTab.WorkflowConnections
                    .Where(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId);
            }
            catch (Exception ex)
            {
                return Enumerable.Empty<WorkflowConnection>();
            }
        }

        /// <summary>
        /// 根据连接ID查找连接
        /// </summary>
        private WorkflowConnection? FindConnection(string connectionId)
        {
            if (_currentTab?.WorkflowConnections == null)
            {
                return null;
            }

            try
            {
                return _currentTab.WorkflowConnections.FirstOrDefault(c => c.Id == connectionId);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 销毁管理器，释放资源
        /// </summary>
        public void Dispose()
        {
            _updateTimer.Stop();
            _updateTimer.Tick -= OnUpdateTimerTick;

            lock (_pendingConnectionUpdates)
            {
                _pendingConnectionUpdates.Clear();
            }

            lock (_pendingNodeUpdates)
            {
                _pendingNodeUpdates.Clear();
            }

            PrintStatistics();
        }

        /// <summary>
        /// 打印统计信息
        /// </summary>
        public void PrintStatistics()
        {
            // 统计信息打印（已禁用）
        }
    }
}

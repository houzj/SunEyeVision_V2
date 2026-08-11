using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Connection;

using SunEyeVision.UI.Features.WorkflowUI;

namespace SunEyeVision.UI.Services.Connection
{
    /// <summary>
    /// 连接批量延迟更新管理器
    /// 用于优化节点拖拽时连接线的更新性能
    /// </summary>
    public class ConnectionBatchUpdateManager
    {
        private readonly ConnectionPathCache _pathCache;
        private readonly DispatcherTimer _updateTimer;
        private readonly HashSet<string> _pendingConnectionUpdates = new();
        private readonly HashSet<string> _pendingNodeUpdates = new();
        private bool _isUpdateScheduled = false;

        /// <summary>
        /// 批量更新延迟(毫秒)
        /// 16ms 约60FPS, 既能保证流畅度又能合并快速连续的更新
        /// </summary>
        private const int UpdateDelayMs = 16;

        /// <summary>
        /// 当前活动的WorkflowTab，用于查找连接
        /// </summary>
        private SunEyeVision.UI.Features.WorkflowUI.WorkflowTabViewModel? _currentTab;

        public ConnectionBatchUpdateManager(ConnectionPathCache pathCache)
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
        public void SetCurrentTab(SunEyeVision.UI.Features.WorkflowUI.WorkflowTabViewModel? tab)
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

                // 批量更新连接
                foreach (var connectionId in allConnectionIds)
                {
                    var connection = FindConnection(connectionId);
                    if (connection != null)
                    {
                        // 标记缓存为脏
                        _pathCache.MarkDirty(connection);

                        // 触发连接更新
                        connection.InvalidatePath();
                    }
                }
            }
            catch (Exception ex)
            {
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
        }
    }
}

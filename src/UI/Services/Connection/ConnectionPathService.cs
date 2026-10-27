using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Connection;

namespace SunEyeVision.UI.Services.Connection
{
    /// <summary>
    /// 连接路径服务接口
    /// </summary>
    public interface IConnectionPathService
    {
        /// <summary>
        /// 更新连接路径
        /// </summary>
        void UpdateConnectionPath(WorkflowConnection connection);

        /// <summary>
        /// 更新所有连接路径
        /// </summary>
        void UpdateAllConnections(IEnumerable<WorkflowConnection> connections);

        /// <summary>
        /// 标记连接为脏（需要重新计算）
        /// </summary>
        void MarkConnectionDirty(WorkflowConnection connection);

        /// <summary>
        /// 标记节点相关的所有连接为脏
        /// </summary>
        void MarkNodeConnectionsDirty(string nodeId);

        /// <summary>
        /// 清除路径缓存
        /// </summary>
        void ClearCache();

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        CacheStatistics GetStatistics();
    }


    /// <summary>
    /// 连接路径服务 - 管理连接线路径的计算和更新
    /// </summary>
    public class ConnectionPathService : IConnectionPathService
    {
        private readonly ConnectionPathCache _pathCache;

        public ConnectionPathService(ConnectionPathCache pathCache)
        {
            _pathCache=pathCache;
        }
        public void UpdateConnectionPath(WorkflowConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            // 标记为脏，下次访问时重新计算
            _pathCache.MarkDirty(connection);

            // 立即更新（如果需要）
            var pathData = _pathCache.GetPathData(connection);
            if (pathData != null)
            {
                connection.PathData = pathData;
                connection.InvalidatePath();
            }
        }

        public void UpdateAllConnections(IEnumerable<WorkflowConnection> connections)
        {
            if (connections == null)
            {
                return;
            }

            foreach (var connection in connections)
            {
                UpdateConnectionPath(connection);
            }
        }

        public void MarkConnectionDirty(WorkflowConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            _pathCache.MarkDirty(connection);
        }

        public void MarkNodeConnectionsDirty(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            _pathCache.MarkNodeDirty(nodeId);
        }

        public void ClearCache()
        {
            _pathCache.Clear();
        }

        public CacheStatistics GetStatistics()
        {
            return _pathCache.GetStatistics();
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Connection
{
    /// <summary>
    /// 连接刷新服务：收敛"节点移动 → 连线更新"的关注点（画布拖拽不再直接触碰 Cache/Batch）。
    /// 刚体平移模型：
    /// - 组内线（两端都在组内）：拖动中坐标级平移（缓存几何平移克隆，零贝塞尔/避障重算），松手真实重算落账
    /// - 跨组线（一端组内一端组外）：5px 位置节流 + 16ms 批量实时重算（数量少）
    /// 画布只负责更新节点 Position 并调用 BeginDrag/UpdateDrag/EndDrag；本服务不接触节点视觉与输入。
    /// </summary>
    public sealed class ConnectivityRefreshService
    {
        private const double PositionUpdateThreshold = 5.0;

        private readonly ConnectionPathCache? _pathCache;
        private readonly ConnectionBatchUpdateManager? _batchManager;

        private List<WorkflowConnection>? _inner;      // 组内线（两端都在组内）
        private List<WorkflowConnection>? _boundary;   // 跨组线（恰一端在组内）
        private Dictionary<string, WorkflowNode>? _groupById; // 组内节点索引（跨组线锚点定位用）
        private readonly Dictionary<string, Point> _lastPositions = new();
        private bool _active;

        public ConnectivityRefreshService(ConnectionPathCache? pathCache, ConnectionBatchUpdateManager? batchManager)
        {
            _pathCache = pathCache;
            _batchManager = batchManager;
        }

        /// <summary>
        /// 开始拖动：按组分类连接（组内/跨组一次完成），并为组内线登记平移会话。
        /// </summary>
        public void BeginDrag(IReadOnlyList<WorkflowNode> group, IReadOnlyList<WorkflowConnection> connections)
        {
            Reset();

            if (group == null || group.Count == 0)
                return;

            _groupById = group.ToDictionary(n => n.Id);
            _inner = new List<WorkflowConnection>();
            _boundary = new List<WorkflowConnection>();

            if (connections != null)
            {
                foreach (var conn in connections)
                {
                    if (conn == null) continue;
                    bool sourceIn = _groupById.ContainsKey(conn.SourceNodeId);
                    bool targetIn = _groupById.ContainsKey(conn.TargetNodeId);
                    if (sourceIn && targetIn) _inner.Add(conn);
                    else if (sourceIn || targetIn) _boundary.Add(conn);
                }
            }

            _pathCache?.BeginGroupTranslation(_inner);
            _active = true;
        }

        /// <summary>
        /// 拖动中：组内线整体平移（零重算），跨组线节流 + 批量实时重算。
        /// offset 为当前组整体位移（逻辑坐标）。
        /// </summary>
        public void UpdateDrag(Point offset)
        {
            if (!_active)
                return;

            // 组内线：两端同步移动、形状不变（刚体）。节流后更新平移偏移并触发 Path 重取，
            // ConnectionPathCache.GetPath 对组内线返回缓存几何的坐标平移克隆（新实例 → Path.Data 更新即重绘）
            if (_inner != null && _inner.Count > 0 && _pathCache != null)
            {
                bool moved = false;
                foreach (var conn in _inner)
                {
                    if (ShouldThrottle(conn.Id, AnchorOf(conn)))
                        moved = true;
                }

                if (moved)
                {
                    _pathCache.UpdateGroupTranslation(new Vector(offset.X, offset.Y));
                    foreach (var conn in _inner)
                    {
                        conn.InvalidatePath();
                    }
                }
            }

            // 跨组线（一端组内一端组外）才需要实时重算——数量少，走 5px 节流 + 16ms 批量
            if (_boundary != null && _boundary.Count > 0 && _batchManager != null)
            {
                foreach (var conn in _boundary)
                {
                    if (ShouldThrottle(conn.Id, AnchorOf(conn)))
                    {
                        _batchManager.ScheduleUpdate(conn);
                    }
                }
            }
        }

        /// <summary>
        /// 松手落账：组内线一次性真实重算刷新缓存（基于最终坐标）；跨组线 ForceUpdateAll 对齐；结束会话。
        /// </summary>
        public void EndDrag()
        {
            if (!_active)
                return;

            if (_inner != null && _inner.Count > 0 && _batchManager != null)
            {
                foreach (var conn in _inner)
                {
                    _batchManager.ScheduleUpdate(conn);
                }
            }

            _batchManager?.ForceUpdateAll();
            _pathCache?.EndGroupTranslation();

            Reset();
        }

        /// <summary>复位会话状态（Begin 前置调用 / 异常兜底）</summary>
        public void Reset()
        {
            _inner = null;
            _boundary = null;
            _groupById = null;
            _lastPositions.Clear();
            _active = false;
        }

        /// <summary>返回连接在组内端节点的当前 Position（节流判断与锚点语义同节点移动量）</summary>
        private Point AnchorOf(WorkflowConnection conn)
        {
            if (_groupById != null)
            {
                if (_groupById.TryGetValue(conn.SourceNodeId, out var sourceNode)) return sourceNode.Position;
                if (_groupById.TryGetValue(conn.TargetNodeId, out var targetNode)) return targetNode.Position;
            }
            return new Point(0, 0);
        }

        /// <summary>5px 位置节流：仅当移动超过阈值才放行一次更新</summary>
        private bool ShouldThrottle(string key, Point currentPosition)
        {
            if (!_lastPositions.TryGetValue(key, out var lastPosition))
            {
                _lastPositions[key] = currentPosition;
                return true;
            }

            if (Math.Abs(currentPosition.X - lastPosition.X) > PositionUpdateThreshold ||
                Math.Abs(currentPosition.Y - lastPosition.Y) > PositionUpdateThreshold)
            {
                _lastPositions[key] = currentPosition;
                return true;
            }

            return false;
        }
    }
}

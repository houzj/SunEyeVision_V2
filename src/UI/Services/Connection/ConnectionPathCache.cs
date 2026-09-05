using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.PathCalculators;
using SunEyeVision.UI.Services.Connection;
using SunEyeVision.UI.Services.Path;
using SunEyeVision.UI.Services.Node;
using PortDirection = SunEyeVision.UI.Services.Path.PortDirection;

namespace SunEyeVision.UI.Services.Connection
{
    /// <summary>
    /// 连接线路径缓存 - 避免重复计算连接线路径
    /// </summary>
    public class ConnectionPathCache
    {
        private readonly Dictionary<string, CachedPath> _pathCache;
        private readonly Dictionary<string, bool> _dirtyFlags;
        private readonly ObservableCollection<WorkflowNodeBase> _nodes;
        private readonly object _lockObj;
        private readonly IPathCalculator _pathCalculator;

        // 5C: 节点位置跟踪（用于统计和调试，不再用于距离阈值判断）
        private readonly Dictionary<string, Point> _lastNodePositions = new Dictionary<string, Point>();
        private readonly Dictionary<string, int> _connectionUsageCount = new Dictionary<string, int>();

        // 拖动组平移会话：组内线在拖动中"整体平移"由 GetPath 返回缓存几何的平移克隆实现
        // （新实例 → Path.Data 更新即重绘），避免每帧真实重算贝塞尔/避障
        private HashSet<string>? _groupTranslatedIds;
        private Vector _groupOffset;
        private Dictionary<string, Point>? _groupInitialArrows;

        /// <summary>
        /// 缓存命中次数
        /// </summary>
        public int CacheHits { get; private set; }

        /// <summary>
        /// 缓存未命中次数
        /// </summary>
        public int CacheMisses { get; private set; }

        /// <summary>
        /// 缓存大小
        /// </summary>
        public int CacheSize => _pathCache.Count;

        /// <summary>
        /// 缓存命中率
        /// </summary>
        public double HitRate => CacheHits + CacheMisses > 0
            ? (double)CacheHits / (CacheHits + CacheMisses)
            : 0;

        public ConnectionPathCache(ObservableCollection<WorkflowNodeBase> nodes, IPathCalculator? pathCalculator = null)
        {
            _pathCache = [];
            _dirtyFlags = [];
            _nodes = nodes;
            _lockObj = new object();
            _pathCalculator = pathCalculator ?? PathCalculatorFactory.CreateCalculator();

            SubscribeToNodes();
        }

        /// <summary>
        /// 获取连接线路径
        /// </summary>
        public PathGeometry? GetPath(WorkflowConnection connection)
        {
            lock (_lockObj)
            {
                // 4C: 增加连接使用计数
                if (_connectionUsageCount.ContainsKey(connection.Id))
                {
                    _connectionUsageCount[connection.Id]++;
                }
                else
                {
                    _connectionUsageCount[connection.Id] = 1;
                }

                if (_pathCache.TryGetValue(connection.Id, out var cachedPath))
                {
                    if (_dirtyFlags.TryGetValue(connection.Id, out bool isDirty) && isDirty)
                    {
                        var path = CalculatePath(connection);
                        UpdateCache(connection.Id, path);
                        CacheMisses++;
                        return path;
                    }

                    // 组平移会话：返回缓存几何的"坐标平移克隆"（新实例 → Path.Data 更新触发重绘），不污染缓存。
                    // 注意必须平移坐标而非 Geometry.Transform：连线渲染链 GetPathData 走 ToString→SVG 字符串，
                    // Transform 不体现在字符串中，仅改坐标才会让绑定层感知变化
                    if ((_groupOffset.X != 0 || _groupOffset.Y != 0)
                        && _groupTranslatedIds != null
                        && _groupTranslatedIds.Contains(connection.Id))
                    {
                        // 同步箭头锚点：箭头贴线随刚体平移（初始位置 + 位移，角度不变）
                        if (_groupInitialArrows != null
                            && _groupInitialArrows.TryGetValue(connection.Id, out Point initialArrow))
                        {
                            connection.ArrowPosition = new Point(
                                initialArrow.X + _groupOffset.X,
                                initialArrow.Y + _groupOffset.Y);
                        }

                        return TranslateGeometry(cachedPath.Geometry, _groupOffset);
                    }

                    CacheHits++;
                    return cachedPath.Geometry;
                }
                var newPath = CalculatePath(connection);
                UpdateCache(connection.Id, newPath);
                CacheMisses++;
                return newPath;
            }
        }

        /// <summary>
        /// 获取连接线路径数据（字符串）
        /// </summary>
        public string? GetPathData(WorkflowConnection connection)
        {
            var geometry = GetPath(connection);
            return geometry?.ToString();
        }

        /// <summary>
        /// 标记连接为脏（需要重新计算）
        /// </summary>
        public void MarkDirty(WorkflowConnection connection)
        {
            lock (_lockObj)
            {
                _dirtyFlags[connection.Id] = true;
            }
        }

        /// <summary>
        /// 标记所有连接为脏
        /// </summary>
        public void MarkAllDirty()
        {
            lock (_lockObj)
            {
                foreach (var key in _dirtyFlags.Keys)
                {
                    _dirtyFlags[key] = true;
                }
            }
        }

        /// <summary>
        /// 标记节点相关的所有连接为脏
        /// </summary>
        public void MarkNodeDirty(string nodeId)
        {
            lock (_lockObj)
            {
                foreach (var kvp in _pathCache)
                {
                    if (kvp.Value.SourceNodeId == nodeId || kvp.Value.TargetNodeId == nodeId)
                    {
                        _dirtyFlags[kvp.Key] = true;
                    }
                }
            }
        }

        /// <summary>
        /// 拖动组平移会话：登记组内线集合（拖动中 GetPath 对它们返回缓存几何的平移克隆）。
        /// 刚体平移下组内线形状不变，平移即精确解——不重算贝塞尔/避障，且以新实例触发 Path.Data 重绘。
        /// </summary>
        public void BeginGroupTranslation(IEnumerable<WorkflowConnection> inner)
        {
            lock (_lockObj)
            {
                var list = inner?.Where(c => c != null).ToList();
                _groupTranslatedIds = list?.Select(c => c.Id).ToHashSet();
                _groupInitialArrows = list?.ToDictionary(c => c.Id, c => c.ArrowPosition);
                _groupOffset = default;
            }
        }

        /// <summary>
        /// 更新拖动组平移偏移（组内线 GetPath 平移克隆的位移量）
        /// </summary>
        public void UpdateGroupTranslation(Vector offset)
        {
            lock (_lockObj)
            {
                _groupOffset = offset;
            }
        }

        /// <summary>
        /// 结束拖动组平移会话（落账后由调用方一次性真实重算刷新缓存，再清除会话状态）
        /// </summary>
        public void EndGroupTranslation()
        {
            lock (_lockObj)
            {
                _groupTranslatedIds = null;
                _groupInitialArrows = null;
                _groupOffset = default;
            }
        }

        /// <summary>
        /// 5C: 标记节点为脏（移除距离阈值，使用节流机制）
        /// 路径更新的节流由ConnectionBatchUpdateManager控制（16ms延迟）
        /// </summary>
        public void MarkNodeDirtySmart(string nodeId, Point newPosition)
        {
            lock (_lockObj)
            {
                // 记录新位置
                _lastNodePositions[nodeId] = newPosition;

                // 直接标记相关连接为脏
                // 不使用距离阈值，让ConnectionBatchUpdateManager控制节流
                MarkNodeDirty(nodeId);
            }
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void Clear()
        {
            lock (_lockObj)
            {
                _pathCache.Clear();
                _dirtyFlags.Clear();
                _lastNodePositions.Clear();
                _connectionUsageCount.Clear();
                CacheHits = 0;
                CacheMisses = 0;
            }
        }

        /// <summary>
        /// 4C: 智能清理缓存（基于使用频率和LRU策略）
        /// </summary>
        public void CleanupCache(int targetSize = 500)
        {
            lock (_lockObj)
            {
                if (_pathCache.Count <= targetSize)
                    return;

                // 按使用频率排序，移除最少使用的
                var sortedConnections = _connectionUsageCount
                    .OrderBy(kvp => kvp.Value)
                    .Take(_pathCache.Count - targetSize)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var connectionId in sortedConnections)
                {
                    _pathCache.Remove(connectionId);
                    _dirtyFlags.Remove(connectionId);
                    _connectionUsageCount.Remove(connectionId);
                }
            }
        }

        /// <summary>
        /// 移除连接的缓存
        /// </summary>
        public void Remove(string connectionId)
        {
            lock (_lockObj)
            {
                _pathCache.Remove(connectionId);
                _dirtyFlags.Remove(connectionId);
            }
        }

        /// <summary>
        /// 预热缓存（预先计算所有连接）
        /// </summary>
        public void WarmUp(IEnumerable<WorkflowConnection> connections)
        {
            lock (_lockObj)
            {
                int warmedCount = 0;
                foreach (var connection in connections)
                {
                    if (!_pathCache.ContainsKey(connection.Id))
                    {
                        var path = CalculatePath(connection);
                        UpdateCache(connection.Id, path);
                        warmedCount++;
                    }
                }
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            lock (_lockObj)
            {
                return new CacheStatistics
                {
                    CacheSize = _pathCache.Count,
                    CacheHits = CacheHits,
                    CacheMisses = CacheMisses,
                    HitRate = HitRate
                };
            }
        }

        /// <summary>
        /// 坐标平移克隆：把路径几何整体平移 offset（逐 Figure/Segment 坐标加位移）。
        /// 供组内线拖动"刚体平移"使用——不重算贝塞尔/避障，仅平移坐标。
        /// </summary>
        private static PathGeometry TranslateGeometry(PathGeometry source, Vector offset)
        {
            var clone = source.Clone();
            foreach (var figure in clone.Figures)
            {
                figure.StartPoint += offset;
                foreach (var segment in figure.Segments)
                {
                    switch (segment)
                    {
                        case LineSegment line:
                            line.Point += offset;
                            break;
                        case QuadraticBezierSegment quad:
                            quad.Point1 += offset;
                            quad.Point2 += offset;
                            break;
                        case BezierSegment bezier:
                            bezier.Point1 += offset;
                            bezier.Point2 += offset;
                            bezier.Point3 += offset;
                            break;
                        case PolyLineSegment polyLine:
                            for (int i = 0; i < polyLine.Points.Count; i++)
                                polyLine.Points[i] = polyLine.Points[i] + offset;
                            break;
                        case PolyBezierSegment polyBezier:
                            for (int i = 0; i < polyBezier.Points.Count; i++)
                                polyBezier.Points[i] = polyBezier.Points[i] + offset;
                            break;
                    }
                }
            }
            return clone;
        }

        private PathGeometry CalculatePath(WorkflowConnection connection)
        {
            // 路径计算需要 WorkflowNode 特有成员（StyleConfigTyped/端口），运行时元素均为 WorkflowNode，安全转型。
            var sourceNode = _nodes.OfType<WorkflowNode>().FirstOrDefault(n => n.Id == connection.SourceNodeId);
            var targetNode = _nodes.OfType<WorkflowNode>().FirstOrDefault(n => n.Id == connection.TargetNodeId);

            if (sourceNode == null || targetNode == null)
                return new PathGeometry();

            // 根据端口名称获取端口方向和位置
            var sourceDirection = PortDirectionExtensions.FromPortName(connection.SourcePort);
            var targetDirection = PortDirectionExtensions.FromPortName(connection.TargetPort);

            var sourcePos = GetPortPosition(sourceNode, connection.SourcePort);
            var targetPos = GetPortPosition(targetNode, connection.TargetPort);

            // 计算节点边界矩形
            var sourceNodeRect = new Rect(
                sourceNode.Position.X,
                sourceNode.Position.Y,
                sourceNode.StyleConfigTyped.NodeWidth,
                sourceNode.StyleConfigTyped.NodeHeight);

            var targetNodeRect = new Rect(
                targetNode.Position.X,
                targetNode.Position.Y,
                targetNode.StyleConfigTyped.NodeWidth,
                targetNode.StyleConfigTyped.NodeHeight);

            // 计算所有节点边界（用于碰撞检测）— 需要 WorkflowNode 特有成员，安全转型
            var allNodeRects = _nodes.OfType<WorkflowNode>().Select(n => new Rect(
                n.Position.X,
                n.Position.Y,
                n.StyleConfigTyped.NodeWidth,
                n.StyleConfigTyped.NodeHeight)).ToArray();

            // 根据端口方向计算箭头尾部位置（路径终点）
            var arrowTailPos = CalculateArrowTailPosition(targetPos, targetDirection);



            // 使用箭头尾部作为路径终点，传递所有节点边界信息用于碰撞检测
            var pathPoints = _pathCalculator.CalculateOrthogonalPath(
                sourcePos,
                arrowTailPos,  // 路径终点 = 箭头尾部
                sourceDirection,
                targetDirection,
                sourceNodeRect,  // 源节点边界
                targetNodeRect,  // 目标节点边界
                allNodeRects);   // 所有节点边界（用于碰撞检测）

            // 创建路径几何
            var pathGeometry = _pathCalculator.CreatePathGeometry(pathPoints);

            // 更新连线路径点集合（用于调试和显示）
            UpdateConnectionPathPoints(connection, pathPoints);

            // 计算箭头位置和角度
            var (arrowPosition, arrowAngle) = _pathCalculator.CalculateArrow(pathPoints, targetPos, targetDirection);
            connection.ArrowPosition = arrowPosition;
            connection.ArrowAngle = arrowAngle;

            return pathGeometry;
        }

        /// <summary>
        /// 根据端口名称获取端口位置
        /// </summary>
        private static Point GetPortPosition(WorkflowNode node, string portName)
        {
            return portName?.ToLower() switch
            {
                "top" or "topport" => node.TopPortPosition,
                "bottom" or "bottomport" => node.BottomPortPosition,
                "left" or "leftport" => node.LeftPortPosition,
                "right" or "rightport" => node.RightPortPosition,
                _ => node.RightPortPosition // 默认为右侧端口
            };
        }

        /// <summary>
        /// 计算箭头尾部位置（路径终点）
        /// 箭头尖端在目标端口中心，箭头尾部向外偏移箭头长度
        /// </summary>
        private static Point CalculateArrowTailPosition(Point arrowTipPosition, PortDirection targetDirection)
        {
            const double arrowLength = 15.0; // 箭头长度

            // 根据端口方向，将箭头尖端向后偏移箭头长度
            return targetDirection switch
            {
                PortDirection.Top => new Point(arrowTipPosition.X, arrowTipPosition.Y - arrowLength),
                PortDirection.Bottom => new Point(arrowTipPosition.X, arrowTipPosition.Y + arrowLength),
                PortDirection.Left => new Point(arrowTipPosition.X - arrowLength, arrowTipPosition.Y),
                PortDirection.Right => new Point(arrowTipPosition.X + arrowLength, arrowTipPosition.Y),
                _ => arrowTipPosition
            };
        }

        /// <summary>
        /// 更新连线的路径点集合
        /// </summary>
        private static void UpdateConnectionPathPoints(WorkflowConnection connection, Point[] pathPoints)
        {
            connection.PathPoints.Clear();
            foreach (var point in pathPoints)
            {
                connection.PathPoints.Add(point);
            }
        }

        private void UpdateCache(string connectionId, PathGeometry path)
        {
            _pathCache[connectionId] = new CachedPath
            {
                Geometry = path,
                Timestamp = DateTime.Now,
                SourceNodeId = ExtractSourceNodeId(connectionId),
                TargetNodeId = ExtractTargetNodeId(connectionId)
            };
            _dirtyFlags[connectionId] = false;
        }

        private static string ExtractSourceNodeId(string connectionId)
        {
            return connectionId.Split('_')[0];
        }

        private static string ExtractTargetNodeId(string connectionId)
        {
            return connectionId.Split('_')[1];
        }

        private void SubscribeToNodes()
        {
            _nodes.CollectionChanged += (s, e) =>
            {
                if (e.OldItems != null)
                {
                    foreach (WorkflowNode node in e.OldItems)
                    {
                        MarkNodeDirty(node.Id);
                    }
                }

                if (e.NewItems != null)
                {
                    foreach (WorkflowNode node in e.NewItems)
                    {
                        MarkNodeDirty(node.Id);
                    }
                }
            };
        }
    }

    /// <summary>
    /// 缓存的路径
    /// </summary>
    internal class CachedPath
    {
        public PathGeometry Geometry { get; set; } = new PathGeometry();
        public DateTime Timestamp { get; set; }
        public string SourceNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStatistics
    {
        public int CacheSize { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public double HitRate { get; set; }
        public int TotalRequests => CacheHits + CacheMisses;

        public override string ToString()
        {
            return $"缓存大小: {CacheSize}, 命中: {CacheHits}, 未命中: {CacheMisses}, 命中率: {HitRate:P2}";
        }
    }
}

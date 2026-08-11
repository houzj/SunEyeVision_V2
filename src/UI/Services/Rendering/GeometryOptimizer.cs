using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Services.Rendering;

namespace SunEyeVision.UI.Services.Rendering
{
    /// <summary>
    /// 几何优化器 - 使用 StreamGeometry 和 Freezable 优化渲染性能
    /// </summary>
    public class GeometryOptimizer
    {
        private readonly Dictionary<string, StreamGeometry> _geometryCache;
        private readonly Dictionary<string, Point> _arrowCache;
        private readonly Dictionary<string, bool> _frozenFlags;
        private readonly object _lockObj;

        /// <summary>
        /// 缓存大小
        /// </summary>
        public int CacheSize => _geometryCache.Count;

        /// <summary>
        /// 冻结的几何体数量
        /// </summary>
        public int FrozenCount { get; private set; }

        public GeometryOptimizer()
        {
            _geometryCache = new Dictionary<string, StreamGeometry>();
            _arrowCache = new Dictionary<string, Point>();
            _frozenFlags = new Dictionary<string, bool>();
            _lockObj = new object();
        }

        /// <summary>
        /// 获取或创建连接线StreamGeometry
        /// </summary>
        public StreamGeometry GetConnectionGeometry(WorkflowConnection connection, WorkflowNode sourceNode, WorkflowNode targetNode)
        {
            lock (_lockObj)
            {
                if (_geometryCache.TryGetValue(connection.Id, out var cachedGeometry))
                {
                    return cachedGeometry;
                }

                var geometry = CreateStreamGeometry(connection, sourceNode, targetNode);
                FreezeGeometry(geometry, connection.Id);
                _geometryCache[connection.Id] = geometry;

                return geometry;
            }
        }

        /// <summary>
        /// 获取箭头位置
        /// </summary>
        public Point GetArrowPosition(WorkflowConnection connection, WorkflowNode targetNode)
        {
            lock (_lockObj)
            {
                if (_arrowCache.TryGetValue(connection.Id, out var cachedPosition))
                {
                    return cachedPosition;
                }

                var position = CalculateArrowPosition(connection, targetNode);
                _arrowCache[connection.Id] = position;

                return position;
            }
        }

        /// <summary>
        /// 批量创建连接线几何体
        /// </summary>
        public Dictionary<string, StreamGeometry> CreateConnectionGeometries(
            IEnumerable<WorkflowConnection> connections,
            Dictionary<string, WorkflowNode> nodeMap)
        {
            lock (_lockObj)
            {
                var result = new Dictionary<string, StreamGeometry>();

                foreach (var connection in connections)
                {
                    if (nodeMap.TryGetValue(connection.SourceNodeId, out var sourceNode) &&
                        nodeMap.TryGetValue(connection.TargetNodeId, out var targetNode))
                    {
                        var geometry = GetConnectionGeometry(connection, sourceNode, targetNode);
                        result[connection.Id] = geometry;
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// 创建共享的连接线几何体（用于相同路径的连接）
        /// </summary>
        public StreamGeometry CreateSharedGeometry(
            Point startPoint,
            Point endPoint,
            string pathKey)
        {
            lock (_lockObj)
            {
                if (_geometryCache.TryGetValue(pathKey, out var cachedGeometry))
                {
                    return cachedGeometry;
                }

                var geometry = CreateBezierGeometry(startPoint, endPoint);
                FreezeGeometry(geometry, pathKey);
                _geometryCache[pathKey] = geometry;

                return geometry;
            }
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void Clear()
        {
            lock (_lockObj)
            {
                _geometryCache.Clear();
                _arrowCache.Clear();
                _frozenFlags.Clear();
                FrozenCount = 0;
            }
        }

        /// <summary>
        /// 移除指定的几何体
        /// </summary>
        public void Remove(string id)
        {
            lock (_lockObj)
            {
                _geometryCache.Remove(id);
                _arrowCache.Remove(id);
                _frozenFlags.Remove(id);
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public GeometryOptimizerStatistics GetStatistics()
        {
            lock (_lockObj)
            {
                return new GeometryOptimizerStatistics
                {
                    CacheSize = _geometryCache.Count,
                    FrozenCount = FrozenCount,
                    MemoryUsageEstimate = EstimateMemoryUsage()
                };
            }
        }

        private StreamGeometry CreateStreamGeometry(
            WorkflowConnection connection,
            WorkflowNode sourceNode,
            WorkflowNode targetNode)
        {
            var sourcePos = new Point(
                sourceNode.Position.X + CanvasConfig.NodeWidth / 2,
                sourceNode.Position.Y + CanvasConfig.NodeHeight / 2
            );
            var targetPos = new Point(
                targetNode.Position.X + CanvasConfig.NodeWidth / 2,
                targetNode.Position.Y + CanvasConfig.NodeHeight / 2
            );

            return CreateBezierGeometry(sourcePos, targetPos);
        }

        private StreamGeometry CreateBezierGeometry(Point startPoint, Point endPoint)
        {
            var geometry = new StreamGeometry
            {
                FillRule = FillRule.EvenOdd
            };

            using (var context = geometry.Open())
            {
                context.BeginFigure(startPoint, isFilled: false, isClosed: false);

                var midPoint1 = new Point(startPoint.X, startPoint.Y + (endPoint.Y - startPoint.Y) / 2);
                var midPoint2 = new Point(endPoint.X, startPoint.Y + (endPoint.Y - startPoint.Y) / 2);

                context.BezierTo(midPoint1, midPoint2, endPoint, isStroked: true, isSmoothJoin: true);
                context.Close();
            }

            return geometry;
        }

        private StreamGeometry CreateArrowGeometry(Point position, double angle)
        {
            var geometry = new StreamGeometry
            {
                FillRule = FillRule.EvenOdd
            };

            var arrowSize = CanvasConfig.Connection.ArrowSize;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);

            var p1 = new Point(position.X, position.Y);
            var p2 = new Point(
                position.X - arrowSize * cos + arrowSize / 2 * sin,
                position.Y - arrowSize * sin - arrowSize / 2 * cos
            );
            var p3 = new Point(
                position.X - arrowSize * cos - arrowSize / 2 * sin,
                position.Y - arrowSize * sin + arrowSize / 2 * cos
            );

            using (var context = geometry.Open())
            {
                context.BeginFigure(p1, isFilled: true, isClosed: true);
                context.LineTo(p2, isStroked: true, isSmoothJoin: true);
                context.LineTo(p3, isStroked: true, isSmoothJoin: true);
                context.LineTo(p1, isStroked: true, isSmoothJoin: true);
                context.Close();
            }

            return geometry;
        }

        private Point CalculateArrowPosition(WorkflowConnection connection, WorkflowNode targetNode)
        {
            return new Point(
                targetNode.Position.X + CanvasConfig.NodeWidth / 2,
                targetNode.Position.Y + CanvasConfig.NodeHeight / 2
            );
        }

        private void FreezeGeometry(Freezable geometry, string id)
        {
            if (!geometry.IsFrozen)
            {
                geometry.Freeze();
                _frozenFlags[id] = true;
                FrozenCount++;
            }
        }

        private long EstimateMemoryUsage()
        {
            const long geometrySize = 256;
            const long pointSize = 16;
            return _geometryCache.Count * geometrySize + _arrowCache.Count * pointSize;
        }
    }

    /// <summary>
    /// 几何优化器统计信息
    /// </summary>
    public class GeometryOptimizerStatistics
    {
        public int CacheSize { get; set; }
        public int FrozenCount { get; set; }
        public long MemoryUsageEstimate { get; set; }

        public override string ToString()
        {
            return $"缓存大小: {CacheSize}, 冻结数量: {FrozenCount}, 内存估算: {MemoryUsageEstimate / 1024}KB";
        }
    }
}

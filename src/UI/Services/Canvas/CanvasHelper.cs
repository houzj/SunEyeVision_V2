using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Canvas
{
    /// <summary>
    /// 节点边界限制策略
    /// </summary>
    public enum NodeBoundaryLimitStrategy
    {
        /// <summary>
        /// 不限制
        /// </summary>
        None,

        /// <summary>
        /// 仅画布边界（Canvas.ActualWidth/Height）
        /// </summary>
        CanvasOnly,

        /// <summary>
        /// 仅视口边界（ScrollViewer.ViewportWidth/Height）
        /// </summary>
        ViewportOnly,

        /// <summary>
        /// 两者都限制（取交集）
        /// </summary>
        Both
    }

    /// <summary>
    /// 画布辅助?- 提供画布相关的通用辅助方法
    /// </summary>
    public static class CanvasHelper
    {
        /// <summary>
        /// 节点边界限制策略（默认：两者都限制）
        /// </summary>
        public static NodeBoundaryLimitStrategy BoundaryLimitStrategy = NodeBoundaryLimitStrategy.Both;
        /// <summary>
        /// 判断点是否在端口?        /// </summary>
        /// <param name="point">要检测的?/param>
        /// <param name="portPosition">端口位置</param>
        /// <param name="hitDistance">命中距离</param>
        /// <returns>是否命中</returns>
        public static bool IsPointInPort(Point point, Point portPosition, double hitDistance = 15)
        {
            double dx = point.X - portPosition.X;
            double dy = point.Y - portPosition.Y;
            return dx * dx + dy * dy <= hitDistance * hitDistance;
        }

        /// <summary>
        /// 验证连接是否有效
        /// </summary>
        public static (bool isValid, string message) ValidateConnection(
            WorkflowNode sourceNode,
            WorkflowNode targetNode,
            IList<WorkflowConnection> existingConnections)
        {
            if (sourceNode == null)
            {
                return (false, "源节点不能为空");
            }

            if (targetNode == null)
            {
                return (false, "目标节点不能为空");
            }

            if (sourceNode.Id == targetNode.Id)
            {
                return (false, "不允许自连接");
            }

            if (existingConnections.Any(c => c.SourceNodeId == sourceNode.Id && c.TargetNodeId == targetNode.Id))
            {
                return (false, "连接已存在");
            }

            if (existingConnections.Any(c => c.TargetNodeId == sourceNode.Id && c.SourceNodeId == targetNode.Id))
            {
                return (false, "反向连接已存在");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// 将点吸附到网?        /// </summary>
        /// <param name="point">原始?/param>
        /// <param name="gridSize">网格大小</param>
        /// <returns>吸附后的?/returns>
        public static Point SnapToGrid(Point point, double gridSize = 10)
        {
            double x = Math.Round(point.X / gridSize) * gridSize;
            double y = Math.Round(point.Y / gridSize) * gridSize;
            return new Point(x, y);
        }

        /// <summary>
        /// 计算两个矩形是否相交
        /// </summary>
        public static bool RectanglesIntersect(Rect rect1, Rect rect2)
        {
            return rect1.IntersectsWith(rect2);
        }

        /// <summary>
        /// 计算节点矩形（Position为HitArea中心点）
        /// </summary>
        public static Rect GetNodeRect(WorkflowNode node)
        {
            var halfWidth = node.StyleConfigTyped.NodeWidth / 2;
            var halfHeight = node.StyleConfigTyped.NodeHeight / 2;
            return new Rect(
                node.Position.X - halfWidth,
                node.Position.Y - halfHeight,
                node.StyleConfigTyped.NodeWidth,
                node.StyleConfigTyped.NodeHeight
            );
        }

        /// <summary>
        /// 查找指定区域内的节点（Position为HitArea中心点）
        /// </summary>
        public static List<WorkflowNode> FindNodesInRegion(
            Point startPoint,
            Point endPoint,
            IList<WorkflowNode> nodes)
        {
            var rect = new Rect(
                Math.Min(startPoint.X, endPoint.X),
                Math.Min(startPoint.Y, endPoint.Y),
                Math.Abs(endPoint.X - startPoint.X),
                Math.Abs(endPoint.Y - startPoint.Y));

            return nodes.Where(n => rect.Contains(n.Position)).ToList();
        }

        /// <summary>
        /// 计算吸附点（Position为HitArea中心点）
        /// </summary>
        public static Point FindSnapPoint(
            Point point,
            IList<WorkflowNode> nodes,
            double snapDistance = 10)
        {
            Point result = point;
            double minDistance = double.MaxValue;

            foreach (var node in nodes)
            {
                // 检查四个角（基于中心点计算）
                var halfWidth = node.StyleConfigTyped.NodeWidth / 2;
                var halfHeight = node.StyleConfigTyped.NodeHeight / 2;
                var corners = new[]
                {
                    new Point(node.Position.X - halfWidth, node.Position.Y - halfHeight),
                    new Point(node.Position.X + halfWidth, node.Position.Y - halfHeight),
                    new Point(node.Position.X - halfWidth, node.Position.Y + halfHeight),
                    new Point(node.Position.X + halfWidth, node.Position.Y + halfHeight)
                };

                foreach (var corner in corners)
                {
                    double distance = Point.Subtract(point, corner).Length;
                    if (distance < snapDistance && distance < minDistance)
                    {
                        minDistance = distance;
                        result = corner;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 清除所有节点的选中状?        /// </summary>
        public static void ClearSelection(IEnumerable<WorkflowNode> nodes)
        {
            if (nodes == null) return;
            foreach (var node in nodes)
            {
                node.IsSelected = false;
            }
        }

        /// <summary>
        /// 获取节点边界矩形（Position为HitArea中心点）
        /// </summary>
        public static Rect GetNodeBounds(WorkflowNode node)
        {
            if (node == null) return Rect.Empty;
            var halfWidth = node.StyleConfigTyped.NodeWidth / 2;
            var halfHeight = node.StyleConfigTyped.NodeHeight / 2;
            return new Rect(
                node.Position.X - halfWidth,
                node.Position.Y - halfHeight,
                node.StyleConfigTyped.NodeWidth,
                node.StyleConfigTyped.NodeHeight
            );
        }

        /// <summary>
        /// 获取端口位置（Position为HitArea中心点）
        /// </summary>
        public static Point GetPortPosition(WorkflowNode node, string portName)
        {
            if (node == null || string.IsNullOrEmpty(portName)) return new Point(0, 0);

            var halfWidth = node.StyleConfigTyped.NodeWidth / 2;
            var halfHeight = node.StyleConfigTyped.NodeHeight / 2;

            // 根据端口名称计算位置
            return portName switch
            {
                "LeftPort" => new Point(node.Position.X - halfWidth, node.Position.Y),
                "RightPort" => new Point(node.Position.X + halfWidth, node.Position.Y),
                "TopPort" => new Point(node.Position.X, node.Position.Y - halfHeight),
                "BottomPort" => new Point(node.Position.X, node.Position.Y + halfHeight),
                _ => new Point(node.Position.X, node.Position.Y)
            };
        }

        /// <summary>
        /// 获取节点所有端口位置（Position为HitArea中心点）
        /// </summary>
        public static Dictionary<string, Point> GetAllPortPositions(WorkflowNode node)
        {
            if (node == null) return new Dictionary<string, Point>();

            var halfWidth = node.StyleConfigTyped.NodeWidth / 2;
            var halfHeight = node.StyleConfigTyped.NodeHeight / 2;

            return new Dictionary<string, Point>
            {
                { "LeftPort", new Point(node.Position.X - halfWidth, node.Position.Y) },
                { "RightPort", new Point(node.Position.X + halfWidth, node.Position.Y) },
                { "TopPort", new Point(node.Position.X, node.Position.Y - halfHeight) },
                { "BottomPort", new Point(node.Position.X, node.Position.Y + halfHeight) }
            };
        }

        /// <summary>
        /// 计算两点之间的距?        /// </summary>
        public static double GetDistance(Point p1, Point p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 获取节点中心点（Position本身就是中心点）
        /// </summary>
        public static Point GetNodeCenter(WorkflowNode node)
        {
            if (node == null) return new Point(0, 0);
            return node.Position;
        }

        /// <summary>
        /// 限制节点在画布边界内（Position为HitArea中心点）
        /// </summary>
        /// <param name="node">工作流节点</param>
        /// <param name="targetPosition">目标位置</param>
        /// <param name="canvas">画布控件</param>
        /// <returns>限制后的位置</returns>
        public static Point ClampNodeToCanvasBounds(WorkflowNode node, Point targetPosition, System.Windows.Controls.Canvas canvas)
        {
            if (canvas == null)
                return targetPosition;

            var halfWidth = node.StyleConfigTyped.NodeWidth / 2;
            var halfHeight = node.StyleConfigTyped.NodeHeight / 2;

            // 动态获取画布尺寸
            double canvasWidth = canvas.ActualWidth;
            double canvasHeight = canvas.ActualHeight;

            // 内边距为 0
            const double BOUNDARY_PADDING = 0;

            double minX = halfWidth + BOUNDARY_PADDING;
            double minY = halfHeight + BOUNDARY_PADDING;
            double maxX = canvasWidth - halfWidth - BOUNDARY_PADDING;
            double maxY = canvasHeight - halfHeight - BOUNDARY_PADDING;

            double clampedX = Math.Clamp(targetPosition.X, minX, maxX);
            double clampedY = Math.Clamp(targetPosition.Y, minY, maxY);

            return new Point(clampedX, clampedY);
        }

        /// <summary>
        /// 限制节点在 ScrollViewer 的可见视口内（Position为HitArea中心点）
        /// </summary>
        /// <param name="node">工作流节点</param>
        /// <param name="targetPosition">目标位置（Canvas 坐标系）</param>
        /// <param name="scrollViewer">ScrollViewer 控件</param>
        /// <param name="canvas">Canvas 控件（用于坐标转换）</param>
        /// <returns>限制后的位置</returns>
        public static Point ClampNodeToViewportBounds(WorkflowNode node, Point targetPosition, ScrollViewer scrollViewer, System.Windows.Controls.Canvas canvas)
        {
            if (scrollViewer == null || canvas == null)
                return targetPosition;

            var halfWidth = node.StyleConfigTyped.NodeWidth / 2;
            var halfHeight = node.StyleConfigTyped.NodeHeight / 2;

            try
            {
                // 使用 TransformToVisual 将 ScrollViewer 的可见区域转换为 Canvas 坐标系
                // 这样可以正确处理 ScaleTransform 和平移的组合场景
                var transform = scrollViewer.TransformToVisual(canvas);

                // 定义 ScrollViewer 的可见区域的四个角点（ScrollViewer 自身坐标系）
                var scrollViewerTopLeft = new Point(0, 0);
                var scrollViewerBottomRight = new Point(
                    scrollViewer.ViewportWidth,
                    scrollViewer.ViewportHeight
                );

                // 转换为 Canvas 坐标系（自动考虑 ScaleTransform 和平移）
                var canvasTopLeft = transform.Transform(scrollViewerTopLeft);
                var canvasBottomRight = transform.Transform(scrollViewerBottomRight);

                // 计算视口边界（Canvas 坐标系）
                double viewportLeft = canvasTopLeft.X;
                double viewportTop = canvasTopLeft.Y;
                double viewportRight = canvasBottomRight.X;
                double viewportBottom = canvasBottomRight.Y;

                // 内边距为 0
                const double BOUNDARY_PADDING = 0;

                double minX = viewportLeft + halfWidth + BOUNDARY_PADDING;
                double minY = viewportTop + halfHeight + BOUNDARY_PADDING;
                double maxX = viewportRight - halfWidth - BOUNDARY_PADDING;
                double maxY = viewportBottom - halfHeight - BOUNDARY_PADDING;

                double clampedX = Math.Clamp(targetPosition.X, minX, maxX);
                double clampedY = Math.Clamp(targetPosition.Y, minY, maxY);

                return new Point(clampedX, clampedY);
            }
            catch (Exception)
            {
                // 如果转换失败，返回原始位置
                return targetPosition;
            }
        }

        /// <summary>
        /// ✅ 批量拖拽：计算全局允许的最大偏移量（确保所有节点统一移动）
        /// </summary>
        /// <param name="nodes">要移动的节点列表</param>
        /// <param name="initialPositions">节点的初始位置</param>
        /// <param name="requestedOffset">请求的偏移量</param>
        /// <param name="canvas">画布控件</param>
        /// <param name="scrollViewer">ScrollViewer 控件（可选）</param>
        /// <returns>全局允许的最大偏移量</returns>
        public static Point CalculateGlobalAllowedOffset(
            List<WorkflowNode> nodes,
            Point[] initialPositions,
            Point requestedOffset,
            System.Windows.Controls.Canvas canvas,
            ScrollViewer scrollViewer = null)
        {
            double globalAllowedOffsetX = requestedOffset.X;
            double globalAllowedOffsetY = requestedOffset.Y;

            // 遍历所有节点，计算每个节点的允许偏移量
            for (int i = 0; i < nodes.Count && i < initialPositions.Length; i++)
            {
                var node = nodes[i];
                var initialPos = initialPositions[i];
                var targetPos = new Point(initialPos.X + requestedOffset.X, initialPos.Y + requestedOffset.Y);

                // 应用边界限制
                var clampedPos = ClampNodeToBounds(node, targetPos, canvas, scrollViewer);

                // 计算实际允许的偏移量
                var allowedOffsetX = clampedPos.X - initialPos.X;
                var allowedOffsetY = clampedPos.Y - initialPos.Y;

                // 根据拖拽方向，取最小值（确保所有节点都能移动）
                if (requestedOffset.X > 0)
                {
                    // 向右拖拽：取最小值（遇到右边界时受限）
                    if (allowedOffsetX < globalAllowedOffsetX)
                        globalAllowedOffsetX = allowedOffsetX;
                }
                else if (requestedOffset.X < 0)
                {
                    // 向左拖拽：取最大值（遇到左边界时受限）
                    if (allowedOffsetX > globalAllowedOffsetX)
                        globalAllowedOffsetX = allowedOffsetX;
                }

                if (requestedOffset.Y > 0)
                {
                    // 向下拖拽：取最小值（遇到下边界时受限）
                    if (allowedOffsetY < globalAllowedOffsetY)
                        globalAllowedOffsetY = allowedOffsetY;
                }
                else if (requestedOffset.Y < 0)
                {
                    // 向上拖拽：取最大值（遇到上边界时受限）
                    if (allowedOffsetY > globalAllowedOffsetY)
                        globalAllowedOffsetY = allowedOffsetY;
                }
            }

            return new Point(globalAllowedOffsetX, globalAllowedOffsetY);
        }

        /// <summary>
        /// 根据当前策略限制节点位置
        /// </summary>
        /// <param name="node">工作流节点</param>
        /// <param name="targetPosition">目标位置</param>
        /// <param name="canvas">画布控件</param>
        /// <param name="scrollViewer">ScrollViewer 控件（可选）</param>
        /// <returns>限制后的位置</returns>
        public static Point ClampNodeToBounds(WorkflowNode node, Point targetPosition, System.Windows.Controls.Canvas canvas, ScrollViewer scrollViewer = null)
        {
            // 根据策略执行限制
            switch (BoundaryLimitStrategy)
            {
                case NodeBoundaryLimitStrategy.None:
                    return targetPosition;

                case NodeBoundaryLimitStrategy.CanvasOnly:
                    return ClampNodeToCanvasBounds(node, targetPosition, canvas);

                case NodeBoundaryLimitStrategy.ViewportOnly:
                    return ClampNodeToViewportBounds(node, targetPosition, scrollViewer, canvas);

                case NodeBoundaryLimitStrategy.Both:
                    // 先限制到画布，再限制到视口（取交集）
                    var canvasClamped = ClampNodeToCanvasBounds(node, targetPosition, canvas);
                    return ClampNodeToViewportBounds(node, canvasClamped, scrollViewer, canvas);

                default:
                    return targetPosition;
            }
        }
    }
}

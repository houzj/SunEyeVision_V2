using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Services.Rendering;

namespace SunEyeVision.UI.Services.Interaction
{
    /// <summary>
    /// 端口位置查询服务 - 基于视觉树查询，完全解耦样式
    /// </summary>
    public class PortPositionService
    {
        private readonly System.Windows.Controls.Canvas _canvas;
        private readonly NodeStyleConfig _styleConfig;

        public PortPositionService(System.Windows.Controls.Canvas canvas, NodeStyleConfig styleConfig)
        {
            _canvas = canvas;
            _styleConfig = styleConfig;
        }

        /// <summary>
        /// 通过节点ID和端口名称查询端口在Canvas上的实际位置
        /// </summary>
        public Point? QueryPortPosition(string nodeId, string portName)
        {
            try
            {
                // 1. 在Canvas的视觉树中查找所有Border
                var borders = WorkflowVisualHelper.FindAllVisualChildren<Border>(_canvas);

                // 2. 找到对应节点的Border（通过Tag匹配）
                var nodeBorder = borders.FirstOrDefault(b => 
                    b.Tag is WorkflowNode node && node.Id == nodeId);

                if (nodeBorder == null)
                {
    
                    return null;
                }

                // 3. 在Border中查找端口Ellipse（通过Name匹配）
                var ellipseName = $"{portName}Ellipse";
                var portEllipse = WorkflowVisualHelper.FindAllVisualChildren<VmExecutionPort>(nodeBorder)
                    .FirstOrDefault(e => e.Name == ellipseName);

                if (portEllipse == null)
                {
    
                    return null;
                }

                if(portEllipse.Visibility==Visibility.Collapsed&&nodeBorder.Tag is WorkflowNode owner)
                    return GetDefaultPortPosition(owner,portName);

                // 4. 使用热区边缘的实际执行锚点。
                var portCenterInEllipse = portEllipse.Anchor;

                // 5. 将端口中心点转换为Canvas坐标
                var canvasPosition = portEllipse.PointToCanvas(portCenterInEllipse);


                return canvasPosition;
            }
            catch (Exception ex)
            {

                return null;
            }
        }

        /// <summary>
        /// 通过节点Border查询所有端口位置
        /// </summary>
        public PortPositionMap QueryAllPortPositions(Border nodeBorder)
        {
            var positions = new PortPositionMap();

            if (nodeBorder?.Tag is not WorkflowNode node)
                return positions;

            var portNames = new[] { "TopPort", "BottomPort", "LeftPort", "RightPort" };

            foreach (var portName in portNames)
            {
                var position = QueryPortPosition(node.Name, portName);
                if (position.HasValue)
                    positions[portName] = position.Value;
            }

            return positions;
        }

        /// <summary>
        /// 降级方案：使用配置对象计算默认端口位置
        /// </summary>
        public Point GetDefaultPortPosition(WorkflowNode node, string portName)
        {
            return portName switch
            {
                "TopPort" => _styleConfig.GetTopPortPosition(node.Position),
                "BottomPort" => _styleConfig.GetBottomPortPosition(node.Position),
                "LeftPort" => _styleConfig.GetLeftPortPosition(node.Position),
                "RightPort" => _styleConfig.GetRightPortPosition(node.Position),
                _ => _styleConfig.GetRightPortPosition(node.Position)
            };
        }

        /// <summary>
        /// 验证端口位置是否正确（用于调试）
        /// </summary>
        public bool ValidatePortPosition(WorkflowNode node, string portName)
        {
            // 1. 查询视觉树中的实际位置
            var actualPosition = QueryPortPosition(node.Id, portName);
            if (!actualPosition.HasValue)
                return false;

            // 2. 获取配置计算的期望位置
            var expectedPosition = GetDefaultPortPosition(node, portName);

            // 3. 计算位置差异
            var diff = Math.Sqrt(
                Math.Pow(actualPosition.Value.X - expectedPosition.X, 2) +
                Math.Pow(actualPosition.Value.Y - expectedPosition.Y, 2)
            );

            var isValid = diff < 5.0; // 允许5像素误差



            return isValid;
        }
    }

    /// <summary>
    /// 端口位置映射字典
    /// </summary>
    public class PortPositionMap
    {
        private readonly System.Collections.Generic.Dictionary<string, Point> _positions =
            new System.Collections.Generic.Dictionary<string, Point>();

        public Point this[string portName]
        {
            get => _positions.TryGetValue(portName, out var pos) ? pos : new Point(0, 0);
            set => _positions[portName] = value;
        }

        public bool ContainsKey(string portName) => _positions.ContainsKey(portName);

        public System.Collections.Generic.Dictionary<string, Point>.KeyCollection Keys => _positions.Keys;
    }

    /// <summary>
    /// Canvas扩展方法
    /// </summary>
    public static class CanvasExtensions
    {
        /// <summary>
        /// 将元素上的点转换为Canvas坐标
        /// </summary>
        public static Point PointToCanvas(this UIElement element, Point point)
        {
            var canvas = element.FindVisualParent<System.Windows.Controls.Canvas>();
            if (canvas == null)
                return point;

            var transform = element.TransformToAncestor(canvas);
            return transform.Transform(point);
        }

        /// <summary>
        /// 查找指定类型的父级元素
        /// </summary>
        private static T? FindVisualParent<T>(this DependencyObject element) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is T t)
                    return t;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}

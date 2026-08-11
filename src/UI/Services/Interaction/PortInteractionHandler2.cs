using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shell.Controls.Canvas;

using SunEyeVision.UI.Features.Shell;

namespace SunEyeVision.UI.Services.Interaction
{
    /// <summary>
    /// 端口交互处理器 - 负责处理端口的高亮和交互
    /// </summary>
    public class PortInteractionHandler
    {
        #region 私有字段

        private readonly System.Windows.Controls.Canvas _canvas;
        private readonly MainWindowViewModel? _viewModel;

        private Border? _highlightedTargetBorder;
        private Ellipse? _highlightedTargetPort;
        private Brush? _originalPortFill;
        private Brush? _originalPortStroke;
        private double _originalPortStrokeThickness;

        private int _highlightCounter;
        private string? _lastHighlightedPort;
        private string? _directHitTargetPort;

        #endregion

        #region 事件

        public event EventHandler<PortHighlightEventArgs>? PortHighlighted;
        public event EventHandler<PortHighlightEventArgs>? PortHighlightCleared;

        #endregion

        #region 属性

        /// <summary>
        /// 当前高亮的目标节点
        /// </summary>
        public Border? HighlightedTargetBorder => _highlightedTargetBorder;

        /// <summary>
        /// 当前高亮的目标端口
        /// </summary>
        public Ellipse? HighlightedTargetPort => _highlightedTargetPort;

        #endregion

        #region 构造函数

        public PortInteractionHandler(
            System.Windows.Controls.Canvas canvas,
            MainWindowViewModel? viewModel)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _viewModel = viewModel;

            _highlightCounter = 0;
            _lastHighlightedPort = null;
            _directHitTargetPort = null;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 判断点击的端口
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="clickPoint">点击点</param>
        /// <returns>端口名称</returns>
        public string? DetermineClickedPort(WorkflowNode node, Point clickPoint)
        {
            var nodeCenter = CanvasHelper.GetNodeCenter(node);
            double offsetX = clickPoint.X - nodeCenter.X;
            double offsetY = clickPoint.Y - nodeCenter.Y;

            string? clickedPort = null;
            if (Math.Abs(offsetX) > Math.Abs(offsetY))
            {
                if (offsetX > 0)
                {
                    clickedPort = "RightPort";
                }
                else
                {
                    clickedPort = "LeftPort";
                }
            }
            else
            {
                if (offsetY > 0)
                {
                    clickedPort = "BottomPort";
                }
                else
                {
                    clickedPort = "TopPort";
                }
            }

            return clickedPort;
        }

        /// <summary>
        /// 高亮目标端口
        /// </summary>
        /// <param name="targetBorder">目标节点Border</param>
        /// <param name="sourceNode">源节点</param>
        public void HighlightTargetPort(Border? targetBorder, WorkflowNode? sourceNode)
        {
            if (targetBorder == null || sourceNode == null)
            {
                return;
            }

            _highlightCounter++;

            if (_highlightCounter % 5 != 0)
            {
                return;
            }

            var sourcePos = CanvasHelper.GetPortPosition(sourceNode, _directHitTargetPort ?? "RightPort");
            var targetNode = targetBorder.Tag as WorkflowNode;

            if (targetNode == null)
            {
                return;
            }

            var targetPos = CanvasHelper.GetNodeCenter(targetNode);
            var deltaX = targetPos.X - sourcePos.X;
            var deltaY = targetPos.Y - sourcePos.Y;

            string direction;
            string targetPortName;

            if (Math.Abs(deltaX) > Math.Abs(deltaY))
            {
                direction = "水平";
                targetPortName = deltaX > 0 ? "LeftPort" : "RightPort";
            }
            else
            {
                direction = "垂直";
                targetPortName = deltaY > 0 ? "TopPort" : "BottomPort";
            }

            var portElement = FindVisualChild<Ellipse>(targetBorder);
            if (portElement != null && portElement.Name == targetPortName + "Ellipse")
            {
                if (_highlightedTargetPort == null)
                {
                    _originalPortFill = portElement.Fill;
                    _originalPortStroke = portElement.Stroke;
                    _originalPortStrokeThickness = portElement.StrokeThickness;
                }

                portElement.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(CanvasConfig.Port.ActivePortColor));
                portElement.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(CanvasConfig.Port.ActivePortColor));
                portElement.StrokeThickness = CanvasConfig.Port.PortHoverSize;

                _highlightedTargetPort = portElement;
                _highlightedTargetBorder = targetBorder;
                _lastHighlightedPort = targetPortName;

                OnPortHighlighted(new PortHighlightEventArgs(targetNode, targetPortName));
            }
            else
            {
                OnPortHighlightCleared(new PortHighlightEventArgs(targetNode, targetPortName));
            }
        }

        /// <summary>
        /// 清除端口高亮
        /// </summary>
        public void ClearPortHighlight()
        {
            if (_highlightedTargetPort != null && _originalPortFill != null)
            {
                _highlightedTargetPort.Fill = _originalPortFill;
                _highlightedTargetPort.Stroke = _originalPortStroke ?? new SolidColorBrush(Colors.Transparent);
                _highlightedTargetPort.StrokeThickness = _originalPortStrokeThickness;
            }

            _highlightedTargetPort = null;
            _highlightedTargetBorder = null;
            _lastHighlightedPort = null;
        }

        /// <summary>
        /// 设置直接命中的目标端口
        /// </summary>
        /// <param name="portName">端口名称</param>
        public void SetDirectHitTargetPort(string? portName)
        {
            _directHitTargetPort = portName;
        }

        /// <summary>
        /// 查找指定类型的子元素
        /// </summary>
        public T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            if (parent is T typedChild)
            {
                return typedChild;
            }

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var visualChild = VisualTreeHelper.GetChild(parent, i);
                if (visualChild is T result)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 检查点是否在端口范围内
        /// </summary>
        /// <param name="point">测试点</param>
        /// <param name="portPosition">端口位置</param>
        /// <returns>是否命中</returns>
        public bool IsPointInPort(Point point, Point portPosition)
        {
            return CanvasHelper.IsPointInPort(point, portPosition, CanvasConfig.Port.PortHitTestDistance);
        }

        /// <summary>
        /// 查找最近的端口
        /// </summary>
        /// <param name="point">测试点</param>
        /// <param name="node">节点</param>
        /// <returns>端口名称和距离</returns>
        public (string? PortName, double Distance) FindNearestPort(Point point, WorkflowNode node)
        {
            var portPositions = CanvasHelper.GetAllPortPositions(node);
            string? nearestPort = null;
            double minDistance = double.MaxValue;

            foreach (var kvp in portPositions)
            {
                var distance = CanvasHelper.GetDistance(point, kvp.Value);
                if (distance < minDistance && distance <= CanvasConfig.Port.PortHitTestDistance)
                {
                    minDistance = distance;
                    nearestPort = kvp.Key;
                }
            }

            return (nearestPort, minDistance == double.MaxValue ? -1 : minDistance);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 触发端口高亮事件
        /// </summary>
        private void OnPortHighlighted(PortHighlightEventArgs e)
        {
            PortHighlighted?.Invoke(this, e);
        }

        /// <summary>
        /// 触发端口高亮清除事件
        /// </summary>
        private void OnPortHighlightCleared(PortHighlightEventArgs e)
        {
            PortHighlightCleared?.Invoke(this, e);
        }

        #endregion
    }

    #region 端口高亮事件参数

    /// <summary>
    /// 端口高亮事件参数
    /// </summary>
    public class PortHighlightEventArgs : EventArgs
    {
        /// <summary>
        /// 节点
        /// </summary>
        public WorkflowNode? Node { get; }

        /// <summary>
        /// 端口名称
        /// </summary>
        public string? PortName { get; }

        public PortHighlightEventArgs(WorkflowNode? node, string? portName)
        {
            Node = node;
            PortName = portName;
        }
    }

    #endregion
}

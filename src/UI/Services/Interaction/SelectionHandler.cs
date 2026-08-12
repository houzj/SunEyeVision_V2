using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Services.Interaction
{
    /// <summary>
    /// 工作流选择处理器
    /// 负责框选、多选等选择功能
    /// </summary>
    public class WorkflowSelectionHandler
    {
        private readonly WorkflowCanvasControl _canvasControl;
        private readonly MainWindowViewModel? _viewModel;

        // 框选相关
        private bool _isSelecting;
        private System.Windows.Point _selectionStartPoint;

        public WorkflowSelectionHandler(
            WorkflowCanvasControl canvasControl,
            MainWindowViewModel? viewModel)
        {
            _canvasControl = canvasControl;
            _viewModel = viewModel;
        }

        /// <summary>
        /// Canvas 鼠标左键按下 - 开始框选
        /// </summary>
        public void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var mousePos = e.GetPosition(_canvasControl.WorkflowCanvas);

            // 检查是否点击在节点上
            var hitResult = VisualTreeHelper.HitTest(_canvasControl.WorkflowCanvas, mousePos);
            if (hitResult?.VisualHit is DependencyObject obj)
            {
                while (obj != null)
                {
                    if (obj is Border border && border.Tag is WorkflowNode)
                    {
                        // 点击在节点上，不触发框选
                        return;
                    }
                    obj = VisualTreeHelper.GetParent(obj);
                }
            }

            // 检查是否按住 Shift 或 Ctrl 键（多选模式）
            bool isMultiSelect = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ||
                               (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            // 如果不是多选模式，清除所有选中状态
            if (!isMultiSelect)
            {
                ClearAllSelections();
            }

            // 开始框选
            _isSelecting = true;
            _selectionStartPoint = mousePos;

            // 显示框选矩形
            _canvasControl.SelectionBox.StartSelection(mousePos);

            e.Handled = true;
        }

        /// <summary>
        /// Canvas 鼠标移动 - 更新框选矩形
        /// </summary>
        public void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting)
                return;

            _isSelecting = false;
            _canvasControl.SelectionBox.EndSelection();

            // 选中框选区域内的节点
            SelectNodesInSelectionRectangle();

            e.Handled = true;
        }

        /// <summary>
        /// Canvas 鼠标移动 - 更新框选矩形
        /// </summary>
        public void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting)
                return;

            var currentPos = e.GetPosition(_canvasControl.WorkflowCanvas);

            // 更新框选矩形
            _canvasControl.SelectionBox.UpdateSelection(currentPos);

            e.Handled = true;
        }

        /// <summary>
        /// 选中框选区域内的节点
        /// </summary>
        private void SelectNodesInSelectionRectangle()
        {
            if (_canvasControl.CurrentWorkflowTab == null)
                return;

            var selectionRect = _canvasControl.SelectionBox.GetSelectionRect();

            foreach (var node in _canvasControl.CurrentWorkflowTab.WorkflowNodes)
            {
                var nodeElement = _canvasControl.WorkflowCanvas.Children
                    .OfType<Border>()
                    .FirstOrDefault(b => b.Tag == node);

                if (nodeElement != null)
                {
                    var nodePosition = new Rect(
                        System.Windows.Controls.Canvas.GetLeft(nodeElement),
                        System.Windows.Controls.Canvas.GetTop(nodeElement),
                        nodeElement.ActualWidth,
                        nodeElement.ActualHeight);

                    // 检查节点是否在框选区域内
                    if (selectionRect.IntersectsWith(nodePosition))
                    {
                        node.IsSelected = true;
                    }
                }
            }
        }

        /// <summary>
        /// 清除所有节点的选中状态
        /// </summary>
        public void ClearAllSelections()
        {
            if (_canvasControl.CurrentWorkflowTab != null)
            {
                foreach (var node in _canvasControl.CurrentWorkflowTab.WorkflowNodes)
                {
                    node.IsSelected = false;
                }
            }
        }

        /// <summary>
        /// 记录选中节点的初始位置
        /// </summary>
        public Dictionary<WorkflowNode, System.Windows.Point> RecordSelectedNodesPositions()
        {
            var positions = new Dictionary<WorkflowNode, System.Windows.Point>();

            if (_canvasControl.CurrentWorkflowTab == null)
                return positions;

            foreach (var node in _canvasControl.CurrentWorkflowTab.WorkflowNodes)
            {
                if (node.IsSelected)
                {
                    positions[node] = node.Position;
                }
            }

            return positions;
        }
    }
}

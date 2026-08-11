using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shell.Controls.Canvas;

using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Features.Shell;

namespace SunEyeVision.UI.Services.Interaction
{
    /// <summary>
    /// 节点拖拽处理器 - 负责处理节点的拖拽操作
    /// </summary>
    public class NodeDragHandler
    {
        #region 私有字段

        private readonly System.Windows.Controls.Canvas _canvas;
        private readonly MainWindowViewModel? _viewModel;
        private readonly Action<WorkflowNode?> _onNodeSelected;

        private bool _isDragging;
        private WorkflowNode? _draggedNode;
        private Point _startDragPosition;
        private Point _initialNodePosition;
        private Point[]? _selectedNodesInitialPositions;

        #endregion

        #region 事件

        public event EventHandler<NodeDragEventArgs>? DragStarted;
        public event EventHandler<NodeDragEventArgs>? Dragging;
        public event EventHandler<NodeDragEventArgs>? DragEnded;

        #endregion

        #region 属性

        /// <summary>
        /// 是否正在拖拽
        /// </summary>
        public bool IsDragging => _isDragging;

        /// <summary>
        /// 当前拖拽的节点
        /// </summary>
        public WorkflowNode? DraggedNode => _draggedNode;

        #endregion

        #region 构造函数

        public NodeDragHandler(
            System.Windows.Controls.Canvas canvas,
            MainWindowViewModel? viewModel,
            Action<WorkflowNode?> onNodeSelected)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _viewModel = viewModel;
            _onNodeSelected = onNodeSelected;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始拖拽节点
        /// </summary>
        /// <param name="node">要拖拽的节点</param>
        /// <param name="startPosition">拖拽起始位置</param>
        public void StartDrag(WorkflowNode node, Point startPosition)
        {
            if (node == null)
            {
                return;
            }

            _isDragging = true;
            _draggedNode = node;
            _initialNodePosition = node.Position;
            _startDragPosition = startPosition;

            _onNodeSelected?.Invoke(node);

            RecordSelectedNodesPositions();

            OnDragStarted(new NodeDragEventArgs(node, startPosition));
        }

        /// <summary>
        /// 更新拖拽位置
        /// </summary>
        /// <param name="currentPosition">当前位置</param>
        public void UpdateDrag(Point currentPosition)
        {
            if (!_isDragging || _draggedNode == null)
            {
                return;
            }

            if (_viewModel?.WorkflowTabViewModel.SelectedTab != null &&
                _selectedNodesInitialPositions != null)
            {
                var selectedNodes = _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes
                    .Where(n => n.IsSelected)
                    .ToList();

                var totalOffset = currentPosition - _startDragPosition;

                for (int i = 0; i < selectedNodes.Count && i < _selectedNodesInitialPositions.Length; i++)
                {
                    var newPos = new Point(
                        _selectedNodesInitialPositions[i].X + totalOffset.X,
                        _selectedNodesInitialPositions[i].Y + totalOffset.Y
                    );

                    selectedNodes[i].Position = newPos;
                }

                OnDragging(new NodeDragEventArgs(_draggedNode, currentPosition, totalOffset));
            }
            else
            {
                var offset = currentPosition - _startDragPosition;
                _draggedNode.Position = new Point(
                    _initialNodePosition.X + offset.X,
                    _initialNodePosition.Y + offset.Y
                );

                OnDragging(new NodeDragEventArgs(_draggedNode, currentPosition, offset));
            }
        }

        /// <summary>
        /// 结束拖拽
        /// </summary>
        public void EndDrag()
        {
            if (!_isDragging || _draggedNode == null)
            {
                return;
            }

            if (_viewModel?.WorkflowTabViewModel.SelectedTab != null)
            {
                var selectedNodes = _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes
                    .Where(n => n.IsSelected)
                    .ToList();

                if (selectedNodes.Count > 0)
                {
                    Vector delta = new Vector(0, 0);
                    if (_selectedNodesInitialPositions != null && selectedNodes.Count > 0)
                    {
                        delta = new Vector(
                            selectedNodes[0].Position.X - _selectedNodesInitialPositions[0].X,
                            selectedNodes[0].Position.Y - _selectedNodesInitialPositions[0].Y
                        );
                    }

                    if (delta.X != 0 || delta.Y != 0)
                    {
                        // 创建批量移动命令（新API：只需传入节点列表）
                        var batchCommand = new SunEyeVision.UI.Features.WorkflowUI.Commands.BatchMoveNodesCommand(
                            new List<WorkflowNode>(selectedNodes)
                        );

                        // 应用移动
                        batchCommand.UpdatePositions(delta);

                        // 执行命令（用于撤销/重做支持）
                        _viewModel.WorkflowTabViewModel.SelectedTab.CommandManager.Execute(batchCommand);
                    }
                }
            }

            _isDragging = false;
            _draggedNode = null;
            _selectedNodesInitialPositions = null;

            OnDragEnded(new NodeDragEventArgs(_draggedNode, _startDragPosition));
        }

        /// <summary>
        /// 取消拖拽
        /// </summary>
        public void CancelDrag()
        {
            if (!_isDragging || _draggedNode == null)
            {
                return;
            }

            if (_viewModel?.WorkflowTabViewModel.SelectedTab != null &&
                _selectedNodesInitialPositions != null)
            {
                var selectedNodes = _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes
                    .Where(n => n.IsSelected)
                    .ToList();

                for (int i = 0; i < selectedNodes.Count && i < _selectedNodesInitialPositions.Length; i++)
                {
                    selectedNodes[i].Position = _selectedNodesInitialPositions[i];
                }
            }
            else
            {
                _draggedNode.Position = _initialNodePosition;
            }

            _isDragging = false;
            _draggedNode = null;
            _selectedNodesInitialPositions = null;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 记录所有选中节点的初始位置
        /// </summary>
        private void RecordSelectedNodesPositions()
        {
            if (_viewModel?.WorkflowTabViewModel.SelectedTab == null)
            {
                return;
            }

            var selectedNodes = _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes
                .Where(n => n.IsSelected)
                .ToList();

            if (selectedNodes.Count > 0)
            {
                _selectedNodesInitialPositions = new Point[selectedNodes.Count];
                for (int i = 0; i < selectedNodes.Count; i++)
                {
                    _selectedNodesInitialPositions[i] = selectedNodes[i].Position;
                }
            }
        }

        /// <summary>
        /// 触发拖拽开始事件
        /// </summary>
        private void OnDragStarted(NodeDragEventArgs e)
        {
            DragStarted?.Invoke(this, e);
        }

        /// <summary>
        /// 触发拖拽中事件
        /// </summary>
        private void OnDragging(NodeDragEventArgs e)
        {
            Dragging?.Invoke(this, e);
        }

        /// <summary>
        /// 触发拖拽结束事件
        /// </summary>
        private void OnDragEnded(NodeDragEventArgs e)
        {
            DragEnded?.Invoke(this, e);
        }

        #endregion
    }

    #region 拖拽事件参数

    /// <summary>
    /// 拖拽事件参数
    /// </summary>
    public class NodeDragEventArgs : EventArgs
    {
        /// <summary>
        /// 拖拽的节点
        /// </summary>
        public WorkflowNode? Node { get; }

        /// <summary>
        /// 当前位置
        /// </summary>
        public Point Position { get; }

        /// <summary>
        /// 偏移量
        /// </summary>
        public Vector? Offset { get; }

        public NodeDragEventArgs(WorkflowNode? node, Point position, Vector? offset = null)
        {
            Node = node;
            Position = position;
            Offset = offset;
        }
    }

    #endregion
}

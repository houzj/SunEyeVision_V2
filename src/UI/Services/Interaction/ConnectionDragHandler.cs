using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Features.Shell;

namespace SunEyeVision.UI.Services.Interaction
{
    /// <summary>
    /// 连接拖拽处理器 - 负责处理连接的创建和拖拽
    /// </summary>
    public class ConnectionDragHandler
    {
        #region 私有字段

        private readonly System.Windows.Controls.Canvas _canvas;
        private readonly MainWindowViewModel? _viewModel;
        private readonly Action<WorkflowConnection> _onConnectionCreated;

        private bool _isDragging;
        private WorkflowNode? _sourceNode;
        private string? _sourcePort;
        private Point _startPosition;

        private System.Windows.Shapes.Path? _tempConnectionLine;

        #endregion

        #region 事件

        public event EventHandler<ConnectionDragEventArgs>? DragStarted;
        public event EventHandler<ConnectionDragEventArgs>? Dragging;
        public event EventHandler<ConnectionCreatedEventArgs>? ConnectionCreated;
        public event EventHandler<ConnectionValidationEventArgs>? ConnectionValidationFailed;

        #endregion

        #region 属性

        /// <summary>
        /// 是否正在拖拽连接
        /// </summary>
        public bool IsDragging => _isDragging;

        /// <summary>
        /// 源节点
        /// </summary>
        public WorkflowNode? SourceNode => _sourceNode;

        /// <summary>
        /// 源端口
        /// </summary>
        public string? SourcePort => _sourcePort;

        /// <summary>
        /// 临时连接线（用于显示拖拽预览）
        /// </summary>
        public System.Windows.Shapes.Path? TempConnectionLine => _tempConnectionLine;

        #endregion

        #region 构造函数

        public ConnectionDragHandler(
            System.Windows.Controls.Canvas canvas,
            MainWindowViewModel? viewModel,
            Action<WorkflowConnection> onConnectionCreated)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _viewModel = viewModel;
            _onConnectionCreated = onConnectionCreated;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始拖拽连接
        /// </summary>
        /// <param name="sourceNode">源节点</param>
        /// <param name="sourcePort">源端口</param>
        /// <param name="startPosition">起始位置</param>
        public void StartDrag(WorkflowNode sourceNode, string sourcePort, Point startPosition)
        {
            if (sourceNode == null)
            {
                return;
            }

            _isDragging = true;
            _sourceNode = sourceNode;
            _sourcePort = sourcePort;
            _startPosition = startPosition;

            ShowTempConnectionLine(startPosition);

            _canvas.CaptureMouse();

            OnDragStarted(new ConnectionDragEventArgs(sourceNode, sourcePort, startPosition));
        }

        /// <summary>
        /// 更新拖拽位置
        /// </summary>
        /// <param name="currentPosition">当前位置</param>
        public void UpdateDrag(Point currentPosition)
        {
            if (!_isDragging || _tempConnectionLine == null)
            {
                return;
            }

            if (_tempConnectionLine.Data is PathGeometry geometry && geometry.Figures.Count > 0)
            {
                var figure = geometry.Figures[0];
                if (figure.Segments.Count > 0)
                {
                    var segment = figure.Segments[0] as LineSegment;
                    if (segment != null)
                    {
                        segment.Point = currentPosition;
                    }
                }
            }

                OnDragging(new ConnectionDragEventArgs(_sourceNode, _sourcePort, currentPosition));
            }

        /// <summary>
        /// 结束拖拽并创建连接
        /// </summary>
        /// <param name="targetNode">目标节点</param>
        /// <param name="targetPort">目标端口</param>
        /// <returns>是否成功创建连接</returns>
        public bool EndDrag(WorkflowNode? targetNode, string? targetPort)
        {
            if (!_isDragging || _sourceNode == null)
            {
                return false;
            }

            HideTempConnectionLine();

            if (targetNode != null && targetNode != _sourceNode)
            {
                var validationResult = ValidateConnection(_sourceNode, targetNode);

                if (!validationResult.IsValid)
                {
                    OnConnectionValidationFailed(new ConnectionValidationEventArgs(_sourceNode, targetNode, validationResult.ErrorMessage));
                    return false;
                }

                var connection = CreateConnection(_sourceNode, targetNode, targetPort);
                if (connection != null)
                {
                    _onConnectionCreated?.Invoke(connection);
                    OnConnectionCreated(new ConnectionCreatedEventArgs(connection));
                    return true;
                }
            }

            ResetDragState();
            return false;
        }

        /// <summary>
        /// 取消拖拽
        /// </summary>
        public void CancelDrag()
        {
            if (!_isDragging)
            {
                return;
            }

            HideTempConnectionLine();
            ResetDragState();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 显示临时连接线
        /// </summary>
        private void ShowTempConnectionLine(Point startPosition)
        {
            if (_tempConnectionLine == null)
            {
                _tempConnectionLine = new System.Windows.Shapes.Path
                {
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(CanvasConfig.Connection.DefaultColor)),
                    StrokeThickness = CanvasConfig.Connection.DefaultThickness,
                    StrokeDashArray = new DoubleCollection(new double[] { 4, 2 }),
                    Visibility = Visibility.Visible
                };

                _canvas.Children.Add(_tempConnectionLine);
            }

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = startPosition, IsClosed = false };
            figure.Segments.Add(new LineSegment(startPosition, true));
            geometry.Figures.Add(figure);
            _tempConnectionLine.Data = geometry;
        }

        /// <summary>
        /// 隐藏临时连接线
        /// </summary>
        private void HideTempConnectionLine()
        {
            if (_tempConnectionLine != null)
            {
                _tempConnectionLine.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 重置拖拽状态
        /// </summary>
        private void ResetDragState()
        {
            _isDragging = false;
            _sourceNode = null;
            _sourcePort = null;
        }

        /// <summary>
        /// 验证连接
        /// </summary>
        private ValidationResult ValidateConnection(
            WorkflowNode sourceNode,
            WorkflowNode targetNode)
        {
            if (sourceNode == null)
            {
                return new ValidationResult(false, "源节点不能为空");
            }

            if (targetNode == null)
            {
                return new ValidationResult(false, "目标节点不能为空");
            }

            if (sourceNode.Id == targetNode.Id)
            {
                return new ValidationResult(false, "不允许自连接");
            }

            if (_viewModel?.WorkflowTabViewModel.SelectedTab == null)
            {
                return new ValidationResult(false, "当前标签页为空");
            }

            var existingConnection = _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowConnections
                .FirstOrDefault(c => c.SourceNodeId == sourceNode.Id && c.TargetNodeId == targetNode.Id);

            if (existingConnection != null)
            {
                return new ValidationResult(false, "连接已存在");
            }

            var reverseConnection = _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowConnections
                .FirstOrDefault(c => c.TargetNodeId == sourceNode.Id && c.SourceNodeId == targetNode.Id);

            if (reverseConnection != null)
            {
                return new ValidationResult(false, "反向连接已存在");
            }

            return new ValidationResult(true, string.Empty);
        }

        /// <summary>
        /// 创建连接
        /// </summary>
        private WorkflowConnection? CreateConnection(
            WorkflowNode sourceNode,
            WorkflowNode targetNode,
            string? targetPort)
        {
            if (_viewModel?.WorkflowTabViewModel.SelectedTab == null)
            {
                return null;
            }

            var connectionId = Guid.NewGuid().ToString();
            var connection = new WorkflowConnection(
                connectionId,
                sourceNode.Id,
                targetNode.Id);

            connection.SourcePort = _sourcePort ?? "RightPort";
            connection.TargetPort = targetPort ?? "LeftPort";

            var sourcePos = CanvasHelper.GetPortPosition(sourceNode, connection.SourcePort);
            var targetPos = CanvasHelper.GetPortPosition(targetNode, connection.TargetPort);

            connection.SourcePosition = sourcePos;
            connection.TargetPosition = targetPos;

            // 通过 MainWindowViewModel 添加连接（记录日志）
            if (_viewModel != null)
            {
                _viewModel.AddConnectionToWorkflow(connection);
            }

            return connection;
        }

        /// <summary>
        /// 触发拖拽开始事件
        /// </summary>
        private void OnDragStarted(ConnectionDragEventArgs e)
        {
            DragStarted?.Invoke(this, e);
        }

        /// <summary>
        /// 触发拖拽中事件
        /// </summary>
        private void OnDragging(ConnectionDragEventArgs e)
        {
            Dragging?.Invoke(this, e);
        }

        /// <summary>
        /// 触发连接创建事件
        /// </summary>
        private void OnConnectionCreated(ConnectionCreatedEventArgs e)
        {
            ConnectionCreated?.Invoke(this, e);
        }

        /// <summary>
        /// 触发连接验证失败事件
        /// </summary>
        private void OnConnectionValidationFailed(ConnectionValidationEventArgs e)
        {
            ConnectionValidationFailed?.Invoke(this, e);
        }

        #endregion
    }

    #region 事件参数类

    /// <summary>
    /// 连接拖拽事件参数
    /// </summary>
    public class ConnectionDragEventArgs : EventArgs
    {
        public WorkflowNode? SourceNode { get; }
        public string? SourcePort { get; }
        public Point Position { get; }

        public ConnectionDragEventArgs(WorkflowNode? sourceNode, string? sourcePort, Point position)
        {
            SourceNode = sourceNode;
            SourcePort = sourcePort;
            Position = position;
        }
    }

    /// <summary>
    /// 连接创建事件参数
    /// </summary>
    public class ConnectionCreatedEventArgs : EventArgs
    {
        public WorkflowConnection Connection { get; }

        public ConnectionCreatedEventArgs(WorkflowConnection connection)
        {
            Connection = connection;
        }
    }

    /// <summary>
    /// 连接验证失败事件参数
    /// </summary>
    public class ConnectionValidationEventArgs : EventArgs
    {
        public WorkflowNode? SourceNode { get; }
        public WorkflowNode? TargetNode { get; }
        public string ErrorMessage { get; }

        public ConnectionValidationEventArgs(WorkflowNode? sourceNode, WorkflowNode? targetNode, string errorMessage)
        {
            SourceNode = sourceNode;
            TargetNode = targetNode;
            ErrorMessage = errorMessage;
        }
    }

    #endregion

    #region 验证结果类

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        public ValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }

    #endregion
}

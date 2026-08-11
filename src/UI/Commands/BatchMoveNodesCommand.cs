using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.UI.Shared;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.UI.Commands
{
    /// <summary>
    /// 批量移动节点命令 - 支持拖拽期间的批量通知抑制
    /// </summary>
    /// <remarks>
    /// 使用方式：
    /// <code>
    /// // 开始拖拽
    /// command.BeginDrag();
    /// // 拖拽过程中更新位置
    /// command.UpdatePositions(delta);
    /// // 结束拖拽
    /// command.EndDrag();
    /// </code>
    /// </remarks>
    public class BatchMoveNodesCommand : IUndoableCommand
    {
        private readonly List<WorkflowNode> _nodes;
        private readonly Dictionary<WorkflowNode, Point> _originalPositions;
        private IDisposable? _batchSuppress;
        private bool _isDragging;

        /// <summary>
        /// 是否正在拖拽
        /// </summary>
        public bool IsDragging
        {
            get => _isDragging;
            set
            {
                if (_isDragging != value)
                {
                    _isDragging = value;
                }
            }
        }

        public BatchMoveNodesCommand(List<WorkflowNode> nodes)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _originalPositions = new Dictionary<WorkflowNode, Point>();
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        /// <summary>
        /// 开始拖拽 - 抑制属性变更通知
        /// </summary>
        public void BeginDrag()
        {
            if (IsDragging)
                return;

            // 记录所有节点的原始位置
            _originalPositions.Clear();
            foreach (var node in _nodes)
            {
                _originalPositions[node] = node.Position;
            }

            // 开始抑制所有节点的属性变更通知
            var suppressors = new List<IDisposable>();
            foreach (var node in _nodes)
            {
                suppressors.Add(node.BeginSuppressNotifications());
            }
            _batchSuppress = new CompositeDisposable(suppressors);

            IsDragging = true;
        }

        /// <summary>
        /// 更新节点位置（拖拽过程中调用）
        /// </summary>
        /// <param name="delta">偏移量</param>
        public void UpdatePositions(Vector delta)
        {
            if (!IsDragging)
                return;

            foreach (var kvp in _originalPositions)
            {
                var node = kvp.Key;
                var originalPos = kvp.Value;
                node.Position = new Point(originalPos.X + delta.X, originalPos.Y + delta.Y);
            }
        }

        /// <summary>
        /// 结束拖拽 - 释放属性变更通知
        /// </summary>
        public void EndDrag()
        {
            if (!IsDragging)
                return;

            // 释放批量抑制，触发所有属性变更通知
            if (_batchSuppress != null)
            {
                _batchSuppress.Dispose();
                _batchSuppress = null;
            }

            IsDragging = false;
        }

        /// <summary>
        /// 执行命令（用于撤销/重做）
        /// </summary>
        public void Execute(object? parameter)
        {
            // 计算当前偏移量
            if (_originalPositions.Count == 0 || _nodes.Count == 0)
                return;

            var firstNode = _nodes[0];
            var originalPos = _originalPositions[firstNode];
            var delta = new Vector(firstNode.Position.X - originalPos.X, firstNode.Position.Y - originalPos.Y);

            // 使用批量抑制应用移动
            using (BeginBatchSuppress())
            {
                foreach (var node in _nodes)
                {
                    if (_originalPositions.TryGetValue(node, out var origPos))
                    {
                        node.Position = new Point(origPos.X + delta.X, origPos.Y + delta.Y);
                    }
                }
            }
        }

        /// <summary>
        /// 撤销命令
        /// </summary>
        public void Undo()
        {
            // 使用批量抑制恢复原始位置
            using (BeginBatchSuppress())
            {
                foreach (var kvp in _originalPositions)
                {
                    var node = kvp.Key;
                    var originalPos = kvp.Value;
                    node.Position = originalPos;
                }
            }
        }

        /// <summary>
        /// 开始批量抑制所有节点的属性变更通知
        /// </summary>
        private IDisposable BeginBatchSuppress()
        {
            var suppressors = new List<IDisposable>();
            foreach (var node in _nodes)
            {
                suppressors.Add(node.BeginSuppressNotifications());
            }
            return new CompositeDisposable(suppressors);
        }
    }

    /// <summary>
    /// 组合式 IDisposable - 管理多个可释放对象
    /// </summary>
    internal class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables;
        private bool _disposed = false;

        public CompositeDisposable(List<IDisposable> disposables)
        {
            _disposables = disposables ?? throw new ArgumentNullException(nameof(disposables));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            // 反向释放，先释放后添加的
            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                try
                {
                    _disposables[i].Dispose();
                }
                catch (Exception ex)
                {
                    // 静默处理释放异常，避免级联失败
                }
            }
            _disposables.Clear();
        }
    }
}

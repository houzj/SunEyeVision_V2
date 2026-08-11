using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Services.Node;

namespace SunEyeVision.UI.Services.Node
{
    /// <summary>
    /// 节点选择服务接口
    /// </summary>
    public interface INodeSelectionService
    {
        /// <summary>
        /// 选中的节点集合
        /// </summary>
        ObservableCollection<WorkflowNode> SelectedNodes { get; }

        /// <summary>
        /// 选中状态变化事件
        /// </summary>
        event EventHandler<SelectionChangedEventArgs> SelectionChanged;

        /// <summary>
        /// 选择单个节点
        /// </summary>
        void SelectNode(WorkflowNode node, bool addToSelection = false);

        /// <summary>
        /// 选择多个节点
        /// </summary>
        void SelectNodes(IEnumerable<WorkflowNode> nodes, bool addToSelection = false);

        /// <summary>
        /// 取消选择节点
        /// </summary>
        void DeselectNode(WorkflowNode node);

        /// <summary>
        /// 取消所有选择
        /// </summary>
        void ClearSelection();

        /// <summary>
        /// 检查节点是否被选中
        /// </summary>
        bool IsNodeSelected(WorkflowNode node);

        /// <summary>
        /// 获取选中节点的边界矩形
        /// </summary>
        Rect GetSelectionBounds();

        /// <summary>
        /// 在指定区域内选择节点
        /// </summary>
        void SelectNodesInRect(Rect selectionRect);

        /// <summary>
        /// 记录选中节点的初始位置
        /// </summary>
        void RecordSelectedNodesPositions();

        /// <summary>
        /// 获取选中节点的位置偏移
        /// </summary>
        Dictionary<string, Vector> GetSelectedNodesOffsets();
    }

    /// <summary>
    /// 选择变化事件参数
    /// </summary>
    public class SelectionChangedEventArgs : EventArgs
    {
        public List<WorkflowNode> AddedNodes { get; set; }
        public List<WorkflowNode> RemovedNodes { get; set; }
        public SelectionAction Action { get; set; }
    }

    /// <summary>
    /// 选择动作类型
    /// </summary>
    public enum SelectionAction
    {
        /// <summary>
        /// 添加选择
        /// </summary>
        Add,

        /// <summary>
        /// 移除选择
        /// </summary>
        Remove,

        /// <summary>
        /// 替换选择
        /// </summary>
        Replace,

        /// <summary>
        /// 清除选择
        /// </summary>
        Clear
    }

    /// <summary>
    /// 节点选择服务 - 管理节点的选择状态
    /// </summary>
    public class NodeSelectionService : INodeSelectionService
    {
        private readonly ObservableCollection<WorkflowNode> _allNodes;
        private readonly Dictionary<string, Point> _initialPositions = new Dictionary<string, Point>();

        public ObservableCollection<WorkflowNode> SelectedNodes { get; } = new ObservableCollection<WorkflowNode>();

        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;

        public NodeSelectionService(ObservableCollection<WorkflowNode> allNodes)
        {
            _allNodes = allNodes ?? throw new ArgumentNullException(nameof(allNodes));
        }

        public void SelectNode(WorkflowNode node, bool addToSelection = false)
        {
            if (node == null)
            {
                return;
            }

            if (addToSelection)
            {
                // 切换选择状态
                if (SelectedNodes.Contains(node))
                {
                    DeselectNode(node);
                }
                else
                {
                    SelectNodes(new[] { node }, true);
                }
            }
            else
            {
                // 替换选择
                SelectNodes(new[] { node }, false);
            }
        }

        public void SelectNodes(IEnumerable<WorkflowNode> nodes, bool addToSelection = false)
        {
            if (nodes == null)
            {
                return;
            }

            var nodesList = nodes.Where(n => n != null).ToList();
            if (nodesList.Count == 0)
            {
                return;
            }

            if (addToSelection)
            {
                // 添加到选择
                var addedNodes = nodesList.Where(n => !SelectedNodes.Contains(n)).ToList();
                foreach (var node in addedNodes)
                {
                    SelectedNodes.Add(node);
                    node.IsSelected = true;
                }

                if (addedNodes.Count > 0)
                {
                    OnSelectionChanged(new SelectionChangedEventArgs
                    {
                        AddedNodes = addedNodes,
                        RemovedNodes = new List<WorkflowNode>(),
                        Action = SelectionAction.Add
                    });
                }
            }
            else
            {
                // 替换选择
                var removedNodes = SelectedNodes.ToList();
                SelectedNodes.Clear();
                foreach (var node in removedNodes)
                {
                    node.IsSelected = false;
                }

                foreach (var node in nodesList)
                {
                    SelectedNodes.Add(node);
                    node.IsSelected = true;
                }

                OnSelectionChanged(new SelectionChangedEventArgs
                {
                    AddedNodes = nodesList,
                    RemovedNodes = removedNodes,
                    Action = SelectionAction.Replace
                });
            }
        }

        public void DeselectNode(WorkflowNode node)
        {
            if (node == null || !SelectedNodes.Contains(node))
            {
                return;
            }

            SelectedNodes.Remove(node);
            node.IsSelected = false;

            OnSelectionChanged(new SelectionChangedEventArgs
            {
                AddedNodes = new List<WorkflowNode>(),
                RemovedNodes = new List<WorkflowNode> { node },
                Action = SelectionAction.Remove
            });
        }

        public void ClearSelection()
        {
            if (SelectedNodes.Count == 0)
            {
                return;
            }

            var removedNodes = SelectedNodes.ToList();
            SelectedNodes.Clear();
            foreach (var node in removedNodes)
            {
                node.IsSelected = false;
            }

            OnSelectionChanged(new SelectionChangedEventArgs
            {
                AddedNodes = new List<WorkflowNode>(),
                RemovedNodes = removedNodes,
                Action = SelectionAction.Clear
            });
        }

        public bool IsNodeSelected(WorkflowNode node)
        {
            return node != null && SelectedNodes.Contains(node);
        }

        public Rect GetSelectionBounds()
        {
            if (SelectedNodes.Count == 0)
            {
                return Rect.Empty;
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (var node in SelectedNodes)
            {
                // 使用节点的NodeRect计算边界（Position为HitArea中心点）
                var nodeRect = node.NodeRect;

                minX = Math.Min(minX, nodeRect.X);
                minY = Math.Min(minY, nodeRect.Y);
                maxX = Math.Max(maxX, nodeRect.X + nodeRect.Width);
                maxY = Math.Max(maxY, nodeRect.Y + nodeRect.Height);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        public void SelectNodesInRect(Rect selectionRect)
        {
            if (selectionRect.IsEmpty)
            {
                return;
            }

            var nodesInRect = _allNodes.Where(node =>
            {
                // 使用节点的NodeRect（Position为HitArea中心点）
                var nodeRect = node.NodeRect;
                return selectionRect.IntersectsWith(nodeRect);
            }).ToList();

            SelectNodes(nodesInRect, true);
        }

        public void RecordSelectedNodesPositions()
        {
            _initialPositions.Clear();
            foreach (var node in SelectedNodes)
            {
                _initialPositions[node.Id] = node.Position;
            }
        }

        public Dictionary<string, Vector> GetSelectedNodesOffsets()
        {
            var offsets = new Dictionary<string, Vector>();
            foreach (var node in SelectedNodes)
            {
                if (_initialPositions.TryGetValue(node.Id, out var initialPos))
                {
                    offsets[node.Id] = new Vector(
                        node.Position.X - initialPos.X,
                        node.Position.Y - initialPos.Y
                    );
                }
            }
            return offsets;
        }

        protected virtual void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, e);
        }
    }
}

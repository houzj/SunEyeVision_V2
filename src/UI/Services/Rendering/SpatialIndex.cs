using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Canvas;

namespace SunEyeVision.UI.Services.Rendering
{
    /// <summary>
    /// ռӿ
    /// </summary>
    public interface ISpatialIndex
    {
        void Insert(WorkflowNode node);
        void Remove(WorkflowNode node);
        void Update(WorkflowNode node);
        List<WorkflowNode> Query(Rect bounds);
        List<WorkflowNode> Query(Point point, double radius);
        void Clear();
        int Count { get; }
    }

    /// <summary>
    /// ռ - Ϊ񣬿ٲҽ?
    /// </summary>
    public class GridSpatialIndex : ISpatialIndex
    {
        private readonly Dictionary<(int, int), List<WorkflowNode>> _grid;
        private readonly double _cellSize;
        private readonly Dictionary<string, (int, int)> _nodeCellMap;
        private readonly object _lockObj;

        public int Count { get; private set; }

        public GridSpatialIndex(double cellSize = 200)
        {
            _cellSize = cellSize;
            _grid = new Dictionary<(int, int), List<WorkflowNode>>();
            _nodeCellMap = new Dictionary<string, (int, int)>();
            _lockObj = new object();
        }

        public void Insert(WorkflowNode node)
        {
            lock (_lockObj)
            {
                var cell = GetCell(node.Position);
                if (!_grid.ContainsKey(cell))
                {
                    _grid[cell] = new List<WorkflowNode>();
                }
                _grid[cell].Add(node);
                _nodeCellMap[node.Id] = cell;
                Count++;
            }
        }

        public void Remove(WorkflowNode node)
        {
            lock (_lockObj)
            {
                if (_nodeCellMap.TryGetValue(node.Id, out var cell))
                {
                    if (_grid.TryGetValue(cell, out var nodes))
                    {
                        nodes.Remove(node);
                        if (nodes.Count == 0)
                        {
                            _grid.Remove(cell);
                        }
                    }
                    _nodeCellMap.Remove(node.Id);
                    Count--;
                }
            }
        }

        public void Update(WorkflowNode node)
        {
            lock (_lockObj)
            {
                if (_nodeCellMap.TryGetValue(node.Id, out var oldCell))
                {
                    var newCell = GetCell(node.Position);
                    if (oldCell != newCell)
                    {
                        if (_grid.TryGetValue(oldCell, out var nodes))
                        {
                            nodes.Remove(node);
                            if (nodes.Count == 0)
                            {
                                _grid.Remove(oldCell);
                            }
                        }

                        if (!_grid.ContainsKey(newCell))
                        {
                            _grid[newCell] = new List<WorkflowNode>();
                        }
                        _grid[newCell].Add(node);
                        _nodeCellMap[node.Id] = newCell;
                    }
                }
            }
        }

        public List<WorkflowNode> Query(Rect bounds)
        {
            lock (_lockObj)
            {
                var result = new List<WorkflowNode>();
                var minCell = GetCell(new Point(bounds.Left, bounds.Top));
                var maxCell = GetCell(new Point(bounds.Right, bounds.Bottom));

                for (int x = minCell.Item1; x <= maxCell.Item1; x++)
                {
                    for (int y = minCell.Item2; y <= maxCell.Item2; y++)
                    {
                        var cell = (x, y);
                        if (_grid.TryGetValue(cell, out var nodes))
                        {
                            foreach (var node in nodes)
                            {
                                var nodeBounds = GetNodeBounds(node);
                                if (bounds.IntersectsWith(nodeBounds))
                                {
                                    result.Add(node);
                                }
                            }
                        }
                    }
                }

                return result;
            }
        }

        public List<WorkflowNode> Query(Point point, double radius)
        {
            var bounds = new Rect(
                point.X - radius,
                point.Y - radius,
                radius * 2,
                radius * 2
            );
            return Query(bounds);
        }

        public void Clear()
        {
            lock (_lockObj)
            {
                _grid.Clear();
                _nodeCellMap.Clear();
                Count = 0;
            }
        }

        private (int, int) GetCell(Point point)
        {
            int x = (int)Math.Floor(point.X / _cellSize);
            int y = (int)Math.Floor(point.Y / _cellSize);
            return (x, y);
        }

        private Rect GetNodeBounds(WorkflowNode node)
        {
            return new Rect(
                node.Position.X,
                node.Position.Y,
                CanvasConfig.NodeWidth,
                CanvasConfig.NodeHeight
            );
        }
    }

    /// <summary>
    /// Ĳռ?- ڴģ
    /// </summary>
    public class QuadTreeSpatialIndex : ISpatialIndex
    {
        private QuadTreeNode? _root;
        private readonly Rect _bounds;
        private readonly int _capacity;
        private readonly int _maxDepth;
        private readonly object _lockObj;

        public int Count { get; private set; }

        public QuadTreeSpatialIndex(Rect bounds, int capacity = 10, int maxDepth = 8)
        {
            _bounds = bounds;
            _capacity = capacity;
            _maxDepth = maxDepth;
            _lockObj = new object();
            _root = new QuadTreeNode(bounds, capacity, maxDepth, 0);
        }

        public void Insert(WorkflowNode node)
        {
            lock (_lockObj)
            {
                _root?.Insert(node);
                Count++;
            }
        }

        public void Remove(WorkflowNode node)
        {
            lock (_lockObj)
            {
                _root?.Remove(node);
                Count--;
            }
        }

        public void Update(WorkflowNode node)
        {
            lock (_lockObj)
            {
                _root?.Update(node);
            }
        }

        public List<WorkflowNode> Query(Rect bounds)
        {
            lock (_lockObj)
            {
                return _root?.Query(bounds) ?? new List<WorkflowNode>();
            }
        }

        public List<WorkflowNode> Query(Point point, double radius)
        {
            var bounds = new Rect(
                point.X - radius,
                point.Y - radius,
                radius * 2,
                radius * 2
            );
            return Query(bounds);
        }

        public void Clear()
        {
            lock (_lockObj)
            {
                _root = new QuadTreeNode(_bounds, _capacity, _maxDepth, 0);
                Count = 0;
            }
        }

        /// <summary>
        /// Ĳ?
        /// </summary>
        private class QuadTreeNode
        {
            private readonly Rect _bounds;
            private readonly int _capacity;
            private readonly int _maxDepth;
            private readonly int _depth;
            private readonly List<WorkflowNode> _nodes;
            private QuadTreeNode[]? _children;

            public bool IsLeaf => _children == null;
            public bool IsFull => _nodes.Count >= _capacity;
            public bool HasChildren => _children != null;

            public QuadTreeNode(Rect bounds, int capacity, int maxDepth, int depth)
            {
                _bounds = bounds;
                _capacity = capacity;
                _maxDepth = maxDepth;
                _depth = depth;
                _nodes = new List<WorkflowNode>();
            }

            public void Insert(WorkflowNode node)
            {
                if (!IsLeaf)
                {
                    InsertToChildren(node);
                    return;
                }

                if (!IsFull || _depth >= _maxDepth)
                {
                    _nodes.Add(node);
                    return;
                }

                Split();
                InsertToChildren(node);
            }

            public void Remove(WorkflowNode node)
            {
                if (IsLeaf)
                {
                    _nodes.Remove(node);
                    return;
                }

                foreach (var child in _children!)
                {
                    if (child.Contains(node))
                    {
                        child.Remove(node);
                        break;
                    }
                }
            }

            public void Update(WorkflowNode node)
            {
                Remove(node);
                Insert(node);
            }

            public List<WorkflowNode> Query(Rect bounds)
            {
                var result = new List<WorkflowNode>();

                if (!_bounds.IntersectsWith(bounds))
                    return result;

                if (IsLeaf)
                {
                    foreach (var node in _nodes)
                    {
                        var nodeBounds = GetNodeBounds(node);
                        if (bounds.IntersectsWith(nodeBounds))
                        {
                            result.Add(node);
                        }
                    }
                }
                else
                {
                    foreach (var child in _children!)
                    {
                        result.AddRange(child.Query(bounds));
                    }
                }

                return result;
            }

            private bool Contains(WorkflowNode node)
            {
                var nodeBounds = GetNodeBounds(node);
                return _bounds.IntersectsWith(nodeBounds);
            }

            private void Split()
            {
                var halfWidth = _bounds.Width / 2;
                var halfHeight = _bounds.Height / 2;
                var x = _bounds.X;
                var y = _bounds.Y;

                _children = new QuadTreeNode[4];
                _children[0] = new QuadTreeNode(new Rect(x, y, halfWidth, halfHeight), _capacity, _maxDepth, _depth + 1);
                _children[1] = new QuadTreeNode(new Rect(x + halfWidth, y, halfWidth, halfHeight), _capacity, _maxDepth, _depth + 1);
                _children[2] = new QuadTreeNode(new Rect(x, y + halfHeight, halfWidth, halfHeight), _capacity, _maxDepth, _depth + 1);
                _children[3] = new QuadTreeNode(new Rect(x + halfWidth, y + halfHeight, halfWidth, halfHeight), _capacity, _maxDepth, _depth + 1);

                foreach (var node in _nodes)
                {
                    InsertToChildren(node);
                }

                _nodes.Clear();
            }

            private void InsertToChildren(WorkflowNode node)
            {
                foreach (var child in _children!)
                {
                    if (child.Contains(node))
                    {
                        child.Insert(node);
                        return;
                    }
                }
            }

            private Rect GetNodeBounds(WorkflowNode node)
            {
                return new Rect(
                    node.Position.X,
                    node.Position.Y,
                    CanvasConfig.NodeWidth,
                    CanvasConfig.NodeHeight
                );
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.ViewModels;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Logic
{
    /// <summary>
    /// 编辑动作接口
    /// </summary>
    public interface IEditAction
    {
        /// <summary>
        /// 撤销
        /// </summary>
        void Undo();

        /// <summary>
        /// 重做
        /// </summary>
        void Redo();

        /// <summary>
        /// 动作描述
        /// </summary>
        string Description { get; }
    }

    /// <summary>
    /// 编辑历史管理器
    /// </summary>
    public class EditHistory
    {
        private readonly Stack<IEditAction> _undoStack = new Stack<IEditAction>();
        private readonly Stack<IEditAction> _redoStack = new Stack<IEditAction>();
        private readonly int _maxHistorySize;

        /// <summary>
        /// 最大历史记录数量
        /// </summary>
        public int MaxHistorySize => _maxHistorySize;

        /// <summary>
        /// 可撤销数量
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// 可重做数量
        /// </summary>
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// 是否可撤销
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// 是否可重做
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// 历史变更事件
        /// </summary>
        public event EventHandler? HistoryChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        public EditHistory(int maxHistorySize = 50)
        {
            _maxHistorySize = maxHistorySize;
        }

        /// <summary>
        /// 添加编辑动作
        /// </summary>
        public void AddAction(IEditAction action)
        {
            _undoStack.Push(action);
            _redoStack.Clear(); // 新动作清除重做栈

            // 限制历史记录大小
            while (_undoStack.Count > _maxHistorySize)
            {
                var temp = new Stack<IEditAction>();
                while (_undoStack.Count > _maxHistorySize / 2)
                {
                    temp.Push(_undoStack.Pop());
                }
                _undoStack.Clear();
                while (temp.Count > 0)
                {
                    _undoStack.Push(temp.Pop());
                }
            }

            OnHistoryChanged();
        }

        /// <summary>
        /// 执行并记录编辑动作
        /// </summary>
        public void ExecuteAction(IEditAction action)
        {
            AddAction(action);
        }

        /// <summary>
        /// 撤销
        /// </summary>
        public void Undo()
        {
            if (!CanUndo) return;

            var action = _undoStack.Pop();
            _redoStack.Push(action);
            action.Undo();

            OnHistoryChanged();
        }

        /// <summary>
        /// 重做
        /// </summary>
        public void Redo()
        {
            if (!CanRedo) return;

            var action = _redoStack.Pop();
            _undoStack.Push(action);
            action.Redo();

            OnHistoryChanged();
        }

        /// <summary>
        /// 清除所有历史
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnHistoryChanged();
        }

        /// <summary>
        /// 获取撤销描述
        /// </summary>
        public string? GetUndoDescription()
        {
            return CanUndo ? _undoStack.Peek().Description : null;
        }

        /// <summary>
        /// 获取重做描述
        /// </summary>
        public string? GetRedoDescription()
        {
            return CanRedo ? _redoStack.Peek().Description : null;
        }

        private void OnHistoryChanged()
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    #region 具体编辑动作

    /// <summary>
    /// 创建区域动作
    /// </summary>
    public class CreateRegionAction : IEditAction
    {
        private readonly RegionData _region;
        private readonly RegionEditorViewModel _editor;

        public string Description => $"创建 {_region.GetShapeType()}";

        public CreateRegionAction(RegionData region, RegionEditorViewModel editor)
        {
            _region = region;
            _editor = editor;
        }

        public void Undo()
        {
            _editor.RemoveRegionInternal(_region);
        }

        public void Redo()
        {
            _editor.AddRegionInternal(_region);
        }
    }

    /// <summary>
    /// 删除区域动作
    /// </summary>
    public class DeleteRegionAction : IEditAction
    {
        private readonly RegionData _region;
        private readonly int _index;
        private readonly RegionEditorViewModel _editor;

        public string Description => $"删除 {_region.GetShapeType()}";

        public DeleteRegionAction(RegionData region, RegionEditorViewModel editor)
        {
            _region = (RegionData)region.Clone();
            _index = editor.Regions.IndexOf(region);
            _editor = editor;
        }

        public void Undo()
        {
            if (_index >= 0 && _index <= _editor.Regions.Count)
            {
                _editor.Regions.Insert(_index, _region);
                _editor.SelectedRegion = _region;
            }
        }

        public void Redo()
        {
            _editor.RemoveRegionInternal(_region);
        }
    }

    /// <summary>
    /// 移动区域动作
    /// </summary>
    public class MoveRegionAction : IEditAction
    {
        private readonly Guid _regionId;
        private readonly double _deltaX;
        private readonly double _deltaY;
        private readonly RegionEditorViewModel _editor;

        public string Description => "移动区域";

        public MoveRegionAction(Guid regionId, double deltaX, double deltaY, RegionEditorViewModel editor)
        {
            _regionId = regionId;
            _deltaX = deltaX;
            _deltaY = deltaY;
            _editor = editor;
        }

        private void OffsetGeometry(ShapeParameters shapeDef, double dx, double dy)
        {
            if (shapeDef.Geometry == null) return;
            shapeDef.Geometry = shapeDef.Geometry switch
            {
                Circle2d c => new Circle2d(c.Center.X + dx, c.Center.Y + dy, c.Radius),
                RotatedRect2d rr => new RotatedRect2d(rr.CenterX + dx, rr.CenterY + dy, rr.Width, rr.Height, rr.Angle),
                Line2d l => new Line2d(
                    new System.Windows.Point(l.Point.X + dx, l.Point.Y + dy), l.Direction),
                System.Windows.Rect r => new System.Windows.Rect(r.X + dx, r.Y + dy, r.Width, r.Height),
                Polygon2d poly => new Polygon2d(
                    poly.Vertices.Select(v => new System.Windows.Point(v.X + dx, v.Y + dy)).ToArray(), poly.IsClosed),
                _ => shapeDef.Geometry,
            };
        }

        public void Undo()
        {
            var region = FindRegion(_regionId);
            if (region?.Parameters is ShapeParameters shapeDef)
            {
                OffsetGeometry(shapeDef, -_deltaX, -_deltaY);
                region.MarkModified();
            }
        }

        public void Redo()
        {
            var region = FindRegion(_regionId);
            if (region?.Parameters is ShapeParameters shapeDef)
            {
                OffsetGeometry(shapeDef, _deltaX, _deltaY);
                region.MarkModified();
            }
        }

        private RegionData? FindRegion(Guid id)
        {
            foreach (var region in _editor.Regions)
            {
                if (region.Id == id) return region;
            }
            return null;
        }
    }

    /// <summary>
    /// 调整区域大小动作
    /// </summary>
    public class ResizeRegionAction : IEditAction
    {
        private readonly Guid _regionId;
        private readonly ShapeParameters _originalState;
        private readonly ShapeParameters _newState;
        private readonly RegionEditorViewModel _editor;

        public string Description => "调整大小";

        public ResizeRegionAction(Guid regionId, ShapeParameters originalState, ShapeParameters newState, RegionEditorViewModel editor)
        {
            _regionId = regionId;
            _originalState = (ShapeParameters)originalState.Clone();
            _newState = (ShapeParameters)newState.Clone();
            _editor = editor;
        }

        public void Undo()
        {
            var region = FindRegion(_regionId);
            if (region?.Parameters is ShapeParameters shapeDef)
            {
                CopyState(_originalState, shapeDef);
                region.MarkModified();
            }
        }

        public void Redo()
        {
            var region = FindRegion(_regionId);
            if (region?.Parameters is ShapeParameters shapeDef)
            {
                CopyState(_newState, shapeDef);
                region.MarkModified();
            }
        }

        private static void CopyState(ShapeParameters source, ShapeParameters target)
        {
            target.Geometry = source.Geometry switch
            {
                Circle2d c => new Circle2d(c.Center.X, c.Center.Y, c.Radius),
                RotatedRect2d rr => new RotatedRect2d(rr.CenterX, rr.CenterY, rr.Width, rr.Height, rr.Angle),
                Line2d l => new Line2d(l.Point, l.Direction),
                Polygon2d poly => new Polygon2d(
                    poly.Vertices.Select(v => new System.Windows.Point(v.X, v.Y)).ToArray(), poly.IsClosed),
                _ => source.Geometry,
            };
        }

        private RegionData? FindRegion(Guid id)
        {
            foreach (var region in _editor.Regions)
            {
                if (region.Id == id) return region;
            }
            return null;
        }
    }

    /// <summary>
    /// 修改区域动作（调整大小/旋转）
    /// </summary>
    public class ModifyRegionAction : IEditAction
    {
        private readonly Guid _regionId;
        private readonly ShapeParameters _originalState;
        private ShapeParameters? _newState;
        private readonly RegionEditorViewModel _editor;

        public string Description => "修改区域";

        public ModifyRegionAction(Guid regionId, ShapeParameters originalState, RegionEditorViewModel editor)
        {
            _regionId = regionId;
            _originalState = (ShapeParameters)originalState.Clone();
            _editor = editor;
        }

        public void CaptureNewState(ShapeParameters newState)
        {
            _newState = (ShapeParameters)newState.Clone();
        }

        public void Undo()
        {
            var region = FindRegion(_regionId);
            if (region?.Parameters is ShapeParameters shapeDef && _originalState != null)
            {
                CopyState(_originalState, shapeDef);
                region.MarkModified();
            }
        }

        public void Redo()
        {
            var region = FindRegion(_regionId);
            if (region?.Parameters is ShapeParameters shapeDef && _newState != null)
            {
                CopyState(_newState, shapeDef);
                region.MarkModified();
            }
        }

        private static void CopyState(ShapeParameters source, ShapeParameters target)
        {
            target.Geometry = source.Geometry switch
            {
                Circle2d c => new Circle2d(c.Center.X, c.Center.Y, c.Radius),
                RotatedRect2d rr => new RotatedRect2d(rr.CenterX, rr.CenterY, rr.Width, rr.Height, rr.Angle),
                Line2d l => new Line2d(l.Point, l.Direction),
                Polygon2d poly => new Polygon2d(
                    poly.Vertices.Select(v => new System.Windows.Point(v.X, v.Y)).ToArray(), poly.IsClosed),
                _ => source.Geometry,
            };
        }

        private RegionData? FindRegion(Guid id)
        {
            foreach (var region in _editor.Regions)
            {
                if (region.Id == id) return region;
            }
            return null;
        }
    }

    /// <summary>
    /// 批量删除动作
    /// </summary>
    public class BatchDeleteAction : IEditAction
    {
        private readonly List<(RegionData Region, int Index)> _deletedRegions;
        private readonly RegionEditorViewModel _editor;

        public string Description => $"批量删除 {_deletedRegions.Count} 个区域";

        public BatchDeleteAction(IEnumerable<(RegionData Region, int Index)> regions, RegionEditorViewModel editor)
        {
            _editor = editor;
            _deletedRegions = new List<(RegionData, int)>();
            foreach (var (region, index) in regions)
            {
                _deletedRegions.Add(((RegionData)region.Clone(), index));
            }
            // 按索引降序排列，以便撤销时按正确顺序恢复
            _deletedRegions.Sort((a, b) => b.Index.CompareTo(a.Index));
        }

        public void Undo()
        {
            foreach (var (region, index) in _deletedRegions)
            {
                if (index >= 0 && index <= _editor.Regions.Count)
                {
                    _editor.Regions.Insert(index, region);
                }
            }
        }

        public void Redo()
        {
            foreach (var (region, _) in _deletedRegions)
            {
                _editor.RemoveRegionInternal(region);
            }
        }
    }

    /// <summary>
    /// 清除所有动作
    /// </summary>
    public class ClearAllAction : IEditAction
    {
        private readonly List<RegionData> _regions;
        private readonly RegionEditorViewModel _editor;

        public string Description => "清除所有区域";

        public ClearAllAction(IEnumerable<RegionData> regions, RegionEditorViewModel editor)
        {
            _editor = editor;
            _regions = new List<RegionData>();
            foreach (var region in regions)
            {
                _regions.Add((RegionData)region.Clone());
            }
        }

        public void Undo()
        {
            foreach (var region in _regions)
            {
                _editor.AddRegionInternal(region);
            }
        }

        public void Redo()
        {
            _editor.SetRegionsInternal(new List<RegionData>());
        }
    }

    #endregion
}

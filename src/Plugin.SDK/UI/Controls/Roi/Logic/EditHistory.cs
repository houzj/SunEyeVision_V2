using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.ViewModels;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Logic
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
    /// 创建几何输入动作
    /// </summary>
    public class CreateRoiAction : IEditAction
    {
        private readonly Roi _roi;
        private readonly RoiEditorViewModel _editor;

        public string Description => $"创建 {_roi.DeclaredShapeType}";

        public CreateRoiAction(Roi roi, RoiEditorViewModel editor)
        {
            _roi = roi;
            _editor = editor;
        }

        public void Undo()
        {
            _editor.RemoveRoiInternal(_roi);
        }

        public void Redo()
        {
            _editor.AddRoiInternal(_roi);
        }
    }

    /// <summary>
    /// 删除几何输入动作
    /// </summary>
    public class DeleteRoiAction : IEditAction
    {
        private readonly Roi _roi;
        private readonly int _index;
        private readonly RoiEditorViewModel _editor;

        public string Description => $"删除 {_roi.DeclaredShapeType}";

        public DeleteRoiAction(Roi roi, RoiEditorViewModel editor)
        {
            _roi = (Roi)roi.Clone();
            _index = editor.Rois.IndexOf(roi);
            _editor = editor;
        }

        public void Undo()
        {
            if (_index >= 0 && _index <= _editor.Rois.Count)
            {
                _editor.Rois.Insert(_index, _roi);
                _editor.SelectedRoi = _roi;
            }
        }

        public void Redo()
        {
            _editor.RemoveRoiInternal(_roi);
        }
    }

    /// <summary>
    /// 移动几何输入动作
    /// </summary>
    public class MoveRoiAction : IEditAction
    {
        private readonly Guid _roiId;
        private readonly double _deltaX;
        private readonly double _deltaY;
        private readonly RoiEditorViewModel _editor;

        public string Description => "移动几何输入";

        public MoveRoiAction(Guid roiId, double deltaX, double deltaY, RoiEditorViewModel editor)
        {
            _roiId = roiId;
            _deltaX = deltaX;
            _deltaY = deltaY;
            _editor = editor;
        }

        private void OffsetGeometry(RoiShape shapeDef, double dx, double dy)
        {
            if (shapeDef.Geometry == null) return;
            shapeDef.Geometry = shapeDef.Geometry switch
            {
                Circle2d c => new Circle2d(c.Center.X + dx, c.Center.Y + dy, c.Radius),
                RotatedRect2d rr => new RotatedRect2d(rr.CenterX + dx, rr.CenterY + dy, rr.Width, rr.Height, rr.Angle),
                Line2d l => new Line2d(
                    new System.Windows.Point(l.Point.X + dx, l.Point.Y + dy), l.Direction),
                // P0-2 封口：几何值恒为契约类型（矩形即 RotatedRect2d），不再有 WPF Rect 分支。
                Polygon2d poly => new Polygon2d(
                    poly.Vertices.Select(v => new System.Windows.Point(v.X + dx, v.Y + dy)).ToArray(), poly.IsClosed),
                _ => shapeDef.Geometry,
            };
        }

        public void Undo()
        {
            var roi = FindRoi(_roiId);
            if (roi?.LocalGeometry is RoiShape shapeDef)
            {
                OffsetGeometry(shapeDef, -_deltaX, -_deltaY);
            }
        }

        public void Redo()
        {
            var roi = FindRoi(_roiId);
            if (roi?.LocalGeometry is RoiShape shapeDef)
            {
                OffsetGeometry(shapeDef, _deltaX, _deltaY);
            }
        }

        private Roi? FindRoi(Guid id)
        {
            foreach (var roi in _editor.Rois)
            {
                if (roi.Id == id) return roi;
            }
            return null;
        }
    }

    /// <summary>
    /// 调整几何输入大小动作
    /// </summary>
    public class ResizeRoiAction : IEditAction
    {
        private readonly Guid _roiId;
        private readonly RoiShape _originalState;
        private readonly RoiShape _newState;
        private readonly RoiEditorViewModel _editor;

        public string Description => "调整大小";

        public ResizeRoiAction(Guid roiId, RoiShape originalState, RoiShape newState, RoiEditorViewModel editor)
        {
            _roiId = roiId;
            _originalState = originalState.Clone();
            _newState = newState.Clone();
            _editor = editor;
        }

        public void Undo()
        {
            var roi = FindRoi(_roiId);
            if (roi?.LocalGeometry is RoiShape shapeDef)
            {
                CopyState(_originalState, shapeDef);
            }
        }

        public void Redo()
        {
            var roi = FindRoi(_roiId);
            if (roi?.LocalGeometry is RoiShape shapeDef)
            {
                CopyState(_newState, shapeDef);
            }
        }

        private static void CopyState(RoiShape source, RoiShape target)
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

        private Roi? FindRoi(Guid id)
        {
            foreach (var roi in _editor.Rois)
            {
                if (roi.Id == id) return roi;
            }
            return null;
        }
    }

    /// <summary>
    /// 修改几何输入动作（调整大小/旋转）
    /// </summary>
    public class ModifyRoiAction : IEditAction
    {
        private readonly Guid _roiId;
        private readonly RoiShape _originalState;
        private RoiShape? _newState;
        private readonly RoiEditorViewModel _editor;

        public string Description => "修改几何输入";

        public ModifyRoiAction(Guid roiId, RoiShape originalState, RoiEditorViewModel editor)
        {
            _roiId = roiId;
            _originalState = originalState.Clone();
            _editor = editor;
        }

        public void CaptureNewState(RoiShape newState)
        {
            _newState = newState.Clone();
        }

        public void Undo()
        {
            var roi = FindRoi(_roiId);
            if (roi?.LocalGeometry is RoiShape shapeDef && _originalState != null)
            {
                CopyState(_originalState, shapeDef);
            }
        }

        public void Redo()
        {
            var roi = FindRoi(_roiId);
            if (roi?.LocalGeometry is RoiShape shapeDef && _newState != null)
            {
                CopyState(_newState, shapeDef);
            }
        }

        private static void CopyState(RoiShape source, RoiShape target)
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

        private Roi? FindRoi(Guid id)
        {
            foreach (var roi in _editor.Rois)
            {
                if (roi.Id == id) return roi;
            }
            return null;
        }
    }

    /// <summary>
    /// 批量删除几何输入动作
    /// </summary>
    public class BatchDeleteRoiAction : IEditAction
    {
        private readonly List<(Roi Roi, int Index)> _deletedRois;
        private readonly RoiEditorViewModel _editor;

        public string Description => $"批量删除 {_deletedRois.Count} 个几何输入";

        public BatchDeleteRoiAction(IEnumerable<(Roi Roi, int Index)> rois, RoiEditorViewModel editor)
        {
            _editor = editor;
            _deletedRois = new List<(Roi, int)>();
            foreach (var (roi, index) in rois)
            {
                _deletedRois.Add(((Roi)roi.Clone(), index));
            }
            // 按索引降序排列，以便撤销时按正确顺序恢复
            _deletedRois.Sort((a, b) => b.Index.CompareTo(a.Index));
        }

        public void Undo()
        {
            foreach (var (roi, index) in _deletedRois)
            {
                if (index >= 0 && index <= _editor.Rois.Count)
                {
                    _editor.Rois.Insert(index, roi);
                }
            }
        }

        public void Redo()
        {
            foreach (var (roi, _) in _deletedRois)
            {
                _editor.RemoveRoiInternal(roi);
            }
        }
    }

    /// <summary>
    /// 清除所有动作
    /// </summary>
    public class ClearAllRoisAction : IEditAction
    {
        private readonly List<Roi> _rois;
        private readonly RoiEditorViewModel _editor;

        public string Description => "清除所有几何输入";

        public ClearAllRoisAction(IEnumerable<Roi> rois, RoiEditorViewModel editor)
        {
            _editor = editor;
            _rois = new List<Roi>();
            foreach (var roi in rois)
            {
                _rois.Add((Roi)roi.Clone());
            }
        }

        public void Undo()
        {
            foreach (var roi in _rois)
            {
                _editor.AddRoiInternal(roi);
            }
        }

        public void Redo()
        {
            _editor.SetRoisInternal(new List<Roi>());
        }
    }

    #endregion
}

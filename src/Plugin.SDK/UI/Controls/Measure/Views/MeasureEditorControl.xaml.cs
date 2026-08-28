using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Interactions;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Rendering;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views
{
    /// <summary>
    /// 测量引导几何交互宿主 - 承载引导几何的绘制/选中/手柄编辑/移动/删除交互
    /// </summary>
    /// <remarks>
    /// 对齐 RoiEditorControl 的完整交互状态机模式（SDK 通用控件，工具层只组装）：
    /// - 绘制模式：在图像上拖动创建引导几何（Line/Circle/Arc）
    /// - 选择模式：绘制完成自动进入，显示编辑手柄
    /// - 手柄编辑：端点/半径/角度（复用 HandleRenderer 静态手柄系统）
    /// - 命中优先级：①手柄 ②引导主体 ③卡尺测量带，命中即编辑/移动
    /// - Delete 删除引导几何
    /// - 绘制过程中只显示引导几何预览，松开完成后再显示测量带
    /// - Fail-Forward：未绑定 ImageControl 时安全跳过，不崩溃
    /// 通过 GuideGeometryChanged 事件通知宿主同步工具参数。
    /// </remarks>
    public partial class MeasureEditorControl : UserControl
    {
        /// <summary>交互状态模型</summary>
        private readonly MeasureInteractionModel _model = new();

        /// <summary>测量带渲染器（绑定图像控件后创建）</summary>
        private MeasureBandOverlay? _overlay;

        /// <summary>当前主界面图像控件</summary>
        private ImageControl? _imageControl;

        /// <summary>叠加画布（手柄渲染目标）</summary>
        private Canvas? _overlayCanvas;

        /// <summary>卡尺宽度（法线方向全宽）</summary>
        private double _bandWidth = 40.0;

        /// <summary>卡尺长度（切线方向全长）</summary>
        private double _bandLength = 10.0;

        /// <summary>测量带数量</summary>
        private int _measureCount = 10;

        // 交互状态（对齐 RoiEditorControl 三态）
        private bool _isDragging;
        private bool _isDraggingHandle;
        private HandleType _activeHandleType = HandleType.None;
        private Point _lastMouse;

        /// <summary>当前编辑手柄数组（选中态渲染）</summary>
        private EditHandle[]? _currentHandles;

        /// <summary>视口缩放（用于手柄尺寸）</summary>
        private double _zoom = 1.0;

        /// <summary>当前几何交互策略（按几何类型路由）</summary>
        private IGuideGeometryInteraction _interaction = GuideInteractionFactory.Get(MeasureGeometryType.Line);

        /// <summary>
        /// 引导几何变更事件
        /// </summary>
        /// <remarks>参数为当前几何类型 + 几何对象（Line2d/Circle2d/Arc2d，均 box 化）</remarks>
        public event EventHandler<GuideGeometryChangedEventArgs>? GuideGeometryChanged;

        /// <summary>
        /// 创建测量引导几何交互宿主
        /// </summary>
        public MeasureEditorControl()
        {
            InitializeComponent();
        }

        #region 公开 API

        /// <summary>
        /// 绑定主界面图像控件（null 表示解除，Fail-Forward 安全）
        /// </summary>
        public void SetMainImageControl(ImageControl? imageControl)
        {
            UnsubscribeEvents();
            _imageControl = imageControl;

            if (imageControl != null)
            {
                _overlayCanvas = imageControl.InteractionOverlayCanvasControl;
                _overlay = new MeasureBandOverlay(_overlayCanvas);
                SubscribeEvents();
            }
            else
            {
                _overlayCanvas = null;
                _overlay = null;
            }

            Render();
        }

        /// <summary>
        /// 设置卡尺渲染参数（法线全宽 / 切线全长 / 数量）
        /// </summary>
        public void SetCaliberParams(double width, double length, int count)
        {
            _bandWidth = width;
            _bandLength = length;
            _measureCount = Math.Max(1, count);
            Render();
        }

        /// <summary>
        /// 设置几何类型并进入绘制模式
        /// </summary>
        public void StartDraw(MeasureGeometryType geometryType)
        {
            _model.Mode = MeasureInteractionMode.Draw;
            _model.IsDrawing = false;
            _model.IsSelected = false;
            _geometryType = geometryType;
            _interaction = GuideInteractionFactory.Get(geometryType);
        }

        /// <summary>
        /// 直接设置引导直线（用于从参数回填渲染，不触发绘制交互）
        /// </summary>
        public void SetGuide(Line2d line)
        {
            _model.SetLine(line);
            Render();
        }

        /// <summary>
        /// 直接设置引导圆（用于从参数回填渲染，不触发绘制交互）
        /// </summary>
        public void SetGuide(Circle2d circle)
        {
            _model.SetCircle(circle);
            Render();
        }

        /// <summary>
        /// 直接设置引导圆弧（用于从参数回填渲染，不触发绘制交互）
        /// </summary>
        public void SetGuide(Arc2d arc)
        {
            _model.SetArc(arc);
            Render();
        }

        /// <summary>
        /// 反转检测方向（当前支持 Line）
        /// </summary>
        public void ReverseDirection()
        {
            if (_model.EditingLine.HasValue)
            {
                var s = _model.LineStartPoint;
                var e = _model.LineEndPoint;
                _model.SetLine(Line2d.FromTwoPoints(e, s), e, s);
                Render();
                NotifyGuideChanged();
            }
        }

        /// <summary>
        /// 清空引导几何
        /// </summary>
        public void ClearGuide()
        {
            _model.Clear();
            _currentHandles = null;
            Render();
        }

        /// <summary>
        /// 删除当前选中的引导几何（Delete 键）
        /// </summary>
        public void HandleDelete()
        {
            if (!_model.IsSelected) return;
            _model.Clear();
            _currentHandles = null;
            Render();
            NotifyGuideChanged();
        }

        /// <summary>
        /// 设置视口缩放（用于手柄尺寸自适应）
        /// </summary>
        public void SetZoom(double zoom)
        {
            _zoom = zoom;
        }

        /// <summary>
        /// 当前是否有有效引导几何
        /// </summary>
        public bool HasGuide => _model.HasGuide;

        /// <summary>
        /// 当前是否正在交互（供宿主路由判断）
        /// </summary>
        public bool IsAnyInteractionInProgress => _model.IsDrawing || _isDragging || _isDraggingHandle;

        #endregion

        #region 鼠标事件

        private void OnGuideMouseDown(object sender, ImageMouseEventArgs e)
        {
            var pos = e.ImagePosition;

            // 绘制模式：创建引导几何
            if (_model.Mode == MeasureInteractionMode.Draw)
            {
                _model.DrawStart = pos;
                _model.IsDrawing = true;
                e.Handled = true;
                return;
            }

            // 选择/编辑模式：命中优先级 ①手柄 ②引导主体 ③卡尺
            if (_model.HasGuide)
            {
                // ① 手柄命中（优先级最高：命中半径带保底下限，确保与卡尺/引导线重叠时优先命中手柄）
                if (_currentHandles != null)
                {
                    var hitType = HitTestHandles(pos);
                    if (hitType != HandleType.None)
                    {
                        BeginHandleDrag(hitType, pos);
                        e.Handled = true;
                        return;
                    }
                }

                // ② 引导主体命中 → 移动
                if (HitTestGuide(pos, _guideHitTolerance))
                {
                    _model.IsSelected = true;
                    _isDragging = true;
                    _lastMouse = pos;
                    e.Handled = true;
                    return;
                }

                // ③ 卡尺测量带命中 → 移动
                if (HitTestBand(pos))
                {
                    _model.IsSelected = true;
                    _isDragging = true;
                    _lastMouse = pos;
                    e.Handled = true;
                    return;
                }
            }

            // 未命中任何交互对象 → 取消选中
            _model.Deselect();
            _currentHandles = null;
            Render();
        }

        private void OnGuideMouseMove(object sender, ImageMouseEventArgs e)
        {
            if (_overlay == null) return;

            var pos = e.ImagePosition;

            // 拖动手柄 → 编辑引导几何
            if (_isDraggingHandle)
            {
                HandleShapeEdit(pos);
                e.Handled = true;
                return;
            }

            // 拖动移动
            if (_isDragging)
            {
                var delta = pos - _lastMouse;
                if (delta.Length > 1e-9)
                {
                    _model.Translate(delta);
                    _lastMouse = pos;
                    Render();
                    NotifyGuideChanged();
                }
                e.Handled = true;
                return;
            }

            // 绘制预览（只画引导几何，不画测量带）
            if (_model.Mode == MeasureInteractionMode.Draw && _model.IsDrawing)
            {
                UpdateDrawPreview(pos);
                e.Handled = true;
                return;
            }

            // 悬停光标反馈：手柄=手（编辑），引导线/卡尺=十字（移动），空白=箭头
            UpdateHoverCursor(pos);
        }

        /// <summary>
        /// 悬停光标反馈：按命中对象切换光标（手柄=编辑/手，引导线/卡尺=移动/十字，空白=箭头）
        /// </summary>
        private void UpdateHoverCursor(Point pos)
        {
            if (_overlayCanvas == null) return;

            // ① 手柄 → 手（编辑，优先级最高）
            if (_currentHandles != null && HitTestHandles(pos) != HandleType.None)
            {
                Mouse.OverrideCursor = Cursors.Hand;
                return;
            }

            // ② 引导主体 或 ③ 卡尺测量带 → 十字（移动）
            if (_model.HasGuide &&
                (HitTestGuide(pos, _guideHitTolerance) || HitTestBand(pos)))
            {
                Mouse.OverrideCursor = Cursors.SizeAll;
                return;
            }

            // 空白 → 箭头
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void OnGuideMouseUp(object sender, ImageMouseEventArgs e)
        {
            if (_model.IsDrawing)
            {
                _model.IsDrawing = false;
                CompleteDraw(e.ImagePosition);
                e.Handled = true;
                return;
            }

            if (_isDraggingHandle || _isDragging)
            {
                _isDraggingHandle = false;
                _isDragging = false;
                _activeHandleType = HandleType.None;
                e.Handled = true;
            }
        }

        #endregion

        #region 命中测试

        /// <summary>引导主体命中容差（像素）</summary>
        private const double _guideHitTolerance = 12.0;

        /// <summary>卡尺命中容差（像素）</summary>
        private const double _bandHitTolerance = 6.0;

        /// <summary>
        /// 手柄命中测试（优先级最高）：命中半径 = max(自适应手柄半径, 保底下限)
        /// </summary>
        /// <remarks>
        /// 手柄命中半径必须 ≥ 引导线/卡尺的命中范围，否则与卡尺带/引导线重叠时
        /// 手柄会被卡尺/引导线"抢走"，导致无法编辑。保底下限保证高缩放（手柄视觉变小）
        /// 时仍能稳定命中。
        /// </remarks>
        private const double _handleHitToleranceFloor = 24.0;

        /// <summary>手柄命中测试（命中半径带保底下限，确保优先级高于卡尺与引导线）</summary>
        private HandleType HitTestHandles(Point point)
        {
            if (_currentHandles == null) return HandleType.None;

            // 保底命中半径：比引导线容差(12) 与卡尺半带(20) 更大，保证重叠时手柄优先
            double floor = _handleHitToleranceFloor;
            EditHandle? closest = null;
            double closestDist = double.MaxValue;
            foreach (var handle in _currentHandles)
            {
                double dx = point.X - handle.Position.X;
                double dy = point.Y - handle.Position.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                // 命中半径 = max(自适应手柄半径, 保底下限)
                double hitRadius = Math.Max(handle.HandleSize, floor);
                if (dist <= hitRadius && dist < closestDist)
                {
                    closestDist = dist;
                    closest = handle;
                }
            }
            return closest?.Type ?? HandleType.None;
        }

        /// <summary>
        /// 引导主体命中测试（委托给当前几何交互策略：线附近/圆环/弧线，容差）
        /// </summary>
        private bool HitTestGuide(Point point, double tolerance)
        {
            return _interaction.HitTestGuide(_model, point, tolerance);
        }

        /// <summary>
        /// 卡尺测量带命中测试（兜底：命中任意测量带矩形 → 移动）
        /// </summary>
        /// <remarks>
        /// 根据引导几何 + 卡尺参数计算各测量带中心，检测点是否落在带内。
        /// 实际测量带位置与 MeasureBandOverlay 渲染一致。
        /// </remarks>
        private bool HitTestBand(Point point)
        {
            if (_measureCount <= 0) return false;

            var halfBand = _bandLength / 2;
            var halfWidth = _bandWidth / 2;

            if (_model.EditingLine.HasValue)
            {
                var seg = _model.LineEndPoint - _model.LineStartPoint;
                var totalLen = seg.Length;
                if (totalLen < 1e-6) return false;
                var dir = seg / totalLen;
                var normal = new Vector(-dir.Y, dir.X);
                double step = totalLen / _measureCount;
                for (int i = 0; i < _measureCount; i++)
                {
                    var center = _model.LineStartPoint + dir * ((i + 0.5) * step);
                    if (GeometryToolkit.PointInQuad(point, center - dir * halfBand - normal * halfWidth,
                        center + dir * halfBand - normal * halfWidth,
                        center + dir * halfBand + normal * halfWidth,
                        center - dir * halfBand + normal * halfWidth))
                        return true;
                }
                return false;
            }

            if (_model.EditingCircle.HasValue)
            {
                var c = _model.EditingCircle.Value;
                double stepAngle = Math.PI * 2 / _measureCount;
                for (int i = 0; i < _measureCount; i++)
                {
                    double angle = i * stepAngle;
                    var dir = new Vector(Math.Cos(angle), Math.Sin(angle));
                    var normal = new Vector(-dir.Y, dir.X);
                    var center = c.Center + dir * c.Radius;
                    if (GeometryToolkit.PointInQuad(point, center - dir * halfWidth - normal * halfBand,
                        center + dir * halfWidth - normal * halfBand,
                        center + dir * halfWidth + normal * halfBand,
                        center - dir * halfWidth + normal * halfBand))
                        return true;
                }
                return false;
            }

            if (_model.EditingArc.HasValue && !_model.EditingArc.Value.IsEmpty)
            {
                var a = _model.EditingArc.Value;
                double span = a.AngularSpan;
                double stepAngle = span / _measureCount;
                for (int i = 0; i < _measureCount; i++)
                {
                    double angle = a.StartAngle + (i + 0.5) * stepAngle;
                    var dir = new Vector(Math.Cos(angle), Math.Sin(angle));
                    var normal = new Vector(-dir.Y, dir.X);
                    var center = a.Center + dir * a.Radius;
                    if (GeometryToolkit.PointInQuad(point, center - dir * halfWidth - normal * halfBand,
                        center + dir * halfWidth - normal * halfBand,
                        center + dir * halfWidth + normal * halfBand,
                        center - dir * halfWidth + normal * halfBand))
                        return true;
                }
                return false;
            }

            return false;
        }

        #endregion

        #region 手柄编辑

        /// <summary>
        /// 开始手柄拖拽（记录初始状态）
        /// </summary>
        private void BeginHandleDrag(HandleType handleType, Point position)
        {
            _isDraggingHandle = true;
            _activeHandleType = handleType;
            _lastMouse = position;
            _model.IsSelected = true;
        }

        /// <summary>
        /// 手柄拖拽编辑（委托给当前几何交互策略）
        /// </summary>
        private void HandleShapeEdit(Point current)
        {
            if (_interaction.EditHandle(_model, _activeHandleType, current))
            {
                Render();
                NotifyGuideChanged();
            }
        }

        #endregion

        #region 绘制逻辑

        /// <summary>
        /// 实时更新绘制预览（委托给当前几何交互策略，只画引导几何不画测量带）
        /// </summary>
        private void UpdateDrawPreview(Point current)
        {
            _interaction.UpdatePreview(_model, _model.DrawStart, current);
            Render();
        }

        /// <summary>
        /// 完成绘制（委托给当前几何交互策略：防退化 + 自动进选择模式）
        /// </summary>
        private void CompleteDraw(Point current)
        {
            bool created = _interaction.Complete(_model, _model.DrawStart, current);
            if (created)
            {
                // 绘制完成自动进选择模式：选中 + 显示手柄
                _model.IsSelected = true;
                _model.Mode = MeasureInteractionMode.Select;
                UpdateHandles();
                Render();
                NotifyGuideChanged();
            }
        }

        #endregion

        #region 渲染

        /// <summary>
        /// 渲染当前引导几何 + 测量带（绘制中只画引导几何）
        /// </summary>
        private void Render()
        {
            if (_overlay == null) return;
            _overlay.Clear();

            // 绘制中只画引导几何预览，不画测量带
            bool drawBands = !_model.IsDrawing;

            if (_model.EditingLine.HasValue)
            {
                _overlay.RenderLine(_model.LineStartPoint, _model.LineEndPoint, _bandWidth, _bandLength, _measureCount, drawBands);
            }
            else if (_model.EditingCircle.HasValue)
            {
                _overlay.RenderCircle(_model.EditingCircle.Value, _bandWidth, _bandLength, _measureCount, drawBands);
            }
            else if (_model.EditingArc.HasValue && !_model.EditingArc.Value.IsEmpty)
            {
                _overlay.RenderArc(_model.EditingArc.Value, _bandWidth, _bandLength, _measureCount, drawBands);
            }

            // 选中态绘制手柄（先按当前几何重建位置，确保手柄随编辑随动）
            if (_model.IsSelected && _model.HasGuide)
            {
                UpdateHandles();
                DrawHandles();
            }
            else
            {
                _currentHandles = null;
            }
        }

        /// <summary>
        /// 更新手柄数组（委托给当前几何交互策略）
        /// </summary>
        private void UpdateHandles()
        {
            _currentHandles = _model.HasGuide
                ? _interaction.CreateHandles(_model, _zoom)
                : null;
        }

        /// <summary>
        /// 绘制手柄到叠加画布
        /// </summary>
        private void DrawHandles()
        {
            if (_overlayCanvas == null || _currentHandles == null) return;
            var shapeType = _geometryType switch
            {
                MeasureGeometryType.Line => ShapeType.Line,
                _ => ShapeType.Circle,
            };
            HandleRenderer.DrawHandles(_overlayCanvas, _currentHandles, shapeType, _zoom);
        }

        /// <summary>
        /// 通知宿主引导几何已变更
        /// </summary>
        private void NotifyGuideChanged()
        {
            GuideGeometryChanged?.Invoke(this, new GuideGeometryChangedEventArgs(_model, _geometryType));
        }

        #endregion

        #region 事件订阅

        private void SubscribeEvents()
        {
            if (_imageControl == null) return;
            _imageControl.CanvasMouseLeftButtonDown += OnGuideMouseDown;
            _imageControl.CanvasMouseLeftButtonUp += OnGuideMouseUp;
            _imageControl.ImageMouseMove += OnGuideMouseMove;
        }

        private void UnsubscribeEvents()
        {
            if (_imageControl == null) return;
            _imageControl.CanvasMouseLeftButtonDown -= OnGuideMouseDown;
            _imageControl.CanvasMouseLeftButtonUp -= OnGuideMouseUp;
            _imageControl.ImageMouseMove -= OnGuideMouseMove;
        }

        #endregion

        #region 字段

        private MeasureGeometryType _geometryType = MeasureGeometryType.Line;

        #endregion
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views
{
    /// <summary>
    /// 测量引导几何交互宿主 - 承载引导几何的绘制/编辑/方向交互
    /// </summary>
    /// <remarks>
    /// 对齐 RoiEditorControl 的"宿主状态机"模式（SDK 通用控件，工具层只组装）：
    /// - 绘制模式：在图像上拖动创建引导几何（Line/Circle/Arc）
    /// - 编辑模式：拖动整体平移引导几何
    /// - 方向反转：翻转检测方向（当前支持 Line）
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

        /// <summary>卡尺宽度（法线方向全宽）</summary>
        private double _bandWidth = 40.0;

        /// <summary>卡尺长度（切线方向全长）</summary>
        private double _bandLength = 10.0;

        /// <summary>测量带数量</summary>
        private int _measureCount = 10;

        /// <summary>检测方向是否翻转（Line 用）</summary>
        private bool _directionFlipped;

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

        /// <summary>
        /// 绑定主界面图像控件（null 表示解除，Fail-Forward 安全）
        /// </summary>
        public void SetMainImageControl(ImageControl? imageControl)
        {
            UnsubscribeEvents();
            _imageControl = imageControl;

            if (imageControl != null)
            {
                _overlay = new MeasureBandOverlay(imageControl.InteractionOverlayCanvasControl);
                SubscribeEvents();
            }
            else
            {
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
            _geometryType = geometryType;
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
        /// 进入编辑模式（平移引导几何）
        /// </summary>
        public void StartEdit()
        {
            _model.Mode = MeasureInteractionMode.Edit;
            _model.IsDrawing = false;
        }

        /// <summary>
        /// 反转检测方向（当前支持 Line）
        /// </summary>
        public void ReverseDirection()
        {
            if (_model.EditingLine.HasValue)
            {
                var line = _model.EditingLine.Value;
                var flipped = new Line2d(line.Point + line.Direction, -line.Direction);
                _model.SetLine(flipped);
                _directionFlipped = !_directionFlipped;
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
            Render();
        }

        /// <summary>
        /// 当前是否有有效引导几何
        /// </summary>
        public bool HasGuide => _model.HasGuide;

        #region 鼠标事件

        private void OnGuideMouseDown(object sender, ImageMouseEventArgs e)
        {
            if (_model.Mode == MeasureInteractionMode.Draw)
            {
                var pos = e.ImagePosition;
                _model.DrawStart = pos;
                _model.IsDrawing = true;
                e.Handled = true;
            }
            else if (_model.Mode == MeasureInteractionMode.Edit)
            {
                _lastMouse = e.ImagePosition;
                _isDragging = _model.HasGuide;
                e.Handled = true;
            }
        }

        private void OnGuideMouseMove(object sender, ImageMouseEventArgs e)
        {
            if (_overlay == null) return;

            var pos = e.ImagePosition;

            if (_model.Mode == MeasureInteractionMode.Draw && _model.IsDrawing)
            {
                UpdateDrawPreview(pos);
                e.Handled = true;
            }
            else if (_model.Mode == MeasureInteractionMode.Edit && _isDragging)
            {
                var delta = pos - _lastMouse;
                if (delta.Length > 1e-9)
                {
                    _model.Translate(delta);
                    _lastMouse = pos;
                    Render();
                    NotifyGuideChanged();
                    e.Handled = true;
                }
            }
        }

        private void OnGuideMouseUp(object sender, ImageMouseEventArgs e)
        {
            if (_model.Mode == MeasureInteractionMode.Draw && _model.IsDrawing)
            {
                _model.IsDrawing = false;
                CompleteDraw(e.ImagePosition);
                e.Handled = true;
            }
            else if (_model.Mode == MeasureInteractionMode.Edit)
            {
                _isDragging = false;
                e.Handled = true;
            }
        }

        #endregion

        #region 绘制逻辑

        /// <summary>
        /// 实时更新绘制预览
        /// </summary>
        private void UpdateDrawPreview(Point current)
        {
            var start = _model.DrawStart;
            switch (_geometryType)
            {
                case MeasureGeometryType.Line:
                    if ((current - start).Length > 1e-6)
                    {
                        _model.SetLine(Line2d.FromTwoPoints(start, current));
                    }
                    break;

                case MeasureGeometryType.Circle:
                {
                    var radius = (current - start).Length;
                    if (radius > 1e-6)
                    {
                        _model.SetCircle(new Circle2d(start, radius));
                    }
                    break;
                }

                case MeasureGeometryType.Arc:
                {
                    var radius = (current - start).Length;
                    if (radius > 1e-6)
                    {
                        double endAngle = Math.Atan2(current.Y - start.Y, current.X - start.X);
                        _model.SetArc(new Arc2d(start, radius, 0, endAngle));
                    }
                    break;
                }
            }
            Render();
        }

        /// <summary>
        /// 完成绘制（防退化）
        /// </summary>
        private void CompleteDraw(Point current)
        {
            var start = _model.DrawStart;

            switch (_geometryType)
            {
                case MeasureGeometryType.Line:
                    if ((current - start).Length > 1e-3)
                    {
                        _model.SetLine(Line2d.FromTwoPoints(start, current));
                        Render();
                        NotifyGuideChanged();
                    }
                    break;

                case MeasureGeometryType.Circle:
                {
                    var radius = (current - start).Length;
                    if (radius > 1e-3)
                    {
                        _model.SetCircle(new Circle2d(start, radius));
                        Render();
                        NotifyGuideChanged();
                    }
                    break;
                }

                case MeasureGeometryType.Arc:
                {
                    var radius = (current - start).Length;
                    if (radius > 1e-3)
                    {
                        double endAngle = Math.Atan2(current.Y - start.Y, current.X - start.X);
                        _model.SetArc(new Arc2d(start, radius, 0, endAngle));
                        Render();
                        NotifyGuideChanged();
                    }
                    break;
                }
            }
        }

        #endregion

        #region 渲染与通知

        /// <summary>
        /// 渲染当前引导几何 + 测量带
        /// </summary>
        private void Render()
        {
            if (_overlay == null) return;
            _overlay.Clear();

            if (_model.EditingLine.HasValue)
            {
                _overlay.RenderLine(_model.EditingLine.Value, _bandWidth, _bandLength, _measureCount);
            }
            else if (_model.EditingCircle.HasValue)
            {
                _overlay.RenderCircle(_model.EditingCircle.Value, _bandWidth, _bandLength, _measureCount);
            }
            else if (_model.EditingArc.HasValue && !_model.EditingArc.Value.IsEmpty)
            {
                _overlay.RenderArc(_model.EditingArc.Value, _bandWidth, _bandLength, _measureCount);
            }
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
        private Point _lastMouse;
        private bool _isDragging;

        #endregion
    }
}

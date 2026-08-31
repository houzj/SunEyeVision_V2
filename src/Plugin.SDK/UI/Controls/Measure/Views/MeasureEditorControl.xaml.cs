using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
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

        /// <summary>位置修正矩阵（正向：参考坐标→当前图像坐标；null=不修正）</summary>
        private AffineMatrix? _positionCorrectionMatrix;

        /// <summary>位置修正逆矩阵（当前图像坐标→参考坐标）</summary>
        private AffineMatrix? _inverseMatrix;

        /// <summary>
        /// 设置位置修正矩阵。显示正向变换，编辑逆变换，存储不变（始终存参考坐标系）。
        /// 对齐 RoiEditorControl 的同名机制。
        /// </summary>
        public void SetPositionCorrectionMatrix(AffineMatrix? matrix)
        {
            _positionCorrectionMatrix = matrix;
            _inverseMatrix = matrix?.Inverse();
            RenderForManager();
        }

        /// <summary>
        /// 正向变换参考系坐标到当前图像坐标（矩阵为空时原样返回）
        /// </summary>
        private Point ToDisplay(Point reference) =>
            _positionCorrectionMatrix != null ? _positionCorrectionMatrix.TransformWpfPoint(reference) : reference;

        /// <summary>
        /// 逆变换当前图像坐标到参考系坐标（矩阵为空时原样返回）
        /// </summary>
        private Point ToReference(Point display) =>
            _inverseMatrix != null ? _inverseMatrix.TransformWpfPoint(display) : display;

        /// <summary>当前几何交互策略（按几何类型路由）</summary>
        private IGuideGeometryInteraction _interaction = GuideInteractionFactory.Get(MeasureGeometryType.Line);

        /// <summary>
        /// 引导几何变更事件
        /// </summary>
        /// <remarks>参数为当前几何类型 + 几何对象（Line2d/Circle2d/Arc2d，均 box 化）</remarks>
        public event EventHandler<GuideGeometryChangedEventArgs>? GuideGeometryChanged;

        /// <summary>
        /// 卡尺参数变更事件（面板/主卡尺手柄编辑后触发，宿主据此回写工具参数）
        /// </summary>
        public event EventHandler<CaliberParamsChangedEventArgs>? CaliberParamsChanged;

        /// <summary>
        /// 创建测量引导几何交互宿主
        /// </summary>
        public MeasureEditorControl()
        {
            InitializeComponent();
            InitializeCaliberTools();
        }

        #region 卡尺工具项

        /// <summary>卡尺工具项集合（驱动内置卡尺类型工具栏，对齐 RoiEditorControl 内置形状工具栏）</summary>
        public System.Collections.ObjectModel.ObservableCollection<MeasureToolItem> CaliberTools { get; } = new();

        /// <summary>
        /// 初始化卡尺工具项（Line/Circle/Arc，图标来自 MeasureToolIconFactory）
        /// </summary>
        private void InitializeCaliberTools()
        {
            CaliberTools.Add(new MeasureToolItem(MeasureGeometryType.Line, "直线卡尺", MeasureToolIconFactory.LineIcon, Key.L));
            CaliberTools.Add(new MeasureToolItem(MeasureGeometryType.Circle, "圆卡尺", MeasureToolIconFactory.CircleIcon, Key.C));
            CaliberTools.Add(new MeasureToolItem(MeasureGeometryType.Arc, "圆弧卡尺", MeasureToolIconFactory.ArcIcon, Key.A));
            MeasureToolBarControl.MeasureTools = CaliberTools;
        }

        /// <summary>
        /// 卡尺类型选择 → 进入绘制模式（内置工具栏事件）
        /// </summary>
        private void OnMeasureSelectionChanged(object? sender, MeasureGeometryType? type)
        {
            if (type == null) return;
            StartDraw(type.Value);
        }

        #endregion

        #region 公开 API

        /// <summary>
        /// 设置叠加画布（由管理器统一路由事件后调用，无头模式，对齐 RoiEditorControl）
        /// </summary>
        /// <param name="overlayCanvas">叠加画布（null 表示解除，Fail-Forward 安全）</param>
        public void SetOverlayCanvas(Canvas? overlayCanvas)
        {
            _overlayCanvas = overlayCanvas;
            _overlay = overlayCanvas != null ? new MeasureBandOverlay(overlayCanvas) : null;
            Render();
        }

        /// <summary>
        /// 设置卡尺渲染参数（法线全宽 / 切线全长 / 数量）
        /// </summary>
        public void SetCaliberParams(double width, double length, int count)
        {
            _bandWidth = Math.Max(1.0, width);
            _bandLength = Math.Max(1.0, length);
            _measureCount = Math.Max(1, count);
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[Diag] SetCaliberParams W={_bandWidth:F2} H={_bandLength:F2} C={_measureCount} overlay={_overlay != null} drawing={_model.IsDrawing} hasGuide={_model.HasGuide}",
                "MeasureEditorControl");
            Render();
            CaliberParamsChanged?.Invoke(this, new CaliberParamsChangedEventArgs(_bandWidth, _bandLength, _measureCount));
        }

        /// <summary>
        /// 限制可选的卡尺引导类型（隐藏不支持的工具栏项）
        /// 对齐 Roi 编辑器"锁定形状类型"模式，工具层调用，SDK 控件只提供能力。
        /// 默认全部可见（EdgeDetect 多几何工具），单几何工具（直线/圆查找）传入自身类型即可。
        /// </summary>
        public void SetAllowedGeometryTypes(params MeasureGeometryType[] allowedTypes)
        {
            var allowed = allowedTypes ?? Array.Empty<MeasureGeometryType>();
            foreach (var item in CaliberTools)
            {
                item.IsVisible = Array.IndexOf(allowed, item.GeometryType) >= 0;
            }
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
        /// 仅锁定几何类型（不进入绘制模式）。
        /// 用于工具打开时绑定类型：设置交互策略但不让编辑器处于"待绘制"状态，
        /// 用户需主动点击工具栏卡尺按钮才会开始绘制。
        /// </summary>
        public void LockGeometryType(MeasureGeometryType geometryType)
        {
            _geometryType = geometryType;
            _interaction = GuideInteractionFactory.Get(geometryType);
        }

        /// <summary>
        /// 清除内置卡尺类型工具栏选中状态（绘制完成后对齐 Roi，回到选择模式）
        /// </summary>
        public void ClearCaliberSelection()
        {
            MeasureToolBarControl.ClearSelection();
        }

        /// <summary>
        /// 取消当前绘制/选中（由管理器在互斥时调用，让另一个编辑器可响应，避免弧冲突）
        /// </summary>
        public void CancelDrawing()
        {
            if (_model.Mode == MeasureInteractionMode.Draw)
            {
                _model.Mode = MeasureInteractionMode.Select;
                _model.IsDrawing = false;
            }
            _model.Deselect();
            _currentHandles = null;
            Render();
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
        /// 当前引导几何是否被选中（供参数面板显示）
        /// </summary>
        public bool IsGuideSelected => _model.IsSelected;

        /// <summary>
        /// 当前编辑中的引导直线（null=非直线模式）
        /// </summary>
        public Line2d? EditingLine => _model.EditingLine;

        /// <summary>
        /// 当前编辑中的引导圆（null=非圆模式）
        /// </summary>
        public Circle2d? EditingCircle => _model.EditingCircle;

        /// <summary>
        /// 当前编辑中的引导圆弧（null=非圆弧模式）
        /// </summary>
        public Arc2d? EditingArc => _model.EditingArc;

        /// <summary>
        /// 当前引导几何类型
        /// </summary>
        public MeasureGeometryType GuideGeometryType => _geometryType;

        /// <summary>
        /// 当前引导几何（用于参数面板双向同步）
        /// </summary>
        public MeasureInteractionModel InteractionModel => _model;

        /// <summary>卡尺宽度（法线方向全宽，供参数面板显示）</summary>
        public double CaliberWidth => _bandWidth;

        /// <summary>卡尺长度（切线方向全长，供参数面板显示）</summary>
        public double CaliberHeight => _bandLength;

        /// <summary>卡尺数量（供参数面板显示）</summary>
        public int CaliberCount => _measureCount;

        /// <summary>
        /// 触发引导渲染（由管理器在统一渲染时调用，无头模式）
        /// </summary>
        public void RenderForManager() => Render();

        /// <summary>
        /// 当前是否正在交互（供宿主路由判断）
        /// </summary>
        public bool IsAnyInteractionInProgress => _model.IsDrawing || _isDragging || _isDraggingHandle;

        /// <summary>
        /// 当前是否处于绘制模式（供管理器路由：绘制模式优先）
        /// </summary>
        public bool IsDrawingMode => _model.Mode == MeasureInteractionMode.Draw;

        #endregion

        #region 鼠标事件

        /// <summary>
        /// 处理鼠标按下（由管理器统一路由，无头模式）
        /// </summary>
        /// <returns>是否消费该事件（false 表示未命中任何交互对象，交给其他编辑器）</returns>
        public bool ProcessMouseDown(Point pos)
        {
            // 位置修正：鼠标显示坐标 → 参考系坐标（几何始终存参考系）
            pos = ToReference(pos);

            // 绘制模式：创建引导几何
            if (_model.Mode == MeasureInteractionMode.Draw)
            {
                _model.DrawStart = pos;
                _model.IsDrawing = true;
                return true;
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
                        return true;
                    }
                }

                // ② 引导主体命中 → 移动
                if (HitTestGuide(pos, _guideHitTolerance))
                {
                    _model.IsSelected = true;
                    _isDragging = true;
                    _lastMouse = pos;
                    return true;
                }

                // ③ 卡尺测量带命中 → 移动
                if (HitTestBand(pos))
                {
                    _model.IsSelected = true;
                    _isDragging = true;
                    _lastMouse = pos;
                    return true;
                }
            }

            // 未命中任何交互对象 → 取消选中（返回 false，允许其他编辑器处理）
            _model.Deselect();
            _currentHandles = null;
            Render();
            return false;
        }

        /// <summary>
        /// 处理鼠标移动（由管理器统一路由，无头模式）
        /// </summary>
        public void ProcessMouseMove(Point pos)
        {
            if (_overlay == null) return;

            // 位置修正：鼠标显示坐标 → 参考系坐标
            pos = ToReference(pos);

            // 拖动手柄 → 编辑引导几何
            if (_isDraggingHandle)
            {
                HandleShapeEdit(pos);
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
                return;
            }

            // 绘制预览（只画引导几何，不画测量带）
            if (_model.Mode == MeasureInteractionMode.Draw && _model.IsDrawing)
            {
                _lastMouse = pos;
                UpdateDrawPreview(pos);
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

            // 位置修正：鼠标显示坐标 → 参考系坐标
            pos = ToReference(pos);

            // ① 手柄 → 按手柄类型显示不同光标（优先级最高）
            if (_currentHandles != null)
            {
                var handleType = HitTestHandles(pos);
                if (handleType != HandleType.None)
                {
                    Mouse.OverrideCursor = CursorForHandle(handleType, _geometryType);
                    return;
                }
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

        /// <summary>
        /// 手柄类型 → 光标映射（不同命中目标显示不同光标）
        /// </summary>
        /// <param name="handleType">手柄类型</param>
        /// <param name="geometryType">当前引导几何类型（圆弧的 Right 手柄是 P3 弧中点，非水平缩放）</param>
        private Cursor CursorForHandle(HandleType handleType, MeasureGeometryType geometryType)
        {
            // 圆弧的 Right 手柄 = P3 弧中点（三点定圆改半径/方向/大小弧）→ 编辑手形，非水平缩放
            if (geometryType == MeasureGeometryType.Arc && handleType == HandleType.Right)
                return Cursors.Hand;

            return handleType switch
            {
                // 中心手柄 → 整体移动
                HandleType.Center => Cursors.SizeAll,
                // 上下缩放手柄 → 垂直缩放
                HandleType.Top or HandleType.Bottom => Cursors.SizeNS,
                // 左右缩放手柄 → 水平缩放
                HandleType.Left or HandleType.Right => Cursors.SizeWE,
                // 角落缩放手柄 → 对角缩放
                HandleType.TopLeft or HandleType.BottomRight => Cursors.SizeNWSE,
                HandleType.TopRight or HandleType.BottomLeft => Cursors.SizeNESW,
                // 主卡尺缩放手柄 → 对角缩放（同时编辑宽/高）
                HandleType.CaliberResize => Cursors.SizeNWSE,
                // 旋转手柄
                HandleType.Rotate => Cursors.Hand,
                // 直线端点 / 多边形顶点 / 其他 → 默认手（编辑）
                _ => Cursors.Hand,
            };
        }

        /// <summary>
        /// 处理鼠标抬起（由管理器统一路由，无头模式；完成位置用最后记录的鼠标位置）
        /// </summary>
        public void ProcessMouseUp()
        {
            if (_model.IsDrawing)
            {
                _model.IsDrawing = false;
                CompleteDraw(_lastMouse);
                return;
            }

            if (_isDraggingHandle || _isDragging)
            {
                _isDraggingHandle = false;
                _isDragging = false;
                _activeHandleType = HandleType.None;
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
                    // ★ 分布相位与 Halcon 对齐：第一条卡尺中心在 start_phi（无半格偏移）
                    double angle = a.StartAngle + i * stepAngle;
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
        /// 中心手柄（Center）走整体平移，其余手柄按几何类型分发编辑
        /// </summary>
        private void HandleShapeEdit(Point current)
        {
            // 主卡尺缩放手柄：同时编辑卡尺宽/高（拖拽对角缩放，从初始半宽/半长恢复基准）
            if (_activeHandleType == HandleType.CaliberResize)
            {
                EditCaliberResize(current);
                return;
            }

            // 中心手柄：整体移动（拖动整个圆/矩形，对齐 Roi 中心手柄语义）
            if (_activeHandleType == HandleType.Center)
            {
                var delta = current - _lastMouse;
                if (delta.Length > 1e-9)
                {
                    _model.Translate(delta);
                    _lastMouse = current;
                    Render();
                    NotifyGuideChanged();
                }
                return;
            }

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
                // 标记"来自绘制流程"，宿主据此清除工具栏选中（对齐 Roi）
                NotifyGuideChanged(isDrawing: true);
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

            // 位置修正矩阵：参考系几何 → 当前图像坐标（overlay 内部变换）
            var matrix = _positionCorrectionMatrix;

            // 绘制中只画引导几何预览，不画测量带
            bool drawBands = !_model.IsDrawing;

            if (_model.EditingLine.HasValue)
            {
                _overlay.RenderLine(_model.LineStartPoint, _model.LineEndPoint, _bandWidth, _bandLength, _measureCount, drawBands, matrix, _zoom);
            }
            else if (_model.EditingCircle.HasValue)
            {
                _overlay.RenderCircle(_model.EditingCircle.Value, _bandWidth, _bandLength, _measureCount, drawBands, matrix, _zoom);
            }
            else if (_model.EditingArc.HasValue && !_model.EditingArc.Value.IsEmpty)
            {
                _overlay.RenderArc(_model.EditingArc.Value, _bandWidth, _bandLength, _measureCount, drawBands, matrix, _zoom);
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
        /// 更新手柄数组（引导几何手柄 + 主卡尺缩放手柄）
        /// </summary>
        private void UpdateHandles()
        {
            _currentHandles = _model.HasGuide
                ? _interaction.CreateHandles(_model, _zoom)
                : null;

            // 追加主卡尺缩放手柄（一个手柄同时编辑宽/高，需引导几何存在）
            if (_currentHandles != null && !_model.IsDrawing)
            {
                var caliberHandle = CreateCaliberResizeHandle();
                if (caliberHandle != null)
                {
                    _currentHandles = _currentHandles.Append(caliberHandle).ToArray();
                }
            }
        }

        /// <summary>
        /// 创建主卡尺缩放手柄（位于主卡尺右上角，拖拽对角缩放同时编辑卡尺宽/高）
        /// </summary>
        private EditHandle? CreateCaliberResizeHandle()
        {
            if (_overlay == null || !_model.HasGuide) return null;
            var frame = GetMainCaliberFrame();
            if (frame == null) return null;

            // 右上角 = 中心 + 长轴*半长 + 宽轴*半宽
            var corner = frame.Value.Center
                + frame.Value.LongAxis * (_bandLength / 2)
                + frame.Value.WidthAxis * (_bandWidth / 2);

            return new EditHandle
            {
                Type = HandleType.CaliberResize,
                Position = corner,
                HandleSize = HandleRenderer.GetScaledHandleSize(_zoom),
                Cursor = Cursors.SizeNWSE,
            };
        }

        /// <summary>
        /// 主卡尺几何帧（中心 + 长轴/宽轴方向基）
        /// </summary>
        private readonly record struct CaliberFrame(Point Center, Vector LongAxis, Vector WidthAxis);

        /// <summary>
        /// 计算主卡尺（中间测量带）的几何帧
        /// </summary>
        /// <remarks>
        /// 与 MeasureBandOverlay 渲染的主卡尺定义一致：
        /// - Line：measureCount/2 索引（中间条）；长轴=引导线方向（切向），宽轴=法线
        /// - Circle：0°（正右方）条；长轴=切向，宽轴=径向
        /// - Arc：measureCount/2 索引（弧中点条）；长轴=切向，宽轴=径向
        /// </remarks>
        private CaliberFrame? GetMainCaliberFrame()
        {
            int mainIndex = _measureCount / 2;

            if (_model.EditingLine.HasValue)
            {
                var seg = _model.LineEndPoint - _model.LineStartPoint;
                var totalLen = seg.Length;
                if (totalLen < 1e-6) return null;
                var dir = seg / totalLen;
                double step = totalLen / Math.Max(1, _measureCount);
                var center = _model.LineStartPoint + dir * ((mainIndex + 0.5) * step);
                return new CaliberFrame(center, dir, new Vector(-dir.Y, dir.X));
            }

            if (_model.EditingCircle.HasValue)
            {
                var c = _model.EditingCircle.Value;
                if (c.Radius <= 0) return null;
                // 主卡尺 = 0°（正右方）条；径向=宽轴(+X)，切向=长轴(+Y)
                var center = c.Center + new Vector(1, 0) * c.Radius;
                return new CaliberFrame(center, new Vector(0, 1), new Vector(1, 0));
            }

            if (_model.EditingArc.HasValue && !_model.EditingArc.Value.IsEmpty)
            {
                var a = _model.EditingArc.Value;
                if (a.Radius <= 0 || Math.Abs(a.AngularSpan) <= 1e-6) return null;
                double span = a.AngularSpan;
                double stepAngle = span / Math.Max(1, _measureCount);
                // ★ 分布相位与 Halcon 对齐：第一条卡尺中心在 start_phi（无半格偏移）
                double angle = a.StartAngle + mainIndex * stepAngle;
                // 角度规范化到 [-π, π]，避免跨 0° 边界时 cos/sin 大角度误差
                angle = ((angle + Math.PI) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI) - Math.PI;
                var radial = new Vector(Math.Cos(angle), Math.Sin(angle));  // 径向=宽轴
                var center = a.Center + radial * a.Radius;
                return new CaliberFrame(center, new Vector(-radial.Y, radial.X), radial);
            }

            return null;
        }

        /// <summary>
        /// 主卡尺缩放手柄拖拽编辑：同时更新卡尺宽/高
        /// </summary>
        /// <remarks>
        /// 以主卡尺中心为锚点，鼠标偏移在长轴/宽轴的投影决定新的半长/半宽。
        /// 单手柄对角缩放：长轴投影 → 卡尺长，宽轴投影 → 卡尺宽（含下限保护）。
        /// </remarks>
        private void EditCaliberResize(Point current)
        {
            if (!_model.HasGuide) return;

            var frame = GetMainCaliberFrame();
            if (frame == null) return;
            var anchor = frame.Value.Center;
            var longAxis = frame.Value.LongAxis;
            var widthAxis = frame.Value.WidthAxis;

            // 鼠标相对锚点偏移在长轴/宽轴的投影（带下限保护）
            var v = current - anchor;
            double halfLength = Math.Abs(v.X * longAxis.X + v.Y * longAxis.Y);
            double halfWidth = Math.Abs(v.X * widthAxis.X + v.Y * widthAxis.Y);

            const double minHalf = 1.0;
            halfLength = Math.Max(minHalf, halfLength);
            halfWidth = Math.Max(minHalf, halfWidth);

            SetCaliberParams(halfWidth * 2, halfLength * 2, _measureCount);
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
            // 位置修正：手柄参考系坐标 → 当前图像坐标（绘制用副本，不污染 _currentHandles）
            if (_positionCorrectionMatrix != null)
            {
                var displayHandles = new EditHandle[_currentHandles.Length];
                for (int i = 0; i < _currentHandles.Length; i++)
                {
                    var h = _currentHandles[i];
                    displayHandles[i] = new EditHandle
                    {
                        Type = h.Type,
                        Position = ToDisplay(h.Position),
                        Bounds = h.Bounds,
                        Cursor = h.Cursor,
                        HandleSize = h.HandleSize,
                        VertexIndex = h.VertexIndex,
                    };
                }
                HandleRenderer.DrawHandles(_overlayCanvas, displayHandles, shapeType, _zoom);
                return;
            }
            HandleRenderer.DrawHandles(_overlayCanvas, _currentHandles, shapeType, _zoom);
        }

        /// <summary>
        /// 通知宿主引导几何已变更
        /// </summary>
        /// <param name="isDrawing">是否来自绘制流程（true=绘制完成，宿主应清除工具栏选中，对齐 Roi）</param>
        private void NotifyGuideChanged(bool isDrawing = false)
        {
            GuideGeometryChanged?.Invoke(this, new GuideGeometryChangedEventArgs(_model, _geometryType, isDrawing));
        }

        #endregion

        #region 字段

        private MeasureGeometryType _geometryType = MeasureGeometryType.Line;

        #endregion
    }
}

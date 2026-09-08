using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Constants;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.ViewModels;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Views
{
    /// <summary>
    /// 区域编辑控件 - 纯UI组件，不负责数据持久化
    /// </summary>
    public partial class RoiEditorControl : UserControl, INotifyPropertyChanged
    {
        private RoiEditorViewModel _viewModel;
        private ShapeType _currentTool = ShapeType.Point;  // 初始为选择模式（Point=选择）

        // 控件实例ID（用于区分不同的 RoiEditorControl 实例）
        private readonly string _controlInstanceId = Guid.NewGuid().ToString("N").Substring(0, 8);

        // 绘制状态
        private bool _isDrawing;
        private bool _isDragging;
        private bool _isDraggingHandle;
        private Point _startPoint;
        private Roi? _currentDrawingRoi;
        private Roi? _selectedRoi;
        private Point _dragStartPoint;
        private Point _handleDragStartPoint;

        // 多边形绘制状态
        private bool _isDrawingPolygon;
        private readonly List<Point> _polygonPoints = new();
        private int _activeVertexIndex = -1;
        private const string RubberBandTag = "PolygonRubberBand";

        // 记录上次鼠标位置（用于多边形删除后临时顶点预览）
        private Point _lastMouseImagePosition;

        // ★ 多边形双击完成检测（时间阈值 ≤ 系统双击时间）
        private DateTime _lastPolygonClickTime = DateTime.MinValue;
        private const int DoubleClickTimeMs = 500;

        // 日志监控计数器
        private int _mouseMoveLogCounter = 0;
        private int _mouseDownLogCounter = 0;
        private int _drawingLogCount = 0;
        private int _previewLogCount = 0;

        // ★ 位置修正诊断计数器（每 100 次渲染输出一次，避免热路径刷日志）
        private int _correctionStatusCount = 0;
        private const int CORRECTION_LOG_INTERVAL = 100;

        // 区域名称索引管理（空洞池策略）
        private int _nextRoiIndex = 1;
        private readonly SortedSet<int> _availableIndices = new();

        private RoiEditorSettings _settings;

        // ROI编辑器风格的手柄管理
        private Rendering.EditHandle[] _currentHandles = null;
        private Rendering.HandleType _activeHandleType = Rendering.HandleType.None;
        private RoiShape? _originalShapeDefinition = null;
        private Point _originalPosition;
        private Size _originalSize;
        private double _originalRotation;
        private double? _rotateStartAngle = null;
        private double? _rotateStartMouseAngle = null;

        // 位置修正矩阵 (参考→测试)
        private AffineMatrix? _positionCorrectionMatrix;
        private AffineMatrix? _inverseMatrix;

        /// <summary>
        /// Overlay Canvas引用（由RoiEditorManagerControl设置，用于手柄/橡皮筋绘制）
        /// </summary>
        public Canvas? OverlayCanvas { get; set; }

        /// <summary>
        /// 当前缩放级别（由Manager设置，用于手柄渲染）
        /// </summary>
        public double CurrentZoom { get; set; } = 1.0;

        /// <summary>
        /// 需要重新渲染时触发（Manager订阅）
        /// </summary>
        public event Action? RenderRequested;

        /// <summary>
        /// 设置位置修正矩阵。
        /// 显示正向变换，编辑逆变换，存储不变（始终存参考坐标系）。
        /// </summary>
        public void SetPositionCorrectionMatrix(AffineMatrix? matrix)
        {
            _positionCorrectionMatrix = matrix;
            _inverseMatrix = matrix?.Inverse();

            // ★ 诊断：设置矩阵时输出完整状态汇总
            LogCorrectionStatus("SetPositionCorrectionMatrix");

            PluginLogger.Info($"[SetPositionCorrectionMatrix] 矩阵已设置 | Matrix={(matrix != null ? $"[{matrix.M11:F4},{matrix.M12:F4},{matrix.M21:F4},{matrix.M22:F4},{matrix.OffsetX:F2},{matrix.OffsetY:F2}]" : "null")} | RenderRequested触发", "RoiEditorControl");

            RenderRequested?.Invoke();
        }

        /// <summary>
        /// 按几何有向性解析位置修正矩阵（形状语义唯一源，对齐 SDK 契约）：
        /// 无向形状（轴对齐矩形/圆/圆环）→ 剥离旋转（仅平移+均匀缩放）；
        /// 有向形状 / 未知 → 完整矩阵（跟随旋转）。
        /// </summary>
        private AffineMatrix? CorrectionFor(object? geometry) =>
            PositionCorrectionResolver.ResolveForShape(geometry, _positionCorrectionMatrix);

        /// <summary>
        /// 显示坐标 → 参考坐标：按几何类型选择对应修正矩阵的逆矩阵。
        /// 编辑/命中始终在参考坐标系进行，无向形状用剥转矩阵逆（无旋转分量），
        /// 保证手柄显示位置与命中位置一致。
        /// </summary>
        private Point ToReferencePoint(Point display, object? geometry)
        {
            var matrix = PositionCorrectionResolver.ResolveForShape(geometry, _positionCorrectionMatrix);
            return matrix != null ? matrix.Inverse().TransformWpfPoint(display) : display;
        }

        /// <summary>
        /// 当前交互几何：优先正在编辑的选中区域，其次正在绘制的新区域。
        /// 用于命中/拖拽时选择与形状一致的修正矩阵。
        /// </summary>
        private object? CurrentInteractionGeometry()
        {
            if (_selectedRoi?.LocalGeometry is RoiShape sShape)
                return sShape.Geometry;
            if (_currentDrawingRoi?.LocalGeometry is RoiShape dShape)
                return dShape.Geometry;
            return null;
        }

        /// <summary>
        /// 新建形状的模板几何（值无意义，仅用于按工具类型判定形状有向性）。
        /// </summary>
        private static object? TemplateGeometryForTool(ShapeType tool) => tool switch
        {
            ShapeType.Rectangle => new Rect(0, 0, 0, 0),
            ShapeType.Circle => new Circle2d(0, 0, 0),
            ShapeType.RotatedRectangle => new RotatedRect2d(0, 0, 0, 0, 0),
            ShapeType.Line => new Line2d(new Point(0, 0), new Vector(1, 0)),
            ShapeType.Polygon => new Polygon2d(Array.Empty<Point>()),
            _ => null,
        };

        /// <summary>
        /// 输出位置修正状态汇总日志（收集-总结模式）。
        /// 一次调用输出完整的状态信息，便于全链分析而非分散查找。
        /// </summary>
        private void LogCorrectionStatus(string trigger)
        {
            if (_correctionStatusCount++ % CORRECTION_LOG_INTERVAL != 0)
                return;

            var matrixInfo = _positionCorrectionMatrix != null
                ? $"M11={_positionCorrectionMatrix.M11:F4} M12={_positionCorrectionMatrix.M12:F4} " +
                  $"M21={_positionCorrectionMatrix.M21:F4} M22={_positionCorrectionMatrix.M22:F4} " +
                  $"OffsetX={_positionCorrectionMatrix.OffsetX:F2} OffsetY={_positionCorrectionMatrix.OffsetY:F2}"
                : "(null)";
            var inverseInfo = _inverseMatrix != null
                ? $"M11={_inverseMatrix.M11:F4} M12={_inverseMatrix.M12:F4} " +
                  $"M21={_inverseMatrix.M21:F4} M22={_inverseMatrix.M22:F4} " +
                  $"OffsetX={_inverseMatrix.OffsetX:F2} OffsetY={_inverseMatrix.OffsetY:F2}"
                : "(null)";

            var rois = _viewModel.Rois;
            var roiSummary = rois.Count == 0
                ? "(空)"
                : string.Join(" | ", rois.Take(5).Select(r =>
                    r.LocalGeometry is RoiShape s
                        ? $"{r.Name}:Type={s.ShapeType} Center={s.GetCenter()}"
                        : $"{r.Name}:订阅={r.Binding?.ToString() ?? "null"}"));

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[LogCorrectionStatus] Trigger={trigger} InstanceId={_controlInstanceId}\n" +
                $"  ForwardMatrix={matrixInfo}\n" +
                $"  InverseMatrix={inverseInfo}\n" +
                $"  RoiCount={rois.Count} Rois=[{roiSummary}]",
                "RoiEditorControl");
        }

        /// <summary>
        /// ★ Phase 4: 工具激活事件（Manager 用于分组互斥控制）
        /// 当用户点击形状工具或选择模式按钮时触发，通知 Manager 同一时间只有一个编辑器的工具被激活
        /// </summary>
        public event Action? ToolActivated;

        public RoiEditorViewModel ViewModel => _viewModel;

        #region 依赖属性

        /// <summary>
        /// 几何输入集合（仅用于 XAML 声明式绑定初始化读取）
        /// </summary>
        /// <remarks>
        /// 此 DependencyProperty 仅作为 XAML 绑定的桥接层。
        /// 真正的共享引用由 SetRoisSource() 方法命令式建立。
        /// 不要依赖此属性的变更回调来建立共享引用。
        /// </remarks>
        public static readonly DependencyProperty RoisProperty =
            DependencyProperty.Register(nameof(Rois), typeof(IEnumerable<Roi>), typeof(RoiEditorControl),
                new PropertyMetadata(null, OnRoisChanged));

        public IEnumerable<Roi>? Rois
        {
            get => (IEnumerable<Roi>?)GetValue(RoisProperty);
            set => SetValue(RoisProperty, value);
        }

        private static void OnRoisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiEditorControl control)
            {
                control.RenderRequested?.Invoke();
            }
        }

        /// <summary>
        /// AvailableDataSources 透传属性（用于内部 RoiSubscribePanel）
        /// </summary>
        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(nameof(AvailableDataSources),
                typeof(System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>),
                typeof(RoiEditorControl),
                new PropertyMetadata(null));

        public System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>? AvailableDataSources
        {
            get => (System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>?)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        /// <summary>
        /// 新创建区域的默认类型（Inspection/Mask）
        /// RoiEditorManagerControl 使用此属性区分检测区域和屏蔽区域编辑器
        /// </summary>
        public static readonly DependencyProperty DefaultRegionTypeProperty =
            DependencyProperty.Register(nameof(DefaultRegionType),
                typeof(RoiType),
                typeof(RoiEditorControl),
                new PropertyMetadata(RoiType.Inspection, OnDefaultRegionTypeChanged));

        public RoiType DefaultRegionType
        {
            get => (RoiType)GetValue(DefaultRegionTypeProperty);
            set => SetValue(DefaultRegionTypeProperty, value);
        }

        #region 区域类型标题（由 DefaultRegionType 自动派生）

        /// <summary>
        /// 区域类型标题文本（检测区域/屏蔽区域）
        /// </summary>
        public static readonly DependencyProperty RegionTypeTitleProperty =
            DependencyProperty.Register(nameof(RegionTypeTitle),
                typeof(string),
                typeof(RoiEditorControl),
                new PropertyMetadata("检测区域"));

        public string RegionTypeTitle
        {
            get => (string)GetValue(RegionTypeTitleProperty);
            set => SetValue(RegionTypeTitleProperty, value);
        }

        /// <summary>
        /// 区域类型标题颜色（检测区域=蓝色，屏蔽区域=橙色；ROI 语义内容色，固定不随主题切换）
        /// </summary>
        public static readonly DependencyProperty RegionTypeTitleBrushProperty =
            DependencyProperty.Register(nameof(RegionTypeTitleBrush),
                typeof(Brush),
                typeof(RoiEditorControl),
                new PropertyMetadata(ColorConstants.InspectionBlueBrush));

        public Brush RegionTypeTitleBrush
        {
            get => (Brush)GetValue(RegionTypeTitleBrushProperty);
            set => SetValue(RegionTypeTitleBrushProperty, value);
        }

        private static void OnDefaultRegionTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiEditorControl control)
            {
                var newType = (RoiType)e.NewValue;
                control.RegionTypeTitle = newType == RoiType.Inspection ? "检测区域" : "屏蔽区域";
                control.RegionTypeTitleBrush = newType == RoiType.Inspection
                    ? ColorConstants.InspectionBlueBrush
                    : ColorConstants.MaskOrangeBrush;
            }
        }

        #endregion

        /// <summary>
        /// 当前选中的形状工具
        /// </summary>
        public ShapeType CurrentShapeTool => _currentTool;

        #endregion

        #region 事件

        /// <summary>
        /// 几何输入数据变更事件
        /// </summary>
        public event EventHandler<Roi>? RoiDataChanged;

        #endregion

        #region 共享引用管理

        /// <summary>
        /// 注入外部几何输入集合，与参数模型共享同一个 ObservableCollection 实例
        /// </summary>
        /// <remarks>
        /// 这是建立共享引用的唯一正确入口。
        /// 调用后 ViewModel.Rois 和外部集合指向同一个对象，
        /// 用户绘制的几何输入直接进入参数集合，序列化时自然有数据。
        /// 
        /// 必须在 SetMainImageControl 之后调用。
        /// </remarks>
        public void SetRoisSource(ObservableCollection<Roi> externalRois)
        {
            if (externalRois == null)
                throw new ArgumentNullException(nameof(externalRois));

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[SetRoisSource] 建立共享引用 | 外部集合HashCode={externalRois.GetHashCode():X8} | 外部集合数量={externalRois.Count}",
                "RoiEditorControl");

            _viewModel.SetRoisSource(externalRois);
            InitializeRoiIndexFromExisting();
            RenderRequested?.Invoke();

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[SetRoisSource] 共享引用已建立 | ViewModel.Rois={_viewModel.Rois.GetHashCode():X8} | 引用相同={_viewModel.Rois == externalRois}",
                "RoiEditorControl");
        }

        #endregion

        public RoiEditorControl()
        {
            // 设计时完全跳过初始化，避免 XAML 解析触发 NullReferenceException
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            // ★ 必须在 InitializeComponent 之前创建 ViewModel
            // 因为 XAML 中 RadioButton IsChecked="True" 会立即触发 Checked 事件
            EnsureViewModel();
            InitializeComponent();
            _settings = RoiEditorSettings.Default;

            // 初始化形状工具栏（数据驱动，每个形状可见性可单独控制）
            InitializeShapeTools();
            ShapeToolBarControl.ShapeSelectionChanged += OnShapeToolBarSelectionChanged;

            // 启用键盘焦点
            Focusable = true;
            FocusVisualStyle = null;

            // 绑定键盘快捷键和生命周期事件
            Loaded += RegionEditorControl_Loaded;
            Unloaded += RegionEditorControl_Unloaded;

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[RoiEditorControl] 控件初始化完成 | InstanceId={_controlInstanceId}",
                "RoiEditorControl");

            // ★ 诊断：输出初始位置修正状态（应显示 Matrix=null）
            LogCorrectionStatus("Constructor");
        }

        private void RegionEditorControl_Loaded(object sender, RoutedEventArgs e)
        {
            VisionLogger.Instance.Log(LogLevel.Success,
                $"[RoiEditorControl] 控件已加载（Tab切换）| InstanceId={_controlInstanceId}",
                "RoiEditorControl");

            Keyboard.Focus(this);

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[RegionEditorControl_Loaded] 加载完成 | InstanceId={_controlInstanceId} | RegionCount={_viewModel.Rois.Count}",
                "RoiEditorControl");
        }

        private void RegionEditorControl_Unloaded(object sender, RoutedEventArgs e)
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RoiEditorControl] 控件已卸载（Tab切换）| InstanceId={_controlInstanceId} | _isDrawing={_isDrawing}",
                "RoiEditorControl");

            // 如果正在绘制中，取消绘制状态（避免切换 Tab 时绘制状态不一致）
            if (_isDrawing)
            {
                _isDrawing = false;
                _currentDrawingRoi = null;
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[RegionEditorControl_Unloaded] Tab切换时取消绘制状态 | InstanceId={_controlInstanceId}",
                    "RoiEditorControl");
            }
        }

        /// <summary>
        /// 确保 ViewModel 已创建（懒加载）
        /// </summary>
        private void EnsureViewModel()
        {
            if (_viewModel == null)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    "[EnsureViewModel] 创建新的 ViewModel 实例",
                    "RoiEditorControl");

                _viewModel = new RoiEditorViewModel();
                DataContext = _viewModel;

                // ★ 订阅 ViewModel 事件（在整个生命周期内保持订阅）
                // 事件订阅生命周期由 SetMainImageControl / ClearMainOverlay 管理
                // Tab 切换不应该影响订阅状态
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _viewModel.RoiChanged += OnRoiChanged;

                // 不再订阅 Rois.CollectionChanged，共享引用后无需同步

                VisionLogger.Instance.Log(LogLevel.Success,
                    "[EnsureViewModel] ViewModel 创建并初始化完成",
                    "RoiEditorControl");
            }
            else
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    "[EnsureViewModel] ViewModel 已存在，跳过创建",
                    "RoiEditorControl");
            }
        }



        /// <summary>
        /// 设置主界面ImageControl引用（已废弃 - 由RoiEditorManagerControl统一管理）
        /// </summary>
        [Obsolete("由RoiEditorManagerControl统一管理事件和渲染，不再需要直接调用")]
        public void SetMainImageControl(ImageControl imageControl)
        {
            // 空实现，保持向后兼容
        }

        /// <summary>
        /// 清理主界面覆盖层（已废弃 - 由RoiEditorManagerControl统一管理）
        /// </summary>
        [Obsolete("由RoiEditorManagerControl统一管理事件和渲染")]
        public void ClearMainOverlay()
        {
            // 空实现，保持向后兼容
        }

        /// <summary>
        /// 加载图像（已废弃）
        /// </summary>
        [Obsolete("不再由RegionEditorControl管理图像加载")]
        public void LoadImage(BitmapSource image)
        {
        }

        /// <summary>
        /// 获取全部几何输入集合
        /// </summary>
        public IEnumerable<Roi> GetRois()
        {
            return _viewModel.Rois;
        }

        /// <summary>
        /// 获取选中的区域
        /// </summary>
        public Roi? GetSelectedRoi()
        {
            return _selectedRoi;
        }

        /// <summary>
        /// 设置选中的区域
        /// </summary>
        public void SetSelectedRoi(Roi? roi)
        {
            SelectRoi(roi);
        }

        /// <summary>
        /// 添加区域
        /// </summary>
        public void AddRoi(Roi roi)
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[AddRoi] 添加几何输入: RoiId={roi.Id}, ShapeType={roi.LocalGeometry?.ShapeType.ToString() ?? "Unknown"}",
                "RoiEditorControl");

            try
            {
                _viewModel.Rois.Add(roi);
                RenderRequested?.Invoke();
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[AddRoi] 添加几何输入失败: RoiId={roi.Id}, 错误={ex.Message}",
                    "RoiEditorControl",
                    ex);
            }
        }

        /// <summary>
        /// 移除区域
        /// </summary>
        public void RemoveRoi(Roi roi)
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RemoveRoi] 移除几何输入: RoiId={roi.Id}, ShapeType={roi.LocalGeometry?.ShapeType.ToString() ?? "Unknown"}",
                "RoiEditorControl");

            try
            {
                _viewModel.Rois.Remove(roi);
                if (_selectedRoi == roi)
                {
                    UnsubscribeRoiShape(_selectedRoi);
                    _selectedRoi = null;
                }
                RenderRequested?.Invoke();
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[RemoveRoi] 移除几何输入失败: RoiId={roi.Id}, 错误={ex.Message}",
                    "RoiEditorControl",
                    ex);
            }
        }

        #region 键盘快捷键

        /// <summary>
        /// 处理键盘按键
        /// </summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // ★ Delete: 直接在编辑器处理（焦点在 DebugWindow 时 Manager 的 Window 级订阅收不到事件）
            //   Manager 的备份处理作为 MainWindow 直接操作时的安全保障
            if (e.Key == Key.Delete)
            {
                HandleDelete();
                e.Handled = true;
                return;
            }

            // ★ Escape/Ctrl+Z/Ctrl+Y 由 Manager 在 Window 级拦截并路由给 _activeEditor
            if (e.Key == Key.Escape)
                return;

            if ((e.Key == Key.Z || e.Key == Key.Y) && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                return;

            // Enter: 完成多边形
            if (e.Key == Key.Enter && _isDrawingPolygon
                && _currentTool == ShapeType.Polygon && _polygonPoints.Count >= 3)
            {
                _isDrawingPolygon = false;
                FinishInteraction();
                e.Handled = true;
                return;
            }

            // Backspace: 撤销多边形最后一个顶点
            if (e.Key == Key.Back && _isDrawingPolygon
                && _currentTool == ShapeType.Polygon && _polygonPoints.Count > 0)
            {
                _polygonPoints.RemoveAt(_polygonPoints.Count - 1);
                SyncPolygonToShapeDef();
                if (_polygonPoints.Count == 0)
                {
                    _isDrawingPolygon = false;
                    if (_currentDrawingRoi != null)
                        ReleaseRoiIndex(_currentDrawingRoi.Name);
                    _currentDrawingRoi = null;
                }
                ClearRubberBand();
                if (_polygonPoints.Count >= 1 && _currentDrawingRoi != null)
                {
                    RenderRequested?.Invoke();
                }
                else if (_polygonPoints.Count == 0)
                    RenderRequested?.Invoke();
                _viewModel.StatusMessage = _polygonPoints.Count > 0
                    ? $"多边形顶点: {_polygonPoints.Count}"
                    : "已取消多边形绘制";
                e.Handled = true;
                return;
            }

            // 快捷键选择形状工具
            switch (e.Key)
            {
                case Key.V:
                    EnterSelectMode();
                    e.Handled = true;
                    break;
                case Key.R:
                    SelectShapeTool(ShapeType.Rectangle);
                    e.Handled = true;
                    break;
                case Key.C:
                    SelectShapeTool(ShapeType.Circle);
                    e.Handled = true;
                    break;
                case Key.O:
                    SelectShapeTool(ShapeType.RotatedRectangle);
                    e.Handled = true;
                    break;
                case Key.L:
                    SelectShapeTool(ShapeType.Line);
                    e.Handled = true;
                    break;
                case Key.P:
                    _isDrawingPolygon = false;
                    SelectShapeTool(ShapeType.Polygon);
                    e.Handled = true;
                    break;
            }
        }

        #endregion

        #region 键盘操作公共方法（由 Manager 路由调用）

        /// <summary>
        /// 处理删除操作（Phase 1 优化：先清空状态再删除）
        /// </summary>
        public void HandleDelete()
        {
            if (_selectedRoi == null) return;

            var roiToDelete = _selectedRoi;

            // ★ 诊断日志：删除前状态
            var beforeRoiNames = string.Join(", ", _viewModel.Rois.Select(r => $"{r.Name}(Id={r.Id:B})"));
            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[Delete] 删除前状态 | InstanceId={_controlInstanceId} | 删除目标={roiToDelete.Name}(Id={roiToDelete.Id:B}) | Rois.Count={_viewModel.Rois.Count} | Rois=[{beforeRoiNames}] | _selectedRoi={roiToDelete.Name}",
                "RoiEditorControl");

            // ★ Phase 1 修复：先取消订阅/清空状态，再触发删除（避免 OnRoiChanged 回调中看到旧状态）
            UnsubscribeRoiShape(roiToDelete);
            _selectedRoi = null;

            _viewModel.RemoveSelectedRoi();

            // RemoveSelectedRoi 内部会触发 RoiChanged → OnRoiChanged → RenderRequested
            // 此时 _selectedRoi=null, GetSelectedRoi()=null, 渲染正确
            var afterRoiNames = string.Join(", ", _viewModel.Rois.Select(r => $"{r.Name}(Id={r.Id:B})"));
            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[Delete] 删除完成 | Rois.Count={_viewModel.Rois.Count} | Rois=[{afterRoiNames}] | SelectedRoi={_viewModel.SelectedRoi?.Name ?? "null"}",
                "RoiEditorControl");
        }

        /// <summary>
        /// 处理 Escape 键（取消绘制/取消选中）
        /// </summary>
        public void HandleEscape()
        {
            if (_isDrawingPolygon)
            {
                _isDrawingPolygon = false;
                _polygonPoints.Clear();
                if (_currentDrawingRoi != null)
                    ReleaseRoiIndex(_currentDrawingRoi.Name);
                _currentDrawingRoi = null;
                ClearRubberBand();
                RenderRequested?.Invoke();
                _viewModel.StatusMessage = "已取消多边形绘制";
            }
            else if (_isDrawing)
            {
                _isDrawing = false;
                _currentDrawingRoi = null;
                RenderRequested?.Invoke();
                _viewModel.StatusMessage = "已取消绘制";
            }
            else if (_isDragging || _isDraggingHandle)
            {
                _isDragging = false;
                _isDraggingHandle = false;
                RenderRequested?.Invoke();
                _viewModel.StatusMessage = "已取消编辑";
            }
            else
            {
                DeselectAll();
            }
        }

        /// <summary>
        /// ★ Phase 2: 设置绘制工具（Manager 共享工具栏按钮调用）
        /// </summary>
        public void SetDrawingTool(ShapeType tool)
        {
            if (tool == ShapeType.Polygon)
                _isDrawingPolygon = false;
            SelectShapeTool(tool);
        }

        /// <summary>
        /// ★ Phase 2: 进入选择模式（Manager 共享工具栏/互斥逻辑调用）
        /// </summary>
        public void EnterSelectMode()
        {
            EnterSelectMode(notifyManager: true);
        }

        /// <summary>
        /// 处理快捷键（由 Manager 在键盘事件中路由给 _activeEditor）
        /// 仅处理非 Delete/Ctrl+Z/Ctrl+Y/Escape 的快捷键
        /// </summary>
        public void HandleShortcutKey(KeyEventArgs e)
        {
            if (e.Handled) return;

            switch (e.Key)
            {
                case Key.V:
                    EnterSelectMode();
                    e.Handled = true;
                    break;
                case Key.R:
                    SelectShapeTool(ShapeType.Rectangle);
                    e.Handled = true;
                    break;
                case Key.C:
                    SelectShapeTool(ShapeType.Circle);
                    e.Handled = true;
                    break;
                case Key.O:
                    SelectShapeTool(ShapeType.RotatedRectangle);
                    e.Handled = true;
                    break;
                case Key.L:
                    SelectShapeTool(ShapeType.Line);
                    e.Handled = true;
                    break;
                case Key.P:
                    _isDrawingPolygon = false;
                    SelectShapeTool(ShapeType.Polygon);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// ★ Phase 2: 退出绘制模式，切回选择模式（Manager 互斥调用）
        /// </summary>
        public void ExitDrawingMode()
        {
            if (_viewModel.IsDrawing)
            {
                EnterSelectMode(notifyManager: false);
            }
        }

        /// <summary>
        /// ★ Phase 4: 进入空闲状态 - 无任何工具选中，无按钮高亮
        /// 由 Manager 在分组互斥时调用，确保被切出的编辑器处于完全空闲状态
        /// </summary>
        public void EnterIdleMode()
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[EnterIdleMode] 进入空闲状态 | InstanceId={_controlInstanceId}",
                "RoiEditorControl");

            // 1. 取消进行中的绘制（多边形顶点、橡皮筋等）
            if (_isDrawing)
            {
                if (_currentDrawingRoi != null)
                    ReleaseRoiIndex(_currentDrawingRoi.Name);
                _currentDrawingRoi = null;
                _isDrawing = false;
                _isDrawingPolygon = false;
                _polygonPoints.Clear();
                ClearRubberBand();
            }

            // 2. 退出绘制状态
            _viewModel.IsDrawing = false;
            _currentTool = ShapeType.None;

            // 3. 取消区域选中
            DeselectAll();

            // 4. 清除形状工具栏选中状态
            ShapeToolBarControl.ClearSelection();

            // 5. 更新状态消息
            _viewModel.StatusMessage = "空闲 - 点击工具栏选择工具";

            // ★ 不触发 ToolActivated（防止连锁反应）
        }

        #endregion

        #region 鼠标事件处理

        /// <summary>
        /// 悬停光标查询（QueryCursor 被动回答）：手柄命中显示手柄光标，区域主体显示移动十字，空白返回 null。
        /// 由管理器路由调用，元素级自动恢复，无全局覆盖泄漏。
        /// </summary>
        /// <param name="displayPos">鼠标在 MainCanvas 的显示坐标</param>
        public Cursor? QueryCursor(Point displayPos)
        {
            if (OverlayCanvas == null) return null;

            var refPosition = ToReferencePoint(displayPos,
                _selectedRoi?.LocalGeometry is RoiShape selShape ? selShape.Geometry : null);

            // ① 手柄命中（复用 HandleRenderer.HitTestHandleDetail，EditHandle 自带光标）
            if (_selectedRoi != null && _currentHandles != null)
            {
                var hit = Rendering.HandleRenderer.HitTestHandleDetail(refPosition, _currentHandles);
                if (hit != null)
                    return hit.Cursor;
            }

            // ② 区域主体命中 → 移动十字（HitTestRoi 内部自行反变换，需传原始显示坐标）
            if (HitTestRoi(displayPos) != null)
                return Cursors.SizeAll;

            return null;
        }

        /// <summary>
        /// 处理鼠标移动（由Manager路由）
        /// </summary>
        public void ProcessMouseMove(Point position)
        {
            if (OverlayCanvas == null) return;

            // 逆变换到参考坐标系用于形状编辑（矩阵按当前交互形状有向性解析）
            Point refPosition = ToReferencePoint(position, CurrentInteractionGeometry());

            _lastMouseImagePosition = refPosition;

            if (_mouseMoveLogCounter++ % 100 == 0)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[ProcessMouseMove] Count={_mouseMoveLogCounter} | Position={position:F1} | RefPosition={refPosition:F1} | Matrix={(_positionCorrectionMatrix != null ? "set" : "null")} | IsDrawing={_isDrawing} | IsDragging={_isDragging} | IsDraggingHandle={_isDraggingHandle}",
                    "RoiEditorControl");
            }

            // ★ 优化: 不再在此触发 RenderRequested，由 Manager 在 OnImageMouseMove 末尾统一调用 RenderAll()
            // 避免两次渲染 (ProcessMouseMove 内部 + Manager.RenderAll)

            if (_isDraggingHandle && _activeHandleType != Rendering.HandleType.None && _selectedRoi?.LocalGeometry is RoiShape shape)
            {
                HandleShapeEdit(shape, refPosition);
                return;
            }

            if (_isDragging && _selectedRoi != null)
            {
                var offset = refPosition - _dragStartPoint;
                MoveRoi(_selectedRoi, offset);
                _dragStartPoint = refPosition;
                return;
            }

            if (_isDrawing && _currentDrawingRoi != null)
            {
                if (_currentTool == ShapeType.Polygon && _isDrawingPolygon)
                {
                    // 多边形橡皮筋线：使用原始测试坐标（OverlayCanvas 在测试坐标系中）
                    DrawPolygonRubberBand(position);
                }
                else
                {
                    if (_drawingLogCount == 0)
                    {
                        VisionLogger.Instance.Log(LogLevel.Info,
                            $"[绘制预览] 开始更新预览 | CurrentDrawingRoi={_currentDrawingRoi.Name} | Position={refPosition:F1}",
                            "RoiEditorControl");
                        _drawingLogCount++;
                    }

                    UpdateDrawingPreview(refPosition);
                }
            }
        }

        /// <summary>
        /// 处理鼠标按下（由Manager路由）
        /// </summary>
        /// <returns>true=事件已消费（区域操作），false=空白处点击（允许平移）</returns>
        public bool ProcessMouseDown(Point position)
        {
            // 逆变换到参考坐标系（内部状态始终使用参考坐标；矩阵按当前形状有向性解析）
            Point refPosition = ToReferencePoint(position,
                _selectedRoi?.LocalGeometry is RoiShape selShape ? selShape.Geometry : null);

            // 降频：每 20 次鼠标按下记录一次，避免交互路径刷日志
            if (_mouseDownLogCounter++ % 20 == 0)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[ProcessMouseDown] MouseDown事件 | InstanceId={_controlInstanceId} | IsDrawing={_viewModel.IsDrawing} | _isDrawing={_isDrawing} | Tool={_currentTool} | Position={position:F1} | RefPosition={refPosition:F1} | Matrix={(_positionCorrectionMatrix != null ? "set" : "null")} | OverlayCanvas={OverlayCanvas != null}",
                    "RoiEditorControl");
            }

            if (OverlayCanvas == null) return false;

            if (_currentTool == ShapeType.Polygon)
            {
                // ★ 双击完成检测（时间阈值 ≤ 系统双击时间）
                if (_isDrawingPolygon && _polygonPoints.Count >= 3)
                {
                    var elapsed = (DateTime.Now - _lastPolygonClickTime).TotalMilliseconds;
                    if (elapsed < DoubleClickTimeMs)
                    {
                        _isDrawingPolygon = false;
                        ClearRubberBand();
                        FinishInteraction();
                        return true;
                    }
                }
                _lastPolygonClickTime = DateTime.Now;

                if (!_isDrawingPolygon)
                {
                    _isDrawingPolygon = true;
                    _isDrawing = true;
                    _polygonPoints.Clear();
                    _currentDrawingRoi = CreateDrawingRoi(_currentTool);
                }

            _polygonPoints.Add(refPosition);
            SyncPolygonToShapeDef();
            ClearRubberBand();
            RenderRequested?.Invoke();
            _viewModel.StatusMessage = $"多边形顶点: {_polygonPoints.Count}";
            return true;
            }

            if (_viewModel.IsDrawing && _currentTool != ShapeType.Point)
            {
                if (_isDrawing)
                {
                    VisionLogger.Instance.Log(LogLevel.Warning,
                        $"[ProcessMouseDown] 警告：已在绘制中，忽略重复触发 | InstanceId={_controlInstanceId}",
                        "RoiEditorControl");
                    return true;
                }

                _isDrawing = true;
                // 新建形状起点：按工具类型有向性选择修正矩阵（Rectangle/Circle 无向 → 剥转）
                refPosition = ToReferencePoint(position, TemplateGeometryForTool(_currentTool));
                _startPoint = refPosition;

                _currentDrawingRoi = CreateDrawingRoi(_currentTool);

                if (_currentDrawingRoi.LocalGeometry is RoiShape shapeDef)
                {
                    switch (_currentTool)
                    {
                        case ShapeType.Rectangle:
                            shapeDef.Geometry = new System.Windows.Rect(refPosition.X, refPosition.Y, 0, 0);
                            break;
                        case ShapeType.RotatedRectangle:
                            shapeDef.Geometry = new RotatedRect2d(refPosition.X, refPosition.Y, 0, 0, 0);
                            break;
                        case ShapeType.Circle:
                            shapeDef.Geometry = new Circle2d(refPosition.X, refPosition.Y, 0);
                            break;
                        case ShapeType.Line:
                            shapeDef.Geometry = Line2d.FromTwoPoints(
                                new System.Windows.Point(refPosition.X, refPosition.Y),
                                new System.Windows.Point(refPosition.X, refPosition.Y));
                            break;
                    }
                }
                return true;
            }

            if (_selectedRoi != null)
            {
                if (_currentHandles != null)
                {
                    // ★ 复用 HandleRenderer.HitTestHandleDetail：一次命中即拿到 Type + VertexIndex，删除内联距离循环
                    var hitHandle = Rendering.HandleRenderer.HitTestHandleDetail(refPosition, _currentHandles);
                    if (hitHandle != null)
                    {
                        var hitHandleType = hitHandle.Type;

                        if (hitHandleType == Rendering.HandleType.Center)
                        {
                            _isDragging = true;
                            _dragStartPoint = refPosition;
                            _viewModel.StatusMessage = "正在拖动区域";
                            return true;
                        }

                        if (hitHandleType == Rendering.HandleType.PolygonVertex)
                        {
                            _isDraggingHandle = true;
                            _activeHandleType = hitHandleType;
                            _handleDragStartPoint = refPosition;
                            _activeVertexIndex = hitHandle.VertexIndex;
                            if (_selectedRoi?.LocalGeometry is RoiShape polyShape)
                                _originalShapeDefinition = polyShape.Clone() as RoiShape;
                            _viewModel.StatusMessage = "正在拖拽多边形顶点";
                            return true;
                        }

                        _isDraggingHandle = true;
                        _activeHandleType = hitHandleType;
                        _handleDragStartPoint = refPosition;

                        if (_selectedRoi.LocalGeometry is RoiShape shapeDef)
                        {
                            _originalShapeDefinition = shapeDef.Clone() as RoiShape;
                            var ctr = shapeDef.GetCenter();
                            _originalPosition = new System.Windows.Point(ctr.X, ctr.Y);
                            _originalSize = shapeDef.Geometry switch
                            {
                                Circle2d c => new Size(c.Radius * 2, c.Radius * 2),
                                RotatedRect2d rr => new Size(rr.Width, rr.Height),
                                System.Windows.Rect r => new Size(r.Width, r.Height),
                                _ => new Size(100, 100),
                            };
                            _originalRotation = shapeDef.Geometry switch
                            {
                                RotatedRect2d rr => rr.Angle,
                                _ => 0,
                            };

                            if (hitHandleType == Rendering.HandleType.Rotate)
                            {
                                _rotateStartAngle = _originalRotation;
                                var center = new Point(
                                    shapeDef.Geometry switch
                                    {
                                        RotatedRect2d rr => rr.CenterX,
                                        _ => 0,
                                    },
                                    shapeDef.Geometry switch
                                    {
                                        RotatedRect2d rr => rr.CenterY,
                                        _ => 0,
                                    });
                                _rotateStartMouseAngle = CalculateMouseAngle(center, refPosition);
                            }
                        }

                        _viewModel.StatusMessage = $"正在拖动 {hitHandleType} 手柄";
                        return true;
                    }
                }
            }

            // 传入原始画布坐标(position)，HitTestRoi 内部会自行做反向变换
            var hitRoi = HitTestRoi(position);
            if (hitRoi != null)
            {
                SelectRoi(hitRoi);
                _isDragging = true;
                _dragStartPoint = refPosition;
                return true;  // ★ 命中区域，事件已消费
            }
            else
            {
                DeselectAll();
                return false;  // ★ 空白处，允许平移
            }
        }





        /// <summary>
        /// 处理鼠标松开（由Manager路由）
        /// </summary>
        public void ProcessMouseUp()
        {
            if (!_isDrawing && !_isDragging && !_isDraggingHandle)
                return;

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[ProcessMouseUp] MouseUp事件 | InstanceId={_controlInstanceId} | _isDrawing={_isDrawing} | _isDragging={_isDragging} | _isDraggingHandle={_isDraggingHandle}",
                "RoiEditorControl");

            FinishInteraction();
        }

        /// <summary>
        /// 处理鼠标右键（多边形撤销顶点）
        /// </summary>
        public void ProcessRightButtonDown()
        {
            if (!_isDrawingPolygon || _currentTool != ShapeType.Polygon
                || _polygonPoints.Count <= 0)
                return;

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[ProcessRightButtonDown] 右键触发多边形撤销 | InstanceId={_controlInstanceId} | 顶点数={_polygonPoints.Count}",
                "RoiEditorControl");

            _polygonPoints.RemoveAt(_polygonPoints.Count - 1);
            SyncPolygonToShapeDef();
            if (_polygonPoints.Count == 0)
            {
                _isDrawingPolygon = false;
                if (_currentDrawingRoi != null)
                    ReleaseRoiIndex(_currentDrawingRoi.Name);
                _currentDrawingRoi = null;
            }
            ClearRubberBand();
            if (_polygonPoints.Count >= 1 && _currentDrawingRoi != null)
            {
                if (_currentDrawingRoi.LocalGeometry is RoiShape shapeDef)
                {
                    RenderRequested?.Invoke();
                }
            }
            else if (_polygonPoints.Count == 0)
                RenderRequested?.Invoke();
            _viewModel.StatusMessage = _polygonPoints.Count > 0
                ? $"多边形顶点: {_polygonPoints.Count}"
                : "已取消多边形绘制";
        }

        /// <summary>
        /// 处理视口变换（由Manager调用）
        /// </summary>
        public void ProcessViewTransformed(double zoom)
        {
            CurrentZoom = zoom;
        }

        public void FinishInteraction()
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[FinishInteraction] 调用 FinishInteraction | InstanceId={_controlInstanceId} | _isDrawing={_isDrawing} | _currentDrawingRoi={_currentDrawingRoi?.Name ?? "null"} | _isDrawingPolygon={_isDrawingPolygon}",
                "RoiEditorControl");

            // ★ 多边形仍在绘制中（未完成），不清理状态
            if (_isDrawingPolygon)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[FinishInteraction] 多边形仍在绘制中，跳过清理 | InstanceId={_controlInstanceId} | 顶点数={_polygonPoints.Count}",
                    "RoiEditorControl");

                // 多边形非最终完成时，清理橡皮筋线但不重置 _isDrawing/_currentDrawingRoi
                ClearRubberBand();
                RenderRequested?.Invoke();
                return;
            }

            if (_isDrawing && _currentDrawingRoi != null)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[FinishInteraction] 完成绘制 | InstanceId={_controlInstanceId} | Roi={_currentDrawingRoi.Name} | _nextRoiIndex={_nextRoiIndex}",
                    "RoiEditorControl");

                // 完成绘制：当前元素即正式元素（角色/预览由编辑器与渲染上下文表达，不写回模型）

                var shapeDef = _currentDrawingRoi.LocalGeometry as RoiShape;
                if (shapeDef != null)
                {
                    bool isValid = _currentTool switch
                    {
                        ShapeType.Rectangle or ShapeType.RotatedRectangle =>
                            shapeDef.Geometry switch
                            {
                                RotatedRect2d rr => rr.Width >= 10 && rr.Height >= 10 && rr.Width * rr.Height >= 100,
                                System.Windows.Rect r => r.Width >= 10 && r.Height >= 10 && r.Width * r.Height >= 100,
                                _ => false,
                            },
                        ShapeType.Circle =>
                            shapeDef.Geometry is Circle2d c && c.Radius >= 5.64 &&
                            Math.PI * c.Radius * c.Radius >= 100,
                        ShapeType.Line => shapeDef.Geometry is Line2d line
                            && (line.GetPointAt(100) - line.Point).Length >= 10,
                        ShapeType.Polygon => shapeDef.Geometry is Polygon2d poly && poly.Vertices.Length >= 3,
                        _ => false
                    };

                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[FinishInteraction] 验证几何输入尺寸 | InstanceId={_controlInstanceId} | Roi={_currentDrawingRoi.Name} | IsValid={isValid}",
                        "RoiEditorControl");

                    if (isValid)
                    {
                        _viewModel.Rois.Add(_currentDrawingRoi);
                        _viewModel.SelectedRoi = _currentDrawingRoi;
                        _viewModel.StatusMessage = $"已创建 {_currentTool} 几何输入";
                        RoiDataChanged?.Invoke(this, _currentDrawingRoi);

                        VisionLogger.Instance.Log(LogLevel.Success,
                            $"[FinishInteraction] 几何输入已添加到集合 | InstanceId={_controlInstanceId} | Roi={_currentDrawingRoi.Name}(Id={_currentDrawingRoi.Id:B}) | TotalRois={_viewModel.Rois.Count}",
                            "RoiEditorControl");
                    }
                    else
                    {
                        _viewModel.StatusMessage = "几何输入太小，已忽略";

                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"[FinishInteraction] 几何输入太小，已忽略 | InstanceId={_controlInstanceId} | Roi={_currentDrawingRoi.Name}",
                            "RoiEditorControl");
                    }
                }
            }

            ClearRubberBand();
            _isDrawing = false;
            _isDragging = false;
            _isDraggingHandle = false;
            _currentDrawingRoi = null;

            // 清理ROI编辑器风格的手柄状态
            _activeHandleType = Rendering.HandleType.None;
            _originalShapeDefinition = null;
            _rotateStartAngle = null;
            _rotateStartMouseAngle = null;

            // 绘制完成自动回到选择模式
            EnterSelectMode(notifyManager: false);

            RenderRequested?.Invoke();

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[FinishInteraction] 清理完成 | InstanceId={_controlInstanceId} | _isDrawing={_isDrawing} | _currentDrawingRoi={_currentDrawingRoi?.Name ?? "null"}",
                "RoiEditorControl");
        }

        #endregion

        #region 绘制和渲染

        private void UpdateDrawingPreview(Point currentPosition)
        {
            if (_currentDrawingRoi?.LocalGeometry is not RoiShape shapeDef || shapeDef.Geometry == null)
                return;

            // Log preview update (every 10 times)
            if (_previewLogCount++ % 10 == 0)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[UpdateDrawingPreview] Count={_previewLogCount} | Position={currentPosition:F1} | Tool={_currentTool}",
                    "RoiEditorControl");
            }

            var dx = currentPosition.X - _startPoint.X;
            var dy = currentPosition.Y - _startPoint.Y;

            switch (_currentTool)
            {
                case ShapeType.Rectangle:
                    shapeDef.Geometry = new System.Windows.Rect(
                        Math.Min(_startPoint.X, currentPosition.X),
                        Math.Min(_startPoint.Y, currentPosition.Y),
                        Math.Abs(dx), Math.Abs(dy));
                    break;

                case ShapeType.RotatedRectangle:
                    shapeDef.Geometry = new RotatedRect2d(
                        _startPoint.X + dx / 2, _startPoint.Y + dy / 2,
                        Math.Abs(dx), Math.Abs(dy), 0);
                    break;

                case ShapeType.Circle:
                    var radius = Math.Sqrt(dx * dx + dy * dy);
                    shapeDef.Geometry = new Circle2d(_startPoint.X, _startPoint.Y, radius);
                    break;

                case ShapeType.Line:
                    shapeDef.Geometry = Line2d.FromTwoPoints(
                        new System.Windows.Point(_startPoint.X, _startPoint.Y),
                        new System.Windows.Point(currentPosition.X, currentPosition.Y));
                    break;
            }
        }

        private void MoveRoi(Roi roi, Vector offset)
        {
            if (roi.LocalGeometry is RoiShape shapeDef && shapeDef.Geometry != null)
            {
                shapeDef.Geometry = shapeDef.Geometry switch
                {
                    Circle2d c => new Circle2d(c.Center.X + offset.X, c.Center.Y + offset.Y, c.Radius),
                    RotatedRect2d rr => new RotatedRect2d(rr.CenterX + offset.X, rr.CenterY + offset.Y, rr.Width, rr.Height, rr.Angle),
                    Line2d l => new Line2d(
                        new System.Windows.Point(l.Point.X + offset.X, l.Point.Y + offset.Y), l.Direction),
                    System.Windows.Rect r => new System.Windows.Rect(r.X + offset.X, r.Y + offset.Y, r.Width, r.Height),
                    Polygon2d poly when poly.Vertices.Length > 0 => new Polygon2d(
                        poly.Vertices.Select(v => new System.Windows.Point(v.X + offset.X, v.Y + offset.Y)).ToArray(),
                        poly.IsClosed),
                    _ => shapeDef.Geometry,
                };

                RoiDataChanged?.Invoke(this, roi);
            }
        }

        #region 多边形辅助方法

        /// <summary>
        /// 将 _polygonPoints 同步到 RoiShape.Points
        /// </summary>
        private void SyncPolygonToShapeDef()
        {
            if (_currentDrawingRoi?.LocalGeometry is RoiShape shapeDef)
            {
                shapeDef.Geometry = new Polygon2d(
                    _polygonPoints.Select(p => new System.Windows.Point(p.X, p.Y)).ToArray());
            }
        }

        /// <summary>
        /// 绘制多边形橡皮筋线（从最后一个顶点到鼠标位置）
        /// </summary>
        private void DrawPolygonRubberBand(Point mousePosition)
        {
            var canvas = OverlayCanvas;
            if (canvas == null || _polygonPoints.Count == 0) return;

            ClearRubberBand();

            var zoom = CurrentZoom;
            var lastPoint = _polygonPoints[_polygonPoints.Count - 1];

            // 正向变换多边形最后一个顶点到测试坐标（OverlayCanvas 在测试坐标系中）
            Point displayLastPoint = _positionCorrectionMatrix != null
                ? _positionCorrectionMatrix.TransformWpfPoint(lastPoint)
                : lastPoint;

            // 实线：最后一个顶点 -> 鼠标位置
            var line = new System.Windows.Shapes.Line
            {
                X1 = displayLastPoint.X, Y1 = displayLastPoint.Y,
                X2 = mousePosition.X, Y2 = mousePosition.Y,
                Stroke = ColorConstants.InspectionBlueBrush,
                StrokeThickness = ViewportScaling.LineWidth(zoom),
                Tag = RubberBandTag
            };
            Canvas.SetZIndex(line, 50);
            canvas.Children.Add(line);

            // 小圆点：鼠标位置
            var dotSize = ViewportScaling.InvScale(zoom, 6.0);
            var dot = new Ellipse
            {
                Width = dotSize, Height = dotSize,
                Fill = ColorConstants.InspectionBlueBrush, Tag = RubberBandTag
            };
            Canvas.SetLeft(dot, mousePosition.X - dotSize / 2);
            Canvas.SetTop(dot, mousePosition.Y - dotSize / 2);
            Canvas.SetZIndex(dot, 50);
            canvas.Children.Add(dot);
        }

        /// <summary>
        /// 清除多边形橡皮筋线
        /// </summary>
        private void ClearRubberBand()
        {
            var canvas = OverlayCanvas;
            if (canvas == null) return;
            var toRemove = canvas.Children
                .OfType<FrameworkElement>()
                .Where(e => e.Tag as string == RubberBandTag)
                .ToList();
            foreach (var item in toRemove)
                canvas.Children.Remove(item);
        }

        #endregion

        /// <summary>
        /// 根据手柄类型更新形状
        /// </summary>
        private void UpdateShapeByHandle(Roi roi, Rendering.HandleType handleType, double dx, double dy)
        {
            if (roi.LocalGeometry is not RoiShape shapeDef)
                return;

            if (shapeDef.Geometry == null) return;

            if (shapeDef.Geometry is Circle2d)
                UpdateCircleByHandle(shapeDef, handleType, dx, dy);
            else if (shapeDef.Geometry is Line2d)
                UpdateLineByHandle(shapeDef, handleType, dx, dy);
            // Rectangle/RotatedRectangle 通过 HandleShapeEdit 中的 ResizeRectangle* 方法处理
            // Polygon 通过顶点手柄直接更新

            RoiDataChanged?.Invoke(this, roi);
        }

        /// <summary>
        /// 更新矩形形状（通过手柄，使用原始状态避免累积误差）
        /// </summary>
        private void UpdateRectangleByHandle(RoiShape shapeDef, Rendering.HandleType handleType, double dx, double dy)
        {
            // 此方法已弃用，现在使用ResizeRectangleFromCorner和ResizeRectangleFromEdge方法
            // 这些方法使用原始状态保存机制，避免累积误差
        }

        /// <summary>
        /// 将角度规范化到[-180°, 180°]范围
        /// </summary>
        private static double NormalizeAngle(double angle)
        {
            angle = angle % 360;
            if (angle < 0) angle += 360;
            if (angle > 180) angle -= 360;
            return angle;
        }

        /// <summary>
        /// 更新圆形形状（通过手柄）
        /// </summary>
        private void UpdateCircleByHandle(RoiShape shapeDef, Rendering.HandleType handleType, double dx, double dy)
        {
            if (shapeDef.Geometry is not Circle2d c) return;

            switch (handleType)
            {
                case Rendering.HandleType.Center:
                    shapeDef.Geometry = new Circle2d(c.Center.X + dx, c.Center.Y + dy, c.Radius);
                    break;
                default:
                    double newRadius = c.Radius;
                    switch (handleType)
                    {
                        case Rendering.HandleType.Top:    newRadius += dy; break;
                        case Rendering.HandleType.Bottom: newRadius += dy; break;
                        case Rendering.HandleType.Left:   newRadius += dx; break;
                        case Rendering.HandleType.Right:  newRadius += dx; break;
                    }
                    if (newRadius < 5) newRadius = 5;
                    shapeDef.Geometry = new Circle2d(c.Center.X, c.Center.Y, newRadius);
                    break;
            }
        }

        private void UpdateLineByHandle(RoiShape shapeDef, Rendering.HandleType handleType, double dx, double dy)
        {
            if (shapeDef.Geometry is not Line2d l) return;

            var startPt = new System.Windows.Point(l.Point.X, l.Point.Y);
            var endPt = l.GetPointAt(100);

            switch (handleType)
            {
                case Rendering.HandleType.LineStart:
                    startPt = new System.Windows.Point(startPt.X + dx, startPt.Y + dy);
                    break;
                case Rendering.HandleType.LineEnd:
                    endPt = new System.Windows.Point(endPt.X + dx, endPt.Y + dy);
                    break;
            }

            var dir = endPt - startPt;
            if (dir.LengthSquared > 1e-20)
                shapeDef.Geometry = new Line2d(startPt, dir);
        }

        /// <summary>
        /// ROI编辑器风格的手柄编辑处理
        /// 参考ROI编辑器的编辑操作逻辑（ROIImageEditor.xaml.cs 1461-1816行）
        /// </summary>
        private void HandleShapeEdit(RoiShape shape, Point currentPosition)
        {
            var delta = currentPosition - _handleDragStartPoint;

            // 多边形顶点拖拽
            if (shape.Geometry is Polygon2d poly && _activeHandleType == Rendering.HandleType.PolygonVertex)
            {
                if (_activeVertexIndex >= 0 && _activeVertexIndex < poly.Vertices.Length)
                {
                    var verts = poly.Vertices.ToArray();
                    verts[_activeVertexIndex] = new System.Windows.Point(currentPosition.X, currentPosition.Y);
                    shape.Geometry = new Polygon2d(verts, poly.IsClosed);
                }
                return;
            }

            // 直线端点调整
            if (shape.Geometry is Line2d)
            {
                HandleLineEndpoint(shape, currentPosition);
                return;
            }

            // 圆形半径调整
            if (shape.Geometry is Circle2d)
            {
                HandleCircleResize(shape, currentPosition);
                return;
            }

            // 旋转矩形调整
            if (shape.Geometry is RotatedRect2d)
            {
                if (_activeHandleType == Rendering.HandleType.Rotate)
                {
                    HandleRotate(shape, currentPosition);
                }
                else
                {
                    HandleRotatedRectangleResize(shape, currentPosition, delta);
                }
                return;
            }

            // 普通矩形（System.Windows.Rect）调整
            if (shape.Geometry is System.Windows.Rect)
            {
                switch (_activeHandleType)
                {
                    case Rendering.HandleType.TopLeft:
                        ResizeRectangleFromCorner(shape, delta, true, true);
                        break;
                    case Rendering.HandleType.TopRight:
                        ResizeRectangleFromCorner(shape, delta, false, true);
                        break;
                    case Rendering.HandleType.BottomLeft:
                        ResizeRectangleFromCorner(shape, delta, true, false);
                        break;
                    case Rendering.HandleType.BottomRight:
                        ResizeRectangleFromCorner(shape, delta, false, false);
                        break;
                    case Rendering.HandleType.Top:
                        ResizeRectangleFromEdge(shape, delta, true, true);
                        break;
                    case Rendering.HandleType.Bottom:
                        ResizeRectangleFromEdge(shape, delta, true, false);
                        break;
                    case Rendering.HandleType.Left:
                        ResizeRectangleFromEdge(shape, delta, false, true);
                        break;
                    case Rendering.HandleType.Right:
                        ResizeRectangleFromEdge(shape, delta, false, false);
                        break;
                }
            }

            // 触发变更事件（几何已就地修改，无需脏标记）
            if (_selectedRoi != null)
            {
                RoiDataChanged?.Invoke(this, _selectedRoi);
            }
        }

        /// <summary>
        /// 处理旋转操作（参考ROI编辑器HandleRotate 1792-1816行）
        /// </summary>
        private void HandleRotate(RoiShape shape, Point currentPosition)
        {
            if (shape.Geometry is not RotatedRect2d rr) return;
            var center = new Point(rr.CenterX, rr.CenterY);

            if (_rotateStartAngle == null)
            {
                _rotateStartAngle = rr.Angle;
                _rotateStartMouseAngle = CalculateMouseAngle(center, _handleDragStartPoint);
            }

            var currentMouseAngle = CalculateMouseAngle(center, currentPosition);
            var deltaAngle = currentMouseAngle - _rotateStartMouseAngle.Value;
            var newAngle = RotatedRectangleHelper.NormalizeAngle(_rotateStartAngle.Value + deltaAngle);
            shape.Geometry = new RotatedRect2d(rr.CenterX, rr.CenterY, rr.Width, rr.Height, newAngle);
        }

        /// <summary>
        /// 计算鼠标相对于中心的图像坐标系角度
        /// </summary>
        private double CalculateMouseAngle(Point center, Point mousePos)
        {
            // 计算鼠标相对于中心的数学角度
            // Math.Atan2返回数学角度：逆时针为正，0°向右
            var mathAngle = Math.Atan2(mousePos.Y - center.Y, mousePos.X - center.X) * 180 / Math.PI;

            // Math.Atan2 在屏幕坐标系（Y向下）中天然返回顺时针为正的角度
            // 直接返回即可，无需取负
            return mathAngle;
        }

        /// <summary>
        /// 处理旋转矩形大小调整（使用坐标变换，参考ROI编辑器）
        /// </summary>
        private void HandleRotatedRectangleResize(RoiShape shape, Point currentPosition, Vector delta)
        {
            if (_originalShapeDefinition == null) return;

            var localDelta = WorldToLocalDelta(delta, _originalRotation);
            var corners = GetLocalCorners(_originalSize.Width, _originalSize.Height);
            var newCorners = CalculateNewCorners(corners, localDelta, _activeHandleType);
            var (newWidth, newHeight, newCenterLocal) = CalculateFromCorners(newCorners);
            var newCenterWorld = LocalToWorldPoint(newCenterLocal, _originalPosition, _originalRotation);

            shape.Geometry = new RotatedRect2d(newCenterWorld.X, newCenterWorld.Y, newWidth, newHeight, _originalRotation);
        }

        /// <summary>
        /// 将世界坐标系的 delta 转换为局部坐标系的 delta（参考ROI编辑器）
        /// </summary>
        private Vector WorldToLocalDelta(Vector worldDelta, double rotation)
        {
            // 将角度转换为弧度
            var angle = rotation * Math.PI / 180;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);

            // 使用逆旋转矩阵：local = transpose(R) * world
            return new Vector(
                worldDelta.X * cos + worldDelta.Y * sin,
                -worldDelta.X * sin + worldDelta.Y * cos);
        }

        /// <summary>
        /// 将局部坐标系的点转换为世界坐标系的点（参考ROI编辑器）
        /// </summary>
        private Point LocalToWorldPoint(Point localPoint, Point center, double rotation)
        {
            // 将角度转换为弧度
            var angle = rotation * Math.PI / 180;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);

            return new Point(
                center.X + localPoint.X * cos - localPoint.Y * sin,
                center.Y + localPoint.X * sin + localPoint.Y * cos);
        }

        /// <summary>
        /// 获取矩形四个角点（局部坐标，原点在中心）
        /// </summary>
        private Point[] GetLocalCorners(double width, double height)
        {
            var hw = width / 2;
            var hh = height / 2;
            return new Point[]
            {
                new Point(-hw, -hh), // TopLeft
                new Point( hw, -hh), // TopRight
                new Point( hw,  hh), // BottomRight
                new Point(-hw,  hh)  // BottomLeft
            };
        }

        /// <summary>
        /// 根据拖动的手柄计算新的角点位置（固定对角锚点模式）
        /// </summary>
        private Point[] CalculateNewCorners(Point[] originalCorners, Vector delta, Rendering.HandleType handle)
        {
            var newCorners = (Point[])originalCorners.Clone();

            switch (handle)
            {
                case Rendering.HandleType.TopLeft:
                    // TopLeft 移动，BottomRight 固定
                    newCorners[0] = new Point(
                        originalCorners[0].X + delta.X,
                        originalCorners[0].Y + delta.Y);
                    // TopRight 和 BottomLeft 跟随变化（保持矩形形状）
                    newCorners[1] = new Point(originalCorners[1].X, newCorners[0].Y);
                    newCorners[3] = new Point(newCorners[0].X, originalCorners[3].Y);
                    break;

                case Rendering.HandleType.TopRight:
                    // TopRight 移动，BottomLeft 固定
                    newCorners[1] = new Point(
                        originalCorners[1].X + delta.X,
                        originalCorners[1].Y + delta.Y);
                    newCorners[0] = new Point(originalCorners[0].X, newCorners[1].Y);
                    newCorners[2] = new Point(newCorners[1].X, originalCorners[2].Y);
                    break;

                case Rendering.HandleType.BottomRight:
                    // BottomRight 移动，TopLeft 固定
                    newCorners[2] = new Point(
                        originalCorners[2].X + delta.X,
                        originalCorners[2].Y + delta.Y);
                    newCorners[1] = new Point(newCorners[2].X, originalCorners[1].Y);
                    newCorners[3] = new Point(originalCorners[3].X, newCorners[2].Y);
                    break;

                case Rendering.HandleType.BottomLeft:
                    // BottomLeft 移动，TopRight 固定
                    newCorners[3] = new Point(
                        originalCorners[3].X + delta.X,
                        originalCorners[3].Y + delta.Y);
                    newCorners[0] = new Point(newCorners[3].X, originalCorners[0].Y);
                    newCorners[2] = new Point(originalCorners[2].X, newCorners[3].Y);
                    break;

                case Rendering.HandleType.Top:
                    // 只改变高度，固定 Bottom 边
                    var newTop = originalCorners[0].Y + delta.Y;
                    newCorners[0] = new Point(originalCorners[0].X, newTop);
                    newCorners[1] = new Point(originalCorners[1].X, newTop);
                    break;

                case Rendering.HandleType.Bottom:
                    // 只改变高度，固定 Top 边
                    var newBottom = originalCorners[2].Y + delta.Y;
                    newCorners[2] = new Point(originalCorners[2].X, newBottom);
                    newCorners[3] = new Point(originalCorners[3].X, newBottom);
                    break;

                case Rendering.HandleType.Left:
                    // 只改变宽度，固定 Right 边
                    var newLeft = originalCorners[0].X + delta.X;
                    newCorners[0] = new Point(newLeft, originalCorners[0].Y);
                    newCorners[3] = new Point(newLeft, originalCorners[3].Y);
                    break;

                case Rendering.HandleType.Right:
                    // 只改变宽度，固定 Left 边
                    var newRight = originalCorners[1].X + delta.X;
                    newCorners[1] = new Point(newRight, originalCorners[1].Y);
                    newCorners[2] = new Point(newRight, originalCorners[2].Y);
                    break;
            }

            return newCorners;
        }

        /// <summary>
        /// 从四个角点计算宽度、高度和中心（局部坐标）
        /// </summary>
        private (double width, double height, Point center) CalculateFromCorners(Point[] corners)
        {
            var width = corners[1].X - corners[0].X;
            var height = corners[2].Y - corners[0].Y;

            // 确保最小尺寸，取绝对值
            width = Math.Max(10, Math.Abs(width));
            height = Math.Max(10, Math.Abs(height));

            var center = new Point(
                (corners[0].X + corners[2].X) / 2,
                (corners[0].Y + corners[2].Y) / 2);

            return (width, height, center);
        }

        /// <summary>
        /// 处理圆形半径调整（参考ROI编辑器HandleCircleResize 1473-1477行）
        /// </summary>
        private void HandleCircleResize(RoiShape shape, Point currentPosition)
        {
            if (shape.Geometry is not Circle2d c) return;
            var center = c.Center;

            var dx = currentPosition.X - center.X;
            var dy = currentPosition.Y - center.Y;
            var newRadius = Math.Sqrt(dx * dx + dy * dy);
            if (newRadius < 5) newRadius = 5;

            shape.Geometry = new Circle2d(center.X, center.Y, newRadius);
        }

        private void HandleLineEndpoint(RoiShape shape, Point currentPosition)
        {
            if (shape.Geometry is not Line2d l) return;
            var startPt = l.Point;
            var endPt = l.GetPointAt(100);

            if (_activeHandleType == Rendering.HandleType.LineStart)
                startPt = new System.Windows.Point(currentPosition.X, currentPosition.Y);
            else if (_activeHandleType == Rendering.HandleType.LineEnd)
                endPt = new System.Windows.Point(currentPosition.X, currentPosition.Y);

            var dir = endPt - startPt;
            if (dir.LengthSquared > 1e-20)
                shape.Geometry = new Line2d(startPt, dir);
        }

        /// <summary>
        /// 从角落调整矩形（不对称编辑 - 固定对角点模式）
        /// 参考ROI编辑器旋转矩形的HandleRotatedRectangleResize逻辑
        /// </summary>
        private void ResizeRectangleFromCorner(RoiShape shape, Vector delta, bool isLeft, bool isTop)
        {
            if (_originalShapeDefinition == null) return;

            var original = _originalShapeDefinition;

            // 从原始 Geometry 获取尺寸
            var origW = original.Geometry switch
            {
                RotatedRect2d rr => rr.Width,
                System.Windows.Rect r => r.Width,
                _ => 100,
            };
            var origH = original.Geometry switch
            {
                RotatedRect2d rr => rr.Height,
                System.Windows.Rect r => r.Height,
                _ => 100,
            };

            var hw = origW / 2;
            var hh = origH / 2;
            var corners = new Point[]
            {
                new Point(-hw, -hh), // TopLeft
                new Point( hw, -hh), // TopRight
                new Point( hw,  hh), // BottomRight
                new Point(-hw,  hh)  // BottomLeft
            };

            // 根据拖动的角点计算新的角点位置（固定对角锚点模式）
            Point[] newCorners;

            // 确定拖动的手柄类型
            if (isLeft && isTop) // TopLeft
            {
                // TopLeft 移动，BottomRight 固定
                newCorners = new Point[]
                {
                    new Point(corners[0].X + delta.X, corners[0].Y + delta.Y),
                    new Point(corners[1].X, corners[0].Y + delta.Y),
                    new Point(corners[2].X, corners[2].Y),
                    new Point(corners[0].X + delta.X, corners[3].Y)
                };
            }
            else if (!isLeft && isTop) // TopRight
            {
                // TopRight 移动，BottomLeft 固定
                newCorners = new Point[]
                {
                    new Point(corners[0].X, corners[0].Y + delta.Y),
                    new Point(corners[1].X + delta.X, corners[1].Y + delta.Y),
                    new Point(corners[1].X + delta.X, corners[2].Y),
                    new Point(corners[3].X, corners[3].Y)
                };
            }
            else if (isLeft && !isTop) // BottomLeft
            {
                // BottomLeft 移动，TopRight 固定
                // TopLeft.X 跟随 BottomLeft.X，BottomRight.Y 跟随 BottomLeft.Y
                newCorners = new Point[]
                {
                    new Point(corners[0].X + delta.X, corners[0].Y),
                    new Point(corners[1].X, corners[1].Y),
                    new Point(corners[2].X, corners[2].Y + delta.Y),
                    new Point(corners[3].X + delta.X, corners[3].Y + delta.Y)
                };
            }
            else // BottomRight
            {
                // BottomRight 移动，TopLeft 固定
                newCorners = new Point[]
                {
                    new Point(corners[0].X, corners[0].Y),
                    new Point(corners[1].X + delta.X, corners[1].Y),
                    new Point(corners[2].X + delta.X, corners[2].Y + delta.Y),
                    new Point(corners[3].X, corners[3].Y + delta.Y)
                };
            }

            // 从新角点计算新的尺寸和中心
            var newWidth = Math.Max(10, newCorners[1].X - newCorners[0].X);
            var newHeight = Math.Max(10, newCorners[2].Y - newCorners[0].Y);
            var centerLocal = new Point(
                (newCorners[0].X + newCorners[2].X) / 2,
                (newCorners[0].Y + newCorners[2].Y) / 2);

            // 将局部坐标转换为世界坐标（左上角 = 中心 - 半宽/半高）
            shape.Geometry = new System.Windows.Rect(
                _originalPosition.X + centerLocal.X - newWidth / 2,
                _originalPosition.Y + centerLocal.Y - newHeight / 2,
                newWidth, newHeight);
        }

        /// <summary>
        /// 从边缘调整矩形（参考ROI编辑器ResizeFromEdge 1760-1790行）
        /// </summary>
        /// <summary>
        /// 从边缘调整矩形（非对称编辑 - 固定对边模式）
        /// 使用与 ResizeRectangleFromCorner 相同的角点计算逻辑
        /// </summary>
        private void ResizeRectangleFromEdge(RoiShape shape, Vector delta, bool isVertical, bool isTopOrLeft)
        {
            if (_originalShapeDefinition == null) return;

            var origW = _originalSize.Width;
            var origH = _originalSize.Height;

            var hw = origW / 2;
            var hh = origH / 2;
            var corners = new Point[]
            {
                new Point(-hw, -hh), // TopLeft
                new Point( hw, -hh), // TopRight
                new Point( hw,  hh), // BottomRight
                new Point(-hw,  hh)  // BottomLeft
            };

            // 根据拖动的边缘计算新的角点位置（固定对边模式）
            Point[] newCorners;

            if (isVertical)
            {
                if (isTopOrLeft) // Top 边：移动 TopLeft 和 TopRight，Bottom 边固定
                {
                    newCorners = new Point[]
                    {
                        new Point(corners[0].X, corners[0].Y + delta.Y), // TopLeft 移动
                        new Point(corners[1].X, corners[1].Y + delta.Y), // TopRight 移动
                        new Point(corners[2].X, corners[2].Y),           // BottomRight 固定
                        new Point(corners[3].X, corners[3].Y)            // BottomLeft 固定
                    };
                }
                else // Bottom 边：移动 BottomLeft 和 BottomRight，Top 边固定
                {
                    newCorners = new Point[]
                    {
                        new Point(corners[0].X, corners[0].Y),           // TopLeft 固定
                        new Point(corners[1].X, corners[1].Y),           // TopRight 固定
                        new Point(corners[2].X, corners[2].Y + delta.Y), // BottomRight 移动
                        new Point(corners[3].X, corners[3].Y + delta.Y)  // BottomLeft 移动
                    };
                }
            }
            else
            {
                if (isTopOrLeft) // Left 边：移动 TopLeft 和 BottomLeft，Right 边固定
                {
                    newCorners = new Point[]
                    {
                        new Point(corners[0].X + delta.X, corners[0].Y), // TopLeft 移动
                        new Point(corners[1].X, corners[1].Y),           // TopRight 固定
                        new Point(corners[2].X, corners[2].Y),           // BottomRight 固定
                        new Point(corners[3].X + delta.X, corners[3].Y)  // BottomLeft 移动
                    };
                }
                else // Right 边：移动 TopRight 和 BottomRight，Left 边固定
                {
                    newCorners = new Point[]
                    {
                        new Point(corners[0].X, corners[0].Y),           // TopLeft 固定
                        new Point(corners[1].X + delta.X, corners[1].Y), // TopRight 移动
                        new Point(corners[2].X + delta.X, corners[2].Y), // BottomRight 移动
                        new Point(corners[3].X, corners[3].Y)            // BottomLeft 固定
                    };
                }
            }

            // 从新角点计算新的尺寸和中心
            var newWidth = Math.Max(10, newCorners[1].X - newCorners[0].X);
            var newHeight = Math.Max(10, newCorners[2].Y - newCorners[0].Y);
            var centerLocal = new Point(
                (newCorners[0].X + newCorners[2].X) / 2,
                (newCorners[0].Y + newCorners[2].Y) / 2);

            // 将局部坐标转换为世界坐标（左上角 = 中心 - 半宽/半高）
            shape.Geometry = new System.Windows.Rect(
                _originalPosition.X + centerLocal.X - newWidth / 2,
                _originalPosition.Y + centerLocal.Y - newHeight / 2,
                newWidth, newHeight);
        }

        /// <summary>
        /// 旧渲染入口（已弃用，由Manager统一管理）
        /// </summary>
        private void UpdateRegionOverlay()
        {
            RenderRequested?.Invoke();
        }

        /// <summary>
        /// 获取所有区域的渲染上下文（供Manager使用）
        /// </summary>
        public List<RoiRenderContext> GetRenderContexts()
        {
            // 订阅模式下不叠加显示本地绘制几何（区域来源由订阅数据源决定；切回绘制模式恢复）
            if (_viewModel?.CurrentMode == RoiSourceMode.Subscribe)
                return new List<RoiRenderContext>();

            var rois = _viewModel?.Rois;
            if (rois == null || rois.Count == 0)
                return new List<RoiRenderContext>();

            var contexts = new List<RoiRenderContext>(rois.Count);
            foreach (var roi in rois)
            {
                if (!roi.IsVisible) continue;
                if (roi.LocalGeometry is not RoiShape shapeDef) continue;
                // 集合元素均为正式几何输入，非预览；预览仅存在于绘制中的 _currentDrawingRoi（走 GetPreviewContext）
                contexts.Add(CreateRenderContextInternal(roi, shapeDef, isPreview: false));
            }
            return contexts;
        }

        /// <summary>
        /// 获取当前绘制预览的渲染上下文（供Manager使用）
        /// </summary>
        public RoiRenderContext? GetPreviewContext()
        {
            if (_isDrawing && _currentDrawingRoi?.LocalGeometry is RoiShape previewShape)
            {
                var context = CreateRenderContextInternal(_currentDrawingRoi, previewShape, isPreview: true);

                // ★ 多边形预览：包含临时鼠标位置，让用户看到"点击后会是什么样的"
                if (_currentTool == ShapeType.Polygon && _isDrawingPolygon && _polygonPoints.Count >= 1)
                {
                    Point displayMousePos = _positionCorrectionMatrix != null
                        ? _positionCorrectionMatrix.TransformWpfPoint(_lastMouseImagePosition)
                        : new Point(_lastMouseImagePosition.X, _lastMouseImagePosition.Y);
                    context.PolygonPoints.Add(displayMousePos);
                    context.CalculateBounds();
                }

                return context;
            }
            return null;
        }

        /// <summary>
        /// 是否有交互操作正在进行中
        /// </summary>
        public bool IsAnyInteractionInProgress => _isDrawing || _isDragging || _isDraggingHandle;

        /// <summary>
        /// ★ 是否正在绘制新形状（拖动/编辑手柄时不视为"绘制"）
        /// Manager 用于判断是否显示编辑手柄（拖动手柄时保持可见）
        /// </summary>
        public bool IsDrawingInteraction => _isDrawing;

        /// <summary>
        /// 创建渲染上下文
        /// </summary>
        private RoiRenderContext CreateRenderContextInternal(Roi roi, RoiShape shape, bool isPreview = false)
        {
            LogCorrectionStatus($"CreateRenderContext(Roi={roi.Name})");
            var context = new RoiRenderContext
            {
                Id = roi.Id,
                ShapeType = shape.ShapeType,
                IsSelected = roi == _selectedRoi,
                IsPreview = isPreview,
                IsVisible = true,
                // 检测/屏蔽角色由编辑器实例承载（DefaultRegionType DP），不落模型元素
                RoiType = DefaultRegionType,

                // ★ 颜色语义回归修复：正式区域与预览分支对齐，按编辑器角色（DefaultRegionType）上色，
                //   不再依赖 RoiShape.FillColorArgb/StrokeColorArgb 字段（旧默认值 0xFF0000FF 会把屏蔽区画成蓝色）。
                FillColor = DefaultRegionType == RoiType.Mask
                    ? ColorConstants.MaskFill
                    : ColorConstants.InspectionFill,
                StrokeColor = DefaultRegionType == RoiType.Mask
                    ? ColorConstants.MaskOrange
                    : ColorConstants.InspectionBlue,
                StrokeThickness = shape.StrokeThickness,
                Opacity = shape.Opacity,

                Label = DefaultRegionType == RoiType.Mask
                    ? $"■ {roi.Name}"
                    : $"□ {roi.Name}"
            };

            // 从 Geometry 提取几何参数（按形状有向性选择修正矩阵：无向剥转 / 有向完整）
            var resolvedMatrix = CorrectionFor(shape.Geometry);
            if (shape.Geometry != null)
            {
                Point TransformPoint(double x, double y) =>
                    resolvedMatrix != null
                        ? resolvedMatrix.TransformWpfPoint(new Point(x, y))
                        : new Point(x, y);

                double scale = resolvedMatrix?.ScaleFactor() ?? 1.0;

                if (shape.Geometry is Circle2d c)
                {
                    var center = TransformPoint(c.Center.X, c.Center.Y);
                    context.Center = center;
                    context.Radius = c.Radius * scale;
                    context.Width = c.Radius * 2 * scale;
                    context.Height = c.Radius * 2 * scale;
                }
                else if (shape.Geometry is RotatedRect2d rr)
                {
                    var center = TransformPoint(rr.CenterX, rr.CenterY);
                    context.Center = center;
                    context.Width = rr.Width * scale;
                    context.Height = rr.Height * scale;
                    context.Angle = resolvedMatrix != null
                        ? PositionCorrectionResolver.DisplayAngleDeg(resolvedMatrix, rr.Angle)
                        : rr.Angle;
                }
                else if (shape.Geometry is Line2d l)
                {
                    var startPt = TransformPoint(l.Point.X, l.Point.Y);
                    var endPt = TransformPoint(l.Point.X + l.Direction.X * 100, l.Point.Y + l.Direction.Y * 100);
                    context.LineStart = startPt;
                    context.LineEnd = endPt;
                }
                else if (shape.Geometry is Polygon2d poly)
                {
                    context.PolygonPoints = poly.Vertices.Select(v => TransformPoint(v.X, v.Y)).ToList();
                }
                else if (shape.Geometry is System.Windows.Rect r)
                {
                    context.Width = r.Width * scale;
                    context.Height = r.Height * scale;
                    var centerPt = TransformPoint(r.X + r.Width / 2, r.Y + r.Height / 2);
                    context.Center = centerPt;
                }
            }

            context.CalculateBounds();

            return context;
        }

        /// <summary>
        /// 绘制编辑手柄（ROI编辑器风格）
        /// </summary>
        public void DrawEditHandles(Roi roi)
        {
            if (roi.LocalGeometry is not RoiShape shapeDef || OverlayCanvas == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"[DrawEditHandles] 跳过手柄绘制 | Roi={roi.Name} | IsShapeParams={roi.LocalGeometry is RoiShape} | OverlayCanvas={OverlayCanvas != null}",
                    "RoiEditorControl");
                return;
            }

            // 使用ROI编辑器的手柄渲染器创建手柄（反除缩放，使手柄在屏幕上视觉恒定）
            CreateHandlesForShape(shapeDef);

            if (_currentHandles != null && _currentHandles.Length > 0)
            {
                // ★ Phase 4: 根据编辑器角色传入差异色
                var zoom = CurrentZoom;
                var handleColor = DefaultRegionType switch
                {
                    RoiType.Mask => ColorConstants.MaskOrangeBrush,
                    RoiType.Inspection => ColorConstants.InspectionBlueBrush,
                    _ => ColorConstants.MaskOrangeBrush
                };

                // 正向变换手柄位置到测试坐标系（OverlayCanvas 在测试坐标系中；矩阵按形状有向性解析）
                var displayHandles = _currentHandles;
                var resolvedMatrix = CorrectionFor(shapeDef.Geometry);
                if (resolvedMatrix != null)
                {
                    displayHandles = _currentHandles.Select(h =>
                    {
                        var displayPos = resolvedMatrix.TransformWpfPoint(h.Position);
                        return new Rendering.EditHandle
                        {
                            Type = h.Type,
                            Position = displayPos,
                            HandleSize = h.HandleSize,
                            Cursor = h.Cursor,
                            VertexIndex = h.VertexIndex,
                            Bounds = new Rect(displayPos.X - h.HandleSize / 2, displayPos.Y - h.HandleSize / 2, h.HandleSize, h.HandleSize)
                        };
                    }).ToArray();
                }

                Rendering.HandleRenderer.DrawHandles(OverlayCanvas, displayHandles, shapeDef.ShapeType, zoom, handleColor);
            }
        }

        /// <summary>
        /// 为形状创建ROI编辑器风格的手柄（强自适应 + 差异化）
        /// </summary>
        private void CreateHandlesForShape(RoiShape shape)
        {
            var zoom = CurrentZoom;

            if (shape.Geometry == null) return;

            if (shape.Geometry is System.Windows.Rect r)
            {
                _currentHandles = Rendering.HandleRenderer.CreateRectangleHandles(r, zoom);
            }
            else if (shape.Geometry is RotatedRect2d rr)
            {
                var rotatedCenter = new Point(rr.CenterX, rr.CenterY);
                var corners = RotatedRectangleHelper.GetCorners(rotatedCenter, rr.Width, rr.Height, rr.Angle);
                var bottomCenter = new Point((corners[2].X + corners[3].X) / 2, (corners[2].Y + corners[3].Y) / 2);
                _currentHandles = Rendering.HandleRenderer.CreateRotatedRectangleHandles(corners, rr.Angle, bottomCenter, zoom, rr.Width);
            }
            else if (shape.Geometry is Circle2d c)
            {
                _currentHandles = Rendering.HandleRenderer.CreateCircleHandles(c.Center, c.Radius, zoom);
            }
            else if (shape.Geometry is Line2d l)
            {
                var endPoint = l.GetPointAt(100);
                _currentHandles = Rendering.HandleRenderer.CreateLineHandles(l.Point, endPoint, zoom);
            }
            else if (shape.Geometry is Polygon2d poly)
            {
                _currentHandles = Rendering.HandleRenderer.CreatePolygonHandles(
                    poly.Vertices.Select(v => new Point2D(v.X, v.Y)).ToList(), zoom);
            }
        }

        /// <summary>
        /// 计算旋转矩形的下边中点
        /// </summary>
        private Point CalculateRotatedRectBottomCenter(Point center, double height, double angle)
        {
            var angleRad = angle * Math.PI / 180;
            var sin = Math.Sin(angleRad);
            var cos = Math.Cos(angleRad);

            return new Point(
                center.X + (height / 2) * sin,
                center.Y + (height / 2) * cos
            );
        }

        /// <summary>
        /// 在可视化树中向上查找指定类型的父级元素
        /// </summary>
        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null)
                return null;

            DependencyObject? parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T result)
                    return result;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        #endregion

        #region 命中测试和选择

        public Roi? HitTestRoi(Point point, double tolerance = 0)
        {
            // 从后往前测试（后绘制的在上面）；逐个几何输入按形状有向性反变换到参考坐标系
            foreach (var roi in _viewModel.Rois.Reverse())
            {
                if (!roi.IsVisible) continue;
                if (roi.LocalGeometry is not RoiShape roiShape || roiShape.Geometry == null)
                    continue;

                Point refPoint = ToReferencePoint(point, roiShape.Geometry);

                if (IsPointInRoi(refPoint, roi, tolerance))
                {
                    VisionLogger.Instance.Log(LogLevel.Success,
                        $"[HitTestRoi] 命中: Roi={roi.Name}(Id={roi.Id:B}) | 编辑器角色={DefaultRegionType} | 参考坐标=({refPoint.X:F1},{refPoint.Y:F1})",
                        "RoiEditorControl");
                    return roi;
                }
            }

            return null;
        }

        private bool IsPointInRoi(Point point, Roi roi, double tolerance)
        {
            if (roi.LocalGeometry is not RoiShape shapeDef || shapeDef.Geometry == null)
                return false;

            var geo = shapeDef.Geometry;
            if (geo is Circle2d c)
                return IsPointInCircle(point, c, tolerance);
            else if (geo is RotatedRect2d rr)
                return IsPointInRotatedRectangle(point, rr, tolerance);
            else if (geo is Line2d l)
                return IsPointNearLine(point, l, tolerance);
            else if (geo is Polygon2d poly)
                return IsPointInPolygon(point, poly, tolerance);
            else if (geo is System.Windows.Rect r)
                return IsPointInRectangle(point, r, tolerance);

            return false;
        }

        private bool IsPointInRectangle(Point point, System.Windows.Rect r, double tolerance)
        {
            var bounds = new Rect(
                r.X - tolerance, r.Y - tolerance,
                r.Width + tolerance * 2, r.Height + tolerance * 2
            );
            return bounds.Contains(point);
        }

        private bool IsPointInCircle(Point point, Circle2d c, double tolerance)
        {
            var distance = (point - c.Center).Length;
            return distance <= c.Radius + tolerance;
        }

        private bool IsPointNearLine(Point point, Line2d l, double tolerance)
        {
            const double baseTolerance = 10.0;
            // 用一个足够远的采样点作"终点"
            var endPt = l.GetPointAt(1000);
            var startPt = new Point(l.Point.X, l.Point.Y);
            return DistanceToLine(point, startPt, endPt) < baseTolerance + tolerance;
        }

        private bool IsPointInRotatedRectangle(Point point, RotatedRect2d rr, double tolerance)
        {
            double dx = point.X - rr.CenterX;
            double dy = point.Y - rr.CenterY;
            double angleRad = -rr.Angle * Math.PI / 180.0;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);
            double localX = dx * cos - dy * sin;
            double localY = dx * sin + dy * cos;
            double halfW = rr.Width / 2 + tolerance;
            double halfH = rr.Height / 2 + tolerance;
            return Math.Abs(localX) <= halfW && Math.Abs(localY) <= halfH;
        }

        private double DistanceToLine(Point point, Point lineStart, Point lineEnd)
        {
            var dx = lineEnd.X - lineStart.X;
            var dy = lineEnd.Y - lineStart.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.001) return Point.Subtract(point, lineStart).Length;
            var t = Math.Max(0, Math.Min(1,
                ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (length * length)));
            var projection = new Point(lineStart.X + t * dx, lineStart.Y + t * dy);
            return Point.Subtract(point, projection).Length;
        }

        private bool IsPointInPolygon(Point point, Polygon2d poly, double tolerance)
        {
            var pts = poly.Vertices;
            if (pts.Length < 3) return false;

            // 射线法判断点是否在多边形内部
            bool inside = false;
            int j = pts.Length - 1;

            for (int i = 0; i < pts.Length; j = i++)
            {
                var pi = pts[i];
                var pj = pts[j];

                if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    inside = !inside;
                }
            }

            if (inside) return true;

            // 容差检查：点到多边形的任意一条边的距离在容差范围内也视为命中
            if (tolerance > 0)
            {
                for (int i = 0; i < pts.Length; i++)
                {
                    var p1 = pts[i];
                    var p2 = pts[(i + 1) % pts.Length];
                    if (DistanceToLine(point, p1, p2) <= tolerance)
                        return true;
                }
            }

            return false;
        }

        private void SelectRoi(Roi? roi)
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[SelectRoi] 选中几何输入 | Roi={roi?.Name ?? "null"}(Id={roi?.Id:B}) | Rois.Count={_viewModel.Rois.Count}",
                "RoiEditorControl");

            // 取消旧几何输入的参数变更订阅
            UnsubscribeRoiShape(_selectedRoi);

            _selectedRoi = roi;
            _viewModel.SelectedRoi = roi;
            RenderRequested?.Invoke();

            // 订阅新几何输入的参数变更（面板→画布自动同步）
            SubscribeRoiShape(roi);

            // 选中几何输入后，确保焦点回到区域编辑器，以接收键盘事件（如Delete删除）
            // 因为选择几何输入是通过主窗口 ImageControl 的鼠标事件触发的，
            // 点击主窗口会导致焦点离开调试窗口，需要在选中后拉回焦点
            if (roi != null)
            {
                Keyboard.Focus(this);
            }
        }

        public void DeselectAll()
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[DeselectAll] 取消所有选中 | _selectedRoi={_selectedRoi?.Name ?? "null"} | Rois.Count={_viewModel.Rois.Count}",
                "RoiEditorControl");

            UnsubscribeRoiShape(_selectedRoi);

            _selectedRoi = null;
            _viewModel.SelectedRoi = null;

            // 清理ROI编辑器风格的手柄状态
            ClearHandleDragState();

            RenderRequested?.Invoke();
        }

        /// <summary>
        /// 清理 ROI 编辑器风格的手柄/拖拽状态（不改变选中区域）。
        /// 用于模式切换等场景：切到订阅模式时丢弃本地手柄渲染状态。
        /// </summary>
        private void ClearHandleDragState()
        {
            _currentHandles = null;
            _activeHandleType = Rendering.HandleType.None;
            _originalShapeDefinition = null;
            _originalPosition = default;
            _originalSize = default;
            _originalRotation = 0;
            _rotateStartAngle = null;
            _rotateStartMouseAngle = null;
            _isDraggingHandle = false;
        }

        #region 形状参数变更订阅（面板→画布自动同步）

        /// <summary>
        /// 取消形状参数变更订阅
        /// 多边形顶点订阅已移除（Polygon2d 使用固定数组，非 Observable）
        /// </summary>
        private void UnsubscribeRoiShape(Roi? roi)
        {
            if (roi?.LocalGeometry is RoiShape shape)
            {
                shape.PropertyChanged -= OnRoiShapeChanged;
            }
        }

        /// <summary>
        /// 订阅形状参数变更（面板→画布自动同步）
        /// 多边形顶点订阅已移除（Polygon2d 使用固定数组，非 Observable）
        /// </summary>
        private void SubscribeRoiShape(Roi? roi)
        {
            if (roi?.LocalGeometry is RoiShape shape)
            {
                shape.PropertyChanged += OnRoiShapeChanged;
            }
        }

        /// <summary>
        /// 形状参数 PropertyChanged 回调（Geometry 变更 → 画布刷新）
        /// </summary>
        private void OnRoiShapeChanged(object? sender, PropertyChangedEventArgs e)
        {
            RenderRequested?.Invoke();
        }

        #endregion

        #endregion

        #region ViewModel事件处理

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 监控关键属性变化
            if (e.PropertyName == nameof(RoiEditorViewModel.IsDrawing))
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[OnViewModelPropertyChanged] IsDrawing 变化 | InstanceId={_controlInstanceId} | NewValue={_viewModel.IsDrawing} | _isDrawing={_isDrawing} | IsLoaded={IsLoaded} | IsVisible={IsVisible} | _currentDrawingRoi={_currentDrawingRoi?.Name ?? "null"}",
                    "RoiEditorControl");
            }
            else if (e.PropertyName == nameof(RoiEditorViewModel.SelectedShapeType))
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[OnViewModelPropertyChanged] SelectedShapeType 变化 | InstanceId={_controlInstanceId} | NewValue={_viewModel.SelectedShapeType}",
                    "RoiEditorControl");
            }
            else if (e.PropertyName == nameof(RoiEditorViewModel.CurrentMode))
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[OnViewModelPropertyChanged] CurrentMode 变化 | InstanceId={_controlInstanceId} | NewValue={_viewModel.CurrentMode}",
                    "RoiEditorControl");

                // 订阅模式：清空本地绘制的交互层（GetRenderContexts 已按模式过滤叠加层）。
                // 切到订阅时若在绘制（含多边形），取消进行中的绘制并清理手柄/橡皮筋状态。
                if (_viewModel.CurrentMode == RoiSourceMode.Subscribe)
                {
                    if (_isDrawingPolygon)
                    {
                        _isDrawingPolygon = false;
                        _polygonPoints.Clear();
                        ClearRubberBand();
                    }
                    if (_currentDrawingRoi != null)
                        ReleaseRoiIndex(_currentDrawingRoi.Name);
                    _isDrawing = false;
                    _currentDrawingRoi = null;
                    _viewModel.IsDrawing = false;
                    ShapeToolBarControl.ClearSelection();
                    ClearHandleDragState();
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[OnViewModelPropertyChanged] 切换到订阅模式，已取消绘制/清空交互层 | InstanceId={_controlInstanceId}",
                        "RoiEditorControl");
                }

                // 任意方向的模式切换（绘制↔订阅）都触发叠加层重刷：
                // 订阅 → 清除本地几何渲染；订阅 → 绘制 恢复叠加层显示。
                RenderRequested?.Invoke();
            }
        }

        private void OnRoiChanged(object? sender, RoiChangedEventArgs e)
        {
            // 精简日志：仅记录数量，不再拼接全部几何输入 GUID 列表（避免超长字符串）
            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[OnRoiChanged] ViewModel 触发 RoiChanged | InstanceId={_controlInstanceId} | RoiId={e.Roi?.Id:B} | RoiName={e.Roi?.Name ?? "null"} | ChangeType={e.ChangeType} | Rois.Count={_viewModel?.Rois?.Count ?? -1}",
                "RoiEditorControl");

            try
            {
                // 几何输入删除时释放索引到空洞池
                if (e.ChangeType == RoiChangeType.Removed && e.Roi != null)
                {
                    ReleaseRoiIndex(e.Roi.Name);
                }
                // 清除所有几何输入时重置索引管理
                else if (e.ChangeType == RoiChangeType.Cleared)
                {
                    _availableIndices.Clear();
                    _nextRoiIndex = 1;
                }

                RenderRequested?.Invoke();
                if (e.Roi != null)
                {
                    RoiDataChanged?.Invoke(this, e.Roi);
                }
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[OnRoiChanged] 几何输入变更处理失败 | InstanceId={_controlInstanceId} | Error={ex.Message}",
                    "RoiEditorControl",
                    ex);
            }
        }

        #endregion

        /// <summary>
        /// 创建绘制中的临时几何输入（本地绘制模式）
        /// </summary>
        /// <remarks>
        /// 绘制完成前不入集合（GetRenderContexts 不渲染它），
        /// 预览态由 _isDrawing/_currentDrawingRoi 表达（GetPreviewContext 单独渲染），
        /// 完成时 FinishInteraction 将其加入 Rois 集合成为正式元素。
        /// 检测/屏蔽角色由编辑器实例 DefaultRegionType 承载，不写回模型。
        /// </remarks>
        private Roi CreateDrawingRoi(ShapeType tool)
        {
            return new Roi
            {
                Name = GenerateRoiName(),
                LocalGeometry = new RoiShape
                {
                    Geometry = RoiShape.CreateDefaultGeometry(tool),
                }
            };
        }

        /// <summary>
        /// 生成几何输入默认名 - 空洞池优先，O(log n)
        /// </summary>
        /// <remarks>
        /// 优先填充被删除输入释放的索引空洞，无空洞时递增。
        /// 例如：已有 几何输入_1、几何输入_3，则新建 几何输入_2（填充空洞）
        /// </remarks>
        private string GenerateRoiName()
        {
            int nextIndex;
            if (_availableIndices.Count > 0)
            {
                // 空洞池非空，取最小索引
                nextIndex = _availableIndices.Min;
                _availableIndices.Remove(nextIndex);
            }
            else
            {
                // 无空洞，递增
                nextIndex = _nextRoiIndex++;
            }
            return $"几何输入_{nextIndex}";
        }

        /// <summary>
        /// 释放几何输入索引到空洞池 - 删除时调用，O(log n)
        /// </summary>
        private void ReleaseRoiIndex(string inputName)
        {
            if (inputName != null && inputName.StartsWith("几何输入_"))
            {
                var indexPart = inputName.Substring(5);
                if (int.TryParse(indexPart, out var index) && index > 0)
                {
                    _availableIndices.Add(index);
                }
            }
        }

        /// <summary>
        /// 根据已有几何输入初始化空洞池 - SetRoisSource 时调用
        /// </summary>
        private void InitializeRoiIndexFromExisting()
        {
            _availableIndices.Clear();
            _nextRoiIndex = 1;

            var usedIndices = new HashSet<int>();
            foreach (var input in _viewModel.Rois)
            {
                if (input.Name != null && input.Name.StartsWith("几何输入_"))
                {
                    var indexPart = input.Name.Substring(5);
                    if (int.TryParse(indexPart, out var index) && index > 0)
                    {
                        usedIndices.Add(index);
                    }
                }
            }

            if (usedIndices.Count == 0)
                return;

            var maxIndex = usedIndices.Max();
            _nextRoiIndex = maxIndex + 1;

            // 扫描 1~max 之间的空洞
            for (int i = 1; i <= maxIndex; i++)
            {
                if (!usedIndices.Contains(i))
                {
                    _availableIndices.Add(i);
                }
            }
        }

        #region 形状工具栏

        /// <summary>
        /// 初始化形状工具栏数据（数据驱动，每个形状可见性可单独控制）
        /// </summary>
        private void InitializeShapeTools()
        {
            const string polygonToolTip = "多边形 (P)\n点击添加顶点\n右键撤销上一点\n双击/Enter完成";
            var shapeTools = new System.Collections.ObjectModel.ObservableCollection<Models.ShapeToolItem>
            {
                new(ShapeType.Rectangle, "矩形", "M2,2 L18,2 L18,18 L2,18 Z", Key.R),
                new(ShapeType.Circle, "圆形",
                    "M9,1 A8,8 0 1,0 9,17 A8,8 0 1,0 9,1 Z M9,4 L9,14 M4,9 L14,9", Key.C),
                new(ShapeType.RotatedRectangle, "旋转矩形",
                    "M5,10 L10,3 L19,10 L14,17 Z M17,7 A3,3 0 0,1 20,11", Key.O),
                new(ShapeType.Line, "直线", "M3,17 L17,3", Key.L, isVisible: false),
                new(ShapeType.Polygon, "多边形",
                    "M11,1 L19,6 L18,15 L12,19 L4,16 L2,9 Z", Key.P, toolTip: polygonToolTip)
            };

            ShapeToolBarControl.ShapeTools = shapeTools;
        }

        /// <summary>
        /// 形状工具栏选择变更处理
        /// </summary>
        private void OnShapeToolBarSelectionChanged(object? sender, ShapeType? shapeType)
        {
            if (shapeType.HasValue)
            {
                // 多边形特殊情况
                if (shapeType.Value == ShapeType.Polygon)
                    _isDrawingPolygon = false;
                SelectShapeTool(shapeType.Value);
            }
            else
            {
                EnterSelectMode();
            }
        }

        /// <summary>
        /// 选择形状工具 - toggle 行为
        /// 已激活的相同形状 → 退出绘制模式回到选择模式
        /// 不同形状 → 切换形状工具
        /// </summary>
        private void SelectShapeTool(ShapeType shapeType)
        {
            // 如果当前是绘制模式且点击的是同一个形状 → 退出绘制，回到选择模式
            if (_viewModel.IsDrawing && _currentTool == shapeType)
            {
                // 取消进行中的绘制
                if (_isDrawing || _isDrawingPolygon)
                {
                    if (_currentDrawingRoi != null)
                        ReleaseRoiIndex(_currentDrawingRoi.Name);
                    _isDrawing = false;
                    _isDrawingPolygon = false;
                    _polygonPoints.Clear();
                    _currentDrawingRoi = null;
                    ClearRubberBand();
                }
                EnterSelectMode();
                return;
            }

            // 设置当前工具
            _currentTool = shapeType;
            _viewModel.IsDrawing = true;

            // 设置 SelectedShapeType
            _viewModel.SelectedShapeType = shapeType;
            _viewModel.CallUpdateParameters();

            // 更新形状工具栏选中状态
            ShapeToolBarControl.SetSelectedShape(shapeType);

            // 更新状态消息
            var shapeName = shapeType switch
            {
                ShapeType.Rectangle => "矩形",
                ShapeType.Circle => "圆形",
                ShapeType.RotatedRectangle => "旋转矩形",
                ShapeType.Line => "直线",
                ShapeType.Polygon => "多边形",
                _ => shapeType.ToString()
            };
            _viewModel.StatusMessage = $"已选择 {shapeName} 工具，在图像上拖动绘制";

            // 通知 Manager：工具已更改（用于分组互斥）
            ToolActivated?.Invoke();
        }

        /// <summary>
        /// 进入选择/编辑模式
        /// </summary>
        private void EnterSelectMode(bool notifyManager = true)
        {
            // 退出绘制状态，进入选择状态
            _viewModel.IsDrawing = false;
            _currentTool = ShapeType.None;

            // 清除形状工具栏选中状态
            ShapeToolBarControl.ClearSelection();

            // 更新状态消息
            _viewModel.StatusMessage = "选择/编辑模式 - 点击区域可编辑，滚轮缩放，拖拽平移";

            // 通知 Manager：工具已更改（用于分组互斥）
            if (notifyManager)
                ToolActivated?.Invoke();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}

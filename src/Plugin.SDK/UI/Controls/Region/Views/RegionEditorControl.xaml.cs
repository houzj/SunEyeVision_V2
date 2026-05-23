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
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.ViewModels;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Logic;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Rendering;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Views
{
    /// <summary>
    /// 区域编辑控件 - 纯UI组件，不负责数据持久化
    /// </summary>
    public partial class RegionEditorControl : UserControl, INotifyPropertyChanged
    {
        private RegionEditorViewModel _viewModel;
        private ShapeType _currentTool = ShapeType.Point;  // 初始为选择模式（Point=选择）

        // 控件实例ID（用于区分不同的 RegionEditorControl 实例）
        private readonly string _controlInstanceId = Guid.NewGuid().ToString("N").Substring(0, 8);

        // 绘制状态
        private bool _isDrawing;
        private bool _isDragging;
        private bool _isDraggingHandle;
        private Point _startPoint;
        private RegionData? _currentDrawingRegion;
        private RegionData? _selectedRegion;
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
        private int _drawingLogCount = 0;
        private int _previewLogCount = 0;

        // 区域名称索引管理（空洞池策略）
        private int _nextRegionIndex = 1;
        private readonly SortedSet<int> _availableIndices = new();

        // 手柄管理
        private HandleManager _handleManager;
        private RegionEditorSettings _settings;

        // ROI编辑器风格的手柄管理
        private Rendering.EditHandle[] _currentHandles = null;
        private Rendering.HandleType _activeHandleType = Rendering.HandleType.None;
        private ShapeParameters? _originalShapeDefinition = null;
        private Point _originalPosition;
        private Size _originalSize;
        private double _originalRotation;
        private double? _rotateStartAngle = null;
        private double? _rotateStartMouseAngle = null;

        /// <summary>
        /// Overlay Canvas引用（由RegionEditorManagerControl设置，用于手柄/橡皮筋绘制）
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
        /// ★ Phase 4: 工具激活事件（Manager 用于分组互斥控制）
        /// 当用户点击形状工具或选择模式按钮时触发，通知 Manager 同一时间只有一个编辑器的工具被激活
        /// </summary>
        public event Action? ToolActivated;

        public RegionEditorViewModel ViewModel => _viewModel;

        #region 依赖属性

        /// <summary>
        /// 区域数据集合（仅用于 XAML 声明式绑定初始化读取）
        /// </summary>
        /// <remarks>
        /// 此 DependencyProperty 仅作为 XAML 绑定的桥接层。
        /// 真正的共享引用由 SetRegionsSource() 方法命令式建立。
        /// 不要依赖此属性的变更回调来建立共享引用。
        /// </remarks>
        public static readonly DependencyProperty RegionsProperty =
            DependencyProperty.Register(nameof(Regions), typeof(IEnumerable<RegionData>), typeof(RegionEditorControl),
                new PropertyMetadata(null, OnRegionsChanged));

        public IEnumerable<RegionData>? Regions
        {
            get => (IEnumerable<RegionData>?)GetValue(RegionsProperty);
            set => SetValue(RegionsProperty, value);
        }

        private static void OnRegionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RegionEditorControl control)
            {
                control.RenderRequested?.Invoke();
            }
        }

        /// <summary>
        /// AvailableDataSources 透传属性（用于内部 RegionSubscribePanel）
        /// </summary>
        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(nameof(AvailableDataSources),
                typeof(System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>),
                typeof(RegionEditorControl),
                new PropertyMetadata(null));

        public System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>? AvailableDataSources
        {
            get => (System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>?)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        /// <summary>
        /// 新创建区域的默认类型（Inspection/Mask）
        /// RegionEditorManagerControl 使用此属性区分检测区域和屏蔽区域编辑器
        /// </summary>
        public static readonly DependencyProperty DefaultRegionTypeProperty =
            DependencyProperty.Register(nameof(DefaultRegionType),
                typeof(RegionType),
                typeof(RegionEditorControl),
                new PropertyMetadata(RegionType.Inspection, OnDefaultRegionTypeChanged));

        public RegionType DefaultRegionType
        {
            get => (RegionType)GetValue(DefaultRegionTypeProperty);
            set => SetValue(DefaultRegionTypeProperty, value);
        }

        #region 区域类型标题（由 DefaultRegionType 自动派生）

        /// <summary>
        /// 区域类型标题文本（检测区域/屏蔽区域）
        /// </summary>
        public static readonly DependencyProperty RegionTypeTitleProperty =
            DependencyProperty.Register(nameof(RegionTypeTitle),
                typeof(string),
                typeof(RegionEditorControl),
                new PropertyMetadata("检测区域"));

        public string RegionTypeTitle
        {
            get => (string)GetValue(RegionTypeTitleProperty);
            set => SetValue(RegionTypeTitleProperty, value);
        }

        /// <summary>
        /// 区域类型标题颜色（检测区域=蓝色 #4488FF，屏蔽区域=橙色 #FFA500）
        /// </summary>
        public static readonly DependencyProperty RegionTypeTitleBrushProperty =
            DependencyProperty.Register(nameof(RegionTypeTitleBrush),
                typeof(Brush),
                typeof(RegionEditorControl),
                new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4488FF"))));

        public Brush RegionTypeTitleBrush
        {
            get => (Brush)GetValue(RegionTypeTitleBrushProperty);
            set => SetValue(RegionTypeTitleBrushProperty, value);
        }

        private static void OnDefaultRegionTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RegionEditorControl control)
            {
                var newType = (RegionType)e.NewValue;
                control.RegionTypeTitle = newType == RegionType.Inspection ? "检测区域" : "屏蔽区域";
                control.RegionTypeTitleBrush = newType == RegionType.Inspection
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4488FF"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500"));
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
        /// 区域数据变更事件
        /// </summary>
        public event EventHandler<RegionData>? RegionDataChanged;

        #endregion

        #region 共享引用管理

        /// <summary>
        /// 注入外部区域集合，与参数模型共享同一个 ObservableCollection 实例
        /// </summary>
        /// <remarks>
        /// 这是建立共享引用的唯一正确入口。
        /// 调用后 ViewModel.Regions 和外部集合指向同一个对象，
        /// 用户绘制区域直接进入参数集合，序列化时自然有数据。
        /// 
        /// 必须在 SetMainImageControl 之后调用。
        /// </remarks>
        public void SetRegionsSource(ObservableCollection<RegionData> externalRegions)
        {
            if (externalRegions == null)
                throw new ArgumentNullException(nameof(externalRegions));

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[SetRegionsSource] 建立共享引用 | 外部集合HashCode={externalRegions.GetHashCode():X8} | 外部集合数量={externalRegions.Count}",
                "RegionEditorControl");

            _viewModel.SetRegionsSource(externalRegions);
            InitializeRegionIndexFromExisting();
            RenderRequested?.Invoke();

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[SetRegionsSource] 共享引用已建立 | ViewModel.Regions={_viewModel.Regions.GetHashCode():X8} | 引用相同={_viewModel.Regions == externalRegions}",
                "RegionEditorControl");
        }

        #endregion

        public RegionEditorControl()
        {
            // 设计时完全跳过初始化，避免 XAML 解析触发 NullReferenceException
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            // ★ 必须在 InitializeComponent 之前创建 ViewModel
            // 因为 XAML 中 RadioButton IsChecked="True" 会立即触发 Checked 事件
            EnsureViewModel();
            InitializeComponent();
            _handleManager = new HandleManager();
            _settings = RegionEditorSettings.Default;

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
                $"[RegionEditorControl] 控件初始化完成 | InstanceId={_controlInstanceId}",
                "RegionEditorControl");
        }

        private void RegionEditorControl_Loaded(object sender, RoutedEventArgs e)
        {
            VisionLogger.Instance.Log(LogLevel.Success,
                $"[RegionEditorControl] 控件已加载（Tab切换）| InstanceId={_controlInstanceId}",
                "RegionEditorControl");

            Keyboard.Focus(this);

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[RegionEditorControl_Loaded] 加载完成 | InstanceId={_controlInstanceId} | RegionCount={_viewModel.Regions.Count}",
                "RegionEditorControl");
        }

        private void RegionEditorControl_Unloaded(object sender, RoutedEventArgs e)
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RegionEditorControl] 控件已卸载（Tab切换）| InstanceId={_controlInstanceId} | _isDrawing={_isDrawing}",
                "RegionEditorControl");

            // 如果正在绘制中，取消绘制状态（避免切换 Tab 时绘制状态不一致）
            if (_isDrawing)
            {
                _isDrawing = false;
                _currentDrawingRegion = null;
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[RegionEditorControl_Unloaded] Tab切换时取消绘制状态 | InstanceId={_controlInstanceId}",
                    "RegionEditorControl");
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
                    "RegionEditorControl");

                _viewModel = new RegionEditorViewModel();
                DataContext = _viewModel;

                // ★ 订阅 ViewModel 事件（在整个生命周期内保持订阅）
                // 事件订阅生命周期由 SetMainImageControl / ClearMainOverlay 管理
                // Tab 切换不应该影响订阅状态
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _viewModel.RegionChanged += OnRegionChanged;

                // 不再订阅 Regions.CollectionChanged，共享引用后无需同步

                VisionLogger.Instance.Log(LogLevel.Success,
                    "[EnsureViewModel] ViewModel 创建并初始化完成",
                    "RegionEditorControl");
            }
            else
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    "[EnsureViewModel] ViewModel 已存在，跳过创建",
                    "RegionEditorControl");
            }
        }



        /// <summary>
        /// 设置主界面ImageControl引用（已废弃 - 由RegionEditorManagerControl统一管理）
        /// </summary>
        [Obsolete("由RegionEditorManagerControl统一管理事件和渲染，不再需要直接调用")]
        public void SetMainImageControl(ImageControl imageControl)
        {
            // 空实现，保持向后兼容
        }

        /// <summary>
        /// 清理主界面覆盖层（已废弃 - 由RegionEditorManagerControl统一管理）
        /// </summary>
        [Obsolete("由RegionEditorManagerControl统一管理事件和渲染")]
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
        /// 获取所有区域数据
        /// </summary>
        public IEnumerable<RegionData> GetRegions()
        {
            return _viewModel.Regions;
        }

        /// <summary>
        /// 获取选中的区域
        /// </summary>
        public RegionData? GetSelectedRegion()
        {
            return _selectedRegion;
        }

        /// <summary>
        /// 设置选中的区域
        /// </summary>
        public void SetSelectedRegion(RegionData? region)
        {
            SelectRegion(region);
        }

        /// <summary>
        /// 添加区域
        /// </summary>
        public void AddRegion(RegionData region)
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[AddRegion] 添加区域: RegionId={region.Id}, RegionType={region.GetShapeType()?.ToString() ?? "Unknown"}",
                "RegionEditorControl");

            try
            {
                _viewModel.Regions.Add(region);
                RenderRequested?.Invoke();
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[AddRegion] 添加区域失败: RegionId={region.Id}, 错误={ex.Message}",
                    "RegionEditorControl",
                    ex);
            }
        }

        /// <summary>
        /// 移除区域
        /// </summary>
        public void RemoveRegion(RegionData region)
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RemoveRegion] 移除区域: RegionId={region.Id}, RegionType={region.GetShapeType()?.ToString() ?? "Unknown"}",
                "RegionEditorControl");

            try
            {
                _viewModel.Regions.Remove(region);
                if (_selectedRegion == region)
                {
                    UnsubscribeShapeParameters(_selectedRegion);
                    _selectedRegion = null;
                }
                RenderRequested?.Invoke();
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[RemoveRegion] 移除区域失败: RegionId={region.Id}, 错误={ex.Message}",
                    "RegionEditorControl",
                    ex);
            }
        }

        /// <summary>
        /// 解析所有区域
        /// </summary>
        public List<Logic.ResolvedRegion> ResolveAllRegions()
        {
            return _viewModel.ResolveAllRegions();
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
                    if (_currentDrawingRegion != null)
                        ReleaseRegionIndex(_currentDrawingRegion.Name);
                    _currentDrawingRegion = null;
                }
                ClearRubberBand();
                if (_polygonPoints.Count >= 1 && _currentDrawingRegion != null)
                {
                    if (_currentDrawingRegion.Parameters is ShapeParameters shapeDef)
                    {
                        shapeDef.Points.Add(new Point2D(_lastMouseImagePosition.X, _lastMouseImagePosition.Y));
                        RenderRequested?.Invoke();
                        shapeDef.Points.RemoveAt(shapeDef.Points.Count - 1);
                    }
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
            if (_selectedRegion == null) return;

            var regionToDelete = _selectedRegion;

            // ★ 诊断日志：删除前状态
            var beforeRegionNames = string.Join(", ", _viewModel.Regions.Select(r => $"{r.Name}(Id={r.Id:B})"));
            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[Delete] 删除前状态 | InstanceId={_controlInstanceId} | 删除目标={regionToDelete.Name}(Id={regionToDelete.Id:B}) | Regions.Count={_viewModel.Regions.Count} | Regions=[{beforeRegionNames}] | _selectedRegion={regionToDelete.Name}",
                "RegionEditorControl");

            // ★ Phase 1 修复：先取消订阅/清空状态，再触发删除（避免 OnRegionChanged 回调中看到旧状态）
            UnsubscribeShapeParameters(regionToDelete);
            _selectedRegion = null;

            _viewModel.RemoveSelectedRegion();

            // RemoveSelectedRegion 内部会触发 RegionChanged → OnRegionChanged → RenderRequested
            // 此时 _selectedRegion=null, GetSelectedRegion()=null, 渲染正确
            var afterRegionNames = string.Join(", ", _viewModel.Regions.Select(r => $"{r.Name}(Id={r.Id:B})"));
            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[Delete] 删除完成 | Regions.Count={_viewModel.Regions.Count} | Regions=[{afterRegionNames}] | SelectedRegion={_viewModel.SelectedRegion?.Name ?? "null"}",
                "RegionEditorControl");
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
                if (_currentDrawingRegion != null)
                    ReleaseRegionIndex(_currentDrawingRegion.Name);
                _currentDrawingRegion = null;
                ClearRubberBand();
                RenderRequested?.Invoke();
                _viewModel.StatusMessage = "已取消多边形绘制";
            }
            else if (_isDrawing)
            {
                _isDrawing = false;
                _currentDrawingRegion = null;
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
                "RegionEditorControl");

            // 1. 取消进行中的绘制（多边形顶点、橡皮筋等）
            if (_isDrawing)
            {
                if (_currentDrawingRegion != null)
                    ReleaseRegionIndex(_currentDrawingRegion.Name);
                _currentDrawingRegion = null;
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
        /// 处理鼠标移动（由Manager路由）
        /// </summary>
        public void ProcessMouseMove(Point position)
        {
            if (OverlayCanvas == null) return;

            _lastMouseImagePosition = position;

            if (_mouseMoveLogCounter++ % 100 == 0)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[ProcessMouseMove] Count={_mouseMoveLogCounter} | Position={position:F1} | IsDrawing={_isDrawing} | IsDragging={_isDragging} | IsDraggingHandle={_isDraggingHandle}",
                    "RegionEditorControl");
            }

            // ★ 优化: 不再在此触发 RenderRequested，由 Manager 在 OnImageMouseMove 末尾统一调用 RenderAll()
            // 避免两次渲染 (ProcessMouseMove 内部 + Manager.RenderAll)

            if (_isDraggingHandle && _activeHandleType != Rendering.HandleType.None && _selectedRegion?.Parameters is ShapeParameters shape)
            {
                HandleShapeEdit(shape, position);
                return;
            }

            if (_isDragging && _selectedRegion != null)
            {
                var offset = position - _dragStartPoint;
                MoveRegion(_selectedRegion, offset);
                _dragStartPoint = position;
                return;
            }

            if (_isDrawing && _currentDrawingRegion != null)
            {
                if (_currentTool == ShapeType.Polygon && _isDrawingPolygon)
                {
                    // 多边形橡皮筋线（从最后一个顶点到鼠标位置）
                    DrawPolygonRubberBand(position);
                }
                else
                {
                    if (_drawingLogCount == 0)
                    {
                        VisionLogger.Instance.Log(LogLevel.Info,
                            $"[绘制预览] 开始更新预览 | CurrentDrawingRegion={_currentDrawingRegion.Name} | Position={position:F1}",
                            "RegionEditorControl");
                        _drawingLogCount++;
                    }

                    UpdateDrawingPreview(position);
                }
            }
        }

        /// <summary>
        /// 处理鼠标按下（由Manager路由）
        /// </summary>
        /// <returns>true=事件已消费（区域操作），false=空白处点击（允许平移）</returns>
        public bool ProcessMouseDown(Point position)
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[ProcessMouseDown] MouseDown事件 | InstanceId={_controlInstanceId} | IsDrawing={_viewModel.IsDrawing} | _isDrawing={_isDrawing} | Tool={_currentTool} | Position={position:F1} | OverlayCanvas={OverlayCanvas != null}",
                "RegionEditorControl");

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
                    _currentDrawingRegion = RegionData.CreateDrawingRegion(
                        GenerateRegionName(), _currentTool);
                    _currentDrawingRegion.RegionType = DefaultRegionType;
                    _currentDrawingRegion.IsPreview = true;
                }

            _polygonPoints.Add(position);
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
                        "RegionEditorControl");
                    return true;
                }

                _isDrawing = true;
                _startPoint = position;

                _currentDrawingRegion = RegionData.CreateDrawingRegion(
                    GenerateRegionName(), _currentTool);
                _currentDrawingRegion.RegionType = DefaultRegionType;

                if (_currentDrawingRegion.Parameters is ShapeParameters shapeDef)
                {
                    switch (_currentTool)
                    {
                        case ShapeType.Rectangle:
                        case ShapeType.RotatedRectangle:
                            shapeDef.CenterX = position.X;
                            shapeDef.CenterY = position.Y;
                            shapeDef.Width = 0;
                            shapeDef.Height = 0;
                            break;
                        case ShapeType.Circle:
                            shapeDef.CenterX = position.X;
                            shapeDef.CenterY = position.Y;
                            shapeDef.Radius = 0;
                            shapeDef.Width = 0;
                            shapeDef.Height = 0;
                            break;
                        case ShapeType.Line:
                            shapeDef.StartX = position.X;
                            shapeDef.StartY = position.Y;
                            shapeDef.EndX = position.X;
                            shapeDef.EndY = position.Y;
                            break;
                    }
                }
                return true;
            }

            if (_selectedRegion != null)
            {
                if (_currentHandles != null)
                {
                    var hitHandleType = Rendering.HandleRenderer.HitTestHandle(position, _currentHandles);

                    if (hitHandleType != Rendering.HandleType.None)
                    {
                        if (hitHandleType == Rendering.HandleType.Center)
                        {
                            _isDragging = true;
                            _dragStartPoint = position;
                            _viewModel.StatusMessage = "正在拖动区域";
                            return true;
                        }

                        if (hitHandleType == Rendering.HandleType.PolygonVertex)
                        {
                            _isDraggingHandle = true;
                            _activeHandleType = hitHandleType;
                            _handleDragStartPoint = position;
                            _activeVertexIndex = -1;
                            if (_currentHandles != null)
                            {
                                foreach (var h in _currentHandles)
                                {
                                    if (h.Type == Rendering.HandleType.PolygonVertex && h.VertexIndex >= 0)
                                    {
                                        var dx = position.X - h.Position.X;
                                        var dy = position.Y - h.Position.Y;
                                        if (Math.Sqrt(dx * dx + dy * dy) < h.HandleSize)
                                        {
                                            _activeVertexIndex = h.VertexIndex;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (_selectedRegion?.Parameters is ShapeParameters polyShape)
                                _originalShapeDefinition = polyShape.Clone() as ShapeParameters;
                            _viewModel.StatusMessage = "正在拖拽多边形顶点";
                            return true;
                        }

                        _isDraggingHandle = true;
                        _activeHandleType = hitHandleType;
                        _handleDragStartPoint = position;

                        if (_selectedRegion.Parameters is ShapeParameters shapeDef)
                        {
                            _originalShapeDefinition = shapeDef.Clone() as ShapeParameters;
                            _originalPosition = new Point(shapeDef.CenterX, shapeDef.CenterY);
                            _originalSize = new Size(shapeDef.Width, shapeDef.Height);
                            _originalRotation = shapeDef.Angle;

                            if (hitHandleType == Rendering.HandleType.Rotate)
                            {
                                _rotateStartAngle = shapeDef.Angle;
                                var center = new Point(shapeDef.CenterX, shapeDef.CenterY);
                                _rotateStartMouseAngle = CalculateMouseAngle(center, position);
                            }
                        }

                        _viewModel.StatusMessage = $"正在拖动 {hitHandleType} 手柄";
                        return true;
                    }
                }
            }

            var hitRegion = HitTestRegion(position);
            if (hitRegion != null)
            {
                SelectRegion(hitRegion);
                _isDragging = true;
                _dragStartPoint = position;
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
                "RegionEditorControl");

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
                "RegionEditorControl");

            _polygonPoints.RemoveAt(_polygonPoints.Count - 1);
            SyncPolygonToShapeDef();
            if (_polygonPoints.Count == 0)
            {
                _isDrawingPolygon = false;
                if (_currentDrawingRegion != null)
                    ReleaseRegionIndex(_currentDrawingRegion.Name);
                _currentDrawingRegion = null;
            }
            ClearRubberBand();
            if (_polygonPoints.Count >= 1 && _currentDrawingRegion != null)
            {
                if (_currentDrawingRegion.Parameters is ShapeParameters shapeDef)
                {
                    shapeDef.Points.Add(new Point2D(_lastMouseImagePosition.X, _lastMouseImagePosition.Y));
                    RenderRequested?.Invoke();
                    shapeDef.Points.RemoveAt(shapeDef.Points.Count - 1);
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
                $"[FinishInteraction] 调用 FinishInteraction | InstanceId={_controlInstanceId} | _isDrawing={_isDrawing} | _currentDrawingRegion={_currentDrawingRegion?.Name ?? "null"} | _isDrawingPolygon={_isDrawingPolygon}",
                "RegionEditorControl");

            // ★ 多边形仍在绘制中（未完成），不清理状态
            if (_isDrawingPolygon)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[FinishInteraction] 多边形仍在绘制中，跳过清理 | InstanceId={_controlInstanceId} | 顶点数={_polygonPoints.Count}",
                    "RegionEditorControl");

                // 多边形非最终完成时，清理橡皮筋线但不重置 _isDrawing/_currentDrawingRegion
                ClearRubberBand();
                RenderRequested?.Invoke();
                return;
            }

            if (_isDrawing && _currentDrawingRegion != null)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[FinishInteraction] 完成绘制 | InstanceId={_controlInstanceId} | Region={_currentDrawingRegion.Name} | IsPreview={_currentDrawingRegion.IsPreview} | _nextRegionIndex={_nextRegionIndex}",
                    "RegionEditorControl");

                // 完成绘制，设置预览状态为 false
                _currentDrawingRegion.IsPreview = false;

                // 应用默认区域类型（检测/屏蔽），颜色由 RegionData.ApplyTypeColors() 自动同步
                _currentDrawingRegion.RegionType = DefaultRegionType;

                var shapeDef = _currentDrawingRegion.Parameters as ShapeParameters;
                if (shapeDef != null)
                {
                    // 统一使用大尺寸算法，并对长宽增加限制：任意尺寸太小都不生成区域
                    // 矩形和旋转矩形：最小宽度和高度都是10像素，同时满足面积≥100
                    bool isValid = _currentTool switch
                    {
                        ShapeType.Rectangle or ShapeType.RotatedRectangle =>
                            shapeDef.Width >= 10 &&
                            shapeDef.Height >= 10 &&
                            shapeDef.Width * shapeDef.Height >= 100,
                        ShapeType.Circle =>
                            shapeDef.Radius >= 5.64 && // 面积≥100对应的半径
                            Math.PI * shapeDef.Radius * shapeDef.Radius >= 100,
                        ShapeType.Line => shapeDef.GetLineLength() >= 10,
                        ShapeType.Polygon => shapeDef.Points.Count >= 3,
                        _ => false
                    };

                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[FinishInteraction] 验证区域尺寸 | InstanceId={_controlInstanceId} | Region={_currentDrawingRegion.Name} | Width={shapeDef.Width:F2} | Height={shapeDef.Height:F2} | IsValid={isValid}",
                        "RegionEditorControl");

                    if (isValid)
                    {
                        _viewModel.Regions.Add(_currentDrawingRegion);
                        _viewModel.SelectedRegion = _currentDrawingRegion;
                        _viewModel.StatusMessage = $"已创建 {_currentTool} 区域";
                        RegionDataChanged?.Invoke(this, _currentDrawingRegion);

                        // 诊断日志：新建区域后的完整状态
                        var allRegionNames = string.Join(", ", _viewModel.Regions.Select(r => $"{r.Name}(Id={r.Id:B},IsVisible={r.IsVisible})"));
                        VisionLogger.Instance.Log(LogLevel.Success,
                            $"[FinishInteraction] 区域已添加到集合 | InstanceId={_controlInstanceId} | Region={_currentDrawingRegion.Name}(Id={_currentDrawingRegion.Id:B}) | TotalRegions={_viewModel.Regions.Count} | Regions=[{allRegionNames}]",
                            "RegionEditorControl");
                    }
                    else
                    {
                        _viewModel.StatusMessage = "区域太小，已忽略";

                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"[FinishInteraction] 区域太小，已忽略 | InstanceId={_controlInstanceId} | Region={_currentDrawingRegion.Name}",
                            "RegionEditorControl");
                    }
                }
            }

            ClearRubberBand();
            _isDrawing = false;
            _isDragging = false;
            _isDraggingHandle = false;
            _currentDrawingRegion = null;

            // 清理ROI编辑器风格的手柄状态
            _activeHandleType = Rendering.HandleType.None;
            _originalShapeDefinition = null;
            _rotateStartAngle = null;
            _rotateStartMouseAngle = null;

            // 绘制完成自动回到选择模式
            EnterSelectMode(notifyManager: false);

            RenderRequested?.Invoke();

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[FinishInteraction] 清理完成 | InstanceId={_controlInstanceId} | _isDrawing={_isDrawing} | _currentDrawingRegion={_currentDrawingRegion?.Name ?? "null"}",
                "RegionEditorControl");
        }

        #endregion

        #region 绘制和渲染

        private void UpdateDrawingPreview(Point currentPosition)
        {
            if (_currentDrawingRegion?.Parameters is not ShapeParameters shapeDef)
                return;

            // Log preview update (every 10 times)
            if (_previewLogCount++ % 10 == 0)
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[UpdateDrawingPreview] Count={_previewLogCount} | Position={currentPosition:F1} | Tool={_currentTool}",
                    "RegionEditorControl");
            }

            var dx = currentPosition.X - _startPoint.X;
            var dy = currentPosition.Y - _startPoint.Y;

            switch (_currentTool)
            {
                case ShapeType.Rectangle:
                case ShapeType.RotatedRectangle:
                    shapeDef.CenterX = _startPoint.X + dx / 2;
                    shapeDef.CenterY = _startPoint.Y + dy / 2;
                    shapeDef.Width = Math.Abs(dx);
                    shapeDef.Height = Math.Abs(dy);
                    break;

                case ShapeType.Circle:
                    var radius = Math.Sqrt(dx * dx + dy * dy);
                    shapeDef.Radius = radius;
                    shapeDef.Width = radius * 2;
                    shapeDef.Height = radius * 2;
                    break;

                case ShapeType.Line:
                    shapeDef.EndX = currentPosition.X;
                    shapeDef.EndY = currentPosition.Y;
                    break;
            }
        }

        private void MoveRegion(RegionData region, Vector offset)
        {
            if (region.Parameters is ShapeParameters shapeDef)
            {
                shapeDef.CenterX += offset.X;
                shapeDef.CenterY += offset.Y;
                shapeDef.StartX += offset.X;
                shapeDef.StartY += offset.Y;
                shapeDef.EndX += offset.X;
                shapeDef.EndY += offset.Y;

                // 移动多边形顶点
                for (int i = 0; i < shapeDef.Points.Count; i++)
                {
                    var pt = shapeDef.Points[i];
                    shapeDef.Points[i] = new Point2D(pt.X + offset.X, pt.Y + offset.Y);
                }

                region.MarkModified();
                RegionDataChanged?.Invoke(this, region);
            }
        }

        #region 多边形辅助方法

        /// <summary>
        /// 将 _polygonPoints 同步到 ShapeParameters.Points
        /// </summary>
        private void SyncPolygonToShapeDef()
        {
            if (_currentDrawingRegion?.Parameters is ShapeParameters shapeDef)
            {
                shapeDef.Points.Clear();
                foreach (var pt in _polygonPoints)
                    shapeDef.Points.Add(new Point2D(pt.X, pt.Y));
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

            // 实线：最后一个顶点 -> 鼠标位置
            var line = new System.Windows.Shapes.Line
            {
                X1 = lastPoint.X, Y1 = lastPoint.Y,
                X2 = mousePosition.X, Y2 = mousePosition.Y,
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 2.0 / zoom,
                Tag = RubberBandTag
            };
            Canvas.SetZIndex(line, 50);
            canvas.Children.Add(line);

            // 小圆点：鼠标位置
            var dotSize = 6.0 / zoom;
            var dot = new Ellipse
            {
                Width = dotSize, Height = dotSize,
                Fill = Brushes.DodgerBlue, Tag = RubberBandTag
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
        private void UpdateShapeByHandle(RegionData region, Rendering.HandleType handleType, double dx, double dy)
        {
            if (region.Parameters is not ShapeParameters shapeDef)
                return;

            switch (shapeDef.ShapeType)
            {
                case ShapeType.Rectangle:
                    // 使用ResizeRectangleFromCorner和ResizeRectangleFromEdge方法
                    // 这些方法在HandleShapeEdit中调用
                    break;
                case ShapeType.RotatedRectangle:
                    // 旋转矩形在HandleShapeEdit中通过HandleRotatedRectangleResize处理
                    break;
                case ShapeType.Circle:
                    UpdateCircleByHandle(shapeDef, handleType, dx, dy);
                    break;
                case ShapeType.Line:
                    UpdateLineByHandle(shapeDef, handleType, dx, dy);
                    break;
            }

            region.MarkModified();
            RegionDataChanged?.Invoke(this, region);
        }

        /// <summary>
        /// 更新矩形形状（通过手柄，使用原始状态避免累积误差）
        /// </summary>
        private void UpdateRectangleByHandle(ShapeParameters shapeDef, Rendering.HandleType handleType, double dx, double dy)
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
        private void UpdateCircleByHandle(ShapeParameters shapeDef, Rendering.HandleType handleType, double dx, double dy)
        {
            switch (handleType)
            {
                case Rendering.HandleType.Center:
                    // 中心手柄用于拖动整个圆形
                    shapeDef.CenterX += dx;
                    shapeDef.CenterY += dy;
                    break;
                default:
                    // 圆形手柄用于调整半径
                    var delta = Math.Sqrt(dx * dx + dy * dy);
                    switch (handleType)
                    {
                        case Rendering.HandleType.Top:
                            shapeDef.Radius += dy; // 向上拖动减小半径
                            break;
                        case Rendering.HandleType.Bottom:
                            shapeDef.Radius += dy; // 向下拖动增大半径
                            break;
                        case Rendering.HandleType.Left:
                            shapeDef.Radius += dx; // 向左拖动减小半径
                            break;
                        case Rendering.HandleType.Right:
                            shapeDef.Radius += dx; // 向右拖动增大半径
                            break;
                    }

                    // 确保半径不为负
                    if (shapeDef.Radius < 5) shapeDef.Radius = 5;

                    // 同步更新 Width 和 Height
                    shapeDef.Width = shapeDef.Radius * 2;
                    shapeDef.Height = shapeDef.Radius * 2;
                    break;
            }
        }

        /// <summary>
        /// 更新直线形状（通过手柄）
        /// </summary>
        private void UpdateLineByHandle(ShapeParameters shapeDef, Rendering.HandleType handleType, double dx, double dy)
        {
            switch (handleType)
            {
                case Rendering.HandleType.LineStart:
                    shapeDef.StartX += dx;
                    shapeDef.StartY += dy;
                    break;
                case Rendering.HandleType.LineEnd:
                    shapeDef.EndX += dx;
                    shapeDef.EndY += dy;
                    break;
            }
        }

        /// <summary>
        /// ROI编辑器风格的手柄编辑处理
        /// 参考ROI编辑器的编辑操作逻辑（ROIImageEditor.xaml.cs 1461-1816行）
        /// </summary>
        private void HandleShapeEdit(ShapeParameters shape, Point currentPosition)
        {
            var delta = currentPosition - _handleDragStartPoint;

            // 多边形顶点拖拽
            if (shape.ShapeType == ShapeType.Polygon && _activeHandleType == Rendering.HandleType.PolygonVertex)
            {
                if (_activeVertexIndex >= 0 && _activeVertexIndex < shape.Points.Count)
                {
                    shape.Points[_activeVertexIndex] = new Point2D(
                        currentPosition.X, currentPosition.Y);
                }
                return;
            }

            // 直线端点调整
            if (shape.ShapeType == ShapeType.Line)
            {
                HandleLineEndpoint(shape, currentPosition);
                return;
            }

            // 圆形半径调整
            if (shape.ShapeType == ShapeType.Circle)
            {
                HandleCircleResize(shape, currentPosition);
                return;
            }

            // 旋转矩形调整
            if (shape.ShapeType == ShapeType.RotatedRectangle)
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

            // 普通矩形调整
            if (shape.ShapeType == ShapeType.Rectangle)
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

            // 触发变更事件
            if (_selectedRegion != null)
            {
                _selectedRegion.MarkModified();
                RegionDataChanged?.Invoke(this, _selectedRegion);
            }
        }

        /// <summary>
        /// 处理旋转操作（参考ROI编辑器HandleRotate 1792-1816行）
        /// </summary>
        private void HandleRotate(ShapeParameters shape, Point currentPosition)
        {
            var center = new Point(shape.CenterX, shape.CenterY);

            // 保存旋转开始时的状态（第一次调用时）
            if (_rotateStartAngle == null)
            {
                _rotateStartAngle = shape.Angle;
                _rotateStartMouseAngle = CalculateMouseAngle(center, _handleDragStartPoint);
            }

            // 计算当前鼠标角度
            var currentMouseAngle = CalculateMouseAngle(center, currentPosition);

            // 计算角度增量
            var deltaAngle = currentMouseAngle - _rotateStartMouseAngle.Value;

            // 应用角度增量（使用RotatedRectangleHelper.NormalizeAngle）
            shape.Angle = RotatedRectangleHelper.NormalizeAngle(_rotateStartAngle.Value + deltaAngle);
        }

        /// <summary>
        /// 计算鼠标相对于中心的图像坐标系角度
        /// </summary>
        private double CalculateMouseAngle(Point center, Point mousePos)
        {
            // 计算鼠标相对于中心的数学角度
            // Math.Atan2返回数学角度：逆时针为正，0°向右
            var mathAngle = Math.Atan2(mousePos.Y - center.Y, mousePos.X - center.X) * 180 / Math.PI;

            // 转换为图像坐标系角度（顺时针为正，0°向下）
            // 公式：imageAngle = -mathAngle + 90
            return -mathAngle + 90;
        }

        /// <summary>
        /// 处理旋转矩形大小调整（使用坐标变换，参考ROI编辑器）
        /// </summary>
        private void HandleRotatedRectangleResize(ShapeParameters shape, Point currentPosition, Vector delta)
        {
            if (_originalShapeDefinition == null) return;

            // 1. 将世界坐标系的 delta 转换为局部坐标系的 delta
            var localDelta = WorldToLocalDelta(delta, _originalRotation);

            // 2. 计算原始矩形的四个角点（局部坐标，原点在中心）
            var corners = GetLocalCorners(_originalSize.Width, _originalSize.Height);

            // 3. 根据拖动的手柄计算新的角点位置
            var newCorners = CalculateNewCorners(corners, localDelta, _activeHandleType);

            // 4. 从新角点计算新的尺寸和中心
            var (newWidth, newHeight, newCenterLocal) = CalculateFromCorners(newCorners);

            // 5. 将新中心从局部坐标转换回世界坐标
            var newCenterWorld = LocalToWorldPoint(newCenterLocal, _originalPosition, _originalRotation);

            // 6. 应用新的状态
            shape.Width = newWidth;
            shape.Height = newHeight;
            shape.CenterX = newCenterWorld.X;
            shape.CenterY = newCenterWorld.Y;
        }

        /// <summary>
        /// 将世界坐标系的 delta 转换为局部坐标系的 delta（参考ROI编辑器）
        /// </summary>
        private Vector WorldToLocalDelta(Vector worldDelta, double rotation)
        {
            // 将角度转换为弧度
            var angle = -rotation * Math.PI / 180;
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
            var angle = -rotation * Math.PI / 180;
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
        private void HandleCircleResize(ShapeParameters shape, Point currentPosition)
        {
            var center = new Point(shape.CenterX, shape.CenterY);

            // 计算从中心到鼠标的距离作为新半径
            var dx = currentPosition.X - center.X;
            var dy = currentPosition.Y - center.Y;
            var newRadius = Math.Sqrt(dx * dx + dy * dy);

            if (newRadius < 5) newRadius = 5;

            shape.Radius = newRadius;
            
            // 同步更新 Width 和 Height，确保手柄位置正确
            shape.Width = newRadius * 2;
            shape.Height = newRadius * 2;
        }

        /// <summary>
        /// 处理直线端点调整（参考ROI编辑器HandleLineResize 1466-1470行）
        /// </summary>
        private void HandleLineEndpoint(ShapeParameters shape, Point currentPosition)
        {
            if (_activeHandleType == Rendering.HandleType.LineStart)
            {
                shape.StartX = currentPosition.X;
                shape.StartY = currentPosition.Y;
            }
            else if (_activeHandleType == Rendering.HandleType.LineEnd)
            {
                shape.EndX = currentPosition.X;
                shape.EndY = currentPosition.Y;
            }
        }

        /// <summary>
        /// 从角落调整矩形（不对称编辑 - 固定对角点模式）
        /// 参考ROI编辑器旋转矩形的HandleRotatedRectangleResize逻辑
        /// </summary>
        private void ResizeRectangleFromCorner(ShapeParameters shape, Vector delta, bool isLeft, bool isTop)
        {
            if (_originalShapeDefinition == null) return;

            var original = _originalShapeDefinition;

            // 计算原始矩形的四个角点（局部坐标，原点在中心）
            var hw = original.Width / 2;
            var hh = original.Height / 2;
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

            // 将局部坐标转换为世界坐标
            shape.Width = newWidth;
            shape.Height = newHeight;
            shape.CenterX = original.CenterX + centerLocal.X;
            shape.CenterY = original.CenterY + centerLocal.Y;
        }

        /// <summary>
        /// 从边缘调整矩形（参考ROI编辑器ResizeFromEdge 1760-1790行）
        /// </summary>
        /// <summary>
        /// 从边缘调整矩形（非对称编辑 - 固定对边模式）
        /// 使用与 ResizeRectangleFromCorner 相同的角点计算逻辑
        /// </summary>
        private void ResizeRectangleFromEdge(ShapeParameters shape, Vector delta, bool isVertical, bool isTopOrLeft)
        {
            if (_originalShapeDefinition == null) return;

            var original = _originalShapeDefinition;

            // 计算原始矩形的四个角点（局部坐标，原点在中心）
            var hw = original.Width / 2;
            var hh = original.Height / 2;
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

            // 将局部坐标转换为世界坐标
            shape.Width = newWidth;
            shape.Height = newHeight;
            shape.CenterX = original.CenterX + centerLocal.X;
            shape.CenterY = original.CenterY + centerLocal.Y;
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
        public List<RegionRenderContext> GetRenderContexts()
        {
            var regions = _viewModel?.Regions;
            if (regions == null || regions.Count == 0)
                return new List<RegionRenderContext>();

            var contexts = new List<RegionRenderContext>(regions.Count);
            foreach (var region in regions)
            {
                if (!region.IsVisible) continue;
                if (region.Parameters is not ShapeParameters shapeDef) continue;
                contexts.Add(CreateRenderContextInternal(region, shapeDef, region.IsPreview));
            }
            return contexts;
        }

        /// <summary>
        /// 获取当前绘制预览的渲染上下文（供Manager使用）
        /// </summary>
        public RegionRenderContext? GetPreviewContext()
        {
            if (_isDrawing && _currentDrawingRegion?.Parameters is ShapeParameters previewShape)
            {
                var context = CreateRenderContextInternal(_currentDrawingRegion, previewShape, isPreview: true);

                // ★ 多边形预览：包含临时鼠标位置，让用户看到"点击后会是什么样的"
                if (_currentTool == ShapeType.Polygon && _isDrawingPolygon && _polygonPoints.Count >= 1)
                {
                    context.PolygonPoints.Add(new Point(_lastMouseImagePosition.X, _lastMouseImagePosition.Y));
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
        private RegionRenderContext CreateRenderContextInternal(RegionData region, ShapeParameters shape, bool isPreview = false)
        {

            var context = new RegionRenderContext
            {
                Id = region.Id,
                ShapeType = shape.ShapeType,
                IsSelected = region == _selectedRegion,
                IsPreview = isPreview,
                IsVisible = true,
                RegionType = region.RegionType,

                // 几何参数
                Center = new Point(shape.CenterX, shape.CenterY),
                Width = shape.Width,
                Height = shape.Height,
                Angle = shape.Angle,
                Radius = shape.Radius,
                LineStart = new Point(shape.StartX, shape.StartY),
                LineEnd = new Point(shape.EndX, shape.EndY),
                PolygonPoints = shape.Points?.Select(p => new Point(p.X, p.Y)).ToList() ?? new List<Point>(),

                // 样式参数
                FillColor = Color.FromArgb(
                    (byte)((shape.FillColorArgb >> 24) & 0xFF),
                    (byte)((shape.FillColorArgb >> 16) & 0xFF),
                    (byte)((shape.FillColorArgb >> 8) & 0xFF),
                    (byte)(shape.FillColorArgb & 0xFF)
                ),
                StrokeColor = Color.FromArgb(
                    (byte)((shape.StrokeColorArgb >> 24) & 0xFF),
                    (byte)((shape.StrokeColorArgb >> 16) & 0xFF),
                    (byte)((shape.StrokeColorArgb >> 8) & 0xFF),
                    (byte)(shape.StrokeColorArgb & 0xFF)
                ),
                StrokeThickness = shape.StrokeThickness,
                Opacity = shape.Opacity,

                // 标签 - ★ Phase 4: 屏蔽区域加前缀以区分
                Label = region.RegionType == Models.RegionType.Mask
                    ? $"■ {region.Name}"
                    : $"□ {region.Name}"
            };

            context.CalculateBounds();
            return context;
        }

        /// <summary>
        /// 绘制编辑手柄（ROI编辑器风格）
        /// </summary>
        public void DrawEditHandles(RegionData region)
        {
            // ★ 诊断日志：手柄绘制入口
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[DrawEditHandles] 入口 | Region={region.Name}(Id={region.Id:B}) | Parameters={region.Parameters?.GetType().Name ?? "null"} | IsShapeParams={region.Parameters is ShapeParameters} | OverlayCanvas={OverlayCanvas != null}",
                "RegionEditorControl");

            if (region.Parameters is not ShapeParameters shapeDef || OverlayCanvas == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"[DrawEditHandles] 跳过手柄绘制 | Region={region.Name} | IsShapeParams={region.Parameters is ShapeParameters} | OverlayCanvas={OverlayCanvas != null}",
                    "RegionEditorControl");
                return;
            }

            // 使用ROI编辑器的手柄渲染器创建手柄（反除缩放，使手柄在屏幕上视觉恒定）
            CreateHandlesForShape(shapeDef);

            if (_currentHandles != null && _currentHandles.Length > 0)
            {
                // ★ Phase 4: 根据区域类型传入差异色
                var zoom = CurrentZoom;
                var handleColor = region.RegionType switch
                {
                    Models.RegionType.Mask => new SolidColorBrush(Color.FromArgb(255, 255, 165, 0)),   // 橙色
                    Models.RegionType.Inspection => new SolidColorBrush(Color.FromArgb(255, 0, 120, 215)), // 蓝色
                    _ => Brushes.Orange
                };
                Rendering.HandleRenderer.DrawHandles(OverlayCanvas, _currentHandles, shapeDef.ShapeType, zoom, handleColor);
            }
        }

        /// <summary>
        /// 为形状创建ROI编辑器风格的手柄（强自适应 + 差异化）
        /// </summary>
        private void CreateHandlesForShape(ShapeParameters shape)
        {
            // 传入zoom，各手柄内部根据类型差异化计算尺寸
            var zoom = CurrentZoom;

            // ★ 关键诊断：记录缩放信息
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[CreateHandlesForShape] zoom={zoom:F3}, ShapeType={shape.ShapeType}",
                "RegionEditorControl");

            switch (shape.ShapeType)
            {
                case ShapeType.Rectangle:
                    var bounds = new Rect(
                        shape.CenterX - shape.Width / 2,
                        shape.CenterY - shape.Height / 2,
                        shape.Width,
                        shape.Height
                    );
                    _currentHandles = Rendering.HandleRenderer.CreateRectangleHandles(bounds, zoom);
                    break;

                case ShapeType.Circle:
                    var center = new Point(shape.CenterX, shape.CenterY);
                    _currentHandles = Rendering.HandleRenderer.CreateCircleHandles(center, shape.Radius, zoom);
                    break;

                case ShapeType.RotatedRectangle:
                    var rotatedCenter = new Point(shape.CenterX, shape.CenterY);
                    var corners = RotatedRectangleHelper.GetCorners(
                        rotatedCenter,
                        shape.Width,
                        shape.Height,
                        shape.Angle
                    );
                    _currentHandles = Rendering.HandleRenderer.CreateRotatedRectangleHandles(
                        corners,
                        shape.Angle,
                        rotatedCenter,
                        zoom,
                        shape.Height
                    );
                    break;

                case ShapeType.Line:
                    var startPoint = new Point(shape.StartX, shape.StartY);
                    var endPoint = new Point(shape.EndX, shape.EndY);
                    _currentHandles = Rendering.HandleRenderer.CreateLineHandles(startPoint, endPoint, zoom);
                    break;

                case ShapeType.Polygon:
                    _currentHandles = Rendering.HandleRenderer.CreatePolygonHandles(shape.Points, zoom);
                    break;
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

        private Shape? CreateRegionShape(RegionData region, bool isPreview = false)
        {
            if (region.Parameters is not ShapeParameters shapeDef)
                return null;

            // 使用ShapeRenderer创建形状（参考ROI编辑器）
            var isSelected = region == _selectedRegion;
            var shape = ShapeRenderer.CreateShape(shapeDef, isSelected, isPreview);

            if (shape != null)
            {
                // 根据shape.Tag获取位置信息
                var tag = shape.Tag;
                if (tag != null && tag.GetType().GetProperty("X") != null && tag.GetType().GetProperty("Y") != null)
                {
                    var xProp = tag.GetType().GetProperty("X");
                    var yProp = tag.GetType().GetProperty("Y");
                    var x = (double?)xProp.GetValue(tag);
                    var y = (double?)yProp.GetValue(tag);

                    if (x.HasValue && y.HasValue)
                    {
                        Canvas.SetLeft(shape, x.Value);
                        Canvas.SetTop(shape, y.Value);
                    }
                }
                // 直线不需要Canvas定位
            }

            return shape;
        }

        private void DrawRegionLabel(RegionData region)
        {
            if (OverlayCanvas == null || string.IsNullOrEmpty(region.Name)) return;

            var bounds = GetRegionBounds(region);
            double labelTop = bounds.Top - 18;

            if (labelTop < 0) labelTop = bounds.Bottom + 2;

            var label = new TextBlock
            {
                Text = region.Name,
                FontSize = 12,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(4, 2, 4, 2),
                Tag = region.Id
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double labelWidth = label.DesiredSize.Width;

            double labelLeft = bounds.Left + (bounds.Width - labelWidth) / 2;
            if (labelLeft < 0) labelLeft = 0;

            Canvas.SetLeft(label, labelLeft);
            Canvas.SetTop(label, labelTop);
            OverlayCanvas.Children.Add(label);
        }

        private Rect GetRegionBounds(RegionData region)
        {
            if (region.Parameters is not ShapeParameters shapeDef)
                return Rect.Empty;

            return shapeDef.ShapeType switch
            {
                ShapeType.Rectangle => new Rect(
                    shapeDef.CenterX - shapeDef.Width / 2,
                    shapeDef.CenterY - shapeDef.Height / 2,
                    shapeDef.Width,
                    shapeDef.Height),
                ShapeType.RotatedRectangle => new Rect(
                    shapeDef.CenterX - shapeDef.Width / 2,
                    shapeDef.CenterY - shapeDef.Height / 2,
                    shapeDef.Width,
                    shapeDef.Height),
                ShapeType.Circle => new Rect(
                    shapeDef.CenterX - shapeDef.Radius,
                    shapeDef.CenterY - shapeDef.Radius,
                    shapeDef.Radius * 2,
                    shapeDef.Radius * 2),
                ShapeType.Line => new Rect(
                    Math.Min(shapeDef.StartX, shapeDef.EndX),
                    Math.Min(shapeDef.StartY, shapeDef.EndY),
                    Math.Abs(shapeDef.EndX - shapeDef.StartX),
                    Math.Abs(shapeDef.EndY - shapeDef.StartY)),
                _ => Rect.Empty
            };
        }

        #endregion

        #region 命中测试和选择

        public RegionData? HitTestRegion(Point point, double tolerance = 0)
        {
            // 从后往前测试（后绘制的在上面）
            foreach (var region in _viewModel.Regions.Reverse())
            {
                if (!region.IsVisible) continue;

                if (IsPointInRegion(point, region, tolerance))
                {
                    return region;
                }
            }

            return null;
        }

        private bool IsPointInRegion(Point point, RegionData region, double tolerance)
        {
            if (region.Parameters is not ShapeParameters shapeDef)
                return false;

            return shapeDef.ShapeType switch
            {
                ShapeType.Rectangle => IsPointInRectangle(point, shapeDef, tolerance),
                ShapeType.RotatedRectangle => IsPointInRotatedRectangle(point, shapeDef, tolerance),
                ShapeType.Circle => IsPointInCircle(point, shapeDef, tolerance),
                ShapeType.Line => IsPointNearLine(point, shapeDef, tolerance),
                ShapeType.Polygon => IsPointInPolygon(point, shapeDef, tolerance),
                _ => false
            };
        }

        /// <summary>
        /// 判断点是否在矩形内（支持容差扩展）
        /// </summary>
        private bool IsPointInRectangle(Point point, ShapeParameters shapeDef, double tolerance)
        {
            var bounds = new Rect(
                shapeDef.CenterX - shapeDef.Width / 2 - tolerance,
                shapeDef.CenterY - shapeDef.Height / 2 - tolerance,
                shapeDef.Width + tolerance * 2,
                shapeDef.Height + tolerance * 2
            );
            return bounds.Contains(point);
        }

        /// <summary>
        /// 判断点是否在圆形内（支持容差扩展）
        /// </summary>
        private bool IsPointInCircle(Point point, ShapeParameters shapeDef, double tolerance)
        {
            var distance = Math.Sqrt(
                Math.Pow(point.X - shapeDef.CenterX, 2) +
                Math.Pow(point.Y - shapeDef.CenterY, 2));
            return distance <= shapeDef.Radius + tolerance;
        }

        /// <summary>
        /// 判断点是否在直线附近（基础容差10像素 + 额外容差）
        /// </summary>
        private bool IsPointNearLine(Point point, ShapeParameters shapeDef, double tolerance)
        {
            const double baseTolerance = 10.0;
            return DistanceToLine(point,
                new Point(shapeDef.StartX, shapeDef.StartY),
                new Point(shapeDef.EndX, shapeDef.EndY)) < baseTolerance + tolerance;
        }

        /// <summary>
        /// 判断点是否在旋转矩形内（支持容差扩展）
        /// </summary>
        private bool IsPointInRotatedRectangle(Point point, ShapeParameters shapeDef, double tolerance)
        {
            // 1. 将测试点平移到以矩形中心为原点
            double dx = point.X - shapeDef.CenterX;
            double dy = point.Y - shapeDef.CenterY;

            // 2. 逆旋转角度（弧度），将点变换到矩形的本地坐标系
            // 使用数学角度系统：逆时针为正
            double angleRad = -shapeDef.Angle * Math.PI / 180.0;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);

            // 3. 计算本地坐标（旋转后的坐标）
            double localX = dx * cos - dy * sin;
            double localY = dx * sin + dy * cos;

            // 4. 判断是否在轴对齐矩形内（扩展容差）
            double halfW = shapeDef.Width / 2 + tolerance;
            double halfH = shapeDef.Height / 2 + tolerance;

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

        /// <summary>
        /// 射线法判断点是否在多边形内（支持容差：在多边形边的容差范围内也可命中）
        /// </summary>
        private bool IsPointInPolygon(Point point, ShapeParameters shapeDef, double tolerance)
        {
            var points = shapeDef.Points;
            if (points.Count < 3) return false;

            // 射线法判断点是否在多边形内部
            bool inside = false;
            int j = points.Count - 1;

            for (int i = 0; i < points.Count; j = i++)
            {
                var pi = new Point(points[i].X, points[i].Y);
                var pj = new Point(points[j].X, points[j].Y);

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
                for (int i = 0; i < points.Count; i++)
                {
                    var p1 = new Point(points[i].X, points[i].Y);
                    var p2 = new Point(points[(i + 1) % points.Count].X, points[(i + 1) % points.Count].Y);
                    if (DistanceToLine(point, p1, p2) <= tolerance)
                        return true;
                }
            }

            return false;
        }

        private void SelectRegion(RegionData? region)
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[SelectRegion] 选中区域 | Region={region?.Name ?? "null"}(Id={region?.Id:B}) | Regions.Count={_viewModel.Regions.Count}",
                "RegionEditorControl");

            // 取消旧区域的参数变更订阅
            UnsubscribeShapeParameters(_selectedRegion);

            _selectedRegion = region;
            _viewModel.SelectedRegion = region;
            RenderRequested?.Invoke();

            // 订阅新区域的参数变更（面板→画布自动同步）
            SubscribeShapeParameters(region);

            // 选中区域后，确保焦点回到区域编辑器，以接收键盘事件（如Delete删除区域）
            // 因为选择区域是通过主窗口 ImageControl 的鼠标事件触发的，
            // 点击主窗口会导致焦点离开调试窗口，需要在选中后拉回焦点
            if (region != null)
            {
                Keyboard.Focus(this);
            }
        }

        public void DeselectAll()
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[DeselectAll] 取消所有选中 | _selectedRegion={_selectedRegion?.Name ?? "null"} | Regions.Count={_viewModel.Regions.Count}",
                "RegionEditorControl");

            UnsubscribeShapeParameters(_selectedRegion);

            _selectedRegion = null;
            _viewModel.SelectedRegion = null;

            // 清理ROI编辑器风格的手柄状态
            _currentHandles = null;
            _activeHandleType = Rendering.HandleType.None;
            _originalShapeDefinition = null;
            _rotateStartAngle = null;
            _rotateStartMouseAngle = null;

            RenderRequested?.Invoke();
        }

        #region 形状参数变更订阅（面板→画布自动同步）

        /// <summary>
        /// 取消形状参数变更订阅
        /// </summary>
        private void UnsubscribeShapeParameters(RegionData? region)
        {
            if (region?.Parameters is ShapeParameters shape)
            {
                shape.PropertyChanged -= OnShapeParametersChanged;
                if (shape.ShapeType == ShapeType.Polygon)
                {
                    foreach (var pt in shape.Points)
                        pt.PropertyChanged -= OnPolygonPointPropertyChanged;
                    shape.Points.CollectionChanged -= OnPolygonPointsCollectionChanged;
                }
            }
        }

        /// <summary>
        /// 订阅形状参数变更（面板→画布自动同步）
        /// PropertyChanged 处理 CenterX/Width/Radius 等属性，
        /// Point2D.PropertyChanged + CollectionChanged 处理多边形顶点
        /// </summary>
        private void SubscribeShapeParameters(RegionData? region)
        {
            if (region?.Parameters is ShapeParameters shape)
            {
                shape.PropertyChanged += OnShapeParametersChanged;
                if (shape.ShapeType == ShapeType.Polygon)
                {
                    foreach (var pt in shape.Points)
                        pt.PropertyChanged += OnPolygonPointPropertyChanged;
                    shape.Points.CollectionChanged += OnPolygonPointsCollectionChanged;
                }
            }
        }

        /// <summary>
        /// 形状参数 PropertyChanged 回调（CenterX, Width, Radius 等 → 画布刷新）
        /// </summary>
        private void OnShapeParametersChanged(object? sender, PropertyChangedEventArgs e)
        {
            RenderRequested?.Invoke();
        }

        /// <summary>
        /// 多边形顶点 Point2D.PropertyChanged 回调（X/Y 绑定变更 → 画布刷新）
        /// </summary>
        private void OnPolygonPointPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RenderRequested?.Invoke();
        }

        /// <summary>
        /// 多边形 Points.CollectionChanged 回调（添加/删除/替换顶点后刷新画布 + 重新订阅）
        /// </summary>
        private void OnPolygonPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 新项订阅 PropertyChanged
            if (e.NewItems != null)
            {
                foreach (Point2D pt in e.NewItems)
                    pt.PropertyChanged += OnPolygonPointPropertyChanged;
            }
            // 旧项取消订阅
            if (e.OldItems != null)
            {
                foreach (Point2D pt in e.OldItems)
                    pt.PropertyChanged -= OnPolygonPointPropertyChanged;
            }

            RenderRequested?.Invoke();
        }

        #endregion

        #endregion

        #region ViewModel事件处理

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 监控关键属性变化
            if (e.PropertyName == nameof(RegionEditorViewModel.IsDrawing))
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[OnViewModelPropertyChanged] IsDrawing 变化 | InstanceId={_controlInstanceId} | NewValue={_viewModel.IsDrawing} | _isDrawing={_isDrawing} | IsLoaded={IsLoaded} | IsVisible={IsVisible} | _currentDrawingRegion={_currentDrawingRegion?.Name ?? "null"}",
                    "RegionEditorControl");
            }
            else if (e.PropertyName == nameof(RegionEditorViewModel.SelectedShapeType))
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[OnViewModelPropertyChanged] SelectedShapeType 变化 | InstanceId={_controlInstanceId} | NewValue={_viewModel.SelectedShapeType}",
                    "RegionEditorControl");
            }
            else if (e.PropertyName == nameof(RegionEditorViewModel.CurrentMode))
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[OnViewModelPropertyChanged] CurrentMode 变化 | InstanceId={_controlInstanceId} | NewValue={_viewModel.CurrentMode}",
                    "RegionEditorControl");

                // 切换到订阅模式时取消进行中的绘制
                if (_viewModel.CurrentMode == RegionSourceMode.Subscribe && (_isDrawing || _isDrawingPolygon))
                {
                    if (_isDrawingPolygon)
                    {
                        _isDrawingPolygon = false;
                        _polygonPoints.Clear();
                        ClearRubberBand();
                    }
                    if (_currentDrawingRegion != null)
                        ReleaseRegionIndex(_currentDrawingRegion.Name);
                    _isDrawing = false;
                    _currentDrawingRegion = null;
                    _viewModel.IsDrawing = false;
                    ShapeToolBarControl.ClearSelection();
                    RenderRequested?.Invoke();
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[OnViewModelPropertyChanged] 切换到订阅模式，已取消绘制 | InstanceId={_controlInstanceId}",
                        "RegionEditorControl");
                }
            }
        }

        private void OnRegionChanged(object? sender, RegionChangedEventArgs e)
        {
            // ★ 诊断日志：RegionChanged 事件详情
            var currentRegionNames = _viewModel?.Regions != null
                ? string.Join(", ", _viewModel.Regions.Select(r => $"{r.Name}(Id={r.Id:B},IsVisible={r.IsVisible})"))
                : "null";
            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[OnRegionChanged] ViewModel 触发 RegionChanged | InstanceId={_controlInstanceId} | RegionId={e.Region?.Id:B} | RegionName={e.Region?.Name ?? "null"} | ChangeType={e.ChangeType} | Regions.Count={_viewModel?.Regions?.Count ?? -1} | Regions=[{currentRegionNames}]",
                "RegionEditorControl");

            try
            {
                // 区域删除时释放索引到空洞池
                if (e.ChangeType == RegionChangeType.Removed && e.Region != null)
                {
                    ReleaseRegionIndex(e.Region.Name);
                }
                // 清除所有区域时重置索引管理
                else if (e.ChangeType == RegionChangeType.Cleared)
                {
                    _availableIndices.Clear();
                    _nextRegionIndex = 1;
                }

                RenderRequested?.Invoke();
                if (e.Region != null)
                {
                    RegionDataChanged?.Invoke(this, e.Region);
                }
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[OnRegionChanged] 区域变更处理失败 | InstanceId={_controlInstanceId} | Error={ex.Message}",
                    "RegionEditorControl",
                    ex);
            }
        }

        #endregion

        /// <summary>
        /// 生成区域名称 - 空洞池优先，O(log n)
        /// </summary>
        /// <remarks>
        /// 优先填充被删除区域释放的索引空洞，无空洞时递增。
        /// 例如：已有 区域_1、区域_3，则新建 区域_2（填充空洞）
        /// </remarks>
        private string GenerateRegionName()
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
                nextIndex = _nextRegionIndex++;
            }
            return $"区域_{nextIndex}";
        }

        /// <summary>
        /// 释放区域索引到空洞池 - 删除时调用，O(log n)
        /// </summary>
        private void ReleaseRegionIndex(string regionName)
        {
            if (regionName != null && regionName.StartsWith("区域_"))
            {
                var indexPart = regionName.Substring(3);
                if (int.TryParse(indexPart, out var index) && index > 0)
                {
                    _availableIndices.Add(index);
                }
            }
        }

        /// <summary>
        /// 根据已有区域初始化空洞池 - SetRegionsSource 时调用
        /// </summary>
        private void InitializeRegionIndexFromExisting()
        {
            _availableIndices.Clear();
            _nextRegionIndex = 1;

            var usedIndices = new HashSet<int>();
            foreach (var region in _viewModel.Regions)
            {
                if (region.Name != null && region.Name.StartsWith("区域_"))
                {
                    var indexPart = region.Name.Substring(3);
                    if (int.TryParse(indexPart, out var index) && index > 0)
                    {
                        usedIndices.Add(index);
                    }
                }
            }

            if (usedIndices.Count == 0)
                return;

            var maxIndex = usedIndices.Max();
            _nextRegionIndex = maxIndex + 1;

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
                new(Models.ShapeType.Rectangle, "矩形", "M2,2 L18,2 L18,18 L2,18 Z", Key.R),
                new(Models.ShapeType.Circle, "圆形",
                    "M9,1 A8,8 0 1,0 9,17 A8,8 0 1,0 9,1 Z M9,4 L9,14 M4,9 L14,9", Key.C),
                new(Models.ShapeType.RotatedRectangle, "旋转矩形",
                    "M5,10 L10,3 L19,10 L14,17 Z M17,7 A3,3 0 0,1 20,11", Key.O),
                new(Models.ShapeType.Line, "直线", "M3,17 L17,3", Key.L, isVisible: false),
                new(Models.ShapeType.Polygon, "多边形",
                    "M11,1 L19,6 L18,15 L12,19 L4,16 L2,9 Z", Key.P, toolTip: polygonToolTip)
            };

            ShapeToolBarControl.ShapeTools = shapeTools;
        }

        /// <summary>
        /// 形状工具栏选择变更处理
        /// </summary>
        private void OnShapeToolBarSelectionChanged(object? sender, Models.ShapeType? shapeType)
        {
            if (shapeType.HasValue)
            {
                // 多边形特殊情况
                if (shapeType.Value == Models.ShapeType.Polygon)
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
        private void SelectShapeTool(Models.ShapeType shapeType)
        {
            // 如果当前是绘制模式且点击的是同一个形状 → 退出绘制，回到选择模式
            if (_viewModel.IsDrawing && _currentTool == shapeType)
            {
                // 取消进行中的绘制
                if (_isDrawing || _isDrawingPolygon)
                {
                    if (_currentDrawingRegion != null)
                        ReleaseRegionIndex(_currentDrawingRegion.Name);
                    _isDrawing = false;
                    _isDrawingPolygon = false;
                    _polygonPoints.Clear();
                    _currentDrawingRegion = null;
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
                Models.ShapeType.Rectangle => "矩形",
                Models.ShapeType.Circle => "圆形",
                Models.ShapeType.RotatedRectangle => "旋转矩形",
                Models.ShapeType.Line => "直线",
                Models.ShapeType.Polygon => "多边形",
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
            _currentTool = Models.ShapeType.None;

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

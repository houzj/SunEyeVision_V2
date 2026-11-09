using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using SunEyeVision.UI.Services.Interaction;

using SunEyeVision.UI.Shared;

using SunEyeVision.UI.Core;

using SunEyeVision.UI.Services.Connection;

using SunEyeVision.UI.Services.Rendering;

using SunEyeVision.UI.Services.Path;
using SunEyeVision.UI.Services.PathCalculators;
using SunEyeVision.Foundation.Logging;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Converters.Path;

using SunEyeVision.UI.Shell.Windows;

using SunEyeVision.Foundation.Logging;

using WpfCanvas = System.Windows.Controls.Canvas;



using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas

{

    /// <summary>
    /// WorkflowCanvasControl.xaml 的交互逻辑

    /// 纯画布控件，负责节点和连线的显示、拖拽、连接等交互

    /// </summary>

    public partial class WorkflowCanvasControl : UserControl

    {

        private MainWindowViewModel? _viewModel;

        // 辅助类

        private WorkflowDragDropHandler? _dragDropHandler;


        private PortPositionService? _portPositionService; // 端口位置查询服务



        private bool _isDragging;

        private CanvasNodeProjection? _draggedNode;

        private System.Windows.Point _startDragPosition;

        private System.Windows.Point _initialNodePosition;



        // 框选相关

        private bool _isBoxSelecting;

        private System.Windows.Point _boxSelectStart;

        private System.Windows.Point[]? _selectedNodesInitialPositions;

        // 刚体平移优化：拖动组快照（Down 锁定一次）+ 连接刷新服务（连线逻辑内聚，Move 不再碰 Cache/Batch）
        private ConnectivityRefreshService? _connectivityRefresh;
        private List<CanvasNodeProjection>? _dragGroup;                       // 拖动组快照（仅用于实时写 Position）

        private System.Windows.Point _mouseDownPosition; // 记录鼠标按下时的初始位置（用于拖动阈值判断）



        // 连接模式相关

        private CanvasNodeProjection? _connectionSourceNode = null;



        // 拖拽连接相关

        private bool _isDraggingConnection = false;

        private CanvasNodeProjection? _dragConnectionSourceNode = null;

        private Border? _dragConnectionSourceBorder = null; // 拖拽连接时的源节点Border

        private System.Windows.Point _dragConnectionStartPoint;

        private System.Windows.Point _dragConnectionEndPoint;

        private string? _dragConnectionSourcePort = null; // 记录拖拽开始时的源端口

    private Border? _highlightedTargetBorder = null; // 高亮的目标节点Border（用于恢复原始样式）

    private ExecutionPortShape? _highlightedTargetPort = null; // 高亮的目标端口（ExecutionPortShape）

    private string? _lastHighlightedPort = null; // 上次高亮的端口名称

    private string? _directHitTargetPort = null; // 用户直接命中的目标端口名称

    private Path? _tempConnectionLine = null; // 临时连接线（用于拖拽时显示）

        private PathGeometry? _tempConnectionGeometry = null; // 临时连接线路径几何

        private Border? _highlightedTargetNodeBorder = null; // 拖拽时高亮的目标节点Border（用于显示端口）

        private ScrollViewer? _parentScrollViewer = null; // 父级 ScrollViewer 引用（用于视口边界限制）

        // 平移相关
        private bool _isPanning = false;
        private Point _panStartPosition;
        private Point _panStartPositionInScrollViewer;
        private const double PanSpeedFactor = 1.0; // 平移速度系数（1.5表示平移速度是鼠标移动速度的1.5倍）



        // 连接线路径缓存

        private ConnectionPathCache? _connectionPathCache;



        // 批量延迟更新管理器

        private ConnectionBatchUpdateManager? _batchUpdateManager;



        // 位置节流优化 - 只在移动超过阈值时才触发连接线更新

        private const double PositionUpdateThreshold = 5.0; // 5px 阈值

        private Dictionary<string, Point> _lastReportedNodePositions = new Dictionary<string, Point>();

        // 性能优化组件





        private bool _lastIsDraggingConnection = false; // 上次的拖拽连接状态

        private bool _lastIsBoxSelecting = false; // 上次的框选状态



        /// <summary>

        /// 是否正在拖拽连接（用于绑定，控制连接点是否显示）

        /// </summary>

        public bool IsDraggingConnection

        {

            get => _isDraggingConnection;

            private set

            {

                _isDraggingConnection = value;

                // 不再控制所有端口的可见性，改为只在鼠标悬停节点时显示该节点的端口

            }

        }



        /// <summary>

        /// 是否显示最大外接矩形

        /// </summary>

        public bool ShowBoundingRectangle

        {

            get => (bool)(GetValue(ShowBoundingRectangleProperty) ?? false);

            set => SetValue(ShowBoundingRectangleProperty, value);

        }



        public static readonly DependencyProperty ShowBoundingRectangleProperty =

            DependencyProperty.Register(

                nameof(ShowBoundingRectangle),

                typeof(bool),

                typeof(WorkflowCanvasControl),

                new PropertyMetadata(false, OnShowBoundingRectangleChanged));



        /// <summary>

        /// 是否正在拖拽连接线（用于预览线显示控制）

        /// </summary>

        public static readonly DependencyProperty IsDraggingConnectionProperty =

            DependencyProperty.Register(

                nameof(IsDraggingConnection),

                typeof(bool),

                typeof(WorkflowCanvasControl),

                new PropertyMetadata(false));






        private static void OnShowBoundingRectangleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)

        {

            if (d is WorkflowCanvasControl control)

            {

                control.UpdateBoundingRectangle();

            }

        }



        /// <summary>

        /// 最大外接矩形的源节点ID

        /// </summary>

        public string? BoundingSourceNodeId

        {

            get => (string)GetValue(BoundingSourceNodeIdProperty);

            set => SetValue(BoundingSourceNodeIdProperty, value);

        }



        public static readonly DependencyProperty BoundingSourceNodeIdProperty =

            DependencyProperty.Register(

                nameof(BoundingSourceNodeId),

                typeof(string),

                typeof(WorkflowCanvasControl),

                new PropertyMetadata(null, OnBoundingRectangleChanged));



        /// <summary>

        /// 最大外接矩形的目标节点ID

        /// </summary>

        public string? BoundingTargetNodeId

        {

            get => (string)GetValue(BoundingTargetNodeIdProperty);

            set => SetValue(BoundingTargetNodeIdProperty, value);

        }



        public static readonly DependencyProperty BoundingTargetNodeIdProperty =

            DependencyProperty.Register(

                nameof(BoundingTargetNodeId),

                typeof(string),

                typeof(WorkflowCanvasControl),

                new PropertyMetadata(null, OnBoundingRectangleChanged));



        private static void OnBoundingRectangleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)

        {

            if (d is WorkflowCanvasControl control)

            {

                control.UpdateBoundingRectangle();

            }

        }



        /// <summary>

        /// 获取当前工作流Tab（从 DataContext 获取）

        /// </summary>

        public SunEyeVision.UI.Features.WorkflowUI.WorkflowTabViewModel? CurrentWorkflowTab => DataContext as WorkflowTabViewModel;



        /// <summary>

        /// 获取当前工作流信息（用于转换器）

        /// </summary>

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            BindConnectionProjectionTab(null);
            ReleaseConnectionProjection();
            if (DataContext is not WorkflowTabViewModel tab) return;
            BindConnectionProjectionTab(tab);
            WorkflowCanvas.SetBinding(System.Windows.Controls.Canvas.RenderTransformProperty,
                new System.Windows.Data.Binding("ScaleTransform") { Source=tab, Mode=System.Windows.Data.BindingMode.OneWay });
            ForceRefreshItemsControls();
            RebindConnectionSystem(tab);
        }
        private WorkflowTabViewModel? _connectionProjectionTab;
        private void BindConnectionProjectionTab(WorkflowTabViewModel? tab)
        {
            if (ReferenceEquals(_connectionProjectionTab,tab)) return;
            if (_connectionProjectionTab is {} previous)
            {
                previous.WorkflowContentChanged -= WorkflowTab_WorkflowContentChanged;
                previous.WorkflowContentChanging -= WorkflowTab_WorkflowContentChanging;
            }
            _connectionProjectionTab=tab;
            if (tab is not null)
            {
                tab.WorkflowContentChanged += WorkflowTab_WorkflowContentChanged;
                tab.WorkflowContentChanging += WorkflowTab_WorkflowContentChanging;
            }
        }
        private void WorkflowTab_WorkflowContentChanging(object? sender, EventArgs e) => ReleaseConnectionProjection();
        private void ReleaseConnectionProjection()
        {
            _batchUpdateManager?.Dispose();
            _batchUpdateManager=null;
            if (Resources["SmartPathConverter"] is SmartPathConverter single) single.PathCache=null;
            if (Resources["SmartPathMultiConverter"] is SmartPathMultiConverter multiple) multiple.PathCache=null;
            _connectionPathCache?.Dispose();
            _connectionPathCache=null;
        }
        private void BindPathCacheResources()
        {
            if (Resources["SmartPathConverter"] is SmartPathConverter single) single.PathCache=_connectionPathCache;
            if (Resources["SmartPathMultiConverter"] is SmartPathMultiConverter multiple) multiple.PathCache=_connectionPathCache;
        }

        private void WorkflowTab_WorkflowContentChanged(object? sender, EventArgs e)
        {
            if (sender is WorkflowTabViewModel workflowTab)
            {
                RebindConnectionSystem(workflowTab);
            }
        }

        /// <summary>
        /// 重建连接线系统：用当前集合重建 ConnectionPathCache（内部订阅新集合）、
        /// 更新 SmartPathConverter 静态引用、重建 BatchUpdateManager、重算全部路径。
        /// 修复子程序导航/返回后连接线丢失（缓存引用旧集合）问题。
        /// </summary>
        private void RebindConnectionSystem(WorkflowTabViewModel workflowTab)
        {
            try
            {
                var calculator = Services.PathCalculators.PathCalculatorFactory.CreateCalculator();
                ReleaseConnectionProjection();
                var canvas = workflowTab.DocumentCanvas;
                if (canvas is null) return;
                _connectionPathCache = new ConnectionPathCache(canvas.Nodes, calculator);
                BindPathCacheResources();

                _batchUpdateManager = new ConnectionBatchUpdateManager(_connectionPathCache);
                _batchUpdateManager.SetCurrentTab(workflowTab);

                _connectionPathCache.WarmUp(canvas.Connections);
                RefreshAllConnectionPaths();
            }
            catch (Exception ex)
            {
                VisionLogger.Instance?.Log(LogLevel.Error,
                    $"重新绑定连接系统失败: {ex.Message}", "WorkflowCanvasControl", ex);
            }
        }

        /// <summary>

        /// 初始化渲染调度器（异步节点渲染优化）

        /// </summary>





        /// <summary>

        /// 强制刷新所有 ItemsControl 的 ItemsSource 绑定

        /// 这是修复工作流 Tab 共享同一画布问题的关键

        /// </summary>

        public void ForceRefreshItemsControls()
        {
            var logger = VisionLogger.Instance;
            var itemsControls = FindVisualChildren<ItemsControl>(WorkflowCanvas).ToList();

            logger.Log(LogLevel.Debug, $"ForceRefreshItemsControls|找到 {itemsControls.Count} 个 ItemsControl", "WorkflowCanvas");

            int refreshed = 0;
            int datacontextHack = 0;

            try
            {
                foreach (var itemsControl in itemsControls)
                {
                    var bindingExpression = itemsControl.GetBindingExpression(ItemsControl.ItemsSourceProperty);

                    if (bindingExpression != null)
                    {
                        bindingExpression.UpdateTarget();
                        refreshed++;
                    }
                    else
                    {
                        datacontextHack++;
                        var oldDataContext = itemsControl.DataContext;

                        logger.Log(LogLevel.Debug,
                            $"ForceRefreshItemsControls|DataContextHack: ItemsControl={itemsControl.GetType().Name}, " +
                            $"DataContext={oldDataContext?.GetType().Name ?? "null"}, " +
                            $"ItemsSource={itemsControl.ItemsSource?.GetType().Name ?? "null"}",
                            "WorkflowCanvas");

                        try
                        {
                            itemsControl.DataContext = null;
                            itemsControl.DataContext = oldDataContext;
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("DeferRefresh"))
                        {
                            logger.Log(LogLevel.Error,
                                $"ForceRefreshItemsControls|DeferRefresh冲突(DataContextHack): " +
                                $"ItemsControl={itemsControl.GetType().Name}, " +
                                $"DataContext={oldDataContext?.GetType().Name ?? "null"}, " +
                                $"错误={ex.Message}",
                                "WorkflowCanvas", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error,
                    $"ForceRefreshItemsControls|异常: 刷新={refreshed}, DataContextHack={datacontextHack}, " +
                    $"错误={ex.Message}",
                    "WorkflowCanvas", ex);
            }
        }



        /// <summary>

        /// 查找所有指定类型的子元素

        /// </summary>

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject

        {

            if (depObj == null) yield break;



            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)

            {

                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);

                if (child != null && child is T)

                {

                    yield return (T)child;

                }



                foreach (T childOfChild in FindVisualChildren<T>(child))

                {

                    yield return childOfChild;

                }

            }

        }

        /// <summary>
        /// 查找指定类型的第一个视觉子级
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            return FindVisualChildren<T>(depObj).FirstOrDefault();
        }


        public WorkflowCanvasControl()

        {

            InitializeComponent();
            InitializeCanvasWireEditing();
            InitializeCanvasNodeGestures();
            InitializeCanvasGeometryTracking();
            InitializeCanvasSelection();
            InitializeCanvasClipboard();



            // 验证BoundingRectangle元素



            // 禁用设备像素对齐，启用亚像素渲染

            this.SnapsToDevicePixels = false;

            this.UseLayoutRounding = false;



            // 监听DataContext变化，绑定ScaleTransform

            DataContextChanged += OnDataContextChanged;



            Loaded += WorkflowCanvasControl_Loaded;
            Loaded += (_,_) => { BindConnectionProjectionTab(CurrentWorkflowTab); if (_connectionPathCache is null && CurrentWorkflowTab is {} tab) RebindConnectionSystem(tab); };
            Unloaded += (_,_) => { BindConnectionProjectionTab(null); ReleaseConnectionProjection(); };

            // 初始化临时连接线

            _tempConnectionLine = this.FindName("TempConnectionLine") as Path;

            _tempConnectionGeometry = this.FindName("TempConnectionGeometry") as PathGeometry;

            if (_tempConnectionLine != null)

            {

                _tempConnectionLine.Visibility = Visibility.Collapsed;

            }



            // 初始化辅助类

            _dragDropHandler = new WorkflowDragDropHandler(this);



            // 初始化性能优化组件


        }



        /// <summary>

        /// 初始化优化的渲染组件

        /// </summary>





        private void WorkflowCanvasControl_Loaded(object sender, RoutedEventArgs e)

        {
            // 获取父级 ScrollViewer（用于视口边界限制）
            _parentScrollViewer = FindVisualParent<ScrollViewer>(this);
            // 检查DataContext

            var dataContext = DataContext;
            if (dataContext is WorkflowTabViewModel workflowTab)

            {
            }

            else

            {
            }



            // 添加调试：检查 ItemsControl 的绑定
            var nodesItemsControl = this.FindName("WorkflowCanvas") as WpfCanvas;

            if (nodesItemsControl != null)

            {
            }



            // 获取 MainWindowViewModel

            if (Window.GetWindow(this)?.DataContext is MainWindowViewModel ownerViewModel)

            {

                _viewModel = ownerViewModel;

                if (_viewModel != null)

                {
                    // 注入 TabViewModel 引用到 DragDropHandler（优化 GetCurrentWorkflowTab 性能）
                    if (_dragDropHandler != null && _viewModel.WorkflowTabViewModel != null)
                    {
                        _dragDropHandler.TabViewModel = _viewModel.WorkflowTabViewModel;
                    }

                    // 初始化辅助类（需要ViewModel）








                    // 初始化端口位置查询服务（完全解耦方案）

                    if (_portPositionService == null)

                    {

                        _portPositionService = new PortPositionService(WorkflowCanvas);
                    }

                }

                else

                {
                }

            }



            // 注意：ConnectionPathCache 的初始化已移到 OnDataContextChanged 方法中

            // 这样可以确保在 DataContext 设置后立即初始化，避免 PathCache 为 null 的问题
        }



        /// <summary>

        /// 刷新所有连接的路径（触发重新计算）

        /// </summary>

        private void RefreshAllConnectionPaths()

        {

            if (CurrentWorkflowTab == null) return;



            // 标记所有缓存为脏数据

            if (_connectionPathCache != null)

            {

                _connectionPathCache.MarkAllDirty();

            }



            // 使用 WorkflowPathCalculator 刷新所有连接路径

            if (CurrentWorkflowTab.DocumentCanvas is { } canvas)
                foreach(var connection in canvas.Connections)connection.InvalidatePath();

        }



        /// <summary>

        /// 重新初始化路径计算器（当前只支持贝塞尔曲线计算器）

        /// </summary>

        public void SetPathCalculator(string pathCalculatorType)
        {
            if (CurrentWorkflowTab is {} tab) RebindConnectionSystem(tab);
        }



        #region 节点交互事件



        /// <summary>

        /// 非拖拽状态下的节点命中测试（用于端口显示）

        /// </summary>

        private void TestNodeHitForPortDisplay(Point mousePosition)
        {
            // 使用统一的命中测试方法
            var hitBorder = FindNodeBorderByHitTest(mousePosition);

            // 处理命中结果
            if (hitBorder != null && hitBorder.Tag is CanvasNodeProjection node)
            {
                // 鼠标在节点上
                if (hitBorder != _highlightedTargetNodeBorder)
                {
                    // 隐藏之前高亮的节点端口
                    if (_highlightedTargetNodeBorder != null)
                    {
                        SetPortsVisibility(_highlightedTargetNodeBorder, false);
                    }

                    // 显示当前节点的端口
                    SetPortsVisibility(hitBorder, true);
                    _highlightedTargetNodeBorder = hitBorder;
                }
            }
            else
            {
                // 鼠标不在节点上，隐藏端口
                if (_highlightedTargetNodeBorder != null)
                {
                    SetPortsVisibility(_highlightedTargetNodeBorder, false);
                    _highlightedTargetNodeBorder = null;
                }
            }
        }

        /// <summary>
        /// 通过命中测试查找节点Border（统一的向上遍历逻辑）
        /// </summary>
        /// <param name="mousePosition">鼠标位置</param>
        /// <returns>命中的节点Border（可能是HitAreaBorder或NodeBorder），未命中返回null</returns>
        private Border? FindNodeBorderByHitTest(Point mousePosition)
        {
            // 🔍 调试日志：检查 Canvas 状态
            bool canvasIsLoaded = WorkflowCanvas.IsLoaded;
            bool canvasIsVisible = WorkflowCanvas.IsVisible;
            double canvasWidth = WorkflowCanvas.ActualWidth;
            double canvasHeight = WorkflowCanvas.ActualHeight;

            if (!canvasIsLoaded || !canvasIsVisible || canvasWidth <= 0 || canvasHeight <= 0)
            {
                // 状态异常，不执行命中测试
                return null;
            }

            // 执行命中测试
            var hitTestResult = VisualTreeHelper.HitTest(WorkflowCanvas, mousePosition) as PointHitTestResult;

            if (hitTestResult == null)
            {
                return null;
            }

            // 向上遍历查找 Border（统一的命中测试逻辑）
            var visual = hitTestResult.VisualHit;
            int traverseCount = 0;
            var traversedTypes = new List<string>();

            while (visual != null)
            {
                traverseCount++;
                traversedTypes.Add(visual.GetType().Name);

                // 同时检查 HitAreaBorder 和 NodeBorder
                // 这样可以确保鼠标在整个 HitArea 区域（180x60）内都能命中节点
                if (visual is Border border && (border.Name == "HitAreaBorder" || border.Name == "NodeBorder"))
                {
                    // 修复：Border 在 DataTemplate 内部，Canvas.Left/Top 在父级 ContentPresenter 上
                    var borderPos = GetBorderPosition(border);
                    var borderSize = new Size(border.ActualWidth, border.ActualHeight);
                    var zIndex = System.Windows.Controls.Panel.GetZIndex(border);
                    var isHitTestVisible = border.IsHitTestVisible;
                    var isEnabled = border.IsEnabled;
                    var opacity = border.Opacity;
                    var visibility = border.Visibility;
                    var isLoaded = border.IsLoaded;
                    var isVisible = border.IsVisible;
                    var renderTransform = border.RenderTransform;

                    return border;
                }

                // 向上查找父元素
                visual = VisualTreeHelper.GetParent(visual) as Visual;
            }

            return null;
        }



        /// <summary>

        /// 连接点鼠标进入事件

        /// </summary>

        private void Ellipse_MouseEnter(object sender, MouseEventArgs e)

        {

            // 连接点样式已通过 XAML 处理

        }



        /// <summary>

        /// 连接点鼠标离开事件

        /// </summary>

        private void Ellipse_MouseLeave(object sender, MouseEventArgs e)

        {

            // 连接点样式已通过 XAML 处理

        }



        /// <summary>

        /// 端口鼠标进入事件 - 调试用

        /// </summary>

        private void Port_MouseEnter(object sender, MouseEventArgs e)

        {

            // 移除高频日志

        }



        /// <summary>

        /// 端口鼠标离开事件 - 调试用

        /// </summary>

        private void Port_MouseLeave(object sender, MouseEventArgs e)

        {

            // 移除高频日志

        }



        /// <summary>

        /// 设置所有节点的连接点可见性

        /// </summary>

        private void SetPortsVisibility(bool isVisible)

        {

            if (_viewModel?.WorkflowTabViewModel.SelectedTab == null)

                return;



            // 遍历所有节点并设置连接点可见性

            var selectedTab = _viewModel.WorkflowTabViewModel.SelectedTab;

            var nodeBorders = WorkflowVisualHelper.FindAllVisualChildren<Border>(WorkflowCanvas);



            foreach (var border in nodeBorders)

            {

                if (border.Tag is CanvasNodeProjection node && selectedTab.WorkflowNodes.Contains(node))

                {

                    SetPortsVisibility(border, isVisible);

                }

            }

        }



        /// <summary>

        /// 设置单个节点的连接点可见性

        /// </summary>

        private void SetPortsVisibility(Border border, bool isVisible)

        {

            if (border.Tag is CanvasNodeProjection node)

            {

                // 端口与 NodeBorder 平级，应从统一命中容器查询。
                DependencyObject portScope = border;
                while (portScope is FrameworkElement scope && scope.Name != "HitAreaBorder")
                {
                    var parent = VisualTreeHelper.GetParent(portScope);
                    if (parent == null) break;
                    portScope = parent;
                }
                var ellipses = WorkflowVisualHelper.FindAllVisualChildren<ExecutionPortShape>(portScope);

                foreach (var ellipse in ellipses)

                {

                    var ellipseName = ellipse.Name ?? "";

                    if (ellipseName.Contains("Port"))

                    {

                        // 使用 Visibility.Collapsed 而不是 Opacity，确保端口不响应鼠标事件

                        // Collapsed 的元素不可见且不响应鼠标事件

                        ellipse.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

                    }

                }

            }

        }







        /// <summary>

        /// 节点鼠标左键按下 - 开始拖拽

        /// </summary>

        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)

        {

            if (sender is not Border border || border.Tag is not CanvasNodeProjection node)

                return;

            // The canvas can receive input in the same dispatcher turn in which
            // the new solution's visual tree is loaded. Resolve the host lazily
            // so node selection/opening never dereferences a stale null owner.
            _viewModel ??= Window.GetWindow(this)?.DataContext as MainWindowViewModel;
            if (_viewModel is null)
            {
                e.Handled = true;
                return;
            }



            // 双击事件：打开调试窗口

            if (e.ClickCount == 2)

            {
                _canvasBoxSelectionActive = false; ClearCanvasSelectionFrame();

                if (_viewModel?.WorkflowTabViewModel.SelectedTab != null)

                {

                    foreach (var n in _viewModel.WorkflowTabViewModel.SelectedTab.DocumentCanvas?.Nodes ?? Enumerable.Empty<CanvasNodeProjection>())

                    {

                        n.IsSelected = (n == node);

                    }

                }

                // 使用 ForceSelectNode 强制更新显示（即使引用相同）

                _viewModel.ForceSelectNode(node.Definition);



                // 打开调试窗口

                _viewModel.OpenDebugWindowCommand.Execute(node.Definition);

                // 不设置 e.Handled，让事件冒泡到 Port_MouseLeftButtonUp

            // 设置 e.Handled = true 会导致 Port_MouseLeftButtonUp 无法被触发，

            // 从而导致临时连接线无法隐藏

                return;

            }



            // 调试窗口打开时，禁止切换选中节点
            if (_viewModel?.IsDebugWindowOpen == true)
            {
                e.Handled = true;
                return;
            }

            if (!CanEditCanvas) { e.Handled = true; return; }
            if (!node.IsSelected) { ClearAllSelections(); node.IsSelected = true; }
            _viewModel.SelectedNode = node.Definition;
            WorkflowCanvas.Focus();
            ArmCanvasNodeMove(node, e.GetPosition(WorkflowCanvas));
            e.Handled = true;
        }

        /// <summary>

        /// 节点鼠标左键释放 - 结束拖拽

        /// </summary>

        private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_canvasNodeMove != null && !_canvasNodeOleActive) CancelCanvasNodeMove();
        }

        /// <summary>

        /// 节点鼠标移动 - 执行拖拽（方案5优化：分层更新策略 - 实时位置+延迟路径）

        /// </summary>

        private void Node_MouseMove(object sender, MouseEventArgs e)
        {
            if (_canvasNodeMove != null && !_canvasNodeOleActive && e.LeftButton == MouseButtonState.Pressed)
                StartCanvasNodeMove(e);
        }

        /// <summary>

        /// 判断是否应该触发连接线更新（位置节流）

        /// 只有当节点移动距离超过阈值时才触发更新

        /// </summary>

        private bool ShouldScheduleConnectionUpdate(string nodeId, Point currentPosition)

        {

            // 如果没有记录过该节点的位置，则记录并返回true（首次更新）

            if (!_lastReportedNodePositions.ContainsKey(nodeId))

            {

                _lastReportedNodePositions[nodeId] = currentPosition;

                return true;

            }



            // 计算距离上次报告位置的偏移

            Point lastPosition = _lastReportedNodePositions[nodeId];

            double deltaX = Math.Abs(currentPosition.X - lastPosition.X);

            double deltaY = Math.Abs(currentPosition.Y - lastPosition.Y);



            // 检查是否超过阈值

            if (deltaX > PositionUpdateThreshold || deltaY > PositionUpdateThreshold)

            {

                _lastReportedNodePositions[nodeId] = currentPosition;

                return true;

            }



            return false;

        }



        /// <summary>

        /// 清除位置节流记录（在拖拽结束时调用）

        /// </summary>

        private void ClearPositionThrottling()

        {

            _lastReportedNodePositions.Clear();

        }

        /// <summary>
        /// 刚体平移：锁定拖动组快照（Down 一次）并移交连接刷新服务。
        /// 连线逻辑（组内平移 / 跨组重算）由 ConnectivityRefreshService 统一处理，Move 不再触碰 Cache/Batch。
        /// </summary>
        private void PrepareDragGroup()
        {
            ResetDragGroup();

            if (_viewModel?.WorkflowTabViewModel.SelectedTab == null) return;

            var tab = _viewModel.WorkflowTabViewModel.SelectedTab;

            _dragGroup = tab.WorkflowNodes
                .OfType<CanvasNodeProjection>()
                .Where(n => n.IsSelected)
                .ToList();

            if (_dragGroup.Count == 0)
            {
                ResetDragGroup();
                return;
            }

            // 连接刷新服务（Down 每次新建：随 Tab/缓存生命周期，状态干净）
            _connectivityRefresh = new ConnectivityRefreshService(_connectionPathCache, _batchUpdateManager);
            _connectivityRefresh.BeginDrag(_dragGroup, tab.WorkflowConnections.ToList());
        }

        /// <summary>
        /// 复位拖动组临时状态（Down/Up 均调用）
        /// </summary>
        private void ResetDragGroup()
        {
            _dragGroup = null;
            _connectivityRefresh?.Reset();
            _connectivityRefresh = null;
        }



        /// <summary>

        /// 端口鼠标左键按下 - 开始拖拽连接线

        /// </summary>

        private void Port_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ExecutionPortShape port && port.DataContext is CanvasNodeProjection node && port.Tag is string side)
                BeginCanvasConnection(node, side);
            e.Handled = true;
        }



        /// <summary>

        /// 更新临时连接线路径（优化版）

        /// </summary>

        /// <param name="startPoint">起点（源端口位置）</param>

        /// <param name="endPoint">终点（鼠标位置或目标端口位置）</param>

        /// <param name="targetNode">目标节点（可选）</param>

        /// <param name="targetPortName">目标端口名称（可选）</param>

        private void UpdateTempConnectionPath(Point startPoint, Point endPoint)
        {
            if (_tempConnectionGeometry == null || _dragConnectionSourceNode == null) return;
            var calculator = PathCalculatorFactory.CreateCalculator();
            var sourceDirection = PortDirectionExtensions.FromPortName(_dragConnectionSourcePort ?? "RightPort");
            var points = calculator.CalculatePreviewPath(startPoint, endPoint, sourceDirection, _dragConnectionSourceNode.NodeRect);
            var geometry = calculator.CreatePathGeometry(points);
            _tempConnectionGeometry.Figures.Clear();
            foreach (var figure in geometry.Figures) _tempConnectionGeometry.Figures.Add(figure);
        }

        /// <summary>端口鼠标释放：只消费移动阶段记录的目标。</summary>
        private void Port_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingConnection) { FinishCanvasConnection(e.GetPosition(WorkflowCanvas)); e.Handled = true; }
        }



        /// <summary>

        /// 端口鼠标移动 - 更新临时连接线

        /// </summary>

        private void Port_MouseMove(object sender, MouseEventArgs e)
        {
            // 由画布 PreviewMouseMove 唯一更新，避免端口与父画布双重处理。
        }



        /// <summary>

        /// 根据端口名称获取端口位置

        /// </summary>

        private Point GetPortPositionByName(CanvasNodeProjection node, string portName)

        {

            return portName switch

            {

                "TopPort" => node.TopPortPosition,

                "BottomPort" => node.BottomPortPosition,

                "LeftPort" => node.LeftPortPosition,

                "RightPort" => node.RightPortPosition,

                _ => node.RightPortPosition

            };

        }



        /// <summary>
        /// 获取 Border 的 Canvas 位置（左上角）
        /// Border 在 DataTemplate 内部，Canvas.Left/Top 设置在父级 ContentPresenter 上
        /// </summary>
        /// <param name="border">目标 Border</param>
        /// <returns>Border 左上角的 Canvas 坐标</returns>
        private Point GetBorderPosition(Border border)
        {
            if (border == null)
                return new Point(double.NaN, double.NaN);

            var parent = VisualTreeHelper.GetParent(border);
            if (parent is ContentPresenter contentPresenter)
            {
                double left = WpfCanvas.GetLeft(contentPresenter);
                double top = WpfCanvas.GetTop(contentPresenter);
                return new Point(left, top);
            }

            return new Point(double.NaN, double.NaN);
        }

        /// <summary>
        /// 获取 HitAreaBorder 的中心点位置
        /// HitAreaBorder 尺寸: 180x60，左上角 = 中心点 - (90, 30)
        /// </summary>
        /// <param name="hitAreaBorder">HitAreaBorder</param>
        /// <returns>HitAreaBorder 的中心点坐标</returns>
        private Point GetHitAreaCenterPoint(Border hitAreaBorder)
        {
            Point topLeft = GetBorderPosition(hitAreaBorder);
            if (double.IsNaN(topLeft.X) || double.IsNaN(topLeft.Y))
                return new Point(double.NaN, double.NaN);

            return new Point(topLeft.X + 90, topLeft.Y + 30); // HitArea 尺寸: 180x60，半尺寸: (90, 30)
        }

        /// <summary>
        /// 获取 NodeBorder 的中心点位置
        /// NodeBorder 尺寸: 160x40，居中于 HitAreaBorder
        /// 中心点 = HitArea 中心点
        /// </summary>
        /// <param name="nodeBorder">NodeBorder</param>
        /// <returns>NodeBorder 的中心点坐标</returns>
        private Point GetNodeCenterPoint(Border nodeBorder)
        {
            Point topLeft = GetBorderPosition(nodeBorder);
            if (double.IsNaN(topLeft.X) || double.IsNaN(topLeft.Y))
                return new Point(double.NaN, double.NaN);

            // NodeBorder (160x40) 居中于 HitAreaBorder (180x60)
            // HitArea 左上角 = Node 左上角 - (10, 10)
            // HitArea 中心点 = (Node 左上角 + (80, 20))
            return new Point(topLeft.X + 90, topLeft.Y + 30);
        }



        /// <summary>

        /// 在两个端口之间创建连接

        /// </summary>

        /// <returns>是否成功创建连接</returns>





        /// <summary>

        /// 节点点击事件 - 用于连接或选中

        /// </summary>

        private void Node_ClickForConnection(object sender, RoutedEventArgs e)

        {

            // 获取节点对象（支持 Border 或 ExecutionPortShape 作为 sender）

            CanvasNodeProjection? targetNode = null;



            if (sender is Border border && border.Tag is CanvasNodeProjection clickedNodeFromBorder)

            {

                targetNode = clickedNodeFromBorder;

            }

            else if (sender is ExecutionPortShape ellipse && ellipse.Tag is CanvasNodeProjection clickedNodeFromEllipse)

            {

                targetNode = clickedNodeFromEllipse;



                // 选中当前节点（连接点点击时也需要选中节点）

                if (_viewModel?.WorkflowTabViewModel.SelectedTab != null)

                {

                    foreach (var n in _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes)

                    {

                        n.IsSelected = (n == targetNode);

                    }

                    _viewModel.SelectedNode = targetNode.Definition;

                }

            }

            else

            {

                return;

            }



            if (targetNode == null)

                return;



            // 阻止事件冒泡到节点的点击事件

            // 不设置 e.Handled，让事件冒泡到 Port_MouseLeftButtonUp

            // 设置 e.Handled = true 会导致 Port_MouseLeftButtonUp 无法被触发，

            // 从而导致临时连接线无法隐藏



            // 使用 SelectedTab 的连接模式状态

            var selectedTab = _viewModel?.WorkflowTabViewModel.SelectedTab;

            if (selectedTab == null)

            {

                return;

            }



            // 检查是否在连接模式

            if (_connectionSourceNode == null)

            {

                // 进入连接模式

                _connectionSourceNode = targetNode;

                _viewModel!.StatusText = $"请选择目标节点进行连接，从: {targetNode.Name}";

            }

            else

            {

                // 检查是否是同一个节点

                if (_connectionSourceNode == targetNode)

                {

                    _viewModel!.StatusText = "无法连接到同一个节点";

                    _viewModel.AddLog(LogLevel.Warning, "无法连接到同一个节点", LogSource.UIConnection);

                    _connectionSourceNode = null;

                    return;

                }



                // 检查连接是否已存在（节点点击模式使用硬编码的 RightPort）

                var existingConnection = selectedTab.WorkflowConnections.FirstOrDefault(c =>

                    c.SourceNodeId == _connectionSourceNode!.Id && 

                    c.TargetNodeId == targetNode.Id &&

                    c.SourcePort == "RightPort" && 

                    c.TargetPort == "LeftPort");



                if (existingConnection != null)

                {

                    _viewModel!.StatusText = "连接已存在";

                    _connectionSourceNode = null;

                    return;

                }



                // 创建新连接

                CurrentWorkflowTab?.DocumentCanvas?.Connect(new ConnectionDefinition(Guid.NewGuid().ToString("N"),_connectionSourceNode.Id,targetNode.Id,"RightPort","LeftPort"));



                // 退出连接模式

                _connectionSourceNode = null;

            }

        }



        /// <summary>

        /// 连接点鼠标按下 - 开始拖拽连接

        /// </summary>

        private void Port_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)

        {

            // 移除此方法，统一使用 Port_MouseLeftButtonDown 和 Canvas 事件处理

            // 避免事件处理器冲突

        }



        /// <summary>

        /// 连接点鼠标释放 - 结束拖拽并创建连接

        /// </summary>

        private void Port_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)

        {

            // 移除此方法，统一使用 Port_MouseLeftButtonUp 和 Canvas 事件处理

            // 避免事件处理器冲突

        }



        #endregion



        #region 框选功能



        /// <summary>

        /// Canvas 鼠标左键按下 - 开始框选或清除选择

        /// </summary>

        private void WorkflowCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 先识别交互所有者，不以描边宽度推测连线身份。
            DependencyObject? current = e.OriginalSource as DependencyObject;
            while (current != null && current != WorkflowCanvas)
            {
                if (ReferenceEquals(current, _canvasSelectionFrame) || current is ExecutionPortShape ||
                    current is Border { Tag: CanvasNodeProjection } ||
                    current is Rectangle handle && _canvasWireHandles.Contains(handle) ||
                    current is FrameworkElement { DataContext: CanvasConnectionProjection }) return;
                current = VisualTreeHelper.GetParent(current);
            }
            if (!CanEditCanvas) { e.Handled = true; return; }
            CancelCanvasGesture();
            WorkflowCanvas.Focus();
            _mouseDownPosition = e.GetPosition(WorkflowCanvas);
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                _isPanning = true;
                _panStartPositionInScrollViewer = e.GetPosition(null);
                Cursor = Cursors.Hand;
            }
            else
            {
                ClearAllSelections();
                _isBoxSelecting = true; _boxSelectStart = _mouseDownPosition;
                SelectionBox?.StartSelection(_boxSelectStart);
            }
            WorkflowCanvas.CaptureMouse();
            e.Handled = true;
        }



        /// <summary>

        /// Canvas 鼠标移动 - 更新框选区域

        /// </summary>

        private void WorkflowCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_canvasWireDrag != null) return;
            if (!CanEditCanvas) { CancelCanvasGesture(); return; }
            if (_isDraggingConnection) { PreviewCanvasConnection(e); return; }
            if (_isPanning)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
                var pointer = e.GetPosition(null); var delta = pointer - _panStartPositionInScrollViewer;
                _panStartPositionInScrollViewer = pointer;
                var scroll = FindScrollViewer();
                scroll?.ScrollToHorizontalOffset(scroll.HorizontalOffset - delta.X);
                scroll?.ScrollToVerticalOffset(scroll.VerticalOffset - delta.Y);
                e.Handled = true; return;
            }
            if (!_isBoxSelecting) { TestNodeHitForPortDisplay(e.GetPosition(WorkflowCanvas)); return; }
            SelectionBox?.UpdateSelection(e.GetPosition(WorkflowCanvas));
            var area = SelectionBox?.GetSelectionRect() ?? Rect.Empty;
            var tab = _viewModel?.WorkflowTabViewModel.SelectedTab;
            if (tab == null) return;
            foreach (var node in tab.WorkflowNodes.OfType<CanvasNodeProjection>()) node.IsSelected = area.Contains(node.NodeRect);
            foreach (var connection in tab.WorkflowConnections) connection.IsSelected = false;
            SelectionBox?.SetItemCount(tab.WorkflowNodes.Count(n => n.IsSelected));
            e.Handled = true;
        }



        /// <summary>

        /// Canvas 鼠标左键释放 - 结束框选或创建连接

        /// </summary>

        private void WorkflowCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_canvasWireDrag != null) { FinishCanvasWireDrag(true); e.Handled = true; return; }
            if (_isDraggingConnection) { FinishCanvasConnection(e.GetPosition(WorkflowCanvas)); e.Handled = true; return; }
            if (_isPanning || _isBoxSelecting)
            {
                if (_isBoxSelecting) _canvasBoxSelectionActive = CanvasSelectedNodes().Length > 0;
                _isPanning = false; _isBoxSelecting = false;
                DrawCanvasSelectionFrame();
                SelectionBox?.EndSelection(); Cursor = Cursors.Arrow;
                WorkflowCanvas.ReleaseMouseCapture(); e.Handled = true;
            }
            if (!_canvasNodeOleActive) CancelCanvasNodeMove();
        }



        /// <summary>

        /// 清除所有节点的选中状态

        /// </summary>

        private void ClearAllSelections()

        {
            _canvasBoxSelectionActive = false; ClearCanvasSelectionFrame();

            if (_viewModel?.WorkflowTabViewModel.SelectedTab != null)

            {

                foreach (var node in _viewModel.WorkflowTabViewModel.SelectedTab.DocumentCanvas?.Nodes ?? Enumerable.Empty<CanvasNodeProjection>())

                {

                    node.IsSelected = false;

                }

                // 清除连接线选中
                foreach (var conn in _viewModel.WorkflowTabViewModel.SelectedTab.DocumentCanvas?.Connections ?? Enumerable.Empty<CanvasConnectionProjection>())
                {
                    conn.IsSelected = false;
                }
            }

            // 清除 ViewModel 的 SelectedConnection
            if (_viewModel != null)
            {
                _viewModel.SelectedConnection = null;
            }

        }



        /// <summary>

        /// 记录选中节点的初始位置

        /// </summary>

        private void RecordSelectedNodesPositions()

        {

            if (_viewModel?.WorkflowTabViewModel.SelectedTab == null) return;



            var selectedNodes = _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes

                .Where(n => n.IsSelected)

                .ToList();



            _selectedNodesInitialPositions = selectedNodes

                .Select(n => n.Position.ToWpf())

                .ToArray();

        }



        #endregion



        #region 拖放事件



        private void WorkflowCanvas_DragEnter(object sender, DragEventArgs e)

        {

            _dragDropHandler?.Canvas_DragEnter(sender, e);

        }



        private void WorkflowCanvas_DragOver(object sender, DragEventArgs e)

        {

            _dragDropHandler?.Canvas_DragOver(sender, e);

        }



        private void WorkflowCanvas_DragLeave(object sender, DragEventArgs e)

        {

            _dragDropHandler?.Canvas_DragLeave(sender, e);

        }



        private void WorkflowCanvas_Drop(object sender, DragEventArgs e)

        {

            _dragDropHandler?.Canvas_Drop(sender, e);

        }


        /// <summary>
        /// Canvas 获得焦点
        /// </summary>

        private void WorkflowCanvas_GotFocus(object sender, RoutedEventArgs e)

        {



            Foundation.Logging.VisionLogger.Instance?.Log(

                Foundation.Logging.LogLevel.Info,

                $"[Canvas_GotFocus] 画布获得焦点 - 节点数: {_viewModel?.WorkflowTabViewModel?.SelectedTab?.WorkflowNodes.Count ?? 0}",

                "WorkflowCanvasControl");

        }


        /// <summary>
        /// Canvas 失去焦点
        /// </summary>

        private void WorkflowCanvas_LostFocus(object sender, RoutedEventArgs e)

        {

            Foundation.Logging.VisionLogger.Instance?.Log(

                Foundation.Logging.LogLevel.Warning,

                $"[Canvas_LostFocus] 画布失去焦点 - 节点数: {_viewModel?.WorkflowTabViewModel?.SelectedTab?.WorkflowNodes.Count ?? 0}",

                "WorkflowCanvasControl");

        }


        #endregion



        #region 辅助方法

        /// <summary>
        /// 判断连接线是否与矩形区域相交（粗筛 + 精筛两阶段优化）
        /// </summary>
        private bool IsConnectionInRect(CanvasConnectionProjection connection, Rect selectionRect)
        {
            if (selectionRect.IsEmpty) return false;

            // ========== 第一阶段：快速粗筛 ==========

            // 粗筛1：边界框检测（极快，O(1)）
            var pathGeometry = _connectionPathCache?.GetPath(connection);
            if (pathGeometry == null) return false;

            var bounds = pathGeometry.Bounds;
            if (!bounds.IntersectsWith(selectionRect))
            {
                // 边界框完全不相交，直接排除（过滤70-90%的连接线）
                return false;
            }

            // 粗筛2：起止点同侧检测（快，O(1)，补充过滤10-20%）
            var startPoint = GetPathGeometryStartPoint(pathGeometry);
            var endPoint = GetPathGeometryEndPoint(pathGeometry);

            if (BothPointsOutsideSameSide(startPoint, endPoint, selectionRect))
            {
                // 起止点都在选择框的同一侧且在外部，可以快速排除
                return false;
            }

            // ========== 第二阶段：精确精筛 ==========

            // 仅对通过粗筛的连接线展平路径（减少90%的展平操作）
            var flattened = pathGeometry.GetFlattenedPathGeometry(0.5, ToleranceType.Relative);

            foreach (var figure in flattened.Figures)
            {
                var start = figure.StartPoint;
                foreach (var segment in figure.Segments)
                {
                    Point? end = null;

                    // 处理 LineSegment（单一线段）
                    if (segment is LineSegment lineSeg)
                    {
                        end = lineSeg.Point;
                    }
                    // 处理 PolyLineSegment（多段折线）
                    else if (segment is PolyLineSegment polyLineSeg)
                    {
                        // 遍历 PolyLineSegment 的所有点
                        foreach (var point in polyLineSeg.Points)
                        {
                            end = point;
                            if (LineSegmentIntersectsRect(start, end.Value, selectionRect))
                                return true;
                            start = end.Value;
                        }
                        continue;
                    }

                    // 处理 ArcSegment（圆弧段，理论上已被展平）
                    else if (segment is ArcSegment arcSeg)
                    {
                        end = arcSeg.Point;
                    }
                    // 处理 QuadraticBezierSegment（二次贝塞尔，理论上已被展平）
                    else if (segment is QuadraticBezierSegment quadSeg)
                    {
                        end = quadSeg.Point2;
                    }
                    // 处理 BezierSegment（三次贝塞尔，理论上已被展平）
                    else if (segment is BezierSegment bezierSeg)
                    {
                        end = bezierSeg.Point3;
                    }

                    // 检测线段相交
                    if (end.HasValue && LineSegmentIntersectsRect(start, end.Value, selectionRect))
                        return true;

                    // 更新起点
                    if (end.HasValue)
                        start = end.Value;
                }

                // 如果 Figure 是闭合的，检测最后一个点回到起点的线段
                if (figure.IsClosed)
                {
                    if (LineSegmentIntersectsRect(start, figure.StartPoint, selectionRect))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取 PathGeometry 的起点
        /// </summary>
        private static Point GetPathGeometryStartPoint(PathGeometry pathGeometry)
        {
            if (pathGeometry?.Figures.Count > 0)
            {
                return pathGeometry.Figures[0].StartPoint;
            }
            return new Point(0, 0);
        }

        /// <summary>
        /// 获取 PathGeometry 的终点
        /// </summary>
        private static Point GetPathGeometryEndPoint(PathGeometry pathGeometry)
        {
            if (pathGeometry?.Figures.Count > 0)
            {
                var figure = pathGeometry.Figures[0];
                if (figure.Segments.Count > 0)
                {
                    // 如果是 PolyLineSegment，返回最后一个点
                    if (figure.Segments[figure.Segments.Count - 1] is PolyLineSegment polyLineSeg &&
                        polyLineSeg.Points.Count > 0)
                    {
                        return polyLineSeg.Points[polyLineSeg.Points.Count - 1];
                    }

                    // 其他情况，使用 Segment 的 Point 或 Point3
                    if (figure.Segments[figure.Segments.Count - 1] is LineSegment lineSeg)
                        return lineSeg.Point;
                    if (figure.Segments[figure.Segments.Count - 1] is ArcSegment arcSeg)
                        return arcSeg.Point;
                    if (figure.Segments[figure.Segments.Count - 1] is QuadraticBezierSegment quadSeg)
                        return quadSeg.Point2;
                    if (figure.Segments[figure.Segments.Count - 1] is BezierSegment bezierSeg)
                        return bezierSeg.Point3;
                }
            }
            return new Point(0, 0);
        }

        /// <summary>
        /// 判断两点是否都在选择框的同一侧（用于粗筛）
        /// </summary>
        /// <remarks>
        /// 如果两点都在同一侧（都在左侧、右侧、上侧、下侧），可以快速排除
        /// 因为连接线不太可能穿过选择框
        /// </remarks>
        private static bool BothPointsOutsideSameSide(Point p1, Point p2, Rect rect)
        {
            // 判断点是否在矩形的某一侧
            bool p1Left = p1.X < rect.Left;
            bool p1Right = p1.X > rect.Right;
            bool p1Top = p1.Y < rect.Top;
            bool p1Bottom = p1.Y > rect.Bottom;

            bool p2Left = p2.X < rect.Left;
            bool p2Right = p2.X > rect.Right;
            bool p2Top = p2.Y < rect.Top;
            bool p2Bottom = p2.Y > rect.Bottom;

            // 如果两点都在同一侧（都在左侧、右侧、上侧、下侧）
            // 可以快速排除（因为连接线不太可能穿过选择框）
            return (p1Left && p2Left) || (p1Right && p2Right) ||
                   (p1Top && p2Top) || (p1Bottom && p2Bottom);
        }

        /// <summary>
        /// 判断线段是否与矩形相交
        /// </summary>
        private static bool LineSegmentIntersectsRect(Point p1, Point p2, Rect rect)
        {
            // 情况1：任一端点在矩形内
            if (rect.Contains(p1) || rect.Contains(p2))
                return true;

            // 情况2：线段与矩形四条边相交
            var topLeft = new Point(rect.Left, rect.Top);
            var topRight = new Point(rect.Right, rect.Top);
            var bottomLeft = new Point(rect.Left, rect.Bottom);
            var bottomRight = new Point(rect.Right, rect.Bottom);

            return LineSegmentsIntersect(p1, p2, topLeft, topRight) ||
                   LineSegmentsIntersect(p1, p2, topRight, bottomRight) ||
                   LineSegmentsIntersect(p1, p2, bottomRight, bottomLeft) ||
                   LineSegmentsIntersect(p1, p2, bottomLeft, topLeft);
        }

        /// <summary>
        /// 判断两条线段是否相交（跨乘积法）
        /// </summary>
        private static bool LineSegmentsIntersect(Point p1, Point p2, Point p3, Point p4)
        {
            double d1 = CrossProduct(p3, p4, p1);
            double d2 = CrossProduct(p3, p4, p2);
            double d3 = CrossProduct(p1, p2, p3);
            double d4 = CrossProduct(p1, p2, p4);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;

            if (Math.Abs(d1) < double.Epsilon && IsOnSegment(p3, p4, p1)) return true;
            if (Math.Abs(d2) < double.Epsilon && IsOnSegment(p3, p4, p2)) return true;
            if (Math.Abs(d3) < double.Epsilon && IsOnSegment(p1, p2, p3)) return true;
            if (Math.Abs(d4) < double.Epsilon && IsOnSegment(p1, p2, p4)) return true;

            return false;
        }

        private static double CrossProduct(Point p1, Point p2, Point p)
        {
            return (p2.X - p1.X) * (p.Y - p1.Y) - (p2.Y - p1.Y) * (p.X - p1.X);
        }

        private static bool IsOnSegment(Point p1, Point p2, Point p)
        {
            return p.X >= Math.Min(p1.X, p2.X) && p.X <= Math.Max(p1.X, p2.X) &&
                   p.Y >= Math.Min(p1.Y, p2.Y) && p.Y <= Math.Max(p1.Y, p2.Y);
        }








        /// <summary>

        /// 判断点击的端口并设置起始点

        /// </summary>

        private string? DetermineClickedPort(CanvasNodeProjection node, Point clickPoint)

        {

            double nodeCenterX = node.Position.X + 70;

            double nodeCenterY = node.Position.Y + 45;

            double offsetX = clickPoint.X - nodeCenterX;

            double offsetY = clickPoint.Y - nodeCenterY;



            string? clickedPort = null;

            if (Math.Abs(offsetX) > Math.Abs(offsetY))

            {

                if (offsetX > 0)

                {

                    clickedPort = "RightPort";

                    _dragConnectionStartPoint = node.RightPortPosition;

                }

                else

                {

                    clickedPort = "LeftPort";

                    _dragConnectionStartPoint = node.LeftPortPosition;

                }

            }

            else

            {

                if (offsetY > 0)

                {

                    clickedPort = "BottomPort";

                    _dragConnectionStartPoint = node.BottomPortPosition;

                }

                else

                {

                    clickedPort = "TopPort";

                    _dragConnectionStartPoint = node.TopPortPosition;

                }

            }



            _dragConnectionSourcePort = clickedPort;

            return clickedPort;

        }







        #endregion



        /// <summary>

        /// 获取节点指定端口的Ellipse元素

        /// </summary>

        private ExecutionPortShape? GetPortElement(Border nodeBorder, string portName)

        {

            if (nodeBorder == null) return null;



            // 根据端口名称构造Ellipse名称（例如："LeftPort" -> "LeftPortEllipse"）

            string ellipseName = portName + "Ellipse";



            // 在节点Border的视觉树中查找指定名称的端口

            var visualChildren = WorkflowVisualHelper.FindAllVisualChildren<DependencyObject>(nodeBorder);



            // 查找包含端口名称的元素（通过Name属性或Tag）

            foreach (var child in visualChildren)

            {

                if (child is FrameworkElement element && element.Name == ellipseName)

                {

                    return element as ExecutionPortShape;

                }

            }



            return null;

        }



        /// <summary>

        /// Path元素加载事件 - 监控连接线路径创建

        /// </summary>

        private void Path_Loaded(object sender, RoutedEventArgs e)

        {

            if (sender is Path path && path.DataContext is CanvasConnectionProjection connection)

            {



                if (path.Data is PathGeometry geom && geom.Figures.Count > 0)

                {

                }

                else

                {

                }

            }

        }



        /// <summary>

        /// Path的DataContext变化事件 - 监控连接数据更新

        /// </summary>

        private void Path_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)

        {

            if (sender is Path path)

            {

                if (e.NewValue is CanvasConnectionProjection newConn)

                {

                }

            }

        }



        /// <summary>
        /// 连接线命中区域悬停进入
        /// </summary>
        private void ConnectionHitArea_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Path hitPath)
            {
                var parentCanvas = hitPath.Parent as System.Windows.Controls.Canvas;
                if (parentCanvas?.DataContext is CanvasConnectionProjection connection)
                {
                    connection.IsHovered = true;
                }
            }
        }

        /// <summary>
        /// 连接线命中区域悬停离开
        /// </summary>
        private void ConnectionHitArea_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Path hitPath)
            {
                var parentCanvas = hitPath.Parent as System.Windows.Controls.Canvas;
                if (parentCanvas?.DataContext is CanvasConnectionProjection connection)
                {
                    connection.IsHovered = false;
                }
            }
        }

        /// <summary>
        /// 连接线命中区域点击 - 选中/取消选中连接线 + 切换中间点显示
        /// </summary>
        private void ConnectionHitArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Path hitPath) return;

            // DataContext 绑定在父 Canvas 上，需要向上查找
            var parentCanvas = hitPath.Parent as System.Windows.Controls.Canvas;
            if (parentCanvas?.DataContext is not CanvasConnectionProjection connection) return;

            var tab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (tab?.DocumentCanvas == null) return;

            // 原件连接由焦点决定，不按Ctrl/Shift切换成多条连接选择；节点组选可保留。
            foreach (var edge in tab.DocumentCanvas.Connections) edge.IsSelected = ReferenceEquals(edge, connection);

            SelectCanvasWire(connection,hitPath);

            // 阻止事件冒泡，避免触发框选
            e.Handled = true;
        }





        /// <summary>

        /// 更新最大外接矩形的显示

        /// </summary>

        private void UpdateBoundingRectangle()

        {

            if (!ShowBoundingRectangle)

            {

                BoundingRectangle.Visibility = Visibility.Collapsed;

                return;

            }



            // 查找源节点和目标节点

            CanvasNodeProjection? sourceNode = null;

            CanvasNodeProjection? targetNode = null;



            if (CurrentWorkflowTab?.WorkflowNodes != null)

            {

                if (!string.IsNullOrEmpty(BoundingSourceNodeId))

                {

                    sourceNode = CurrentWorkflowTab.WorkflowNodes.OfType<CanvasNodeProjection>().FirstOrDefault(n => n.Id == BoundingSourceNodeId);

                }



                if (!string.IsNullOrEmpty(BoundingTargetNodeId))

                {

                    targetNode = CurrentWorkflowTab.WorkflowNodes.OfType<CanvasNodeProjection>().FirstOrDefault(n => n.Id == BoundingTargetNodeId);

                }

            }



            // 如果找到了源节点和目标节点，计算并显示矩形

            if (sourceNode != null && targetNode != null)

            {

                double sourceLeft = sourceNode.Position.X;

                double sourceRight = sourceNode.Position.X + 140; // NodeWidth

                double sourceTop = sourceNode.Position.Y;

                double sourceBottom = sourceNode.Position.Y + 36; // NodeHeight



                double targetLeft = targetNode.Position.X;

                double targetRight = targetNode.Position.X + 140;

                double targetTop = targetNode.Position.Y;

                double targetBottom = targetNode.Position.Y + 36;





                // 计算包围两个节点的原始矩形

                double minX = Math.Min(sourceLeft, targetLeft);

                double maxX = Math.Max(sourceRight, targetRight);

                double minY = Math.Min(sourceTop, targetTop);

                double maxY = Math.Max(sourceBottom, targetBottom);



                // 计算矩形的宽度和高度

                double rectWidth = maxX - minX;

                double rectHeight = maxY - minY;



                // 使用最大边长作为正方形的边长,增加搜索范围

                double maxSide = Math.Max(rectWidth, rectHeight);



                // 以源节点和目标节点的中心点为基准,构建正方形搜索区域

                double centerX = (minX + maxX) / 2;

                double centerY = (minY + maxY) / 2;



                // 设置正方形的位置和大小

                double rectX = centerX - maxSide / 2;

                double rectY = centerY - maxSide / 2;



                System.Windows.Controls.Canvas.SetLeft(BoundingRectangle, rectX);

                System.Windows.Controls.Canvas.SetTop(BoundingRectangle, rectY);

                BoundingRectangle.Width = maxSide;

                BoundingRectangle.Height = maxSide;



                BoundingRectangle.Visibility = Visibility.Visible;



            }

            else

            {

                BoundingRectangle.Visibility = Visibility.Collapsed;

            }

        }

        /// <summary>
        /// 查找父级 ScrollViewer
        /// </summary>
        private ScrollViewer? FindScrollViewer()
        {
            return FindVisualParent<ScrollViewer>(this);
        }

        /// <summary>
        /// 查找指定类型的视觉父级
        /// </summary>
        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            if (parentObject is T parent)
                return parent;

            return FindVisualParent<T>(parentObject);
        }

        #region 节点重命名

        /// <summary>
        /// 右键菜单 - 重命名节点
        /// </summary>
        private void NodeContextMenu_Rename_Click(object sender, RoutedEventArgs e)
        {
            e.Handled=true;
            if(sender is not MenuItem {Parent:ContextMenu {PlacementTarget:FrameworkElement placementTarget}} || placementTarget.DataContext is not CanvasNodeProjection node || !BeginCanvasNodeRename(node))return;
            Dispatcher.BeginInvoke(new Action(()=>
            {
                if(!ValidCanvasNodeRename(node)){CancelCanvasNodeRename();return;}
                var textBox=FindVisualChild<TextBox>(placementTarget);
                if(textBox!=null){textBox.Focus();textBox.SelectAll();}
            }),System.Windows.Threading.DispatcherPriority.Input);
        }

        private void NodeRenameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if(sender is not TextBox {DataContext:CanvasNodeProjection node})return;
            if(e.Key==Key.Enter){CommitCanvasNodeRename(node,false);e.Handled=true;}
            else if(e.Key==Key.Escape){CancelCanvasNodeRename();e.Handled=true;}
        }
        private void NodeRenameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if(sender is TextBox {DataContext:CanvasNodeProjection node}&&node.IsEditingName)CommitCanvasNodeRename(node,true);
        }

        /// <summary>
        /// 子程序设置按钮点击 - 打开子程序参数配置对话框
        /// </summary>
        private void SubroutineNodeSettings_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var canEdit=CanEditCanvas;
            if (!canEdit && _viewModel?.IsDebugWindowOpen != true) return;

            if (sender is FrameworkElement element && element.DataContext is CanvasNodeProjection node)
            {
                if(canEdit)
                {
                    node.IsSelected = true;
                    if (_viewModel != null) _viewModel.SelectedNode = node.Definition;
                }
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.OpenSubroutineNodeParameterDialog(node);
                }
            }
        }


        #endregion

    }

}


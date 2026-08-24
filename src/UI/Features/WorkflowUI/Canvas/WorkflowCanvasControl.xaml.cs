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
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Converters.Path;

using SunEyeVision.UI.Shell.Windows;

using SunEyeVision.Plugin.SDK.Logging;

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

        // 已注册的子程序齿轮按钮（主题切换时重新诊断，用于两主题可见性对比）
        private readonly List<Button> _gearButtons = new();

        // 主题切换诊断（两主题对比用），由构造函数订阅/取消
        private SunEyeVision.UI.Services.ThemeManager? _themeManagerForGearDiagnosis;

        // 辅助类

        private WorkflowDragDropHandler? _dragDropHandler;

        private WorkflowConnectionCreator? _connectionCreator;

        private PortPositionService? _portPositionService; // 端口位置查询服务



        private bool _isDragging;

        private WorkflowNode? _draggedNode;

        private System.Windows.Point _startDragPosition;

        private System.Windows.Point _initialNodePosition;



        // 框选相关

        private bool _isBoxSelecting;

        private System.Windows.Point _boxSelectStart;

        private System.Windows.Point[]? _selectedNodesInitialPositions;

        private System.Windows.Point _mouseDownPosition; // 记录鼠标按下时的初始位置（用于拖动阈值判断）



        // 连接模式相关

        private WorkflowNode? _connectionSourceNode = null;



        // 拖拽连接相关

        private bool _isDraggingConnection = false;

        private WorkflowNode? _dragConnectionSourceNode = null;

        private Border? _dragConnectionSourceBorder = null; // 拖拽连接时的源节点Border

        private System.Windows.Point _dragConnectionStartPoint;

        private System.Windows.Point _dragConnectionEndPoint;

        private string? _dragConnectionSourcePort = null; // 记录拖拽开始时的源端口

    private Border? _highlightedTargetBorder = null; // 高亮的目标节点Border（用于恢复原始样式）

    private Ellipse? _highlightedTargetPort = null; // 高亮的目标端口（Ellipse）

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
        private const double PanSpeedFactor = 1.3; // 平移速度系数（1.5表示平移速度是鼠标移动速度的1.5倍）



        // 连接线路径缓存

        private ConnectionPathCache? _connectionPathCache;



        // 批量延迟更新管理器

        private ConnectionBatchUpdateManager? _batchUpdateManager;



        // 位置节流优化 - 只在移动超过阈值时才触发连接线更新

        private const double PositionUpdateThreshold = 5.0; // 5px 阈值

        private Dictionary<string, Point> _lastReportedNodePositions = new Dictionary<string, Point>();

        // 性能优化组件
        private NodeUIPool? _nodeUIPool;





        private bool _lastIsDraggingConnection = false; // 上次的拖拽连接状态

        private bool _lastIsBoxSelecting = false; // 上次的框选状态
        private NodeRenderScheduler? _renderScheduler;



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

        public SunEyeVision.UI.Features.WorkflowUI.WorkflowInfo? CurrentWorkflow

        {

            get

            {

                var tab = CurrentWorkflowTab;

                if (tab == null) return null;

                return new SunEyeVision.UI.Features.WorkflowUI.WorkflowInfo { Id = tab.Id, Name = tab.Name };

            }

        }



        /// <summary>

        /// DataContext变化时绑定ScaleTransform并初始化ConnectionPathCache

        /// </summary>

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)

        {

            if (DataContext is WorkflowTabViewModel workflowTab)

            {
                // 将WorkflowCanvas的RenderTransform绑定到ViewModel的ScaleTransform

                var binding = new System.Windows.Data.Binding("ScaleTransform")

                {

                    Source = workflowTab,

                    Mode = System.Windows.Data.BindingMode.OneWay

                };

                WorkflowCanvas.SetBinding(System.Windows.Controls.Canvas.RenderTransformProperty, binding);
                // 初始化渲染调度器（用于异步节点渲染）

                InitializeRenderScheduler(workflowTab);



                // 设置SmartPathConverter的节点集合和连接集合

                SmartPathConverter.Nodes = workflowTab.WorkflowNodes;

                SmartPathConverter.Connections = workflowTab.WorkflowConnections;
                // ?? 关键修复：强制刷新所有 ItemsControl 的 ItemsSource 绑定
                ForceRefreshItemsControls();



                // 每次 DataContext 变化时都重新创建 ConnectionPathCache（确保每个工作流独立）
                try

                {

                    var calculator = Services.PathCalculators.PathCalculatorFactory.CreateCalculator();
                    // 每次都创建新的 ConnectionPathCache，确保每个工作流独立

                    _connectionPathCache = new ConnectionPathCache(

                        workflowTab.WorkflowNodes,

                        calculator

                    );
                    // 设置SmartPathConverter的PathCache引用

                    SmartPathConverter.PathCache = _connectionPathCache;
                    // 初始化批量延迟更新管理器

                    if (_connectionPathCache != null)

                    {

                        _batchUpdateManager = new ConnectionBatchUpdateManager(_connectionPathCache);

                        _batchUpdateManager.SetCurrentTab(workflowTab);
                    }



                        // 预热缓存

                        _connectionPathCache.WarmUp(workflowTab.WorkflowConnections);

                        var stats = _connectionPathCache.GetStatistics();
                        // 订阅连接集合变化事件

                        if (workflowTab.WorkflowConnections != null)

                        {

                            workflowTab.WorkflowConnections.CollectionChanged += (s, args) =>

                            {
                                UpdateBoundingRectangle();

                                if (_connectionPathCache != null)

                                {

                                    _connectionPathCache.MarkAllDirty();

                                }

                            };

                        }



                        // 订阅节点集合变化事件

                        if (workflowTab.WorkflowNodes is ObservableCollection<WorkflowNodeBase> nodesCollection)

                        {

                            nodesCollection.CollectionChanged += (s, args) =>

                            {

                                SmartPathConverter.Nodes = workflowTab.WorkflowNodes;

                                RefreshAllConnectionPaths();

                                if (_connectionPathCache != null)

                                {

                                    if (args.NewItems != null)

                                    {

                                        foreach (WorkflowNodeBase node in args.NewItems)

                                        {

                                            _connectionPathCache.MarkNodeDirty(node.Id);

                                            // 🔍 关键日志：记录新节点添加
                                            Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                                                Plugin.SDK.Logging.LogLevel.Info,
                                                $"[CollectionChanged] 新节点添加 - 节点ID: {node.Id}, 节点名称: {node.Name}, 位置: ({node.Position.X:F1}, {node.Position.Y:F1}), 节点总数: {workflowTab.WorkflowNodes.Count}, Canvas聚焦={WorkflowCanvas.IsFocused}",
                                                "WorkflowCanvasControl");

                                            // 🔍 调试日志：尝试查找新节点对应的UI元素
                                            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                                            {
                                                // 📌 关键日志：记录查找时刻Canvas状态
                                                Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                                                    Plugin.SDK.Logging.LogLevel.Info,
                                                    $"[CollectionChanged] 开始查找新节点UI - 节点ID: {node.Id}, CanvasLoaded={WorkflowCanvas.IsLoaded}, CanvasVisible={WorkflowCanvas.IsVisible}, CanvasSize={WorkflowCanvas.ActualWidth}x{WorkflowCanvas.ActualHeight}, 节点位置=({node.Position.X:F1}, {node.Position.Y:F1})",
                                                    "WorkflowCanvasControl");

                                                // WPF 兼容的命中测试方法
                                                var hitTestResults = new List<Border>();
                                                VisualTreeHelper.HitTest(
                                                    WorkflowCanvas,
                                                    null,
                                                    result =>
                                                    {
                                                        if (result.VisualHit is Border border &&
                                                            border.Tag is WorkflowNode wn &&
                                                            wn.Id == node.Id)
                                                        {
                                                            hitTestResults.Add(border);
                                                        }
                                                        return HitTestResultBehavior.Continue;
                                                    },
                                                    new PointHitTestParameters(node.Position));

                                                // 📌 关键日志：详细记录找到的Border信息
                                                if (hitTestResults.Any())
                                                {
                                                    foreach (var border in hitTestResults)
                                                    {
                                                        var borderPos = new Point();
                                                        var borderSize = new Size();
                                                        if (border.Child is FrameworkElement child)
                                                        {
                                                            // 修复：Border 在 DataTemplate 内部，Canvas.Left/Top 在父级 ContentPresenter 上
                                                            borderPos = GetBorderPosition(border);
                                                            borderSize = new Size(border.ActualWidth, border.ActualHeight);
                                                        }
                                                        var zIndex = System.Windows.Controls.Panel.GetZIndex(border);
                                                        var opacity = border.Opacity;
                                                        var visibility = border.Visibility;
                                                        var isLoaded = border.IsLoaded;
                                                        var isVisible = border.IsVisible;
                                                        var renderTransform = border.RenderTransform;
                                                    }
                                                }
                                                else
                                                {
                                                    // 📌 关键日志：查找失败，扫描Canvas所有Border
                                                    var allBorders = new List<Border>();
                                                    VisualTreeHelper.HitTest(
                                                        WorkflowCanvas,
                                                        null,
                                                        result =>
                                                        {
                                                            if (result.VisualHit is Border border)
                                                            {
                                                                allBorders.Add(border);
                                                            }
                                                            return HitTestResultBehavior.Stop; // 只找第一个
                                                        },
                                                        new PointHitTestParameters(node.Position));

                                                    Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                                                        Plugin.SDK.Logging.LogLevel.Warning,
                                                        $"[CollectionChanged] 未找到目标节点Border - 节点ID: {node.Id}, 节点位置=({node.Position.X:F1}, {node.Position.Y:F1}), 该位置命中的Border数={allBorders.Count}, Border名称={string.Join(", ", allBorders.Select(b => b.Name))}",
                                                        "WorkflowCanvasControl");

                                                    // 📌 扫描Canvas中所有Border元素
                                                    var canvasBorders = WorkflowVisualHelper.FindAllVisualChildren<Border>(WorkflowCanvas);
                                                    var nodeBorders = canvasBorders.Where(b => b.Tag is WorkflowNode).ToList();
                                                    var targetNodeBorder = nodeBorders.FirstOrDefault(b => (b.Tag as WorkflowNode)?.Id == node.Id);

                                                    string targetBorderInfo;
                                                    if (targetNodeBorder != null)
                                                    {
                                                        // 修复：Border 在 DataTemplate 内部，Canvas.Left/Top 在父级 ContentPresenter 上
                                                        var borderPos = GetBorderPosition(targetNodeBorder);
                                                        targetBorderInfo = "存在(Name=" + targetNodeBorder.Name + ", Position=(" + borderPos.X.ToString("F1") + ", " + borderPos.Y.ToString("F1") + "))";
                                                    }
                                                    else
                                                    {
                                                        targetBorderInfo = "不存在";
                                                    }

                                                    // 注释掉：该日志导致编译错误
                                                    // var scanResultMessage = "[CollectionChanged] Canvas扫描结果 - 总Border数=" + canvasBorders.Count.ToString() + ", 节点Border数=" + nodeBorders.Count.ToString() + ", 目标节点Border=" + targetBorderInfo;
                                                    // Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                                                    //     Plugin.SDK.Logging.LogLevel.Warning,
                                                    //     scanResultMessage,
                                                    //     "WorkflowCanvasControl");
                                                }
                                            }));
                                        }

                                    }

                                    // 处理删除的节点：清理相关状态引用
                                    if (args.OldItems != null)
                                    {
                                        // 🔍 诊断日志：记录删除前的状态
                                        Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                                            Plugin.SDK.Logging.LogLevel.Info,
                                            $"[CollectionChanged] 节点删除前状态 - Canvas聚焦={WorkflowCanvas.IsFocused}, _isDraggingConnection={_isDraggingConnection}, _isBoxSelecting={_isBoxSelecting}, _dragConnectionSourceNode={(_dragConnectionSourceNode?.Name ?? "null")}, 删除节点数: {args.OldItems.Count}",
                                            "WorkflowCanvasControl");

                                        foreach (WorkflowNode node in args.OldItems)
                                        {
                                            // 🔍 诊断日志：记录被删除节点的UI状态
                                            var allBorders = WorkflowVisualHelper.FindAllVisualChildren<Border>(WorkflowCanvas);
                                            var deletedNodeBorders = allBorders.Where(b => b.Tag is WorkflowNode wn && wn.Id == node.Id).ToList();

                                            Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                                                Plugin.SDK.Logging.LogLevel.Info,
                                                $"[CollectionChanged] 节点删除 - 节点ID: {node.Id}, 节点名称: {node.Name}, 位置: ({node.Position.X:F1}, {node.Position.Y:F1}), 找到的Border数: {deletedNodeBorders.Count}",
                                                "WorkflowCanvasControl");

                                            if (deletedNodeBorders.Any())
                                            {
                                                foreach (var border in deletedNodeBorders)
                                                {
                                                    // 修复：Border 在 DataTemplate 内部，Canvas.Left/Top 在父级 ContentPresenter 上
                                                    var borderPos = GetBorderPosition(border);
                                                    var borderSize = new Size(border.ActualWidth, border.ActualHeight);
                                                    var zIndex = System.Windows.Controls.Panel.GetZIndex(border);
                                                    Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                                                        Plugin.SDK.Logging.LogLevel.Info,
                                                        $"[CollectionChanged] 删除节点Border - 节点ID: {node.Id}, BorderName={border.Name}, Border位置=({borderPos.X:F1}, {borderPos.Y:F1}), Border大小={borderSize.Width:F1}x{borderSize.Height:F1}, ZIndex={zIndex}, IsHitTestVisible={border.IsHitTestVisible}, IsEnabled={border.IsEnabled}, Opacity={border.Opacity:F2}, Visibility={border.Visibility}, IsLoaded={border.IsLoaded}, IsVisible={border.IsVisible}",
                                                        "WorkflowCanvasControl");
                                                }
                                            }

                                            // 1. 清理高亮的目标节点Border
                                            if (_highlightedTargetNodeBorder?.Tag is WorkflowNode highlightedNode && highlightedNode.Id == node.Id)
                                            {
                                                SetPortsVisibility(_highlightedTargetNodeBorder, false);
                                                _highlightedTargetNodeBorder = null;
                                            }

                                            // 2. 清理高亮的目标Border（用于端口高亮）
                                            if (_highlightedTargetBorder?.Tag is WorkflowNode targetBorderNode && targetBorderNode.Id == node.Id)
                                            {
                                                // 清空状态
                                                _highlightedTargetBorder = null;
                                                _highlightedTargetPort = null;
                                                _lastHighlightedPort = "";
                                            }

                                            // 3. 清理拖拽连接的源节点Border
                                            if (_dragConnectionSourceBorder?.Tag is WorkflowNode sourceBorderNode && sourceBorderNode.Id == node.Id)
                                            {
                                                SetPortsVisibility(_dragConnectionSourceBorder, false);
                                                _dragConnectionSourceBorder = null;
                                                _dragConnectionSourceNode = null;
                                            }

                                            // 4. 清理缓存
                                            _connectionPathCache.MarkNodeDirty(node.Id);

                                            // 🔍 关键日志：记录节点删除清理
                                            var highlightedTargetNodeBorderCleared = (_highlightedTargetNodeBorder?.Tag as WorkflowNode)?.Id == node.Id;
                                            var highlightedTargetBorderCleared = (_highlightedTargetBorder?.Tag as WorkflowNode)?.Id == node.Id;
                                            var dragConnectionSourceBorderCleared = (_dragConnectionSourceBorder?.Tag as WorkflowNode)?.Id == node.Id;

                                            Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                                                Plugin.SDK.Logging.LogLevel.Info,
                                                $"[CollectionChanged] 节点删除清理完成 - 节点ID: {node.Id}, 清理的状态引用: HighlightedTargetNodeBorder={highlightedTargetNodeBorderCleared}, HighlightedTargetBorder={highlightedTargetBorderCleared}, DragConnectionSourceBorder={dragConnectionSourceBorderCleared}",
                                                "WorkflowCanvasControl");
                                        }

                                        // 📌 关键日志：删除后扫描Canvas中的节点Border
                                        Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                                        {
                                            var allBorders = WorkflowVisualHelper.FindAllVisualChildren<Border>(WorkflowCanvas);
                                            var nodeBorders = allBorders.Where(b => b.Tag is WorkflowNode).ToList();
                                            var workflowNodes = workflowTab.WorkflowNodes;

                                            // 每个节点对应2个 Border（HitAreaBorder + NodeBorder），按 ID 去重后比较
                                            var uniqueNodeIds = nodeBorders
                                                .Select(b => ((WorkflowNode)b.Tag).Id)
                                                .Distinct()
                                                .Count();

                                            if (uniqueNodeIds != workflowNodes.Count)
                                            {
                                                Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                                                    Plugin.SDK.Logging.LogLevel.Warning,
                                                    $"[CollectionChanged] Border与Nodes数量不一致 - 含Tag Border数={nodeBorders.Count}, 去重节点ID数={uniqueNodeIds}, Nodes数={workflowNodes.Count}, 差异={uniqueNodeIds - workflowNodes.Count}",
                                                    "WorkflowCanvasControl");

                                                // 列出所有节点Border的信息
                                                foreach (var border in nodeBorders)
                                                {
                                                    if (border.Tag is WorkflowNode wn)
                                                    {
                                                        // 修复：Border 在 DataTemplate 内部，Canvas.Left/Top 在父级 ContentPresenter 上
                                                        var borderPos = GetBorderPosition(border);
                                                    }
                                                }
                                            }
                                        }));
                                    }

                                }

                            };

                        }
                    }

                    catch (Exception ex)

                    {
                        // 备用方案：使用 PathCalculatorFactory 创建（默认贝塞尔曲线）

                        _connectionPathCache = new ConnectionPathCache(

                            workflowTab.WorkflowNodes,

                            Services.PathCalculators.PathCalculatorFactory.CreateCalculator()

                        );

                        SmartPathConverter.PathCache = _connectionPathCache;

                    }

                }

            else

            {
            }

        }



        /// <summary>

        /// 初始化渲染调度器（异步节点渲染优化）

        /// </summary>

        private void InitializeRenderScheduler(WorkflowTabViewModel workflowTab)

        {

            if (_renderScheduler == null)

                return;



            // 设置节点渲染回调

            _renderScheduler.OnNodeRendered = (node, element) =>

            {

                // 节点渲染完成回调 - 可以在这里添加额外的处理

                // 当前版本使用XAML绑定，这个回调预留给未来扩展

            };



            _renderScheduler.OnNodeRemoved = (nodeId) =>

            {

                // 节点移除回调 - 清理资源

                _nodeUIPool?.ReturnNodeUI(nodeId);

            };

        }



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



            // 验证BoundingRectangle元素



            // 禁用设备像素对齐，启用亚像素渲染

            this.SnapsToDevicePixels = false;

            this.UseLayoutRounding = false;



            // 监听DataContext变化，绑定ScaleTransform

            DataContextChanged += OnDataContextChanged;



            Loaded += WorkflowCanvasControl_Loaded;

            Unloaded += WorkflowCanvasControl_Unloaded;

            // 主题切换时对已有子程序齿轮重新诊断，支持两主题可见性对比
            _themeManagerForGearDiagnosis = SunEyeVision.UI.Core.ServiceLocator.Instance
                .GetService<SunEyeVision.UI.Services.ThemeManager>();
            if (_themeManagerForGearDiagnosis != null)
            {
                _themeManagerForGearDiagnosis.ThemeChanged += OnThemeChangedForGearDiagnosis;
            }

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

            InitializeOptimizedRendering();

        }



        /// <summary>

        /// 初始化优化的渲染组件

        /// </summary>

        private void InitializeOptimizedRendering()

        {

            // 初始化节点UI池

            _nodeUIPool = new NodeUIPool();

            _nodeUIPool.Prewarm(10); // 预创建10个节点UI



            // 初始化渲染调度器

            _renderScheduler = new NodeRenderScheduler();

            _renderScheduler.SetNodeUIPool(_nodeUIPool);

        }



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

            if (Window.GetWindow(this) is MainWindow mainWindow)

            {

                _viewModel = mainWindow.DataContext as MainWindowViewModel;

                if (_viewModel != null)

                {
                    // 注入 TabViewModel 引用到 DragDropHandler（优化 GetCurrentWorkflowTab 性能）
                    if (_dragDropHandler != null && _viewModel.WorkflowTabViewModel != null)
                    {
                        _dragDropHandler.TabViewModel = _viewModel.WorkflowTabViewModel;
                    }

                    // 初始化辅助类（需要ViewModel）

                    if (_connectionCreator == null)

                    {

                        _connectionCreator = new WorkflowConnectionCreator(_viewModel);
                    }






                    // 初始化端口位置查询服务（完全解耦方案）

                    if (_portPositionService == null)

                    {

                        _portPositionService = new PortPositionService(WorkflowCanvas, NodeStyles.Standard);
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

            WorkflowPathCalculator.RefreshAllConnectionPaths(CurrentWorkflowTab.WorkflowConnections);

        }



        /// <summary>

        /// 重新初始化路径计算器（当前只支持贝塞尔曲线计算器）

        /// </summary>

        public void SetPathCalculator(string pathCalculatorType)

        {

            try

            {

                // 创建新的路径计算器实例

                var newCalculator = Services.PathCalculators.PathCalculatorFactory.CreateCalculator();



                // 替换ConnectionPathCache

                if (CurrentWorkflowTab != null)

                {

                    _connectionPathCache = new ConnectionPathCache(

                        CurrentWorkflowTab.WorkflowNodes,

                        newCalculator

                    );



                    // 更新SmartPathConverter的缓存引用

                    SmartPathConverter.PathCache = _connectionPathCache;



                    // 刷新所有连接路径

                    RefreshAllConnectionPaths();

                }

            }

            catch (System.Exception ex)

            {

                // 忽略异常

            }

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
            if (hitBorder != null && hitBorder.Tag is WorkflowNode node)
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

                if (border.Tag is WorkflowNode node && selectedTab.WorkflowNodes.Contains(node))

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

            if (border.Tag is WorkflowNode node)

            {

                var ellipses = WorkflowVisualHelper.FindAllVisualChildren<Ellipse>(border);

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

            if (sender is not Border border || border.Tag is not WorkflowNode node)

                return;



            // 双击事件：打开调试窗口

            if (e.ClickCount == 2)

            {

                if (_viewModel?.WorkflowTabViewModel.SelectedTab != null)

                {

                    foreach (var n in _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes)

                    {

                        n.IsSelected = (n == node);

                    }

                }

                // 使用 ForceSelectNode 强制更新显示（即使引用相同）

                _viewModel.ForceSelectNode(node);



                // 打开调试窗口

                _viewModel.OpenDebugWindowCommand.Execute(node);

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

            // 检查是否按住 Shift 或 Ctrl 键（多选模式）

            bool isMultiSelect = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ||

                               (Keyboard.Modifiers & ModifierKeys.Control) != 0;



            // 如果节点未被选中，且不是多选模式，则只选中当前节点

            if (!node.IsSelected && !isMultiSelect)

            {

                ClearAllSelections();

                node.IsSelected = true;

            }

            // 如果是多选模式，切换选中状态

            else if (isMultiSelect)

            {

                node.IsSelected = !node.IsSelected;

            }



            _viewModel.SelectedNode = node;



            // 记录所有选中节点的初始位置

            RecordSelectedNodesPositions();



            // 单击事件：拖拽准备

            _isDragging = true;

            _draggedNode = node;

            _initialNodePosition = node.Position;

            _startDragPosition = e.GetPosition(WorkflowCanvas);



            border.CaptureMouse();



            // 阻止事件冒泡到 Canvas，避免触发框选

            // 不设置 e.Handled，让事件冒泡到 Port_MouseLeftButtonUp

            // 设置 e.Handled = true 会导致 Port_MouseLeftButtonUp 无法被触发，

            // 从而导致临时连接线无法隐藏

        }



        /// <summary>

        /// 节点鼠标左键释放 - 结束拖拽

        /// </summary>

        private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)

        {

            if (_isDragging && _draggedNode != null)

            {

                // ?? 减少日志输出以提高性能
                // 拖拽结束

                if (_viewModel?.WorkflowTabViewModel.SelectedTab != null)

                {

                    var selectedNodes = _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes

                        .Where(n => n.IsSelected)

                        .ToList();



                    if (selectedNodes.Count > 0 && _selectedNodesInitialPositions != null)

                    {

                        // 计算统一的移动偏移量（从初始位置到当前位置）

                        var delta = new Vector(

                            selectedNodes[0].Position.X - _selectedNodesInitialPositions[0].X,

                            selectedNodes[0].Position.Y - _selectedNodesInitialPositions[0].Y

                        );



                        /*

                        */



                        // ?? 关键修复：不要再次执行 BatchMoveNodesCommand

                        // 因为节点位置已经在 Node_MouseMove 中被更新了

                        // 如果这里再执行一次，会导致节点被移动两次



                        // 拖拽结束后，强制更新所有相关连接的缓存

                        if (_connectionPathCache != null)

                        {

                            foreach (var node in selectedNodes)

                            {

                                _connectionPathCache.MarkNodeDirty(node.Id);
                            }

                        }



                        // ?? 使用批量延迟更新管理器：立即执行所有待处理的更新

                        if (_batchUpdateManager != null)

                        {

                            _batchUpdateManager.ForceUpdateAll();
                        }



                        // ?? TODO: 如果需要支持撤销/重做，需要在这里创建并执行命令

                        // 但是要确保命令不会重复移动节点

                    }

                }



                _isDragging = false;

                _draggedNode = null!;

                (sender as Border)?.ReleaseMouseCapture();
                // 清除位置节流记录，准备下次拖拽

                ClearPositionThrottling();

            }

        }



        /// <summary>

        /// 节点鼠标移动 - 执行拖拽（方案5优化：分层更新策略 - 实时位置+延迟路径）

        /// </summary>

        private void Node_MouseMove(object sender, MouseEventArgs e)

        {

            // 如果正在拖拽连接，则不移动节点（避免冲突）

            if (_isDraggingConnection)

                return;



            if (_isDragging && _draggedNode != null && e.LeftButton == MouseButtonState.Pressed)

            {

                var currentPosition = e.GetPosition(WorkflowCanvas);



                // 计算从拖动开始到现在的总偏移量

                var totalOffset = currentPosition - _startDragPosition;



                // 5A: 获取所有选中节点

                var selectedNodes = _viewModel?.WorkflowTabViewModel.SelectedTab?.WorkflowNodes
                    // 拖动需 WorkflowNode 特有成员，运行时元素均为 WorkflowNode，安全转型
                    .OfType<WorkflowNode>()

                    .Where(n => n.IsSelected)

                    .ToList();



                if (selectedNodes != null && selectedNodes.Count > 0 && _selectedNodesInitialPositions != null)

                {

                    // ?? 关键优化：立即更新节点位置（实时层），不使用批处理

                    // 位置更新必须实时响应鼠标移动，否则会出现延迟和闪烁

                    // ✅ 关键修复：计算全局允许的最大偏移量（确保所有节点统一移动）
                    var globalAllowedOffset = CanvasHelper.CalculateGlobalAllowedOffset(
                        selectedNodes,
                        _selectedNodesInitialPositions,
                        new Point(totalOffset.X, totalOffset.Y),
                        WorkflowCanvas,
                        _parentScrollViewer);

                    for (int i = 0; i < selectedNodes.Count && i < _selectedNodesInitialPositions.Length; i++)

                    {

                        var newPos = new System.Windows.Point(

                            _selectedNodesInitialPositions[i].X + globalAllowedOffset.X,

                            _selectedNodesInitialPositions[i].Y + globalAllowedOffset.Y

                        );



                        // ✅ 不再需要单独应用边界限制（全局偏移已经考虑了边界限制）
                        // newPos = CanvasHelper.ClampNodeToBounds(selectedNodes[i], newPos, WorkflowCanvas, _parentScrollViewer);



                        // 直接设置位置，立即触发PropertyChanged

                        // 这会立即更新Canvas绑定，节点位置实时跟随鼠标

                        selectedNodes[i].Position = newPos;

                    }



                    // 5C: 路径更新使用位置节流 + 批量延迟机制（双层优化）

                    // 路径计算成本高，先通过距离节流减少更新次数，再通过批量延迟合并快速更新

                    if (_batchUpdateManager != null)

                    {

                        // 收集需要更新的节点ID（通过位置节流过滤）

                        var nodesToUpdate = new List<string>();

                        foreach (var node in selectedNodes)

                        {

                            if (ShouldScheduleConnectionUpdate(node.Id, node.Position))

                            {

                                nodesToUpdate.Add(node.Id);

                            }

                        }



                        // 只有当有节点需要更新时才调用批量更新管理器

                        if (nodesToUpdate.Count > 0)

                        {

                            _batchUpdateManager.ScheduleUpdateForNodes(nodesToUpdate);

                        }

                    }

                }

                else

                {

                    // 单个节点移动（向后兼容）

                    var newPos = new System.Windows.Point(

                        _initialNodePosition.X + totalOffset.X,

                        _initialNodePosition.Y + totalOffset.Y

                    );



                    // 应用边界限制
                    newPos = CanvasHelper.ClampNodeToBounds(_draggedNode, newPos, WorkflowCanvas, _parentScrollViewer);



                    // 直接设置位置，立即触发PropertyChanged

                    _draggedNode.Position = newPos;



                    // 5C: 单个节点的路径更新也使用位置节流机制

                    if (_batchUpdateManager != null)

                    {

                        if (ShouldScheduleConnectionUpdate(_draggedNode.Id, _draggedNode.Position))

                        {

                            _batchUpdateManager.ScheduleUpdateForNode(_draggedNode.Id);

                        }

                    }

                }

            }

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

        /// 端口鼠标左键按下 - 开始拖拽连接线

        /// </summary>

        private void Port_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)

        {

            var mousePos = e.GetPosition(WorkflowCanvas);



            if (sender is not Ellipse ellipse || ellipse.Tag is not string portName)
            {
                return;
            }

            // 🔍 关键日志：记录端口详细信息
            Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                Plugin.SDK.Logging.LogLevel.Info,
                $"[Port_MouseLeftButtonDown] 端口点击 - 端口名称: {portName}, Ellipse名称: {ellipse.Name}, 可见性: {ellipse.Visibility}, 是否拖拽中: {_isDraggingConnection}",
                "WorkflowCanvasControl");

            // 🔍 调试日志：记录端口在视觉树中的状态
            if (ellipse.Visibility == Visibility.Visible)
            {
                Point ellipsePosition = ellipse.TransformToAncestor(WorkflowCanvas).Transform(new Point(0, 0));
                Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                    Plugin.SDK.Logging.LogLevel.Success,
                    $"[Port_MouseLeftButtonDown] 端口可见且可点击 - 端口名称: {portName}, 位置: ({ellipsePosition.X:F1}, {ellipsePosition.Y:F1}), 实际尺寸: {ellipse.ActualWidth:F1}x{ellipse.ActualHeight:F1}",
                    "WorkflowCanvasControl");
            }
            else
            {
                Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                    Plugin.SDK.Logging.LogLevel.Warning,
                    $"[Port_MouseLeftButtonDown] ⚠️ 端口不可见，可能无法点击 - 端口名称: {portName}, 可见性: {ellipse.Visibility}",
                    "WorkflowCanvasControl");
            }

            // 保护：如果已经在拖拽状态，直接返回，不启动新的拖拽

            if (_isDraggingConnection)
            {
                e.Handled = true;
                return;
            }



            // 获取父节点Border（向上遍历查找）

            DependencyObject? current = VisualTreeHelper.GetParent(ellipse);

            Border? border = null;

            WorkflowNode? node = null;



            while (current != null)

            {

                if (current is Border currentBorder && currentBorder.Tag is WorkflowNode WorkflowNode)

                {

                    border = currentBorder;

                    node = WorkflowNode;

                    break;

                }

                current = VisualTreeHelper.GetParent(current);

            }



            if (border == null || node == null)
            {
                return;
            }



            // 设置连接拖拽状态

            _isDraggingConnection = true;

            _dragConnectionSourceNode = node;

            _dragConnectionSourceBorder = border; // 保存源节点的Border

            _dragConnectionSourcePort = portName;

            // 保持源节点的端口可见

            SetPortsVisibility(border, true);



            // 获取端口位置

            Point portPosition = GetPortPositionByName(node, portName);

            _dragConnectionStartPoint = portPosition;
            _dragConnectionEndPoint = portPosition;



            // 显示临时连接线

            if (_tempConnectionLine != null && _tempConnectionGeometry != null)

            {
                _tempConnectionLine.Visibility = Visibility.Visible;
                UpdateTempConnectionPath(portPosition, portPosition);
            }
            else
            {
                // 临时连接线未初始化
            }

            // 捕获鼠标
            ellipse.CaptureMouse();



            // 阻止事件冒泡到Border的Node_MouseLeftButtonDown

            // 这样可以避免在拖拽连接时错误地触发节点移动

            e.Handled = true;


        }



        /// <summary>

        /// 更新临时连接线路径（优化版）

        /// </summary>

        /// <param name="startPoint">起点（源端口位置）</param>

        /// <param name="endPoint">终点（鼠标位置或目标端口位置）</param>

        /// <param name="targetNode">目标节点（可选）</param>

        /// <param name="targetPortName">目标端口名称（可选）</param>

        private void UpdateTempConnectionPath(
            Point startPoint,
            Point endPoint,
            WorkflowNode? targetNode = null,
            string? targetPortName = null)
        {
            if (_tempConnectionGeometry == null)
                return;

            var calculator = PathCalculatorFactory.CreateCalculator();
            var sourceDirection = PortDirectionExtensions.FromPortName(_dragConnectionSourcePort ?? "RightPort");

            // 根据是否有目标节点，计算目标位置和方向
            Point targetPosition;
            PortDirection targetDirection;

            if (targetNode != null && !string.IsNullOrEmpty(targetPortName))
            {
                // 情况1：有目标节点 - 使用实际端口位置和方向
                targetPosition = GetPortPositionByName(targetNode, targetPortName);
                targetDirection = PortDirectionExtensions.FromPortName(targetPortName);
            }
            else
            {
                // 情况2：无目标节点 - 使用鼠标位置和推断方向
                targetPosition = endPoint;
                targetDirection = InferTargetDirection(startPoint, endPoint);
            }

            // 统一使用贝塞尔曲线计算器
            var pathPoints = calculator.CalculateOrthogonalPath(
                startPoint,
                targetPosition,
                sourceDirection,
                targetDirection);

            var geometry = calculator.CreatePathGeometry(pathPoints);

            _tempConnectionGeometry.Figures.Clear();
            foreach (var figure in geometry.Figures)
            {
                _tempConnectionGeometry.Figures.Add(figure);
            }
        }

        



        /// <summary>
        /// 根据起点和鼠标位置推算目标端口方向
        /// </summary>
        private PortDirection InferTargetDirection(Point source, Point mouse)
        {
            double dx = mouse.X - source.X;
            double dy = mouse.Y - source.Y;

            if (Math.Abs(dx) > Math.Abs(dy))
            {
                return dx > 0 ? PortDirection.Left : PortDirection.Right;
            }
            else
            {
                return dy > 0 ? PortDirection.Top : PortDirection.Bottom;
            }
        }



        /// <summary>
        /// 端口鼠标左键释放 - 结束拖拽连接线

        /// </summary>

        private void Port_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)

        {
            if (!_isDraggingConnection || _dragConnectionSourceNode == null)
            {
                return;
            }



            // 释放鼠标捕获

            if (sender is Ellipse ellipse)

            {

                ellipse.ReleaseMouseCapture();

            }



            // 使用回调式 HitTest 查找目标端口（修复间歇性命中失败问题）
            // Preview 处理器中的回调式 HitTest 能找到所有层级的元素，
            // 而简单式 HitTest 只返回最顶层元素，端口 Ellipse 可能被上层元素遮挡
            var mousePos = e.GetPosition(WorkflowCanvas);
            var hitPorts = new List<(Ellipse port, string portName, double distance)>();

            VisualTreeHelper.HitTest(WorkflowCanvas, null,
                result =>
                {
                    if (result.VisualHit is Ellipse hitEllipse)
                    {
                        var ellipseName = hitEllipse.Name;
                        if (!string.IsNullOrEmpty(ellipseName) &&
                            (ellipseName == "LeftPortEllipse" ||
                             ellipseName == "RightPortEllipse" ||
                             ellipseName == "TopPortEllipse" ||
                             ellipseName == "BottomPortEllipse"))
                        {
                            string portName = ellipseName.Replace("Ellipse", "");

                            var portCenterX = hitEllipse.RenderSize.Width / 2;
                            var portCenterY = hitEllipse.RenderSize.Height / 2;
                            var portPos = hitEllipse.PointToScreen(new Point(portCenterX, portCenterY));
                            var canvasPos = WorkflowCanvas.PointFromScreen(portPos);
                            double portDistance = Math.Sqrt(
                                Math.Pow(mousePos.X - canvasPos.X, 2) +
                                Math.Pow(mousePos.Y - canvasPos.Y, 2));

                            hitPorts.Add((hitEllipse, portName, portDistance));
                        }
                    }
                    return HitTestResultBehavior.Continue;
                },
                new PointHitTestParameters(mousePos));

            if (hitPorts.Count > 0)
            {
                var nearestPort = hitPorts.OrderBy(p => p.distance).First();
                string targetPortName = nearestPort.portName;

                Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                    Plugin.SDK.Logging.LogLevel.Success,
                    $"[Port_MouseLeftButtonUp] 回调式 HitTest 命中端口 - 端口名称: {targetPortName}, 命中端口数: {hitPorts.Count}, 鼠标位置: ({mousePos.X:F1}, {mousePos.Y:F1})",
                    "WorkflowCanvasControl");

                // 向上遍历视觉树找到端口所属的 Border（节点容器）
                Border? targetBorder = null;
                DependencyObject? parent = nearestPort.port;
                while (parent != null)
                {
                    if (parent is Border border)
                    {
                        targetBorder = border;
                        break;
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }

                if (targetBorder?.Tag is WorkflowNode targetNode &&
                    targetNode != _dragConnectionSourceNode)
                {
                    // 创建连接
                    var connectionCreated = CreateConnectionBetweenPorts(
                        _dragConnectionSourceNode,
                        _dragConnectionSourcePort ?? "RightPort",
                        targetNode,
                        targetPortName
                    );

                    // 连接创建成功后，阻止事件冒泡，避免触发节点选择导致图像预览器被隐藏
                    if (connectionCreated)
                    {
                        e.Handled = true;
                    }
                }
            }



            // 重置连接拖拽状态

            _isDraggingConnection = false;

            _dragConnectionSourceNode = null;

            _dragConnectionSourcePort = null;



            // 清理源节点的端口可见性

            if (_dragConnectionSourceBorder != null)

            {

                SetPortsVisibility(_dragConnectionSourceBorder, false);

                _dragConnectionSourceBorder = null;

            }



            // 隐藏临时连接线

            if (_tempConnectionLine != null)

            {

                var oldVisibility = _tempConnectionLine.Visibility;

                _tempConnectionLine.Visibility = Visibility.Collapsed;



                // 清除几何数据，避免旧数据残留

                if (_tempConnectionGeometry != null)

                {

                    _tempConnectionGeometry.Figures.Clear();

                }



                _tempConnectionLine.UpdateLayout();

            }

            else

            {

            }



            // 清理高亮的目标节点

            if (_highlightedTargetNodeBorder != null)

            {

                SetPortsVisibility(_highlightedTargetNodeBorder, false);

                _highlightedTargetNodeBorder = null;

            }



            // 注意：在连接创建成功时已设置 e.Handled = true 阻止事件冒泡

            // 如果没有成功创建连接，事件会继续冒泡，允许其他处理器响应

        }



        /// <summary>

        /// 端口鼠标移动 - 更新临时连接线

        /// </summary>

        private void Port_MouseMove(object sender, MouseEventArgs e)

        {

            if (!_isDraggingConnection || e.LeftButton != MouseButtonState.Pressed)

                return;



            // 更新临时连接线终点

            var currentPosition = e.GetPosition(WorkflowCanvas);

            _dragConnectionEndPoint = currentPosition;



            if (_tempConnectionLine != null)

            {

                UpdateTempConnectionPath(_dragConnectionStartPoint, currentPosition);

            }



            // HitTest 查找鼠标下的节点

            var hitResult = VisualTreeHelper.HitTest(WorkflowCanvas, currentPosition);

            if (hitResult?.VisualHit is DependencyObject obj)

            {

                // 向上查找 Border（节点容器）

                Border? targetBorder = null;

                while (obj != null)

                {

                    if (obj is Border border && border.Tag is WorkflowNode node)

                    {

                        targetBorder = border;

                        break;

                    }

                    obj = VisualTreeHelper.GetParent(obj);

                }



                // 如果目标节点改变，更新端口可见性

                if (targetBorder != null && targetBorder != _highlightedTargetNodeBorder)

                {

                    // 隐藏之前高亮的节点端口

                    if (_highlightedTargetNodeBorder != null)

                    {

                        SetPortsVisibility(_highlightedTargetNodeBorder, false);

                    }



                    // 显示新的目标节点端口

                    _highlightedTargetNodeBorder = targetBorder;

                    SetPortsVisibility(_highlightedTargetNodeBorder, true);

                }

            }

            else

            {

                // 鼠标不在任何节点上，隐藏之前高亮的节点端口

                if (_highlightedTargetNodeBorder != null)

                {

                    SetPortsVisibility(_highlightedTargetNodeBorder, false);

                    _highlightedTargetNodeBorder = null;

                }

            }



            // 不设置 e.Handled，让事件冒泡到 Port_MouseLeftButtonUp

            // 设置 e.Handled = true 会导致 Port_MouseLeftButtonUp 无法被触发，

            // 从而导致临时连接线无法隐藏

        }



        /// <summary>

        /// 根据端口名称获取端口位置

        /// </summary>

        private Point GetPortPositionByName(WorkflowNode node, string portName)

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

        private bool CreateConnectionBetweenPorts(WorkflowNode sourceNode, string sourcePort, 

            WorkflowNode targetNode, string targetPort)

        {

            var selectedTab = _viewModel?.WorkflowTabViewModel.SelectedTab;

            if (selectedTab == null)
            {
                return false;
            }



            // 检查是否已存在相同连接（同时检查节点和端口）

            var existingConnection = selectedTab.WorkflowConnections?.FirstOrDefault(

                c => c.SourceNodeId == sourceNode.Id && 

                     c.TargetNodeId == targetNode.Id &&

                     c.SourcePort == sourcePort && 

                     c.TargetPort == targetPort);

            

            if (existingConnection != null)
            {
                return false;
            }



            // 创建新连接

            var connectionId = $"conn_{Guid.NewGuid().ToString("N")[..8]}";

            var newConnection = new WorkflowConnection(connectionId, sourceNode.Id, targetNode.Id);

            newConnection.SourcePort = sourcePort;

            newConnection.TargetPort = targetPort;



            // 添加连接到集合（通过 ViewModel 统一入口：日志 + 命令 + ImageSource绑定）
            _viewModel?.AddConnectionToWorkflow(newConnection);



            // 标记相关节点为脏，触发连接线更新

            if (_connectionPathCache != null)

            {

                _connectionPathCache.MarkNodeDirty(sourceNode.Id);

                _connectionPathCache.MarkNodeDirty(targetNode.Id);

            }





            // 连接创建后，选中目标节点，让图像预览器自动显示上游图像采集节点的图像

            // UpdateImagePreviewVisibility 会自动通过 BFS 追溯上游来决定是否显示图像预览器
if (_viewModel != null)

            {
_viewModel.SelectedNode = targetNode;

                // ★ 强制刷新图像预览器，确保即使 SelectedNode 值相同也会重新计算

                _viewModel.ForceRefreshImagePreview();
}

            else

            {
}



            return true;

        }



        /// <summary>

        /// 节点点击事件 - 用于连接或选中

        /// </summary>

        private void Node_ClickForConnection(object sender, RoutedEventArgs e)

        {

            // 获取节点对象（支持 Border 或 Ellipse 作为 sender）

            WorkflowNode? targetNode = null;



            if (sender is Border border && border.Tag is WorkflowNode clickedNodeFromBorder)

            {

                targetNode = clickedNodeFromBorder;

            }

            else if (sender is Ellipse ellipse && ellipse.Tag is WorkflowNode clickedNodeFromEllipse)

            {

                targetNode = clickedNodeFromEllipse;



                // 选中当前节点（连接点点击时也需要选中节点）

                if (_viewModel?.WorkflowTabViewModel.SelectedTab != null)

                {

                    foreach (var n in _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes)

                    {

                        n.IsSelected = (n == targetNode);

                    }

                    _viewModel.SelectedNode = targetNode;

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

                _connectionCreator?.CreateConnection(_connectionSourceNode, targetNode, "RightPort", CurrentWorkflowTab);



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

            var mousePos = e.GetPosition(WorkflowCanvas);



            // 保护：如果处于拖拽连接状态，先重置状态

            if (_isDraggingConnection)

            {

                _isDraggingConnection = false;

                _dragConnectionSourceNode = null;

                _dragConnectionSourcePort = null;

                if (_tempConnectionLine != null)

                {

                    _tempConnectionLine.Visibility = Visibility.Collapsed;

                    _tempConnectionLine.UpdateLayout();

                }

            }



            // 检查点击的是否是节点或端口（通过原始源）

            var originalSource = e.OriginalSource as DependencyObject;



            // 手动查找带 WorkflowNode Tag 的 Border 或端口 Ellipse

            WorkflowNode? clickedNode = null;

            bool clickedPort = false;

            bool clickedConnection = false;

            DependencyObject? current = originalSource;



            while (current != null)

            {

                if (current is Border border && border.Tag is WorkflowNode node)

                {

                    clickedNode = node;

                    break;

                }

                // 检查是否点击了端口 Ellipse

                if (current is Ellipse ellipse)

                {

                    var ellipseName = ellipse.Name ?? "";

                    if (ellipseName.Contains("Port"))

                    {

                        clickedPort = true;

                        break;

                    }

                }

                // 检查是否点击了连接线命中区域的透明宽Path（StrokeThickness > 10 且 DataContext 为 WorkflowConnection）
                if (current is Path path && path.StrokeThickness > 10)
                {
                    // 查找父级 Canvas 的 DataContext 是否为 WorkflowConnection
                    var parent = VisualTreeHelper.GetParent(path);
                    while (parent != null)
                    {
                        if (parent is System.Windows.Controls.Canvas canvas && canvas.DataContext is WorkflowConnection)
                        {
                            clickedConnection = true;
                            break;
                        }
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                    if (clickedConnection) break;
                }

                current = VisualTreeHelper.GetParent(current);

            }



            // 如果点击的是有 WorkflowNode Tag 的 Border 或端口 Ellipse 或连接线命中区域，则由节点/连线的事件处理，不触发框选

            if (clickedNode != null || clickedPort || clickedConnection)

            {

                return;

            }



            // ✅ 检查是否按住 Ctrl 键（只有按住 Ctrl 才能启动框选）
            bool isCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            // 记录鼠标按下时的初始位置（用于拖动阈值判断）
            _mouseDownPosition = e.GetPosition(WorkflowCanvas);
            _boxSelectStart = _mouseDownPosition;

            // ✅ 如果按住 Ctrl 键，启动框选
            if (isCtrlPressed)
            {
                // 检查是否也按住 Shift 键（多选模式）
                bool isMultiSelect = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

                // 如果不是多选模式，清除所有选择
                if (!isMultiSelect)
                {
                    ClearAllSelections();
                }

                // 启动框选
                _isBoxSelecting = true;
                _boxSelectStart = e.GetPosition(WorkflowCanvas);

                // 开始框选
                SelectionBox?.StartSelection(_boxSelectStart);

                WorkflowCanvas.CaptureMouse();
            }
            else
            {
                // ✅ 没有按住 Ctrl 键时，清除选择并启动平移
                ClearAllSelections();

                // 启动平移
                _isPanning = true;
                _panStartPosition = e.GetPosition(WorkflowCanvas);
                _panStartPositionInScrollViewer = e.GetPosition(null);  // 相对ScrollViewer的位置
                WorkflowCanvas.CaptureMouse();
                Cursor = Cursors.Hand;  // 显示手型光标
            }

            // 不设置 e.Handled，让事件冒泡到 Port_MouseLeftButtonUp

            // 设置 e.Handled = true 会导致 Port_MouseLeftButtonUp 无法被触发，

            // 从而导致临时连接线无法隐藏


        }



        /// <summary>

        /// Canvas 鼠标移动 - 更新框选区域

        /// </summary>

        private void WorkflowCanvas_PreviewMouseMove(object sender, MouseEventArgs e)

        {

            var mousePos = e.GetPosition(WorkflowCanvas);

            bool stateChanged = (_isDraggingConnection != _lastIsDraggingConnection) ||
                              (_isBoxSelecting != _lastIsBoxSelecting);

            if (stateChanged)
            {
                _lastIsDraggingConnection = _isDraggingConnection;
                _lastIsBoxSelecting = _isBoxSelecting;
            }

            // ✅ 处理平移
            if (_isPanning)
            {
                var currentMousePos = e.GetPosition(null);  // 相对ScrollViewer的位置
                var deltaX = currentMousePos.X - _panStartPositionInScrollViewer.X;
                var deltaY = currentMousePos.Y - _panStartPositionInScrollViewer.Y;

                // ✅ 关键修复：更新起始位置为当前位置，以便下一次计算增量
                _panStartPositionInScrollViewer = currentMousePos;

                // 获取ScrollViewer并调整滚动位置
                var scrollViewer = FindScrollViewer();
                if (scrollViewer != null)
                {
                    // 应用速度系数，使平移更平滑
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - deltaX * PanSpeedFactor);
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - deltaY * PanSpeedFactor);
                }

                return;
            }

            // 处理拖拽连接

            if (_isDraggingConnection)

            {

                // 保护：如果状态不一致（_isDraggingConnection=true 但 _dragConnectionSourceNode=null），立即重置状态

                if (_dragConnectionSourceNode == null)

                {

                    _isDraggingConnection = false;

                    if (_tempConnectionLine != null)

                    {

                        _tempConnectionLine.Visibility = Visibility.Collapsed;

                        _tempConnectionLine.UpdateLayout();

                    }

                    return;

                }



                // 保护：确保临时连接线没有意外显示

                if (_tempConnectionLine != null && _tempConnectionLine.Visibility != Visibility.Visible)

                {

                    _tempConnectionLine.Visibility = Visibility.Collapsed;

                    return;

                }



                // 确保源节点不为空才更新临时连接线

                if (_tempConnectionGeometry != null && _dragConnectionSourceNode != null)

                {

                    var currentPoint = e.GetPosition(WorkflowCanvas);

                    // 获取源节点的连接点位置
                    var sourcePort = GetPortPositionByName(_dragConnectionSourceNode, _dragConnectionSourcePort ?? "RightPort");


                    // 动态高亮目标端口

                    var hitPorts = new List<(Ellipse port, string portName)>();

                    int hitTestCount = 0;

                    // 用于预览线的目标节点和端口信息
                    WorkflowNode? targetNode = null;
                    string? targetPortName = null;



                    VisualTreeHelper.HitTest(WorkflowCanvas, null,

                        result =>

                        {

                            hitTestCount++;



                            // 检查是否命中端口

                            if (result.VisualHit is Ellipse hitEllipse)

                            {

                                var ellipseName = hitEllipse.Name;

                                if (!string.IsNullOrEmpty(ellipseName) && (ellipseName == "LeftPortEllipse" ||

                                    ellipseName == "RightPortEllipse" ||

                                    ellipseName == "TopPortEllipse" ||

                                    ellipseName == "BottomPortEllipse"))

                                {

                                    string portName = ellipseName.Replace("Ellipse", "");

                                    hitPorts.Add((hitEllipse, portName));

                                }

                            }

                            return HitTestResultBehavior.Continue;

                        },

                        new PointHitTestParameters(currentPoint));




                    // 优先处理命中的端口（需要排除源节点的端口）

                    if (hitPorts.Count > 0)

                    {

                        var portName = hitPorts[0].portName;



                        // 找到端口所属的节点

                        Border? portBorder = null;
                        WorkflowNode? portNode = null;

                        foreach (var hitPort in hitPorts)

                        {

                            DependencyObject? parent = hitPort.port;

                            while (parent != null)

                            {

                                if (parent is Border border && border.Tag is WorkflowNode node)

                                {

                                    if (node != _dragConnectionSourceNode)

                                    {

                                        portBorder = border;
                                        portNode = node;

                                        // 只在端口变化时才高亮和记录

                                        if (_lastHighlightedPort != portName)

                                        {

                                            // ✅ 不做任何操作，IsMouseOver 自动触发橙色效果

                                            _directHitTargetPort = portName;

                                            _lastHighlightedPort = portName;

                                        }
                                        
                                        // ✅ 提取目标节点和端口信息
                                        targetNode = node;
                                        targetPortName = portName;

                                        break;

                                    }

                                }

                                parent = VisualTreeHelper.GetParent(parent);

                            }

                            if (portBorder != null) break;

                        }



                        if (portBorder == null)

                        {

                            // 命中的都是源节点的端口，清除高亮

                            if (_lastHighlightedPort != null)

                            {

                                _directHitTargetPort = null;

                                _lastHighlightedPort = null;

                            }

                        }
                        else
                        {
                            // ✅ 命中目标节点端口，显示该节点的所有端口
                            if (_highlightedTargetNodeBorder != portBorder)
                            {
                                if (_highlightedTargetNodeBorder != null)
                                {
                                    SetPortsVisibility(_highlightedTargetNodeBorder, false);
                                }
                                _highlightedTargetNodeBorder = portBorder;
                            }
                            SetPortsVisibility(portBorder, true);
                        }


                    }
                    else
                    {
                        // ⭐ 没有命中任何端口，检查是否命中了目标节点
                        var hitNodes = new List<(WorkflowNode node, Border border, double distance)>();

                        VisualTreeHelper.HitTest(WorkflowCanvas, null,
                            result =>
                            {
                                // 如果找到 Border 且带有 WorkflowNode Tag，计算距离并记录
                                if (result.VisualHit is Border hitBorder && hitBorder.Tag is WorkflowNode hitNode)
                                {
                                    if (hitNode != _dragConnectionSourceNode)
                                    {
                                        var nodeCenter = hitNode.NodeCenter;
                                        double distance = Math.Sqrt(Math.Pow(currentPoint.X - nodeCenter.X, 2) + Math.Pow(currentPoint.Y - nodeCenter.Y, 2));
                                        hitNodes.Add((hitNode, hitBorder, distance));
                                    }
                                }

                                // 对于任何命中的元素，都向上查找带有WorkflowNode Tag的Border
                                DependencyObject? current = result.VisualHit as DependencyObject;
                                int depth = 0;
                                while (current != null && depth < 30)
                                {
                                    depth++;
                                    if (current is Border currentBorder && currentBorder.Tag is WorkflowNode currentBorderNode)
                                    {
                                        if (currentBorderNode != _dragConnectionSourceNode)
                                        {
                                            var nodeCenter = currentBorderNode.NodeCenter;
                                            double distance = Math.Sqrt(Math.Pow(currentPoint.X - nodeCenter.X, 2) + Math.Pow(currentPoint.Y - nodeCenter.Y, 2));
                                            hitNodes.Add((currentBorderNode, currentBorder, distance));
                                        }
                                        break;
                                    }
                                    current = VisualTreeHelper.GetParent(current);
                                }

                                return HitTestResultBehavior.Continue;
                            },
                            new PointHitTestParameters(currentPoint));

                        if (hitNodes.Count > 0)
                        {
                            var nearest = hitNodes.OrderBy(n => n.distance).First();
                            const double MaxDistance = 150.0; // 最大容错距离150px

                            if (nearest.distance <= MaxDistance)
                            {
                                // ✅ 命中目标节点（但未命中端口），显示该节点的所有端口
                                if (_highlightedTargetNodeBorder != nearest.border)
                                {
                                    if (_highlightedTargetNodeBorder != null)
                                    {
                                        SetPortsVisibility(_highlightedTargetNodeBorder, false);
                                    }
                                    _highlightedTargetNodeBorder = nearest.border;
                                }
                                SetPortsVisibility(nearest.border, true);
                            }
                            else
                            {
                                // 隐藏之前高亮的节点端口
                                if (_highlightedTargetNodeBorder != null)
                                {
                                    SetPortsVisibility(_highlightedTargetNodeBorder, false);
                                    _highlightedTargetNodeBorder = null;
                                }
                            }
                        }
                        else
                        {
                            // 没有命中任何节点或端口，隐藏之前高亮的节点端口
                            if (_highlightedTargetNodeBorder != null)
                            {
                                SetPortsVisibility(_highlightedTargetNodeBorder, false);
                                _highlightedTargetNodeBorder = null;
                            }
                        }
                        // 清除命中记录
                        _directHitTargetPort = null;
                        _lastHighlightedPort = null;
                    }

                    // ✅ 使用优化的预览线计算（统一使用贝塞尔曲线计算器）
                    UpdateTempConnectionPath(sourcePort, currentPoint, targetNode, targetPortName);

                }

                return;

            }





            // 非拖拽状态下的端口显示检测

            if (!_isBoxSelecting)

            {

                TestNodeHitForPortDisplay(mousePos);

                return;

            }





            // 更新框选框

            var selectionPoint = e.GetPosition(WorkflowCanvas);

            SelectionBox?.UpdateSelection(selectionPoint);



            // 获取框选区域

            var selectionRect = SelectionBox?.GetSelectionRect() ?? new Rect();



            // 更新选中的节点

            if (_viewModel?.WorkflowTabViewModel.SelectedTab != null)

            {

                int selectedCount = 0;



                foreach (var node in _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes.OfType<WorkflowNode>())

                {

                    // 获取节点边界（动态计算，完全解耦）

                    var nodeRect = node.NodeRect;



                    // 检查节点是否与框选区域相交

                    bool isSelected = selectionRect.IntersectsWith(nodeRect);

                    node.IsSelected = isSelected;



                    if (isSelected) selectedCount++;

                }

                // 框选连接线
                int selectedConnCount = 0;
                foreach (var conn in _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowConnections)
                {
                    conn.IsSelected = IsConnectionInRect(conn, selectionRect);
                    if (conn.IsSelected) selectedConnCount++;
                }



                // 更新框选信息显示

                SelectionBox?.SetItemCount(selectedCount + selectedConnCount);

            }

        }



        /// <summary>

        /// Canvas 鼠标左键释放 - 结束框选或创建连接

        /// </summary>

        private void WorkflowCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)

        {

            var mousePos = e.GetPosition(WorkflowCanvas);



            // 立即隐藏临时连接线（在任何逻辑处理之前）

            // 这样可以确保无论后续逻辑如何，临时连接线都被隐藏

            if (_isDraggingConnection && _tempConnectionLine != null)

            {

                var oldVisibility = _tempConnectionLine.Visibility;

                _tempConnectionLine.Visibility = Visibility.Collapsed;



                // 清除几何数据，避免旧数据残留

                if (_tempConnectionGeometry != null)

                {

                    _tempConnectionGeometry.Figures.Clear();

                }



                // 强制刷新UI，确保临时连接线立即被隐藏

                _tempConnectionLine.UpdateLayout();

            }



            // 如果正在拖拽连接，尝试创建连接

            if (_isDraggingConnection)

            {

                var mousePosition = e.GetPosition(WorkflowCanvas);





            // 不设置 e.Handled，让事件冒泡到 Port_MouseLeftButtonUp

            // 设置 e.Handled = true 会导致 Port_MouseLeftButtonUp 无法被触发，

            // 从而导致临时连接线无法隐藏



            // 隐藏临时连接线

            if (_tempConnectionLine != null)

            {

                _tempConnectionLine.Visibility = Visibility.Collapsed;

            }

            else

            {

            }



                // 清除之前的高亮

                if (_highlightedTargetBorder != null)

                {

                    _highlightedTargetBorder.Background = new SolidColorBrush(Colors.White);

                    _highlightedTargetBorder.BorderBrush = new SolidColorBrush(Colors.Transparent);

                    _highlightedTargetBorder.BorderThickness = new Thickness(0);

                    _highlightedTargetBorder = null;

                }



                // 收集所有命中的节点并选择最近的一个

                var hitNodes = new List<(WorkflowNode node, Border border, double distance)>();

                var hitPorts = new List<(Ellipse port, string portName, double distance)>(); // 新增：命中的端口列表

                int hitTestCount = 0;



                // 输出所有节点的位置信息（用于诊断）

                if (CurrentWorkflowTab?.WorkflowNodes != null)

                {

                    foreach (var node in CurrentWorkflowTab.WorkflowNodes)

                    {

                    }

                }



                // 检查节点是否被渲染到Canvas（用于诊断）

                var nodeBorders = WorkflowVisualHelper.FindAllVisualChildren<Border>(WorkflowCanvas);

                foreach (var border in nodeBorders)

                {

                    if (border.Tag is WorkflowNode node)

                    {

                        // 检查Border的父元素ContentPresenter的位置

                        var parent = VisualTreeHelper.GetParent(border);

                        if (parent is System.Windows.Controls.ContentPresenter cp)

                        {

                            double cpLeft = System.Windows.Controls.Canvas.GetLeft(cp);

                            double cpTop = System.Windows.Controls.Canvas.GetTop(cp);

                        }

                        else

                        {

                        }

                    }

                }



                // 使用 HitTest 查找鼠标位置下的所有元素

                VisualTreeHelper.HitTest(WorkflowCanvas, null,

                    result =>

                    {

                        hitTestCount++;



                        // 检查是否命中了端口

                        if (result.VisualHit is Ellipse hitEllipse)

                        {

                            var ellipseName = hitEllipse.Name;



                            // 检查是否是端口

                            if (!string.IsNullOrEmpty(ellipseName) && (ellipseName == "LeftPortEllipse" ||

                                ellipseName == "RightPortEllipse" ||

                                ellipseName == "TopPortEllipse" ||

                                ellipseName == "BottomPortEllipse"))

                            {

                                // 提取端口名称

                                string portName = ellipseName.Replace("Ellipse", "");

                                var portCenterX = hitEllipse.RenderSize.Width / 2;

                                var portCenterY = hitEllipse.RenderSize.Height / 2;

                                var portPos = hitEllipse.PointToScreen(new Point(portCenterX, portCenterY));

                                var canvasPos = WorkflowCanvas.PointFromScreen(portPos);

                                double portDistance = Math.Sqrt(Math.Pow(mousePosition.X - canvasPos.X, 2) +

                                                                    Math.Pow(mousePosition.Y - canvasPos.Y, 2));



                                hitPorts.Add((hitEllipse, portName, portDistance));



                                // 查找端口所属的节点

                                DependencyObject? parent = hitEllipse;

                                while (parent != null)

                                {

                                    if (parent is Border border && border.Tag is WorkflowNode node)

                                    {

                                        break;

                                    }

                                    parent = VisualTreeHelper.GetParent(parent);

                                }

                            }

                        }



                        // 如果找到 Border 且带有 WorkflowNode Tag，计算距离并记录

                        if (result.VisualHit is Border hitBorder && hitBorder.Tag is WorkflowNode hitNode)

                        {

                            // 动态计算节点中心（完全解耦）

                            var nodeCenter = hitNode.NodeCenter;

                            double distance = Math.Sqrt(Math.Pow(mousePosition.X - nodeCenter.X, 2) + Math.Pow(mousePosition.Y - nodeCenter.Y, 2));

                            hitNodes.Add((hitNode, hitBorder, distance));

                        }



                        // 对于任何命中的元素，都向上查找带有WorkflowNode Tag的Border

                        DependencyObject? current = result.VisualHit as DependencyObject;

                        int depth = 0;

                        while (current != null && depth < 30)

                        {

                            depth++;

                            if (current is Border currentBorder && currentBorder.Tag is WorkflowNode currentBorderNode)

                            {

                                // 动态计算节点中心（完全解耦）

                                var nodeCenter = currentBorderNode.NodeCenter;

                                double distance = Math.Sqrt(Math.Pow(mousePosition.X - nodeCenter.X, 2) + Math.Pow(mousePosition.Y - nodeCenter.Y, 2));

                                hitNodes.Add((currentBorderNode, currentBorder, distance));

                                break;

                            }

                            current = VisualTreeHelper.GetParent(current);

                        }



                        return HitTestResultBehavior.Continue;

                    },

                    new PointHitTestParameters(mousePosition));



            if (hitPorts.Count > 0)

            {

                var nearestPort = hitPorts.OrderBy(p => p.distance).First();

            }



            if (hitPorts.Count > 0)

            {

            }

            

            if (hitNodes.Count > 0)

            {

                foreach (var (node, border, distance) in hitNodes)

                {

                }

            }

            else

            {

            }



                // 选择距离鼠标最近的节点

                WorkflowNode? targetNode = null;

                Border? targetBorder = null;



                // 优先选择命中的端口（排除源节点的端口）

                if (hitPorts.Count > 0)

                {

                    // 先过滤掉源节点的端口

                    var validPorts = hitPorts.Where(p =>

                    {

                        DependencyObject? parent = p.port;

                        while (parent != null)

                        {

                            if (parent is Border border && border.Tag is WorkflowNode node)

                            {

                                return node != _dragConnectionSourceNode;

                            }

                            parent = VisualTreeHelper.GetParent(parent);

                        }

                        return false;

                    }).ToList();



                    if (validPorts.Count > 0)

                    {

                        var nearestPort = validPorts.OrderBy(p => p.distance).First();

                        var targetPortEllipse = nearestPort.port;



                        // 找到端口所属的节点Border

                        DependencyObject? parent = targetPortEllipse;

                        while (parent != null)

                        {

                            if (parent is Border border && border.Tag is WorkflowNode node)

                            {

                                if (node != _dragConnectionSourceNode)

                                {

                                    targetNode = node;

                                    targetBorder = border;

                                    _directHitTargetPort = nearestPort.portName;

                                    break;

                                }

                            }

                            parent = VisualTreeHelper.GetParent(parent);

                        }



                        if (targetBorder != null)

                        {

                            // ✅ 不做任何高亮，端口已经显示，用户可以通过 IsMouseOver 选择

                        }

                    }

                    else

                    {

                    }

                }



                // 如果没有命中有效的目标端口，则使用节点选择逻辑

                // 增加容错距离：即使鼠标不在节点上，如果距离足够近（150px以内），也认为命中该节点

                if (targetNode == null && hitNodes.Count > 0)

                {

                    // 先过滤掉源节点，避免把自己当成目标节点

                    var validNodes = hitNodes.Where(n => n.node != _dragConnectionSourceNode).ToList();

                    

                    if (validNodes.Count > 0)

                    {

                        var nearest = validNodes.OrderBy(n => n.distance).First();

                        const double MaxDistance = 150.0; // 最大容错距离150px



                        if (nearest.distance <= MaxDistance)

                        {

                            targetNode = nearest.node;

                            targetBorder = nearest.border;



                            // ✅ 移除智能推荐逻辑，直接显示端口让用户选择
                            // 端口会自动显示，用户移动鼠标到想要连接的端口即可触发橙色高亮
                            SetPortsVisibility(targetBorder, true);


                        }

                        else

                        {

                        }

                    }

                    else

                    {

                    }

                }



                // 首先检查是否命中了任何端口

                // 如果命中了端口（无论是源端口自己还是其他节点端口），让 Port_MouseLeftButtonUp 处理

                // 这样避免Preview事件提前重置拖拽状态

                if (hitPorts.Count > 0)

                {

                    // 命中了端口，让 Port_MouseLeftButtonUp 处理连接创建或状态清理

                    // 包括：拖到其他端口创建连接，或拖回自己端口取消连接

                    return;

                }



                // 检查是否找到目标节点（拖拽到节点主体，而非端口）

                if (targetNode != null && targetNode != _dragConnectionSourceNode)

                {

                    // 确定源端口和目标端口

                    string sourcePort = _dragConnectionSourcePort ?? "RightPort";

                    string targetPort = _directHitTargetPort ?? (hitPorts.Count > 0 ? hitPorts.OrderBy(p => p.distance).First().portName : null);



                    // 检查相同连接点是否已存在连接

                    var selectedTab = _viewModel?.WorkflowTabViewModel.SelectedTab;

                    if (selectedTab != null)

                    {

                        var existingConnection = selectedTab.WorkflowConnections.FirstOrDefault(c =>

                            c.SourceNodeId == _dragConnectionSourceNode.Id &&

                            c.TargetNodeId == targetNode.Id &&

                            c.SourcePort == sourcePort &&

                            c.TargetPort == targetPort);



                        if (existingConnection == null)

                        {

                            if (!string.IsNullOrEmpty(targetPort))

                            {

                                _connectionCreator?.CreateConnectionWithSpecificPort(_dragConnectionSourceNode, targetNode, _dragConnectionSourcePort ?? "RightPort", targetPort, CurrentWorkflowTab);

                            }

                            else

                            {

                                _connectionCreator?.CreateConnection(_dragConnectionSourceNode, targetNode, _dragConnectionSourcePort ?? "RightPort", CurrentWorkflowTab);

                            }

                        }

                    }

                }



                // 没有命中有效端口，重置拖拽状态

                WorkflowCanvas.ReleaseMouseCapture();

                IsDraggingConnection = false;

                _dragConnectionSourceNode = null;

                _dragConnectionSourcePort = null;



                // 清理源节点的端口可见性

                if (_dragConnectionSourceBorder != null)

                {

                    SetPortsVisibility(_dragConnectionSourceBorder, false);

                    _dragConnectionSourceBorder = null;

                }



                SetPortsVisibility(false); // 隐藏所有端口



                return;

            }



            // ✅ 结束平移
            if (_isPanning)
            {
                _isPanning = false;
                WorkflowCanvas.ReleaseMouseCapture();
                Cursor = Cursors.Arrow;  // 恢复默认光标
                return;
            }

            if (!_isBoxSelecting)

            {

                return;

            }



            _isBoxSelecting = false;





            // 结束框选

            SelectionBox?.EndSelection();

            WorkflowCanvas.ReleaseMouseCapture();



            // 记录选中节点的初始位置（用于批量移动）

            RecordSelectedNodesPositions();



            // 不设置 e.Handled，让事件冒泡到 Port_MouseLeftButtonUp

            // 设置 e.Handled = true 会导致 Port_MouseLeftButtonUp 无法被触发，

            // 从而导致临时连接线无法隐藏

        }



        /// <summary>

        /// 清除所有节点的选中状态

        /// </summary>

        private void ClearAllSelections()

        {

            if (_viewModel?.WorkflowTabViewModel.SelectedTab != null)

            {

                foreach (var node in _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes)

                {

                    node.IsSelected = false;

                }

                // 清除连接线选中
                foreach (var conn in _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowConnections)
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

                .Select(n => n.Position)

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



            Plugin.SDK.Logging.VisionLogger.Instance?.Log(

                Plugin.SDK.Logging.LogLevel.Info,

                $"[Canvas_GotFocus] 画布获得焦点 - 节点数: {_viewModel?.WorkflowTabViewModel?.SelectedTab?.WorkflowNodes.Count ?? 0}",

                "WorkflowCanvasControl");

        }


        /// <summary>
        /// Canvas 失去焦点
        /// </summary>

        private void WorkflowCanvas_LostFocus(object sender, RoutedEventArgs e)

        {

            Plugin.SDK.Logging.VisionLogger.Instance?.Log(

                Plugin.SDK.Logging.LogLevel.Warning,

                $"[Canvas_LostFocus] 画布失去焦点 - 节点数: {_viewModel?.WorkflowTabViewModel?.SelectedTab?.WorkflowNodes.Count ?? 0}",

                "WorkflowCanvasControl");

        }


        #endregion



        #region 辅助方法

        /// <summary>
        /// 判断连接线是否与矩形区域相交（粗筛 + 精筛两阶段优化）
        /// </summary>
        private bool IsConnectionInRect(WorkflowConnection connection, Rect selectionRect)
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

        private string? DetermineClickedPort(WorkflowNode node, Point clickPoint)

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

        private Ellipse? GetPortElement(Border nodeBorder, string portName)

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

                    return element as Ellipse;

                }

            }



            return null;

        }



        /// <summary>

        /// Path元素加载事件 - 监控连接线路径创建

        /// </summary>

        private void Path_Loaded(object sender, RoutedEventArgs e)

        {

            if (sender is Path path && path.DataContext is WorkflowConnection connection)

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

                if (e.NewValue is WorkflowConnection newConn)

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
                if (parentCanvas?.DataContext is WorkflowConnection connection)
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
                if (parentCanvas?.DataContext is WorkflowConnection connection)
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
            if (parentCanvas?.DataContext is not WorkflowConnection connection) return;

            var tab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (tab == null) return;

            bool isMultiSelect = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ||
                                 (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (!isMultiSelect)
            {
                // 单选模式：清除所有节点和连接线选中
                ClearAllSelections();
                // 设置当前连接线选中
                connection.IsSelected = true;
                _viewModel.SelectedConnection = connection;
            }
            else
            {
                // 多选模式：切换当前连接线选中状态
                connection.IsSelected = !connection.IsSelected;
                _viewModel.SelectedConnection = connection.IsSelected ? connection : null;
            }

            // 阻止事件冒泡，避免触发框选
            e.Handled = true;
        }





        /// <summary>

        /// 箭头Path加载事件 - 设置箭头旋转角度

        /// </summary>

        private void ArrowPath_Loaded(object sender, RoutedEventArgs e)

        {

            if (sender is Path arrowPath && arrowPath.DataContext is WorkflowConnection connection)

            {

                // 设置箭头旋转角度

                var rotateTransform = new RotateTransform(connection.ArrowAngle);

                arrowPath.RenderTransform = rotateTransform;



                // 关键日志：记录箭头渲染



                // 监听ArrowAngle变化，动态更新旋转角度

                connection.PropertyChanged += (s, args) =>

                {

                    if (args.PropertyName == nameof(WorkflowConnection.ArrowAngle))

                    {

                        if (arrowPath.RenderTransform is RotateTransform rt)

                        {

                            rt.Angle = connection.ArrowAngle;

                        }

                    }

                };

            }

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

            WorkflowNode? sourceNode = null;

            WorkflowNode? targetNode = null;



            if (CurrentWorkflowTab?.WorkflowNodes != null)

            {

                if (!string.IsNullOrEmpty(BoundingSourceNodeId))

                {

                    sourceNode = CurrentWorkflowTab.WorkflowNodes.OfType<WorkflowNode>().FirstOrDefault(n => n.Id == BoundingSourceNodeId);

                }



                if (!string.IsNullOrEmpty(BoundingTargetNodeId))

                {

                    targetNode = CurrentWorkflowTab.WorkflowNodes.OfType<WorkflowNode>().FirstOrDefault(n => n.Id == BoundingTargetNodeId);

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
            if (sender is MenuItem menuItem &&
                menuItem.Parent is ContextMenu contextMenu &&
                contextMenu.PlacementTarget is FrameworkElement placementTarget &&
                placementTarget.DataContext is WorkflowNodeBase node)
            {
                node.BeginRename();

                // 延迟到渲染完成后聚焦编辑框
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var textBox = FindVisualChild<TextBox>(placementTarget);
                    if (textBox != null)
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }

            e.Handled = true;
        }

        /// <summary>
        /// 节点重命名编辑框键盘事件 - Enter确认 / Esc取消
        /// </summary>
        private void NodeRenameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is WorkflowNodeBase node)
            {
                if (e.Key == Key.Enter)
                {
                    if (node.ConfirmRename())
                    {
                        ReleaseNodeLocalIndex(node);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    node.CancelRename();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 节点重命名编辑框失去焦点 - 确认重命名
        /// </summary>
        private void NodeRenameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is WorkflowNodeBase node)
            {
                if (node.IsEditingName && node.ConfirmRename())
                {
                    ReleaseNodeLocalIndex(node);
                }
            }
        }

        /// <summary>
        /// 释放节点的局部序号到空洞池
        /// </summary>
        private void ReleaseNodeLocalIndex(WorkflowNodeBase node)
        {
            var tab = _viewModel?.WorkflowTabViewModel.SelectedTab;
            if (tab != null)
            {
                tab.ReleaseNodeLocalIndex(node);
            }
        }

        /// <summary>
        /// 子程序设置按钮点击 - 打开子程序参数配置对话框
        /// </summary>
        private void SubroutineNodeSettings_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is FrameworkElement element && element.DataContext is WorkflowNode node)
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.OpenSubroutineNodeParameterDialog(node);
                }
            }
        }

        /// <summary>
        /// 子程序设置齿轮按钮加载诊断（全链埋点，两主题对比）：
        /// 记录影响齿轮可见性的完整链路，供亮/暗主题下图标不可见问题排查。
        /// 覆盖维度：当前主题、ToolType(DataTrigger)、按钮可见性状态、节点背景、
        /// 状态色块底、齿轮 Foreground/FontFamily/FontSize/FontWeight、实际对比度。
        /// 排障完成后删除本方法、XAML 中 Loaded="SubroutineNodeSettings_Loaded" 及相关辅助代码。
        /// </summary>
        private void SubroutineNodeSettings_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Button gearButton && gearButton.DataContext is WorkflowNode node)
            {
                // 注册按钮以便主题切换时重新诊断（去重）
                if (!_gearButtons.Contains(gearButton))
                {
                    _gearButtons.Add(gearButton);
                }
                LogGearDiagnosis(gearButton, node, "加载");
            }
            else
            {
                VisionLogger.Instance?.Log(LogLevel.Warning,
                    $"齿轮诊断: DataContext 不是 WorkflowNode (实际={sender.GetType().Name}), 齿轮可能不会显示",
                    "WorkflowCanvasControl");
            }
        }

        /// <summary>
        /// 控件卸载时退订主题切换诊断事件，避免内存泄漏。
        /// </summary>
        private void WorkflowCanvasControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_themeManagerForGearDiagnosis != null)
            {
                _themeManagerForGearDiagnosis.ThemeChanged -= OnThemeChangedForGearDiagnosis;
                _themeManagerForGearDiagnosis = null;
            }
            _gearButtons.Clear();
        }

        /// <summary>
        /// 主题切换后，对画布上所有已注册的子程序齿轮重新输出完整诊断，
        /// 从而在同一份日志里对比亮/暗两种主题下的可见性链路。
        /// </summary>
        private void OnThemeChangedForGearDiagnosis(SunEyeVision.UI.Services.AppTheme theme)
        {
            foreach (var gearButton in _gearButtons.ToList())
            {
                if (gearButton.DataContext is WorkflowNode node)
                {
                    LogGearDiagnosis(gearButton, node, "主题切换");
                }
            }
        }

        /// <summary>
        /// 输出齿轮按钮的完整可见性诊断（单次）。
        /// </summary>
        private static void LogGearDiagnosis(Button gearButton, WorkflowNode node, string reason)
        {
            try
            {
                // 1. 当前主题（两主题对比的关键维度）
                var theme = SunEyeVision.UI.Core.ServiceLocator.Instance
                    .GetService<SunEyeVision.UI.Services.ThemeManager>()?.CurrentTheme.ToString() ?? "未知";

                // 2. 节点卡片背景色（设计值，适配器写死浅色）
                var adapter = NodeDisplayAdapterFactory.GetAdapter(node.ToolType);
                var nodeBg = adapter.GetBackgroundColor(node);

                // 3. 状态色块背景色（设计值）
                var statusBg = GetNodeStatusColor(node.Status);

                // 4. 设计有效底
                var effectiveBg = statusBg.A == 0 ? (Color)nodeBg : statusBg;

                // 5. 齿轮按钮可见性状态
                var fg = gearButton.Foreground as SolidColorBrush;
                var fgColor = fg?.Color ?? Colors.Black;
                var fontFamily = gearButton.FontFamily?.Source ?? "null";

                // 6. WCAG 对比度（基于"设计值"叠色，仅供快速判断；真正判断看下面的"实际渲染对比度"）
                var designContrast = ComputeContrastRatio(fgColor, effectiveBg);

                // === A. 实际渲染链路（关键诊断维度，从视觉树读取 WPF 实际使用的值） ===

                // A1. 节点卡片 Border (x:Name="NodeBorder") 的实际 Background
                Color actualNodeCardBg = ReadActualBackground(gearButton, b => b.Name == "NodeBorder");

                // A2. 状态色块 Border 的实际 Background。
                //    状态色块是齿轮的兄弟节点（同父 Grid，CornerRadius="6,0,0,6"），
                //    从齿轮的父级 Grid 遍历子元素精确定位，读取真实渲染色。
                Color actualStatusBlockBg = ReadStatusBlockBackground(gearButton);

                // A3. 真正"实际"的有效底：状态色块不透明则取色块，否则取节点卡片实际背景
                var actualEffectiveBg = actualStatusBlockBg.A > 0 ? actualStatusBlockBg : actualNodeCardBg;

                // A4. 真正对比度：齿轮 Foreground vs 实际叠的底
                var actualContrast = ComputeContrastRatio(fgColor, actualEffectiveBg);
                var actuallyLegible = actualContrast >= 3.0;

                VisionLogger.Instance?.Log(LogLevel.Info,
                    $"齿轮诊断[{reason}]: 主题={theme}, ToolType={node.ToolType}, " +
                    $"按钮Visibility={gearButton.Visibility}, IsVisible={gearButton.IsVisible}, " +
                    $"IsEnabled={gearButton.IsEnabled}, Opacity={gearButton.Opacity:F2}, " +
                    $"[设计]节点背景={nodeBg}, 状态色块底={statusBg}, 有效底={effectiveBg}, " +
                    $"设计对比度={designContrast:F2}, " +
                    $"[实际]节点卡片Border背景={actualNodeCardBg}, 状态色块Border背景={actualStatusBlockBg}, " +
                    $"实际有效底={actualEffectiveBg}, 实际对比度={actualContrast:F2}({(actuallyLegible ? "可辨识" : "不可辨识")}), " +
                    $"齿轮Foreground={fgColor}, FontFamily={fontFamily}, FontSize={gearButton.FontSize}, " +
                    $"FontWeight={gearButton.FontWeight}, 节点显示名={node.DisplayName}",
                    "WorkflowCanvasControl");
            }
            catch (Exception ex)
            {
                VisionLogger.Instance?.Log(LogLevel.Warning,
                    $"齿轮诊断[{reason}] 异常: {ex.GetType().Name}: {ex.Message}",
                    "WorkflowCanvasControl");
            }
        }

        /// <summary>
        /// 从齿轮按钮向上遍历视觉树，按指定谓词查找祖先 Border，并读取其实际渲染的 Background 颜色。
        /// 找不到返回 #00FFFFFF（透明，便于上层判断"未找到"）。
        /// </summary>
        private static Color ReadActualBackground(DependencyObject start, Func<Border, bool> predicate)
        {
            DependencyObject? current = start;
            int depth = 0;
            while (current != null && depth < 20)
            {
                if (current is Border border && predicate(border))
                {
                    if (border.Background is SolidColorBrush scb)
                    {
                        return scb.Color;
                    }
                    return Colors.Magenta; // 命中但 Background 非纯色，用 Magenta 标识
                }
                current = VisualTreeHelper.GetParent(current);
                depth++;
            }
            return Color.FromArgb(0, 255, 255, 255);
        }

        /// <summary>
        /// 定位齿轮所在图标列（Column 0）内的状态色块 Border（CornerRadius="6,0,0,6"）并读取其实际 Background。
        /// 从齿轮的父级 Grid 遍历子元素（状态色块是齿轮的兄弟节点）。
        /// 找不到返回 #00FFFFFF（透明）。
        /// </summary>
        private static Color ReadStatusBlockBackground(DependencyObject gearButton)
        {
            // 找到齿轮所在的父级 Grid（图标列 Column 0）
            DependencyObject? parent = VisualTreeHelper.GetParent(gearButton);
            if (parent is not Grid iconColumnGrid)
            {
                return Color.FromArgb(0, 255, 255, 255);
            }

            // 遍历该 Grid 的子元素，找 CornerRadius 为 (6,0,0,6) 的状态色块 Border
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(iconColumnGrid); i++)
            {
                if (VisualTreeHelper.GetChild(iconColumnGrid, i) is Border border)
                {
                    var cr = border.CornerRadius;
                    bool isStatusBlock = Math.Abs(cr.TopLeft - 6) < 0.1
                                         && Math.Abs(cr.TopRight) < 0.1
                                         && Math.Abs(cr.BottomRight) < 0.1
                                         && Math.Abs(cr.BottomLeft) < 0.1;
                    if (isStatusBlock)
                    {
                        if (border.Background is SolidColorBrush scb)
                        {
                            return scb.Color;
                        }
                        return Colors.Magenta; // 命中但背景非纯色
                    }
                }
            }

            return Color.FromArgb(0, 255, 255, 255);
        }

        /// <summary>
        /// 根据节点状态映射状态色块背景色（与 NodeIconStatusBackgroundConverter 同步）。
        /// </summary>
        private static Color GetNodeStatusColor(string? status)
        {
            return status switch
            {
                "运行中" => Color.FromRgb(33, 150, 243),   // #2196F3
                "成功" or "OK" => Color.FromRgb(76, 175, 80),  // #4CAF50
                "失败" or "NG" => Color.FromRgb(244, 67, 54),  // #F44336
                "跳过" => Color.FromRgb(158, 158, 158),   // #9E9E9E
                _ => Colors.Transparent
            };
        }

        /// <summary>
        /// 计算两个颜色的 WCAG 对比度（1~21），≥3.0 视为图标可辨识。
        /// </summary>
        private static double ComputeContrastRatio(Color fg, Color bg)
        {
            double L(double c)
            {
                var srgb = c / 255.0;
                return srgb <= 0.03928
                    ? srgb / 12.92
                    : Math.Pow((srgb + 0.055) / 1.055, 2.4);
            }

            var lFg = 0.2126 * L(fg.R) + 0.7152 * L(fg.G) + 0.0722 * L(fg.B);
            var lBg = 0.2126 * L(bg.R) + 0.7152 * L(bg.G) + 0.0722 * L(bg.B);
            var lighter = Math.Max(lFg, lBg);
            var darker = Math.Min(lFg, lBg);
            return (lighter + 0.05) / (darker + 0.05);
        }

        #endregion

    }

}


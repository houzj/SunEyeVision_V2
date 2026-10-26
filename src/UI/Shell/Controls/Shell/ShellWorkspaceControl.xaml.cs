using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SubroutineOutputChange = SunEyeVision.Plugin.SDK.Models.SubroutineOutputChange;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;

using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Shell.Controls.Shell
{
    /// <summary>
    /// 中间工作区：多流程 Tab 管理 + 画布层。
    /// 从 MainWindow 拆分而来，负责 Tab 增删改、子程序导航、缩放与视口同步。
    /// </summary>
    public partial class ShellWorkspaceControl : UserControl
    {
        private MainWindowViewModel _viewModel = null!;
        private bool _isTabItemClick;  // 标记是否是通过点击TabItem触发的切换
        private WorkflowCanvasControl? _currentWorkflowCanvas;  // 当前显示的WorkflowCanvasControl
        private Window? _openSubroutineDebugWindow;  // 子程序调试窗口单例跟踪
        private ScrollViewer? _canvasScrollViewer;  // 当前工作流的画布 ScrollViewer（用于视口框 1:1 同步与跳转）

        // 小地图视口拖动状态
        private bool _isDraggingMiniMapView;  // 是否正在小地图上拖动视口框
        private Point _miniMapDragOffset;     // 拖动起点处视口框左上角与小地图点击点的偏移

        // 缩放相关
        private const double MinScale = 0.1;   // 10%
        private const double MaxScale = 2.0;   // 200%

        // 小地图重绘节流：拖动节点时合并每帧重绘，避免饿死连接线批量更新定时器
        private readonly System.Windows.Threading.DispatcherTimer _miniMapThrottleTimer;
        private const double MiniMapThrottleIntervalMs = 40;  // 40ms ≈ 25 帧/秒

        // 画布容器尺寸缓存（值不变时跳过赋值，避免 Extent 变化来回触发 ScrollChanged 死循环）
        private double _lastCanvasSizingWidth = -1;
        private double _lastCanvasSizingHeight = -1;

        /// <summary>
        /// 缩放比例变化事件（替代 MainWindow 中直接操作工具栏 ZoomTextBlock 的跨区域耦合）
        /// </summary>
        public event Action<double>? ZoomScaleChanged;

        public ShellWorkspaceControl()
        {
            InitializeComponent();

            // 小地图重绘节流定时器：单次触发，Tick 时重绘一次后停止
            _miniMapThrottleTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(MiniMapThrottleIntervalMs)
            };
            _miniMapThrottleTimer.Tick += (s, e) =>
            {
                _miniMapThrottleTimer.Stop();
                DrawMiniMap();
            };
        }

        /// <summary>
        /// 注入 ViewModel（MainWindow 构造函数调用）
        /// </summary>
        public void Initialize(MainWindowViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        /// <summary>
        /// 获取当前画布控件
        /// </summary>
        public WorkflowCanvasControl? GetCurrentWorkflowCanvas()
        {
            return _currentWorkflowCanvas;
        }

        /// <summary>
        /// 获取当前画布类型
        /// </summary>
        public CanvasType GetCurrentCanvasType()
        {
            return _viewModel?.WorkflowTabViewModel?.SelectedTab?.CanvasType ?? CanvasType.WorkflowCanvas;
        }

        /// <summary>
        /// 通过代码切换画布类型（用于单元测试和特殊场景）
        /// </summary>
        public void SwitchCanvasType(CanvasType canvasType)
        {
            if (_viewModel?.WorkflowTabViewModel?.SelectedTab == null)
            {
                throw new InvalidOperationException("请先选择一个工作流标签页");
            }
            var currentTab = _viewModel.WorkflowTabViewModel.SelectedTab;
            currentTab.CanvasType = canvasType;
            currentTab.RefreshProperty("CanvasType");
        }

        /// <summary>
        /// 设置路径计算器类型（用于单元测试）
        /// </summary>
        public void SetPathCalculator(string pathCalculatorType)
        {
            var canvasType = GetCurrentCanvasType();
            if (canvasType == CanvasType.WorkflowCanvas)
            {
                var workflowCanvas = FindVisualChild<WorkflowCanvasControl>(this);
                if (workflowCanvas != null)
                {
                    workflowCanvas.SetPathCalculator(pathCalculatorType);
                }
            }
        }

        /// <summary>
        /// 获取当前画布引擎
        /// </summary>
        public ICanvasEngine? GetCurrentCanvasEngine()
        {
            return CanvasEngineManager.GetCurrentEngine();
        }

        #region Tab 管理事件

        /// <summary>
        /// WorkflowCanvasControl加载事件 - 保存引用
        /// </summary>
        private void WorkflowCanvasControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is WorkflowCanvasControl workflowCanvas)
            {
                _currentWorkflowCanvas = workflowCanvas;
                // 缓存当前画布对应的 ScrollViewer，用于视口框 1:1 同步与跳转
                _canvasScrollViewer = FindVisualParent<ScrollViewer>(workflowCanvas);
                // 检查DataContext
                var dataContext = workflowCanvas.DataContext;
                // 如果DataContext为null，手动设置为当前选中的Tab
                if (dataContext == null && _viewModel?.WorkflowTabViewModel?.SelectedTab != null)
                {
                    workflowCanvas.DataContext = _viewModel.WorkflowTabViewModel.SelectedTab;
                    dataContext = workflowCanvas.DataContext;
                }
                // 订阅DataContextChanged事件，以便在CanvasType变化时更新Visibility
                workflowCanvas.DataContextChanged += (s, args) =>
                {
                    UpdateCanvasVisibility();
                };
                // 立即根据CanvasType更新Visibility
                UpdateCanvasVisibility();
                // 延迟更新缩放显示
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateZoomDisplay();
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                // 初始化小地图：数据驱动绘制画布全景
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefreshMiniMap();
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                // 布局完成后再同步画布容器尺寸并自动定位到节点（此时 Viewport 尺寸才就绪）
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateCanvasSizingGridSize();
                    CenterOnWorkflowNodes();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// 根据CanvasType更新画布的Visibility
        /// </summary>
        private void UpdateCanvasVisibility()
        {
            try
            {
                if (_viewModel?.WorkflowTabViewModel?.SelectedTab == null)
                {
                    return;
                }
                var currentTab = _viewModel.WorkflowTabViewModel.SelectedTab;
                var canvasType = currentTab.CanvasType;
                // 查找两个画布的ScrollViewer
                var tabItem = WorkflowTabControl.ItemContainerGenerator.ContainerFromIndex(WorkflowTabControl.SelectedIndex) as TabItem;
                if (tabItem != null)
                {
                    // 查找WorkflowCanvasControl的父级ScrollViewer
                    if (_currentWorkflowCanvas != null)
                    {
                        var workflowScrollViewer = FindVisualParent<ScrollViewer>(_currentWorkflowCanvas);
                        if (workflowScrollViewer != null)
                        {
                            var shouldShow = canvasType == CanvasType.WorkflowCanvas;
                            workflowScrollViewer.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 忽略异常
            }
        }

        /// <summary>
        /// TabControl 加载完成后,监测ScrollViewer的ScrollableWidth变化
        /// </summary>
        private void WorkflowTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 找到ScrollViewer
            var scrollViewer = FindVisualChild<ScrollViewer>(WorkflowTabControl);
            if (scrollViewer != null)
            {
                // 监听ScrollViewer的SizeChanged事件
                scrollViewer.SizeChanged += ScrollViewer_SizeChanged;
                // 初始检查 - 需要传入TabControl的视觉树根元素来查找两个按钮
                UpdateAddButtonPosition(WorkflowTabControl);
            }
        }

        /// <summary>
        /// ScrollViewer大小变化事件 - 更新添加按钮位置
        /// </summary>
        private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                // 从ScrollViewer向上找到TabControl，然后查找两个按钮
                var tabControl = FindVisualParent<TabControl>(scrollViewer);
                if (tabControl != null)
                {
                    UpdateAddButtonPosition(tabControl);
                }
            }
        }

        /// <summary>
        /// 根据ScrollableWidth判断TabItems是否超出,动态调整添加按钮位置
        /// </summary>
        private void UpdateAddButtonPosition(TabControl tabControl)
        {
            // 找到ScrollViewer
            var scrollViewer = FindVisualChild<ScrollViewer>(tabControl);
            if (scrollViewer == null)
                return;
            // 找到两个按钮的Border容器 - 在TabControl的视觉树中查找
            var scrollableButton = FindChildByName<Border>(tabControl, "ScrollableAddButtonBorder");
            var fixedButton = FindChildByName<Border>(tabControl, "FixedAddButtonBorder");
            if (scrollableButton == null || fixedButton == null)
                return;
            // ScrollableWidth > 0 表示有滚动条,即TabItems超出了可视区域
            bool isOverflow = scrollViewer.ScrollableWidth > 0;
            if (isOverflow)
            {
                // 超出时:显示右侧固定按钮,隐藏滚动区域内的按钮
                scrollableButton.Visibility = Visibility.Collapsed;
                fixedButton.Visibility = Visibility.Visible;
            }
            else
            {
                // 未超出时:显示滚动区域内的按钮(跟随TabItems),隐藏右侧固定按钮
                scrollableButton.Visibility = Visibility.Visible;
                fixedButton.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 在视觉树中通过Name查找指定类型的子元素
        /// </summary>
        private T? FindChildByName<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            if (parent == null)
                return null;
            if (parent is T child && (child as FrameworkElement)?.Name == name)
                return child;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var found = FindChildByName<T>(VisualTreeHelper.GetChild(parent, i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// TabControl 选择变化事件 - 根据切换方式决定是否滚动
        /// </summary>
        private void WorkflowTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 退订旧 Tab 的小地图内容订阅（Tab 切换时清理，防内存泄漏）
            foreach (var removed in e.RemovedItems)
            {
                if (removed is WorkflowTabViewModel oldTab)
                {
                    oldTab.WorkflowContentChanged -= MiniMap_WorkflowContentChanged;
                }
            }

            // 用户切换 Tab 时通过唯一写入口 SetSelectedTab 更新，避免双向绑定回写重入
            if (e.AddedItems.Count > 0
                && !ReferenceEquals(_viewModel.WorkflowTabViewModel.SelectedTab, e.AddedItems[0]))
            {
                _viewModel.SetSelectedTab(e.AddedItems[0]);
            }

            // 获取选中的Tab
            var selectedTab = _viewModel.WorkflowTabViewModel.SelectedTab;
            // 优化：更新WorkflowCanvasControl的DataContext（ObservableCollection会自动通知UI更新）
            if (selectedTab != null && _currentWorkflowCanvas != null)
            {
                _currentWorkflowCanvas.DataContext = selectedTab;
            }
            // 优化：合并Dispatcher调用，减少UI重绘次数
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 只有通过下拉器切换时才滚动到中间，点击TabItem时不滚动
                if (!_isTabItemClick)
                {
                    ScrollToSelectedTabItem();
                }
                // 更新添加按钮位置
                UpdateAddButtonPosition(WorkflowTabControl);
                // 重置标志
                _isTabItemClick = false;
                // 应用缩放
                var workflow = _viewModel.WorkflowTabViewModel.SelectedTab;
                if (workflow != null)
                {
                    ApplyZoom(workflow.CurrentScale, workflow.CurrentScale);
                }
                // 更新缩放显示
                UpdateZoomDisplay();
                // 刷新小地图
                RefreshMiniMap();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// TabControl 预览鼠标左键按下事件 - 检测是否点击了TabItem
        /// </summary>
        private void WorkflowTabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 检查点击的是否是TabItem
            var source = e.OriginalSource as DependencyObject;
            if (source != null)
            {
                var tabItem = FindVisualParent<TabItem>(source);
                if (tabItem != null)
                {
                    // 标记为TabItem点击
                    _isTabItemClick = true;
                }
            }
        }

        /// <summary>
        /// 添加工作流点击事件
        /// </summary>
        private void AddWorkflow_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.WorkflowTabViewModel.AddWorkflow();
            _viewModel.StatusText = "已添加新工作流";
            // 自动滚动到新添加的TabItem，使其居中显示
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ScrollToSelectedTabItem();
                // 等待 Canvas 加载完成后应用初始缩放
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var workflow = _viewModel.WorkflowTabViewModel.SelectedTab;
                    if (workflow != null)
                    {
                        var currentScale = workflow.CurrentScale;
                        ApplyZoom(currentScale, currentScale);
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        /// <summary>
        /// 滚动到选中的TabItem，使其显示在可见范围的中间
        /// </summary>
        private void ScrollToSelectedTabItem()
        {
            var selectedTabItem = FindTabItem(_viewModel.WorkflowTabViewModel.SelectedTab);
            if (selectedTabItem != null)
            {
                // 找到ScrollViewer
                var scrollViewer = FindVisualParent<ScrollViewer>(selectedTabItem);
                if (scrollViewer != null)
                {
                    // 获取TabPanel（内容容器）
                    var tabPanel = FindVisualChild<TabPanel>(scrollViewer);
                    if (tabPanel == null)
                        return;
                    // 计算TabItem相对于TabPanel的位置（内容区域的绝对位置）
                    var transform = selectedTabItem.TransformToVisual(tabPanel);
                    var position = transform.Transform(new Point(0, 0));
                    // 计算使TabItem居中的滚动位置
                    var targetOffset = position.X + (selectedTabItem.ActualWidth / 2) - (scrollViewer.ViewportWidth / 2);
                    // 确保滚动位置在有效范围内
                    targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.ScrollableWidth));
                    // 滚动到目标位置，使TabItem居中显示
                    scrollViewer.ScrollToHorizontalOffset(targetOffset);
                }
            }
        }

        /// <summary>
        /// 在视觉树中查找指定类型的所有子元素
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;
            if (parent is T child)
                return child;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var found = FindVisualChild<T>(VisualTreeHelper.GetChild(parent, i));
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// 在视觉树中查找指定数据对应的TabItem
        /// </summary>
        private TabItem? FindTabItem(WorkflowTabViewModel? workflow)
        {
            if (workflow == null)
                return null;
            // 在TabControl的Items中查找TabItem
            var tabControl = WorkflowTabControl;
            if (tabControl == null)
                return null;
            // 通过遍历TabControl的视觉树找到所有TabItem
            var tabItems = new List<TabItem>();
            FindVisualChildren<TabItem>(tabControl, tabItems);
            return tabItems.FirstOrDefault(item => item.DataContext == workflow);
        }

        /// <summary>
        /// 在视觉树中查找指定类型的所有子元素
        /// </summary>
        private void FindVisualChildren<T>(DependencyObject parent, List<T> results) where T : DependencyObject
        {
            if (parent == null)
                return;
            if (parent is T child)
                results.Add(child);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                FindVisualChildren<T>(VisualTreeHelper.GetChild(parent, i), results);
            }
        }

        /// <summary>
        /// 在视觉树中查找指定类型的所有子元素（返回IEnumerable的便捷方法）
        /// </summary>
        private IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            var results = new List<T>();
            FindVisualChildren(parent, results);
            return results;
        }

        /// <summary>
        /// 在视觉树中查找指定类型的父元素
        /// </summary>
        private T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null)
                return null;
            if (parentObject is T parent)
                return parent;
            return FindVisualParent<T>(parentObject);
        }

        /// <summary>
        /// 在视觉树中查找指定类型的所有子元素
        /// </summary>
        private List<T> FindAllVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            var results = new List<T>();
            if (parent == null)
            {
                return results;
            }
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                {
                    results.Add(t);
                }
                results.AddRange(FindAllVisualChildren<T>(child));
            }
            return results;
        }

        #endregion

        #region TabItem 操作

        /// <summary>
        /// TabItem 单次运行点击事件
        /// </summary>
        private void TabItem_SingleRun_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WorkflowTabViewModel workflow)
            {
                // 设置选中的工作流（唯一写入口，避免双向绑定回写重入）
                _viewModel.SetSelectedTab(workflow);
                // 触发 RunWorkflowCommand 的 Execute 方法
                _viewModel.RunWorkflowCommand.Execute(null);
                _viewModel.StatusText = $"单次运行: {workflow.Name}";
            }
        }

        /// <summary>
        /// TabItem 连续运行/停止点击事件
        /// </summary>
        private void TabItem_ContinuousRun_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WorkflowTabViewModel workflow)
            {
                var wasRunning = workflow.IsRunning;
                _viewModel.WorkflowTabViewModel.ToggleContinuous(workflow);
                if (wasRunning)
                    _viewModel.StatusText = $"正在停止: {workflow.Name}";
                else if (workflow.IsRunning)
                    _viewModel.StatusText = $"开始连续运行: {workflow.Name}";
            }
        }

        /// <summary>
        /// 右键菜单 - 重命名工作流
        /// </summary>
        private void TabItem_Rename_Click(object sender, RoutedEventArgs e)
        {
            var workflow = GetWorkflowFromContextMenu(sender);
            if (workflow == null) return;
            workflow.BeginRename();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var tabItem = FindTabItem(workflow);
                if (tabItem == null) return;
                var textBox = FindVisualChild<TextBox>(tabItem);
                if (textBox?.Name == "TabNameEditor")
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        /// <summary>
        /// 重命名TextBox键盘事件 - Enter确认 / Esc取消
        /// </summary>
        private void TabRenameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is WorkflowTabViewModel workflow)
            {
                if (e.Key == Key.Enter)
                {
                    workflow.ConfirmRename();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    workflow.CancelRename();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 重命名TextBox失去焦点 - 确认重命名
        /// </summary>
        private void TabRenameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is WorkflowTabViewModel workflow)
            {
                if (workflow.IsEditingName)
                {
                    workflow.ConfirmRename();
                }
            }
        }

        /// <summary>
        /// 右键菜单 - 删除工作流
        /// </summary>
        private void TabItem_ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            var workflow = GetWorkflowFromContextMenu(sender);
            if (workflow == null) return;
            DeleteWorkflowWithConfirm(workflow);
        }

        #endregion

        #region 子程序导航

        /// <summary>
        /// [↩] 返回按钮点击 - 返回上层工作流
        /// </summary>
        private void NavigateBack_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel?.NavigateBackFromSubroutine() == true)
            {
                _viewModel.StatusText = $"已返回: {_viewModel.WorkflowTabViewModel?.SelectedTab?.EffectiveDisplayName}";
            }
        }

        /// <summary>
        /// [⚙] 子程序参数设置按钮点击 - 打开参数映射对话框
        /// </summary>
        private void SubroutineSettings_Click(object sender, RoutedEventArgs e)
        {
            var selectedTab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (selectedTab == null || !selectedTab.IsInSubroutine)
            {
                _viewModel?.LogWarning("没有选中的子程序", "Navigation");
                return;
            }
            OpenSubroutineParameterDialog(selectedTab);
        }

        /// <summary>
        /// 统一的子程序调试窗口打开逻辑（消除两个入口点的重复代码）
        /// </summary>
        private void OpenSubroutineDebugWindowCore(
            string subroutineId,
            string nodeId,
            string nodeDispName,
            SubroutineParameters parameters)
        {
            // 单例管理：如果同一子程序的调试窗口已打开，激活并返回
            if (_openSubroutineDebugWindow?.IsLoaded == true)
            {
                _openSubroutineDebugWindow.Activate();
                _openSubroutineDebugWindow.Focus();
                return;
            }
            // 1. 找到父工作流 ID
            var parentWorkflowId = FindParentWorkflowId(subroutineId);
            // 2. 父工作流作用域查询服务 → 输入绑定
            var queryService = _viewModel?.CreateDataSourceQueryService(parentWorkflowId);
            // 3. 子工作流内部数据源 → 输出/显示绑定
            var subQueryService = _viewModel?.CreateDataSourceQueryService(subroutineId);
            var internalSources = subQueryService?.GetWorkflowNodeDataSources() ?? new();
            // 4. 创建增量刷新回调：Outputs 变更时按变更类型局部更新模块结果树 + 主界面图像源下拉
            Action<SubroutineOutputChange>? onOutputsChanged = (change) =>
            {
                if (parameters == null || change == null) return;
                _viewModel?.ApplySubroutineOutputChange(change, nodeDispName, nodeId);
            };
            // 5. 绑定路径校验：输入绑定的源节点必须在父工作流中存在（Fail-Fast）
            if (queryService != null && parameters.Inputs.Count > 0)
            {
                var sources = queryService.GetAvailableDataSources(nodeId);
                var registeredNodeIds = new HashSet<string>(sources
                    .Where(s => !string.IsNullOrEmpty(s.SourceNodeId))
                    .Select(s => s.SourceNodeId!));
                foreach (var input in parameters.Inputs)
                {
                    if (string.IsNullOrEmpty(input.BindingSource)) continue;
                    var parts = input.BindingSource.Split('.');
                    if (parts.Length < 1) continue;
                    if (!registeredNodeIds.Contains(parts[0]))
                    {
                        var msg = $"子程序输入 '{input.Name}' 绑定的源节点 '{parts[0]}' 在父工作流中不存在。\n" +
                                  $"请检查是否删除了上游节点或绑定的节点 ID 已变更。\n" +
                                  $"(子程序: {nodeDispName})";
                        ThemeMessageDialog.Show(msg, "绑定路径错误",
                            MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Warning);
                        return; // 不打开调试窗口
                    }
                }
            }
            var debugWindow = SubroutineDebugServiceProvider.GetService().CreateDebugWindow(
                Window.GetWindow(this)!, subroutineId, nodeDispName,
                nodeId, queryService, parameters, internalSources, onOutputsChanged);
            debugWindow.Title = nodeDispName;
            // 单例跟踪：关闭时清理引用
            debugWindow.Closed += (s, e) => _openSubroutineDebugWindow = null;
            _openSubroutineDebugWindow = debugWindow;
            debugWindow.Show();
        }

        /// <summary>
        /// 打开子程序参数设置对话框（从子程序 Tab 导航栏按钮进入）
        /// </summary>
        private void OpenSubroutineParameterDialog(WorkflowTabViewModel tab)
        {
            try
            {
                var parentNodeId = _viewModel?.FindParentSubroutineNodeId(tab.CurrentWorkflowId)
                    ?? tab.CurrentWorkflowId;
                var parameters = FindParentSubroutineParameters(tab.CurrentWorkflowId);
                if (parameters == null)
                {
                    _viewModel?.LogWarning("未找到子程序参数", "Subroutine");
                    return;
                }
                OpenSubroutineDebugWindowCore(
                    tab.CurrentWorkflowId, parentNodeId,
                    tab.EffectiveDisplayName, parameters);
            }
            catch (Exception ex)
            {
                ThemeMessageDialog.Show(
                    $"打开子程序参数设置失败: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    ThemeMessageDialog.MessageIcon.Error);
                _viewModel?.AddLog(LogLevel.Error, $"打开子程序参数设置失败: {ex.Message}", LogSource.UIOperation);
            }
        }

        /// <summary>
        /// 从画布节点双击打开子程序参数设置对话框
        /// </summary>
        public void OpenSubroutineNodeParameterDialog(SunEyeVision.UI.Shared.WorkflowNode node)
        {
            try
            {
                var subroutineId = node.SubroutineId ?? node.Id;
                if (node.Parameters is not SubroutineParameters parameters)
                {
                    _viewModel?.LogWarning($"节点 '{node.Name}' 不是子程序或参数为空", "Subroutine");
                    return;
                }
                OpenSubroutineDebugWindowCore(
                    subroutineId, node.Id,
                    node.DispName, parameters);
            }
            catch (Exception ex)
            {
                ThemeMessageDialog.Show(
                    $"打开子程序参数设置失败: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    ThemeMessageDialog.MessageIcon.Error);
                _viewModel?.AddLog(LogLevel.Error, $"打开子程序参数设置失败: {ex.Message}", LogSource.UIOperation);
            }
        }

        /// <summary>
        /// 在工作流引擎中查找父节点对应的 SubroutineParameters
        /// </summary>
        private SubroutineParameters? FindParentSubroutineParameters(string subroutineWorkflowId)
        {
            if (_viewModel?.WorkflowEngine == null) return null;
            foreach (var workflow in _viewModel.WorkflowEngine.GetAllWorkflows())
            {
                foreach (var node in workflow.Nodes)
                {
                    if (node.SubroutineId == subroutineWorkflowId)
                        return node.Parameters as SubroutineParameters;
                }
            }
            return null;
        }

        /// <summary>
        /// 在所有工作流中查找引用指定子程序工作流的父工作流 ID
        /// </summary>
        private string? FindParentWorkflowId(string subroutineWorkflowId)
        {
            if (_viewModel?.WorkflowEngine == null) return null;
            foreach (var workflow in _viewModel.WorkflowEngine.GetAllWorkflows())
            {
                foreach (var node in workflow.Nodes)
                {
                    if (node.SubroutineId == subroutineWorkflowId)
                        return workflow.Id;
                }
            }
            return null;
        }

        #endregion

        #region 缩放功能

        /// <summary>
        /// 应用缩放变换（支持围绕指定位置缩放）
        /// </summary>
        private void ApplyZoom(double oldScale, double newScale, Point? centerPosition = null, ScrollViewer? scrollViewer = null)
        {
            if (_viewModel.WorkflowTabViewModel.SelectedTab == null)
                return;
            var workflow = _viewModel.WorkflowTabViewModel.SelectedTab;
            // 更新CurrentScale
            workflow.CurrentScale = newScale;
            // 如果没有提供ScrollViewer，尝试查找
            if (scrollViewer == null)
            {
                scrollViewer = GetCurrentScrollViewer();
            }
            // 如果提供了缩放中心且有ScrollViewer，计算并调整滚动偏移
            if (centerPosition.HasValue && scrollViewer != null)
            {
                // 计算缩放前后的比例变化
                var scaleRatio = newScale / oldScale;
                // 如果缩放值没有变化，直接返回
                if (Math.Abs(scaleRatio - 1.0) < 0.0001)
                {
                    return;
                }
                // 获取当前滚动偏移
                var oldHorizontalOffset = scrollViewer.HorizontalOffset;
                var oldVerticalOffset = scrollViewer.VerticalOffset;
                // 计算缩放中心在画布坐标系中的位置（考虑当前缩放）
                var centerInCanvasX = (oldHorizontalOffset + centerPosition.Value.X) / oldScale;
                var centerInCanvasY = (oldVerticalOffset + centerPosition.Value.Y) / oldScale;
                // 应用新的缩放值（不使用CenterX/CenterY，因为我们在调整滚动偏移）
                workflow.ScaleTransform.CenterX = 0;
                workflow.ScaleTransform.CenterY = 0;
                workflow.ScaleTransform.ScaleX = newScale;
                workflow.ScaleTransform.ScaleY = newScale;
                // 计算新的滚动偏移，保持缩放中心指向的内容位置不变
                var newHorizontalOffset = centerInCanvasX * newScale - centerPosition.Value.X;
                var newVerticalOffset = centerInCanvasY * newScale - centerPosition.Value.Y;
                // 应用新的滚动偏移
                scrollViewer.ScrollToHorizontalOffset(newHorizontalOffset);
                scrollViewer.ScrollToVerticalOffset(newVerticalOffset);
            }
            else
            {
                // 没有缩放中心或没有ScrollViewer时，直接应用缩放（用于初始加载或重置）
                workflow.ScaleTransform.CenterX = 0;
                workflow.ScaleTransform.CenterY = 0;
                workflow.ScaleTransform.ScaleX = newScale;
                workflow.ScaleTransform.ScaleY = newScale;
            }
            // 更新显示
            UpdateZoomDisplay();
            UpdateZoomIndicator();
            _viewModel.StatusText = $"画布缩放: {Math.Round(workflow.CurrentScale * 100, 0)}%";
            UpdateCanvasSizingGridSize();   // 缩放后同步画布容器尺寸，限制滚动范围
        }

        /// <summary>
        /// 更新缩放指示器
        /// </summary>
        private void UpdateZoomIndicator()
        {
            // 在当前Tab中查找缩放指示器
            if (_viewModel.WorkflowTabViewModel.SelectedTab != null)
            {
                var container = WorkflowTabControl.ItemContainerGenerator.ContainerFromItem(_viewModel.WorkflowTabViewModel.SelectedTab);
                if (container is TabItem tabItem)
                {
                    var contentPresenter = FindVisualChild<ContentPresenter>(tabItem);
                    if (contentPresenter != null)
                    {
                        var grid = FindVisualChild<Grid>(contentPresenter);
                        if (grid != null)
                        {
                            // 查找所有 TextBlock 元素
                            var textBlocks = FindAllVisualChildren<TextBlock>(grid);
                            foreach (var textBlock in textBlocks)
                            {
                                if (textBlock.Name == "ZoomIndicatorText")
                                {
                                    int percentage = (int)(_viewModel.WorkflowTabViewModel.SelectedTab.CurrentScale * 100);
                                    textBlock.Text = $"{percentage}%";
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 更新缩放百分比显示（通过 ZoomScaleChanged 事件驱动工具栏 ZoomTextBlock）
        /// </summary>
        private void UpdateZoomDisplay()
        {
            // 使用 Dispatcher 延迟执行，确保 TabItem 内容已完全加载
            Dispatcher.BeginInvoke(new Action(() =>
            {
                int percentage = 100;
                try
                {
                    if (_viewModel.WorkflowTabViewModel.SelectedTab != null)
                    {
                        percentage = (int)(_viewModel.WorkflowTabViewModel.SelectedTab.CurrentScale * 100);
                    }
                }
                catch (Exception)
                {
                }
                // 通过事件通知工具栏更新缩放百分比显示（跨区域解耦）
                ZoomScaleChanged?.Invoke(percentage);
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// 鼠标滚轮缩放事件
        /// </summary>
        private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_viewModel.WorkflowTabViewModel.SelectedTab == null)
                return;
            var workflow = _viewModel.WorkflowTabViewModel.SelectedTab;
            // 直接使用滚轮进行缩放（不要求Ctrl键）
            e.Handled = true;
            // sender 就是 ScrollViewer
            if (sender is not ScrollViewer scrollViewer)
                return;
            // 获取鼠标位置
            var mousePositionInScrollViewer = e.GetPosition(scrollViewer);
            if (e.Delta > 0)
            {
                // 向上滚动，放大（每次 +10%，线性步进）
                if (workflow.CurrentScale < MaxScale)
                {
                    var oldScale = workflow.CurrentScale;
                    var newScale = Math.Min(Math.Round(oldScale + 0.1, 1), MaxScale);
                    ApplyZoom(oldScale, newScale, mousePositionInScrollViewer, scrollViewer); // 鼠标位置作为缩放中心
                }
            }
            else
            {
                // 向下滚动，缩小（每次 -10%，线性步进）
                if (workflow.CurrentScale > MinScale)
                {
                    var oldScale = workflow.CurrentScale;
                    var newScale = Math.Max(Math.Round(oldScale - 0.1, 1), MinScale);
                    ApplyZoom(oldScale, newScale, mousePositionInScrollViewer, scrollViewer); // 鼠标位置作为缩放中心
                }
            }
        }

        /// <summary>
        /// 获取当前活动的ScrollViewer
        /// </summary>
        private ScrollViewer GetCurrentScrollViewer()
        {
            try
            {
                if (_viewModel.WorkflowTabViewModel.SelectedTab == null)
                {
                    return null!;
                }
                // TabControl的内容通过ContentPresenter显示在模板中,而不是在TabItem的视觉树中
                // 所以直接从WorkflowTabControl的视觉树中查找ScrollViewer
                var allScrollViewers = FindAllVisualChildren<ScrollViewer>(WorkflowTabControl);
                // 查找名为 CanvasScrollViewer 的
                foreach (var sv in allScrollViewers)
                {
                    if (sv.Name == "CanvasScrollViewer")
                    {
                        return sv;
                    }
                }
                // 如果找不到指定名称的,返回第一个
                if (allScrollViewers.Count > 0)
                {
                    return allScrollViewers[0];
                }
            }
            catch (Exception)
            {
            }
            return null!;
        }

        #endregion

        #region 小地图

        // 小地图画布尺寸（与 XAML MiniMapCanvas 保持一致，正方形与画布等比）
        private const double MiniMapWidth = 187;
        private const double MiniMapHeight = 150;

        private const double CanvasSize = 5000;

        // 节点缩略图配色（按 ToolType 分类，与主画布节点配色一致）
        private static readonly Color MiniMapAlgorithmColor = Color.FromRgb(0x00, 0x66, 0xCC);   // 算法-蓝
        private static readonly Color MiniMapSubroutineColor = Color.FromRgb(0x00, 0x99, 0x66);  // 子程序-绿
        private static readonly Color MiniMapConditionColor = Color.FromRgb(0xFF, 0x88, 0x00);   // 条件-橙
        private static readonly Color MiniMapDefaultColor = Color.FromRgb(0x66, 0x66, 0x66);     // 其他-灰

        // 已订阅节点位置变化监听的节点集合（用于在集合变化时增量订阅/退订）— 基类引用，元素为 WorkflowNodeBase
        private readonly Dictionary<string, WorkflowNodeBase> _miniMapSubscribedNodes = new();

        /// <summary>
        /// 刷新小地图：数据驱动双视图（与主画布共享同一数据源，此处绘制全局视角）
        /// 遍历节点/连线数据画简化缩略图，节点增删/移动时自动重绘
        /// 触发通道：节点 Position 变化 + 40ms 节流合并，避免 LayoutUpdated 每帧风暴饿死连接线定时器
        /// </summary>
        private void RefreshMiniMap()
        {
            var selectedTab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (selectedTab == null || MiniMapCanvas == null)
                return;

            // 订阅画布内容变更：SetNodes/SetConnections 整体替换集合引用时
            // （子程序导航/返回路径），重挂接小地图并重绘，不依赖 Tab 切换事件。
            selectedTab.WorkflowContentChanged -= MiniMap_WorkflowContentChanged;
            selectedTab.WorkflowContentChanged += MiniMap_WorkflowContentChanged;

            // 挂接节点/连线集合变化监听（增删时重绘并增量订阅节点位置）
            selectedTab.WorkflowNodes.CollectionChanged -= MiniMapContent_CollectionChanged;
            selectedTab.WorkflowNodes.CollectionChanged += MiniMapContent_CollectionChanged;
            if (selectedTab.WorkflowConnections != null)
            {
                selectedTab.WorkflowConnections.CollectionChanged -= MiniMapContent_CollectionChanged;
                selectedTab.WorkflowConnections.CollectionChanged += MiniMapContent_CollectionChanged;
            }

            // 订阅当前全部节点的位置变化（节点移动时触发节流重绘）
            SubscribeMiniMapNodes(selectedTab.WorkflowNodes);

            DrawMiniMap();
        }

        /// <summary>
        /// 画布内容变更 - 节点/连接集合被整体替换（SetNodes/SetConnections，
        /// 即子程序导航与返回路径）时重新挂接小地图并重绘。
        /// 数据驱动双视图的核心：小地图始终跟随当前集合，不依赖 Tab 切换事件。
        /// </summary>
        private void MiniMap_WorkflowContentChanged(object? sender, EventArgs e)
        {
            RefreshMiniMap();
        }

        /// <summary>
        /// 增量订阅节点位置变化：仅订阅小地图可见的所有节点，节点移除时退订，避免泄漏
        /// </summary>
        private void SubscribeMiniMapNodes(IEnumerable<WorkflowNodeBase> nodes)
        {
            var currentIds = new HashSet<string>();
            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id))
                    continue;
                currentIds.Add(node.Id);
                if (!_miniMapSubscribedNodes.ContainsKey(node.Id))
                {
                    node.PropertyChanged += MiniMapNode_PropertyChanged;
                    _miniMapSubscribedNodes[node.Id] = node;
                }
            }

            // 退订已不存在的节点
            var staleIds = _miniMapSubscribedNodes.Keys.Where(id => !currentIds.Contains(id)).ToList();
            foreach (var id in staleIds)
            {
                _miniMapSubscribedNodes[id].PropertyChanged -= MiniMapNode_PropertyChanged;
                _miniMapSubscribedNodes.Remove(id);
            }
        }

        /// <summary>
        /// 节点位置变化 - 触发节流重绘（不立即重绘，避免每帧风暴）
        /// </summary>
        private void MiniMapNode_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(WorkflowNode.Position))
                return;
            ScheduleMiniMapRedraw();
        }

        /// <summary>
        /// 内容集合变化 - 增删节点时重绘小地图并增量订阅位置
        /// </summary>
        private void MiniMapContent_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var selectedTab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (selectedTab?.WorkflowNodes != null)
                SubscribeMiniMapNodes(selectedTab.WorkflowNodes);
            ScheduleMiniMapRedraw();
        }

        /// <summary>
        /// 节流调度小地图重绘：40ms 合并高频触发（如拖动节点每帧），保证不饿死连接线批量更新定时器
        /// </summary>
        private void ScheduleMiniMapRedraw()
        {
            if (!_miniMapThrottleTimer.IsEnabled)
                _miniMapThrottleTimer.Start();
        }

        /// <summary>
        /// 画布容器尺寸跟随缩放：容器 = 画布缩放后尺寸（滚动范围 = 画布大小，对齐 VM）。
        /// 缩放由画布内部 RenderTransform 实现，故画布控件保持固定 6000×6000 左上对齐，
        /// 仅容器 Grid 尺寸放大以匹配放大后的画布视觉区域；画布外露出 ScrollViewer 深灰工作区。
        /// </summary>
        private void UpdateCanvasSizingGridSize()
        {
            var scrollViewer = _canvasScrollViewer;
            if (scrollViewer?.Content is not Grid sizingGrid)
                return;
            double scale = _viewModel?.WorkflowTabViewModel?.SelectedTab?.CurrentScale ?? 1.0;
            if (scale <= 0 || !double.IsFinite(scale))
                return;
            double w = CanvasSize * scale;
            double h = CanvasSize * scale;
            if (Math.Abs(w - _lastCanvasSizingWidth) < 0.01 && Math.Abs(h - _lastCanvasSizingHeight) < 0.01)
                return;
            _lastCanvasSizingWidth = w;
            _lastCanvasSizingHeight = h;
            sizingGrid.Width = w;
            sizingGrid.Height = h;
        }

        /// <summary>
        /// 打开工作流后自动滚动到节点包围盒中心（仅调整滚动位置，不改变缩放）。
        /// 让用户打开即见节点，无需手动拖动滚动条。空画布或无有效节点时保持左上角 (0,0)。
        /// </summary>
        private void CenterOnWorkflowNodes()
        {
            var scrollViewer = _canvasScrollViewer;
            var selectedTab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (scrollViewer == null || selectedTab == null || selectedTab.CurrentScale <= 0)
                return;
            var nodes = selectedTab.WorkflowNodes;
            if (nodes == null || nodes.Count == 0)
                return;

            // 计算节点包围盒（过滤无效坐标）
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            bool hasValid = false;
            foreach (var node in nodes)
            {
                if (!double.IsFinite(node.Position.X) || !double.IsFinite(node.Position.Y))
                    continue;
                minX = Math.Min(minX, node.Position.X);
                minY = Math.Min(minY, node.Position.Y);
                maxX = Math.Max(maxX, node.Position.X);
                maxY = Math.Max(maxY, node.Position.Y);
                hasValid = true;
            }
            if (!hasValid)
                return;

            double scale = selectedTab.CurrentScale;
            // 节点包围盒中心（画布坐标）→ 视口居中所需的滚动偏移（缩放后像素）
            double targetX = (minX + maxX) / 2.0 * scale - scrollViewer.ViewportWidth / 2.0;
            double targetY = (minY + maxY) / 2.0 * scale - scrollViewer.ViewportHeight / 2.0;
            // clamp 到合法滚动范围（防滚出画布）
            targetX = Math.Max(0, Math.Min(targetX, scrollViewer.ScrollableWidth));
            targetY = Math.Max(0, Math.Min(targetY, scrollViewer.ScrollableHeight));

            scrollViewer.ScrollToHorizontalOffset(targetX);
            scrollViewer.ScrollToVerticalOffset(targetY);
            ScheduleMiniMapRedraw();
        }

        /// <summary>
        /// 按节点/连线数据绘制小地图：固定按 10000×10000 画布全景等比例显示（不拉伸、居中）
        /// 节点按真实坐标与真实宽高比严格 1:1 摆放（不做任何"为了看得清"的尺寸钳制）；
        /// 连线细线显示流程走向；半透明视口框标出主画布当前可视区域（与真实视口严格 1:1）。
        /// </summary>
        private void DrawMiniMap()
        {
            var selectedTab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (selectedTab == null || MiniMapCanvas == null)
                return;

            MiniMapCanvas.Children.Clear();
            if(_currentWorkflowCanvas==null)return;
            var preview=new System.Windows.Shapes.Rectangle
            {
                Width=MiniMapWidth,Height=MiniMapHeight,Stroke=Brushes.Black,IsHitTestVisible=false,
                Fill=new VisualBrush(_currentWorkflowCanvas)
                {
                    Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,
                    Viewbox=new Rect(0,0,CanvasSize*selectedTab.CurrentScale,CanvasSize*selectedTab.CurrentScale),AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top
                }
            };
            MiniMapCanvas.Children.Add(preview);
            DrawMiniMapViewport(1,0,0);
        }
        /// <summary>
        /// 在主画布可视区域对应的画布坐标矩形映射到小地图上，绘制半透明视口框。
        /// 与节点/连线共用同一比例尺（scale/origin），故与真实视口严格 1:1。
        /// </summary>
        private void DrawMiniMapViewport(double scale, double originX, double originY)
        {
            var scrollViewer = _canvasScrollViewer;
            var selectedTab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (scrollViewer == null || selectedTab == null)
                return;
            if(scrollViewer.ExtentWidth<=0||scrollViewer.ExtentHeight<=0)return;
            var viewMmRect=new Rect(
                scrollViewer.HorizontalOffset/scrollViewer.ExtentWidth*MiniMapWidth,
                scrollViewer.VerticalOffset/scrollViewer.ExtentHeight*MiniMapHeight,
                Math.Min(scrollViewer.ActualWidth/scrollViewer.ExtentWidth*MiniMapWidth,MiniMapWidth),
                Math.Min(scrollViewer.ActualHeight/scrollViewer.ExtentHeight*MiniMapHeight,MiniMapHeight));
            var viewRect = new System.Windows.Shapes.Rectangle
            {
                Width = viewMmRect.Width,
                Height = viewMmRect.Height,
                Fill = Brushes.Transparent,
                Stroke = new SolidColorBrush(Color.FromRgb(250, 98, 18)),
                StrokeThickness = 2
            };
            Canvas.SetLeft(viewRect, viewMmRect.X);
            Canvas.SetTop(viewRect, viewMmRect.Y);
            MiniMapCanvas.Children.Add(viewRect);
        }

        /// <summary>
        /// 主画布滚动变化 - 同步小地图视口框（1:1 跟随）
        /// </summary>
        private void CanvasScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateCanvasSizingGridSize();   // 视口尺寸变化时同步画布容器尺寸
            if (_isDraggingMiniMapView)
                return;  // 拖动跳转期间由跳转逻辑统一重绘，避免拉扯
            ScheduleMiniMapRedraw();
        }

        /// <summary>
        /// 小地图鼠标按下：若点在视口框内则开始拖动；否则把主画布中心跳到点击处。
        /// </summary>
        private void MiniMap_PreviewMouseWheel(object sender,MouseWheelEventArgs e)
        {
            var viewer=_canvasScrollViewer;
            var tab=_viewModel?.WorkflowTabViewModel?.SelectedTab;
            if(viewer==null||tab==null)return;
            if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)
            {if(e.Delta<0)viewer.LineRight();else if(e.Delta>0)viewer.LineLeft();e.Handled=true;}
            if((Keyboard.Modifiers&ModifierKeys.Control)!=0)
            {
                var point=e.GetPosition(MiniMapCanvas);
                var center=new Point(point.X/MiniMapWidth*CanvasSize*tab.CurrentScale-viewer.HorizontalOffset,
                    point.Y/MiniMapHeight*CanvasSize*tab.CurrentScale-viewer.VerticalOffset);
                ApplyZoom(tab.CurrentScale,Math.Clamp(tab.CurrentScale+(e.Delta>0?.1:e.Delta<0?-.1:0),MinScale,MaxScale),center,viewer);
                e.Handled=true;
            }
        }
        private Point _miniMapPointerStart,_miniMapRegionStart;
        private void NavigateVmMiniMap(Point topLeft)
        {
            var viewer=_canvasScrollViewer;
            if(viewer==null||viewer.ExtentWidth<=0||viewer.ExtentHeight<=0)return;
            viewer.ScrollToHorizontalOffset(topLeft.X/MiniMapWidth*viewer.ExtentWidth);
            viewer.ScrollToVerticalOffset(topLeft.Y/MiniMapHeight*viewer.ExtentHeight);
            viewer.UpdateLayout();DrawMiniMap();
        }
        private void MiniMapBorder_MouseLeftButtonDown(object sender,MouseButtonEventArgs e)
        {
            if(e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase)return;
            var viewer=_canvasScrollViewer;
            if(viewer==null||viewer.ExtentWidth<=0||viewer.ExtentHeight<=0)return;
            var point=e.GetPosition(MiniMapCanvas);
            var region=new Rect(viewer.HorizontalOffset/viewer.ExtentWidth*MiniMapWidth,
                viewer.VerticalOffset/viewer.ExtentHeight*MiniMapHeight,
                Math.Min(viewer.ActualWidth/viewer.ExtentWidth*MiniMapWidth,MiniMapWidth),
                Math.Min(viewer.ActualHeight/viewer.ExtentHeight*MiniMapHeight,MiniMapHeight));
            _isDraggingMiniMapView=true;
            var requested=region.Contains(point)?region.Location:new Point(point.X-region.Width/2,point.Y-region.Height/2);
            if(!region.Contains(point))NavigateVmMiniMap(requested);
            _miniMapPointerStart=point;_miniMapRegionStart=requested;
            e.Handled=true;
        }
        /// <summary>
        /// 小地图鼠标移动（拖动视口框）：主画布滚动跟随，保持视口框 1:1。
        /// </summary>
        private void MiniMapBorder_MouseMove(object sender,MouseEventArgs e)
        {
            if(!_isDraggingMiniMapView||e.LeftButton!=MouseButtonState.Pressed)return;
            var point=e.GetPosition(MiniMapCanvas);
            if(point==_miniMapPointerStart)return;
            if(!MiniMapCanvas.IsMouseCaptured)MiniMapCanvas.CaptureMouse();
            NavigateVmMiniMap(_miniMapRegionStart+(point-_miniMapPointerStart));
            e.Handled=true;
        }
        /// <summary>
        /// 小地图鼠标释放 / 失去鼠标捕获 - 结束拖动
        /// </summary>
        private void MiniMapBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndMiniMapDrag();
        }

        private void MiniMapBorder_LostMouseCapture(object sender, MouseEventArgs e)
        {
            EndMiniMapDrag();
        }

        private void EndMiniMapDrag()
        {
            if (!_isDraggingMiniMapView)
                return;
            _isDraggingMiniMapView = false;
            if (MiniMapCanvas.IsMouseCaptured)
                MiniMapCanvas.ReleaseMouseCapture();
            ScheduleMiniMapRedraw();
        }

        /// <summary>
        /// 最小化小地图 → 收起为还原按钮；停止重绘节流，避免隐藏状态下空跑 CPU。
        /// </summary>
        private void MiniMapMinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;  // 阻断冒泡，避免误触发小地图视口跳转
            MiniMapBorder.Visibility = Visibility.Collapsed;
            MiniMapRestoreButton.Visibility = Visibility.Visible;
            _miniMapThrottleTimer.Stop();
        }

        /// <summary>
        /// 还原小地图 → 重新展开并立即重绘一次，显示当前最新流程（不依赖节流延迟）。
        /// </summary>
        private void MiniMapRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            MiniMapRestoreButton.Visibility = Visibility.Collapsed;
            MiniMapBorder.Visibility = Visibility.Visible;
            RefreshMiniMap();
        }

        /// <summary>
        /// 把主画布中心滚动到点击处对应的画布坐标（视口框外点击）
        /// </summary>
        private void CenterMainCanvasOnMiniMapPoint(Point click, double scale, double originX, double originY)
        {
            var scrollViewer = _canvasScrollViewer;
            var selectedTab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (scrollViewer == null || selectedTab == null || selectedTab.CurrentScale <= 0)
                return;

            double canvasViewWidth = scrollViewer.ViewportWidth / selectedTab.CurrentScale;
            double canvasViewHeight = scrollViewer.ViewportHeight / selectedTab.CurrentScale;

            // 点击处对应的画布坐标，并让视口中心对准该点
            double canvasX = (click.X - originX) / scale;
            double canvasY = (click.Y - originY) / scale;
            double canvasViewLeft = canvasX - canvasViewWidth / 2;
            double canvasViewTop = canvasY - canvasViewHeight / 2;

            var maxCanvas = CanvasSize - canvasViewWidth;
            canvasViewLeft = Math.Max(0, Math.Min(canvasViewLeft, maxCanvas));
            canvasViewTop = Math.Max(0, Math.Min(canvasViewTop, CanvasSize - canvasViewHeight));

            ScrollToCanvasPosition(canvasViewLeft, canvasViewTop);
        }

        /// <summary>
        /// 主画布滚动到指定画布坐标（单位：画布原始坐标），缩放后换算成 ScrollViewer 偏移。
        /// </summary>
        private void ScrollToCanvasPosition(double canvasViewLeft, double canvasViewTop)
        {
            var scrollViewer = _canvasScrollViewer;
            var selectedTab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (scrollViewer == null || selectedTab == null || selectedTab.CurrentScale <= 0)
                return;

            scrollViewer.ScrollToHorizontalOffset(canvasViewLeft * selectedTab.CurrentScale);
            scrollViewer.ScrollToVerticalOffset(canvasViewTop * selectedTab.CurrentScale);
            ScheduleMiniMapRedraw();
        }

        /// <summary>
        /// 计算小地图投影参数（比例尺与左上角偏移）；返回 false 表示当前无有效数据
        /// </summary>
        private bool TryGetMiniMapProjection(out double scale, out double originX, out double originY)
        {
            scale = Math.Min(MiniMapWidth / CanvasSize, MiniMapHeight / CanvasSize);
            originX = (MiniMapWidth - CanvasSize * scale) / 2;
            originY = (MiniMapHeight - CanvasSize * scale) / 2;
            return true;
        }

        /// <summary>
        /// 根据节点类型返回缩略图着色（禁用节点灰色、子程序绿色、条件橙色、算法蓝色）
        /// </summary>
        private static Color GetMiniMapNodeColor(WorkflowNode node)
        {
            if (node.IsEnabled == false)
                return Color.FromRgb(0xCC, 0xCC, 0xCC);
            var t = node.ToolType;
            if (!string.IsNullOrEmpty(t))
            {
                if (t.Contains("Subroutine", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("子程序", StringComparison.OrdinalIgnoreCase))
                    return MiniMapSubroutineColor;
                if (t.Contains("Condition", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("条件", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("If", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("Switch", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("逻辑", StringComparison.OrdinalIgnoreCase))
                    return MiniMapConditionColor;
            }
            return MiniMapAlgorithmColor;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从右键菜单获取WorkflowTabViewModel
        /// </summary>
        private static WorkflowTabViewModel? GetWorkflowFromContextMenu(object sender)
        {
            if (sender is MenuItem menuItem)
            {
                var contextMenu = menuItem.Parent as ContextMenu;
                return contextMenu?.DataContext as WorkflowTabViewModel;
            }
            return null;
        }

        /// <summary>
        /// 确认并删除工作流
        /// </summary>
        private void DeleteWorkflowWithConfirm(WorkflowTabViewModel workflow)
        {
            var owner = Window.GetWindow(this);
            if (workflow.IsRunning)
            {
                ThemeMessageDialog.Show(
                    "请先停止该工作流",
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    ThemeMessageDialog.MessageIcon.Warning,
                    owner);
                return;
            }
            var result = ThemeMessageDialog.Show(
                $"确定要删除工作流 '{workflow.Name}' 吗?",
                "确认删除",
                System.Windows.MessageBoxButton.YesNo,
                ThemeMessageDialog.MessageIcon.Question,
                owner);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_viewModel.WorkflowTabViewModel.DeleteWorkflow(workflow))
                {
                    _viewModel.StatusText = $"已删除工作流: {workflow.Name}";
                }
                else
                {
                    ThemeMessageDialog.Show(
                        "至少需要保留一个工作流",
                        "提示",
                        System.Windows.MessageBoxButton.OK,
                        ThemeMessageDialog.MessageIcon.Warning,
                        owner);
                }
            }
        }

        #endregion
    }
}

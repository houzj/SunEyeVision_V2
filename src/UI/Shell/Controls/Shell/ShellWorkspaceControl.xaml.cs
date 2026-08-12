using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SubroutineOutputChange = SunEyeVision.Plugin.SDK.Models.SubroutineOutputChange;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Core;
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

        // 缩放相关
        private const double MinScale = 0.25;  // 25%
        private const double MaxScale = 3.0;   // 300%

        /// <summary>
        /// 缩放比例变化事件（替代 MainWindow 中直接操作工具栏 ZoomTextBlock 的跨区域耦合）
        /// </summary>
        public event Action<double>? ZoomScaleChanged;

        public ShellWorkspaceControl()
        {
            InitializeComponent();
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

                // 初始化小地图：渲染缩略图 + 挂接滚动监听
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    AttachMiniMapScrollListener();
                    RefreshMiniMap();
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
                // 设置选中的工作流
                _viewModel.WorkflowTabViewModel.SelectedTab = workflow;
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
                _viewModel.WorkflowTabViewModel.ToggleContinuous(workflow);
                var action = workflow.IsRunning ? "开始连续运行" : "停止";
                _viewModel.StatusText = $"{action}: {workflow.Name}";
            }
        }

        /// <summary>
        /// TabItem 删除点击事件
        /// </summary>
        private void TabItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WorkflowTabViewModel workflow)
            {
                DeleteWorkflowWithConfirm(workflow);
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
        /// 右键菜单 - 关闭工作流
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
                        System.Windows.MessageBox.Show(msg, "绑定路径错误",
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
                System.Windows.MessageBox.Show(
                    $"打开子程序参数设置失败: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
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
                System.Windows.MessageBox.Show(
                    $"打开子程序参数设置失败: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
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
            UpdateMiniMapViewRect();
            _viewModel.StatusText = $"画布缩放: {Math.Round(workflow.CurrentScale * 100, 0)}%";
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
                // 向上滚动，放大
                if (workflow.CurrentScale < MaxScale)
                {
                    var oldScale = workflow.CurrentScale;
                    var newScale = Math.Min(oldScale * 1.1, MaxScale);
                    ApplyZoom(oldScale, newScale, mousePositionInScrollViewer, scrollViewer); // 鼠标位置作为缩放中心
                }
            }
            else
            {
                // 向下滚动，缩小
                if (workflow.CurrentScale > MinScale)
                {
                    var oldScale = workflow.CurrentScale;
                    var newScale = Math.Max(oldScale / 1.1, MinScale);
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

        // 小地图画布尺寸（与 XAML MiniMapCanvas 保持一致）
        private const double MiniMapWidth = 168;
        private const double MiniMapHeight = 120;

        /// <summary>
        /// 刷新小地图：渲染节点缩略图 + 更新红框（根据当前工作流节点与可视区）
        /// </summary>
        private void RefreshMiniMap()
        {
            var selectedTab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (selectedTab?.WorkflowNodes == null || MiniMapCanvas == null)
                return;

            // 挂接节点集合变化监听（添加/删除节点时刷新小地图）
            selectedTab.WorkflowNodes.CollectionChanged -= MiniMapNodes_CollectionChanged;
            selectedTab.WorkflowNodes.CollectionChanged += MiniMapNodes_CollectionChanged;

            MiniMapCanvas.Children.Clear();

            // 计算节点边界框，决定自适应比例尺
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var node in selectedTab.WorkflowNodes)
            {
                var p = node.Position;
                double nw = node.Width > 0 ? node.Width : 180;
                double nh = node.Height > 0 ? node.Height : 60;
                minX = Math.Min(minX, p.X - nw / 2);
                minY = Math.Min(minY, p.Y - nh / 2);
                maxX = Math.Max(maxX, p.X + nw / 2);
                maxY = Math.Max(maxY, p.Y + nh / 2);
            }

            if (selectedTab.WorkflowNodes.Count == 0)
            {
                MiniMapViewRect.Width = 0;
                MiniMapViewRect.Height = 0;
                return;
            }

            double contentWidth = Math.Max(1, maxX - minX);
            double contentHeight = Math.Max(1, maxY - minY);
            double scaleX = (MiniMapWidth - 8) / contentWidth;
            double scaleY = (MiniMapHeight - 8) / contentHeight;
            double scale = Math.Min(scaleX, scaleY);
            double originX = (MiniMapWidth - contentWidth * scale) / 2 - minX * scale;
            double originY = (MiniMapHeight - contentHeight * scale) / 2 - minY * scale;

            // 绘制节点缩略图（简化矩形）
            foreach (var node in selectedTab.WorkflowNodes)
            {
                double nw = node.Width > 0 ? node.Width : 180;
                double nh = node.Height > 0 ? node.Height : 60;
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = Math.Max(2, nw * scale),
                    Height = Math.Max(2, nh * scale),
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x66, 0xCC)),
                    Opacity = 0.8
                };
                Canvas.SetLeft(rect, originX + (node.Position.X - nw / 2) * scale);
                Canvas.SetTop(rect, originY + (node.Position.Y - nh / 2) * scale);
                MiniMapCanvas.Children.Add(rect);
            }

            UpdateMiniMapViewRect();
        }

        /// <summary>
        /// 节点集合变化 - 刷新小地图缩略图
        /// </summary>
        private void MiniMapNodes_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => RefreshMiniMap()), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 更新小地图红框：根据当前 ScrollViewer 可视区（offset/viewport/scale）映射
        /// </summary>
        private void UpdateMiniMapViewRect()
        {
            var selectedTab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (selectedTab?.WorkflowNodes == null || MiniMapCanvas == null || MiniMapViewRect == null)
                return;

            // 空工作流：无节点可映射，隐藏红框避免无穷大计算
            if (selectedTab.WorkflowNodes.Count == 0)
            {
                MiniMapViewRect.Width = 0;
                MiniMapViewRect.Height = 0;
                return;
            }

            // 节点边界框（与 RefreshMiniMap 相同的比例尺计算）
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var node in selectedTab.WorkflowNodes)
            {
                double nw = node.Width > 0 ? node.Width : 180;
                double nh = node.Height > 0 ? node.Height : 60;
                minX = Math.Min(minX, node.Position.X - nw / 2);
                minY = Math.Min(minY, node.Position.Y - nh / 2);
                maxX = Math.Max(maxX, node.Position.X + nw / 2);
                maxY = Math.Max(maxY, node.Position.Y + nh / 2);
            }
            double contentWidth = Math.Max(1, maxX - minX);
            double contentHeight = Math.Max(1, maxY - minY);
            double scaleX = (MiniMapWidth - 8) / contentWidth;
            double scaleY = (MiniMapHeight - 8) / contentHeight;
            double scale = Math.Min(scaleX, scaleY);
            double originX = (MiniMapWidth - contentWidth * scale) / 2 - minX * scale;
            double originY = (MiniMapHeight - contentHeight * scale) / 2 - minY * scale;

            var scrollViewer = GetCurrentScrollViewer();
            if (scrollViewer == null)
            {
                MiniMapViewRect.Width = 0;
                MiniMapViewRect.Height = 0;
                return;
            }

            double currentScale = selectedTab.CurrentScale > 0 ? selectedTab.CurrentScale : 1.0;
            // 可视区在画布坐标下的范围
            double viewLeft = scrollViewer.HorizontalOffset / currentScale;
            double viewTop = scrollViewer.VerticalOffset / currentScale;
            double viewRight = viewLeft + scrollViewer.ViewportWidth / currentScale;
            double viewBottom = viewTop + scrollViewer.ViewportHeight / currentScale;

            double left = originX + viewLeft * scale;
            double top = originY + viewTop * scale;
            double width = (viewRight - viewLeft) * scale;
            double height = (viewBottom - viewTop) * scale;

            MiniMapViewRect.Width = Math.Max(3, width);
            MiniMapViewRect.Height = Math.Max(3, height);
            Canvas.SetLeft(MiniMapViewRect, left);
            Canvas.SetTop(MiniMapViewRect, top);
        }

        /// <summary>
        /// 挂接画布 ScrollViewer 滚动监听（小地图红框跟随）
        /// </summary>
        private void AttachMiniMapScrollListener()
        {
            var scrollViewer = GetCurrentScrollViewer();
            if (scrollViewer == null)
                return;
            scrollViewer.ScrollChanged -= MiniMapScrollViewer_ScrollChanged;
            scrollViewer.ScrollChanged += MiniMapScrollViewer_ScrollChanged;
        }

        /// <summary>
        /// ScrollViewer 滚动变化 - 更新小地图红框
        /// </summary>
        private void MiniMapScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateMiniMapViewRect();
        }

        /// <summary>
        /// 小地图点击：跳转到对应画布位置
        /// </summary>
        private void MiniMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var selectedTab = _viewModel?.WorkflowTabViewModel?.SelectedTab;
            if (selectedTab?.WorkflowNodes == null || MiniMapCanvas == null || selectedTab.WorkflowNodes.Count == 0)
                return;

            var pos = e.GetPosition(MiniMapCanvas);

            // 反向映射：节点边界框 → 比例尺
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var node in selectedTab.WorkflowNodes)
            {
                double nw = node.Width > 0 ? node.Width : 180;
                double nh = node.Height > 0 ? node.Height : 60;
                minX = Math.Min(minX, node.Position.X - nw / 2);
                minY = Math.Min(minY, node.Position.Y - nh / 2);
                maxX = Math.Max(maxX, node.Position.X + nw / 2);
                maxY = Math.Max(maxY, node.Position.Y + nh / 2);
            }
            double contentWidth = Math.Max(1, maxX - minX);
            double contentHeight = Math.Max(1, maxY - minY);
            double scaleX = (MiniMapWidth - 8) / contentWidth;
            double scaleY = (MiniMapHeight - 8) / contentHeight;
            double scale = Math.Min(scaleX, scaleY);
            double originX = (MiniMapWidth - contentWidth * scale) / 2 - minX * scale;
            double originY = (MiniMapHeight - contentHeight * scale) / 2 - minY * scale;

            double canvasX = (pos.X - originX) / scale;
            double canvasY = (pos.Y - originY) / scale;

            var scrollViewer = GetCurrentScrollViewer();
            if (scrollViewer == null)
                return;
            double currentScale = selectedTab.CurrentScale > 0 ? selectedTab.CurrentScale : 1.0;
            scrollViewer.ScrollToHorizontalOffset(canvasX * currentScale - scrollViewer.ViewportWidth / 2);
            scrollViewer.ScrollToVerticalOffset(canvasY * currentScale - scrollViewer.ViewportHeight / 2);
            UpdateMiniMapViewRect();
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
            if (workflow.IsRunning)
            {
                System.Windows.MessageBox.Show(
                    "请先停止该工作流",
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }
            var result = System.Windows.MessageBox.Show(
                $"确定要删除工作流 '{workflow.Name}' 吗?",
                "确认删除",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_viewModel.WorkflowTabViewModel.DeleteWorkflow(workflow))
                {
                    _viewModel.StatusText = $"已删除工作流: {workflow.Name}";
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "至少需要保留一个工作流",
                        "提示",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
        }

        #endregion
    }
}

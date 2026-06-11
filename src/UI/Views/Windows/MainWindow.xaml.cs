
using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using SunEyeVision.UI.Views.Controls.Canvas;
using SunEyeVision.UI.Views.Controls.Panels;
using SunEyeVision.UI.Views.Controls.Common;
using SunEyeVision.UI.Models;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.ViewModels;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

using AIStudio.Wpf.DiagramDesigner;
using AIStudio.Wpf.DiagramDesigner.ViewModels;

using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Converters.Path;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Tool.Subroutine;
using SunEyeVision.Tool.Subroutine.Models;






namespace SunEyeVision.UI.Views.Windows

{

    /// <summary>

    /// MainWindow - 太阳眼视觉风格的主界面窗口

    /// 实现完整的机器视觉平台主界面，包含工作流画布、工具箱、属性面板等

    /// </summary>

    public partial class MainWindow : Window

    {

        private readonly MainWindowViewModel _viewModel;

        private bool _isTabItemClick = false;  // 标记是否是通过点击TabItem触发的切换

        private WorkflowCanvasControl? _currentWorkflowCanvas = null;  // 当前显示的WorkflowCanvasControl
        private Window? _openSubroutineDebugWindow;  // 子程序调试窗口单例跟踪




        // 画布引擎管理器容器

        public System.Windows.Controls.Decorator CanvasContainer { get; private set; } = new System.Windows.Controls.Decorator();



        // 缩放相关

        private const double MinScale = 0.25;  // 25%

        private const double MaxScale = 3.0;   // 300%



        // 画布虚拟大小

        private const double CanvasVirtualWidth = 5000;

        private const double CanvasVirtualHeight = 5000;









        public MainWindow()

        {

            InitializeComponent();

            _viewModel = new MainWindowViewModel();

            DataContext = _viewModel;

            // ★ 设置获取主窗口 ImageControl 的委托
            _viewModel.GetMainImageControl = () => ImageDisplayContent;



            // 初始化画布引擎管理器 - 设置默认引擎

            CanvasEngineManager.SetDataContext(_viewModel.WorkflowTabViewModel?.SelectedTab);



            RegisterHotkeys();



            // 后台切换到NativeDiagramControl（使用原生AIStudio.Wpf.DiagramDesigner库）

            SwitchToDefaultConfiguration();

            // Force initialize log panel - fix TabControl lazy loading
            // LogPanelContent is at index 1 (second tab)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (BottomPanelTabControl != null && BottomPanelTabControl.Items.Count > 1)
                {
                    BottomPanelTabControl.SelectedIndex = 1; // Select log tab to trigger load
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);

        }






        /// <summary>

        /// 切换到默认配置：WorkflowCanvasControl画布 + BezierPathCalculator路径计算器

        /// </summary>

        private void SwitchToDefaultConfiguration()

        {

            try

            {

                // 切换画布到WorkflowCanvasControl（自定义画布）

                if (_viewModel?.WorkflowTabViewModel?.SelectedTab != null)

                {

                    _viewModel.WorkflowTabViewModel.SelectedTab.CanvasType = CanvasType.WorkflowCanvas;

                    _viewModel.WorkflowTabViewModel.SelectedTab.RefreshProperty("CanvasType");

                }



                // 设置路径计算器为 Bezier（贝塞尔曲线）

                CanvasEngineManager.SetPathCalculator("Bezier");

            }

            catch (Exception ex)

            {

                // 忽略异常

            }

        }



        /// <summary>

        /// 切换到WorkflowCanvasControl画布（使用贝塞尔曲线）

        /// </summary>

        private void SwitchToWorkflowCanvasConfiguration()

        {

            try

            {

                // 切换画布到WorkflowCanvasControl（自定义画布）

                if (_viewModel?.WorkflowTabViewModel?.SelectedTab != null)

                {

                    _viewModel.WorkflowTabViewModel.SelectedTab.CanvasType = CanvasType.WorkflowCanvas;

                    _viewModel.WorkflowTabViewModel.SelectedTab.RefreshProperty("CanvasType");



                    // 使用 CanvasEngineManager 设置路径计算器为贝塞尔曲线

                    CanvasEngineManager.SetPathCalculator("Bezier");

                }

            }

            catch (Exception ex)

            {

                // 忽略异常

            }

        }





        /// <summary>

        /// 注册快捷键

        /// </summary>

        private void RegisterHotkeys()

        {

            // 文件操作快捷键

            InputBindings.Add(new KeyBinding(_viewModel.NewWorkflowCommand, Key.N, ModifierKeys.Control));

            InputBindings.Add(new KeyBinding(_viewModel.OpenWorkflowCommand, Key.O, ModifierKeys.Control));

            InputBindings.Add(new KeyBinding(_viewModel.SaveWorkflowCommand, Key.S, ModifierKeys.Control));



            // 运行控制快捷键

            InputBindings.Add(new KeyBinding(_viewModel.RunWorkflowCommand, Key.F5, ModifierKeys.None));

            InputBindings.Add(new KeyBinding(_viewModel.StopWorkflowCommand, Key.F5, ModifierKeys.Shift));



            // 帮助快捷键

            InputBindings.Add(new KeyBinding(_viewModel.ShowHelpCommand, Key.F1, ModifierKeys.None));

            InputBindings.Add(new KeyBinding(new PauseCommandWrapper(_viewModel.PauseCommand), Key.Pause, ModifierKeys.None));



            // 编辑快捷键

            InputBindings.Add(new KeyBinding(new UndoCommandWrapper(_viewModel.UndoCommand), Key.Z, ModifierKeys.Control));

            InputBindings.Add(new KeyBinding(new RedoCommandWrapper(_viewModel.RedoCommand), Key.Y, ModifierKeys.Control));

            InputBindings.Add(new KeyBinding(_viewModel.DeleteSelectedNodesCommand, Key.Delete, ModifierKeys.None));

        }



        #region 窗口事件

        /// <summary>
        /// 窗口关闭前事件 - 提示保存解决方案
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 检查是否有当前解决方案
                var solutionManager = Adapters.ServiceInitializer.SolutionManager;

                // ==================== 场景1：没有打开的解决方案 ====================
                if (solutionManager?.CurrentSolution == null)
                {
                    // ✅ 提示用户是否新建解决方案
                    var result = System.Windows.MessageBox.Show(
                        "当前没有打开的解决方案，是否新建一个解决方案？",
                        "新建解决方案",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        if (CreateNewSolutionBeforeClosing(e))
                        {
                            base.OnClosing(e);
                        }
                    }
                    else
                    {
                        _viewModel?.AddLog(LogLevel.Info, "未新建解决方案，直接关闭", LogSource.UIOperation);
                        base.OnClosing(e);
                    }
                    return;
                }

                // ==================== 场景2：有打开的解决方案 ====================
                // 获取当前解决方案的元数据
                var metadata = solutionManager.GetMetadata(solutionManager.CurrentSolution.Id);
                var solutionName = metadata?.Name ?? "未命名解决方案";

                // 提示用户保存
                var saveResult = System.Windows.MessageBox.Show(
                    $"是否保存当前解决方案 '{solutionName}' 的修改？",
                    "保存解决方案",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                switch (saveResult)
                {
                    case MessageBoxResult.Yes:
                        // 保存解决方案
                        try
                        {
                            SyncWorkflowsToSolution();
                            solutionManager.SaveSolution();
                            _viewModel?.AddLog(LogLevel.Success, "关闭前已保存解决方案", LogSource.UIOperation);
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show(
                                $"保存解决方案失败: {ex.Message}",
                                "保存失败",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            e.Cancel = true;
                            return;
                        }
                        break;

                    case MessageBoxResult.Cancel:
                        // 取消关闭
                        e.Cancel = true;
                        return;

                    case MessageBoxResult.No:
                        // 不保存，直接关闭
                        _viewModel?.AddLog(LogLevel.Info, "未保存解决方案，直接关闭", LogSource.UIOperation);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"关闭窗口时发生错误: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            base.OnClosing(e);
        }

        /// <summary>
        /// 关闭前创建新解决方案
        /// </summary>
        /// <param name="e">取消事件参数</param>
        /// <returns>是否成功创建并保存解决方案</returns>
        private bool CreateNewSolutionBeforeClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                var solutionManager = Adapters.ServiceInitializer.SolutionManager;

                // ✅ 统一使用解决方案管理器的默认目录
                var defaultPath = solutionManager.SolutionsDirectory;

                var newDialog = new NewSolutionDialog(defaultPath)
                {
                    Owner = this,
                    Title = "新建解决方案"
                };

                var result = newDialog.ShowDialog();

                if (result != true || string.IsNullOrEmpty(newDialog.SolutionName))
                {
                    e.Cancel = true;
                    return false;
                }

                // 创建元数据
                var metadata = new SunEyeVision.Workflow.SolutionMetadata
                {
                    Name = newDialog.SolutionName,
                    Description = newDialog.Description ?? "",
                    DirectoryPath = newDialog.SolutionPath
                };

                // ✅ 修复：直接创建Solution对象并保存
                // 流程：ViewModel → Solution → 文件
                // Name 和 Description 由 SolutionMetadata 统一管理
                var solution = SunEyeVision.Workflow.Solution.Create();
                solution.Id = metadata.Id;
                solution.Version = metadata.Version ?? "1.0";

                // 构建文件路径
                var filePath = System.IO.Path.Combine(metadata.DirectoryPath, $"{metadata.Name}.solution");
                solution.FilePath = filePath;

                // 从ViewModel同步工作流
                _viewModel?.AddLog(LogLevel.Info, $"开始同步工作流到解决方案: {metadata.Name}", LogSource.UIOperation);
                foreach (var tab in _viewModel.WorkflowTabViewModel.Tabs)
                {
                    var workflow = new SunEyeVision.Workflow.Workflow(tab.Id, tab.Name);
                    solution.Workflows.Add(workflow);

                    // 同步节点数据（同一对象引用）
                    foreach (var node in tab.WorkflowNodes)
                    {
                        workflow.Nodes.Add(node);
                    }

                    // 同步连接数据（共享引用，直接加载）
                    foreach (var conn in tab.WorkflowConnections)
                    {
                        workflow.Connections.Add(conn);
                    }

                    _viewModel?.AddLog(LogLevel.Info, $"已同步工作流: {tab.Name}, 节点数={tab.WorkflowNodes.Count}, 连接数={tab.WorkflowConnections.Count}", LogSource.UIOperation);
                }

                // 直接保存Solution对象（不依赖CurrentSolution）
                bool saveSuccess = solutionManager.SaveSolutionDirect(solution, filePath, metadata);

                if (!saveSuccess)
                {
                    System.Windows.MessageBox.Show("保存解决方案失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Cancel = true;
                    return false;
                }

                _viewModel?.AddLog(LogLevel.Success, $"已创建并保存新解决方案: {metadata.Name} -> {filePath}", LogSource.UIOperation);
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"创建解决方案失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Cancel = true;
                return false;
            }
        }

        protected override void OnClosed(EventArgs e)

        {

            // 取消订阅事件，防止内存泄漏
            if (ImagePreviewContent != null)
            {
                ImagePreviewContent.WorkflowExecutionRequested -= OnWorkflowExecutionRequested;
            }

            // TODO: 清理资源

            _viewModel?.StopWorkflowCommand.Execute(null);

            base.OnClosed(e);

        }

        /// <summary>
        /// 同步UI层的工作流到底层解决方案
        /// </summary>
        /// <remarks>
        /// 节点已直接引用 Solution.Workflow.Nodes，无需重建 Workflow。
        /// 与 MainWindowViewModel.SaveSolutionData 保持一致的同步逻辑。
        /// </remarks>
        private void SyncWorkflowsToSolution()
        {
            if (_viewModel?.WorkflowTabViewModel == null || _viewModel?.WorkflowTabViewModel.Tabs == null)
                return;

            var solutionManager = Adapters.ServiceInitializer.SolutionManager;
            if (solutionManager?.CurrentSolution == null)
                return;

            var solution = solutionManager.CurrentSolution;

            foreach (var tab in _viewModel.WorkflowTabViewModel.Tabs)
            {
                var workflow = solution.Workflows.FirstOrDefault(w => w.Id == tab.Id);

                if (workflow == null)
                {
                    workflow = new SunEyeVision.Workflow.Workflow(tab.Id, tab.Name);
                    solution.Workflows.Add(workflow);
                }
                else if (workflow.Name != tab.Name)
                {
                    workflow.Name = tab.Name;
                }

                // 同步节点
                foreach (var node in tab.WorkflowNodes)
                {
                    if (!workflow.Nodes.Contains(node))
                        workflow.Nodes.Add(node);
                }

                var nodesToRemove = workflow.Nodes
                    .Where(n => !tab.WorkflowNodes.Contains(n))
                    .ToList();
                foreach (var node in nodesToRemove)
                    workflow.Nodes.Remove(node);


                // 同步连接（共享引用，直接加载）
                foreach (var conn in tab.WorkflowConnections)
                {
                    if (!workflow.Connections.Any(c => c.Id == conn.Id))
                    {
                        workflow.Connections.Add(conn);
                    }
                }

                var connsToRemove = workflow.Connections
                    .Where(c => !tab.WorkflowConnections.Any(tc => tc.Id == c.Id))
                    .ToList();
                foreach (var conn in connsToRemove)
                    workflow.Connections.Remove(conn);
            }
        }



        private void MainWindow_Loaded(object sender, RoutedEventArgs e)

        {

            try

            {

                // ★ 手动订阅ImagePreviewControl的工作流执行请求事件

                // 注意：普通CLR事件不能通过XAML订阅，必须在代码后台手动订阅

                if (ImagePreviewContent != null)

                {

                    ImagePreviewContent.WorkflowExecutionRequested += OnWorkflowExecutionRequested;
}

                else

                {
}

                // ★ 设置 ResultRenderLayer 引用到 NodeResultManager
                if (ImageDisplayContent != null)
                {
                    var resultRenderLayer = ImageDisplayContent.ResultRenderLayerControl;
                    if (resultRenderLayer != null)
                    {
                        // 通过 ViewModel 获取 NodeResultManager 并设置 ResultRenderLayer
                        var nodeResultManager = GetNodeResultManager(_viewModel);
                        nodeResultManager?.SetResultRenderLayer(resultRenderLayer);
                        
                        // ★ 同步视口缩放（幂律公式：线宽 / 缩放 = 视觉恒定）
                        resultRenderLayer.SetViewportScale(_viewModel.ImageScale);
                    }
                }



                // 工具插件现在通过ToolboxViewModel自动加载

                var toolCount = ToolRegistry.GetToolCount();

                _viewModel.StatusText = $"已加载 {toolCount} 个工具插件";



                // 初始化智能路径转换器的节点集合（使用当前选中的 Tab 的节点集合）

                if (_viewModel.WorkflowTabViewModel?.SelectedTab != null)

                {

                    SmartPathConverter.Nodes = _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowNodes;

                    SmartPathConverter.Connections = _viewModel.WorkflowTabViewModel.SelectedTab.WorkflowConnections;

                }



                // 初始化缩放显示

                Dispatcher.BeginInvoke(new Action(() =>

                {

                    UpdateZoomDisplay();

                }), System.Windows.Threading.DispatcherPriority.Loaded);



                // 注释：以下代码已废弃，工具箱分隔器已删除（2026-02-10）

                /*

                // 同步工具箱分隔线箭头方向

                Dispatcher.BeginInvoke(new Action(() =>

                {

                    UpdateToolboxSplitterArrow();

                }), System.Windows.Threading.DispatcherPriority.Loaded);

                */



                // TODO: 加载工作流

            }

            catch (Exception ex)

            {

                System.Windows.MessageBox.Show(

                    $"加载工具插件时出错: {ex.Message}",

                    "加载失败",

                    System.Windows.MessageBoxButton.OK,

                    System.Windows.MessageBoxImage.Warning);

            }

        }

        /// <summary>
        /// 获取 MainWindowViewModel 中的 NodeResultManager
        /// </summary>
        private Services.Workflow.NodeResultManager? GetNodeResultManager(ViewModels.MainWindowViewModel viewModel)
        {
            // 通过反射获取私有字段
            var field = typeof(ViewModels.MainWindowViewModel).GetField("_nodeResultManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(viewModel) as Services.Workflow.NodeResultManager;
        }



        #endregion



        #region 初始化



        /// <summary>

        /// 主内容区域加载完成

        /// </summary>

        private void MainContentGrid_Loaded(object sender, RoutedEventArgs e)

        {

            // 初始化缩放显示

            UpdateZoomDisplay();

        }



        #endregion



        #region TabControl 多流程管理事件处理



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

        private void WorkflowTabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)

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

                    // TabItem中心位置 = position.X + selectedTabItem.ActualWidth / 2

                    // 视口中心位置 = scrollViewer.ViewportWidth / 2

                    // 目标滚动位置 = TabItem中心位置 - 视口中心位置

                    var targetOffset = position.X + (selectedTabItem.ActualWidth / 2) - (scrollViewer.ViewportWidth / 2);



                    // 确保滚动位置在有效范围内

                    targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.ScrollableWidth));



                    // 滚动到目标位置，使TabItem居中显示

                    scrollViewer.ScrollToHorizontalOffset(targetOffset);

                }

            }

        }



        /// <summary>

        /// 在视觉树中查找指定类型的第一个子元素

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

        /// 获取当前显示的WorkflowCanvasControl

        /// </summary>

        public WorkflowCanvasControl? GetCurrentWorkflowCanvas()

        {

            return _currentWorkflowCanvas;

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



        /// <summary>

        /// TabItem 单次运行点击事件

        /// </summary>

        private async void TabItem_SingleRun_Click(object sender, RoutedEventArgs e)

        {

            if (sender is Button button && button.Tag is WorkflowTabViewModel workflow)

            {

                // 设置选中的工作流

                _viewModel.WorkflowTabViewModel.SelectedTab = workflow;



                // 触发 RunWorkflowCommand 的 Execute 方法

                // RunWorkflowCommand 是异步命令，Execute 方法会启动异步任务

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

        #region 子程序导航事件

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

            // 4. 创建实时刷新回调：Outputs 变更时立即更新模块结果树
            Action? onOutputsChanged = () =>
            {
                if (parameters == null) return;
                var tempResults = new SubroutineResults
                {
                    OutputConfigs = parameters.Outputs.ToList()
                };
                _viewModel?.UpdateModuleResults(tempResults, nodeDispName, "Subroutine", nodeId);
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

            var debugWindow = SubroutinePlugin.ShowDebugWindow(this, subroutineId, nodeDispName,
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
        internal void OpenSubroutineNodeParameterDialog(Models.WorkflowNode node)
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
        private SunEyeVision.Tool.Subroutine.Models.SubroutineParameters? FindParentSubroutineParameters(string subroutineWorkflowId)
        {
            if (_viewModel?.WorkflowEngine == null) return null;

            foreach (var workflow in _viewModel.WorkflowEngine.GetAllWorkflows())
            {
                foreach (var node in workflow.Nodes)
                {
                    if (node.SubroutineId == subroutineWorkflowId)
                        return node.Parameters as SunEyeVision.Tool.Subroutine.Models.SubroutineParameters;
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

private void OnWorkflowExecutionRequested(object sender, WorkflowExecutionRequestEventArgs e)

        {
if (_viewModel?.WorkflowTabViewModel?.SelectedTab == null)

            {

                _viewModel?.AddLog(LogLevel.Error, "没有选中的工作流，无法执行", LogSource.UIOperation);

                return;

            }



            var workflow = _viewModel.WorkflowTabViewModel.SelectedTab;



            // ★ 注入当前图像路径到运行时参数（不设置 ViewModel.CurrentImageIndex）
            // 避免 UpdateCurrentImageDisplay() 加载源图后立即被工作流结果替换导致的闪烁

            _viewModel.SetRuntimeParameter("CurrentImagePath", e.ImageInfo.FilePath);

            _viewModel.SetRuntimeParameter("CurrentImageIndex", e.Index);



            var workflowName = workflow.Name ?? "未知工作流";
            _viewModel.AddLog(LogLevel.Info, $"执行工作流 - 图像: {e.ImageInfo.Name}", LogSource.Runtime(workflowName));

            _viewModel.AddLog(LogLevel.Info, $"路径: {e.ImageInfo.FilePath}", LogSource.Runtime(workflowName));



            // 触发工作流执行（DisplayImage 由工作流结果驱动更新，无需在此预加载源图）

            _viewModel.RunWorkflowCommand.Execute(null);



            _viewModel.StatusText = $"运行工作流: {workflow.Name} - {e.ImageInfo.Name}";

        }





        #endregion



        #region WorkflowCanvasControl 事件处理



        /// <summary>

        /// 节点添加事件处理

        /// </summary>

        private void OnWorkflowCanvas_NodeAdded(object sender, WorkflowNode node)

        {

            _viewModel.StatusText = $"添加节点: {node.Name}";

        }



        /// <summary>

        /// 节点选中事件处理

        /// </summary>

        private void OnWorkflowCanvas_NodeSelected(object sender, WorkflowNode node)

        {

            // 通过属性访问，触发属性变更通知和后续逻辑

            _viewModel.SelectedNode = node;

        }



        /// <summary>

        /// 节点双击事件处理

        /// </summary>

        private void OnWorkflowCanvas_NodeDoubleClicked(object sender, WorkflowNode node)

        {

            _viewModel.OpenDebugWindowCommand.Execute(node);

        }



        #endregion



        #region 拖放事件



        private void ToolItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)

        {

            if (sender is Border border && border.Tag is Models.ToolItem tool)

            {

                var dragData = new DataObject("ToolItem", tool);

                DragDrop.DoDragDrop(border, dragData, DragDropEffects.Copy);

            }

        }



        private void WorkflowCanvas_DragEnter(object sender, DragEventArgs e)

        {

            if (e.Data.GetDataPresent("ToolItem"))

            {

                e.Effects = DragDropEffects.Copy;

            }

            else

            {

                e.Effects = DragDropEffects.None;

            }

        }



        private void WorkflowCanvas_DragOver(object sender, DragEventArgs e)

        {

            if (e.Data.GetDataPresent("ToolItem"))

            {

                e.Effects = DragDropEffects.Copy;

            }

            else

            {

                e.Effects = DragDropEffects.None;

            }

        }



        private void WorkflowCanvas_DragLeave(object sender, DragEventArgs e)

        {

            // 可选:添加离开画布时的视觉效果

        }



        private void WorkflowCanvas_Drop(object sender, DragEventArgs e)

        {

            try

            {

                if (e.Data.GetData("ToolItem") is Models.ToolItem tool)

                {

                    var position = e.GetPosition(sender as Canvas);



                    // 使用工厂创建新节点（遵循rule-008: 原型设计期代码纯净原则）
                    var selectedTab = _viewModel.WorkflowTabViewModel.SelectedTab;
                    if (selectedTab == null)
                    {
                        _viewModel.LogWarning("没有选中工作流标签页");
                        return;
                    }

                    var node = selectedTab.WorkflowNodeFactory.CreateNode(tool.ToolId);



                    // 设置拖放位置(居中放置,节点大小140x90)

                    var x = Math.Max(0, position.X - 70);

                    var y = Math.Max(0, position.Y - 45);

                    node.Position = new System.Windows.Point(x, y);



                    // 使用命令模式添加节点

                    _viewModel.AddNodeToWorkflow(node);

                }

            }

            catch (Exception ex)

            {

                System.Windows.MessageBox.Show($"添加节点时出错: {ex.Message}", "错误",

                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

            }

        }



        #endregion



        #region 缩放功能





















        /// <summary>

        /// 诊断方法:打印视觉树层次结构

        /// </summary>

        private void PrintVisualTree(DependencyObject parent, int indent = 0)

        {

            // 注意：此方法未使用，如果需要输出日志，应使用 _viewModel?.AddLog

            string prefix = new string(' ', indent * 2);



            int childCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childCount; i++)

            {

                var child = VisualTreeHelper.GetChild(parent, i);

                PrintVisualTree(child, indent + 1);

            }

        }



















        /// <summary>

        /// 切换到正交折线画布 (WorkflowCanvas)

        /// </summary>

        private void SwitchToWorkflowCanvas_Click(object sender, RoutedEventArgs e)

        {

            SwitchToWorkflowCanvasConfiguration();

        }



        /// <summary>

        /// 切换到贝塞尔曲线画布 (NativeDiagram)

        /// </summary>

        private void SwitchToNativeDiagram_Click(object sender, RoutedEventArgs e)

        {

            SwitchToDefaultConfiguration();

        }



        /// <summary>

        /// 应用缩放变换（支持围绕指定位置缩放）

        /// </summary>

        /// <param name="oldScale">缩放前的缩放值</param>

        /// <param name="newScale">缩放后的缩放值</param>

        /// <param name="centerPosition">缩放中心相对于ScrollViewer的坐标（可选）</param>

        /// <param name="scrollViewer">可用的ScrollViewer实例（可选，如果提供则不需要重新查找）</param>

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

                // 新的滚动偏移 = 缩放中心在画布坐标 * 新缩放比例 - 缩放中心在ScrollViewer位置

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

        /// 更新缩放百分比显示

        /// </summary>

        private void UpdateZoomDisplay()

        {

            // 使用 Dispatcher 延迟执行，确保 TabItem 内容已完全加载

            Dispatcher.BeginInvoke(new Action(() =>

            {



                int percentage = 100;



                try

                {

                    var canvasType = GetCurrentCanvasType();

                    if (_viewModel.WorkflowTabViewModel.SelectedTab != null)

                    {

                        percentage = (int)(_viewModel.WorkflowTabViewModel.SelectedTab.CurrentScale * 100);

                    }

                    else

                    {

                    }

                }

                catch (Exception ex)

                {

                }



                // 直接使用命名的ZoomTextBlock控件

                if (ZoomTextBlock != null)

                {

                    ZoomTextBlock.Text = $"缩放: {percentage}%";

                }

                else

                {

                }



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

        /// 获取当前活动的Canvas

        /// </summary>

        private Canvas GetCurrentCanvas()

        {

            try

            {

                if (_viewModel.WorkflowTabViewModel.SelectedTab == null)

                    return null!;



                var container = WorkflowTabControl.ItemContainerGenerator.ContainerFromItem(_viewModel.WorkflowTabViewModel.SelectedTab);

                if (container is TabItem tabItem)

                {

                    // 在整个 TabItem 中查找所有 Canvas

                    var allCanvases = FindAllVisualChildren<Canvas>(tabItem);



                    foreach (var canvas in allCanvases)

                    {

                        // 找到名为 WorkflowCanvas 的 Canvas

                        if (canvas.Name == "WorkflowCanvas")

                        {

                            return canvas;

                        }

                    }



                    // 如果没有找到名为 WorkflowCanvas 的,返回第一个 Canvas

                    if (allCanvases.Count > 0)

                    {

                        return allCanvases[0];

                    }

                }

            }

            catch (Exception ex)

            {

                // 静默处理异常

            }

            return null!;

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

            catch (Exception ex)

            {

                // 注意：此处无法访问 _viewModel，因为此方法是辅助方法

                // 如需记录错误日志，可以传入 ViewModel 参数或使用其他日志机制

            }

            return null!;

        }



        /// <summary>

        /// 获取画布中心在Canvas上的坐标

        /// </summary>

        private Point GetCanvasCenterPosition(ScrollViewer scrollViewer)

        {

            if (scrollViewer == null)

                return new Point(0, 0);



            // 返回视口中心相对于ScrollViewer的坐标（即鼠标在视口中心的位置）

            return new Point(

                scrollViewer.ViewportWidth / 2,

                scrollViewer.ViewportHeight / 2

            );

        }



        #endregion

        #region 全屏功能

        /// <summary>
        /// 切换图像区域全屏（调用 ImageControl 的全屏功能）
        /// </summary>
        private void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (ImageDisplayContent != null)
            {
ImageDisplayContent.ToggleFullscreen();
            }
            else
            {
}
        }

        #endregion

        #region 辅助方法



        /// <summary>

        /// 查找父级Canvas

        /// </summary>

        private Canvas FindParentCanvas(DependencyObject element)

        {

            if (element == null)

                return null!;



            var parent = VisualTreeHelper.GetParent(element);

            while (parent != null)

            {

                if (parent is System.Windows.Controls.Canvas canvas)

                    return canvas;

                parent = VisualTreeHelper.GetParent(parent);

            }

            return null!;

        }



        #endregion



        #region SplitterWithToggle 事件处理



        // 注释：以下代码已废弃，工具箱双模切换功能已完全由ToolboxControl内部按钮实现（2026-02-10）

        /*

        private double _originalToolboxWidth = 260;

        */

        private double _rightPanelWidth = 500;

        private double _previousSplitterPosition;



        /// <summary>

        /// 图像-属性分隔条开始拖动

        /// </summary>

        private void ImagePropertySplitter_DragStarted(object sender, DragStartedEventArgs e)

        {

            // 记录拖动开始前的状态

            _previousSplitterPosition = RightPanelGrid.RowDefinitions[0].ActualHeight;
}



        /// <summary>

        /// 图像-属性分隔条拖动中 - 实时更新高度

        /// </summary>

        private void ImagePropertySplitter_DragDelta(object sender, DragDeltaEventArgs e)

        {

            // 获取当前图像显示区域的高度

            double currentImageHeight = RightPanelGrid.RowDefinitions[0].ActualHeight;



            // 实时记录拖动过程中的位置变化（用于调试）
// 注意：由于ShowsPreview="False"，GridSplitter会自动调整相邻Row的高度

            // 不需要手动更新Height，只需要记录状态即可

        }



        /// <summary>

        /// 图像-属性分隔条拖动完成

        /// </summary>

        private void ImagePropertySplitter_DragCompleted(object sender, DragCompletedEventArgs e)

        {

            // 保存新的分隔条位置到ViewModel

            double newPosition = RightPanelGrid.RowDefinitions[0].ActualHeight;
if (DataContext is MainWindowViewModel viewModel)

            {

                viewModel.SaveSplitterPosition(newPosition);

            }

        }



        /// <summary>

        /// 工具箱分割器的折叠/展开事件（已废弃 - 切换功能已由ToolboxControl内部按钮实现）

        /// </summary>

        /*

        private void ToolboxSplitter_ToggleClick(object? sender, EventArgs e)

        {

            var viewModel = DataContext as MainWindowViewModel;

            if (viewModel?.Toolbox == null)

                return;



            if (viewModel.IsToolboxCollapsed)

            {

                // 展开：切换到展开模式（260px）

                ToolboxColumn.Width = new GridLength(260);

                ToolboxContent.Visibility = Visibility.Visible;

                viewModel.IsToolboxCollapsed = false;

                viewModel.Toolbox.IsCompactMode = false;

            }

            else

            {

                // 折叠：切换到紧凑模式（60px）

                ToolboxColumn.Width = new GridLength(60);

                ToolboxContent.Visibility = Visibility.Visible;

                viewModel.IsToolboxCollapsed = true;

                viewModel.Toolbox.IsCompactMode = true;

            }

            UpdateToolboxSplitterArrow();

        }

        */



        /// <summary>

        /// 更新工具箱分割器箭头方向（已废弃 - 工具箱分隔器已删除）

        /// </summary>

        /*

        private void UpdateToolboxSplitterArrow()

        {

            var newDirection = _viewModel.IsToolboxCollapsed

                ? ToggleDirectionType.Right

                : ToggleDirectionType.Left;

            ToolboxSplitter.ToggleDirection = newDirection;

        }

        */



        /// <summary>

        /// 右侧面板分割器的折叠/展开事件

        /// </summary>

        private void RightPanelSplitter_ToggleClick(object? sender, EventArgs e)

        {

            if (_viewModel.IsRightPanelCollapsed)

            {

                // 展开整个右侧面板

                RightPanelColumn.Width = new GridLength(_rightPanelWidth);

                _viewModel.IsRightPanelCollapsed = false;

            }

            else

            {

                // 折叠整个右侧面板

                _rightPanelWidth = RightPanelColumn.ActualWidth;

                RightPanelColumn.Width = new GridLength(40);

                _viewModel.IsRightPanelCollapsed = true;

            }

            UpdateRightPanelSplitterArrow();

        }



        /// <summary>

        /// 更新右侧面板分割器箭头方向

        /// </summary>

        private void UpdateRightPanelSplitterArrow()

        {

            var newDirection = _viewModel.IsRightPanelCollapsed

                ? ToggleDirectionType.Left

                : ToggleDirectionType.Right;

            RightPanelSplitter.ToggleDirection = newDirection;

        }



        #endregion



        #region SDK ImageControl 事件处理

        /// <summary>
        /// DataGrid 结果行展开/折叠点击事件
        /// </summary>
        private void ResultExpandIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is FlatResultRow row && row.IsGroup)
            {
                row.IsExpanded = !row.IsExpanded;
                e.Handled = true;
            }
        }

        /// <summary>
        /// 图像加载完成事件
        /// </summary>
        private void OnImageLoaded(object sender, ImageLoadedEventArgs e)
        {
            // 图像加载完成，可触发后续操作
            if (_viewModel != null && e.Image != null)
            {
                _viewModel.StatusText = $"图像加载完成: {e.Width}x{e.Height}";
            }
        }

        /// <summary>
        /// 缩放变化事件
        /// </summary>
        private void OnZoomChanged(object sender, ZoomChangedEventArgs e)
        {
            // 更新ViewModel中的缩放值
            if (_viewModel != null)
            {
                _viewModel.ImageScale = e.Zoom;
            }
            
            // ★ 同步视口缩放到结果渲染层（幂律公式：线宽 / 缩放 = 视觉恒定）
            if (ImageDisplayContent?.ResultRenderLayerControl != null)
            {
                ImageDisplayContent.ResultRenderLayerControl.SetViewportScale(e.Zoom);
            }
        }

        #endregion


        #region 画布引擎管理器支持



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



            // 获取当前画布类型

            var canvasType = GetCurrentCanvasType();



            // 根据画布类型调用对应的控件方法

            switch (canvasType)

            {

                case CanvasType.WorkflowCanvas:

                    // 查找当前显示的WorkflowCanvasControl

                    var workflowCanvas = FindVisualChild<WorkflowCanvasControl>(this);

                    if (workflowCanvas != null)

                    {

                        workflowCanvas.SetPathCalculator(pathCalculatorType);

                    }

                    else

                    {

                    }

                    break;


            }



            // 同时调用CanvasEngineManager，保持兼容性

            CanvasEngineManager.SetPathCalculator(pathCalculatorType);

        }



        /// <summary>

        /// 获取当前画布类型

        /// </summary>

        public CanvasType GetCurrentCanvasType()

        {

            return _viewModel?.WorkflowTabViewModel?.SelectedTab?.CanvasType ?? CanvasType.WorkflowCanvas;

        }



        /// <summary>

        /// 获取当前画布引擎

        /// </summary>

        public ICanvasEngine? GetCurrentCanvasEngine()

        {

            return CanvasEngineManager.GetCurrentEngine();

        }



        #endregion



        #region 命令包装器类



        private class PauseCommandWrapper : ICommand

        {

            private readonly ICommand? _command;



            public PauseCommandWrapper(ICommand? command)

            {

                _command = command;

            }



            public event EventHandler? CanExecuteChanged;



            public bool CanExecute(object? parameter)

            {

                return _command?.CanExecute(parameter) ?? false;

            }



            public void Execute(object? parameter)

            {

                _command?.Execute(parameter);

            }

        }



        private class UndoCommandWrapper : ICommand

        {

            private readonly ICommand? _command;



            public UndoCommandWrapper(ICommand? command)

            {

                _command = command;

            }



            public event EventHandler? CanExecuteChanged;



            public bool CanExecute(object? parameter)

            {

                return _command?.CanExecute(parameter) ?? false;

            }



            public void Execute(object? parameter)

            {

                _command?.Execute(parameter);

            }

        }



        private class RedoCommandWrapper : ICommand

        {

            private readonly ICommand? _command;



            public RedoCommandWrapper(ICommand? command)

            {

                _command = command;

            }



            public event EventHandler? CanExecuteChanged;



            public bool CanExecute(object? parameter)

            {

                return _command?.CanExecute(parameter) ?? false;

            }



            public void Execute(object? parameter)

            {

                _command?.Execute(parameter);

            }

        }



        #endregion



    }

}

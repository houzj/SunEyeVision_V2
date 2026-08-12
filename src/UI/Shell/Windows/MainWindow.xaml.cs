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
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Shell.Controls.Panels;
using SunEyeVision.UI.Shell.Controls.Common;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Core;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using AIStudio.Wpf.DiagramDesigner;
using AIStudio.Wpf.DiagramDesigner.ViewModels;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Converters.Path;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Shell.Windows
{
    /// <summary>
    /// MainWindow - 太阳眼视觉风格的主界面窗口
    /// 实现完整的机器视觉平台主界面，包含工作流画布、工具箱、属性面板等
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            // ★ 中间工作区注入 ViewModel 并订阅缩放事件（替代直接操作 ZoomTextBlock 的跨区域耦合）
            WorkspaceHost.Initialize(_viewModel);
            WorkspaceHost.ZoomScaleChanged += p => TopBarHost.SetZoomPercentage(p);
            // ★ 图像结果区注入 ViewModel 并订阅图像预览事件
            ImageResultHost.Initialize(_viewModel);
            // ★ 顶部栏请求全屏 → 图像结果区全屏（跨区域事件转发）
            TopBarHost.FullscreenRequested += (s, e) => ImageResultHost.ToggleFullscreen();
            // ★ 设置获取主窗口 ImageControl 的委托（经图像结果区获取）
            _viewModel.GetMainImageControl = () => ImageResultHost.GetImageDisplayControl();
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
                var solutionManager = SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager;
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
                var solutionManager = SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager;
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
            // 取消订阅图像预览事件（经图像结果区退订），防止内存泄漏
            ImageResultHost.Shutdown();
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
            var solutionManager = SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager;
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
                // 同步基准取引擎中该标签页根工作流（tab.Id）的节点/连接。
                // 不能拿 tab.WorkflowNodes / tab.WorkflowConnections，因为它们在下钻子程序时
                // 会被 NavigateToWorkflowCanvas 整体替换为子程序集合，导致主程序节点被误清空。
                var rootWorkflow = _viewModel.WorkflowEngine.GetWorkflow(tab.Id);
                if (rootWorkflow == null)
                    throw new InvalidOperationException($"SyncWorkflowsToSolution: 引擎中未找到根工作流 (ID: {tab.Id})");
                // 同步节点
                foreach (var node in rootWorkflow.Nodes)
                {
                    if (!workflow.Nodes.Contains(node))
                        workflow.Nodes.Add(node);
                }
                var nodesToRemove = workflow.Nodes
                    .Where(n => !rootWorkflow.Nodes.Contains(n))
                    .ToList();
                foreach (var node in nodesToRemove)
                    workflow.Nodes.Remove(node);
                // 同步连接（共享引用，直接加载）
                foreach (var conn in rootWorkflow.Connections)
                {
                    if (!workflow.Connections.Any(c => c.Id == conn.Id))
                    {
                        workflow.Connections.Add(conn);
                    }
                }
                var connsToRemove = workflow.Connections
                    .Where(c => !rootWorkflow.Connections.Any(tc => tc.Id == c.Id))
                    .ToList();
                foreach (var conn in connsToRemove)
                    workflow.Connections.Remove(conn);
            }
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // ★ 设置 ResultRenderLayer 引用到 NodeResultManager（经图像结果区获取 ImageDisplayContent）
                var imageDisplay = ImageResultHost.GetImageDisplayControl();
                if (imageDisplay != null)
                {
                    var resultRenderLayer = imageDisplay.ResultRenderLayerControl;
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
                // 缩放显示由中间工作区 ZoomScaleChanged 事件驱动，无需在此初始化
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
        private Services.Workflow.NodeResultManager? GetNodeResultManager(SunEyeVision.UI.Shell.Windows.MainWindowViewModel viewModel)
        {
            // 通过反射获取私有字段
            var field = typeof(SunEyeVision.UI.Shell.Windows.MainWindowViewModel).GetField("_nodeResultManager",
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
            // 缩放显示由中间工作区 ZoomScaleChanged 事件驱动
        }
        #endregion
        #region TabControl 多流程管理事件处理
        public WorkflowCanvasControl? GetCurrentWorkflowCanvas()
        {
            return WorkspaceHost.GetCurrentWorkflowCanvas();
        }
        internal void OpenSubroutineNodeParameterDialog(SunEyeVision.UI.Shared.WorkflowNode node)
        {
            WorkspaceHost.OpenSubroutineNodeParameterDialog(node);
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
        #region 画布引擎管理器支持
        /// <summary>
        /// 通过代码切换画布类型（用于单元测试和特殊场景）
        /// </summary>
        public void SwitchCanvasType(CanvasType canvasType)
        {
            WorkspaceHost.SwitchCanvasType(canvasType);
        }
        /// <summary>
        /// 设置路径计算器类型（转发到中间工作区）
        /// </summary>
        public void SetPathCalculator(string pathCalculatorType)
        {
            WorkspaceHost.SetPathCalculator(pathCalculatorType);
            // 同时调用CanvasEngineManager，保持兼容性
            CanvasEngineManager.SetPathCalculator(pathCalculatorType);
        }
        /// <summary>
        /// 获取当前画布类型
        /// </summary>
        public CanvasType GetCurrentCanvasType()
        {
            return WorkspaceHost.GetCurrentCanvasType();
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

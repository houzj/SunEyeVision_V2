using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Diagnostics;
using SunEyeVision.UI.Features.Dialogs;
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
        /// <summary>
        /// 自定义无边框窗口 Chrome：CaptionHeight=56（标题栏=ShellTopBarControl，可拖动），
        /// 菜单/按钮经 WindowChrome.IsHitTestVisibleInChrome=True 在 Caption 区域可点击，边缘可缩放
        /// </summary>
        public static System.Windows.Shell.WindowChrome MainWindowChrome { get; } = new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 56,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        };

        private readonly MainWindowViewModel _viewModel;
        private readonly Stopwatch _solutionLoadingStopwatch = new();

        /// <summary>Win32 消息：查询窗口最小/最大尺寸限制</summary>
        private const int WM_GETMINMAXINFO = 0x0024;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfo
        {
            public int CbSize;
            public Win32Rect Monitor;
            public Win32Rect Work;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            public Win32Point PtReserved;
            public Win32Point PtMaxSize;
            public Win32Point PtMaxPosition;
            public Win32Point PtMinTrackSize;
            public Win32Point PtMaxTrackSize;
        }

        /// <summary>窗口句柄创建后挂接 Win32 消息钩子</summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        }

        /// <summary>按窗口所在显示器的工作区限制最大化，兼容副屏任务栏及混合 DPI。</summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_GETMINMAXINFO)
                return IntPtr.Zero;

            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MonitorInfo { CbSize = Marshal.SizeOf<MonitorInfo>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo))
                return IntPtr.Zero;

            var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            // WM_GETMINMAXINFO 使用原生像素，偏移量相对于当前显示器而非虚拟桌面。
            info.PtMaxSize.X = monitorInfo.Work.Right - monitorInfo.Work.Left;
            info.PtMaxSize.Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top;
            info.PtMaxPosition.X = monitorInfo.Work.Left - monitorInfo.Monitor.Left;
            info.PtMaxPosition.Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top;

            Marshal.StructureToPtr(info, lParam, true);
            handled = true;
            return IntPtr.Zero;
        }

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
            InputBindings.Add(new KeyBinding(_viewModel.NewSolutionCommand, Key.N, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(_viewModel.OpenSolutionCommand, Key.O, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(_viewModel.SaveCurrentSolutionCommand, Key.S, ModifierKeys.Control));
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
        private bool _isClosingPromptInProgress;
        private bool _closeApproved;

        /// <summary>
        /// 窗口关闭前事件 - 提示保存解决方案。
        /// 注意：不能在此同步调用 ShowDialog()——主窗口关闭流程中 WPF 模态窗口不会显示（ShowDialog 秒退）。
        /// 正确做法：先用 e.Cancel 拦截关闭，再延迟到关闭流程结束后异步弹窗，根据结果决定是否真正关闭。
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_closeApproved)
            {
                // 确认弹窗已弹出，用户再次触发关闭 -> 放行（由弹窗结果决定）
                e.Cancel = false;
                return;
            }

            // 拦截本次关闭，异步弹确认框后再决定（不能同步 ShowDialog——主窗口关闭流程中模态窗口不显示）
            e.Cancel = true;
            if (_isClosingPromptInProgress) return;
            _isClosingPromptInProgress = true;

            Dispatcher.BeginInvoke(new Action(() => PromptAndMaybeClose()));
        }

        /// <summary>
        /// 异步弹出关闭确认框，根据结果决定是否真正关闭窗口。
        /// </summary>
        private async void PromptAndMaybeClose()
        {
            try
            {
                  if (await _viewModel.PrepareSolutionTransitionAsync())
                  {
                      SaveLayoutConfig();
                      SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.SaveUserSettings();
                      _closeApproved = true;
                      (Application.Current as SunEyeVision.UI.App)?.MarkInstanceClosing();
                      Close();
                }
                else _isClosingPromptInProgress = false;
            }
            catch (Exception ex)
            {
                _isClosingPromptInProgress = false;
                _viewModel.EndSolutionTransition();
                ThemeMessageDialog.Show(ex.Message, "退出未完成", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            }
        }
        /// <summary>
        /// 关闭前创建新解决方案
        /// </summary>
        /// <returns>是否成功创建并保存解决方案</returns>
        private bool CreateNewSolutionBeforeClosing()
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
                    // 方案必须复用引擎中的工作流定义，不能重新创建第二份节点集合。
                    // 新建方案时也必须保持 UI、Engine、Solution 的稳定 ID 和对象引用一致。
                    var workflow = _viewModel.WorkflowEngine.GetWorkflowById(tab.Id)
                        ?? throw new InvalidOperationException(
                            $"创建方案时未找到工作流定义: {tab.Name} ({tab.Id})");
                    _viewModel.WorkflowDefinitionService.AttachToSolution(solution, workflow);
                    _viewModel?.AddLog(LogLevel.Info, $"已同步工作流: {tab.Name}, 节点数={tab.WorkflowNodes.Count}, 连接数={tab.WorkflowConnections.Count}", LogSource.UIOperation);
                }
                // 直接保存Solution对象（不依赖CurrentSolution）
                bool saveSuccess = solutionManager.SaveSolutionDirect(solution, filePath, metadata);
                if (!saveSuccess)
                {
                    ThemeMessageDialog.Show("保存解决方案失败", "错误",
                        MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
                    return false;
                }
                _viewModel?.AddLog(LogLevel.Success, $"已创建并保存新解决方案: {metadata.Name} -> {filePath}", LogSource.UIOperation);
                return true;
            }
            catch (Exception ex)
            {
                ThemeMessageDialog.Show($"创建解决方案失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
                return false;
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            // 主窗口关闭时主动关闭仍存活的子窗口，避免预览窗口或对话框继续持有
            // Dispatcher、相机采集会话和 Debug 输出程序集，导致进程残留并锁定 DLL。
            var childWindows = Application.Current?.Windows
                .OfType<Window>()
                .Where(window => !ReferenceEquals(window, this))
                .ToArray();
            if (childWindows != null)
            {
                foreach (var childWindow in childWindows)
                {
                    try
                    {
                        childWindow.Close();
                    }
                    catch (Exception ex)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"关闭子窗口失败: {childWindow.GetType().Name}, {ex.Message}", "MainWindow");
                    }
                }
            }

            // 取消订阅图像预览事件（经图像结果区退订），防止内存泄漏
            ImageResultHost.Shutdown();
            _viewModel?.StopWorkflowCommand.Execute(null);
            base.OnClosed(e);

            // 防止存在无 Owner 的窗口或其他 WPF Dispatcher 对象使进程继续驻留。
            Application.Current?.Shutdown();
        }

        public void ShowSolutionLoadingOverlay(string stage, string detail)
        {
            _solutionLoadingStopwatch.Restart();
            UpdateSolutionLoadingOverlay(stage, detail);
            SolutionLoadingOverlay.Visibility = Visibility.Visible;
        }

        public void UpdateSolutionLoadingOverlay(string stage, string detail)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateSolutionLoadingOverlay(stage, detail));
                return;
            }

            SolutionLoadingStageText.Text = stage;
            SolutionLoadingDetailText.Text = detail;
            SolutionLoadingElapsedText.Text = $"已用时 {_solutionLoadingStopwatch.Elapsed.TotalSeconds:0.0} 秒";
        }

        public void HideSolutionLoadingOverlay()
        {
            SolutionLoadingOverlay.Visibility = Visibility.Collapsed;
            _solutionLoadingStopwatch.Stop();
        }

        /// <summary>
        /// 保存布局配置（右侧面板折叠状态 + 列宽），重启后恢复
        /// </summary>
        private void SaveLayoutConfig()
        {
            var config = LayoutConfig.Load();
            config.IsRightPanelCollapsed = RightPanelSplitter.IsCollapsed;
            config.RightColumnWidth = RightPanelColumn.ActualWidth > 0
                ? RightPanelColumn.ActualWidth
                : config.RightColumnWidth;
            if (!config.Save()) throw new System.IO.IOException("窗口布局配置保存失败，原配置保留。");
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
                ThemeMessageDialog.Show(
                    $"加载工具插件时出错: {ex.Message}",
                    "加载失败",
                    MessageBoxButton.OK,
                    ThemeMessageDialog.MessageIcon.Warning);
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
            // 恢复布局配置（右侧面板折叠状态 + 列宽）
            RestoreLayoutConfig();
        }

        /// <summary>
        /// 恢复布局配置（右侧面板折叠状态 + 列宽）
        /// </summary>
        private void RestoreLayoutConfig()
        {
            var config = LayoutConfig.Load();
            if (config.IsRightPanelCollapsed)
            {
                RightPanelSplitter.IsCollapsed = true;
                RightPanelSplitter.ToggleCollapsed();
            }
            else if (config.RightColumnWidth > 0)
            {
                RightPanelColumn.Width = new GridLength(config.RightColumnWidth);
            }
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
        #region 右侧面板分隔条事件处理
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

        private void WorkspaceHost_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}

using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Diagnostics;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Shell.Controls.Common;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Core;
using AIStudio.Wpf.DiagramDesigner;
using AIStudio.Wpf.DiagramDesigner.ViewModels;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Converters.Path;
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

        public MainWindow(SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DesktopToolAdmission admission)
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel(admission);
            _viewModel.SolutionDialogOwner = this;
            DataContext = _viewModel;
            // ★ 中间工作区注入 ViewModel 并订阅缩放事件（替代直接操作 ZoomTextBlock 的跨区域耦合）
            WorkspaceHost.Initialize(_viewModel);
            WorkspaceHost.ZoomScaleChanged += p => TopBarHost.SetZoomPercentage(p);
            // ★ 图像结果区注入 ViewModel 并订阅图像预览事件
            ImageResultHost.Initialize(_viewModel);
            // ★ 顶部栏请求全屏 → 图像结果区全屏（跨区域事件转发）
            TopBarHost.FullscreenRequested += (s, e) => ImageResultHost.ToggleFullscreen();
            // ★ 设置获取主窗口 ImageControl 的委托（经图像结果区获取）
            // 初始化画布引擎管理器 - 设置默认引擎
            CanvasEngineManager.SetDataContext(_viewModel.WorkflowTabViewModel?.SelectedTab);
            RegisterHotkeys();
            // 后台切换到NativeDiagramControl（使用原生AIStudio.Wpf.DiagramDesigner库）
            SwitchToDefaultConfiguration();
        }
        /// <summary>
        /// 切换到默认配置：WorkflowCanvasControl画布 + VmPathCalculator路径计算器
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
            InputBindings.Add(new KeyBinding(new CanvasEditCommandWrapper(_viewModel.UndoCommand), Key.Z, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new CanvasEditCommandWrapper(_viewModel.RedoCommand), Key.Y, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new CanvasEditCommandWrapper(_viewModel.DeleteSelectedNodesCommand, deferToWire: true), Key.Delete, ModifierKeys.None));
        }
        #region 窗口事件
        /// <summary>
        /// 窗口关闭前事件 - 提示保存解决方案
        /// </summary>
        private bool _isClosingPromptInProgress;
        private bool _closeApproved;
        private bool _shutdownPreparationStarted;
        private bool _consumerReleaseStarted;

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
            if (_shutdownPreparationStarted)
            {
                // A child that refused to close has not released any consumers; retry
                // only that reversible phase after the owner has resolved the failure.
                if (!_consumerReleaseStarted && !_isClosingPromptInProgress)
                {
                    _isClosingPromptInProgress=true;
                    Dispatcher.BeginInvoke(new Action(RetryPreparedClose));
                }
                return;
            }
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
                  if (await _viewModel.PrepareSolutionTransitionAsync(owner: this))
                  {
                      SaveLayoutConfig();
                      (System.Windows.Application.Current as App)?.SaveShellPreferences();
                      (System.Windows.Application.Current as SunEyeVision.UI.App)?.MarkInstanceClosing();
                      _shutdownPreparationStarted = true;
                      if (Content is UIElement content) content.IsEnabled = false;
                      Title = "正在关闭：等待资源释放";
                      await CompletePreparedCloseAsync();
                }
                else _isClosingPromptInProgress = false;
            }
            catch (Exception ex)
            {
                _isClosingPromptInProgress = false;
                if (!_shutdownPreparationStarted) _viewModel.EndSolutionTransition();
                var detail=_shutdownPreparationStarted ? "退出准备已开始，工作区保持关闭状态，请检查资源释放错误。\n"+ex.Message : ex.Message;
                ThemeMessageDialog.Show(detail, "退出未完成", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error, this);
            }
        }
        private async void RetryPreparedClose()
        {
            try { await CompletePreparedCloseAsync(); }
            catch(Exception ex)
            {
                _isClosingPromptInProgress=false;
                ThemeMessageDialog.Show("退出准备已开始，工作区保持关闭状态，请检查资源释放错误。\n"+ex.Message,
                    "退出未完成",MessageBoxButton.OK,ThemeMessageDialog.MessageIcon.Error,this);
            }
        }
        private async Task CompletePreparedCloseAsync()
        {
            PreparePluginConsumersForShutdown();
            if(System.Windows.Application.Current is SunEyeVision.UI.App app)
            {
                var drain=app.DrainPluginDirectoryAsync();
                if(await Task.WhenAny(drain,Task.Delay(TimeSpan.FromSeconds(10)))!=drain)
                    ThemeMessageDialog.Show("仍有结果或插件资源正在使用。窗口将保持打开并继续等待，资源释放后自动关闭。", "等待资源释放", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Info, this);
                await drain;
            }
            _closeApproved=true;
            Close();
        }
        private void PreparePluginConsumersForShutdown()
        {
            var failures=new List<Exception>();
            // 主窗口关闭时主动关闭仍存活的子窗口，避免预览窗口或对话框继续持有
            // Dispatcher、相机采集会话和 Debug 输出程序集，导致进程残留并锁定 DLL。
            var childWindows = System.Windows.Application.Current?.Windows
                .OfType<Window>()
                .Where(window => !ReferenceEquals(window, this))
                .ToArray();
            if (childWindows != null)
            {
                foreach (var childWindow in childWindows)
                {
                    Exception? dispatcherFailure=null;
                    System.Windows.Threading.DispatcherUnhandledExceptionEventHandler capture=(_,args)=>dispatcherFailure??=args.Exception;
                    Dispatcher.UnhandledException+=capture;
                    try
                    {
                        childWindow.Close();
                        if(dispatcherFailure is not null)failures.Add(dispatcherFailure);
                        if(System.Windows.Application.Current?.Windows.OfType<Window>().Contains(childWindow)==true)
                            failures.Add(new InvalidOperationException("子窗口未关闭："+childWindow.GetType().Name));
                    }
                    catch (Exception ex)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"关闭子窗口失败: {childWindow.GetType().Name}, {ex.Message}", "MainWindow");
                        failures.Add(ex);
                    }
                    finally { Dispatcher.UnhandledException-=capture; }
                }
            }
            // A still-open child may retain result readers or an editor. Do not
            // release its shared consumers or start retirement after a failed close.
            if(failures.Count>0)throw new AggregateException("子窗口未能全部关闭，资源保持持有。",failures);
            _consumerReleaseStarted=true;
            try { _viewModel.CloseExecutionSnapshotDetails(); } catch(Exception ex) { failures.Add(ex); }
            _viewModel.SolutionDialogOwner = null;

            // 取消订阅图像预览事件（经图像结果区退订），防止内存泄漏
            try { ImageResultHost.Shutdown(); } catch(Exception ex) { failures.Add(ex); }
            try { _viewModel.ReleasePluginConsumersForShutdown(); } catch(Exception ex) { failures.Add(ex); }
            try { _viewModel.ReleaseDocumentHost(); } catch(Exception ex) { failures.Add(ex); }
            if(failures.Count>0)throw new AggregateException("插件消费者未能全部释放，窗口保持打开。",failures);
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            System.Windows.Application.Current?.Shutdown();
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
            SunEyeVision.UI.Shell.Controls.Shell.MainWorkspaceLayout.Capture(config, RightPanelColumn, RightPanelSplitter);
            if (!config.Save()) throw new System.IO.IOException("窗口布局配置保存失败，原配置保留。");
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 工具插件现在通过ToolboxViewModel自动加载
                var toolCount = _viewModel.Toolbox.AvailableToolCount;
                _viewModel.StatusText = $"已加载 {toolCount} 个工具插件";
                // 初始化智能路径转换器的节点集合（使用当前选中的 Tab 的节点集合）
                if (_viewModel.WorkflowTabViewModel?.SelectedTab != null)
                {
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
            SunEyeVision.UI.Shell.Controls.Shell.MainWorkspaceLayout.Restore(config, RightPanelColumn, RightPanelSplitter);
        }
        #endregion
        #region TabControl 多流程管理事件处理
        public WorkflowCanvasControl? GetCurrentWorkflowCanvas()
        {
            return WorkspaceHost.GetCurrentWorkflowCanvas();
        }
        internal void OpenSubroutineNodeParameterDialog(CanvasNodeProjection node)
        {
            WorkspaceHost.OpenSubroutineNodeParameterDialog(node);
        }
        #endregion
        #region 工具箱分隔条事件（已废弃）
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
        private sealed class CanvasEditCommandWrapper : ICommand
        {
            private readonly ICommand? _command;
            private readonly bool _deferToWire;
            public CanvasEditCommandWrapper(ICommand? command, bool deferToWire=false) { _command=command;_deferToWire=deferToWire; }
            public event EventHandler? CanExecuteChanged
            {
                add { if (_command != null) _command.CanExecuteChanged += value; }
                remove { if (_command != null) _command.CanExecuteChanged -= value; }
            }
            public bool CanExecute(object? parameter)
            {
                var focus = Keyboard.FocusedElement as DependencyObject;
                while (focus != null)
                {
                    if (_deferToWire && focus is System.Windows.Shapes.Path { DataContext: SunEyeVision.UI.Features.WorkflowUI.Canvas.CanvasConnectionProjection }) return false;
                    if (focus is System.Windows.Controls.Primitives.TextBoxBase || focus is ComboBox || focus is PasswordBox) return false;
                    if (focus is SunEyeVision.UI.Features.WorkflowUI.Canvas.WorkflowCanvasControl canvas)
                    {
                        var vm = Window.GetWindow(canvas)?.DataContext as MainWindowViewModel;
                        return canvas.IsEnabled && vm?.CanEditWorkflowCanvas != false && _command?.CanExecute(parameter) == true;
                    }
                    focus = focus is Visual || focus is System.Windows.Media.Media3D.Visual3D
                        ? VisualTreeHelper.GetParent(focus) : LogicalTreeHelper.GetParent(focus);
                }
                return false;
            }
            public void Execute(object? parameter) { if (CanExecute(parameter)) _command?.Execute(parameter); }
        }
        #endregion

        private void WorkspaceHost_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}

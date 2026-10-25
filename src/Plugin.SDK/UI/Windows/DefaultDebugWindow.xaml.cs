using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Commands;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Events;

namespace SunEyeVision.Plugin.SDK.UI.Windows
{
    /// <summary>
    /// 普通窗口壳 - 包装工具调试控件为标准 Windows 窗口
    /// </summary>
    /// <remarks>
    /// 架构说明：
    /// - 轻量级窗口壳，提供窗口容器功能
    /// - 接受任何 UserControl 作为内容
    /// - 自动添加底部按钮栏（连续运行、运行、确定）
    /// - 使用路由命令和路由事件实现声明式编程
    /// 
    /// 使用场景：
    /// - 大部分标准工具（阈值、滤波、定位等）
    /// - 需要标准窗口样式的工具
    /// 
    /// 不适用场景：
    /// - 需要无边框窗口的工具（使用 CustomDebugWindow）
    /// 
    /// 声明式编程支持：
    /// - 使用路由命令（ToolCommands）绑定按钮
    /// - 使用路由事件（ToolExecutionCompleted）监听执行完成
    /// - 工具控件通过继承 ToolDebugControlBase 实现标准接口
    /// </remarks>
    public partial class DefaultDebugWindow : Window, IToolControlInitializer
    {
        public static readonly DependencyProperty ToolIconKeyProperty = DependencyProperty.Register(nameof(ToolIconKey), typeof(string), typeof(DefaultDebugWindow), new PropertyMetadata("generic"));
        public string ToolIconKey { get => (string)GetValue(ToolIconKeyProperty); set => SetValue(ToolIconKeyProperty, value); }
        #region 事件

        /// <summary>
        /// 工具执行完成事件（CLR 事件，供外部订阅，如 MainWindowViewModel）
        /// </summary>
        public event ToolExecutionCompletedEventHandler? ToolExecutionCompleted;

        #endregion

        #region 字段

        private readonly UserControl _control;
        private readonly ToolDebugControlBase? _toolControl;
        public ToolDebugControlBase? EditorControl => _toolControl;
        
        // 状态
        private bool _isContinuousRunning;
        private bool _isExecuting;

        /// <summary>
        /// 自定义无边框窗口 Chrome：CaptionHeight=32（标题栏高度，可拖动），边缘可缩放。
        /// 用 WindowChrome 而非 AllowsTransparency，保证 Owner 层叠正常（调试窗口不被主窗口遮挡——
        /// AllowsTransparency=True 的分层窗口无法保证 Owner 子窗口始终在 Owner 之上，是 WPF 已知限制）。
        /// </summary>
        public static System.Windows.Shell.WindowChrome DebugWindowChrome { get; } = new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 32,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        };

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建普通窗口壳
        /// </summary>
        /// <param name="control">工具调试控件实例</param>
        public DefaultDebugWindow(UserControl control)
        {
            InitializeComponent();
            
            _control = control;
            _toolControl = control as ToolDebugControlBase;

            // 设置内容
            ContentHost.Content = _control;

            // 自适应尺寸：使用工具控件指定的首选尺寸
            if (_toolControl != null)
            {
                Width = _toolControl.PreferredDebugWindowSize.Width;
                Height = _toolControl.PreferredDebugWindowSize.Height;
                MaximizeButton.Visibility = _toolControl.ShowMaximizeButton
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                // 非单工具执行控件（如子程序调试窗口）："执行/连续执行"路由命令无人接收，
                // 属于无效按钮，一律隐藏，仅保留"确定"。脚本工具等继承 ToolDebugControlBase 不受影响。
                RunButton.Visibility = Visibility.Collapsed;
                ContinuousRunButton.Visibility = Visibility.Collapsed;
            }

            // ★ 修复初始图标：根据窗口实际状态设置正确的按钮图标
            UpdateMaximizeButtonContent();

            // 订阅路由事件
            SubscribeRoutedEvents();

            // 窗口关闭时清理
            Closed += OnWindowClosed;

            // 窗口关闭前提交/取消所有 DataGrid 编辑事务，
            // 防止"编辑中直接关窗"导致下次绑定 DeferRefresh 抛异常（防御保底）
            Closing += OnWindowClosing;

            // 监听 Title 属性变化，同步更新标题栏显示
            var dpDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                Window.TitleProperty, typeof(Window));
            dpDescriptor.AddValueChanged(this, (s, e) =>
            {
                TitleTextBlock.Text = Title;
            });

            PluginLogger.Success("DefaultDebugWindow 构造函数完成", "DefaultDebugWindow");
        }

        #endregion

        #region 路由事件订阅

        /// <summary>
        /// 订阅路由事件
        /// </summary>
        private void SubscribeRoutedEvents()
        {
            // 订阅工具执行完成事件
            _control.AddHandler(
                ToolDebugControlBase.ToolExecutionCompletedEvent,
                new ToolExecutionCompletedEventHandler(OnToolExecutionCompleted));
        }

        /// <summary>
        /// 取消订阅路由事件
        /// </summary>
        private void UnsubscribeRoutedEvents()
        {
            _control.RemoveHandler(
                ToolDebugControlBase.ToolExecutionCompletedEvent,
                new ToolExecutionCompletedEventHandler(OnToolExecutionCompleted));
        }

        #endregion

        #region 命令处理

        /// <summary>
        /// 执行命令处理
        /// </summary>
        private void OnExecuteCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (_isExecuting) return;

            _isExecuting = true;
            SetButtonsEnabled(false);

            PluginLogger.Info("执行命令触发", "DefaultDebugWindow");

            // 显式将 Execute 命令路由到工具控件执行（按钮在 Window 底部栏，
            // 命令冒泡路径不经过 ContentHost 中的工具控件，必须显式转发）
            ToolCommands.Execute.Execute(null, _control);
        }

        /// <summary>
        /// 判断是否可执行
        /// </summary>
        private void CanExecuteCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !_isExecuting;
        }

        /// <summary>
        /// 确认命令处理
        /// </summary>
        private void OnConfirmCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (!TryConfirm(out var error))
            { PluginLogger.Warning(error, "DefaultDebugWindow"); MessageBox.Show(error, "配置未提交", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            PluginLogger.Success("参数验证通过，关闭窗口", "DefaultDebugWindow");
        }
        public bool TryConfirm(out string error)
        {
            if (!ToolEditorCommit.TryCommit(_control, out error)) return false;
            Close();
            return true;
        }

        /// <summary>
        /// 连续执行命令处理
        /// </summary>
        private void OnContinuousExecuteCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (_isContinuousRunning)
            {
                // 停止连续运行
                _isContinuousRunning = false;
                ContinuousRunButton.Content = "连续执行";
                RunButton.Visibility = Visibility.Visible;
            }
            else
            {
                // 开始连续运行
                _isContinuousRunning = true;
                ContinuousRunButton.Content = "停止运行";
                RunButton.Visibility = Visibility.Collapsed;
                
                // 触发第一次执行
                TriggerExecute();
            }
        }

        #endregion

        #region 工具事件处理

        /// <summary>
        /// 工具执行完成事件处理
        /// </summary>
        private void OnToolExecutionCompleted(object sender, ToolExecutionCompletedEventArgs e)
        {
            // ★ 向外传播执行结果（CLR 事件供 MainWindowViewModel 订阅）
            var resultInfo = e.Results?.GetType().Name ?? "null";
            var execTimeInfo = e.ExecutionTime > 0 ? $" | 耗时={e.ExecutionTime}ms" : "";
            PluginLogger.Info($"[OnToolExecutionCompleted] 路由事件到达 | 结果={resultInfo}{execTimeInfo}", "DefaultDebugWindow");

            Dispatcher.Invoke(() =>
            {
                _isExecuting = false;
                SetButtonsEnabled(true);
                if ((e.Results as SunEyeVision.Plugin.SDK.Execution.Results.ToolResults)?.ExecutionIdentity?.IsCurrent == false)
                {
                    (e.Results as IDisposable)?.Dispose();
                    return;
                }
                ToolExecutionCompleted?.Invoke(this, e);
                
                // 如果是连续运行模式，继续下一次执行
                if (_isContinuousRunning && IsLoaded)
                {
                    // 延迟500ms后再次执行
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (_isContinuousRunning && IsLoaded)
                            {
                                TriggerExecute();
                            }
                        });
                    });
                }
            });
        }

        /// <summary>
        /// 触发执行
        /// </summary>
        private void TriggerExecute()
        {
            if (_isExecuting) return;

            _isExecuting = true;
            SetButtonsEnabled(false);

            // 显式将 Execute 命令路由到工具控件执行
            ToolCommands.Execute.Execute(null, _control);
        }

        #endregion

        #region 数据初始化转发

        /// <summary>
        /// 设置当前节点（委托给内部控件）
        /// </summary>
        /// <remarks>
        /// 必须在 SetDataProvider 之前调用，因为 SetDataProvider 内部会恢复配置，
        /// 需要当前节点引用已设置。
        ///
        /// 调用链：
        /// MainWindowViewModel → DefaultDebugWindow.SetCurrentNode → ThresholdToolDebugControl.SetCurrentNode
        /// </remarks>
        public void SetCurrentNode(object node)
        {
            if (_toolControl is IToolControlInitializer initializer)
            {
                initializer.SetCurrentNode(node);
                PluginLogger.Success(
                    $"已设置当前节点到调试控件: {_control.GetType().Name}",
                    "DefaultDebugWindow");
            }
            else if (_toolControl != null)
            {
                // 兼容旧控件
                _toolControl.SetCurrentNode(node);
                PluginLogger.Success(
                    $"已设置当前节点到调试控件（旧接口）: {_control.GetType().Name}",
                    "DefaultDebugWindow");
            }
            else
            {
                PluginLogger.Warning(
                    $"控件 {_control.GetType().Name} 未实现 IToolControlInitializer 接口",
                    "DefaultDebugWindow");
            }
        }

        /// <summary>
        /// 设置节点参数（委托给内部控件）
        /// </summary>
        /// <remarks>
        /// 调用链：
        /// MainWindowViewModel → DefaultDebugWindow.SetParameters → ThresholdToolDebugControl.SetParameters
        /// </remarks>
        public void SetParameters(Execution.Parameters.ToolParameters parameters)
        {
            if (_toolControl is IToolControlInitializer initializer)
            {
                initializer.SetParameters(parameters);
                PluginLogger.Success(
                    $"已将节点参数初始化到调试控件: {_control.GetType().Name}",
                    "DefaultDebugWindow");
            }
            else
            {
                PluginLogger.Info(
                    $"控件 {_control.GetType().Name} 未实现 IToolControlInitializer，跳过参数初始化",
                    "DefaultDebugWindow");
            }
        }

        /// <summary>
        /// 设置数据提供者（委托给内部控件）
        /// </summary>
        /// <remarks>
        /// 数据提供者包含所有前驱节点的输出数据，用于填充图像源选择器。
        /// 此方法必须在 SetCurrentNode 之后调用。
        ///
        /// 调用链：
        /// MainWindowViewModel → DefaultDebugWindow.SetDataProvider → ThresholdToolDebugControl.SetDataProvider
        /// </remarks>
        public void SetDataProvider(object dataProvider)
        {
            if (_toolControl is IToolControlInitializer initializer)
            {
                initializer.SetDataProvider(dataProvider);
                PluginLogger.Success(
                    $"已将数据提供者初始化到调试控件: {_control.GetType().Name}",
                    "DefaultDebugWindow");
            }
            else if (_toolControl != null)
            {
                // 兼容旧控件
                _toolControl.SetDataProvider(dataProvider);
                PluginLogger.Success(
                    $"已将数据提供者初始化到调试控件（旧接口）: {_control.GetType().Name}",
                    "DefaultDebugWindow");
            }
            else
            {
                PluginLogger.Warning(
                    $"控件 {_control.GetType().Name} 未继承 ToolDebugControlBase 基类",
                    "DefaultDebugWindow");
            }
        }

        /// <summary>
        /// 外部结果到达通知（委托给内部控件）
        /// </summary>
        /// <remarks>
        /// 工作流引擎执行完成后调用，触发控件刷新依赖运行时的 UI 状态。
        /// </remarks>
        public void OnExternalResultArrived()
        {
            _toolControl?.OnExternalResultArrived();
        }

        /// <summary>
        /// 设置主窗口 ImageControl 引用（委托给内部控件）
        /// </summary>
        /// <remarks>
        /// 用于区域编辑器在主窗口图像上绘制 ROI。
        ///
        /// 调用链：
        /// MainWindowViewModel → DefaultDebugWindow.SetMainImageControl → ThresholdToolDebugControl.SetMainImageControl
        /// </remarks>
        public void SetMainImageControl(ImageControl? imageControl)
        {
            PluginLogger.Info(
                $"[SetMainImageControl] 方法被调用 | ImageControl={imageControl?.GetType().Name ?? "null"} | _control={_control.GetType().Name}",
                "DefaultDebugWindow");

            if (_toolControl is IToolControlInitializer initializer)
            {
                PluginLogger.Info(
                    $"[SetMainImageControl] 委托给 _toolControl | Type={_toolControl.GetType().Name}",
                    "DefaultDebugWindow");

                initializer.SetMainImageControl(imageControl);
                PluginLogger.Success(
                    $"已设置主窗口 ImageControl 到调试控件: {_control.GetType().Name}",
                    "DefaultDebugWindow");
            }
            else
            {
                // 兼容旧控件：使用反射
                PluginLogger.Warning(
                    $"[SetMainImageControl] _toolControl 不是 IToolControlInitializer 类型 | Type={_toolControl?.GetType().Name ?? "null"}",
                    "DefaultDebugWindow");

                var setMainImageControlMethod = _control.GetType().GetMethod("SetMainImageControl");
                if (setMainImageControlMethod != null)
                {
                    setMainImageControlMethod.Invoke(_control, new object?[] { imageControl });
                    PluginLogger.Success(
                        $"已设置主窗口 ImageControl 到调试控件（旧接口）: {_control.GetType().Name}",
                        "DefaultDebugWindow");
                }
                else
                {
                    PluginLogger.Info(
                        $"控件 {_control.GetType().Name} 不支持 SetMainImageControl 方法",
                        "DefaultDebugWindow");
                }
            }
        }

        /// <summary>
        /// 异步初始化（委托给内部控件）
        /// </summary>
        /// <remarks>
        /// 调用链：
        /// MainWindowViewModel → DefaultDebugWindow.InitializeAsync → ThresholdToolDebugControl.InitializeAsync
        /// </remarks>
        public async Task InitializeAsync()
        {
            if (_toolControl is IToolControlInitializer initializer)
            {
                await initializer.InitializeAsync();
                PluginLogger.Success(
                    $"调试控件异步初始化完成: {_control.GetType().Name}",
                    "DefaultDebugWindow");
            }
            else
            {
                PluginLogger.Info(
                    $"控件 {_control.GetType().Name} 未实现 IToolControlInitializer，跳过异步初始化",
                    "DefaultDebugWindow");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 设置按钮启用状态
        /// </summary>
        private void SetButtonsEnabled(bool enabled)
        {
            if (!_isContinuousRunning)
            {
                ContinuousRunButton.IsEnabled = enabled;
            }
            
            if (RunButton.Visibility == Visibility.Visible)
            {
                RunButton.IsEnabled = enabled;
            }
            
            ConfirmButton.IsEnabled = enabled;
        }

        /// <summary>
        /// 执行参数验证
        /// </summary>
        private bool ValidateParameters()
        {
            if (_toolControl != null)
            {
                return _toolControl.ValidateParameters();
            }

            // 默认返回true
            return true;
        }

        #endregion

        #region 窗口事件处理

        /// <summary>
        /// 标题栏鼠标左键按下（WindowChrome CaptionHeight 已处理拖拽，此处仅保留双击检测）
        /// </summary>
        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // WindowChrome 的 CaptionHeight=32 已接管标题栏拖拽；不再调用 DragMove()
            //（手动 DragMove 与 WindowChrome 冲突，且窗口未激活时可能抛异常）。
        }

        /// <summary>
        /// 标题栏鼠标左键弹起（检测双击最大化/还原）
        /// </summary>
        private void OnTitleBarMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
            }
        }

        /// <summary>
        /// 最大化按钮点击
        /// </summary>
        private void OnMaximizeButtonClick(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        /// <summary>
        /// 切换最大化/还原状态
        /// </summary>
        private void ToggleMaximizeRestore()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            UpdateMaximizeButtonContent();
        }

        /// <summary>
        /// 根据当前 WindowState 更新按钮图标（Segoe MDL2 Assets）
        /// </summary>
        private void UpdateMaximizeButtonContent()
        {
            // \uE739 = Maximize (❐), \uE923 = Restore (□)
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE739";
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 窗口关闭前事件处理 - 防御保底
        /// </summary>
        /// <remarks>
        /// 在窗口销毁前提交或取消所有 DataGrid 的编辑事务，防止"编辑中直接关窗"
        /// 残留 EditItem 事务，导致下次打开同集合绑定的窗口时 DeferRefresh 抛异常。
        /// DataGrid 不在编辑状态时 CommitEdit 返回 false 且无副作用，对所有工具调试窗口安全。
        /// </remarks>
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            PluginLogger.Info($"[绑定诊断] 标准调试窗口 Closing | 控件={_control.GetType().Name}", "BindingDiag");
            if (ToolEditorCommit.GetDraftContext(_control) is not null) return;
            if (!ToolEditorClose.TryClose(_control, this))
            {
                e.Cancel = true;
                return;
            }
            // 绑定状态必须在窗口关闭前提交到节点 ParamSettings，不能只依赖
            // 内部控件的 Unloaded 事件顺序。
            if (ToolEditorCommit.GetDraftContext(_control) is null) _toolControl?.PersistParameterBindings();
            PluginLogger.Info($"[绑定诊断] 标准调试窗口 Closing 完成 | 控件={_control.GetType().Name}", "BindingDiag");
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void OnWindowClosed(object? sender, EventArgs e)
        {
            if (ToolEditorCommit.GetDraftContext(_control) is not null)
                ToolEditorCommit.DiscardPendingEdits(_control);
            // 停止连续运行
            _isContinuousRunning = false;
            
            // 清理路由事件订阅
            UnsubscribeRoutedEvents();
        }

        #endregion
    }
}

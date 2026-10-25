using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Themes;
using SunEyeVision.Plugin.SDK.Commands;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Events;
using SunEyeVision.Plugin.SDK.Vision.Contracts;



namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 工具调试控件基类 - 提供标准实现和声明式编程支持
    /// </summary>
    /// <remarks>
    /// 架构说明：
    /// - 提供路由命令和路由事件的标准实现
    /// - 支持 XAML 声明式绑定
    /// - 提供数据注入和参数验证的标准方法
    /// 
    /// ⚠️ 重要：WPF XAML 根元素要求
    /// 
    /// 由于 WPF 框架限制，不支持泛型类型作为 XAML 根元素。
    /// 正确的使用方式：
    /// 
    /// 1. XAML 根元素使用 UserControl（非泛型基类）
    ///    <![CDATA[
    ///    <UserControl x:Class="MyNamespace.MyToolDebugControl"
    ///                 xmlns:commands="clr-namespace:SunEyeVision.Plugin.SDK.Commands;assembly=SunEyeVision.Plugin.SDK">
    ///        
    ///        <UserControl.CommandBindings>
    ///            <CommandBinding Command="commands:ToolCommands.Execute"
    ///                           Executed="OnExecuteCommand"
    ///                           CanExecute="CanExecuteCommand"/>
    ///            <CommandBinding Command="commands:ToolCommands.Confirm"
    ///                           Executed="OnConfirmCommand"/>
    ///        </UserControl.CommandBindings>
    ///        
    ///        <Button Content="执行"
    ///                Command="commands:ToolCommands.Execute"/>
    ///    </UserControl>
    ///    ]]>
    /// 
    /// 2. 后台代码继承非泛型基类
    ///    <![CDATA[
    ///    public partial class MyToolDebugControl : ToolDebugControlBase
    ///    {
    ///        public MyToolDebugControl()
    ///        {
    ///            InitializeComponent();
    ///            // 基类已自动调用 SetupCommandBindings()
    ///        }
    ///        
    ///        protected override object ExecuteTool()
    ///        {
    ///            // 实现执行逻辑
    ///            return new MyResults();
    ///        }
    ///    }
    ///    ]]>
    /// 
    /// 基类提供的功能（派生类自动获得）：
    /// ✅ 路由事件：ToolExecutionCompletedEvent
    /// ✅ 路由命令：Execute, Confirm, ContinuousExecute, ResetParameters
    /// ✅ 命令绑定：SetupCommandBindings() 自动设置
    /// ✅ 执行逻辑：OnExecuteCommand() 自动封装
    /// ✅ 数据注入：SetCurrentNode(), SetDataProvider()
    /// ✅ 参数验证：ValidateParameters()
    /// ✅ 抽象方法：ExecuteTool(), CanExecuteTool()
    /// 
    /// 使用示例：
    /// <![CDATA[
    /// public class ThresholdToolDebugControl : ToolDebugControlBase
    /// {
    ///     private ThresholdParameters _parameters;
    ///     
    ///     public ThresholdToolDebugControl()
    ///     {
    ///         InitializeComponent();
    ///         // 基类已自动调用 SetupCommandBindings()
    ///         
    ///         // 初始化默认参数
    ///         _parameters = new ThresholdParameters();
    ///         
    ///         // 设置绑定
    ///         SetupBindings();
    ///     }
    ///     
    ///     private void SetupBindings()
    ///     {
    ///         // 绑定到参数对象
    ///         var binding = new Binding("Threshold")
    ///         {
    ///             Source = _parameters,
    ///             Mode = BindingMode.TwoWay
    ///         };
    ///         thresholdSlider.SetBinding(Slider.ValueProperty, binding);
    ///     }
    ///     
    ///     protected override object ExecuteTool()
    ///     {
    ///         // 实现执行逻辑
    ///         var image = GetInputImage();
    ///         return ProcessImage(image, _parameters);
    ///     }
    ///     
    ///     protected override bool CanExecuteTool()
    ///     {
    ///         // 可选：添加执行条件检查
    ///         return _parameters != null && HasInputImage();
    ///     }
    /// }
    /// ]]>
    /// 
    /// 参考文档：
    /// - WPF 控件：https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/
    /// - 路由事件：https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/routed-events-overview
    /// </remarks>
    public abstract partial class ToolDebugControlBase : UserControl, System.ComponentModel.INotifyPropertyChanged, IToolControlInitializer
    {
        private bool _asyncCommandRunning;
        #region INotifyPropertyChanged 实现

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region 路由事件

        /// <summary>
        /// 工具执行完成路由事件
        /// </summary>
        /// <remarks>
        /// 使用示例：
        /// <![CDATA[
        /// <UserControl>
        ///     <i:Interaction.Triggers>
        ///         <i:EventTrigger EventName="ToolExecutionCompleted">
        ///             <!-- 事件处理 -->
        ///         </i:EventTrigger>
        ///     </i:Interaction.Triggers>
        /// </UserControl>
        /// ]]>
        /// </remarks>
        public static readonly RoutedEvent ToolExecutionCompletedEvent =
            EventManager.RegisterRoutedEvent(
                "ToolExecutionCompleted",
                RoutingStrategy.Bubble,
                typeof(ToolExecutionCompletedEventHandler),
                typeof(ToolDebugControlBase));

        /// <summary>
        /// 工具执行完成事件
        /// </summary>
        public event ToolExecutionCompletedEventHandler ToolExecutionCompleted
        {
            add => AddHandler(ToolExecutionCompletedEvent, value);
            remove => RemoveHandler(ToolExecutionCompletedEvent, value);
        }

        #endregion

        #region 属性

        /// <summary>
        /// 当前工作流节点
        /// </summary>
        public object? CurrentNode { get; protected set; }

        /// <summary>
        /// 数据提供者
        /// </summary>
        public object? DataProvider { get; protected set; }

        /// <summary>
        /// 执行耗时（毫秒）
        /// </summary>
        protected long ExecutionTime { get; private set; }

        /// <summary>
        /// 所有类型的数据源缓存（统一的绑定源，控件内部根据参数类型自动过滤）
        /// </summary>
        private readonly System.Collections.ObjectModel.ObservableCollection<AvailableDataSource> _availableDataSources;

        /// <summary>
        /// 所有类型的数据源（统一的绑定源，控件内部根据参数类型自动过滤）
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<AvailableDataSource> AvailableDataSources
        {
            get => _availableDataSources;
        }
        
        /// <summary>
        /// 当前节点ID
        /// </summary>
        private string? _currentNodeId;
        private readonly List<ConfigSetting> _parameterBindingControls = new();
        private readonly List<ParamBinding> _shapeBindingControls = new();
        private bool _restoringParameterBindings;

        /// <summary>
        /// 调试窗口是否显示最大化按钮（默认 false，脚本工具重写为 true）
        /// </summary>
        public virtual bool ShowMaximizeButton => false;

        /// <summary>
        /// 调试窗口首选尺寸（默认 400x550）
        /// </summary>
        public virtual Size PreferredDebugWindowSize => new Size(400, 550);

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建工具调试控件基类
        /// </summary>
        protected ToolDebugControlBase()
        {
            // 基类构造发生在具体工具 InitializeComponent 之前；提前为 VS 设计器装配
            // Generic，避免根 XAML 在解析 DynamicResource/共享 Style 时只得到空壳。
            DesignTimeThemeLoader.EnsureApplicationResources(this);

            // 工具根面板也必须消费语义资源。否则设计器画布的深色背景会从透明的
            // UserControl 透出，而内部 SDK 控件已命中 DefaultTheme，形成“外暗内亮”。
            // DynamicResource 在运行时继续由宿主 Light/Dark 字典实时覆盖。
            SetResourceReference(BackgroundProperty, SdkThemeKeys.PanelBackgroundBrush);
            SetResourceReference(ForegroundProperty, SdkThemeKeys.TextPrimaryBrush);

            // 初始化所有类型的数据源缓存
            _availableDataSources = new System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>();

            // 添加标准命令绑定
            SetupCommandBindings();

            Loaded += OnToolDebugControlLoaded;
            Unloaded += OnToolDebugControlUnloaded;
            ConfigSetting.InstanceLoaded += OnConfigSettingInstanceLoaded;
        }

        #endregion

        #region 命令绑定

        /// <summary>
        /// 设置标准命令绑定
        /// </summary>
        /// <remarks>
        /// 子类可以重写此方法添加自定义命令绑定
        /// </remarks>
        protected virtual void SetupCommandBindings()
        {
            // 执行命令
            CommandBindings.Add(new CommandBinding(
                ToolCommands.Execute,
                OnExecuteCommand,
                CanExecuteCommand));

            // 确认命令
            CommandBindings.Add(new CommandBinding(
                ToolCommands.Confirm,
                OnConfirmCommand,
                CanConfirmCommand));

            // 连续执行命令
            CommandBindings.Add(new CommandBinding(
                ToolCommands.ContinuousExecute,
                OnContinuousExecuteCommand,
                CanExecuteCommand));

            // 重置参数命令
            CommandBindings.Add(new CommandBinding(
                ToolCommands.ResetParameters,
                OnResetParametersCommand));
        }

        #endregion

        #region 命令处理

        /// <summary>
        /// 执行命令处理
        /// </summary>
        protected virtual async void OnExecuteCommand(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (_asyncCommandRunning) return;
            _asyncCommandRunning = true;
            PluginLogger.Info("执行命令触发", GetType().Name);

            try
            {
                var stopwatch = Stopwatch.StartNew();

                // 执行工具逻辑
                if (!ToolEditorCommit.TryPrepare(this, out var commitError))
                    throw new InvalidOperationException(commitError);
                PersistParameterBindings();
                var results = await ExecuteToolAsync();
                if ((results as SunEyeVision.Plugin.SDK.Execution.Results.ToolResults)?.ExecutionIdentity?.IsCurrent == false)
                {
                    (results as IDisposable)?.Dispose();
                    return;
                }

                stopwatch.Stop();
                ExecutionTime = stopwatch.ElapsedMilliseconds;

                // 触发完成事件
                try { RaiseToolExecutionCompleted(results, ExecutionTime); }
                finally { (results as SunEyeVision.Plugin.SDK.Execution.Results.ToolResults)?.Resources?.Dispose(); }

                PluginLogger.Success($"工具执行完成，耗时 {ExecutionTime}ms", GetType().Name);
            }
            catch (Exception ex)
            {
                PluginLogger.Error($"工具执行失败: {ex.Message}", GetType().Name, ex);
                RaiseToolExecutionFailed(ex.Message);
            }
            finally { _asyncCommandRunning = false; System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
        }

        /// <summary>
        /// 判断是否可执行
        /// </summary>
        protected virtual void CanExecuteCommand(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !_asyncCommandRunning && CanExecuteTool();
        }

        /// <summary>
        /// 确认命令处理
        /// </summary>
        protected virtual void OnConfirmCommand(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            PluginLogger.Info("确认命令触发", GetType().Name);

            if (ValidateParameters())
            {
                PluginLogger.Success("参数验证通过", GetType().Name);
                // 窗口会监听此事件并关闭
            }
            else
            {
                PluginLogger.Warning("参数验证失败", GetType().Name);
                MessageBox.Show("参数验证失败，请检查输入参数。", "验证失败", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 判断是否可确认
        /// </summary>
        protected virtual void CanConfirmCommand(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        /// <summary>
        /// 连续执行命令处理
        /// </summary>
        protected virtual void OnContinuousExecuteCommand(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            PluginLogger.Info("连续执行命令触发", GetType().Name);
            // 子类可重写实现连续执行逻辑
        }

        /// <summary>
        /// 重置参数命令处理
        /// </summary>
        protected virtual void OnResetParametersCommand(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            PluginLogger.Info("重置参数命令触发", GetType().Name);
            ResetParameters();
        }

        #endregion

        #region 数据注入方法

        /// <summary>
        /// 外部结果到达通知（基类默认空实现）
        /// </summary>
        /// <remarks>
        /// 工作流引擎执行完成后调用，子类可重写以刷新依赖运行时的 UI 状态（如位置修正矩阵）。
        /// </remarks>
        public virtual void OnExternalResultArrived()
        {
            // 基类默认空实现
        }

        /// <summary>
        /// 设置当前节点
        /// </summary>
        public virtual void SetCurrentNode(object node)
        {
            CurrentNode = node;
            PluginLogger.Info($"已设置当前节点: {node?.GetType().Name}", GetType().Name);

            // 从节点对象中提取节点ID和名称
            string? nodeName = null;
            if (node != null)
            {
                // 尝试通过反射获取 Id 属性
                var idProperty = node.GetType().GetProperty("Id");
                if (idProperty != null)
                {
                    _currentNodeId = idProperty.GetValue(node) as string;
                    PluginLogger.Info($"已提取节点ID: {_currentNodeId}", GetType().Name);
                }
                else
                {
                    PluginLogger.Warning($"节点对象 {node.GetType().Name} 没有 Id 属性", GetType().Name);
                }

                // 优先 DispName（UI显示名），回退到 Name
                var dispNameProperty = node.GetType().GetProperty("DispName");
                var nameProperty = node.GetType().GetProperty("Name");
                nodeName = dispNameProperty?.GetValue(node) as string
                        ?? nameProperty?.GetValue(node) as string;

                // ★ 监控日志：SetCurrentNode 完整节点信息
                var dispName = dispNameProperty?.GetValue(node) as string;
                var name = nameProperty?.GetValue(node) as string;
                PluginLogger.Info(
                    $"[监控] SetCurrentNode 完整信息 | " +
                    $"nodeId={_currentNodeId ?? "null"} | " +
                    $"DispName={dispName ?? "null"} | " +
                    $"Name={name ?? "null"} | " +
                    $"nodeName={nodeName ?? "null"} | " +
                    $"nodeType={node.GetType().FullName}",
                    "App-Monitor");
            }
            else
            {
                _currentNodeId = null;
                PluginLogger.Warning("[监控] SetCurrentNode: node 为 null", "App-Monitor");
            }

            // 如果数据提供者已设置，重新填充数据源（使用当前节点ID）
            if (DataProvider is DataSourceQueryService queryService && !string.IsNullOrEmpty(_currentNodeId))
            {
                PluginLogger.Info($"重新填充数据源（节点ID: {_currentNodeId}）", GetType().Name);
                PopulateParameterSources(queryService);
            }

            RestoreExternalParameterBindings();

            // 子类重写以更新 UI
        }

        /// <summary>
        /// 设置节点参数（基类默认空实现）
        /// </summary>
        /// <remarks>
        /// 基类仅记录日志，派生类应重写以存储参数引用并更新UI。
        /// 派生类重写时建议调用 base.SetParameters(parameters)。
        /// </remarks>
        public virtual void SetParameters(ToolParameters parameters)
        {
            if (_previewParameters != null)
                PropertyChangedEventManager.RemoveHandler(_previewParameters, OnPreviewParameterChanged, "");
            _previewParameters = parameters;
            PropertyChangedEventManager.AddHandler(parameters, OnPreviewParameterChanged, "");
            PluginLogger.Info(
                $"参数已设置: {parameters?.GetType().Name}",
                GetType().Name);

            // SetCurrentNode 通常先于 SetParameters 调用，而派生控件会在
            // base.SetParameters 返回后才把 Parameters 绑定到 DataContext。
            // 延迟到当前 Dispatcher 队列，确保恢复发生在派生控件完成参数注入之后。
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle,
                new Action(RestoreExternalParameterBindings));
        }

        private void OnToolDebugControlLoaded(object sender, RoutedEventArgs e)
        {
            InstallBindingIssueSummary();
            if (_previewParameters != null)
            {
                PropertyChangedEventManager.RemoveHandler(_previewParameters, OnPreviewParameterChanged, "");
                PropertyChangedEventManager.AddHandler(_previewParameters, OnPreviewParameterChanged, "");
            }
            Dispatcher.BeginInvoke(new Action(OnExternalResultArrived));
            SuppressClosePersistence = false;
            ToolEditorCommit.Register(this);
            ConfigSetting.InstanceLoaded -= OnConfigSettingInstanceLoaded;
            ConfigSetting.InstanceLoaded += OnConfigSettingInstanceLoaded;
            SubscribeParameterBindingControls();
            RestoreExternalParameterBindings();
            QueueBindingIssueRefresh();
        }

        private void OnToolDebugControlUnloaded(object sender, RoutedEventArgs e)
        {
            if (_previewParameters != null)
                PropertyChangedEventManager.RemoveHandler(_previewParameters, OnPreviewParameterChanged, "");
            ToolEditorCommit.Unregister(this);
            ConfigSetting.InstanceLoaded -= OnConfigSettingInstanceLoaded;
            // 关闭/切换调试窗口时做一次最终落盘，避免绑定选择事件在控件
            // 尚未完成 Loaded 或窗口关闭竞态中丢失。
            if (!SuppressClosePersistence) PersistParameterBindings();

            foreach (var control in _parameterBindingControls)
                control.BindingConfigChanged -= OnParameterBindingChanged;
            foreach (var control in _shapeBindingControls)
                control.RemoveHandler(ParamBinding.BindingSourceSelectedEvent, new RoutedEventHandler(OnShapeBindingChanged));
            _parameterBindingControls.Clear();
            _shapeBindingControls.Clear();
        }

        private void OnConfigSettingInstanceLoaded(ConfigSetting control)
        {
            if (Dispatcher != control.Dispatcher || !Dispatcher.CheckAccess()) return;
            // ConfigSetting 通过静态事件报告自身已经进入视觉树。只有属于当前
            // 调试控件的实例才触发恢复，避免依赖 TabControl 的轮询时序。
            if (!ReferenceEquals(control, this)
                && !control.IsDescendantOf(this))
                return;

            SubscribeParameterBindingControls();
            RestoreExternalParameterBindings();
        }

        /// <summary>
        /// 将当前调试控件中的绑定状态立即同步到当前节点。
        /// 窗口壳在 Closed 时也会调用此方法，避免依赖 WPF 的 Unloaded 时序。
        /// </summary>
        public void PersistParameterBindings()
        {
            if (SuppressClosePersistence) return;
            SubscribeParameterBindingControls();
            LogBindingDiagnostics("关闭前提交", includeControls: true);
            foreach (var control in _parameterBindingControls)
                OnParameterBindingChanged(control, EventArgs.Empty);
            foreach (var control in _shapeBindingControls)
                OnShapeBindingChanged(control, new RoutedEventArgs());
            LogBindingDiagnostics("关闭后提交", includeControls: true);
        }

        private void SubscribeParameterBindingControls()
        {
            foreach (var control in FindParameterBindingControls(this))
            {
                if (_parameterBindingControls.Contains(control))
                    continue;
                _parameterBindingControls.Add(control);
                control.BindingConfigChanged += OnParameterBindingChanged;
            }
            foreach (var control in FindShapeBindingControls(this))
            {
                if (_shapeBindingControls.Contains(control))
                    continue;
                _shapeBindingControls.Add(control);
                control.AddHandler(ParamBinding.BindingSourceSelectedEvent, new RoutedEventHandler(OnShapeBindingChanged));
                control.AddHandler(ParamBinding.BindingStateChangedEvent, new RoutedEventHandler(OnShapeBindingChanged));
            }
        }

        private void OnParameterBindingChanged(object? sender, EventArgs e)
        {
            if (_restoringParameterBindings || sender is not ConfigSetting control)
                return;

            if (CurrentNode?.GetType().GetProperty("ParamSettings")?.GetValue(CurrentNode)
                is not ParamSettingContainer settings)
            {
                PluginLogger.Warning(
                    $"[绑定诊断] 写入失败：当前节点没有 ParamSettings | 参数={control.ParameterName} | 控件={GetType().Name}",
                    "BindingDiag");
                return;
            }

            if (control.BindingType == BindingType.Binding && !string.IsNullOrWhiteSpace(control.BindingSource))
            {
                settings.SetSetting(control.ToParamSetting());
                PluginLogger.Info($"[绑定持久化] 参数={control.ParameterName} | 源={control.BindingSource} | 设置数={settings.Count}", "ToolDebugControlBase");
            }
            else
            {
                var requiresConfiguration = (CurrentNode?.GetType().GetProperty("Parameters")?.GetValue(CurrentNode) as ToolParameters)?.RebindingRequired.ContainsKey(control.ParameterName) == true;
                if (requiresConfiguration && control.BindingType == BindingType.Constant) settings.SetSetting(control.ToParamSetting());
                else settings.RemoveSetting(control.ParameterName);
                PluginLogger.Info($"[绑定清除] 参数={control.ParameterName} | 设置数={settings.Count}", "ToolDebugControlBase");
            }
        }

        private void OnShapeBindingChanged(object sender, RoutedEventArgs e)
        {
            if (_restoringParameterBindings)
                return;

            var control = e.OriginalSource as ParamBinding ?? sender as ParamBinding;
            if (control == null || string.IsNullOrWhiteSpace(control.ParameterName))
                return;

            if (CurrentNode?.GetType().GetProperty("ParamSettings")?.GetValue(CurrentNode)
                is not ParamSettingContainer settings)
                return;

            var source = control.BindingSource;
            if (string.IsNullOrWhiteSpace(source))
            {
                settings.RemoveSetting(control.ParameterName);
                return;
            }

            var dotIndex = source.IndexOf('.');
            var sourceNodeId = dotIndex > 0 ? source[..dotIndex] : source;
            var sourceProperty = dotIndex > 0 ? source[(dotIndex + 1)..] : string.Empty;
            if (string.IsNullOrWhiteSpace(sourceNodeId) || string.IsNullOrWhiteSpace(sourceProperty))
            {
                settings.RemoveSetting(control.ParameterName);
                return;
            }

            settings.SetSetting(ParamSetting.CreateBinding(control.ParameterName, sourceNodeId, sourceProperty));
        }

        public void RefreshCommittedBindings(bool onlyInvalidated = false)
        {
            RestoreExternalParameterBindings(onlyInvalidated);
            if (DataProvider is DataSourceQueryService query) PopulateParameterSources(query);
            RefreshBindingIssues();
        }

        private void RestoreExternalParameterBindings() => RestoreExternalParameterBindings(false);

        private void RestoreExternalParameterBindings(bool onlyInvalidated)
        {
            QueueBindingIssueRefresh();
            if (CurrentNode?.GetType().GetProperty("ParamSettings")?.GetValue(CurrentNode)
                is not ParamSettingContainer settings)
                return;

            var byName = settings.Settings.ToDictionary(item => item.ParameterName, StringComparer.Ordinal);
            PluginLogger.Info(
                $"[绑定诊断] 开始恢复 | 节点={GetCurrentNodeStringProperty("Id") ?? "null"} | " +
                $"设置数={byName.Count} | 标量控件={_parameterBindingControls.Count} | 图形控件={_shapeBindingControls.Count}",
                "BindingDiag");
            var required = (CurrentNode?.GetType().GetProperty("Parameters")?.GetValue(CurrentNode) as ToolParameters)?.RebindingRequired;
            SubscribeParameterBindingControls();
            if (_parameterBindingControls.Count == 0 && _shapeBindingControls.Count == 0)
            {
                // 参数控件尚未生成。ConfigSetting.Loaded 会通过 InstanceLoaded
                // 事件再次触发恢复，不在这里使用定时轮询。
                return;
            }
            _restoringParameterBindings = true;
            try
            {
                foreach (var control in _parameterBindingControls)
                {
                    if (onlyInvalidated && required?.ContainsKey(control.ParameterName) != true) continue;
                    if (byName.TryGetValue(control.ParameterName, out var setting))
                    {
                        PluginLogger.Info(
                            $"[绑定诊断] 恢复参数={control.ParameterName} | 类型={setting.BindingType} | " +
                            $"源={setting.SourceNodeId}.{setting.SourceProperty}",
                            "BindingDiag");
                        control.FromParamSetting(setting);
                    }
                    else if (required?.ContainsKey(control.ParameterName) == true)
                    {
                        control.ClearCommittedBinding();
                    }
                }
                foreach (var control in _shapeBindingControls)
                {
                    if (onlyInvalidated && required?.ContainsKey(control.ParameterName) != true) continue;
                    if (!byName.TryGetValue(control.ParameterName, out var setting)
                        || setting.BindingType != BindingType.Binding)
                    {
                        if (required?.ContainsKey(control.ParameterName) == true) control.BindingSource = string.Empty;
                        continue;
                    }

                    var source = string.IsNullOrWhiteSpace(setting.SourceNodeId)
                        ? string.Empty
                        : $"{setting.SourceNodeId}.{setting.SourceProperty}";
                    control.BindingSource = source;
                }
            }
            finally
            {
                _restoringParameterBindings = false;
            }
        }


        private void LogBindingDiagnostics(string stage, bool includeControls)
        {
            var nodeId = GetCurrentNodeStringProperty("Id") ?? "null";
            var settings = CurrentNode?.GetType().GetProperty("ParamSettings")?.GetValue(CurrentNode)
                as ParamSettingContainer;
            var definitions = settings?.Settings.ToList() ?? new List<ParamSetting>();
            PluginLogger.Info(
                $"[绑定诊断] {stage} | 节点={nodeId} | 设置数={definitions.Count} | " +
                $"标量控件={_parameterBindingControls.Count} | 图形控件={_shapeBindingControls.Count}",
                "BindingDiag");

            foreach (var setting in definitions)
            {
                PluginLogger.Info(
                    $"[绑定诊断] 定义 | 参数={setting.ParameterName} | 类型={setting.BindingType} | " +
                    $"源={setting.SourceNodeId}.{setting.SourceProperty} | 显示={setting.SourceNodeName}.{setting.SourceDisplayName}",
                    "BindingDiag");
            }

            if (!includeControls)
                return;

            foreach (var control in _parameterBindingControls)
            {
                PluginLogger.Info(
                    $"[绑定诊断] 控件 | 参数={control.ParameterName} | 类型={control.BindingType} | " +
                    $"源={control.BindingSource} | 显示={control.BindingDisplayText}",
                    "BindingDiag");
            }
        }

        private static IEnumerable<ConfigSetting> FindParameterBindingControls(DependencyObject root)
        {
            if (root is ConfigSetting configSetting)
                yield return configSetting;

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in FindParameterBindingControls(VisualTreeHelper.GetChild(root, index)))
                    yield return child;
            }
        }

        private static IEnumerable<ParamBinding> FindShapeBindingControls(DependencyObject root)
        {
            // ConfigSetting owns its template's ParamBinding. Persisting that inner control
            // separately would erase a constant just written by ConfigSetting for the same key.
            if (root is ConfigSetting) yield break;
            // Anonymous protocol list inputs persist their own expression in the item parameters.
            // They must not share an empty ParamSettings key or overwrite each other on reopen.
            if (root is ParamBinding paramBinding && !string.IsNullOrWhiteSpace(paramBinding.ParameterName))
                yield return paramBinding;

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in FindShapeBindingControls(VisualTreeHelper.GetChild(root, index)))
                    yield return child;
            }
        }

        /// <summary>
        /// 设置数据提供者
        /// </summary>
        public virtual void SetDataProvider(object dataProvider)
        {
            DataProvider = dataProvider;
            PluginLogger.Info($"已设置数据提供者: {dataProvider?.GetType().Name}", GetType().Name);

            // 填充参数数据源
            if (DataProvider is DataSourceQueryService queryService)
            {
                PopulateParameterSources(queryService);
            }

            // 子类重写以更新图像源选择器等
        }

        /// <summary>
        /// 经统一执行器运行调试工具。
        /// 调试执行与工作流执行使用相同的上游结果上下文，确保订阅 ROI、参数绑定和
        /// 文本样式绑定在两种入口中的解析结果一致。
        /// </summary>
        protected ToolResults ExecuteWithLiveContext(
            IToolPlugin tool,
            ToolParameters parameters,
            IVisionImage? resolvedInputImage = null)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            var request = CreateLiveExecutionRequest(parameters, resolvedInputImage);
            var logger = VisionLogger.Instance;
            return new ToolExecutor(new ParameterResolver(DataProvider as IDataSourceQueryService, logger), logger)
                .Run(tool, request);
        }

        /// <summary>
        /// 经统一执行器运行强类型调试工具，并保留工具声明的具体结果类型。
        /// </summary>
        protected TResult ExecuteWithLiveContext<TParameters, TResult>(
            IToolPlugin<TParameters, TResult> tool,
            TParameters parameters,
            IVisionImage? resolvedInputImage = null)
            where TParameters : ToolParameters, new()
            where TResult : ToolResults, new()
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            var request = CreateLiveExecutionRequest(parameters, resolvedInputImage);
            var logger = VisionLogger.Instance;
            return new ToolExecutor(new ParameterResolver(DataProvider as IDataSourceQueryService, logger), logger)
                .Run(tool, request);
        }

        private ToolParameters? _previewParameters;
        private readonly List<Measure.Views.MeasureEditorControl> _caliperEditors = new();
        private void OnPreviewParameterChanged(object? sender, PropertyChangedEventArgs e)
        {
            QueueBindingIssueRefresh();
            if (e.PropertyName is not ("IsPositionCorrectionEnabled" or "PositionCorrectionBindingSource" or "PositionCorrectionSource" or "ImageSource" or "ImageSourceRef")) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsLoaded && ReferenceEquals(sender, _previewParameters))
                {
                    foreach (var editor in _caliperEditors) editor.CancelDrawing();
                    OnExternalResultArrived();
                }
            }));
        }

        /// <summary>Shared first-draw policy for every caliper, using its actual input binding.</summary>
        protected void ConfigureCaliperDrawing(Measure.Views.MeasureEditorControl editor,
            MeasureGuideParameters parameters, int element, double defaultWidth = 40.0, double defaultHeight = 10.0)
        {
            if (!_caliperEditors.Contains(editor)) _caliperEditors.Add(editor);
            editor.CaliberSizeEdited = () => parameters.CaliperSizing.Preserve(element);
            (int Width, int Height) validatedSize = default;
            editor.DrawingStarted = () =>
            {
                if (!parameters.CaliperSizing.NeedsInitialization(element)) return;
                // Custom sizes supplied before first draw also belong to the user.
                if (Math.Abs(editor.CaliberWidth - defaultWidth) < 1e-9 &&
                    Math.Abs(editor.CaliberHeight - defaultHeight) < 1e-9)
                    editor.InitializeCaliberParamsForImage(validatedSize.Width, validatedSize.Height);
                parameters.CaliperSizing.Preserve(element);
            };
            editor.DrawingAvailabilityCheck = () =>
            {
                try
                {
                    if (_caliperImageControl?.SourceImage is not { } preview)
                        throw new InvalidOperationException("当前没有可绘制的图像，请先显示工具绑定的输入图像。");
                    if (DataProvider is not IDataSourceQueryService query)
                        throw new InvalidOperationException("图像数据来源尚未就绪。");
                    var reference = ToolExecutionPreparation.ReadReference(parameters, "ImageSource")
                        ?? throw new InvalidOperationException("请先绑定工具的输入图像。");
                    var result = query.GetNodeContext(reference.SourceNodeId)?.Result;
                    if (result == null)
                        throw new InvalidOperationException("绑定的输入图像暂无结果，请先执行图像来源节点。");
                    var image = ToolExecutionPreparation.ResolveInputImage(parameters,
                        new Dictionary<string, ToolResults> { [reference.SourceNodeId] = result }, null)!;
                    if (!ReferenceEquals(result, query.GetNodeContext(reference.SourceNodeId)?.Result))
                        throw new InvalidOperationException("输入图像正在更新，请稍后重新绘制。");
                    var frame = ImageFrameProvenance.CaptureInput(image);
                    var displayedFrame = PreviewFrameProvenance.Read(preview);
                    if (preview.PixelWidth != image.Width || preview.PixelHeight != image.Height ||
                        (frame.HasValue && displayedFrame.HasValue && frame != displayedFrame))
                        throw new InvalidOperationException("预览与工具绑定的输入图像不一致，请先显示绑定图像再绘制。");
                    validatedSize = (image.Width, image.Height);
                    return true;
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    MessageBox.Show(Window.GetWindow(this), ex.Message, "无法绘制卡尺",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }
            };
        }

        protected void ApplyPositionCorrectionPreview(ToolParameters parameters,
            Action<AffineMatrix?> applyMatrix, FrameworkElement? editor = null)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => ApplyPositionCorrectionPreview(parameters, applyMatrix, editor)));
                return;
            }
            if (DataProvider is not IDataSourceQueryService query) return;
            var correction = PositionCorrectionResolution.Capture(parameters, query,
                ToolExecutionPreparation.RequiresConformalCorrection(parameters));
            if (editor != null)
            {
                editor.IsEnabled = correction.CanUse;
                editor.ToolTip = correction.Message;
            }
            applyMatrix(correction.Matrix);
        }

        protected ToolExecutionRequest CreateLiveExecutionRequest(
            ToolParameters parameters,
            IVisionImage? resolvedInputImage,
            IReadOnlyDictionary<string, ToolResults>? nodeResultsSnapshot = null,
            System.Threading.CancellationToken cancellationToken = default)
        {

            if (DataProvider is DataSourceQueryService query && query.CapturePublishedGlobals is { } captureGlobals)
                query.SetGlobalVariables(captureGlobals());
            var provider = DataProvider as IDataSourceQueryService;
            var bindings = ParameterBindingSet.FromSettings(
                CurrentNode?.GetType().GetProperty("ParamSettings")?.GetValue(CurrentNode)
                    as ParamSettingContainer);
            var nodeResults = nodeResultsSnapshot != null
                ? nodeResultsSnapshot.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : provider == null && parameters.NodeResults != null
                ? new Dictionary<string, ToolResults>(parameters.NodeResults)
                : new Dictionary<string, ToolResults>();
            ToolResultSnapshot? capturedResults = null;
            try
            {

            if (provider != null)
            {
                var currentNodeId = GetCurrentNodeStringProperty("Id") ?? _currentNodeId;
                var sourceIds = provider.GetAvailableDataSources(currentNodeId ?? string.Empty)
                    .Select(source => source.SourceNodeId)
                    .Concat(BindingInputCatalog.Describe(parameters).Where(input => input.Active)
                        .Select(input => input.Read()).Where(path => !string.IsNullOrWhiteSpace(path) && path.Contains('.'))
                        .Select(path => path[..path.IndexOf('.')]))
                    .Concat(bindings.Bindings.Where(binding => binding.BindingType == BindingType.Binding)
                        .Select(binding => binding.SourceNodeId)).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();
                if (!string.IsNullOrEmpty(currentNodeId))
                {
                    var upstream = provider.GetParentNodes(currentNodeId).Select(n => n.NodeId).ToHashSet();
                    if (sourceIds.Any(id => !id.StartsWith("__", StringComparison.Ordinal) && !upstream.Contains(id)))
                        throw new InvalidOperationException("绑定来源不是画布连接的上游节点，请先建立执行连接或改用全局变量。");
                }
                // Never seed a new debug run with runtime fields left on an editable parameter
                // object by an earlier run. The query provider is the current workflow SSOT.
                if (nodeResultsSnapshot == null)
                {
                    capturedResults = ToolDebugResultSnapshot.Capture(provider, sourceIds, requireAll: false);
                    nodeResults = capturedResults.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                }
            }

            resolvedInputImage = ToolExecutionPreparation.ResolveInputImage(parameters, nodeResults,
                resolvedInputImage ?? parameters.ResolvedInputImage);
            var executionParameters = (DataProvider as DataSourceQueryService)?.CreateParameterSnapshot?.Invoke(parameters)
                ?? ToolExecutionPreparation.CopyParameters(parameters);
            var request = new ToolExecutionRequest(executionParameters)
            {
                InputResources = capturedResults ?? new ToolResultSnapshot(nodeResults),
                NodeResults = nodeResults,
                ResolvedInputImage = resolvedInputImage,
                NodeId = GetCurrentNodeStringProperty("Id") ?? _currentNodeId,
                CancellationToken = cancellationToken.CanBeCanceled ? cancellationToken
                    : (DataProvider as DataSourceQueryService)?.EditScopeCancellation ?? default,
                WorkflowId = (DataProvider as DataSourceQueryService)?.WorkflowId,
                ExecutionOwner = (DataProvider as DataSourceQueryService)?.ExecutionOwner,
                RunId = Guid.NewGuid(),
                ResourcePaths = (DataProvider as DataSourceQueryService)?.ResourcePaths,
                NodeName = GetCurrentNodeStringProperty("DisplayName")
                    ?? GetCurrentNodeStringProperty("Name"),
                ParameterBindings = bindings,
                GlobalVariables = (DataProvider as DataSourceQueryService)?.GlobalVariables,
                CommunicationInputs = (DataProvider as DataSourceQueryService)?.CommunicationReceiveService,
                CommunicationOutputs = (DataProvider as DataSourceQueryService)?.CommunicationSendService,
            };

            if (DataProvider is DataSourceQueryService sourceService
                && sourceService.GlobalVariables.Count > 0)
            {
                var globalResults = new GenericToolResults { Status = ExecutionStatus.Success };
                foreach (var variable in sourceService.GlobalVariables)
                    globalResults.AddResultItem(variable.Key, variable.Value);
                nodeResults["__GlobalVars__"] = globalResults;
            }

            return request;
            }
            catch
            {
                capturedResults?.Dispose();
                throw;
            }
        }

        private string? GetCurrentNodeStringProperty(string propertyName)
            => CurrentNode?.GetType().GetProperty(propertyName)?.GetValue(CurrentNode) as string;

        /// <summary>
        /// 设置主窗口图像控件（基类默认空实现）
        /// </summary>
        /// <remarks>
        /// 仅用于区域编辑器等需要在主窗口图像上绘制 ROI 的场景。
        /// 不需要此功能的控件无需重写此方法。
        /// </remarks>
        private ImageControl? _caliperImageControl;

        public virtual void SetMainImageControl(ImageControl? imageControl)
        {
            _caliperImageControl = imageControl;
        }

        /// <summary>
        /// 异步初始化（基类默认空实现）
        /// </summary>
        /// <remarks>
        /// 在所有 Set 方法完成后调用。
        /// 派生类可重写以执行依赖多项数据的初始化逻辑。
        /// </remarks>
        public virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 填充参数数据源
        /// </summary>
        /// <summary>Retained for host compatibility; it no longer permits non-upstream candidates.</summary>
        public bool IncludeOtherBindingSources { get; set; }
        internal bool SuppressClosePersistence { get; set; }

        private void PopulateParameterSources(DataSourceQueryService dataProvider)
        {
            // 清空数据源缓存
            _availableDataSources.Clear();

            // 获取所有数据源（保持父节点距离顺序）
            var allDataSources = dataProvider.GetAvailableDataSources(_currentNodeId ?? "");

            // 添加到统一缓存（保持父节点顺序）
            foreach (var ds in allDataSources)
            {
                _availableDataSources.Add(ds);
            }

            PluginLogger.Info($"PopulateParameterSources: 参数数据源更新完成 [总数: {_availableDataSources.Count}]（保持父节点距离顺序）", GetType().Name);
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        public virtual bool ValidateParameters()
        {
            // 子类重写以实现具体验证逻辑
            return true;
        }

        #endregion

        #region 抽象方法

        /// <summary>
        /// 执行工具逻辑 - 子类必须实现
        /// </summary>
        /// <returns>执行结果</returns>
        protected abstract object ExecuteTool();
        protected virtual System.Threading.Tasks.Task<object> ExecuteToolAsync() => System.Threading.Tasks.Task.FromResult(ExecuteTool());

        /// <summary>
        /// 判断是否可执行工具 - 子类可重写
        /// </summary>
        /// <returns>是否可执行</returns>
        protected virtual bool CanExecuteTool()
        {
            return true;
        }

        /// <summary>
        /// 重置参数到默认值 - 子类可重写
        /// </summary>
        protected virtual void ResetParameters()
        {
            // 子类重写以实现重置逻辑
            PluginLogger.Info("参数已重置", GetType().Name);
        }

        #endregion

        #region 事件触发

        /// <summary>
        /// 触发工具执行完成事件
        /// </summary>
        /// <param name="results">执行结果</param>
        /// <param name="executionTime">执行耗时（毫秒，可选）</param>
        protected void RaiseToolExecutionCompleted(object results, long executionTime = 0)
        {
            var args = executionTime > 0
                ? new ToolExecutionCompletedEventArgs(results, executionTime, ToolExecutionCompletedEvent, this)
                : new ToolExecutionCompletedEventArgs(results, ToolExecutionCompletedEvent, this);

            RaiseEvent(args);
        }

        /// <summary>
        /// 触发工具执行失败事件
        /// </summary>
        /// <param name="errorMessage">错误信息</param>
        protected void RaiseToolExecutionFailed(string errorMessage)
        {
            var args = new ToolExecutionCompletedEventArgs(errorMessage, ToolExecutionCompletedEvent, this);
            RaiseEvent(args);
        }

        #endregion
    }
}

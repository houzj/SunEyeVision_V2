using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Themes;
using SunEyeVision.Plugin.SDK.Commands;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
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
    public abstract class ToolDebugControlBase : UserControl, System.ComponentModel.INotifyPropertyChanged, IToolControlInitializer
    {
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
        protected virtual void OnExecuteCommand(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            PluginLogger.Info("执行命令触发", GetType().Name);

            try
            {
                var stopwatch = Stopwatch.StartNew();

                // 执行工具逻辑
                var results = ExecuteTool();

                stopwatch.Stop();
                ExecutionTime = stopwatch.ElapsedMilliseconds;

                // 触发完成事件
                RaiseToolExecutionCompleted(results, ExecutionTime);

                PluginLogger.Success($"工具执行完成，耗时 {ExecutionTime}ms", GetType().Name);
            }
            catch (Exception ex)
            {
                PluginLogger.Error($"工具执行失败: {ex.Message}", GetType().Name, ex);
                RaiseToolExecutionFailed(ex.Message);
            }
        }

        /// <summary>
        /// 判断是否可执行
        /// </summary>
        protected virtual void CanExecuteCommand(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanExecuteTool();
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
            PluginLogger.Info(
                $"参数已设置: {parameters?.GetType().Name}",
                GetType().Name);
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

        private ToolExecutionRequest CreateLiveExecutionRequest(
            ToolParameters parameters,
            IVisionImage? resolvedInputImage)
        {

            var nodeResults = parameters.NodeResults != null
                ? new Dictionary<string, ToolResults>(parameters.NodeResults)
                : new Dictionary<string, ToolResults>();

            var provider = DataProvider as IDataSourceQueryService;
            if (provider != null)
            {
                var currentNodeId = GetCurrentNodeStringProperty("Id");
                foreach (var source in provider.GetAvailableDataSources(currentNodeId ?? string.Empty))
                {
                    if (string.IsNullOrWhiteSpace(source.SourceNodeId)) continue;

                    if (provider.GetNodeContext(source.SourceNodeId)?.Result is { } result)
                        nodeResults[source.SourceNodeId] = result;
                }
            }

            var request = new ToolExecutionRequest(parameters)
            {
                NodeResults = nodeResults,
                ResolvedInputImage = resolvedInputImage ?? parameters.ResolvedInputImage,
                NodeId = GetCurrentNodeStringProperty("Id"),
                NodeName = GetCurrentNodeStringProperty("DisplayName")
                    ?? GetCurrentNodeStringProperty("Name"),
            };

            return request;
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
        public virtual void SetMainImageControl(ImageControl? imageControl)
        {
            // 基类默认空实现
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

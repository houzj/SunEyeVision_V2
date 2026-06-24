using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.IO;
using SunEyeVision.UI.Adapters;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using System.Diagnostics;
using System.Windows.Threading;
using System.Windows.Data;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.UI.Views.Windows;
using System.Runtime.InteropServices;
using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Monitoring;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using System.Windows.Media;
using SunEyeVision.UI.ViewModels;
using SunEyeVision.UI.Infrastructure;
using SunEyeVision.UI.Events;

namespace SunEyeVision.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // RPC_S_SERVER_UNAVAILABLE = 0x000006BA = 1722
    private const int RPC_S_SERVER_UNAVAILABLE = 1722;
    private const int HRESULT_RPC_UNAVAILABLE = unchecked((int)0x800706BA);

    // 监控定时器
    private DispatcherTimer? _monitorTimer;
    private int _monitorReportInterval = 30; // 每30秒报告一次

    static App()
    {
        // 只记录警告和错误级别的绑定信息，避免性能问题
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // 设置控制台编码为UTF-8，解决中文乱码问题
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // P0优化：预热线程池，消除首次加载延迟
        PrewarmThreadPool();
        
        // 添加FirstChanceException处理 - 捕获RPC异常避免调试器中断
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;

        base.OnStartup(e);

        // 添加全局异常处理
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 注册文件关联（仅当前用户，无需管理员权限）
        RegisterFileAssociation();

        // ===== P0优化：调整启动顺序，先加载插件 =====
        // 原因：SolutionSettings.Save() 使用 WorkflowSerializationOptions.Default
        // 会触发 ParameterTypeRegistry.EnsureInitialized()，需要插件已加载
        // 解决：在任何可能触发序列化操作之前加载插件

        // 1. 初始化插件管理器（在任何其他初始化之前）
        var pluginManager = new PluginManager(VisionLogger.Instance);
        string pluginsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        pluginManager.LoadPlugins(pluginsPath);

        // 2. 标记插件已加载（全局状态）
        PluginLoader.MarkAsLoaded();

        // 2.5 初始化视觉图像加载入口
        // HalImage 及其静态工厂方法已内置在 Plugin.SDK.Imaging 中
        // 使用 VisionImage.FromFile / VisionImage.FromBitmapSource 直接加载图像

        // 3. 初始化服务（包括节点显示适配器）
        ServiceInitializer.InitializeServices();

        // 3.5 Phase 2: 注册工作流节点类型转换器
        // 确保反序列化时 WorkflowNodeBase 自动转换为 WorkflowNode 实例
        WorkflowNodeFactory.TypeConverter = baseNode =>
            baseNode is Models.WorkflowNode ? baseNode : Models.WorkflowNode.FromBase(baseNode);

        // 4. 使用默认路径
        var defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        // 5. 初始化解决方案管理器
        ServiceInitializer.InitializeSolutionManager(defaultPath);

        // 5.5 注册全局变量选择事件处理（所有 ParamBinding 实例）
        EventManager.RegisterClassHandler(typeof(ParamBinding),
            ParamBinding.GlobalVariableSelectionRequestedEvent,
            new RoutedEventHandler(OnGlobalVariableSelectionRequested));

        // 5.6 订阅 ConfigSetting 全局变量解绑事件（清理 ConsumerBinding + 刷新 UI）
        ConfigSetting.BeforeGlobalVariableUnbinding += OnConfigSettingBeforeGlobalVariableUnbinding;

        // 5.7 订阅全局变量重命名事件（更新已打开的 ConfigSetting 绑定显示）
        var eventBus = ServiceLocator.Instance.GetService<SunEyeVision.Core.Events.IEventBus>();
        if (eventBus != null)
        {
            var renameHandler = new SunEyeVision.Core.Events.EventHandler<UIEvents.GlobalVariableRenamedEvent>(OnGlobalVariableRenamed);
            eventBus.Subscribe(renameHandler);
        }

        // 初始化DispatcherTimer监控
        InitializeMonitoring();

        // 启动决策
        HandleStartupDecision();
    }

    /// <summary>
    /// 处理启动决策
    /// </summary>
    private void HandleStartupDecision()
    {
        var solutionManager = ServiceInitializer.SolutionManager;
        var startupDecisionService = new StartupDecisionService(solutionManager);
        var decision = startupDecisionService.GetStartupDecision();

        var mainWindow = new MainWindow();

        switch (decision)
        {
            case StartupDecision.SkipConfiguration:
                // 跳过配置，自动打开默认解决方案
                var logger = SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance;
                logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Info,
                    "启动决策: 跳过配置，准备加载默认解决方案", "App");
                
                try
                {
                    var defaultMetadata = solutionManager.GetDefaultSolutionMetadata();
                    if (defaultMetadata != null)
                    {
                        logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Info,
                            $"找到默认解决方案元数据: Id={defaultMetadata.Id}, Name={defaultMetadata.Name}, FilePath={defaultMetadata.FilePath}", "App");
                        
                        // 使用 LoadSolutionOnly 加载解决方案
                        logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Info,
                            "开始加载解决方案 (LoadSolutionOnly)...", "App");
                        
                        var solution = solutionManager.LoadSolutionOnly(defaultMetadata.FilePath);
                        
                        if (solution != null)
                        {
                            logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Success,
                                $"解决方案加载成功: Id={solution.Id}, Workflows={solution.Workflows.Count}, GlobalVariables={solution.GlobalVariables?.Count ?? 0}, Devices={solution.Devices?.Count ?? 0}", "App");
                            
                            // 调用 SetCurrentSolution 设置当前解决方案
                            // 这与配置对话框启动的流程保持一致
                            logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Info,
                                "开始设置当前解决方案 (SetCurrentSolution)...", "App");
                            
                            solutionManager.SetCurrentSolution(solution);
                            
                            logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Success,
                                $"当前解决方案设置完成: CurrentSolution.Id={solutionManager.CurrentSolution?.Id}, CurrentFilePath={solutionManager.CurrentFilePath}", "App");
                            
                            mainWindow.Show();
                            logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Success,
                                $"主窗口已显示，自动打开默认解决方案完成: {defaultMetadata.Name}", "App");
                        }
                        else
                        {
                            // 默认解决方案加载失败，记录警告并显示主界面
                            logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Error,
                                $"默认解决方案加载失败: {defaultMetadata.Name} (LoadSolutionOnly 返回 null)", "App");
                            mainWindow.Show();
                        }
                    }
                    else
                    {
                        // 没有设置默认解决方案，直接显示主界面
                        logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Warning,
                            "未找到默认解决方案元数据，将显示空白主界面", "App");
                        mainWindow.Show();
                    }
                }
                catch (Exception ex)
                {
                    logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Error,
                        $"自动打开默认解决方案异常: {ex.Message}\n堆栈: {ex.StackTrace}", "App", ex);
                    MessageBox.Show($"自动打开默认解决方案失败: {ex.Message}",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    mainWindow.Show();
                }
                break;

            case StartupDecision.ShowConfiguration:
            default:
                // 显示配置界面（无预选）
                ShowConfigurationDialog(mainWindow, null);
                break;
        }
    }

    /// <summary>
    /// 显示配置对话框
    /// </summary>
    private void ShowConfigurationDialog(MainWindow mainWindow, string? preselectSolutionId = null)
    {
        var logger = SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance;
        logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Info,
            "启动决策: 显示配置对话框", "App");
        
        var solutionManager = ServiceInitializer.SolutionManager;
        var configDialog = new SolutionConfigurationDialog(solutionManager, preselectSolutionId);

        var result = configDialog.ShowDialog();

        if (result == true && configDialog.IsLaunched && configDialog.LaunchResult != null)
        {
            // 用户点击启动，加载解决方案
            var solution = configDialog.LaunchResult;

            if (solution != null)
            {
                logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Info,
                    $"用户从配置对话框启动解决方案: Id={solution.Id}, Workflows={solution.Workflows.Count}, GlobalVariables={solution.GlobalVariables?.Count ?? 0}, Devices={solution.Devices?.Count ?? 0}", "App");
                
                try
                {
                    logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Info,
                        "开始设置当前解决方案 (SetCurrentSolution)...", "App");
                    
                    solutionManager.SetCurrentSolution(solution);
                    
                    logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Success,
                        $"当前解决方案设置完成: CurrentSolution.Id={solutionManager.CurrentSolution?.Id}, CurrentFilePath={solutionManager.CurrentFilePath}", "App");
                    
                    mainWindow.Show();
                    logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Success,
                        "主窗口已显示，从配置对话框启动完成", "App");
                }
                catch (Exception ex)
                {
                    logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Error,
                        $"设置当前解决方案失败: {ex.Message}\n堆栈: {ex.StackTrace}", "App", ex);
                    MessageBox.Show($"加载解决方案失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    mainWindow.Show();
                }
            }
            else
            {
                logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Warning,
                    "配置对话框返回的 LaunchResult 为 null", "App");
                mainWindow.Show();
            }
        }
        else
        {
            // 用户点击跳过或取消
            logger.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Info,
                "用户跳过或取消配置对话框", "App");
            mainWindow.Show();
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = e.ExceptionObject as Exception;

        if (ex?.InnerException != null)
        {
            if (ex.InnerException.InnerException != null)
            {
                // 第二层内部异常，不做处理
            }
        }

        // 记录到日志系统
        try
        {
            var logger = VisionLogger.Instance;
            logger.Log(LogLevel.Error, $"全局未处理异常: {ex?.Message}", "App", ex);
            logger.Log(LogLevel.Error, $"异常类型: {ex?.GetType().FullName}", "App");
            logger.Log(LogLevel.Error, $"堆栈跟踪:\n{ex?.StackTrace}", "App");
            if (ex?.InnerException != null)
            {
                logger.Log(LogLevel.Error, $"内部异常: {ex.InnerException.Message}", "App", ex.InnerException);
            }
        }
        catch { }

        MessageBox.Show($"应用程序发生未处理的异常:\n{ex?.Message}\n\n堆栈跟踪:\n{ex?.StackTrace}",
            "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // 记录到插件日志系统
        try
        {
            var logger = VisionLogger.Instance;
            logger.Log(LogLevel.Error, $"Dispatcher 未处理异常: {e.Exception.Message}", "App", e.Exception);
            logger.Log(LogLevel.Error, $"异常类型: {e.Exception.GetType().FullName}", "App");
            logger.Log(LogLevel.Error, $"堆栈跟踪: {e.Exception.StackTrace}", "App");
            if (e.Exception.InnerException != null)
            {
                logger.Log(LogLevel.Error, $"内部异常: {e.Exception.InnerException.Message}", "App", e.Exception.InnerException);
            }
        }
        catch { }

        e.Handled = true; // 防止应用程序崩溃
        MessageBox.Show($"UI 线程发生异常:\n{e.Exception.Message}\n\n堆栈跟踪:\n{e.Exception.StackTrace}",
            "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>
    /// FirstChanceException处理 - 在异常被抛出但未被捕获时触发
    /// 用于捕获并标记RPC异常，避免调试器中断
    /// </summary>
    private void OnFirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        // 只捕获RPC服务器不可用异常，标记为已知的非致命异常
        if (e.Exception is COMException comEx)
        {
            if (comEx.ErrorCode == HRESULT_RPC_UNAVAILABLE || (comEx.ErrorCode & 0xFFFF) == RPC_S_SERVER_UNAVAILABLE)
            {
                // 标记为已知的非致命RPC异常，异常会被解码器的降级逻辑处理
                // 不记录日志，避免性能问题
            }
        }
        // 捕获WIC相关的内部异常
        else if (e.Exception is System.Reflection.TargetInvocationException tie &&
                 tie.InnerException is COMException innerComEx)
        {
            if (innerComEx.ErrorCode == HRESULT_RPC_UNAVAILABLE || (innerComEx.ErrorCode & 0xFFFF) == RPC_S_SERVER_UNAVAILABLE)
            {
                // 标记为已知的非致命RPC内部异常
                // 不记录日志，避免性能问题
            }
        }
    }

    /// <summary>
    /// P0优化：预热线程池 - 消除首次Task.Run的冷启动延迟
    /// 预期效果：首张缩略图加载从1783ms降至~50ms
    /// </summary>
    private void PrewarmThreadPool()
    {
        try
        {
            // 设置最小线程数，确保线程池立即可用
            ThreadPool.GetMinThreads(out int minWorker, out int minIO);
            ThreadPool.SetMinThreads(
                Math.Max(minWorker, 8),  // 至少8个工作线程
                Math.Max(minIO, 4)       // 至少4个IO线程
            );

            // 预热：启动几个空任务，强制线程池创建线程
            var warmupTasks = new Task[4];
            for (int i = 0; i < 4; i++)
            {
                warmupTasks[i] = Task.Run(() => Thread.Sleep(10));
            }
            Task.WaitAll(warmupTasks);
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Warning, $"线程池预热失败: {ex.Message}", "App");
        }
    }

    /// <summary>
    /// 注册文件关联
    /// </summary>
    private void RegisterFileAssociation()
    {
        try
        {
            var fileAssociationService = new FileAssociationService();
            if (!fileAssociationService.IsRegistered())
            {
                fileAssociationService.RegisterFileAssociation();
            }
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Warning, $"文件关联注册失败: {ex.Message}", "App");
        }
    }

    /// <summary>
    /// 初始化监控
    /// </summary>
    private void InitializeMonitoring()
    {
        // 监控功能已禁用，避免性能问题
        // 如需调试，可手动启用
    }

    /// <summary>
    /// 应用程序退出时清理
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        // 保存用户设置
        var solutionManager = ServiceInitializer.SolutionManager;
        solutionManager?.SaveUserSettings();

        // 停止监控定时器
        _monitorTimer?.Stop();

        base.OnExit(e);
    }

    /// <summary>
    /// 全局变量选择请求处理 — 打开选择对话框并完成绑定
    /// </summary>
    private static void OnGlobalVariableSelectionRequested(object sender, RoutedEventArgs e)
    {
        if (sender is not ParamBinding paramBinding)
        {
            VisionLogger.Instance.Log(LogLevel.Warning,
                "[监控] OnGlobalVariableSelectionRequested: sender 不是 ParamBinding",
                "App-Monitor");
            return;
        }

        var solutionManager = ServiceInitializer.SolutionManager;
        if (solutionManager == null)
        {
            VisionLogger.Instance.Log(LogLevel.Warning,
                "[监控] OnGlobalVariableSelectionRequested: SolutionManager 为 null",
                "App-Monitor");
            return;
        }

        // ★ 监控日志：拦截入口
        var logger = VisionLogger.Instance;
        logger.Log(LogLevel.Info,
            $"[监控] 进入 OnGlobalVariableSelectionRequested | " +
            $"sender={sender.GetType().Name} | " +
            $"ParameterName={paramBinding.ParameterName} | " +
            $"DataType={paramBinding.DataType?.Name ?? "null"}",
            "App-Monitor");

        // ★ 使用 GlobalVariableTypeMapper 将 DataType 映射为 GlobalVariable 类型字符串，用于选择器过滤
        var filterType = GlobalVariableTypeMapper.ToGlobalVarType(paramBinding.DataType);

        // ★ 监控日志：类型映射结果
        logger.Log(LogLevel.Info,
            $"[监控] 类型映射 | DataType={paramBinding.DataType?.Name ?? "null"} → filterType={filterType ?? "null"}",
            "App-Monitor");

        // ★ 无兜底原则：DataType 已设置但无法映射到任何 GlobalVariable 类型 → 直接报错
        if (filterType == null && paramBinding.DataType != null)
        {
            MessageBox.Show(
                $"参数类型 \"{paramBinding.DataType.Name}\" 不是全局变量支持的通用类型。\n" +
                $"\n请将工具输出适配为通用类型后再绑定全局变量。\n" +
                $"支持的通用类型：Integer, Float, String, Boolean, Double, Point, Rectangle, Size, Image 等。",
                "不支持的参数类型", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
            return;
        }

        // 打开全局变量选择对话框（按数据类型过滤）
        var selectedVariable = GlobalVariableSelectDialog.ShowSelect(solutionManager, filterType);

        // ★ 监控日志：变量选择结果
        logger.Log(selectedVariable != null ? LogLevel.Success : LogLevel.Info,
            $"[监控] 全局变量选择结果 | " +
            $"selected={(selectedVariable != null ? $"Name={selectedVariable.Name}, Type={selectedVariable.Type}, Id={selectedVariable.Id}" : "null")}",
            "App-Monitor");

        if (selectedVariable != null)
        {
            paramBinding.CompleteGlobalVariableBinding(selectedVariable.Name);

            // ★ 监控日志：开始记录 ConsumerBinding
            logger.Log(LogLevel.Info,
                $"[监控] 即将调用 RecordConsumerBinding | variableName={selectedVariable.Name}",
                "App-Monitor");

            // ★ Phase 5: 自动记录 ConsumerBinding
            RecordConsumerBinding(paramBinding, selectedVariable, solutionManager);
        }

        // 标记事件已处理
        e.Handled = true;
    }

    /// <summary>
    /// 处理 ConfigSetting 解绑全局变量 — 清理 ConsumerBinding 并刷新 UI
    /// </summary>
    private static void OnConfigSettingBeforeGlobalVariableUnbinding(ConfigSetting configSetting, string variableName)
    {
        try
        {
            var solutionManager = ServiceInitializer.SolutionManager;
            if (solutionManager?.CurrentSolution == null)
                return;

            var variable = solutionManager.CurrentSolution.GlobalVariables
                .FirstOrDefault(v => v.Name == variableName);
            if (variable == null)
                return;

            // 从视觉树向上查找最近的 ToolDebugControlBase，获取节点信息
            var toolControl = FindVisualParent<ToolDebugControlBase>(configSetting);
            string? nodeId = null;
            string? paramName = configSetting.ParameterName;

            if (toolControl?.CurrentNode is { } node)
            {
                nodeId = node.GetType().GetProperty("Id")?.GetValue(node) as string;
            }

            if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(paramName))
                return;

            // 查找并移除匹配的 ConsumerBinding
            var removed = variable.ConsumerBindings.RemoveAll(c =>
                c.NodeId == nodeId && c.ParameterName == paramName);

            if (removed > 0)
            {
                variable.NotifyOutputDisplayChanged();
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[监控] 清理 ConsumerBinding 成功 | " +
                    $"variable={variableName} | nodeId={nodeId} | param={paramName}",
                    "App-Monitor");
            }
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"[监控] 清理 ConsumerBinding 异常 | {ex.Message}",
                "App-Monitor", ex);
        }
    }

    /// <summary>
    /// 全局变量重命名事件处理 — 通过 ConfigSetting 静态通知机制更新所有已打开实例的绑定显示
    /// </summary>
    private static void OnGlobalVariableRenamed(UIEvents.GlobalVariableRenamedEvent evt)
    {
        VisionLogger.Instance.Log(LogLevel.Info,
            $"[监控] 全局变量重命名事件进入 | old='{evt.OldName}' → new='{evt.NewName}' | source={evt.Source}",
            "App-Monitor");
        try
        {
            ConfigSetting.NotifyAllGlobalVariableRenamed(evt.OldName, evt.NewName);

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[监控] 全局变量重命名 ConfigSetting 通知完成 | old='{evt.OldName}' → new='{evt.NewName}'",
                "App-Monitor");
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"[监控] 全局变量重命名更新异常 | {ex.Message}",
                "App-Monitor", ex);
        }
    }

    /// <summary>
    /// 记录消费者绑定：参数绑定到全局变量时自动追加 ConsumerBinding 记录
    /// </summary>
    private static void RecordConsumerBinding(ParamBinding paramBinding, GlobalVariable variable, SolutionManager solutionManager)
    {
        try
        {
            var logger = VisionLogger.Instance;

            // ★ 从视觉树向上查找最近的 ToolDebugControlBase，获取节点 ID 和名称
            // 使用动态解析而非预设置，避免因 TabItem 懒加载导致 ParamBinding 创建时未设置 NodeId
            string? nodeId = null;
            string? nodeName = null;

            var toolControl = FindVisualParent<ToolDebugControlBase>(paramBinding);

            // ★ 监控日志1：FindVisualParent 结果
            logger.Log(LogLevel.Info,
                $"[监控] FindVisualParent<ToolDebugControlBase> | " +
                $"found={(toolControl != null)} | " +
                $"controlType={toolControl?.GetType().FullName ?? "null"}",
                "App-Monitor");

            if (toolControl?.CurrentNode is { } node)
            {
                // ★ 监控日志2：CurrentNode 存在
                logger.Log(LogLevel.Info,
                    $"[监控] CurrentNode | " +
                    $"nodeType={node.GetType().FullName}",
                    "App-Monitor");

                nodeId = node.GetType().GetProperty("Id")?.GetValue(node) as string;
                nodeName = node.GetType().GetProperty("DispName")?.GetValue(node) as string
                        ?? node.GetType().GetProperty("Name")?.GetValue(node) as string;

                // ★ 监控日志3：反射提取结果
                logger.Log(LogLevel.Info,
                    $"[监控] 节点信息反射提取 | " +
                    $"Id={nodeId ?? "null"} | " +
                    $"DispName/Name={nodeName ?? "null"}",
                    "App-Monitor");
            }
            else
            {
                // ★ 监控日志4：CurrentNode 为 null 或 toolControl 为 null
                logger.Log(LogLevel.Warning,
                    $"[监控] ★★★ CurrentNode 不可用 ★★★ | " +
                    $"toolControl null={(toolControl == null)} | " +
                    $"CurrentNode null={(toolControl?.CurrentNode == null)}",
                    "App-Monitor");
            }

            if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(nodeName))
            {
                logger.Log(LogLevel.Warning,
                    $"[监控] ★★★ 节点信息为空，ConsumerBinding 终止 ★★★ | " +
                    $"nodeId={nodeId ?? "null"} | " +
                    $"nodeName={nodeName ?? "null"} | " +
                    $"toolControl found={(toolControl != null)} | " +
                    $"CurrentNode null={(toolControl?.CurrentNode == null)}",
                    "App-Monitor");
                return;
            }

            var paramName = paramBinding.ParameterName;
            // ★ 监控日志5：参数名
            logger.Log(LogLevel.Info,
                $"[监控] ParameterName={paramName ?? "null"}",
                "App-Monitor");

            if (string.IsNullOrEmpty(paramName))
            {
                logger.Log(LogLevel.Warning,
                    "[监控] ★★★ ParameterName 为空，ConsumerBinding 终止 ★★★",
                    "App-Monitor");
                return;
            }

            // ★ 查找节点所属工作流名称和参数显示名称
            string? workflowName = null;
            string? parameterDisplayName = null;

            var solution = solutionManager.CurrentSolution;
            if (solution != null)
            {
                foreach (var wf in solution.Workflows)
                {
                    var workflowNode = wf.Nodes?.FirstOrDefault(n => n.Id == nodeId);
                    if (workflowNode != null)
                    {
                        workflowName = wf.Name ?? wf.Id;

                        // 从参数的 [DisplayName] 属性获取中文显示名
                        if (workflowNode.Parameters != null)
                        {
                            var prop = workflowNode.Parameters.GetType().GetProperty(paramName);
                            if (prop != null)
                            {
                                var attr = prop.GetCustomAttribute<DisplayNameAttribute>(false);
                                parameterDisplayName = attr?.DisplayName ?? paramName;
                            }
                        }
                        break;
                    }
                }
            }

            // ★ 监控日志6：工作流查找结果
            logger.Log(LogLevel.Info,
                $"[监控] 工作流查找 | " +
                $"solution null={(solution == null)} | " +
                $"workflowCount={solution?.Workflows?.Count ?? 0} | " +
                $"found={(workflowName != null)} | " +
                $"workflowName={workflowName ?? "null"} | " +
                $"parameterDisplayName={parameterDisplayName ?? "null"}",
                "App-Monitor");

            // 检查是否已存在相同的消费者绑定
            var existing = variable.ConsumerBindings
                .FirstOrDefault(c => c.NodeId == nodeId && c.ParameterName == paramName);

            if (existing != null)
            {
                // 已存在则更新节点名称、工作流名称、参数显示名称
                existing.NodeName = nodeName;
                if (workflowName != null) existing.WorkflowName = workflowName;
                if (parameterDisplayName != null) existing.ParameterDisplayName = parameterDisplayName;
            }
            else
            {
                // 追加新的消费者绑定（含 WorkflowName + ParameterDisplayName）
                variable.ConsumerBindings.Add(new ConsumerBinding
                {
                    NodeId = nodeId,
                    NodeName = nodeName,
                    WorkflowName = workflowName ?? "",
                    ParameterName = paramName,
                    ParameterDisplayName = parameterDisplayName ?? paramName
                });
            }

            // ★ 监控日志7：ConsumerBinding 操作结果
            logger.Log(existing != null ? LogLevel.Info : LogLevel.Success,
                $"[监控] ConsumerBinding {(existing != null ? "更新" : "添加")}成功 | " +
                $"consumerCount={variable.ConsumerBindings.Count} | " +
                $"DisplayPath={variable.OutputTargetDisplayText}",
                "App-Monitor");

            // ★ 通知 UI 刷新"目标输出"列
            variable.NotifyOutputDisplayChanged();

            // ★ 监控日志8：UI 刷新通知完成
            logger.Log(LogLevel.Info,
                $"[监控] NotifyOutputDisplayChanged 已调用 | " +
                $"OutputTargetDisplayText 新值={variable.OutputTargetDisplayText}",
                "App-Monitor");

            // 持久化保存
            try
            {
                if (solutionManager.CurrentSolution?.FilePath != null)
                    solutionManager.SaveSolution(solutionManager.CurrentSolution.FilePath);
            }
            catch { /* 静默忽略保存失败，不影响用户体验 */ }
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"[监控] ★★★ RecordConsumerBinding 异常 ★★★ | {ex.Message} | " +
                $"StackTrace: {ex.StackTrace}",
                "App-Monitor", ex);
        }
    }

    /// <summary>
    /// 在视觉树中向上查找指定类型的父控件
    /// </summary>
    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null)
            return null;

        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T typedParent)
                return typedParent;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

}


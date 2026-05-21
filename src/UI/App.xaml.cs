using System;
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
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Monitoring;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.VisionAdapters;

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
        var pluginManager = new PluginManager();
        string pluginsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        pluginManager.LoadPlugins(pluginsPath);

        // 2. 标记插件已加载（全局状态）
        PluginLoader.MarkAsLoaded();

        // 2.5 初始化视觉图像加载入口
        // HalImage 及其静态工厂方法已内置在 Plugin.SDK.Imaging 中
        // 使用 VisionImage.FromFile / VisionImage.FromBitmapSource 直接加载图像

        // 3. 初始化服务（包括节点显示适配器）
        ServiceInitializer.InitializeServices();

        // 4. 使用默认路径
        var defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        // 5. 初始化解决方案管理器
        ServiceInitializer.InitializeSolutionManager(defaultPath);

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
}


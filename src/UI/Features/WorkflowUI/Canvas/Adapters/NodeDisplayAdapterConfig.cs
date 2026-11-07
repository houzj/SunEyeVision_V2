using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SunEyeVision.Foundation;
using SunEyeVision.Foundation.Logging;
using SunEyeVision.UI.Services.Logging;
using SunEyeVision.Infrastructure.Persistence.ApplicationData;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Services.Communication;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 节点显示适配器配置类
    /// 用于注册和初始化所有节点显示适配器。
    /// </summary>
    public static class NodeDisplayAdapterConfig
    {
        private static bool _isInitialized = false;

    /// <summary>
    /// 初始化所有节点显示适配器。
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        // 注册图像源节点适配器。
        NodeDisplayAdapterFactory.RegisterAdapter("ImageSource", new ImageSourceNodeDisplayAdapter());

        // 注册视频源节点适配器。
        NodeDisplayAdapterFactory.RegisterAdapter("VideoSource", new VideoSourceNodeDisplayAdapter());

        // 注册多集合节点适配器。
        NodeDisplayAdapterFactory.RegisterAdapter("MultiCollection", new MultiCollectionNodeDisplayAdapter());

        // 注册处理节点适配器。
        NodeDisplayAdapterFactory.RegisterAdapter("Processing", new ProcessingNodeDisplayAdapter());

        // 注册AI分析节点适配器。
        NodeDisplayAdapterFactory.RegisterAdapter("AIAnalysis", new AIAnalysisNodeDisplayAdapter());

        // 注册控制流节点适配器（子程序、条件判断等）。
        NodeDisplayAdapterFactory.RegisterAdapter("Subroutine", new ControlFlowNodeDisplayAdapter());
        NodeDisplayAdapterFactory.RegisterAdapter("Condition", new ControlFlowNodeDisplayAdapter());

        // 可以继续添加其他类型的适配器...

        _isInitialized = true;
    }

        /// <summary>
        /// 重置适配器配置。
        /// </summary>
        public static void Reset()
        {
            NodeDisplayAdapterFactory.Clear();
            _isInitialized = false;
        }
    }

    /// <summary>
    /// 服务初始化扩展方法类。
    /// </summary>
    public static class ServiceInitializer
    {
        private static UILogWriter? _uiLogWriter;
        private static SunEyeVision.Application.DocumentSessionManager? _solutionManager;
        private static ApplicationDataPaths? _dataPaths;
        public static string DocumentFilesDirectory=>_dataPaths?.SolutionsDirectory??throw new InvalidOperationException("应用数据目录尚未初始化。");
        private static FileLogWriter? _fileLogWriter;
        public static void ConfigureDataPaths(ApplicationDataPaths paths) => _dataPaths = paths;

        public static void InitializeSolutionManager(ApplicationDataPaths paths,SunEyeVision.Plugin.Runtime.RuntimeDirectorySnapshot directory)
        {
            if (_solutionManager != null) return;
            _dataPaths = paths;
            _solutionManager = new SunEyeVision.Application.DocumentSessionManager(directory);
        }
        private static CameraRuntimeComposition? _cameraRuntime;
        private static CommunicationRuntimeComposition? _communicationRuntime;
        private static CommunicationApplicationService? _communicationService;
        private static IDisposable? _communicationActivity;
        private static bool _communicationClosing;
        private static readonly SemaphoreSlim CommunicationDisposeGate = new(1, 1);
        private static readonly object CommunicationInitializationGate = new();

        /// <summary>
        /// 获取 UI 日志写入器
        /// </summary>
        /// <remarks>
        /// 支持延迟自动创建：如果尚未通过 InitializeServices() 初始化，
        /// 首次访问时会自动创建实例并注册到 VisionLogger，确保任何时序访问都安全。
        /// </remarks>
        public static UILogWriter UILogWriter
        {
            get
            {
                if (_uiLogWriter == null)
                {
                    InitializeUILogWriter();
                }
                return _uiLogWriter!;
            }
        }

        /// <summary>
        /// 获取解决方案管理器
        /// </summary>
        public static SunEyeVision.Application.DocumentSessionManager SolutionManager => _solutionManager ?? throw new InvalidOperationException("方案会话管理器未初始化，请先调用 InitializeSolutionManager()");

        public static CameraRuntimeComposition CameraRuntime
            => _cameraRuntime ??= new CameraRuntimeComposition(diagnostic => VisionLogger.Instance.Log(diagnostic.Level switch { SunEyeVision.DeviceDriver.Cameras.Contracts.CameraDiagnosticLevel.Error => LogLevel.Error, SunEyeVision.DeviceDriver.Cameras.Contracts.CameraDiagnosticLevel.Warning => LogLevel.Warning, _ => LogLevel.Info }, $"[{diagnostic.DeviceId}][{diagnostic.CorrelationId}] {diagnostic.Message}{(diagnostic.Error is null ? string.Empty : Environment.NewLine + diagnostic.Error)}", "CameraRuntime"));

        public static CommunicationRuntimeComposition CommunicationRuntime
        {
            get
            {
                lock (CommunicationInitializationGate)
                {
                    if (_communicationClosing) throw new InvalidOperationException("通讯运行时正在关闭或上次关闭未完成。");
                    var session = SolutionManager.CurrentSession ?? throw new InvalidOperationException("当前没有方案会话，不能启动通讯运行时。");
                    // 即使构造失败也保留租约，直到统一异步关闭链完成排空。
                    _communicationActivity ??= session.AcquireActivityLease("通讯服务及驱动");
                    return _communicationRuntime ??= new CommunicationRuntimeComposition();
                }
            }
        }

        public static CommunicationApplicationService CommunicationService
        {
            get
            {
                lock (CommunicationInitializationGate)
                {
                    if (_communicationClosing) throw new InvalidOperationException("通讯服务正在关闭或上次关闭未完成。");
                    if (_communicationService == null)
                    {
                        var session = SolutionManager.CurrentSession ?? throw new InvalidOperationException("当前没有方案会话，不能启动通讯服务。");
                        _communicationService = new CommunicationApplicationService(session, CommunicationRuntime);
                        _communicationService.Automation.CameraConnectionObserver = id =>
                            _cameraRuntime != null && _cameraRuntime.Runtime.TryGetSession(
                                new SunEyeVision.Plugin.Contracts.Acquisition.CameraDeviceRef(id), out var session)
                            && session.Adapter.State is SunEyeVision.Plugin.Contracts.Acquisition.CameraState.Connected
                                or SunEyeVision.Plugin.Contracts.Acquisition.CameraState.Capturing;
                    }
                    return _communicationService;
                }
            }
        }

        public static async ValueTask DisposeCommunicationRuntimeAsync()
        {
            await CommunicationDisposeGate.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (CommunicationInitializationGate) _communicationClosing = true;
                if (_communicationService is not null)
                {
                    await _communicationService.DisposeAsync().ConfigureAwait(false);
                    _communicationService = null;
                }
                if (_communicationRuntime is not null)
                {
                    await _communicationRuntime.DisposeAsync().ConfigureAwait(false);
                    _communicationRuntime = null;
                }
                _communicationActivity?.Dispose();
                _communicationActivity = null;
                lock (CommunicationInitializationGate) _communicationClosing = false;
            }
            finally { CommunicationDisposeGate.Release(); }
        }

        public static async ValueTask DisposeCameraRuntimeAsync()
        {
            if (_cameraRuntime is null)
                return;
            await _cameraRuntime.DisposeAsync().ConfigureAwait(false);
            _cameraRuntime = null;
        }

        /// <summary>
        /// 初始化所有服务。
        /// </summary>
        private static readonly object ServicesInitializationGate = new();
        private static bool _servicesInitialized;
        public static void InitializeServices()
        {
            lock (ServicesInitializationGate)
            {
                if (_servicesInitialized) return;
                // Application services have one lifetime. Re-entering from an editor must
                // not replace a log writer while a background batch awaits the UI owner.
                NodeDisplayAdapterConfig.Initialize();
                InitializePluginLogger();
                _servicesInitialized = true;
            }
        }

        /// <summary>
        /// 初始化解决方案管理器
        /// </summary>
        /// <param name="solutionsDirectory">解决方案目录，默认为solutions/</param>


        /// <summary>
        /// 初始化 UILogWriter（延迟自动创建入口）
        /// </summary>
        /// <remarks>
        /// 可在两种场景下被调用：
        /// 1. InitializePluginLogger() 中预先创建（运行时正常路径）
        /// 2. UILogWriter 属性首次访问时延迟创建（设计时或特殊时序）
        /// 使用锁确保线程安全，仅创建一次。
        /// </remarks>
        private static readonly object _uiLogWriterLock = new();
        private static void InitializeUILogWriter()
        {
            lock (_uiLogWriterLock)
            {
                if (_uiLogWriter != null)
                    return;

                var logger = VisionLogger.Instance;
                _uiLogWriter = new UILogWriter();
                logger.AddWriter(_uiLogWriter);
            }
        }

        /// <summary>
        /// 初始化插件日志器
        /// </summary>
        private static void InitializePluginLogger()
        {
            var logger = VisionLogger.Instance;

            // 添加默认日志写入器（确保日志从一开始就有输出）
            // 1. 文件写入器 - 持久化到应用数据根下的 Logs 目录（双轨制：UI显示 + 文件落盘）
            var logRoot = (_dataPaths ?? throw new InvalidOperationException("应用数据目录尚未初始化。")).LogDirectory;
            // 写入器必须先注册：VisionLogger.Log() 只入队，后台批量取出后按当前写入器分发，
            // 写入器为 0 时整批直接丢弃。若先做保留清理再注册，清理失败的 Warning 会随机丢失。
            _fileLogWriter = new FileLogWriter(logRoot);
            logger.AddWriter(_fileLogWriter);
            if (Directory.Exists(logRoot))
            {
                var files = Directory.GetFiles(logRoot, "*.txt", SearchOption.AllDirectories).Select(path => new FileInfo(path)).OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
                long retained = 0;
                foreach (var file in files)
                {
                    retained += file.Length;
                    if (file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-30) || retained > 256L * 1024 * 1024)
                        try { file.Delete(); }
                        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
                        { logger.Log(LogLevel.Warning, $"旧日志文件无法删除，已保留：{file.FullName}（{failure.Message}）", "ServiceInitializer"); }
                }
            }

            // 2. UI写入器 - 通过延迟初始化方法创建，确保全局共享唯一实例
            InitializeUILogWriter();

            // 设置 PluginLogger 的提供者
            Foundation.Logging.PluginLogger.SetProvider(logger);

            // 日志改走 VM 同款范式（ILogService + log4net）。过渡期与 FileLogWriter 双写，
            // 待自研队列与 Writer 体系下线后移除双写（见架构基线方案 R1）。
            ServiceHost.Build(services =>
            {
                services.AddSingleton(Options.Create(new LogOptions
                {
                    Directory = logRoot,
                    MinimumLevel = "Info"
                }));
                services.AddSingleton<ILogService, Log4netLogService>();
            });
            VisionLogger.AttachService(ServiceHost.GetRequired<ILogService>());

            // 测试日志
            logger.Info("日志系统初始化完成", "ServiceInitializer");
            // 数据根来源必须如实落盘：配置缺失走缺省根时同样可见。
            logger.Info($"应用数据根：{_dataPaths.Root}（来源：{_dataPaths.RootSource}）", "ServiceInitializer");
        }

        /// <summary>
        /// 启动期自查一次文件日志写入失败：日志系统已进入稳定态，若磁盘写入已经在失败，就在这里记一条
        /// Warning（累计次数 + 最近原因），让「日志写不进磁盘」在日志自身与 UI 日志面板都看得见。
        /// 正常启动写入失败次数为 0 时不写任何内容，避免每次启动都刷噪音。
        /// </summary>
        public static void ReportFileLogWriteFailure()
        {
            var writer = _fileLogWriter ?? throw new InvalidOperationException("日志系统尚未初始化。");
            var logger = VisionLogger.Instance;

            // 后台队列是异步落盘的：先催一次刷新并等它排空（300ms 上限），否则读到的可能是还没更新过的计数。
            // 等待放在线程池上，避免撞上 UI 同步上下文造成死锁。
            Task.Run(() => logger.FlushAsync()).GetAwaiter().GetResult();
            var deadline = Environment.TickCount64 + 300;
            while (logger.QueueSize > 0 && Environment.TickCount64 < deadline)
                Thread.Sleep(5);

            if (writer.WriteFailureCount <= 0)
                return;

            logger.Warning(
                $"日志文件写入已失败 {writer.WriteFailureCount} 次，最近一次原因：{writer.LastWriteFailure?.Message ?? "未知"}；磁盘上的日志可能不完整。",
                "ServiceInitializer");
        }

        /// <summary>
        /// 从程序运行目录逐级向上查找包含 docs/ 子目录的祖先目录，即项目根。
        /// 使日志根目录 logs/ 与 docs/ 平级。找不到则返回 null（由调用方兜底到运行目录）。
        /// </summary>
        private static string? ResolveProjectRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "docs")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
